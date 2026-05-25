using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using GRF.Core;
using GRF.IO;
using GRF.Threading;

namespace GRFEditor.Core.GrfCompare {
	public sealed class GrfCompareFileExportService {
		private const string FilesSubfolder = "files";
		private const string RemovedListFileName = "removed-files.txt";

		public GrfCompareFileExportResult Export(
			GrfHolder grfOld,
			GrfHolder grfNew,
			GrfCompareResult compareResult,
			IReadOnlyList<GrfCompareView> views,
			GrfCompareFileExportMode mode,
			string destinationFolder,
			bool allowOverwrite,
			IProgress progress = null,
			CancellationToken cancellationToken = default(CancellationToken)) {
			if (String.IsNullOrWhiteSpace(destinationFolder))
				throw new ArgumentException("Destination folder is required.", nameof(destinationFolder));

			if (compareResult == null || views == null)
				throw new InvalidOperationException("Run compare before exporting files.");

			Directory.CreateDirectory(destinationFolder);

			var result = new GrfCompareFileExportResult {
				Mode = mode,
				DestinationPath = Path.GetFullPath(destinationFolder),
			};

			var fileJobs = _collectFileJobs(mode, views).ToList();

			if (fileJobs.Count > 0) {
				string filesRoot = mode == GrfCompareFileExportMode.FullDifferenceReport
					? Path.Combine(destinationFolder, FilesSubfolder)
					: destinationFolder;

				Directory.CreateDirectory(filesRoot);

				var plannedPaths = fileJobs
					.Select(j => _toOutputPath(filesRoot, j.OutputRelativePath))
					.ToList();

				if (!allowOverwrite) {
					foreach (string path in plannedPaths.Where(File.Exists)) {
						if (!result.IgnoredFiles.Contains(path))
							result.IgnoredFiles.Add(path);
					}
				}

				int total = fileJobs.Count;
				int index = 0;

				foreach (var job in fileJobs) {
					ThrowIfCancelled(cancellationToken);
					_reportProgress(progress, ++index, total);

					string outputPath = _toOutputPath(filesRoot, job.OutputRelativePath);

					if (!allowOverwrite && File.Exists(outputPath))
						continue;

					try {
						FileEntry entry = _tryGetEntry(grfNew, job.SourceRelativePath);

						if (entry == null) {
							result.Errors.Add("Not found in GRF: " + job.SourceRelativePath);
							continue;
						}

						entry.ExtractFromAbsolute(outputPath);
						result.ExportedCount++;
					}
					catch (Exception ex) {
						result.Errors.Add(job.SourceRelativePath + ": " + ex.Message);
					}
				}
			}

			_writeReports(mode, destinationFolder, compareResult, views, result, cancellationToken);

			return result;
		}

		public static IReadOnlyList<string> GetPlannedOutputPaths(
			GrfCompareFileExportMode mode,
			IReadOnlyList<GrfCompareView> views,
			string destinationFolder) {
			if (String.IsNullOrWhiteSpace(destinationFolder))
				return Array.Empty<string>();

			var paths = new List<string>();
			string filesRoot = mode == GrfCompareFileExportMode.FullDifferenceReport
				? Path.Combine(destinationFolder, FilesSubfolder)
				: destinationFolder;

			foreach (var job in _collectFileJobs(mode, views))
				paths.Add(_toOutputPath(filesRoot, job.OutputRelativePath));

			foreach (string reportPath in _plannedReportPaths(mode, destinationFolder))
				paths.Add(reportPath);

			return paths;
		}

		private static IEnumerable<string> _plannedReportPaths(GrfCompareFileExportMode mode, string destinationFolder) {
			if (mode == GrfCompareFileExportMode.RemovedFilesList) {
				yield return Path.Combine(destinationFolder, RemovedListFileName);
				yield break;
			}

			if (mode != GrfCompareFileExportMode.FullDifferenceReport)
				yield break;

			yield return Path.Combine(destinationFolder, RemovedListFileName);
			yield return Path.Combine(destinationFolder, "grf-compare-report.csv");
			yield return Path.Combine(destinationFolder, "grf-compare-report.json");
			yield return Path.Combine(destinationFolder, "grf-compare-report.txt");
		}

		private static void _writeReports(
			GrfCompareFileExportMode mode,
			string destinationFolder,
			GrfCompareResult compareResult,
			IReadOnlyList<GrfCompareView> views,
			GrfCompareFileExportResult result,
			CancellationToken cancellationToken) {
			if (mode == GrfCompareFileExportMode.RemovedFilesList) {
				string path = Path.Combine(destinationFolder, RemovedListFileName);
				GrfCompareExport.WriteUtf8(path, GrfCompareExport.ToRemovedFilesList(compareResult, views));
				result.WrittenReportFiles.Add(path);
				return;
			}

			if (mode != GrfCompareFileExportMode.FullDifferenceReport)
				return;

			ThrowIfCancelled(cancellationToken);

			string csvPath = Path.Combine(destinationFolder, "grf-compare-report.csv");
			GrfCompareExport.WriteUtf8(csvPath, GrfCompareExport.ToCsv(views));
			result.WrittenReportFiles.Add(csvPath);

			string jsonPath = Path.Combine(destinationFolder, "grf-compare-report.json");
			GrfCompareExport.WriteUtf8(jsonPath, GrfCompareExport.ToJson(compareResult, views));
			result.WrittenReportFiles.Add(jsonPath);

			string txtPath = Path.Combine(destinationFolder, "grf-compare-report.txt");
			GrfCompareExport.WriteUtf8(txtPath, GrfCompareExport.ToFullDifferenceReport(compareResult, views));
			result.WrittenReportFiles.Add(txtPath);

			string removedPath = Path.Combine(destinationFolder, RemovedListFileName);
			GrfCompareExport.WriteUtf8(removedPath, GrfCompareExport.ToRemovedFilesList(compareResult, views));
			result.WrittenReportFiles.Add(removedPath);
		}

		private static IEnumerable<GrfFileExportJob> _collectFileJobs(
			GrfCompareFileExportMode mode,
			IReadOnlyList<GrfCompareView> views) {
			switch (mode) {
				case GrfCompareFileExportMode.AddedFiles:
					return views
						.Where(v => v.StatusValue == GrfCompareStatus.Added)
						.Select(v => GrfFileExportJob.FromNewGrf(v));

				case GrfCompareFileExportMode.ModifiedFiles:
					return views
						.Where(v => v.StatusValue == GrfCompareStatus.Modified
							|| v.StatusValue == GrfCompareStatus.SamePathDifferentContent)
						.Select(v => GrfFileExportJob.FromNewGrf(v));

				case GrfCompareFileExportMode.AddedAndModifiedFiles:
					return views
						.Where(v => v.StatusValue == GrfCompareStatus.Added
							|| v.StatusValue == GrfCompareStatus.Modified
							|| v.StatusValue == GrfCompareStatus.SamePathDifferentContent
							|| v.StatusValue == GrfCompareStatus.SameContentDifferentPath)
						.Select(v => GrfFileExportJob.FromNewGrf(v));

				case GrfCompareFileExportMode.RemovedFilesList:
					return Enumerable.Empty<GrfFileExportJob>();

				case GrfCompareFileExportMode.FullDifferenceReport:
					return views
						.Where(v => v.StatusValue != GrfCompareStatus.Same
							&& v.StatusValue != GrfCompareStatus.Removed)
						.Select(v => GrfFileExportJob.FromNewGrf(v));

				default:
					return Enumerable.Empty<GrfFileExportJob>();
			}
		}

		private static FileEntry _tryGetEntry(GrfHolder grf, string relativePath) {
			string cleanPath = GrfPath.CleanGrfPath(relativePath);

			if (String.IsNullOrWhiteSpace(cleanPath))
				return null;

			if (grf.FileTable.ContainsFile(cleanPath))
				return grf.FileTable[cleanPath];

			foreach (var entry in grf.FileTable.Entries) {
				if (entry.IsRemoved)
					continue;

				if (String.Equals(GrfPath.CleanGrfPath(entry.RelativePath), cleanPath, StringComparison.OrdinalIgnoreCase))
					return entry;
			}

			return null;
		}

		private static string _toOutputPath(string root, string relativePath) {
			string normalized = GrfPath.CleanGrfPath(relativePath);
			return Path.Combine(root, normalized.Replace('/', Path.DirectorySeparatorChar));
		}

		private static void _reportProgress(IProgress progress, int current, int total) {
			if (progress == null || total <= 0)
				return;

			progress.Progress = Math.Min(99f, current / (float) total * 100f);
		}

		private static void ThrowIfCancelled(CancellationToken cancellationToken) {
			if (cancellationToken.IsCancellationRequested)
				throw new OperationCanceledException();
		}

		private sealed class GrfFileExportJob {
			public string SourceRelativePath { get; set; }
			public string OutputRelativePath { get; set; }

			public static GrfFileExportJob FromNewGrf(GrfCompareView view) {
				string path = !String.IsNullOrWhiteSpace(view.PathInB) ? view.PathInB : view.RelativePath;

				return new GrfFileExportJob {
					SourceRelativePath = path,
					OutputRelativePath = path,
				};
			}
		}
	}
}
