using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace GRFEditor.Core.BuildPipeline {
	internal static class BuildPipelineReportWriter {
		public static string ToJson(BuildPipelineResult result) {
			return BuildPipelineJson.Serialize(BuildPipelineReportDocument.FromResult(result));
		}

		public static void Export(BuildPipelineResult result, string outputDirectory) {
			if (result == null)
				throw new ArgumentNullException(nameof(result));

			if (String.IsNullOrWhiteSpace(outputDirectory))
				throw new ArgumentException("Output directory is required.", nameof(outputDirectory));

			if (!Directory.Exists(outputDirectory))
				Directory.CreateDirectory(outputDirectory);

			string jsonPath = Path.Combine(outputDirectory, BuildPipelineArtifacts.ReportJsonFileName);
			string textPath = Path.Combine(outputDirectory, BuildPipelineArtifacts.ReportTextFileName);

			BuildPipelineArtifacts.WriteJson(jsonPath, BuildPipelineReportDocument.FromResult(result));
			File.WriteAllText(textPath, ToPlainText(result), new UTF8Encoding(true));

			result.ReportJsonPath = jsonPath;
			result.ReportTextPath = textPath;
		}

		public static string ToPlainText(BuildPipelineResult result) {
			var sb = new StringBuilder();
			sb.AppendLine("GRF Editor — Build pipeline report");
			sb.AppendLine("Generated (UTC): " + (result.FinishedAt ?? DateTime.UtcNow).ToString("u"));
			sb.AppendLine("GRF: " + (result.GrfFileName ?? ""));
			sb.AppendLine("Profile: " + (result.ProfileName ?? "(none)"));
			sb.AppendLine("Output: " + (result.OutputDirectory ?? ""));
			sb.AppendLine("Success: " + result.Success);
			sb.AppendLine("Severity: " + result.Severity);
			sb.AppendLine();

			if (result.ValidationResult != null) {
				sb.AppendLine("Validation summary");
				sb.AppendLine("  Total issues: " + result.ValidationResult.TotalCount);
				sb.AppendLine("  Critical: " + result.ValidationResult.CountBySeverity(RagnarokValidation.RagnarokValidationSeverity.Critical));
				sb.AppendLine("  Error: " + result.ValidationResult.CountBySeverity(RagnarokValidation.RagnarokValidationSeverity.Error));
				sb.AppendLine("  Warning: " + result.ValidationResult.CountBySeverity(RagnarokValidation.RagnarokValidationSeverity.Warning));
				sb.AppendLine("  Info: " + result.ValidationResult.CountBySeverity(RagnarokValidation.RagnarokValidationSeverity.Info));
				sb.AppendLine();
			}

			sb.AppendLine("Steps");
			foreach (var step in result.Steps) {
				sb.AppendLine("--- " + step.Name + " ---");
				sb.AppendLine("  Success: " + step.Success);
				sb.AppendLine("  Severity: " + step.Severity);
				if (step.Duration.HasValue)
					sb.AppendLine("  Duration: " + step.Duration.Value.TotalSeconds.ToString("0.###") + "s");
				sb.AppendLine("  Files affected: " + step.FilesAffected.Count);

				foreach (string error in step.Errors.Take(20))
					sb.AppendLine("  ERROR: " + error);

				if (step.Errors.Count > 20)
					sb.AppendLine("  ... (" + (step.Errors.Count - 20) + " more errors)");

				foreach (string warning in step.Warnings.Take(20))
					sb.AppendLine("  WARN: " + warning);

				if (step.Warnings.Count > 20)
					sb.AppendLine("  ... (" + (step.Warnings.Count - 20) + " more warnings)");

				sb.AppendLine();
			}

			if (!String.IsNullOrEmpty(result.ManifestFilePath))
				sb.AppendLine("Manifest (JSON): " + result.ManifestFilePath);
			if (!String.IsNullOrEmpty(result.ManifestCsvFilePath))
				sb.AppendLine("Manifest (CSV): " + result.ManifestCsvFilePath);
			if (!String.IsNullOrEmpty(result.ChangelogFilePath))
				sb.AppendLine("Changelog (JSON): " + result.ChangelogFilePath);
			if (!String.IsNullOrEmpty(result.ChangelogTextPath))
				sb.AppendLine("Changelog (TXT): " + result.ChangelogTextPath);
			if (!String.IsNullOrEmpty(result.HashesFilePath))
				sb.AppendLine("Hashes: " + result.HashesFilePath);

			foreach (string message in result.Messages)
				sb.AppendLine(message);

			return sb.ToString();
		}

		[System.Runtime.Serialization.DataContract]
		internal sealed class BuildPipelineReportDocument {
			[System.Runtime.Serialization.DataMember] public DateTime StartedAtUtc { get; set; }
			[System.Runtime.Serialization.DataMember] public DateTime? FinishedAtUtc { get; set; }
			[System.Runtime.Serialization.DataMember] public bool Success { get; set; }
			[System.Runtime.Serialization.DataMember] public string Severity { get; set; }
			[System.Runtime.Serialization.DataMember] public string GrfFileName { get; set; }
			[System.Runtime.Serialization.DataMember] public string ProfileName { get; set; }
			[System.Runtime.Serialization.DataMember] public string OutputDirectory { get; set; }
			[System.Runtime.Serialization.DataMember] public List<BuildStepReportEntry> Steps { get; set; }
			[System.Runtime.Serialization.DataMember] public List<string> Messages { get; set; }

			public static BuildPipelineReportDocument FromResult(BuildPipelineResult result) {
				return new BuildPipelineReportDocument {
					StartedAtUtc = result.StartedAt,
					FinishedAtUtc = result.FinishedAt,
					Success = result.Success,
					Severity = result.Severity.ToString(),
					GrfFileName = result.GrfFileName,
					ProfileName = result.ProfileName,
					OutputDirectory = result.OutputDirectory,
					Messages = new List<string>(result.Messages),
					Steps = result.Steps.Select(s => new BuildStepReportEntry {
						Name = s.Name,
						StartedAtUtc = s.StartedAt,
						FinishedAtUtc = s.FinishedAt,
						Success = s.Success,
						Severity = s.Severity.ToString(),
						Warnings = new List<string>(s.Warnings),
						Errors = new List<string>(s.Errors),
						FilesAffected = new List<string>(s.FilesAffected),
					}).ToList(),
				};
			}
		}

		[System.Runtime.Serialization.DataContract]
		internal sealed class BuildStepReportEntry {
			[System.Runtime.Serialization.DataMember] public string Name { get; set; }
			[System.Runtime.Serialization.DataMember] public DateTime StartedAtUtc { get; set; }
			[System.Runtime.Serialization.DataMember] public DateTime? FinishedAtUtc { get; set; }
			[System.Runtime.Serialization.DataMember] public bool Success { get; set; }
			[System.Runtime.Serialization.DataMember] public string Severity { get; set; }
			[System.Runtime.Serialization.DataMember] public List<string> Warnings { get; set; }
			[System.Runtime.Serialization.DataMember] public List<string> Errors { get; set; }
			[System.Runtime.Serialization.DataMember] public List<string> FilesAffected { get; set; }
		}
	}
}
