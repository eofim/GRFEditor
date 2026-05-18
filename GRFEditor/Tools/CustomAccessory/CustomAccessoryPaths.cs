using System;
using System.IO;
using System.Reflection;
using GRF.Core;
using GRFEditor.ApplicationConfiguration;

namespace GRFEditor.Tools.CustomAccessory {
	public static class CustomAccessoryPaths {
		public static string ResolveDataRoot() {
			var candidates = new[] {
				Directory.GetCurrentDirectory(),
				Path.GetDirectoryName(Assembly.GetEntryAssembly().Location),
				Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..")),
				Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "..")),
			};

			foreach (var candidate in candidates) {
				if (string.IsNullOrEmpty(candidate))
					continue;

				var dataPath = Path.Combine(candidate, "data");
				if (Directory.Exists(dataPath))
					return dataPath;
			}

			return Path.Combine(Directory.GetCurrentDirectory(), "data");
		}

		public static string DefaultAccessoryIdPath() {
			return Path.Combine(ResolveDataRoot(), @"luafiles514\lua files\datainfo\accessoryid.lub");
		}

		public static string DefaultAccnamePath() {
			return Path.Combine(ResolveDataRoot(), @"luafiles514\lua files\datainfo\accname.lub");
		}

		public static string GetAccessoryIdPath(GrfHolder grf = null) {
			return CustomAccessoryLubLocations.Resolve(grf).EditAccessoryIdPath
				?? DefaultAccessoryIdPath();
		}

		public static string GetAccnamePath(GrfHolder grf = null) {
			return CustomAccessoryLubLocations.Resolve(grf).EditAccnamePath
				?? DefaultAccnamePath();
		}
	}
}
