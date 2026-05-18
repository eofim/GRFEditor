using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Utilities.Services;

namespace GRFEditor.Tools.CustomAccessory {
	public static class CustomAccessoryLuaWriter {
		private static readonly Regex AccessoryIdLineRegex = new Regex(
			@"^\s*(?<name>ACCESSORY_[A-Za-z0-9_]+)\s*=\s*(?<id>\d+)\s*,?\s*$",
			RegexOptions.Compiled);

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

			var accessoryContent = ReadText(accessoryIdPath);
			var accnameContent = ReadText(accnamePath);

			foreach (var entry in list) {
				accessoryContent = UpsertAccessoryIdLine(accessoryContent, entry.ConstantName, entry.ViewId);
				accnameContent = UpsertAccnameLine(accnameContent, entry.ConstantName, entry.DisplayName);
			}

			WriteText(accessoryIdPath, accessoryContent);
			WriteText(accnamePath, accnameContent);
		}

		private static string UpsertAccessoryIdLine(string content, string constantName, int viewId) {
			var lines = content.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n').ToList();
			var newLine = "\t" + constantName + " = " + viewId + ",";
			var replaced = false;

			for (int i = 0; i < lines.Count; i++) {
				var match = AccessoryIdLineRegex.Match(lines[i]);
				if (!match.Success)
					continue;

				if (string.Equals(match.Groups["name"].Value, constantName, StringComparison.OrdinalIgnoreCase)) {
					lines[i] = newLine;
					replaced = true;
					break;
				}
			}

			if (!replaced)
				lines = InsertBeforeClosingBrace(lines, newLine, "-- CUSTOM");

			return string.Join(Environment.NewLine, lines);
		}

		private static string UpsertAccnameLine(string content, string constantName, string displayName) {
			var lines = content.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n').ToList();
			var escaped = EscapeLuaString(displayName);
			var newLine = "\t[ACCESSORY_IDs." + constantName + "] = \"" + escaped + "\",";
			var replaced = false;

			var pattern = @"^\s*\[ACCESSORY_IDs\." + Regex.Escape(constantName) + @"\]\s*=\s*"".*"",?\s*$";

			for (int i = 0; i < lines.Count; i++) {
				if (Regex.IsMatch(lines[i], pattern, RegexOptions.IgnoreCase)) {
					lines[i] = newLine;
					replaced = true;
					break;
				}
			}

			if (!replaced)
				lines = InsertBeforeClosingBrace(lines, newLine, "-- CUSTOM");

			return string.Join(Environment.NewLine, lines);
		}

		private static List<string> InsertBeforeClosingBrace(List<string> lines, string newLine, string preferredSectionMarker) {
			int insertAt = lines.Count;

			for (int i = lines.Count - 1; i >= 0; i--) {
				if (lines[i].Trim() == "}") {
					insertAt = i;
					break;
				}
			}

			int sectionIndex = -1;
			for (int i = 0; i < lines.Count; i++) {
				if (lines[i].IndexOf(preferredSectionMarker, StringComparison.OrdinalIgnoreCase) >= 0) {
					sectionIndex = i;
					break;
				}
			}

			if (sectionIndex >= 0) {
				insertAt = sectionIndex + 1;
				while (insertAt < lines.Count && string.IsNullOrWhiteSpace(lines[insertAt]))
					insertAt++;
			}

			lines.Insert(insertAt, newLine);
			return lines;
		}

		private static string EscapeLuaString(string value) {
			if (value == null)
				return "";

			return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
		}

		private static string ReadText(string path) {
			return EncodingService.DisplayEncoding.GetString(File.ReadAllBytes(path));
		}

		private static void WriteText(string path, string content) {
			File.WriteAllBytes(path, EncodingService.DisplayEncoding.GetBytes(content));
		}
	}
}
