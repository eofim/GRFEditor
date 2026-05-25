using System;
using System.IO;
using GRF.Core;
using GRFEditor.Core.ProjectProfiles;

namespace GRFEditor.Core.BuildPipeline {
	public class BuildPipelineOptions {
		public GrfHolder Grf { get; set; }

		/// <summary>
		/// Folder for manifest, changelog, hashes and build report. Uses active profile export/patch folder when empty.
		/// </summary>
		public string OutputDirectory { get; set; }

		public string ProfileName { get; set; }

		/// <summary>
		/// Run RemoveJunkFiles step (junk/system files only).
		/// </summary>
		public bool RunValidation { get; set; } = true;

		public bool RemoveJunkFiles { get; set; }

		/// <summary>
		/// Run NormalizePaths step (slash normalization renames).
		/// </summary>
		public bool NormalizePaths { get; set; }

		/// <summary>
		/// When true, optional fix steps may modify the opened GRF via command queue.
		/// When false, fix steps only report affected files (dry run).
		/// </summary>
		public bool ApplyGrfModifications { get; set; }

		/// <summary>
		/// Stop the pipeline after validation if critical/error issues remain (after profile ignored rules).
		/// </summary>
		public bool StopOnValidationErrors { get; set; }

		public bool GenerateManifest { get; set; } = true;

		/// <summary>
		/// Writes build-manifest.csv alongside JSON when manifest is generated.
		/// </summary>
		public bool ExportManifestCsv { get; set; } = true;

		public bool GenerateChangelog { get; set; } = true;

		/// <summary>
		/// Optional path to a previous build-manifest.json for changelog comparison.
		/// </summary>
		public string PreviousManifestPath { get; set; }
		public bool GenerateHashes { get; set; } = true;
		public bool ExportBuildReport { get; set; } = true;

		public static BuildPipelineOptions FromActiveProfile(GrfHolder grf) {
			var options = new BuildPipelineOptions {
				Grf = grf,
				ProfileName = ActiveProjectProfile.DisplayName,
			};

			string export = ActiveProjectProfile.GetExportFolderPath();
			if (!String.IsNullOrEmpty(export))
				options.OutputDirectory = export;
			else
				options.OutputDirectory = ProjectProfileBuildContext.GetPatchOutputDirectory();

			string removeJunk = ActiveProjectProfile.GetBuildRule(ProjectProfileBuildRuleKeys.RemoveJunkOnBuild);
			if (!String.IsNullOrEmpty(removeJunk))
				options.RemoveJunkFiles = String.Equals(removeJunk, "true", StringComparison.OrdinalIgnoreCase) || removeJunk == "1";

			string normalize = ActiveProjectProfile.GetBuildRule(ProjectProfileBuildRuleKeys.NormalizePathsOnBuild);
			if (!String.IsNullOrEmpty(normalize))
				options.NormalizePaths = String.Equals(normalize, "true", StringComparison.OrdinalIgnoreCase) || normalize == "1";

			return options;
		}

		public string ResolveOutputDirectory() {
			if (!String.IsNullOrWhiteSpace(OutputDirectory))
				return Path.GetFullPath(OutputDirectory.Trim());

			string export = ActiveProjectProfile.GetExportFolderPath();
			if (!String.IsNullOrEmpty(export))
				return Path.GetFullPath(export);

			return Path.GetFullPath(ProjectProfileBuildContext.GetPatchOutputDirectory());
		}
	}
}
