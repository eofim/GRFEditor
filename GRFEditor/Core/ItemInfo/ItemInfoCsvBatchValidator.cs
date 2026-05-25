using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace GRFEditor.Core.ItemInfo {
	public static class ItemInfoCsvBatchValidator {
		public static void Validate(
			ItemInfoCsvImportResult importResult,
			string itemInfoPath,
			ItemInfoValidationOptions options) {
			if (importResult == null || importResult.Rows.Count == 0)
				return;

			options = options ?? new ItemInfoValidationOptions();

			var existingItemIds = _loadExistingItemIds(itemInfoPath);
			var knownViewIds = options.KnownViewIds;

			importResult.DuplicateItemIdCount = 0;
			importResult.ExistsInItemInfoCount = 0;
			importResult.MissingViewIdCount = 0;

			var duplicateCsvIds = importResult.Rows
				.Where(r => r.Entry.ItemId > 0)
				.GroupBy(r => r.Entry.ItemId)
				.Where(g => g.Count() > 1)
				.Select(g => g.Key)
				.ToHashSet();

			foreach (var row in importResult.Rows) {
				row.Entry.Issues.Clear();
				row.DuplicateItemIdInCsv = row.Entry.ItemId > 0 && duplicateCsvIds.Contains(row.Entry.ItemId);
				row.ExistsInItemInfo = row.Entry.ItemId > 0 && existingItemIds.Contains(row.Entry.ItemId);
				row.MissingViewIdInAccessoryId = false;

				if (row.DuplicateItemIdInCsv) {
					importResult.DuplicateItemIdCount++;
					row.Entry.AddIssue("ItemId duplicado no CSV.");
				}

				if (row.ExistsInItemInfo)
					importResult.ExistsInItemInfoCount++;

				if (row.Entry.ItemId <= 0)
					row.Entry.AddIssue("ItemId inválido.");

				if (!row.Entry.EffectiveViewId.HasValue || row.Entry.EffectiveViewId.Value <= 0)
					row.Entry.AddIssue("ClassNum / ViewId inválido ou ausente.");
				else if (knownViewIds != null && knownViewIds.Count > 0
					&& !knownViewIds.Contains(row.Entry.EffectiveViewId.Value)) {
					row.MissingViewIdInAccessoryId = true;
					importResult.MissingViewIdCount++;
					row.Entry.AddIssue("ClassNum " + row.Entry.EffectiveViewId.Value + " não existe em accessoryid.lub.");
				}

				if (string.IsNullOrWhiteSpace(row.Entry.IdentifiedDisplayName)
					&& string.IsNullOrWhiteSpace(row.Entry.UnidentifiedDisplayName))
					row.Entry.AddIssue("DisplayName ausente.");

				row.Entry.RecomputeValidity();

				if (row.ExistsInItemInfo && row.CanApply)
					row.Entry.Issues.Add("ItemId já existe no iteminfo (bloco será substituído).");
			}

			var entries = importResult.Rows.Select(r => r.Entry).ToList();
			var summary = ItemInfoService.ValidateEntries(entries, options);

			foreach (string issue in summary.GlobalIssues)
				importResult.Messages.Add(issue);

			foreach (var row in importResult.Rows) {
				if (!row.CanApply)
					row.Selected = false;
				else
					row.Selected = true;
			}
		}

		private static HashSet<int> _loadExistingItemIds(string itemInfoPath) {
			var set = new HashSet<int>();
			if (string.IsNullOrWhiteSpace(itemInfoPath) || !File.Exists(itemInfoPath))
				return set;

			var parse = ItemInfoService.ParseFile(itemInfoPath);
			foreach (var entry in parse.Entries) {
				if (entry.ItemId > 0)
					set.Add(entry.ItemId);
			}

			return set;
		}
	}
}
