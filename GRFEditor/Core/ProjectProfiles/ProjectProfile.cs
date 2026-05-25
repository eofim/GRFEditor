using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace GRFEditor.Core.ProjectProfiles {
	[DataContract]
	public class ProjectProfile {
		public ProjectProfile() {
			IgnoredValidationRules = new List<string>();
			BuildRules = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
			CreatedAt = DateTime.UtcNow;
			UpdatedAt = CreatedAt;
		}

		[DataMember(Order = 0)]
		public string Name { get; set; }

		[DataMember(Order = 1)]
		public string MainGrfPath { get; set; }

		[DataMember(Order = 2)]
		public string DataFolderPath { get; set; }

		[DataMember(Order = 3)]
		public string ExportFolderPath { get; set; }

		[DataMember(Order = 4)]
		public string PatchOutputFolderPath { get; set; }

		[DataMember(Order = 5)]
		public string ClientFolderPath { get; set; }

		[DataMember(Order = 6)]
		public string AccessoryIdPath { get; set; }

		[DataMember(Order = 7)]
		public string AccNamePath { get; set; }

		[DataMember(Order = 8)]
		public string ItemInfoPath { get; set; }

		[DataMember(Order = 9)]
		public string EncodingName { get; set; }

		[DataMember(Order = 10)]
		public int LastUsedViewId { get; set; }

		[DataMember(Order = 11)]
		public List<string> IgnoredValidationRules { get; set; }

		[DataMember(Order = 12)]
		public Dictionary<string, string> BuildRules { get; set; }

		[DataMember(Order = 13)]
		public DateTime CreatedAt { get; set; }

		[DataMember(Order = 14)]
		public DateTime UpdatedAt { get; set; }

		public void EnsureDefaults() {
			if (IgnoredValidationRules == null)
				IgnoredValidationRules = new List<string>();

			if (BuildRules == null)
				BuildRules = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

			if (CreatedAt == default(DateTime))
				CreatedAt = DateTime.UtcNow;

			if (UpdatedAt == default(DateTime))
				UpdatedAt = CreatedAt;
		}

		public ProjectProfile Clone() {
			var copy = new ProjectProfile {
				Name = Name,
				MainGrfPath = MainGrfPath,
				DataFolderPath = DataFolderPath,
				ExportFolderPath = ExportFolderPath,
				PatchOutputFolderPath = PatchOutputFolderPath,
				ClientFolderPath = ClientFolderPath,
				AccessoryIdPath = AccessoryIdPath,
				AccNamePath = AccNamePath,
				ItemInfoPath = ItemInfoPath,
				EncodingName = EncodingName,
				LastUsedViewId = LastUsedViewId,
				CreatedAt = CreatedAt,
				UpdatedAt = UpdatedAt,
			};

			copy.IgnoredValidationRules = new List<string>(IgnoredValidationRules ?? new List<string>());
			copy.BuildRules = new Dictionary<string, string>(BuildRules ?? new Dictionary<string, string>(), StringComparer.OrdinalIgnoreCase);
			return copy;
		}
	}
}
