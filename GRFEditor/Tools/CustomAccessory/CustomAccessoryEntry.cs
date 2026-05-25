namespace GRFEditor.Tools.CustomAccessory {
	public class CustomAccessoryEntry {
		public string SpritePath { get; set; }
		public string ConstantName { get; set; }
		public int ViewId { get; set; }
		public int ItemId { get; set; }
		public string DisplayName { get; set; }
		public CustomAccessoryEntryStatus Status { get; set; }
		public bool IsNew { get; set; }
		public bool Selected { get; set; }

		public string StatusDisplay {
			get {
				switch (Status) {
					case CustomAccessoryEntryStatus.Existing:
						return "Existente";
					case CustomAccessoryEntryStatus.IncompleteMissingAccessoryId:
						return "Incompleto: falta accessoryid";
					case CustomAccessoryEntryStatus.IncompleteMissingAccname:
						return "Incompleto: falta accname";
					default:
						return "Novo";
				}
			}
		}

		public bool ShouldWriteAccessoryId {
			get { return Status != CustomAccessoryEntryStatus.IncompleteMissingAccname; }
		}

		public bool ShouldWriteAccname {
			get { return Status != CustomAccessoryEntryStatus.IncompleteMissingAccessoryId; }
		}

		public CustomAccessoryEntry() {
			Selected = true;
			Status = CustomAccessoryEntryStatus.New;
			IsNew = true;
		}
	}
}
