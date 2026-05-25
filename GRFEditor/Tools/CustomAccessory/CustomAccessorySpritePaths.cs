using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GRFEditor.ApplicationConfiguration;

namespace GRFEditor.Tools.CustomAccessory {
	/// <summary>
	/// Caminhos de sprites de acessório no GRF (mesma convenção que EMenuInteraction / cliente RO).
	/// </summary>
	public static class CustomAccessorySpritePaths {
		/// <summary>Pasta padrão: data/sprite/악세서리/여 (feminino).</summary>
		public const string DefaultFolder = @"data\sprite\¾Ç¼¼»ç¸®\¿©";

		/// <summary>Prefixo do nome de arquivo .spr feminino (여_).</summary>
		public const string FemaleSpriteFilePrefix = "¿©_";

		/// <summary>Prefixo do nome de arquivo .spr masculino (남_).</summary>
		public const string MaleSpriteFilePrefix = "³²_";

		public static IEnumerable<string> GetConfiguredPrefixes() {
			var raw = GrfEditorConfiguration.CustomAccessorySpriteFolder;
			if (string.IsNullOrWhiteSpace(raw))
				return new[] { NormalizeFolderPrefix(DefaultFolder) };

			var parts = raw.Split(new[] { ';', '|', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
			var prefixes = parts.Select(NormalizeFolderPrefix).Where(p => !string.IsNullOrEmpty(p)).ToList();

			// Config antiga: só "sprite" não encontra data/sprite/...
			if (parts.Length == 1 && string.Equals(parts[0].Trim(), "sprite", StringComparison.OrdinalIgnoreCase)) {
				prefixes.Add(NormalizeFolderPrefix(@"data\sprite"));
				prefixes.Add(NormalizeFolderPrefix(DefaultFolder));
			}

			return prefixes.Distinct(StringComparer.OrdinalIgnoreCase);
		}

		public static string NormalizeGrfPath(string path) {
			if (string.IsNullOrEmpty(path))
				return "";

			var value = path.Replace('\\', '/');
			if (value.StartsWith("root/", StringComparison.OrdinalIgnoreCase))
				value = value.Substring(5);

			return value;
		}

		public static string NormalizeFolderPrefix(string folder) {
			var value = NormalizeGrfPath((folder ?? "").Trim());
			if (string.IsNullOrEmpty(value))
				return "";

			if (!value.EndsWith("/"))
				value += "/";

			return value;
		}

		public static bool MatchesAnyPrefix(string grfPath, IEnumerable<string> prefixes) {
			var path = NormalizeGrfPath(grfPath);
			foreach (var prefix in prefixes) {
				if (string.IsNullOrEmpty(prefix))
					return true;

				if (path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
					return true;
			}

			return false;
		}

		public static bool HasRoSpriteFilePrefix(string fileName) {
			if (string.IsNullOrEmpty(fileName))
				return false;

			return fileName.StartsWith(FemaleSpriteFilePrefix, StringComparison.Ordinal)
				|| fileName.StartsWith(MaleSpriteFilePrefix, StringComparison.Ordinal);
		}

		/// <summary>
		/// Ignora .spr/.act sem prefixo ¿©_/³²_ quando já existir o mesmo item com prefixo na mesma pasta.
		/// Ex.: c_capybara.spr é omitido se ¿©_C_Capybara.spr estiver na mesma pasta.
		/// </summary>
		public static List<string> FilterDuplicateUnprefixedSprites(IEnumerable<string> spritePaths) {
			var list = spritePaths.ToList();
			if (list.Count == 0)
				return list;

			var prefixedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			foreach (var path in list) {
				var fileName = Path.GetFileName(path.Replace('/', '\\'));
				if (!HasRoSpriteFilePrefix(fileName))
					continue;

				prefixedKeys.Add(BuildSpriteDuplicateKey(path));
			}

			if (prefixedKeys.Count == 0)
				return list;

			return list.Where(path => {
				var fileName = Path.GetFileName(path.Replace('/', '\\'));
				if (HasRoSpriteFilePrefix(fileName))
					return true;

				return !prefixedKeys.Contains(BuildSpriteDuplicateKey(path));
			}).ToList();
		}

		private static string BuildSpriteDuplicateKey(string grfPath) {
			var normalized = NormalizeGrfPath(grfPath);
			var lastSlash = normalized.LastIndexOf('/');
			var directory = lastSlash >= 0 ? normalized.Substring(0, lastSlash + 1) : "";
			var constant = CustomAccessoryNaming.FromSpritePath(grfPath);
			return directory + constant;
		}
	}
}
