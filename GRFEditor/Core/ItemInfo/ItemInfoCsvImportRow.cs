namespace GRFEditor.Core.ItemInfo {
	public sealed class ItemInfoCsvImportRow {
		public ItemInfoCsvImportRow(ItemInfoEntry entry, int lineNumber) {
			Entry = entry ?? new ItemInfoEntry();
			LineNumber = lineNumber;
			Selected = true;
		}

		public ItemInfoEntry Entry { get; }

		public int LineNumber { get; }

		private bool _selected = true;

		public bool Selected {
			get { return _selected; }
			set { _selected = CanApply && value; }
		}

		public bool DuplicateItemIdInCsv { get; set; }

		public bool ExistsInItemInfo { get; set; }

		public bool MissingViewIdInAccessoryId { get; set; }

		public bool CanApply {
			get {
				return Entry != null
					&& Entry.IsValid
					&& !DuplicateItemIdInCsv
					&& !MissingViewIdInAccessoryId
					&& Entry.ItemId > 0
					&& Entry.EffectiveViewId.HasValue
					&& Entry.EffectiveViewId.Value > 0;
			}
		}

		public int ItemId => Entry.ItemId;

		public string IdentifiedDisplayName => Entry.IdentifiedDisplayName;

		public string UnidentifiedDisplayName => Entry.UnidentifiedDisplayName;

		public string IdentifiedResourceName => Entry.IdentifiedResourceName;

		public string UnidentifiedResourceName => Entry.UnidentifiedResourceName;

		public string Description => Entry.IdentifiedDescription;

		public int? SlotCount => Entry.SlotCount;

		public int? ClassNum => Entry.ClassNum;

		public bool IsValid => Entry.IsValid;

		public string IssuesText {
			get {
				return Entry.Issues == null || Entry.Issues.Count == 0
					? ""
					: string.Join("; ", Entry.Issues);
			}
		}

		public string StatusText {
			get {
				if (!CanApply)
					return "Inválida";

				if (ExistsInItemInfo)
					return "Substituir";

				return "Nova";
			}
		}
	}
}
