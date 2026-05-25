using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace GRFEditor.Core.ProjectProfiles {
	public static class ProjectProfilePathValidator {
		public static IList<string> Validate(ProjectProfile profile) {
			var warnings = new List<string>();

			if (profile == null)
				return warnings;

			_checkFile(profile.MainGrfPath, "Main GRF", warnings, new[] { ".grf", ".gpf", ".thor" });
			_checkFolder(profile.DataFolderPath, "Data folder", warnings);
			_checkFolder(profile.ExportFolderPath, "Export folder", warnings);
			_checkFolder(profile.PatchOutputFolderPath, "Patch output folder", warnings);
			_checkFolder(profile.ClientFolderPath, "Client folder", warnings);
			_checkFile(profile.AccessoryIdPath, "Accessory ID (Lua)", warnings, new[] { ".lub", ".lua" });
			_checkFile(profile.AccNamePath, "Accessory name (Lua)", warnings, new[] { ".lub", ".lua" });
			_checkFile(profile.ItemInfoPath, "Item info (Lua)", warnings, new[] { ".lub", ".lua" });

			if (!String.IsNullOrWhiteSpace(profile.EncodingName) && !_isValidEncodingName(profile.EncodingName))
				warnings.Add("Encoding is not recognized: " + profile.EncodingName.Trim());

			return warnings;
		}

		private static void _checkFile(string path, string label, ICollection<string> warnings, string[] expectedExtensions) {
			if (String.IsNullOrWhiteSpace(path))
				return;

			string trimmed = path.Trim();

			if (!File.Exists(trimmed)) {
				warnings.Add(label + " — file not found: " + trimmed);
				return;
			}

			if (expectedExtensions != null && expectedExtensions.Length > 0) {
				string ext = Path.GetExtension(trimmed);

				if (!String.IsNullOrEmpty(ext) && !_hasExtension(expectedExtensions, ext))
					warnings.Add(label + " — unexpected extension (" + ext + "): " + trimmed);
			}
		}

		private static bool _hasExtension(string[] expectedExtensions, string ext) {
			for (int i = 0; i < expectedExtensions.Length; i++) {
				if (String.Equals(expectedExtensions[i], ext, StringComparison.OrdinalIgnoreCase))
					return true;
			}

			return false;
		}

		private static void _checkFolder(string path, string label, ICollection<string> warnings) {
			if (String.IsNullOrWhiteSpace(path))
				return;

			string trimmed = path.Trim();

			if (!Directory.Exists(trimmed))
				warnings.Add(label + " — folder not found: " + trimmed);
		}

		private static bool _isValidEncodingName(string encodingName) {
			if (Int32.TryParse(encodingName.Trim(), out int codepage)) {
				try {
					Encoding.GetEncoding(codepage);
					return true;
				}
				catch {
					return false;
				}
			}

			try {
				Encoding.GetEncoding(encodingName.Trim());
				return true;
			}
			catch {
				return false;
			}
		}

	}
}
