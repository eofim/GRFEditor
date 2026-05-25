using System;
using System.IO;
using GRF.Core;
using Utilities.Services;

namespace GRFEditor.Core.ItemInfo {
	internal static class ItemInfoFileReader {
		public static string ReadText(GrfHolder grf, string path) {
			if (string.IsNullOrEmpty(path))
				return null;

			if (Path.IsPathRooted(path) && File.Exists(path)) {
				try {
					return File.ReadAllText(path, EncodingService.DisplayEncoding);
				}
				catch {
					return null;
				}
			}

			if (grf == null || !grf.IsOpened || !grf.FileTable.ContainsFile(path))
				return null;

			try {
				byte[] data = grf.FileTable[path].GetDecompressedData();
				return EncodingService.DisplayEncoding.GetString(data);
			}
			catch {
				return null;
			}
		}
	}
}
