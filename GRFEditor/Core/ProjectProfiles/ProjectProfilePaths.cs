using System;
using System.IO;
using System.Linq;
using GRF.IO;
using GRFEditor.ApplicationConfiguration;

namespace GRFEditor.Core.ProjectProfiles {
	internal static class ProjectProfilePaths {
		public const string ProfilesFolderName = "Profiles";
		public const string ProjectEditorFolderName = ".grfeditor";
		public const string ProfileFileExtension = ".json";
		public const string ActiveProfileConfigKey = "[GRFEditor - Active project profile]";

		public static string UserProfilesDirectory {
			get {
				string path = GrfPath.Combine(GrfEditorConfiguration.ProgramDataPath, ProfilesFolderName);

				if (!Directory.Exists(path))
					Directory.CreateDirectory(path);

				return path;
			}
		}

		public static string GetProjectProfilesDirectory(string projectRootPath) {
			if (String.IsNullOrWhiteSpace(projectRootPath))
				return null;

			string path = GrfPath.Combine(Path.GetFullPath(projectRootPath), ProjectEditorFolderName, ProfilesFolderName);

			if (!Directory.Exists(path))
				Directory.CreateDirectory(path);

			return path;
		}

		public static string GetProfileFilePath(string profilesDirectory, string profileName) {
			if (String.IsNullOrEmpty(profilesDirectory))
				throw new ArgumentException("Profiles directory is not defined.", nameof(profilesDirectory));

			return Path.Combine(profilesDirectory, ToProfileFileName(profileName));
		}

		public static string ToProfileFileName(string profileName) {
			string safeName = SanitizeProfileName(profileName);

			if (String.IsNullOrEmpty(safeName))
				safeName = "profile";

			return safeName + ProfileFileExtension;
		}

		public static string SanitizeProfileName(string profileName) {
			if (String.IsNullOrWhiteSpace(profileName))
				return "";

			var invalid = Path.GetInvalidFileNameChars();
			var chars = profileName.Trim()
				.Select(c => invalid.Contains(c) ? '_' : c)
				.ToArray();

			string result = new string(chars).Trim('.', ' ');

			while (result.Contains("__"))
				result = result.Replace("__", "_");

			return result;
		}
	}
}
