namespace GRFEditor.Core.ProjectProfiles {
	/// <summary>
	/// Keys for <see cref="ProjectProfile.BuildRules"/> used by build/patch tooling.
	/// </summary>
	public static class ProjectProfileBuildRuleKeys {
		public const string OutputGrfName = "output.grfName";
		public const string Compression = "compression";
		public const string RepackOnBuild = "repackOnBuild";
		public const string RemoveJunkOnBuild = "removeJunkOnBuild";
		public const string NormalizePathsOnBuild = "normalizePathsOnBuild";
	}
}
