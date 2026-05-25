using System.Collections.Generic;

namespace GRFEditor.Core.AccessoryScanner {
	public sealed class AccessoryScanEntry {
		public string SpritePath { get; set; }
		public string ActPath { get; set; }
		public string ConstantName { get; set; }
		public int? ViewId { get; set; }
		public string DisplayName { get; set; }
		public AccessoryScanStatus Status { get; set; }
		public List<string> Issues { get; set; } = new List<string>();
	}
}
