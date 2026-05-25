using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using GRF.Core;
using GRF.Threading;
using GRFEditor.Tools.CustomAccessory;
using Utilities.Extension;

namespace GRFEditor.Core.RagnarokValidation {
	internal static class RagnarokAccessoryValidation {
		private static readonly Regex ItemInfoViewIdRegex = new Regex(
			@"(?:\bClassNum\b|\bView\b)\s*[=:]\s*(?<id>\d+)",
			RegexOptions.Compiled | RegexOptions.IgnoreCase);

		public static void Validate(
			GrfHolder grf,
			List<KeyValuePair<string, FileEntry>> entries,
			RagnarokValidationResult result,
			IProgress progress,
			CancellationToken cancellationToken,
			ref bool cancelled) {
			var files = RagnarokAccessoryGrfLocator.Locate(grf);

			if (!files.HasAny)
				return;

			var sprPaths = _buildSpritePathIndex(entries);
			var constantToSprite = _buildConstantToSpriteMap(sprPaths);

			if (!String.IsNullOrEmpty(files.AccessoryIdPath))
				_validateAccessoryIdFile(grf, files, sprPaths, constantToSprite, result, cancellationToken, ref cancelled);

			if (!String.IsNullOrEmpty(files.AccnamePath))
				_validateAccnameFile(grf, files, result, cancellationToken, ref cancelled);

			if (files.HasAccessoryPair)
				_validateCrossReferences(grf, files, constantToSprite, result, cancellationToken, ref cancelled);

			if (!String.IsNullOrEmpty(files.ItemInfoLuaPath))
				_validateItemInfoFile(grf, files.ItemInfoLuaPath, files.AccessoryIdPath, result, cancellationToken, ref cancelled);

			if (!String.IsNullOrEmpty(files.ItemInfoLubPath))
				_validateItemInfoFile(grf, files.ItemInfoLubPath, files.AccessoryIdPath, result, cancellationToken, ref cancelled);

			if (progress != null)
				progress.Progress = Math.Min(progress.Progress + 1f, 99f);
		}

		private static HashSet<string> _buildSpritePathIndex(List<KeyValuePair<string, FileEntry>> entries) {
			var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

			foreach (var entry in entries) {
				if (entry.Key.IsExtension(".spr"))
					set.Add(entry.Key.Replace('/', '\\'));
			}

			return set;
		}

		private static Dictionary<string, List<string>> _buildConstantToSpriteMap(HashSet<string> sprPaths) {
			var map = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

			foreach (var sprPath in sprPaths) {
				string constant = CustomAccessoryNaming.FromSpritePath(sprPath);

				if (!map.ContainsKey(constant))
					map[constant] = new List<string>();

				map[constant].Add(sprPath);
			}

			return map;
		}

		private static void _validateAccessoryIdFile(
			GrfHolder grf,
			RagnarokAccessoryGrfLocator.AccessoryGrfFiles files,
			HashSet<string> sprPaths,
			Dictionary<string, List<string>> constantToSprite,
			RagnarokValidationResult result,
			CancellationToken cancellationToken,
			ref bool cancelled) {
			string text = RagnarokAccessoryLuaParser.ReadEntryText(grf, files.AccessoryIdPath);
			if (text == null)
				return;

			var parsed = RagnarokAccessoryLuaParser.ParseAccessoryId(text);
			var constantLine = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
			var viewIdOwners = new Dictionary<int, List<string>>();

			int lineNumber = 0;
			foreach (var line in RagnarokAccessoryLuaParser.EnumerateLines(text)) {
				ThrowIfCancelled(cancellationToken, ref cancelled);
				lineNumber++;

				var trimmed = line.Trim();
				if (RagnarokAccessoryLuaParser.IsSkippableLine(trimmed))
					continue;

				if (RagnarokAccessoryLuaParser.IsAccessoryIdLineMatch(line)) {
					var match = CustomAccessoryLuaPatterns.AccessoryIdLineRegex.Match(line);
					string name = CustomAccessoryNaming.NormalizeConstantName(match.Groups["name"].Value);
					int id = Int32.Parse(match.Groups["id"].Value);

					if (constantLine.ContainsKey(name)) {
						result.Add(_issue(
							RagnarokValidationSeverity.Error,
							files.AccessoryIdPath,
							String.Format(RagnarokValidationStrings.AccessoryDuplicateConstant, name, files.AccessoryIdPath),
							RagnarokValidationStrings.FixReviewLuaLine,
							lineNumber,
							new[] { name }));
					}
					else {
						constantLine[name] = lineNumber;
					}

					if (!viewIdOwners.ContainsKey(id))
						viewIdOwners[id] = new List<string>();

					viewIdOwners[id].Add(name);
					continue;
				}

				string badConstant;
				string rawId;
				if (RagnarokAccessoryLuaParser.TryParseNonNumericAssignment(line, out badConstant, out rawId)) {
					result.Add(_issue(
						RagnarokValidationSeverity.Warning,
						files.AccessoryIdPath,
						RagnarokValidationStrings.AccessoryNonNumericId + " (" + badConstant + " = " + rawId + ")",
						RagnarokValidationStrings.FixReviewLuaLine,
						lineNumber));
					continue;
				}

				if (RagnarokAccessoryLuaParser.LineLooksLikeAccessoryData(trimmed, true)) {
					result.Add(_issue(
						_severityForSuspiciousLine(parsed),
						files.AccessoryIdPath,
						RagnarokValidationStrings.AccessorySuspiciousLine,
						RagnarokValidationStrings.FixReviewLuaLine,
						lineNumber));
				}
			}

			_reportDuplicateViewIds(files.AccessoryIdPath, viewIdOwners, result);

			if (parsed.AccessoryIdOrder.Count >= 8) {
				int outOfOrder = RagnarokAccessoryLuaParser.CountOutOfOrderSteps(parsed.AccessoryIdOrder);
				if (outOfOrder >= 3) {
					result.Add(_issue(
						RagnarokValidationSeverity.Warning,
						files.AccessoryIdPath,
						RagnarokValidationStrings.AccessoryIdsOutOfOrder,
						RagnarokValidationStrings.FixReviewLuaLine,
						null));
				}
			}

			if (!String.IsNullOrEmpty(files.AccessoryIdPath) && String.IsNullOrEmpty(files.AccnamePath))
				_validateSpritesForConstants(parsed.AccessoryIds.Keys, files.AccessoryIdPath, constantToSprite, result);
		}

		private static void _validateAccnameFile(
			GrfHolder grf,
			RagnarokAccessoryGrfLocator.AccessoryGrfFiles files,
			RagnarokValidationResult result,
			CancellationToken cancellationToken,
			ref bool cancelled) {
			string text = RagnarokAccessoryLuaParser.ReadEntryText(grf, files.AccnamePath);
			if (text == null)
				return;

			var parsed = RagnarokAccessoryLuaParser.ParseAccname(text);
			var constantLine = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

			int lineNumber = 0;
			foreach (var line in RagnarokAccessoryLuaParser.EnumerateLines(text)) {
				ThrowIfCancelled(cancellationToken, ref cancelled);
				lineNumber++;

				var trimmed = line.Trim();
				if (RagnarokAccessoryLuaParser.IsSkippableLine(trimmed))
					continue;

				if (RagnarokAccessoryLuaParser.IsAccnameLineMatch(line)) {
					var match = CustomAccessoryLuaPatterns.AccnameLineRegex.Match(line);
					string name = CustomAccessoryNaming.NormalizeConstantName(match.Groups["name"].Value);

					if (constantLine.ContainsKey(name)) {
						result.Add(_issue(
							RagnarokValidationSeverity.Error,
							files.AccnamePath,
							String.Format(RagnarokValidationStrings.AccessoryDuplicateConstant, name, files.AccnamePath),
							RagnarokValidationStrings.FixReviewLuaLine,
							lineNumber));
					}
					else {
						constantLine[name] = lineNumber;
					}

					continue;
				}

				if (RagnarokAccessoryLuaParser.LineLooksLikeAccessoryData(trimmed, false)) {
					result.Add(_issue(
						_severityForSuspiciousLine(parsed),
						files.AccnamePath,
						RagnarokValidationStrings.AccessorySuspiciousLine,
						RagnarokValidationStrings.FixReviewLuaLine,
						lineNumber));
				}
			}
		}

		private static void _validateCrossReferences(
			GrfHolder grf,
			RagnarokAccessoryGrfLocator.AccessoryGrfFiles files,
			Dictionary<string, List<string>> constantToSprite,
			RagnarokValidationResult result,
			CancellationToken cancellationToken,
			ref bool cancelled) {
			string idText = RagnarokAccessoryLuaParser.ReadEntryText(grf, files.AccessoryIdPath);
			string nameText = RagnarokAccessoryLuaParser.ReadEntryText(grf, files.AccnamePath);
			if (idText == null || nameText == null)
				return;

			var ids = RagnarokAccessoryLuaParser.ParseAccessoryId(idText);
			var names = RagnarokAccessoryLuaParser.ParseAccname(nameText);

			if (ids.AccessoryIds.Count == 0 || names.Accnames.Count == 0) {
				result.Add(_issue(
					RagnarokValidationSeverity.Warning,
					files.AccessoryIdPath ?? files.AccnamePath,
					"Could not parse enough accessoryid/accname entries to compare cross-references.",
					RagnarokValidationStrings.FixReviewLuaLine,
					null));
				return;
			}

			foreach (var constant in ids.AccessoryIds.Keys) {
				ThrowIfCancelled(cancellationToken, ref cancelled);

				if (!names.Accnames.ContainsKey(constant)) {
					result.Add(_issue(
						RagnarokValidationSeverity.Error,
						files.AccessoryIdPath,
						RagnarokValidationStrings.AccessoryMissingAccname + " (" + constant + ")",
						RagnarokValidationStrings.FixSyncAccname,
						null,
						new[] { files.AccnamePath }));
				}
			}

			foreach (var constant in names.Accnames.Keys) {
				ThrowIfCancelled(cancellationToken, ref cancelled);

				if (!ids.AccessoryIds.ContainsKey(constant)) {
					result.Add(_issue(
						RagnarokValidationSeverity.Error,
						files.AccnamePath,
						RagnarokValidationStrings.AccessoryMissingAccessoryId + " (" + constant + ")",
						RagnarokValidationStrings.FixSyncAccessoryId,
						null,
						new[] { files.AccessoryIdPath }));
				}
			}

			_validateSpritesForConstants(ids.AccessoryIds.Keys, files.AccessoryIdPath, constantToSprite, result);
		}

		private static void _validateSpritesForConstants(
			IEnumerable<string> constants,
			string reportPath,
			Dictionary<string, List<string>> constantToSprite,
			RagnarokValidationResult result) {
			foreach (var constant in constants) {
				List<string> sprites;
				if (constantToSprite.TryGetValue(constant, out sprites) && sprites.Count > 0)
					continue;

				result.Add(_issue(
					RagnarokValidationSeverity.Warning,
					reportPath,
					RagnarokValidationStrings.AccessorySpriteNotFound + " (" + constant + ")",
					RagnarokValidationStrings.FixAddSprite,
					null));
			}
		}

		private static void _validateItemInfoFile(
			GrfHolder grf,
			string itemInfoPath,
			string accessoryIdPath,
			RagnarokValidationResult result,
			CancellationToken cancellationToken,
			ref bool cancelled) {
			if (String.IsNullOrEmpty(accessoryIdPath))
				return;

			string itemText = RagnarokAccessoryLuaParser.ReadEntryText(grf, itemInfoPath);
			string idText = RagnarokAccessoryLuaParser.ReadEntryText(grf, accessoryIdPath);
			if (itemText == null || idText == null)
				return;

			var ids = RagnarokAccessoryLuaParser.ParseAccessoryId(idText);
			if (ids.AccessoryIds.Count == 0)
				return;

			var knownViewIds = new HashSet<int>(ids.AccessoryIds.Values);
			var reported = new HashSet<int>();

			foreach (Match match in ItemInfoViewIdRegex.Matches(itemText)) {
				ThrowIfCancelled(cancellationToken, ref cancelled);

				int viewId = Int32.Parse(match.Groups["id"].Value);
				if (knownViewIds.Contains(viewId) || !reported.Add(viewId))
					continue;

				result.Add(_issue(
					RagnarokValidationSeverity.Error,
					itemInfoPath,
					String.Format(RagnarokValidationStrings.AccessoryIteminfoViewId, viewId),
					RagnarokValidationStrings.FixIteminfoViewId,
					null,
					new[] { accessoryIdPath }));
			}

			if (ids.UsedLowConfidenceFallback && ItemInfoViewIdRegex.Matches(itemText).Count == 0) {
				result.Add(_issue(
					RagnarokValidationSeverity.Warning,
					itemInfoPath,
					"iteminfo file found but no ClassNum/View references were detected (format may differ).",
					null,
					null));
			}
		}

		private static void _reportDuplicateViewIds(
			string filePath,
			Dictionary<int, List<string>> viewIdOwners,
			RagnarokValidationResult result) {
			foreach (var pair in viewIdOwners.Where(p => p.Value.Count > 1)) {
				var owners = pair.Value.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
				if (owners.Count < 2)
					continue;

				result.Add(_issue(
					RagnarokValidationSeverity.Error,
					filePath,
					String.Format(
						RagnarokValidationStrings.AccessoryDuplicateViewId,
						pair.Key,
						owners[0],
						owners[1],
						filePath),
					RagnarokValidationStrings.FixReviewLuaLine,
					null,
					owners));
			}
		}

		private static RagnarokValidationSeverity _severityForSuspiciousLine(RagnarokAccessoryParseResult parsed) {
			if (parsed.MatchedAccessoryIdLines == 0 && parsed.MatchedAccnameLines == 0)
				return RagnarokValidationSeverity.Warning;

			return RagnarokValidationSeverity.Warning;
		}

		private static RagnarokValidationIssue _issue(
			RagnarokValidationSeverity severity,
			string relativePath,
			string message,
			string suggestedFix,
			int? lineNumber,
			IEnumerable<string> relatedFiles = null,
			string ruleId = "accessory.data") {
			if (lineNumber.HasValue)
				message = "Line " + lineNumber.Value + ": " + message;

			var issue = new RagnarokValidationIssue {
				RuleId = ruleId,
				Severity = severity,
				Category = RagnarokValidationCategory.AccessoryData,
				RelativePath = relativePath ?? "",
				Message = message,
				SuggestedFix = suggestedFix,
				CanAutoFix = false,
			};

			if (relatedFiles != null) {
				foreach (var file in relatedFiles.Where(p => !String.IsNullOrEmpty(p)))
					issue.RelatedFiles.Add(file);
			}

			return issue;
		}

		private static void ThrowIfCancelled(CancellationToken cancellationToken, ref bool cancelled) {
			if (cancellationToken.IsCancellationRequested) {
				cancelled = true;
				throw new OperationCanceledException();
			}
		}
	}
}
