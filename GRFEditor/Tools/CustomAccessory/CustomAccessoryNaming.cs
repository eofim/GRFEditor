using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace GRFEditor.Tools.CustomAccessory {
	public static class CustomAccessoryNaming {
		private static readonly Regex MultiUnderscoreRegex = new Regex(@"_+", RegexOptions.Compiled);

		public static string SanitizeIdentifierPart(string raw) {
			if (string.IsNullOrEmpty(raw))
				return "";

			var sb = new StringBuilder(raw.Length);
			foreach (char c in raw) {
				if (char.IsControl(c))
					continue;

				if ((c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '_')
					sb.Append(c);
				else
					sb.Append('_');
			}

			return MultiUnderscoreRegex.Replace(sb.ToString(), "_").Trim('_');
		}

		public static string NormalizeConstantName(string constantName) {
			if (string.IsNullOrWhiteSpace(constantName))
				return "ACCESSORY_UNKNOWN";

			var name = constantName.Trim();
			if (name.StartsWith("ACCESSORY_", StringComparison.OrdinalIgnoreCase))
				name = name.Substring("ACCESSORY_".Length);

			name = SanitizeIdentifierPart(name).ToLowerInvariant();
			if (string.IsNullOrEmpty(name))
				return "ACCESSORY_UNKNOWN";

			return "ACCESSORY_" + name;
		}

		public static string NormalizeDisplayName(string constantName, string suggestedDisplayName = null) {
			var constant = NormalizeConstantName(constantName);
			var part = constant.Length > "ACCESSORY_".Length
				? constant.Substring("ACCESSORY_".Length)
				: "unknown";

			var fromConstant = "_" + part;

			if (string.IsNullOrWhiteSpace(suggestedDisplayName))
				return fromConstant;

			var cleaned = suggestedDisplayName.Trim();
			if (!cleaned.StartsWith("_", StringComparison.Ordinal))
				cleaned = "_" + SanitizeIdentifierPart(cleaned);
			else
				cleaned = "_" + SanitizeIdentifierPart(cleaned.TrimStart('_'));

			if (string.IsNullOrEmpty(cleaned) || cleaned == "_")
				return fromConstant;

			if (string.Equals(cleaned, fromConstant, StringComparison.OrdinalIgnoreCase))
				return fromConstant;

			var cleanedLower = cleaned.ToLowerInvariant();
			if (string.Equals(cleanedLower, fromConstant, StringComparison.Ordinal))
				return fromConstant;

			return cleanedLower;
		}

		public static string FromSpritePath(string spritePath) {
			var normalizedPath = (spritePath ?? "").Replace('/', '\\');
			var fileName = Path.GetFileNameWithoutExtension(normalizedPath);
			string constantName;
			string displayName;
			FromSpriteFileName(fileName, out constantName, out displayName);
			return constantName;
		}

		public static void FromSpriteFileName(string fileNameWithoutExtension, out string constantName, out string displayName) {
			var baseName = StripRoSpriteFilePrefix(NormalizeSpriteFileName(fileNameWithoutExtension));
			baseName = SanitizeIdentifierPart(baseName);

			if (string.IsNullOrEmpty(baseName)) {
				constantName = "ACCESSORY_UNKNOWN";
				displayName = "_unknown";
				return;
			}

			constantName = NormalizeConstantName("ACCESSORY_" + baseName);
			displayName = NormalizeDisplayName(constantName);
		}

		private static string NormalizeSpriteFileName(string fileNameWithoutExtension) {
			var name = (fileNameWithoutExtension ?? "").Trim();
			if (name.EndsWith(".spr", StringComparison.OrdinalIgnoreCase)
				|| name.EndsWith(".act", StringComparison.OrdinalIgnoreCase))
				name = Path.GetFileNameWithoutExtension(name);

			return name;
		}

		private static string StripRoSpriteFilePrefix(string baseName) {
			if (string.IsNullOrEmpty(baseName))
				return "";

			if (baseName.StartsWith(CustomAccessorySpritePaths.FemaleSpriteFilePrefix, StringComparison.Ordinal))
				baseName = baseName.Substring(CustomAccessorySpritePaths.FemaleSpriteFilePrefix.Length);

			if (baseName.StartsWith(CustomAccessorySpritePaths.MaleSpriteFilePrefix, StringComparison.Ordinal))
				baseName = baseName.Substring(CustomAccessorySpritePaths.MaleSpriteFilePrefix.Length);

			int start = 0;
			while (start < baseName.Length && !IsIdentifierChar(baseName[start]))
				start++;

			int end = baseName.Length;
			while (end > start && !IsIdentifierChar(baseName[end - 1]))
				end--;

			if (start >= end)
				return "";

			return baseName.Substring(start, end - start);
		}

		private static bool IsIdentifierChar(char c) {
			return (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '_';
		}
	}
}
