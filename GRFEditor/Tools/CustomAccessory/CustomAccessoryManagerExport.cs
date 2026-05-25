using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using GRFEditor.Core.AccessoryScanner;

namespace GRFEditor.Tools.CustomAccessory {
	internal static class CustomAccessoryManagerExport {
		public static string ToImportPreviewCsv(IEnumerable<CustomAccessoryManagerEntry> entries) {
			var sb = new StringBuilder();
			sb.AppendLine("SpriteFile,ActFile,ConstantName,DisplayName,ViewId,Selected,Status,Issues");

			foreach (var row in entries ?? Enumerable.Empty<CustomAccessoryManagerEntry>()) {
				sb.Append(_csv(_spriteFile(row.SpritePath)));
				sb.Append(',');
				sb.Append(_csv(_spriteFile(row.ActPath)));
				sb.Append(',');
				sb.Append(_csv(row.ConstantName));
				sb.Append(',');
				sb.Append(_csv(row.DisplayName));
				sb.Append(',');
				sb.Append(_csv(row.ViewId > 0 ? row.ViewId.ToString() : ""));
				sb.Append(',');
				sb.Append(row.Selected ? "true" : "false");
				sb.Append(',');
				sb.Append(_csv(row.Status));
				sb.Append(',');
				sb.Append(_csv(row.Issues));
				sb.AppendLine();
			}

			return sb.ToString();
		}

		public static string ToImportTemplateCsv() {
			return "SpriteFile,ConstantName,DisplayName,ViewId" + Environment.NewLine
				+ "data/sprite/À©±×¾÷¿ö/item/my_hat.spr,ACCESSORY_MY_HAT,_my_hat,3500";
		}

		public static string ToCsv(IEnumerable<CustomAccessoryManagerEntry> views) {
			var sb = new StringBuilder();
			sb.AppendLine("SpritePath,ActPath,ConstantName,ViewId,DisplayName,Status,Issues");

			foreach (var view in views) {
				sb.Append(_csv(view.SpritePath));
				sb.Append(',');
				sb.Append(_csv(view.ActPath));
				sb.Append(',');
				sb.Append(_csv(view.ConstantName));
				sb.Append(',');
				sb.Append(_csv(view.ViewId > 0 ? view.ViewId.ToString() : ""));
				sb.Append(',');
				sb.Append(_csv(view.DisplayName));
				sb.Append(',');
				sb.Append(_csv(view.Status));
				sb.Append(',');
				sb.Append(_csv(view.Issues));
				sb.AppendLine();
			}

			return sb.ToString();
		}

		public static string ToJson(AccessoryScanResult result, IEnumerable<CustomAccessoryManagerEntry> views) {
			var sb = new StringBuilder();
			sb.AppendLine("{");
			sb.AppendLine("  \"scannedAtUtc\": " + _json(result?.ScannedAtUtc.ToString("u") ?? "") + ",");
			sb.AppendLine("  \"grfPath\": " + _json(result?.GrfPath) + ",");
			sb.AppendLine("  \"localSpriteFolder\": " + _json(result?.LocalSpriteFolder) + ",");
			sb.AppendLine("  \"accessoryIdSource\": " + _json(result?.AccessoryIdSource) + ",");
			sb.AppendLine("  \"accnameSource\": " + _json(result?.AccnameSource) + ",");
			sb.AppendLine("  \"summary\": {");

			if (result != null) {
				foreach (AccessoryScanStatus status in Enum.GetValues(typeof(AccessoryScanStatus)))
					sb.AppendLine("    \"" + status + "\": " + result.CountByStatus(status) + ",");
			}

			sb.AppendLine("    \"total\": " + (views?.Count() ?? 0));

			sb.AppendLine("  },");
			sb.AppendLine("  \"entries\": [");

			var list = views.ToList();
			for (int i = 0; i < list.Count; i++) {
				var view = list[i];
				sb.AppendLine("    {");
				sb.AppendLine("      \"spritePath\": " + _json(view.SpritePath) + ",");
				sb.AppendLine("      \"actPath\": " + _json(view.ActPath) + ",");
				sb.AppendLine("      \"constantName\": " + _json(view.ConstantName) + ",");
				sb.AppendLine("      \"viewId\": " + _json(view.ViewId > 0 ? view.ViewId.ToString() : null) + ",");
				sb.AppendLine("      \"displayName\": " + _json(view.DisplayName) + ",");
				sb.AppendLine("      \"status\": " + _json(view.Status) + ",");
				sb.Append("      \"issues\": " + _jsonArray(view.ScanEntry?.Issues));
				sb.AppendLine();
				sb.Append(i < list.Count - 1 ? "    }," : "    }");
			}

			sb.AppendLine("  ]");
			sb.AppendLine("}");
			return sb.ToString();
		}

		public static void WriteUtf8(string path, string content) {
			File.WriteAllText(path, content, new UTF8Encoding(true));
		}

		private static string _jsonArray(List<string> items) {
			if (items == null || items.Count == 0)
				return "[]";

			return "[" + String.Join(", ", items.Select(_json)) + "]";
		}

		private static string _spriteFile(string path) {
			if (string.IsNullOrWhiteSpace(path))
				return "";

			return path.Replace('\\', '/');
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
