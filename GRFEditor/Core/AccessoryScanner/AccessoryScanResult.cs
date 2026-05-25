using System;
using System.Collections.Generic;
using System.Linq;

namespace GRFEditor.Core.AccessoryScanner {
	public sealed class AccessoryScanResult {
		public DateTime ScannedAtUtc { get; set; }
		public string AccessoryIdSource { get; set; }
		public string AccnameSource { get; set; }
		public string GrfPath { get; set; }
		public string LocalSpriteFolder { get; set; }
		public List<AccessoryScanEntry> Entries { get; set; } = new List<AccessoryScanEntry>();
		public List<string> Messages { get; set; } = new List<string>();

		public int CountByStatus(AccessoryScanStatus status) {
			return Entries?.Count(e => e.Status == status) ?? 0;
		}
	}
}
