using System;
using System.IO;

namespace GRFEditor.Tools.CustomAccessory {
	public static class CustomAccessoryNaming {
		public static void FromSpriteFileName(string fileNameWithoutExtension, out string constantName, out string displayName) {
			var baseName = StripRoSpriteFilePrefix((fileNameWithoutExtension ?? "").Trim());
			if (string.IsNullOrEmpty(baseName)) {
				constantName = "ACCESSORY_UNKNOWN";
				displayName = "_unknown";
				return;
			}

			displayName = baseName.StartsWith("_", StringComparison.Ordinal) ? baseName : "_" + baseName;

			var idPart = baseName.StartsWith("_", StringComparison.Ordinal) ? baseName.Substring(1) : baseName;
			if (!idPart.StartsWith("ACCESSORY_", StringComparison.OrdinalIgnoreCase))
				idPart = "ACCESSORY_" + idPart;

			constantName = idPart;
		}

		public static string FromSpritePath(string spritePath) {
			var fileName = Path.GetFileNameWithoutExtension(spritePath ?? "");
			string constantName;
			string displayName;
			FromSpriteFileName(fileName, out constantName, out displayName);
			return constantName;
		}

		private static string StripRoSpriteFilePrefix(string baseName) {
			if (baseName.StartsWith(CustomAccessorySpritePaths.FemaleSpriteFilePrefix, StringComparison.Ordinal))
				return baseName.Substring(CustomAccessorySpritePaths.FemaleSpriteFilePrefix.Length);

			if (baseName.StartsWith(CustomAccessorySpritePaths.MaleSpriteFilePrefix, StringComparison.Ordinal))
				return baseName.Substring(CustomAccessorySpritePaths.MaleSpriteFilePrefix.Length);

			return baseName;
		}
	}
}
