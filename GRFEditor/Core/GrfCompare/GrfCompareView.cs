namespace GRFEditor.Core.GrfCompare {
	public sealed class GrfCompareView {
		public GrfCompareView(GrfCompareEntry entry) {
			Entry = entry;
		}

		public GrfCompareEntry Entry { get; }

		public string RelativePath => Entry?.RelativePath ?? "";

		public string Status => Entry?.Status.ToString() ?? "";

		public GrfCompareStatus StatusValue => Entry?.Status ?? GrfCompareStatus.Same;

		public string SizeA => Entry?.SizeDecompressedA?.ToString() ?? "";

		public string SizeB => Entry?.SizeDecompressedB?.ToString() ?? "";

		public string HashA => Entry?.Md5A ?? "";

		public string HashB => Entry?.Md5B ?? "";

		public string PathInA => Entry?.PathInA ?? "";

		public string PathInB => Entry?.PathInB ?? "";
	}
}
