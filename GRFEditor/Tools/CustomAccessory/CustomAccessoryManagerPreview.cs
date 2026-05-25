using System;
using System.IO;
using GRF.Core;
using GRF.FileFormats.SprFormat;
using GRF.IO;
using GRF.Image;
using GrfToWpfBridge;
using System.Windows.Media.Imaging;

namespace GRFEditor.Tools.CustomAccessory {
	internal static class CustomAccessoryManagerPreview {
		public static object GetPreview(GrfHolder grf, string localSpriteRoot, string sprPath) {
			if (String.IsNullOrWhiteSpace(sprPath))
				return IconProvider.GetSmallIcon(".spr");

			try {
				byte[] bytes = _readSpriteBytes(grf, localSpriteRoot, sprPath);

				if (bytes != null && bytes.Length > 0) {
					var spr = new Spr(bytes);

					if (spr.NumberOfIndexed8Images + spr.NumberOfBgra32Images > 0) {
						GrfImage image = spr.GetImage(0);

						if (image != null)
							return image.Cast<BitmapSource>();
					}
				}
			}
			catch {
			}

			return IconProvider.GetSmallIcon(sprPath);
		}

		private static byte[] _readSpriteBytes(GrfHolder grf, string localSpriteRoot, string sprPath) {
			if (grf != null && grf.IsOpened) {
				string clean = GrfPath.CleanGrfPath(sprPath);

				if (grf.FileTable.ContainsFile(clean))
					return grf.FileTable[clean].GetDecompressedData();
			}

			if (!String.IsNullOrWhiteSpace(localSpriteRoot) && Directory.Exists(localSpriteRoot)) {
				string fullPath = Path.Combine(localSpriteRoot, sprPath.Replace('/', Path.DirectorySeparatorChar));

				if (File.Exists(fullPath))
					return File.ReadAllBytes(fullPath);
			}

			return null;
		}
	}
}
