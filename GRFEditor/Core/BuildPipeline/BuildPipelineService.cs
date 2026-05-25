using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using GRF.Core;
using GRF.IO;
using GRF.Threading;
using GRFEditor.Core.RagnarokValidation;
using Utilities.Hash;
using Md5Hash = Utilities.Hash.Md5Hash;

namespace GRFEditor.Core.BuildPipeline {
	public class BuildPipelineService {
		public const string StepValidateRagnarokClientFiles = "ValidateRagnarokClientFiles";
		public const string StepRemoveJunkFiles = "RemoveJunkFiles";
		public const string StepNormalizePaths = "NormalizePaths";
		public const string StepGenerateManifest = "GenerateManifest";
		public const string StepGenerateChangelog = "GenerateChangelog";
		public const string StepGenerateHashes = "GenerateHashes";
		public const string StepExportBuildReport = "ExportBuildReport";

		private readonly RagnarokValidationService _validationService = new RagnarokValidationService();
		private readonly RagnarokValidationAutoFixService _autoFixService = new RagnarokValidationAutoFixService();

		private List<RagnarokValidationView> _activeValidationViews = new List<RagnarokValidationView>();
		private BuildManifestDocument _currentManifest;
		private Dictionary<string, BuildPipelineFileHashes> _fileHashes =
			new Dictionary<string, BuildPipelineFileHashes>(StringComparer.OrdinalIgnoreCase);

		public BuildPipelineResult Run(
			BuildPipelineOptions options,
			IProgress progress = null,
			CancellationToken cancellationToken = default(CancellationToken),
			Action<string> onStepStarting = null,
			Action<BuildStepResult> onStepCompleted = null) {
			if (options == null)
				throw new ArgumentNullException(nameof(options));

			var pipelineResult = new BuildPipelineResult {
				OutputDirectory = options.ResolveOutputDirectory(),
				ProfileName = options.ProfileName,
				GrfFileName = options.Grf?.FileName,
			};

			try {
				_validateOptions(options);
				_fileHashes.Clear();

				if (options.RunValidation) {
					_runStep(pipelineResult, StepValidateRagnarokClientFiles, onStepStarting, onStepCompleted, () =>
						_stepValidate(options, pipelineResult, progress, cancellationToken));

					if (cancellationToken.IsCancellationRequested)
						throw new OperationCanceledException();

					if (options.StopOnValidationErrors && _validationHasBlockingIssues(pipelineResult)) {
						if (options.ExportBuildReport)
							_runStep(pipelineResult, StepExportBuildReport, onStepStarting, onStepCompleted, () => _stepExportReport(pipelineResult));

						return _finish(pipelineResult, false, BuildSeverity.Error,
							"Pipeline stopped after validation errors.");
					}
				}

				if (cancellationToken.IsCancellationRequested)
					throw new OperationCanceledException();

				if (options.RemoveJunkFiles) {
					_runStep(pipelineResult, StepRemoveJunkFiles, onStepStarting, onStepCompleted, () =>
						_stepRemoveJunk(options, pipelineResult, cancellationToken));
				}

				if (options.NormalizePaths) {
					_runStep(pipelineResult, StepNormalizePaths, onStepStarting, onStepCompleted, () =>
						_stepNormalizePaths(options, pipelineResult, cancellationToken));
				}

				if (options.GenerateHashes) {
					_runStep(pipelineResult, StepGenerateHashes, onStepStarting, onStepCompleted, () =>
						_stepGenerateHashes(options, pipelineResult, progress, cancellationToken));
				}

				if (options.GenerateManifest) {
					_runStep(pipelineResult, StepGenerateManifest, onStepStarting, onStepCompleted, () =>
						_stepGenerateManifest(options, pipelineResult));
				}

				if (options.GenerateChangelog) {
					_runStep(pipelineResult, StepGenerateChangelog, onStepStarting, onStepCompleted, () =>
						_stepGenerateChangelog(options, pipelineResult));
				}

				if (options.ExportBuildReport) {
					_runStep(pipelineResult, StepExportBuildReport, onStepStarting, onStepCompleted, () =>
						_stepExportReport(pipelineResult));
				}

				bool success = pipelineResult.Steps.All(s => s.Success);
				var severity = _aggregateSeverity(pipelineResult.Steps);
				return _finish(pipelineResult, success, severity, null);
			}
			catch (OperationCanceledException) {
				pipelineResult.Messages.Add("Build pipeline cancelled.");
				return _finish(pipelineResult, false, BuildSeverity.Warning, null);
			}
			catch (Exception ex) {
				pipelineResult.Messages.Add("Build pipeline failed: " + ex.Message);
				return _finish(pipelineResult, false, BuildSeverity.Critical, null);
			}
		}

		private static void _validateOptions(BuildPipelineOptions options) {
			if (options.Grf == null || !options.Grf.IsOpened)
				throw new InvalidOperationException("An opened GRF is required to run the build pipeline.");
		}

		private static void _runStep(
			BuildPipelineResult pipeline,
			string name,
			Action<string> onStepStarting,
			Action<BuildStepResult> onStepCompleted,
			Action action) {
			var step = new BuildStepResult(name);
			pipeline.Steps.Add(step);
			onStepStarting?.Invoke(name);

			try {
				action();
			}
			catch (Exception ex) {
				step.Errors.Add(ex.Message);
				step.MarkFinished(false, BuildSeverity.Critical);
			}
			finally {
				onStepCompleted?.Invoke(step);
			}
		}

		private void _stepValidate(
			BuildPipelineOptions options,
			BuildPipelineResult pipeline,
			IProgress progress,
			CancellationToken cancellationToken) {
			var step = pipeline.GetStep(StepValidateRagnarokClientFiles);

			var validation = _validationService.Validate(options.Grf, progress, cancellationToken);
			pipeline.ValidationResult = validation;

			_activeValidationViews = validation.Issues
				.Where(issue => !RagnarokValidationIgnoredRules.IsIgnored(issue))
				.Select(issue => new RagnarokValidationView(issue))
				.ToList();

			foreach (var view in _activeValidationViews) {
				string line = "[" + view.Severity + "] " + view.RelativePath + ": " + view.Message;

				switch (view.Issue.Severity) {
					case RagnarokValidationSeverity.Critical:
					case RagnarokValidationSeverity.Error:
						step.Errors.Add(line);
						break;
					case RagnarokValidationSeverity.Warning:
						step.Warnings.Add(line);
						break;
					default:
						step.Warnings.Add(line);
						break;
				}
			}

			if (validation.WasCancelled) {
				step.Warnings.Add("Validation was cancelled before completion.");
				step.MarkFinished(false, BuildSeverity.Warning);
				return;
			}

			bool blocking = _validationHasBlockingIssues(pipeline);
			step.MarkFinished(!blocking, blocking ? BuildSeverity.Error : BuildSeverity.Info);

			if (blocking)
				step.Errors.Add("Validation reported critical or error issues (after profile ignored rules).");
		}

		private void _stepRemoveJunk(
			BuildPipelineOptions options,
			BuildPipelineResult pipeline,
			CancellationToken cancellationToken) {
			var step = pipeline.GetStep(StepRemoveJunkFiles);
			ThrowIfCancelled(cancellationToken);

			var targets = _activeValidationViews
				.Where(v => v.Issue != null
					&& v.Issue.CanAutoFix
					&& v.Issue.FixKind == RagnarokValidationFixKind.RemoveJunkFile)
				.ToList();

			foreach (var view in targets)
				step.FilesAffected.Add(view.Issue.RelativePath);

			if (targets.Count == 0) {
				step.Warnings.Add("No removable junk files were reported by validation.");
				step.MarkFinished(true, BuildSeverity.Info);
				return;
			}

			if (!options.ApplyGrfModifications) {
				step.Warnings.Add("Dry run — " + targets.Count + " junk file(s) would be removed. Set ApplyGrfModifications to apply.");
				step.MarkFinished(true, BuildSeverity.Warning);
				return;
			}

			foreach (var view in targets)
				view.IsSelectedForFix = true;

			var fixResult = _autoFixService.Apply(options.Grf, targets);

			if (fixResult.RemovedCount > 0)
				step.Warnings.Add("Removed " + fixResult.RemovedCount + " junk file(s) from GRF (pending save).");

			if (fixResult.SkippedCount > 0)
				step.Warnings.Add(fixResult.SkippedCount + " junk removal(s) skipped.");

			foreach (string message in fixResult.Messages)
				step.Warnings.Add(message);

			step.MarkFinished(fixResult.RemovedCount > 0 || fixResult.SkippedCount == 0, BuildSeverity.Info);
			_refreshValidationViews(options.Grf);
		}

		private void _stepNormalizePaths(
			BuildPipelineOptions options,
			BuildPipelineResult pipeline,
			CancellationToken cancellationToken) {
			var step = pipeline.GetStep(StepNormalizePaths);
			ThrowIfCancelled(cancellationToken);

			var targets = _activeValidationViews
				.Where(v => v.Issue != null
					&& v.Issue.CanAutoFix
					&& v.Issue.FixKind == RagnarokValidationFixKind.NormalizePathSlashes)
				.ToList();

			foreach (var view in targets) {
				string normalized = (view.Issue.RelativePath ?? "").Replace('/', '\\');
				if (!String.Equals(view.Issue.RelativePath, normalized, StringComparison.OrdinalIgnoreCase))
					step.FilesAffected.Add(view.Issue.RelativePath + " -> " + normalized);
			}

			if (targets.Count == 0) {
				step.Warnings.Add("No paths require slash normalization.");
				step.MarkFinished(true, BuildSeverity.Info);
				return;
			}

			if (!options.ApplyGrfModifications) {
				step.Warnings.Add("Dry run — " + targets.Count + " path(s) would be renamed. Set ApplyGrfModifications to apply.");
				step.MarkFinished(true, BuildSeverity.Warning);
				return;
			}

			foreach (var view in targets)
				view.IsSelectedForFix = true;

			var fixResult = _autoFixService.Apply(options.Grf, targets);

			if (fixResult.RenamedCount > 0)
				step.Warnings.Add(fixResult.RenamedCount + " path(s) renamed.");

			if (fixResult.SkippedCount > 0)
				step.Warnings.Add(fixResult.SkippedCount + " rename(s) skipped.");

			foreach (string message in fixResult.Messages)
				step.Warnings.Add(message);

			step.MarkFinished(fixResult.RenamedCount > 0 || fixResult.SkippedCount == 0, BuildSeverity.Info);
			_refreshValidationViews(options.Grf);
		}

		private void _stepGenerateManifest(BuildPipelineOptions options, BuildPipelineResult pipeline) {
			var step = pipeline.GetStep(StepGenerateManifest);

			bool includeHashes = options.GenerateHashes && _fileHashes.Count > 0;
			_currentManifest = BuildPipelineArtifacts.CreateManifest(
				options.Grf,
				options.ProfileName,
				pipeline.ValidationResult,
				includeHashes,
				includeHashes ? _fileHashes : null);

			string jsonPath = Path.Combine(pipeline.OutputDirectory, BuildPipelineArtifacts.ManifestFileName);
			BuildPipelineArtifacts.WriteJson(jsonPath, _currentManifest);
			pipeline.ManifestFilePath = jsonPath;
			step.FilesAffected.Add(jsonPath);

			if (options.ExportManifestCsv) {
				string csvPath = Path.Combine(pipeline.OutputDirectory, BuildManifestExporter.ManifestCsvFileName);
				BuildManifestExporter.WriteCsv(csvPath, _currentManifest);
				pipeline.ManifestCsvFilePath = csvPath;
				step.FilesAffected.Add(csvPath);
			}

			step.Warnings.Add("Files: " + _currentManifest.FileCount
				+ ", validation warnings: " + _currentManifest.Warnings.Count
				+ ", errors: " + _currentManifest.Errors.Count
				+ (includeHashes ? ", hashes included" : ", hashes omitted"));

			step.MarkFinished(true, BuildSeverity.Info);
		}

		private void _stepGenerateChangelog(BuildPipelineOptions options, BuildPipelineResult pipeline) {
			var step = pipeline.GetStep(StepGenerateChangelog);

			if (_currentManifest == null) {
				bool includeHashes = options.GenerateHashes && _fileHashes.Count > 0;
				_currentManifest = BuildPipelineArtifacts.CreateManifest(
					options.Grf,
					options.ProfileName,
					pipeline.ValidationResult,
					includeHashes,
					includeHashes ? _fileHashes : null);
			}

			BuildManifestDocument previous = null;
			string previousPath = options.PreviousManifestPath;

			if (!String.IsNullOrWhiteSpace(previousPath)) {
				previous = ManifestComparisonService.LoadFromFile(previousPath);

				if (previous == null)
					step.Warnings.Add("Could not load previous manifest: " + previousPath);
			}

			string currentManifestPath = pipeline.ManifestFilePath
				?? Path.Combine(pipeline.OutputDirectory, BuildPipelineArtifacts.ManifestFileName);

			var changelog = ManifestComparisonService.Compare(
				_currentManifest,
				previous,
				previousPath,
				currentManifestPath);

			string jsonPath = Path.Combine(pipeline.OutputDirectory, BuildPipelineArtifacts.ChangelogFileName);
			BuildPipelineArtifacts.WriteJson(jsonPath, changelog);
			pipeline.ChangelogFilePath = jsonPath;
			step.FilesAffected.Add(jsonPath);

			string txtPath = Path.Combine(pipeline.OutputDirectory, BuildChangelogExporter.ChangelogTxtFileName);
			BuildChangelogExporter.WriteTxt(txtPath, changelog);
			pipeline.ChangelogTextPath = txtPath;
			step.FilesAffected.Add(txtPath);

			if (!changelog.HasPreviousManifest)
				step.Warnings.Add("No previous manifest selected; changelog has no comparison data.");
			else {
				step.Warnings.Add(
					"Added: " + changelog.Added.Count
					+ ", Removed: " + changelog.Removed.Count
					+ ", Changed: " + changelog.Changed.Count
					+ ", Unchanged: " + changelog.Unchanged.Count
					+ ", Same hash/different path: " + changelog.SameHashDifferentPath.Count);
			}

			step.MarkFinished(true, BuildSeverity.Info);
		}

		private void _stepGenerateHashes(
			BuildPipelineOptions options,
			BuildPipelineResult pipeline,
			IProgress progress,
			CancellationToken cancellationToken) {
			var step = pipeline.GetStep(StepGenerateHashes);
			_fileHashes.Clear();

			var md5 = new Md5Hash();
			var sha1 = new BuildPipelineSha1Hash();
			var doc = new BuildHashesDocument {
				GrfFileName = options.Grf.FileName,
				GeneratedAtUtc = DateTime.UtcNow,
				Entries = new List<BuildHashEntry>(),
			};

			var entries = options.Grf.FileTable.Entries.Where(e => !e.IsRemoved).ToList();
			int total = entries.Count;
			int processed = 0;

			foreach (var entry in entries) {
				ThrowIfCancelled(cancellationToken);

				try {
					string relativePath = GrfPath.CleanGrfPath(entry.RelativePath);
					byte[] data = entry.GetDecompressedData();
					string md5Value = md5.ComputeHash(data);
					string sha1Value = sha1.ComputeHash(data);

					var hashes = new BuildPipelineFileHashes {
						Md5 = md5Value,
						Sha1 = sha1Value,
					};

					_fileHashes[relativePath] = hashes;

					doc.Entries.Add(new BuildHashEntry {
						RelativePath = relativePath,
						Md5 = md5Value,
						Sha1 = sha1Value,
					});
				}
				catch (Exception ex) {
					step.Warnings.Add("Hash failed for " + entry.RelativePath + ": " + ex.Message);
				}

				processed++;
				if (progress != null && total > 0 && processed % 50 == 0)
					progress.Progress = (float)processed / total * 100f;
			}

			if (progress != null)
				AProgress.Finalize(progress);

			doc.Entries = doc.Entries.OrderBy(e => e.RelativePath, StringComparer.OrdinalIgnoreCase).ToList();
			string path = Path.Combine(pipeline.OutputDirectory, BuildPipelineArtifacts.HashesFileName);
			BuildPipelineArtifacts.WriteJson(path, doc);

			pipeline.HashesFilePath = path;
			step.FilesAffected.Add(path);
			step.Warnings.Add("Hashed " + doc.Entries.Count + " of " + total + " file(s) (MD5 + SHA1, compressed data).");
			step.MarkFinished(true, BuildSeverity.Info);
		}

		private static void _stepExportReport(BuildPipelineResult pipeline) {
			var step = pipeline.GetStep(StepExportBuildReport);

			BuildPipelineReportWriter.Export(pipeline, pipeline.OutputDirectory);
			step.FilesAffected.Add(pipeline.ReportJsonPath);
			step.FilesAffected.Add(pipeline.ReportTextPath);
			step.MarkFinished(true, BuildSeverity.Info);
		}

		private void _refreshValidationViews(GrfHolder grf) {
			var validation = _validationService.Validate(grf);
			_activeValidationViews = validation.Issues
				.Where(issue => !RagnarokValidationIgnoredRules.IsIgnored(issue))
				.Select(issue => new RagnarokValidationView(issue))
				.ToList();
		}

		private static bool _validationHasBlockingIssues(BuildPipelineResult pipeline) {
			if (pipeline.ValidationResult == null)
				return false;

			return pipeline.ValidationResult.Issues
				.Where(issue => !RagnarokValidationIgnoredRules.IsIgnored(issue))
				.Any(issue =>
					issue.Severity == RagnarokValidationSeverity.Critical
					|| issue.Severity == RagnarokValidationSeverity.Error);
		}

		private static BuildSeverity _aggregateSeverity(IEnumerable<BuildStepResult> steps) {
			var max = BuildSeverity.Info;
			foreach (var step in steps) {
				if (step.Severity > max)
					max = step.Severity;
			}
			return max;
		}

		private static BuildPipelineResult _finish(
			BuildPipelineResult pipeline,
			bool success,
			BuildSeverity severity,
			string message) {
			if (!String.IsNullOrEmpty(message))
				pipeline.Messages.Add(message);

			pipeline.MarkCompleted(success, severity);
			return pipeline;
		}

		private static void ThrowIfCancelled(CancellationToken cancellationToken) {
			if (cancellationToken.IsCancellationRequested)
				throw new OperationCanceledException();
		}
	}
}
