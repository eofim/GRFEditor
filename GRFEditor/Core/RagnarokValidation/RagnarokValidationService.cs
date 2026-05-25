using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GRF.Core;
using GRF.Threading;
using Utilities;
using Utilities.Extension;
using Utilities.Services;

namespace GRFEditor.Core.RagnarokValidation {
	/// <summary>
	/// Read-only validation of GRF contents for Ragnarok Online client compatibility.
	/// Does not modify the container; safe to run on a background thread.
	/// </summary>
	public class RagnarokValidationService {
		private static readonly HashSet<string> JunkFileNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
			"thumbs.db",
			"desktop.ini",
		};

		private static readonly string[] CriticalFolderPrefixes = {
			@"data\sprite\",
			@"data\texture\",
			@"data\model\",
			@"data\wav\",
			@"data\palette\",
			@"data\",
		};

		private static readonly Dictionary<string, HashSet<string>> AllowedExtensionsByFolder =
			new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase) {
				{ @"data\sprite\", new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".spr", ".act", ".bmp", ".pal", ".str" } },
				{ @"data\texture\", new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".bmp", ".tga", ".jpg", ".jpeg", ".png", ".gif", ".spr" } },
				{ @"data\model\", new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".rsm", ".rsm2" } },
				{ @"data\wav\", new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".wav", ".mp3", ".ogg" } },
				{ @"data\palette\", new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".pal", ".bmp" } },
				{ @"data\", new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
					".gat", ".rsw", ".gnd", ".bmp", ".tga", ".spr", ".act", ".rsm", ".rsm2", ".wav", ".pal",
					".lub", ".lua", ".txt", ".xml", ".ini", ".eff", ".str", ".imf", ".fna", ".eot", ".gpf",
					".grf", ".rgz", ".thor", ".json", ".csv", ".scp", ".ase", ".db", ".svn",
				} },
			};

		private readonly char[] _invalidFileNameChars = Path.GetInvalidFileNameChars();

		/// <summary>
		/// Runs validation on a background thread.
		/// </summary>
		public Task<RagnarokValidationResult> ValidateAsync(GrfHolder grf, IProgress progress = null, CancellationToken cancellationToken = default(CancellationToken)) {
			return Task.Run(() => Validate(grf, progress, cancellationToken), cancellationToken);
		}

		/// <summary>
		/// Scans the opened GRF file table and returns issues without modifying the container.
		/// </summary>
		public RagnarokValidationResult Validate(GrfHolder grf, IProgress progress = null, CancellationToken cancellationToken = default(CancellationToken)) {
			var result = new RagnarokValidationResult();

			if (grf == null || !grf.IsOpened) {
				result.Add(new RagnarokValidationIssue {
					RuleId = RagnarokValidationRuleIds.ClientNoGrf,
					Severity = RagnarokValidationSeverity.Error,
					Category = RagnarokValidationCategory.ClientCompatibility,
					RelativePath = "",
					Message = "No GRF is opened.",
				});
				result.MarkCompleted(false);
				return result;
			}

			bool cancelled = false;

			try {
				List<KeyValuePair<string, FileEntry>> entries = grf.FileTable.FastAccessEntries;
				int total = entries.Count;
				int processed = 0;

				ValidateSpritePairing(entries, result);
				ThrowIfCancelled(cancellationToken, progress, ref cancelled);

				ValidateEmptyFiles(entries, result);
				ThrowIfCancelled(cancellationToken, progress, ref cancelled);

				ValidateJunkFiles(entries, result);
				ThrowIfCancelled(cancellationToken, progress, ref cancelled);

				ValidateRootFiles(entries, result);
				ThrowIfCancelled(cancellationToken, progress, ref cancelled);

				ValidatePathQuality(entries, result, ref processed, total, progress, cancellationToken, ref cancelled);
				ThrowIfCancelled(cancellationToken, progress, ref cancelled);

				ValidateDuplicatePaths(entries, result);
				ThrowIfCancelled(cancellationToken, progress, ref cancelled);

				ValidateUnknownExtensionsInCriticalFolders(entries, result, ref processed, total, progress, cancellationToken, ref cancelled);
				ThrowIfCancelled(cancellationToken, progress, ref cancelled);

				RagnarokAccessoryValidation.Validate(grf, entries, result, progress, cancellationToken, ref cancelled);
			}
			catch (OperationCanceledException) {
				cancelled = true;
			}
			finally {
				if (progress != null)
					AProgress.Finalize(progress);

				result.MarkCompleted(cancelled);
			}

			return result;
		}

		private static void ThrowIfCancelled(CancellationToken cancellationToken, IProgress progress, ref bool cancelled) {
			if (cancellationToken.IsCancellationRequested || (progress != null && progress.IsCancelling)) {
				cancelled = true;
				throw new OperationCanceledException();
			}
		}

		private static void UpdateProgress(IProgress progress, int processed, int total) {
			if (progress == null || total <= 0)
				return;

			progress.Progress = (float)processed / total * 100f;
		}

		private void ValidateSpritePairing(List<KeyValuePair<string, FileEntry>> entries, RagnarokValidationResult result) {
			string garmentFolder = EncodingService.FromAnyToDisplayEncoding(@"data\sprite\·Îºê\");

			var actPaths = new HashSet<string>(
				entries.Where(p => !p.Key.Contains(garmentFolder) && p.Key.IsExtension(".act"))
					.Select(p => p.Value.RelativePath.RemoveExtension()),
				StringComparer.OrdinalIgnoreCase);

			var sprPaths = new HashSet<string>(
				entries.Where(p => !p.Key.Contains(garmentFolder) && p.Key.IsExtension(".spr"))
					.Select(p => p.Value.RelativePath.RemoveExtension()),
				StringComparer.OrdinalIgnoreCase);

			foreach (var entry in entries.Where(p => !p.Key.Contains(garmentFolder) && p.Key.IsExtension(".act"))) {
				string basePath = entry.Value.RelativePath.RemoveExtension();

				if (!sprPaths.Contains(basePath)) {
					result.Add(new RagnarokValidationIssue {
						RuleId = RagnarokValidationRuleIds.SpriteMissingSpr,
						Severity = RagnarokValidationSeverity.Error,
						Category = RagnarokValidationCategory.SpritePairing,
						RelativePath = entry.Value.RelativePath,
						Message = RagnarokValidationStrings.MissingSprForAct,
						SuggestedFix = RagnarokValidationStrings.FixAddSpr,
						CanAutoFix = false,
						RelatedFiles = { entry.Value.RelativePath, basePath + ".spr" },
					});
				}
			}

			foreach (var entry in entries.Where(p => !p.Key.Contains(garmentFolder) && p.Key.IsExtension(".spr"))) {
				string basePath = entry.Value.RelativePath.RemoveExtension();

				if (!actPaths.Contains(basePath)) {
					result.Add(new RagnarokValidationIssue {
						RuleId = RagnarokValidationRuleIds.SpriteMissingAct,
						Severity = RagnarokValidationSeverity.Warning,
						Category = RagnarokValidationCategory.SpritePairing,
						RelativePath = entry.Value.RelativePath,
						Message = RagnarokValidationStrings.MissingActForSpr,
						SuggestedFix = RagnarokValidationStrings.FixAddAct,
						CanAutoFix = false,
						RelatedFiles = { entry.Value.RelativePath, basePath + ".act" },
					});
				}
			}
		}

		private static void ValidateEmptyFiles(List<KeyValuePair<string, FileEntry>> entries, RagnarokValidationResult result) {
			string emptySizeString = Methods.FileSizeToString(0);

			foreach (var entry in entries.Where(p => p.Value.DisplaySize == emptySizeString || p.Value.NewSizeDecompressed == 0)) {
				result.Add(new RagnarokValidationIssue {
					RuleId = RagnarokValidationRuleIds.FileEmpty,
					Severity = RagnarokValidationSeverity.Warning,
					Category = RagnarokValidationCategory.EmptyFile,
					RelativePath = entry.Value.RelativePath,
					Message = RagnarokValidationStrings.EmptyFile,
					SuggestedFix = RagnarokValidationStrings.FixRemoveFile,
					CanAutoFix = true,
					FixKind = RagnarokValidationFixKind.RemoveEmptyFile,
				});
			}
		}

		private static void ValidateJunkFiles(List<KeyValuePair<string, FileEntry>> entries, RagnarokValidationResult result) {
			foreach (var entry in entries) {
				string path = entry.Key;
				string fileName = Path.GetFileName(path);

				if (fileName.IsExtension(".db")) {
					result.Add(_junkIssue(entry.Value.RelativePath, RagnarokValidationStrings.JunkDb, RagnarokValidationSeverity.Warning, RagnarokValidationRuleIds.JunkDb));
					continue;
				}

				if (path.IsExtension(".svn") || path.IndexOf("\\.svn\\", StringComparison.OrdinalIgnoreCase) >= 0) {
					result.Add(_junkIssue(entry.Value.RelativePath, RagnarokValidationStrings.JunkSvn, RagnarokValidationSeverity.Warning, RagnarokValidationRuleIds.JunkSvn));
					continue;
				}

				if (JunkFileNames.Contains(fileName)) {
					string message = fileName.Equals("thumbs.db", StringComparison.OrdinalIgnoreCase)
						? RagnarokValidationStrings.JunkThumbsDb
						: RagnarokValidationStrings.JunkDesktopIni;
					result.Add(_junkIssue(entry.Value.RelativePath, message, RagnarokValidationSeverity.Warning, RagnarokValidationRuleIds.JunkSystemFile));
				}
			}
		}

		private static RagnarokValidationIssue _junkIssue(string relativePath, string message, RagnarokValidationSeverity severity, string ruleId) {
			return new RagnarokValidationIssue {
				RuleId = ruleId,
				Severity = severity,
				Category = RagnarokValidationCategory.JunkFile,
				RelativePath = relativePath,
				Message = message,
				SuggestedFix = RagnarokValidationStrings.FixRemoveFile,
				CanAutoFix = true,
				FixKind = RagnarokValidationFixKind.RemoveJunkFile,
			};
		}

		private static void ValidateRootFiles(List<KeyValuePair<string, FileEntry>> entries, RagnarokValidationResult result) {
			foreach (var entry in entries.Where(p => _isRootPath(p.Key))) {
				result.Add(new RagnarokValidationIssue {
					RuleId = RagnarokValidationRuleIds.RootUnexpected,
					Severity = RagnarokValidationSeverity.Warning,
					Category = RagnarokValidationCategory.RootPlacement,
					RelativePath = entry.Value.RelativePath,
					Message = RagnarokValidationStrings.RootFile,
					SuggestedFix = RagnarokValidationStrings.FixMoveToData,
					CanAutoFix = false,
				});
			}
		}

		private static bool _isRootPath(string relativePath) {
			if (String.IsNullOrEmpty(relativePath))
				return true;

			string directory = Path.GetDirectoryName(relativePath);
			return String.IsNullOrEmpty(directory);
		}

		private void ValidatePathQuality(
			List<KeyValuePair<string, FileEntry>> entries,
			RagnarokValidationResult result,
			ref int processed,
			int total,
			IProgress progress,
			CancellationToken cancellationToken,
			ref bool cancelled) {
			foreach (var entry in entries) {
				if (cancellationToken.IsCancellationRequested) {
					cancelled = true;
					cancellationToken.ThrowIfCancellationRequested();
				}

				string path = entry.Value.RelativePath;

				if (_hasOnlySlashNormalizationIssue(path)) {
					result.Add(new RagnarokValidationIssue {
						RuleId = RagnarokValidationRuleIds.PathBackslash,
						Severity = RagnarokValidationSeverity.Warning,
						Category = RagnarokValidationCategory.PathQuality,
						RelativePath = path,
						Message = RagnarokValidationStrings.PathSlashNormalization,
						SuggestedFix = RagnarokValidationStrings.FixNormalizeSlashes,
						CanAutoFix = true,
						FixKind = RagnarokValidationFixKind.NormalizePathSlashes,
					});
				}
				else if (_hasSuspiciousPathCharacters(path)) {
					result.Add(new RagnarokValidationIssue {
						RuleId = RagnarokValidationRuleIds.PathInvalidChars,
						Severity = RagnarokValidationSeverity.Warning,
						Category = RagnarokValidationCategory.PathQuality,
						RelativePath = path,
						Message = RagnarokValidationStrings.SuspiciousPathChars,
						SuggestedFix = RagnarokValidationStrings.FixRenamePath,
						CanAutoFix = false,
						FixKind = RagnarokValidationFixKind.None,
					});
				}

				processed++;
				if (processed % 200 == 0)
					UpdateProgress(progress, processed, total * 2);
			}
		}

		private static bool _hasOnlySlashNormalizationIssue(string path) {
			if (String.IsNullOrEmpty(path) || path.IndexOf('/') < 0)
				return false;

			return !_hasSuspiciousPathCharactersExceptSlashes(path);
		}

		private static bool _hasSuspiciousPathCharactersExceptSlashes(string path) {
			if (path.IndexOf('?') >= 0)
				return true;

			if (path.IndexOf("//", StringComparison.Ordinal) >= 0 || path.IndexOf("\\\\", StringComparison.Ordinal) >= 0)
				return true;

			foreach (char c in path) {
				if (c < 32 || c == 127)
					return true;

				if (Array.IndexOf(Path.GetInvalidFileNameChars(), c) >= 0)
					return true;

				if (c == '*' || c == '<' || c == '>' || c == '|' || c == '"')
					return true;
			}

			string fileName = Path.GetFileName(path);
			if (!String.IsNullOrEmpty(fileName) && (fileName.StartsWith(" ") || fileName.EndsWith(" ")))
				return true;

			return false;
		}

		private bool _hasSuspiciousPathCharacters(string path) {
			if (String.IsNullOrEmpty(path))
				return false;

			if (path.IndexOf('?') >= 0)
				return true;

			if (path.IndexOf("//", StringComparison.Ordinal) >= 0 || path.IndexOf("\\\\", StringComparison.Ordinal) >= 0)
				return true;

			foreach (char c in path) {
				if (c < 32 || c == 127)
					return true;

				if (Array.IndexOf(_invalidFileNameChars, c) >= 0)
					return true;

				if (c == '*' || c == '<' || c == '>' || c == '|' || c == '"')
					return true;
			}

			string fileName = Path.GetFileName(path);
			if (!String.IsNullOrEmpty(fileName) && (fileName.StartsWith(" ") || fileName.EndsWith(" ")))
				return true;

			return false;
		}

		private static void ValidateDuplicatePaths(List<KeyValuePair<string, FileEntry>> entries, RagnarokValidationResult result) {
			var duplicateGroups = entries
				.GroupBy(e => e.Value.RelativePath, StringComparer.OrdinalIgnoreCase)
				.Where(g => g.Count() > 1);

			foreach (var group in duplicateGroups) {
				var paths = group.Select(p => p.Value.RelativePath).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
				string displayPath = paths.FirstOrDefault() ?? group.Key;

				result.Add(new RagnarokValidationIssue {
					RuleId = RagnarokValidationRuleIds.PathDuplicate,
					Severity = RagnarokValidationSeverity.Critical,
					Category = RagnarokValidationCategory.DuplicateEntry,
					RelativePath = displayPath,
					Message = String.Format(RagnarokValidationStrings.DuplicatePath, group.Count()),
					SuggestedFix = RagnarokValidationStrings.FixDeduplicate,
					CanAutoFix = false,
					RelatedFiles = paths,
				});
			}

			var duplicateKeys = entries
				.GroupBy(e => e.Key, StringComparer.OrdinalIgnoreCase)
				.Where(g => g.Count() > 1 && !g.All(e => String.Equals(e.Value.RelativePath, g.Key, StringComparison.OrdinalIgnoreCase)));

			foreach (var group in duplicateKeys) {
				if (result.Issues.Any(p => p.Category == RagnarokValidationCategory.DuplicateEntry &&
				                            String.Equals(p.RelativePath, group.Key, StringComparison.OrdinalIgnoreCase)))
					continue;

				result.Add(new RagnarokValidationIssue {
					RuleId = RagnarokValidationRuleIds.PathDuplicate,
					Severity = RagnarokValidationSeverity.Critical,
					Category = RagnarokValidationCategory.DuplicateEntry,
					RelativePath = group.Key,
					Message = String.Format(RagnarokValidationStrings.DuplicatePath, group.Count()),
					SuggestedFix = RagnarokValidationStrings.FixDeduplicate,
					CanAutoFix = false,
					RelatedFiles = group.Select(p => p.Value.RelativePath).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
				});
			}
		}

		private void ValidateUnknownExtensionsInCriticalFolders(
			List<KeyValuePair<string, FileEntry>> entries,
			RagnarokValidationResult result,
			ref int processed,
			int total,
			IProgress progress,
			CancellationToken cancellationToken,
			ref bool cancelled) {
			foreach (var entry in entries) {
				if (cancellationToken.IsCancellationRequested) {
					cancelled = true;
					cancellationToken.ThrowIfCancellationRequested();
				}

				string path = entry.Value.RelativePath;
				string extension = Path.GetExtension(path);

				if (String.IsNullOrEmpty(extension))
					continue;

				string normalized = path.Replace("/", "\\");

				if (!_tryGetCriticalFolder(normalized, out string folderPrefix, out HashSet<string> allowed))
					continue;

				if (allowed.Contains(extension))
					continue;

				result.Add(new RagnarokValidationIssue {
					RuleId = RagnarokValidationRuleIds.ExtensionUnknown,
					Severity = RagnarokValidationSeverity.Info,
					Category = RagnarokValidationCategory.ClientCompatibility,
					RelativePath = path,
					Message = String.Format(RagnarokValidationStrings.UnknownExtensionInFolder, extension, folderPrefix),
					SuggestedFix = RagnarokValidationStrings.FixUseKnownExtension,
					CanAutoFix = false,
				});

				processed++;
				if (processed % 200 == 0)
					UpdateProgress(progress, total + processed, total * 2);
			}
		}

		private static bool _tryGetCriticalFolder(string normalizedPath, out string folderPrefix, out HashSet<string> allowed) {
			folderPrefix = null;
			allowed = null;

			foreach (string prefix in CriticalFolderPrefixes.OrderByDescending(p => p.Length)) {
				if (normalizedPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) {
					folderPrefix = prefix;
					return AllowedExtensionsByFolder.TryGetValue(prefix, out allowed);
				}
			}

			return false;
		}
	}
}
