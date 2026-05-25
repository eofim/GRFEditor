using System;
using System.IO;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using GRF.Core;
using GRFEditor.Tools.CustomAccessory;
using Utilities.Services;

namespace GRFEditor.Core.RagnarokValidation {
	internal sealed class RagnarokAccessoryParseResult {
		public Dictionary<string, int> AccessoryIds { get; } = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
		public Dictionary<string, string> Accnames { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		public List<int> AccessoryIdOrder { get; } = new List<int>();
		public int MatchedAccessoryIdLines { get; set; }
		public int MatchedAccnameLines { get; set; }
		public bool UsedLowConfidenceFallback { get; set; }
	}

	internal static class RagnarokAccessoryLuaParser {
		private static readonly Regex NonNumericIdRegex = new Regex(
			@"^\s*(?<name>ACCESSORY_[A-Za-z0-9_]+)\s*=\s*(?<id>[^0-9\s,][^\s,]*)\s*,?\s*(?:--.*)?$",
			RegexOptions.Compiled | RegexOptions.IgnoreCase);

		public static RagnarokAccessoryParseResult ParseAccessoryId(string text) {
			var result = new RagnarokAccessoryParseResult();
			if (String.IsNullOrEmpty(text))
				return result;

			var parsed = CustomAccessoryLuaTables.ParseAccessoryIdsFromTextFallback(text);
			result.MatchedAccessoryIdLines = parsed.Count;

			if (parsed.Count == 0)
				result.UsedLowConfidenceFallback = true;

			foreach (var pair in parsed) {
				var name = CustomAccessoryNaming.NormalizeConstantName(pair.Key);
				if (!result.AccessoryIds.ContainsKey(name)) {
					result.AccessoryIds[name] = pair.Value;
					result.AccessoryIdOrder.Add(pair.Value);
				}
			}

			return result;
		}

		public static RagnarokAccessoryParseResult ParseAccname(string text) {
			var result = new RagnarokAccessoryParseResult();
			if (String.IsNullOrEmpty(text))
				return result;

			var parsed = CustomAccessoryLuaTables.ParseAccnamesFromTextFallback(text);
			result.MatchedAccnameLines = parsed.Count;

			if (parsed.Count == 0)
				result.UsedLowConfidenceFallback = true;

			foreach (var pair in parsed)
				result.Accnames[CustomAccessoryNaming.NormalizeConstantName(pair.Key)] = pair.Value;

			return result;
		}

		public static string ReadEntryText(GrfHolder grf, string relativePath) {
			if (String.IsNullOrEmpty(relativePath))
				return null;

			if (Path.IsPathRooted(relativePath) && File.Exists(relativePath)) {
				try {
					return File.ReadAllText(relativePath, EncodingService.DisplayEncoding);
				}
				catch {
					return null;
				}
			}

			if (grf == null || !grf.FileTable.ContainsFile(relativePath))
				return null;

			try {
				byte[] data = grf.FileTable[relativePath].GetDecompressedData();
				if (data == null || data.Length == 0)
					return "";

				return EncodingService.DisplayEncoding.GetString(data);
			}
			catch {
				return null;
			}
		}

		public static IEnumerable<string> EnumerateLines(string text) {
			if (String.IsNullOrEmpty(text))
				yield break;

			foreach (var line in text.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n'))
				yield return line;
		}

		public static bool IsSkippableLine(string trimmed) {
			if (String.IsNullOrEmpty(trimmed))
				return true;

			if (trimmed.StartsWith("--"))
				return true;

			if (trimmed == "{" || trimmed == "}" || trimmed == "}," || trimmed == "};")
				return true;

			if (trimmed.StartsWith("return ", StringComparison.OrdinalIgnoreCase))
				return true;

			if (trimmed.StartsWith("function", StringComparison.OrdinalIgnoreCase))
				return true;

			if (trimmed.StartsWith("ACCESSORY_IDs", StringComparison.OrdinalIgnoreCase))
				return true;

			if (trimmed.StartsWith("AccNameTable", StringComparison.OrdinalIgnoreCase))
				return true;

			if (trimmed.StartsWith("import ", StringComparison.OrdinalIgnoreCase))
				return true;

			return false;
		}

		public static bool IsAccessoryIdLineMatch(string line) {
			return CustomAccessoryLuaPatterns.AccessoryIdLineRegex.IsMatch(line);
		}

		public static bool IsAccnameLineMatch(string line) {
			return CustomAccessoryLuaPatterns.AccnameLineRegex.IsMatch(line);
		}

		public static bool LineLooksLikeAccessoryData(string trimmed, bool accessoryIdFile) {
			if (IsSkippableLine(trimmed))
				return false;

			if (accessoryIdFile)
				return trimmed.IndexOf("ACCESSORY_", StringComparison.OrdinalIgnoreCase) >= 0;

			return trimmed.IndexOf("ACCESSORY_IDs.", StringComparison.OrdinalIgnoreCase) >= 0
				|| (trimmed.IndexOf("ACCESSORY_", StringComparison.OrdinalIgnoreCase) >= 0 && trimmed.IndexOf('=') >= 0);
		}

		public static bool TryParseNonNumericAssignment(string line, out string constantName, out string rawId) {
			constantName = null;
			rawId = null;

			var match = NonNumericIdRegex.Match(line);
			if (!match.Success)
				return false;

			constantName = CustomAccessoryNaming.NormalizeConstantName(match.Groups["name"].Value);
			rawId = match.Groups["id"].Value.Trim();
			return true;
		}

		public static int CountOutOfOrderSteps(IList<int> idsInFileOrder) {
			if (idsInFileOrder == null || idsInFileOrder.Count < 2)
				return 0;

			int steps = 0;
			int previous = idsInFileOrder[0];

			for (int i = 1; i < idsInFileOrder.Count; i++) {
				if (idsInFileOrder[i] < previous)
					steps++;

				previous = idsInFileOrder[i];
			}

			return steps;
		}
	}
}
