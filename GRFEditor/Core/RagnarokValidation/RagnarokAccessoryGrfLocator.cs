using System;
using System.Collections.Generic;
using System.IO;
using GRF.Core;
using GRF.IO;
using GRFEditor.Core.ProjectProfiles;

namespace GRFEditor.Core.RagnarokValidation {
	internal static class RagnarokAccessoryGrfLocator {
		public const string AccessoryIdFileName = "accessoryid.lub";
		public const string AccnameFileName = "accname.lub";
		public const string ItemInfoLuaFileName = "iteminfo.lua";
		public const string ItemInfoLubFileName = "iteminfo.lub";

		public sealed class AccessoryGrfFiles {
			public string AccessoryIdPath { get; set; }
			public string AccnamePath { get; set; }
			public string ItemInfoLuaPath { get; set; }
			public string ItemInfoLubPath { get; set; }

			public bool HasAny =>
				!String.IsNullOrEmpty(AccessoryIdPath)
				|| !String.IsNullOrEmpty(AccnamePath)
				|| !String.IsNullOrEmpty(ItemInfoLuaPath)
				|| !String.IsNullOrEmpty(ItemInfoLubPath);

			public bool HasAccessoryPair =>
				!String.IsNullOrEmpty(AccessoryIdPath) && !String.IsNullOrEmpty(AccnamePath);
		}

		public static AccessoryGrfFiles Locate(GrfHolder grf) {
			var files = new AccessoryGrfFiles();

			if (grf == null || !grf.IsOpened) {
				_applyActiveProfileDiskPaths(files);
				return files;
			}

			files.AccessoryIdPath = FindBestEntryPath(grf, AccessoryIdFileName, preferDatainfo: true);
			files.AccnamePath = FindBestEntryPath(grf, AccnameFileName, preferDatainfo: true);
			files.ItemInfoLuaPath = FindBestEntryPath(grf, ItemInfoLuaFileName, preferDatainfo: false);
			files.ItemInfoLubPath = FindBestEntryPath(grf, ItemInfoLubFileName, preferDatainfo: false);

			_applyActiveProfileDiskPaths(files);

			return files;
		}

		private static void _applyActiveProfileDiskPaths(AccessoryGrfFiles files) {
			if (String.IsNullOrEmpty(files.AccessoryIdPath)) {
				string path = ActiveProjectProfile.GetAccessoryIdPath();
				if (!String.IsNullOrEmpty(path))
					files.AccessoryIdPath = path;
			}

			if (String.IsNullOrEmpty(files.AccnamePath)) {
				string path = ActiveProjectProfile.GetAccNamePath();
				if (!String.IsNullOrEmpty(path))
					files.AccnamePath = path;
			}

			if (String.IsNullOrEmpty(files.ItemInfoLuaPath) && String.IsNullOrEmpty(files.ItemInfoLubPath)) {
				string itemInfo = ActiveProjectProfile.GetItemInfoPath();
				if (!String.IsNullOrEmpty(itemInfo) && File.Exists(itemInfo)) {
					if (itemInfo.EndsWith(".lub", StringComparison.OrdinalIgnoreCase))
						files.ItemInfoLubPath = itemInfo;
					else
						files.ItemInfoLuaPath = itemInfo;
				}
			}
		}

		private static string FindBestEntryPath(GrfHolder grf, string fileName, bool preferDatainfo) {
			string bestPath = null;
			int bestScore = -1;

			foreach (var entry in grf.FileTable.Entries) {
				if (entry.IsRemoved)
					continue;

				if (!String.Equals(GrfPath.GetFileName(entry.RelativePath), fileName, StringComparison.OrdinalIgnoreCase))
					continue;

				int score = ScorePath(entry.RelativePath, preferDatainfo);

				if (score > bestScore) {
					bestScore = score;
					bestPath = GrfPath.CleanGrfPath(entry.RelativePath);
				}
			}

			return bestPath;
		}

		private static int ScorePath(string path, bool preferDatainfo) {
			int score = 0;

			if (path.IndexOf(@"datainfo\", StringComparison.OrdinalIgnoreCase) >= 0
			    || path.IndexOf("datainfo/", StringComparison.OrdinalIgnoreCase) >= 0)
				score += preferDatainfo ? 120 : 40;

			if (path.IndexOf(@"luafiles514\", StringComparison.OrdinalIgnoreCase) >= 0
			    || path.IndexOf("luafiles514/", StringComparison.OrdinalIgnoreCase) >= 0)
				score += 60;

			if (path.IndexOf(@"iteminfo\", StringComparison.OrdinalIgnoreCase) >= 0
			    || path.IndexOf("iteminfo/", StringComparison.OrdinalIgnoreCase) >= 0)
				score += 30;

			if (path.IndexOf(@"\data\", StringComparison.OrdinalIgnoreCase) >= 0
			    || path.StartsWith("data\\", StringComparison.OrdinalIgnoreCase)
			    || path.StartsWith("data/", StringComparison.OrdinalIgnoreCase))
				score += 10;

			return score;
		}
	}
}
