using System;
using System.IO;

namespace GRFEditor.Core.ProjectProfiles {
	/// <summary>
	/// Resolves build/patch defaults from the active profile without changing global settings.
	/// </summary>
	public static class ProjectProfileBuildContext {
		public static string GetOutputGrfFileName(string defaultFileName) {
			return ActiveProjectProfile.GetBuildRule(ProjectProfileBuildRuleKeys.OutputGrfName, defaultFileName);
		}

		public static string GetPatchOutputDirectory() {
			return ActiveProjectProfile.GetPatchOutputDirectory();
		}

		public static string CombinePatchOutputPath(string fileName) {
			if (String.IsNullOrWhiteSpace(fileName))
				fileName = "data.grf";

			return Path.Combine(GetPatchOutputDirectory(), Path.GetFileName(fileName));
		}
	}
}
