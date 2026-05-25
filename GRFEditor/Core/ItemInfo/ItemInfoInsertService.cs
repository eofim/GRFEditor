using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using GRFEditor.Tools.CustomAccessory;
using Utilities.Services;

namespace GRFEditor.Core.ItemInfo {
	public sealed class ItemInfoInsertResult {
		public string TargetPath { get; set; }
		public string BackupPath { get; set; }
		public bool Applied { get; set; }
	}

	public static class ItemInfoInsertService {
		private static readonly Regex BlockStartRegex = new Regex(
			@"\[\s*(?<id>\d+)\s*\]\s*=\s*\{",
			RegexOptions.Compiled | RegexOptions.CultureInvariant);

		public static string ResolveWritableItemInfoPath(string profilePath) {
			if (!string.IsNullOrWhiteSpace(profilePath) && File.Exists(profilePath))
				return Path.GetFullPath(profilePath);

			return null;
		}

		public static string ReadItemInfoText(string filePath) {
			if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
				return null;

			return File.ReadAllText(filePath, EncodingService.DisplayEncoding);
		}

		public static string PreviewUpsert(string fileContent, ItemInfoEntry entry) {
			if (entry == null || entry.ItemId <= 0)
				throw new ArgumentException("ItemId inválido.");

			string block = ItemInfoService.GenerateLuaBlock(entry);
			return UpsertContent(fileContent ?? "", block.TrimEnd(), entry.ItemId);
		}

		public static string BuildFileDiff(string before, string after) {
			return CustomAccessoryLuaWriter.BuildLineDiffPreview("iteminfo", before ?? "", after ?? "");
		}

		public static string UpsertContent(string content, string newBlock, int itemId) {
			content = content ?? "";
			content = RemoveBlock(content, itemId);

			if (string.IsNullOrWhiteSpace(content))
				return newBlock.TrimEnd() + Environment.NewLine;

			newBlock = newBlock.TrimEnd();
			int insertAt = FindInsertIndex(content);

			if (insertAt < 0)
				return content.TrimEnd() + Environment.NewLine + Environment.NewLine + newBlock + Environment.NewLine;

			string prefix = content.Substring(0, insertAt).TrimEnd();
			string suffix = content.Substring(insertAt);

			return prefix + Environment.NewLine + newBlock + Environment.NewLine + suffix;
		}

		public static string RemoveBlock(string content, int itemId) {
			if (string.IsNullOrEmpty(content) || itemId <= 0)
				return content ?? "";

			var matches = BlockStartRegex.Matches(content);
			for (int i = matches.Count - 1; i >= 0; i--) {
				var match = matches[i];
				if (!int.TryParse(match.Groups["id"].Value, out int id) || id != itemId)
					continue;

				int blockStart = match.Index;
				int braceStart = match.Index + match.Length - 1;
				int braceEnd = FindClosingBrace(content, braceStart);
				if (braceEnd < 0)
					continue;

				int removeEnd = braceEnd + 1;
				while (removeEnd < content.Length && (content[removeEnd] == ',' || content[removeEnd] == '\r' || content[removeEnd] == '\n' || content[removeEnd] == ' ' || content[removeEnd] == '\t'))
					removeEnd++;

				content = content.Substring(0, blockStart) + content.Substring(removeEnd);
			}

			return content;
		}

		public static string CreateBackup(string filePath) {
			if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
				return null;

			var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
			var backupPath = filePath + "." + stamp + ".bak";
			File.Copy(filePath, backupPath, overwrite: false);
			return backupPath;
		}

		public static ItemInfoInsertResult ApplyUpsert(string filePath, ItemInfoEntry entry) {
			if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
				throw new FileNotFoundException("iteminfo não encontrado.", filePath);

			if (entry == null || entry.ItemId <= 0)
				throw new ArgumentException("ItemId inválido.");

			string before = ReadItemInfoText(filePath);
			string after = PreviewUpsert(before, entry);

			var result = new ItemInfoInsertResult {
				TargetPath = Path.GetFullPath(filePath),
				BackupPath = CreateBackup(filePath),
			};

			File.WriteAllBytes(filePath, EncodingService.DisplayEncoding.GetBytes(after));
			result.Applied = true;
			return result;
		}

		public static ItemInfoInsertResult ApplyUpsertMany(string filePath, System.Collections.Generic.IEnumerable<ItemInfoEntry> entries) {
			if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
				throw new FileNotFoundException("iteminfo não encontrado.", filePath);

			string content = ReadItemInfoText(filePath) ?? "";

			foreach (var entry in entries) {
				if (entry == null || entry.ItemId <= 0)
					continue;

				string block = ItemInfoService.GenerateLuaBlock(entry);
				content = UpsertContent(content, block.TrimEnd(), entry.ItemId);
			}

			var result = new ItemInfoInsertResult {
				TargetPath = Path.GetFullPath(filePath),
				BackupPath = CreateBackup(filePath),
			};

			File.WriteAllBytes(filePath, EncodingService.DisplayEncoding.GetBytes(content));
			result.Applied = true;
			return result;
		}

		private static int FindInsertIndex(string content) {
			for (int i = content.Length - 1; i >= 0; i--) {
				if (content[i] != '}')
					continue;

				int depth = 0;
				bool inString = false;
				char stringChar = '\0';

				for (int j = i; j >= 0; j--) {
					char c = content[j];

					if (inString) {
						if (c == '\\' && j > 0) {
							j--;
							continue;
						}

						if (c == stringChar)
							inString = false;

						continue;
					}

					if (c == '"' || c == '\'') {
						inString = true;
						stringChar = c;
						continue;
					}

					if (c == '}')
						depth++;
					else if (c == '{') {
						depth--;
						if (depth == 0)
							return j;
					}
				}

				return i;
			}

			return -1;
		}

		private static int FindClosingBrace(string text, int openBraceIndex) {
			if (openBraceIndex < 0 || openBraceIndex >= text.Length || text[openBraceIndex] != '{')
				return -1;

			int depth = 0;
			bool inString = false;
			char stringChar = '\0';

			for (int i = openBraceIndex; i < text.Length; i++) {
				char c = text[i];

				if (inString) {
					if (c == '\\' && i + 1 < text.Length) {
						i++;
						continue;
					}

					if (c == stringChar)
						inString = false;

					continue;
				}

				if (c == '"' || c == '\'') {
					inString = true;
					stringChar = c;
					continue;
				}

				if (c == '{')
					depth++;
				else if (c == '}') {
					depth--;
					if (depth == 0)
						return i;
				}
			}

			return -1;
		}
	}
}
