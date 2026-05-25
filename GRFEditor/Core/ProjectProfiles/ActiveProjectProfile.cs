using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;

namespace GRFEditor.Core.ProjectProfiles {
	/// <summary>
	/// Read-only access to the active project profile for tools. Does not change global editor settings.
	/// </summary>
	public static class ActiveProjectProfile {
		private static ProjectProfile _cached;
		private static string _cachedActiveName;

		public static event EventHandler Changed;

		public static ProjectProfile Current => _getCurrent();

		public static bool HasActive => Current != null;

		public static string DisplayName => Current?.Name;

		public static void Invalidate() {
			_cached = null;
			_cachedActiveName = null;
			Changed?.Invoke(null, EventArgs.Empty);
		}

		public static void NotifyChanged() {
			Invalidate();
		}

		public static string GetMainGrfPath() => _trimmed(Current?.MainGrfPath);

		public static string GetDataFolderPath() => _trimmed(Current?.DataFolderPath);

		public static string GetExportFolderPath() => _trimmed(Current?.ExportFolderPath);

		public static string GetPatchOutputFolderPath() => _trimmed(Current?.PatchOutputFolderPath);

		public static string GetClientFolderPath() => _trimmed(Current?.ClientFolderPath);

		public static string GetAccessoryIdPath() =>
			_firstExistingFile(_trimmed(Current?.AccessoryIdPath), GRFEditor.ApplicationConfiguration.GrfEditorConfiguration.CustomAccessoryIdLubPath);

		public static string GetAccNamePath() =>
			_firstExistingFile(_trimmed(Current?.AccNamePath), GRFEditor.ApplicationConfiguration.GrfEditorConfiguration.CustomAccessoryAccnameLubPath);

		public static string GetConfiguredAccessoryIdPath() =>
			_preferProfilePath(_trimmed(Current?.AccessoryIdPath), GRFEditor.ApplicationConfiguration.GrfEditorConfiguration.CustomAccessoryIdLubPath);

		public static string GetConfiguredAccNamePath() =>
			_preferProfilePath(_trimmed(Current?.AccNamePath), GRFEditor.ApplicationConfiguration.GrfEditorConfiguration.CustomAccessoryAccnameLubPath);

		public static string GetItemInfoPath() => _trimmed(Current?.ItemInfoPath);

		public static string GetItemInfoOutputFolder() {
			string export = GetExportFolderPath();
			if (!String.IsNullOrEmpty(export) && Directory.Exists(export))
				return export;

			string itemInfo = GetItemInfoPath();
			if (!String.IsNullOrEmpty(itemInfo)) {
				string dir = Path.GetDirectoryName(itemInfo);
				if (!String.IsNullOrEmpty(dir) && Directory.Exists(dir))
					return dir;
			}

			return null;
		}

		public static int? GetLastUsedViewId() {
			if (Current == null)
				return null;

			return Current.LastUsedViewId > 0 ? Current.LastUsedViewId : (int?)null;
		}

		public static int? TryGetEncodingCodepage() {
			if (Current == null || String.IsNullOrWhiteSpace(Current.EncodingName))
				return null;

			if (Int32.TryParse(Current.EncodingName.Trim(), out int codepage)) {
				try {
					Encoding.GetEncoding(codepage);
					return codepage;
				}
				catch {
					return null;
				}
			}

			try {
				return Encoding.GetEncoding(Current.EncodingName.Trim()).CodePage;
			}
			catch {
				return null;
			}
		}

		public static string GetBuildRule(string key, string defaultValue = null) {
			if (Current?.BuildRules == null || String.IsNullOrWhiteSpace(key))
				return defaultValue;

			string value;
			return Current.BuildRules.TryGetValue(key, out value) && !String.IsNullOrWhiteSpace(value)
				? value.Trim()
				: defaultValue;
		}

		public static string GetPatchOutputDirectory() {
			string folder = GetPatchOutputFolderPath();
			if (!String.IsNullOrEmpty(folder))
				return folder;

			return GRFEditor.ApplicationConfiguration.GrfEditorConfiguration.ProgramDataPath;
		}

		public static IList<string> GetPathWarningsForTool(params Func<ProjectProfile, string>[] pathSelectors) {
			var profile = Current;
			if (profile == null || pathSelectors == null || pathSelectors.Length == 0)
				return new List<string>();

			var allWarnings = ProjectProfilePathValidator.Validate(profile);
			if (allWarnings.Count == 0)
				return allWarnings;

			var relevant = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			foreach (var selector in pathSelectors) {
				string path = selector != null ? _trimmed(selector(profile)) : null;
				if (String.IsNullOrEmpty(path))
					continue;

				foreach (string warning in allWarnings) {
					if (warning.IndexOf(path, StringComparison.OrdinalIgnoreCase) >= 0)
						relevant.Add(warning);
				}
			}

			return relevant.Count > 0 ? relevant.ToList() : allWarnings;
		}

		public static bool ConfirmContinueWithInvalidPaths(Window owner, string toolTitle, IList<string> warnings) {
			if (warnings == null || warnings.Count == 0)
				return true;

			string message =
				"The active project profile has invalid or missing paths:" + Environment.NewLine + Environment.NewLine
				+ String.Join(Environment.NewLine, warnings.Take(8))
				+ (warnings.Count > 8 ? Environment.NewLine + "... (" + (warnings.Count - 8) + " more)" : "")
				+ Environment.NewLine + Environment.NewLine
				+ "You can continue using default paths or pick paths manually.";

			var result = MessageBox.Show(
				owner,
				message,
				String.IsNullOrEmpty(toolTitle) ? "Project profile" : toolTitle,
				MessageBoxButton.YesNo,
				MessageBoxImage.Warning);

			return result == MessageBoxResult.Yes;
		}

		public static string GetStatusToolTip() {
			var profile = Current;
			if (profile == null)
				return "No active project profile.";

			var lines = new List<string> { "Active profile: " + profile.Name };

			_appendPathLine(lines, "Main GRF", profile.MainGrfPath);
			_appendPathLine(lines, "Client", profile.ClientFolderPath);
			_appendPathLine(lines, "Export", profile.ExportFolderPath);

			var warnings = ProjectProfilePathValidator.Validate(profile);
			if (warnings.Count > 0)
				lines.Add(warnings.Count + " path warning(s) — see Project profiles.");

			return String.Join(Environment.NewLine, lines);
		}

		private static ProjectProfile _getCurrent() {
			var service = new ProjectProfileService();
			string activeName = service.GetActiveProfileName();

			if (String.IsNullOrWhiteSpace(activeName)) {
				_cached = null;
				_cachedActiveName = null;
				return null;
			}

			if (_cached != null && String.Equals(_cachedActiveName, activeName, StringComparison.OrdinalIgnoreCase))
				return _cached;

			_cachedActiveName = activeName;
			_cached = service.GetActiveProfile();
			return _cached;
		}

		private static string _preferProfilePath(string profilePath, string configPath) {
			if (!String.IsNullOrWhiteSpace(profilePath))
				return profilePath.Trim();

			return String.IsNullOrWhiteSpace(configPath) ? null : configPath.Trim();
		}

		private static string _firstExistingFile(string profilePath, string configPath) {
			if (!String.IsNullOrWhiteSpace(profilePath)) {
				string trimmed = profilePath.Trim();
				if (File.Exists(trimmed))
					return trimmed;
			}

			if (!String.IsNullOrWhiteSpace(configPath)) {
				string trimmed = configPath.Trim();
				if (File.Exists(trimmed))
					return trimmed;
			}

			return null;
		}

		private static string _trimmed(string value) {
			return String.IsNullOrWhiteSpace(value) ? null : value.Trim();
		}

		private static void _appendPathLine(List<string> lines, string label, string path) {
			if (!String.IsNullOrWhiteSpace(path))
				lines.Add(label + ": " + path.Trim());
		}
	}
}
