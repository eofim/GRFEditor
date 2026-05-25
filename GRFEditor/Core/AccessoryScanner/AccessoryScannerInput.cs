using GRF.Core;

namespace GRFEditor.Core.AccessoryScanner {
	public sealed class AccessoryScannerInput {
		public GrfHolder Grf { get; set; }

		/// <summary>
		/// Optional local folder scanned recursively for .spr/.act (paths relative to this root).
		/// </summary>
		public string LocalSpriteFolder { get; set; }

		public string AccessoryIdPath { get; set; }
		public string AccnamePath { get; set; }
	}
}
