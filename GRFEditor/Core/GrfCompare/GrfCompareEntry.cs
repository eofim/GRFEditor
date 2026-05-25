namespace GRFEditor.Core.GrfCompare {
	public sealed class GrfCompareEntry {
		public GrfCompareStatus Status { get; set; }
		public string RelativePath { get; set; }
		public string PathInA { get; set; }
		public string PathInB { get; set; }
		public int? SizeDecompressedA { get; set; }
		public int? SizeDecompressedB { get; set; }
		public string Md5A { get; set; }
		public string Md5B { get; set; }
	}
}
