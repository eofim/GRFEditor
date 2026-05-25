using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using GRF.Core;
using GRF.IO;
using GRFEditor.Core.RagnarokValidation;

namespace GRFEditor.Core.BuildPipeline {
	[DataContract]
	internal sealed class BuildManifestDocument {
		[DataMember] public DateTime GeneratedAtUtc { get; set; }
		[DataMember] public string GrfFileName { get; set; }
		[DataMember] public string GrfFilePath { get; set; }
		[DataMember] public string ProfileName { get; set; }
		[DataMember] public int FileCount { get; set; }
		[DataMember] public bool HashesIncluded { get; set; }
		[DataMember] public BuildManifestValidationSummary Validation { get; set; }
		[DataMember] public List<string> Warnings { get; set; }
		[DataMember] public List<string> Errors { get; set; }
		[DataMember] public List<BuildManifestEntry> Files { get; set; }
	}

	[DataContract]
	internal sealed class BuildManifestValidationSummary {
		[DataMember] public bool RanValidation { get; set; }
		[DataMember] public bool WasCancelled { get; set; }
		[DataMember] public int TotalIssues { get; set; }
		[DataMember] public int CriticalCount { get; set; }
		[DataMember] public int ErrorCount { get; set; }
		[DataMember] public int WarningCount { get; set; }
		[DataMember] public int InfoCount { get; set; }
		[DataMember] public bool HasBlockingIssues { get; set; }
	}

	[DataContract]
	internal sealed class BuildManifestEntry {
		[DataMember] public string RelativePath { get; set; }
		[DataMember] public long Size { get; set; }
		[DataMember] public long CompressedSize { get; set; }
		[DataMember] public string Extension { get; set; }
		[DataMember] public string Md5 { get; set; }
		[DataMember] public string Sha1 { get; set; }
	}

	[DataContract]
	internal sealed class BuildHashesDocument {
		[DataMember] public string GrfFileName { get; set; }
		[DataMember] public DateTime GeneratedAtUtc { get; set; }
		[DataMember] public List<BuildHashEntry> Entries { get; set; }
	}

	[DataContract]
	internal sealed class BuildHashEntry {
		[DataMember] public string RelativePath { get; set; }
		[DataMember] public string Md5 { get; set; }
		[DataMember] public string Sha1 { get; set; }
	}

	internal static class BuildPipelineArtifacts {
		public const string ManifestFileName = "build-manifest.json";
		public const string HashesFileName = "build-hashes.json";
		public const string ChangelogFileName = "build-changelog.json";
		public const string ReportJsonFileName = "build-report.json";
		public const string ReportTextFileName = "build-report.txt";

		public static BuildManifestDocument CreateManifest(
			GrfHolder grf,
			string profileName,
			RagnarokValidationResult validationResult,
			bool includeHashes,
			IDictionary<string, BuildPipelineFileHashes> fileHashes) {
			string grfPath = grf?.FileName ?? "";
			var doc = new BuildManifestDocument {
				GeneratedAtUtc = DateTime.UtcNow,
				GrfFileName = String.IsNullOrEmpty(grfPath) ? "" : Path.GetFileName(grfPath),
				GrfFilePath = grfPath,
				ProfileName = profileName ?? "",
				HashesIncluded = includeHashes,
				Files = new List<BuildManifestEntry>(),
				Warnings = new List<string>(),
				Errors = new List<string>(),
				Validation = _buildValidationSummary(validationResult),
			};

			_populateValidationMessages(doc, validationResult);

			if (grf == null || grf.IsClosed) {
				doc.FileCount = 0;
				return doc;
			}

			foreach (var entry in grf.FileTable.Entries) {
				if (entry.IsRemoved)
					continue;

				string relativePath = GrfPath.CleanGrfPath(entry.RelativePath);
				var manifestEntry = new BuildManifestEntry {
					RelativePath = relativePath,
					Size = entry.NewSizeDecompressed,
					CompressedSize = entry.NewSizeCompressed,
					Extension = _getExtension(relativePath),
				};

				if (includeHashes && fileHashes != null) {
					BuildPipelineFileHashes hashes;
					if (fileHashes.TryGetValue(relativePath, out hashes)
						|| fileHashes.TryGetValue(entry.RelativePath, out hashes)) {
						manifestEntry.Md5 = hashes.Md5;
						manifestEntry.Sha1 = hashes.Sha1;
					}
				}

				doc.Files.Add(manifestEntry);
			}

			doc.Files = doc.Files
				.OrderBy(e => e.RelativePath, StringComparer.OrdinalIgnoreCase)
				.ToList();
			doc.FileCount = doc.Files.Count;
			return doc;
		}

		public static BuildManifestDocument TryLoadManifest(string directory) {
			string path = Path.Combine(directory, ManifestFileName);
			if (!File.Exists(path))
				return null;

			try {
				string json = File.ReadAllText(path, Encoding.UTF8);
				return BuildPipelineJson.Read<BuildManifestDocument>(json);
			}
			catch {
				return null;
			}
		}

		public static void WriteJson<T>(string filePath, T document) {
			string directory = Path.GetDirectoryName(filePath);
			if (!String.IsNullOrEmpty(directory) && !Directory.Exists(directory))
				Directory.CreateDirectory(directory);

			File.WriteAllText(filePath, BuildPipelineJson.Serialize(document), new UTF8Encoding(true));
		}

		private static BuildManifestValidationSummary _buildValidationSummary(RagnarokValidationResult validationResult) {
			if (validationResult == null) {
				return new BuildManifestValidationSummary {
					RanValidation = false,
				};
			}

			var activeIssues = validationResult.Issues
				.Where(issue => !RagnarokValidationIgnoredRules.IsIgnored(issue))
				.ToList();

			return new BuildManifestValidationSummary {
				RanValidation = true,
				WasCancelled = validationResult.WasCancelled,
				TotalIssues = activeIssues.Count,
				CriticalCount = activeIssues.Count(i => i.Severity == RagnarokValidationSeverity.Critical),
				ErrorCount = activeIssues.Count(i => i.Severity == RagnarokValidationSeverity.Error),
				WarningCount = activeIssues.Count(i => i.Severity == RagnarokValidationSeverity.Warning),
				InfoCount = activeIssues.Count(i => i.Severity == RagnarokValidationSeverity.Info),
				HasBlockingIssues = activeIssues.Any(i =>
					i.Severity == RagnarokValidationSeverity.Critical
					|| i.Severity == RagnarokValidationSeverity.Error),
			};
		}

		private static void _populateValidationMessages(
			BuildManifestDocument doc,
			RagnarokValidationResult validationResult) {
			if (validationResult == null)
				return;

			foreach (var issue in validationResult.Issues) {
				if (RagnarokValidationIgnoredRules.IsIgnored(issue))
					continue;

				string line = "[" + issue.Severity + "] "
					+ (String.IsNullOrEmpty(issue.RelativePath) ? "" : issue.RelativePath + ": ")
					+ issue.Message;

				switch (issue.Severity) {
					case RagnarokValidationSeverity.Critical:
					case RagnarokValidationSeverity.Error:
						doc.Errors.Add(line);
						break;
					default:
						doc.Warnings.Add(line);
						break;
				}
			}
		}

		private static string _getExtension(string relativePath) {
			if (String.IsNullOrEmpty(relativePath))
				return "";

			string ext = Path.GetExtension(relativePath);
			return String.IsNullOrEmpty(ext) ? "" : ext;
		}
	}
}
