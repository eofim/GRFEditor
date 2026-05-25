using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GRFEditor.ApplicationConfiguration;

namespace GRFEditor.Core.ProjectProfiles {
	/// <summary>
	/// Persists project profiles as JSON under the user configuration folder
	/// (%AppData%/GRF Editor/Profiles) or under a project-local .grfeditor/Profiles folder.
	/// </summary>
	public class ProjectProfileService {
		private readonly string _optionalProjectRootPath;

		public ProjectProfileService()
			: this(null) {
		}

		public ProjectProfileService(string projectRootPath) {
			_optionalProjectRootPath = String.IsNullOrWhiteSpace(projectRootPath) ? null : projectRootPath;
		}

		public string UserProfilesDirectory => ProjectProfilePaths.UserProfilesDirectory;

		public string ProjectProfilesDirectory =>
			_optionalProjectRootPath == null ? null : ProjectProfilePaths.GetProjectProfilesDirectory(_optionalProjectRootPath);

		public List<ProjectProfile> LoadProfiles() {
			var profiles = new Dictionary<string, ProjectProfile>(StringComparer.OrdinalIgnoreCase);

			_loadFromDirectory(UserProfilesDirectory, profiles);

			if (ProjectProfilesDirectory != null)
				_loadFromDirectory(ProjectProfilesDirectory, profiles);

			return profiles.Values
				.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
				.ToList();
		}

		public void SaveProfile(ProjectProfile profile, bool saveToProjectFolder = false) {
			if (profile == null)
				throw new ArgumentNullException(nameof(profile));

			if (String.IsNullOrWhiteSpace(profile.Name))
				throw new ArgumentException("Profile name is required.", nameof(profile));

			profile.EnsureDefaults();

			DateTime now = DateTime.UtcNow;
			if (profile.CreatedAt == default(DateTime))
				profile.CreatedAt = now;

			profile.UpdatedAt = now;

			string directory = saveToProjectFolder && ProjectProfilesDirectory != null
				? ProjectProfilesDirectory
				: UserProfilesDirectory;

			string filePath = ProjectProfilePaths.GetProfileFilePath(directory, profile.Name);
			ProjectProfileJsonSerializer.WriteToFile(filePath, profile);
		}

		public bool DeleteProfile(string profileName, bool deleteFromProjectFolder = false) {
			if (String.IsNullOrWhiteSpace(profileName))
				return false;

			bool deleted = false;

			deleted |= _deleteIfExists(ProjectProfilePaths.GetProfileFilePath(UserProfilesDirectory, profileName));

			if (deleteFromProjectFolder && ProjectProfilesDirectory != null)
				deleted |= _deleteIfExists(ProjectProfilePaths.GetProfileFilePath(ProjectProfilesDirectory, profileName));

			string activeName = GetActiveProfileName();
			if (deleted && !String.IsNullOrEmpty(activeName)
				&& String.Equals(activeName, profileName, StringComparison.OrdinalIgnoreCase))
				SetActiveProfile(null);

			return deleted;
		}

		public ProjectProfile GetActiveProfile() {
			string activeName = GetActiveProfileName();

			if (String.IsNullOrWhiteSpace(activeName))
				return null;

			return LoadProfiles().FirstOrDefault(p =>
				String.Equals(p.Name, activeName, StringComparison.OrdinalIgnoreCase));
		}

		public void SetActiveProfile(string profileName) {
			if (String.IsNullOrWhiteSpace(profileName)) {
				GrfEditorConfiguration.ConfigAsker[ProjectProfilePaths.ActiveProfileConfigKey] = "";
				return;
			}

			var profile = LoadProfiles().FirstOrDefault(p =>
				String.Equals(p.Name, profileName, StringComparison.OrdinalIgnoreCase));

			if (profile == null)
				throw new InvalidOperationException("Profile not found: " + profileName);

			GrfEditorConfiguration.ConfigAsker[ProjectProfilePaths.ActiveProfileConfigKey] = profile.Name;
		}

		public string GetActiveProfileName() {
			return GrfEditorConfiguration.ConfigAsker[ProjectProfilePaths.ActiveProfileConfigKey, ""];
		}

		public ProjectProfile TryGetProfile(string profileName) {
			if (String.IsNullOrWhiteSpace(profileName))
				return null;

			return LoadProfiles().FirstOrDefault(p =>
				String.Equals(p.Name, profileName, StringComparison.OrdinalIgnoreCase));
		}

		private static void _loadFromDirectory(string directory, IDictionary<string, ProjectProfile> profiles) {
			if (String.IsNullOrEmpty(directory) || !Directory.Exists(directory))
				return;

			foreach (string file in Directory.GetFiles(directory, "*" + ProjectProfilePaths.ProfileFileExtension)) {
				try {
					var profile = ProjectProfileJsonSerializer.ReadFromFile(file);

					if (profile == null || String.IsNullOrWhiteSpace(profile.Name))
						continue;

					profile.EnsureDefaults();
					profiles[profile.Name] = profile;
				}
				catch {
					// Skip corrupted profile files.
				}
			}
		}

		private static bool _deleteIfExists(string filePath) {
			if (!File.Exists(filePath))
				return false;

			File.Delete(filePath);
			return true;
		}
	}
}
