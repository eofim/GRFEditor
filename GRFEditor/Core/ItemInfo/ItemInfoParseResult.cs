using System;
using System.Collections.Generic;
using System.Linq;

namespace GRFEditor.Core.ItemInfo {
	public sealed class ItemInfoParseResult {
		public string SourcePath { get; set; }
		public List<ItemInfoEntry> Entries { get; } = new List<ItemInfoEntry>();
		public List<string> ParseMessages { get; } = new List<string>();
		public int BlocksFound { get; set; }
		public int BlocksParsed { get; set; }
		public bool UsedHeuristicFallback { get; set; }

		public int ValidCount => Entries.Count(e => e.IsValid);
		public int InvalidCount => Entries.Count(e => !e.IsValid);
	}
}
