using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace GRFEditor.Core.GrfCompare {
	internal static class GrfCompareExport {
		public static string ToCsv(IEnumerable<GrfCompareView> views) {
			var sb = new StringBuilder();
			sb.AppendLine("RelativePath,Status,SizeA,SizeB,HashA,HashB,PathInA,PathInB");

			foreach (var view in views) {
				sb.Append(_csv(view.RelativePath));
				sb.Append(',');
				sb.Append(_csv(view.Status));
				sb.Append(',');
				sb.Append(_csv(view.SizeA));
				sb.Append(',');
				sb.Append(_csv(view.SizeB));
				sb.Append(',');
				sb.Append(_csv(view.HashA));
				sb.Append(',');
				sb.Append(_csv(view.HashB));
				sb.Append(',');
				sb.Append(_csv(view.PathInA));
				sb.Append(',');
				sb.Append(_csv(view.PathInB));
				sb.AppendLine();
			}

			return sb.ToString();
		}

		public static string ToJson(GrfCompareResult result, IEnumerable<GrfCompareView> views) {
			var sb = new StringBuilder();
			sb.AppendLine("{");
			sb.AppendLine("  \"comparedAtUtc\": " + _json(result?.ComparedAtUtc.ToString("u") ?? "") + ",");
			sb.AppendLine("  \"grfAPath\": " + _json(result?.GrfAPath) + ",");
			sb.AppendLine("  \"grfBPath\": " + _json(result?.GrfBPath) + ",");
			sb.AppendLine("  \"summary\": {");
			sb.AppendLine("    \"same\": " + (result?.SameCount ?? 0) + ",");
			sb.AppendLine("    \"added\": " + (result?.AddedCount ?? 0) + ",");
			sb.AppendLine("    \"removed\": " + (result?.RemovedCount ?? 0) + ",");
			sb.AppendLine("    \"modified\": " + (result?.ModifiedCount ?? 0) + ",");
			sb.AppendLine("    \"sameContentDifferentPath\": " + (result?.SameContentDifferentPathCount ?? 0) + ",");
			sb.AppendLine("    \"samePathDifferentContent\": " + (result?.SamePathDifferentContentCount ?? 0));
			sb.AppendLine("  },");
			sb.AppendLine("  \"entries\": [");

			var list = views.ToList();
			for (int i = 0; i < list.Count; i++) {
				var view = list[i];
				sb.AppendLine("    {");
				sb.AppendLine("      \"relativePath\": " + _json(view.RelativePath) + ",");
				sb.AppendLine("      \"status\": " + _json(view.Status) + ",");
				sb.AppendLine("      \"sizeA\": " + _json(view.SizeA) + ",");
				sb.AppendLine("      \"sizeB\": " + _json(view.SizeB) + ",");
				sb.AppendLine("      \"hashA\": " + _json(view.HashA) + ",");
				sb.AppendLine("      \"hashB\": " + _json(view.HashB) + ",");
				sb.AppendLine("      \"pathInA\": " + _json(view.PathInA) + ",");
				sb.Append("      \"pathInB\": " + _json(view.PathInB));
				sb.AppendLine();
				sb.Append(i < list.Count - 1 ? "    }," : "    }");
			}

			sb.AppendLine("  ]");
			sb.AppendLine("}");
			return sb.ToString();
		}

		public static string ToRemovedFilesList(GrfCompareResult result, IEnumerable<GrfCompareView> views) {
			var sb = new StringBuilder();
			sb.AppendLine("GRF Editor — Compare GRFs — removed files (GRF A only)");
			sb.AppendLine("Generated (UTC): " + (result?.ComparedAtUtc.ToString("u") ?? ""));
			sb.AppendLine("GRF A (old): " + (result?.GrfAPath ?? ""));
			sb.AppendLine("GRF B (new): " + (result?.GrfBPath ?? ""));
			sb.AppendLine();

			foreach (var view in views.Where(v => v.StatusValue == GrfCompareStatus.Removed)
				.OrderBy(v => v.RelativePath, StringComparer.OrdinalIgnoreCase)) {
				sb.AppendLine(!String.IsNullOrWhiteSpace(view.PathInA) ? view.PathInA : view.RelativePath);
			}

			return sb.ToString();
		}

		public static string ToFullDifferenceReport(GrfCompareResult result, IEnumerable<GrfCompareView> views) {
			var sb = new StringBuilder();
			sb.AppendLine("GRF Editor — Compare GRFs — full difference report");
			sb.AppendLine("Generated (UTC): " + (result?.ComparedAtUtc.ToString("u") ?? ""));
			sb.AppendLine("GRF A (old): " + (result?.GrfAPath ?? ""));
			sb.AppendLine("GRF B (new): " + (result?.GrfBPath ?? ""));
			sb.AppendLine();
			sb.AppendLine("Summary");
			sb.AppendLine("  Same: " + (result?.SameCount ?? 0));
			sb.AppendLine("  Added: " + (result?.AddedCount ?? 0));
			sb.AppendLine("  Removed: " + (result?.RemovedCount ?? 0));
			sb.AppendLine("  Modified: " + (result?.ModifiedCount ?? 0));
			sb.AppendLine("  Same content, different path: " + (result?.SameContentDifferentPathCount ?? 0));
			sb.AppendLine("  Same path, different content: " + (result?.SamePathDifferentContentCount ?? 0));
			sb.AppendLine();
			sb.AppendLine("Exported files under subfolder 'files' are from GRF B (new), except removed entries (listed only).");
			sb.AppendLine();

			foreach (var group in views.GroupBy(v => v.Status).OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)) {
				sb.AppendLine("=== " + group.Key + " (" + group.Count() + ") ===");

				foreach (var view in group.OrderBy(v => v.RelativePath, StringComparer.OrdinalIgnoreCase))
					_appendReportLine(sb, view);

				sb.AppendLine();
			}

			return sb.ToString();
		}

		public static string ToChangedFilesTxt(GrfCompareResult result, IEnumerable<GrfCompareView> views) {
			var sb = new StringBuilder();
			sb.AppendLine("GRF Editor — Compare GRFs — changed files list");
			sb.AppendLine("Generated (UTC): " + (result?.ComparedAtUtc.ToString("u") ?? ""));
			sb.AppendLine("GRF A (old): " + (result?.GrfAPath ?? ""));
			sb.AppendLine("GRF B (new): " + (result?.GrfBPath ?? ""));
			sb.AppendLine();

			foreach (var view in views.Where(v => v.StatusValue != GrfCompareStatus.Same)) {
				switch (view.StatusValue) {
					case GrfCompareStatus.SameContentDifferentPath:
						sb.Append("[");
						sb.Append(view.Status);
						sb.Append("] ");
						sb.Append(view.PathInA);
						sb.Append("  ->  ");
						sb.AppendLine(view.PathInB);
						break;
					default:
						sb.Append("[");
						sb.Append(view.Status);
						sb.Append("] ");
						sb.AppendLine(view.RelativePath);
						break;
				}
			}

			return sb.ToString();
		}

		private static void _appendReportLine(StringBuilder sb, GrfCompareView view) {
			switch (view.StatusValue) {
				case GrfCompareStatus.SameContentDifferentPath:
					sb.Append("  ");
					sb.Append(view.PathInA);
					sb.Append("  ->  ");
					sb.Append(view.PathInB);
					sb.Append("  [A:");
					sb.Append(view.SizeA);
					sb.Append(" B:");
					sb.Append(view.SizeB);
					sb.AppendLine("]");
					break;
				default:
					sb.Append("  ");
					sb.Append(view.RelativePath);
					sb.Append("  [A:");
					sb.Append(view.SizeA);
					sb.Append(" B:");
					sb.Append(view.SizeB);
					if (!String.IsNullOrWhiteSpace(view.HashA) || !String.IsNullOrWhiteSpace(view.HashB)) {
						sb.Append(" MD5 A:");
						sb.Append(view.HashA);
						sb.Append(" B:");
						sb.Append(view.HashB);
					}
					sb.AppendLine();
					break;
			}
		}

		public static void WriteUtf8(string path, string content) {
			File.WriteAllText(path, content, new UTF8Encoding(true));
		}

		private static string _csv(string value) {
			if (value == null)
				return "\"\"";

			if (value.IndexOfAny(new[] { '"', ',', '\r', '\n' }) < 0)
				return value;

			return "\"" + value.Replace("\"", "\"\"") + "\"";
		}

		private static string _json(string value) {
			if (value == null)
				return "null";

			return "\"" + value
				.Replace("\\", "\\\\")
				.Replace("\"", "\\\"")
				.Replace("\r", "\\r")
				.Replace("\n", "\\n")
				.Replace("\t", "\\t") + "\"";
		}
	}
}
