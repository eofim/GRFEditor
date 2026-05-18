namespace GRFEditor.Tools.CustomAccessory {
	public class CustomAccessoryEntry {
		public string SpritePath { get; set; }
		public string ConstantName { get; set; }
		public int ViewId { get; set; }
		public string DisplayName { get; set; }
		public bool IsNew { get; set; }
		public bool Selected { get; set; }

		public CustomAccessoryEntry() {
			Selected = true;
		}
	}
}
