using System;
using System.IO;
using System.Linq;
using System.Text;

namespace GRFEditor.Core.BuildPipeline {
	internal static class BuildChangelogExporter {
		public const string ChangelogTxtFileName = "build-changelog.txt";

		public static string ToPlainText(BuildChangelogDocument changelog) {
			if (changelog == null)
				return "";

			var sb = new StringBuilder();
			sb.AppendLine("GRF Editor — Build changelog (manifest comparison)");
			sb.AppendLine("Generated (UTC): " + changelog.GeneratedAtUtc.ToString("u"));
			sb.AppendLine("GRF: " + (changelog.GrfFileName ?? ""));
			sb.AppendLine("Previous manifest: " + (String.IsNullOrEmpty(changelog.PreviousManifestPath) ? "(none)" : changelog.PreviousManifestPath));
			sb.AppendLine("Current manifest: " + (String.IsNullOrEmpty(changelog.CurrentManifestPath) ? "(none)" : changelog.CurrentManifestPath));
			sb.AppendLine();

			if (!changelog.HasPreviousManifest) {
				sb.AppendLine("No previous manifest was selected for comparison.");
				return sb.ToString();
			}

			_appendSection(sb, "Added files", changelog.Added);
			_appendSection(sb, "Removed files", changelog.Removed);
			_appendSection(sb, "Changed files", changelog.Changed);
			_appendSection(sb, "Unchanged files", changelog.Unchanged);
			_appendMoves(sb, changelog);

			sb.AppendLine("Summary");
			sb.AppendLine("  Added: " + (changelog.Added?.Count ?? 0));
			sb.AppendLine("  Removed: " + (changelog.Removed?.Count ?? 0));
			sb.AppendLine("  Changed: " + (changelog.Changed?.Count ?? 0));
			sb.AppendLine("  Unchanged: " + (changelog.Unchanged?.Count ?? 0));
			sb.AppendLine("  Same hash, different path: " + (changelog.SameHashDifferentPath?.Count ?? 0));

			return sb.ToString();
		}

		public static void WriteTxt(string filePath, BuildChangelogDocument changelog) {
			string directory = Path.GetDirectoryName(filePath);
			if (!String.IsNullOrEmpty(directory) && !Directory.Exists(directory))
				Directory.CreateDirectory(directory);

			File.WriteAllText(filePath, ToPlainText(changelog), new UTF8Encoding(true));
		}

		private static void _appendSection(StringBuilder sb, string title, System.Collections.Generic.List<string> paths) {
			sb.AppendLine(title + " (" + (paths?.Count ?? 0) + ")");
			if (paths == null || paths.Count == 0) {
				sb.AppendLine("  (none)");
			}
			else {
				foreach (string path in paths)
					sb.AppendLine("  " + path);
			}

			sb.AppendLine();
		}

		private static void _appendMoves(StringBuilder sb, BuildChangelogDocument changelog) {
			var moves = changelog.SameHashDifferentPath;
			sb.AppendLine("Same hash, different path (" + (moves?.Count ?? 0) + ")");

			if (moves == null || moves.Count == 0) {
				sb.AppendLine("  (none)");
			}
			else {
				foreach (var move in moves) {
					sb.Append("  ");
					sb.Append(move.PreviousPath);
					sb.Append("  ->  ");
					sb.Append(move.CurrentPath);
					if (!String.IsNullOrWhiteSpace(move.Md5))
						sb.Append("  [MD5 " + move.Md5 + "]");
					sb.AppendLine();
				}
			}

			sb.AppendLine();
		}
	}
}
