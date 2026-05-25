using System;
using System.Collections.Generic;
using System.Linq;
using GRF.ContainerFormat.Commands;
using GRF.Core;
using GRF.IO;

namespace GRFEditor.Core.RagnarokValidation {
	public sealed class RagnarokValidationAutoFixResult {
		public int RemovedCount { get; set; }
		public int RenamedCount { get; set; }
		public int SkippedCount { get; set; }
		public List<string> Messages { get; } = new List<string>();
	}

	public class RagnarokValidationAutoFixService {
		public RagnarokValidationAutoFixResult Apply(
			GrfHolder grf,
			IEnumerable<RagnarokValidationView> selectedViews) {
			var result = new RagnarokValidationAutoFixResult();

			if (grf == null || !grf.IsOpened)
				throw new InvalidOperationException("No GRF is opened.");

			var fixes = selectedViews
				.Where(p => p != null && p.IsSelectedForFix && p.Issue != null && p.Issue.CanAutoFix)
				.ToList();

			if (fixes.Count == 0)
				return result;

			var removePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			var renames = new List<Tuple<string, string>>();

			foreach (var view in fixes) {
				string path = view.Issue.RelativePath;
				if (String.IsNullOrEmpty(path)) {
					result.SkippedCount++;
					continue;
				}

				switch (view.Issue.FixKind) {
					case RagnarokValidationFixKind.RemoveJunkFile:
					case RagnarokValidationFixKind.RemoveEmptyFile:
						if (grf.FileTable.ContainsFile(path))
							removePaths.Add(path);
						else
							result.SkippedCount++;
						break;

					case RagnarokValidationFixKind.NormalizePathSlashes:
						string normalized = _normalizeSlashes(path);
						if (String.Equals(path, normalized, StringComparison.OrdinalIgnoreCase)) {
							result.SkippedCount++;
							break;
						}

						if (!grf.FileTable.ContainsFile(path)) {
							result.SkippedCount++;
							break;
						}

						if (grf.FileTable.ContainsFile(normalized)) {
							result.SkippedCount++;
							result.Messages.Add("Skipped rename (target already exists): " + normalized);
							break;
						}

						renames.Add(Tuple.Create(path, normalized));
						break;

					default:
						result.SkippedCount++;
						break;
				}
			}

			foreach (var pair in renames) {
				grf.Commands.Rename(pair.Item1, pair.Item2);
				result.RenamedCount++;
			}

			if (removePaths.Count > 0) {
				grf.Commands.RemoveFiles(removePaths);
				result.RemovedCount = removePaths.Count;
			}

			return result;
		}

		public static string BuildConfirmationSummary(IEnumerable<RagnarokValidationView> selectedViews) {
			int junk = 0;
			int empty = 0;
			int slashes = 0;

			foreach (var view in selectedViews.Where(p => p != null && p.IsSelectedForFix && p.Issue != null && p.Issue.CanAutoFix)) {
				switch (view.Issue.FixKind) {
					case RagnarokValidationFixKind.RemoveJunkFile:
						junk++;
						break;
					case RagnarokValidationFixKind.RemoveEmptyFile:
						empty++;
						break;
					case RagnarokValidationFixKind.NormalizePathSlashes:
						slashes++;
						break;
				}
			}

			var parts = new List<string>();
			if (junk > 0)
				parts.Add(junk + " junk/system file(s) to remove");
			if (empty > 0)
				parts.Add(empty + " empty file(s) to remove");
			if (slashes > 0)
				parts.Add(slashes + " path(s) to normalize (slashes)");

			return String.Join(", ", parts.ToArray());
		}

		private static string _normalizeSlashes(string path) {
			return (path ?? "").Replace('/', '\\');
		}
	}
}
