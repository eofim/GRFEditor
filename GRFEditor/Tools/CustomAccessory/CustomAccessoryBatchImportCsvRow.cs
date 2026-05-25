namespace GRFEditor.Tools.CustomAccessory {
	public sealed class CustomAccessoryBatchImportCsvRow {
		public int LineNumber { get; set; }
		public string SpriteFile { get; set; }
		public string ConstantName { get; set; }
		public string DisplayName { get; set; }
		public int? ViewId { get; set; }
	}
}
