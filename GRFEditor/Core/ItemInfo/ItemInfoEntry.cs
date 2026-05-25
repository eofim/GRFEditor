using System.Collections.Generic;
using System.Linq;

namespace GRFEditor.Core.ItemInfo {
	public sealed class ItemInfoEntry {
		public int ItemId { get; set; }
		public string IdentifiedDisplayName { get; set; }
		public string UnidentifiedDisplayName { get; set; }
		public string IdentifiedResourceName { get; set; }
		public string UnidentifiedResourceName { get; set; }
		public string IdentifiedDescription { get; set; }
		public string UnidentifiedDescription { get; set; }
		public int? SlotCount { get; set; }
		public int? ClassNum { get; set; }
		public int? CostumeViewId { get; set; }
		public bool Costume { get; set; }

		public bool IsValid { get; set; }
		public List<string> Issues { get; } = new List<string>();

		/// <summary>Texto bruto do bloco Lua, quando disponível após o parse.</summary>
		public string SourceBlock { get; set; }

		public int? EffectiveViewId => CostumeViewId ?? ClassNum;

		public void AddIssue(string message) {
			if (string.IsNullOrWhiteSpace(message))
				return;

			if (!Issues.Contains(message))
				Issues.Add(message);

			IsValid = false;
		}

		public void RecomputeValidity() {
			IsValid = ItemId > 0 && Issues.Count == 0;
		}
	}
}
