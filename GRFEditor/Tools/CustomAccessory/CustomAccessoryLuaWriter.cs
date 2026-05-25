using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Utilities.Services;

namespace GRFEditor.Tools.CustomAccessory {
	public static class CustomAccessoryLuaWriter {
		public static void ApplyEntries(string accessoryIdPath, string accnamePath, IEnumerable<CustomAccessoryEntry> entries) {
			var list = entries.Where(p => p != null && !string.IsNullOrWhiteSpace(p.ConstantName)).ToList();
			if (list.Count == 0)
				return;

			if (!File.Exists(accessoryIdPath))
				throw new FileNotFoundException(
					"accessoryid.lub não encontrado em:\n" + accessoryIdPath,
					accessoryIdPath);

			if (!File.Exists(accnamePath))
				throw new FileNotFoundException(
					"accname.lub não encontrado em:\n" + accnamePath,
					accnamePath);

			foreach (var entry in list)
				NormalizeEntryNames(entry);

			string accessoryContent;
			string accnameContent;

			try {
				accessoryContent = ReadText(accessoryIdPath);
			}
			catch (Exception ex) {
				throw new IOException("Falha ao ler accessoryid.lub:\n" + accessoryIdPath + "\n" + ex.Message, ex);
			}

			try {
				accnameContent = ReadText(accnamePath);
			}
			catch (Exception ex) {
				throw new IOException("Falha ao ler accname.lub:\n" + accnamePath + "\n" + ex.Message, ex);
			}

			foreach (var entry in list) {
				if (entry.ShouldWriteAccessoryId)
					accessoryContent = UpsertAccessoryIdLine(accessoryContent, entry.ConstantName, entry.ViewId);

				if (entry.ShouldWriteAccname)
					accnameContent = UpsertAccnameLine(accnameContent, entry.ConstantName, entry.DisplayName);
			}

			try {
				WriteText(accessoryIdPath, accessoryContent);
			}
			catch (Exception ex) {
				throw new IOException("Falha ao gravar accessoryid.lub:\n" + accessoryIdPath + "\n" + ex.Message, ex);
			}

			try {
				WriteText(accnamePath, accnameContent);
			}
			catch (Exception ex) {
				throw new IOException("Falha ao gravar accname.lub:\n" + accnamePath + "\n" + ex.Message, ex);
			}
		}

		public static void ValidateWrittenEntries(string accessoryIdPath, string accnamePath, IEnumerable<CustomAccessoryEntry> entries) {
			var list = entries.Where(p => p != null && p.Selected && !string.IsNullOrWhiteSpace(p.ConstantName)).ToList();
			if (list.Count == 0)
				return;

			foreach (var entry in list)
				NormalizeEntryNames(entry);

			if (!File.Exists(accessoryIdPath) || !File.Exists(accnamePath))
				throw new FileNotFoundException("Arquivos .lub não encontrados após gravação.");

			var accessoryContent = ReadText(accessoryIdPath);
			var accnameContent = ReadText(accnamePath);

			if (string.IsNullOrEmpty(accessoryContent))
				throw new InvalidOperationException("accessoryid.lub ficou vazio após a gravação: " + accessoryIdPath);

			if (string.IsNullOrEmpty(accnameContent))
				throw new InvalidOperationException("accname.lub ficou vazio após a gravação: " + accnamePath);

			var failures = new List<string>();

			foreach (var entry in list) {
				if (entry.ShouldWriteAccessoryId
					&& !ContainsAccessoryIdEntry(accessoryContent, entry.ConstantName, entry.ViewId)) {
					failures.Add(BuildValidationFailure(
						"accessoryid.lub",
						accessoryIdPath,
						entry,
						"constante não encontrada no arquivo",
						accessoryContent));
				}

				if (entry.ShouldWriteAccname
					&& !ContainsAccnameEntry(accnameContent, entry.ConstantName, entry.DisplayName)) {
					failures.Add(BuildValidationFailure(
						"accname.lub",
						accnamePath,
						entry,
						"nome não encontrado no arquivo",
						accnameContent));
				}
			}

			if (failures.Count > 0) {
				throw new InvalidOperationException(
					"Validação dos arquivos .lub falhou após gravar:" + Environment.NewLine + Environment.NewLine
					+ string.Join(Environment.NewLine + Environment.NewLine, failures));
			}
		}

		public static string PreviewAccessoryIdContent(string content, IEnumerable<CustomAccessoryEntry> entries) {
			var result = content ?? "";
			foreach (var entry in entries.Where(p => p != null && p.ShouldWriteAccessoryId && !string.IsNullOrWhiteSpace(p.ConstantName)))
				result = UpsertAccessoryIdLine(result, entry.ConstantName, entry.ViewId);

			return result;
		}

		public static string PreviewAccnameContent(string content, IEnumerable<CustomAccessoryEntry> entries) {
			var result = content ?? "";
			foreach (var entry in entries.Where(p => p != null && p.ShouldWriteAccname && !string.IsNullOrWhiteSpace(p.ConstantName)))
				result = UpsertAccnameLine(result, entry.ConstantName, entry.DisplayName);

			return result;
		}

		public static string BuildLineDiffPreview(string label, string before, string after) {
			var beforeLines = SplitLines(before ?? "");
			var afterLines = SplitLines(after ?? "");
			var sb = new StringBuilder();
			sb.AppendLine("=== " + label + " ===");

			int max = Math.Max(beforeLines.Length, afterLines.Length);
			bool anyChange = false;

			for (int i = 0; i < max; i++) {
				var oldLine = i < beforeLines.Length ? beforeLines[i] : null;
				var newLine = i < afterLines.Length ? afterLines[i] : null;

				if (string.Equals(oldLine, newLine, StringComparison.Ordinal))
					continue;

				anyChange = true;

				if (oldLine != null)
					sb.AppendLine("- " + oldLine);

				if (newLine != null)
					sb.AppendLine("+ " + newLine);
			}

			if (!anyChange)
				sb.AppendLine("(sem alterações de linha)");

			return sb.ToString();
		}

		public static void NormalizeEntryNames(CustomAccessoryEntry entry) {
			if (entry == null)
				return;

			entry.ConstantName = CustomAccessoryNaming.NormalizeConstantName(entry.ConstantName);
			entry.DisplayName = CustomAccessoryNaming.NormalizeDisplayName(entry.ConstantName, entry.DisplayName);
		}

		private static string BuildValidationFailure(string label, string path, CustomAccessoryEntry entry, string reason, string fileContent) {
			var lines = fileContent.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');
			var tail = string.Join(Environment.NewLine, lines.Skip(Math.Max(0, lines.Length - 20)));

			return label + Environment.NewLine
				+ "  Arquivo: " + path + Environment.NewLine
				+ "  ConstantName: " + entry.ConstantName + Environment.NewLine
				+ "  DisplayName: " + entry.DisplayName + Environment.NewLine
				+ "  ViewId: " + entry.ViewId + Environment.NewLine
				+ "  Motivo: " + reason + Environment.NewLine
				+ "  Últimas linhas:" + Environment.NewLine
				+ tail;
		}

		private static bool ContainsAccessoryIdEntry(string content, string constantName, int viewId) {
			foreach (var line in SplitLines(content)) {
				var match = CustomAccessoryLuaPatterns.AccessoryIdLineRegex.Match(line);
				if (!match.Success)
					continue;

				if (!string.Equals(match.Groups["name"].Value, constantName, StringComparison.OrdinalIgnoreCase))
					continue;

				int id;
				if (int.TryParse(match.Groups["id"].Value, out id) && id == viewId)
					return true;
			}

			return false;
		}

		private static bool ContainsAccnameEntry(string content, string constantName, string displayName) {
			var expected = CustomAccessoryNaming.NormalizeDisplayName(constantName, displayName);

			foreach (var line in SplitLines(content)) {
				var match = CustomAccessoryLuaPatterns.AccnameLineRegex.Match(line);
				if (!match.Success)
					continue;

				if (!string.Equals(match.Groups["name"].Value, constantName, StringComparison.OrdinalIgnoreCase))
					continue;

				var value = UnescapeLuaString(match.Groups["value"].Value);
				if (string.Equals(value, expected, StringComparison.OrdinalIgnoreCase))
					return true;
			}

			return false;
		}

		private static string UpsertAccessoryIdLine(string content, string constantName, int viewId) {
			var lines = SplitLines(content).ToList();
			var newLine = "\t" + constantName + " = " + viewId + ",";

			RemoveAccessoryIdLines(lines, constantName);
			lines = InsertBeforeTableClose(lines, newLine, "ACCESSORY_IDs", "-- CUSTOM");

			return string.Join(Environment.NewLine, lines);
		}

		private static string UpsertAccnameLine(string content, string constantName, string displayName) {
			var lines = SplitLines(content).ToList();
			var escaped = EscapeLuaString(CustomAccessoryNaming.NormalizeDisplayName(constantName, displayName));
			var newLine = "\t[ACCESSORY_IDs." + constantName + "] = \"" + escaped + "\",";

			RemoveAccnameLines(lines, constantName);
			lines = InsertBeforeTableClose(lines, newLine, "AccNameTable", "tbl", "-- CUSTOM");

			return string.Join(Environment.NewLine, lines);
		}

		private static void RemoveAccessoryIdLines(List<string> lines, string constantName) {
			for (int i = lines.Count - 1; i >= 0; i--) {
				var match = CustomAccessoryLuaPatterns.AccessoryIdLineRegex.Match(lines[i]);
				if (!match.Success)
					continue;

				if (string.Equals(match.Groups["name"].Value, constantName, StringComparison.OrdinalIgnoreCase))
					lines.RemoveAt(i);
			}
		}

		private static void RemoveAccnameLines(List<string> lines, string constantName) {
			for (int i = lines.Count - 1; i >= 0; i--) {
				var match = CustomAccessoryLuaPatterns.AccnameLineRegex.Match(lines[i]);
				if (!match.Success)
					continue;

				if (string.Equals(match.Groups["name"].Value, constantName, StringComparison.OrdinalIgnoreCase))
					lines.RemoveAt(i);
			}
		}

		private static List<string> InsertBeforeTableClose(List<string> lines, string newLine, params string[] tableMarkers) {
			int closeIndex = FindTableClosingBraceLine(lines, tableMarkers);
			if (closeIndex < 0)
				closeIndex = FindLastClosingBraceLine(lines);

			if (closeIndex < 0) {
				lines.Add(newLine);
				return lines;
			}

			lines.Insert(closeIndex, newLine);
			return lines;
		}

		private static int FindTableClosingBraceLine(List<string> lines, string[] tableMarkers) {
			for (int i = 0; i < lines.Count; i++) {
				var line = lines[i];
				bool isTableStart = false;

				foreach (var marker in tableMarkers) {
					if (string.IsNullOrEmpty(marker))
						continue;

					if (line.IndexOf(marker, StringComparison.OrdinalIgnoreCase) >= 0 && line.IndexOf('{') >= 0) {
						isTableStart = true;
						break;
					}
				}

				if (!isTableStart)
					continue;

				int depth = 0;
				for (int j = i; j < lines.Count; j++) {
					depth += CountChar(lines[j], '{');
					depth -= CountChar(lines[j], '}');

					if (j > i && depth <= 0)
						return j;
				}
			}

			return -1;
		}

		private static int FindLastClosingBraceLine(List<string> lines) {
			for (int i = lines.Count - 1; i >= 0; i--) {
				if (lines[i].Trim() == "}")
					return i;
			}

			return -1;
		}

		private static int CountChar(string line, char c) {
			int count = 0;
			foreach (var ch in line) {
				if (ch == c)
					count++;
			}

			return count;
		}

		private static string[] SplitLines(string content) {
			return content.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');
		}

		private static string EscapeLuaString(string value) {
			if (value == null)
				return "";

			return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
		}

		private static string UnescapeLuaString(string value) {
			if (value == null)
				return "";

			return value.Replace("\\\"", "\"").Replace("\\\\", "\\");
		}

		private static string ReadText(string path) {
			return EncodingService.DisplayEncoding.GetString(File.ReadAllBytes(path));
		}

		private static void WriteText(string path, string content) {
			File.WriteAllBytes(path, EncodingService.DisplayEncoding.GetBytes(content));
		}
	}
}
