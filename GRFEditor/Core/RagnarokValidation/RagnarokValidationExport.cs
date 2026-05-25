using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace GRFEditor.Core.RagnarokValidation {
	internal static class RagnarokValidationExport {
		public static string ToCsv(IEnumerable<RagnarokValidationView> views) {
			var sb = new StringBuilder();
			sb.AppendLine("Severity,Category,RelativePath,Message,SuggestedFix,CanAutoFix");

			foreach (var view in views) {
				sb.Append(_csv(view.Severity));
				sb.Append(',');
				sb.Append(_csv(view.Category));
				sb.Append(',');
				sb.Append(_csv(view.RelativePath));
				sb.Append(',');
				sb.Append(_csv(view.Message));
				sb.Append(',');
				sb.Append(_csv(view.SuggestedFix));
				sb.Append(',');
				sb.Append(_csv(view.CanAutoFix));
				sb.AppendLine();
			}

			return sb.ToString();
		}

		public static string ToJson(IEnumerable<RagnarokValidationView> views) {
			var sb = new StringBuilder();
			sb.AppendLine("[");

			var list = views.ToList();
			for (int i = 0; i < list.Count; i++) {
				var view = list[i];
				sb.AppendLine("  {");
				sb.AppendLine("    \"severity\": " + _json(view.Severity) + ",");
				sb.AppendLine("    \"category\": " + _json(view.Category) + ",");
				sb.AppendLine("    \"relativePath\": " + _json(view.RelativePath) + ",");
				sb.AppendLine("    \"message\": " + _json(view.Message) + ",");
				sb.AppendLine("    \"suggestedFix\": " + _json(view.SuggestedFix) + ",");
				sb.Append("    \"canAutoFix\": " + (view.Issue.CanAutoFix ? "true" : "false"));

				if (view.Issue.RelatedFiles != null && view.Issue.RelatedFiles.Count > 0) {
					sb.AppendLine(",");
					sb.Append("    \"relatedFiles\": [");
					sb.Append(String.Join(", ", view.Issue.RelatedFiles.Select(_json)));
					sb.Append("]");
				}

				sb.AppendLine();
				sb.Append(i < list.Count - 1 ? "  }," : "  }");
			}

			sb.AppendLine("]");
			return sb.ToString();
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
