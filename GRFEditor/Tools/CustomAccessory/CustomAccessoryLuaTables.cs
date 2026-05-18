using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using GRF;
using GRF.FileFormats.LubFormat.Preset;
using Utilities;
using Utilities.Services;

namespace GRFEditor.Tools.CustomAccessory {
	public class CustomAccessoryLuaTables {
		public Dictionary<string, int> AccessoryIds { get; private set; }
		public Dictionary<string, string> Accnames { get; private set; }

		public static CustomAccessoryLuaTables Load(string accessoryIdPath, string accnamePath) {
			var tables = new CustomAccessoryLuaTables {
				AccessoryIds = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase),
				Accnames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
			};

			if (!File.Exists(accessoryIdPath) || !File.Exists(accnamePath))
				return tables;

			try {
				MultiType accnameBytes = File.ReadAllBytes(accnamePath);
				MultiType accessoryIdBytes = File.ReadAllBytes(accessoryIdPath);
				var accnameData = new AccnameLubData(accnameBytes, accessoryIdBytes);

				tables.AccessoryIds = new Dictionary<string, int>(accnameData.AccessoryId, StringComparer.OrdinalIgnoreCase);
				tables.Accnames = new Dictionary<string, string>(accnameData.Accname, StringComparer.OrdinalIgnoreCase);
			}
			catch {
			}

			return tables;
		}

		public int GetNextViewId() {
			if (AccessoryIds.Count == 0)
				return 1;

			return AccessoryIds.Values.Max() + 1;
		}

		public bool HasConstant(string constantName) {
			return AccessoryIds.ContainsKey(constantName);
		}
	}
}
