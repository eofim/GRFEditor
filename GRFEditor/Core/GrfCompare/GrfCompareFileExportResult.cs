using System.Collections.Generic;

namespace GRFEditor.Core.GrfCompare {
	public sealed class GrfCompareFileExportResult {
		public GrfCompareFileExportMode Mode { get; set; }
		public string DestinationPath { get; set; }
		public int ExportedCount { get; set; }
		public int IgnoredCount => IgnoredFiles?.Count ?? 0;
		public int ErrorCount => Errors?.Count ?? 0;
		public List<string> Errors { get; set; } = new List<string>();
		public List<string> IgnoredFiles { get; set; } = new List<string>();
		public List<string> WrittenReportFiles { get; set; } = new List<string>();
	}
}
