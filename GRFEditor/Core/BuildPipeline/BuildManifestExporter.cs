using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace GRFEditor.Core.BuildPipeline {
	internal static class BuildManifestExporter {
		public const string ManifestCsvFileName = "build-manifest.csv";

		public static string ToCsv(BuildManifestDocument manifest) {
			if (manifest == null)
				return "";

			var sb = new StringBuilder();
			bool includeHashes = manifest.HashesIncluded;

			sb.AppendLine("RelativePath,Size,Extension"
				+ (includeHashes ? ",MD5,SHA1" : ""));

			if (manifest.Files != null) {
				foreach (var file in manifest.Files) {
					sb.Append(_csv(file.RelativePath));
					sb.Append(',');
					sb.Append(file.Size.ToString());
					sb.Append(',');
					sb.Append(_csv(file.Extension ?? ""));

					if (includeHashes) {
						sb.Append(',');
						sb.Append(_csv(file.Md5 ?? ""));
						sb.Append(',');
						sb.Append(_csv(file.Sha1 ?? ""));
					}

					sb.AppendLine();
				}
			}

			return sb.ToString();
		}

		public static void WriteCsv(string filePath, BuildManifestDocument manifest) {
			string directory = Path.GetDirectoryName(filePath);
			if (!String.IsNullOrEmpty(directory) && !Directory.Exists(directory))
				Directory.CreateDirectory(directory);

			File.WriteAllText(filePath, ToCsv(manifest), new UTF8Encoding(true));
		}

		private static string _csv(string value) {
			if (value == null)
				return "\"\"";

			if (value.IndexOfAny(new[] { '"', ',', '\r', '\n' }) < 0)
				return value;

			return "\"" + value.Replace("\"", "\"\"") + "\"";
		}
	}
}
