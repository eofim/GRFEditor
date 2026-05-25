using System.Collections.Generic;

namespace GRFEditor.Core.ItemInfo {
	public sealed class ItemInfoValidationSummary {
		public int TotalEntries { get; set; }
		public int ValidEntries { get; set; }
		public int InvalidEntries { get; set; }
		public int DuplicateItemIdCount { get; set; }
		public int DuplicateViewIdCount { get; set; }
		public int MissingViewIdCount { get; set; }
		public int MissingTextureCount { get; set; }
		public List<string> GlobalIssues { get; } = new List<string>();
	}
}
