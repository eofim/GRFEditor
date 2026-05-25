using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace GRFEditor.Core.ItemInfo {
	public static class ItemInfoBuilderExport {
		public static string ToJson(ItemInfoEntry entry) {
			if (entry == null)
				return "{}";

			return ToJson(new[] { entry });
		}

		public static string ToJson(IEnumerable<ItemInfoEntry> entries) {
			var sb = new StringBuilder();
			sb.AppendLine("[");

			var list = entries.Where(e => e != null).ToList();
			for (int i = 0; i < list.Count; i++) {
				var e = list[i];
				sb.AppendLine("  {");
				sb.AppendLine("    \"itemId\": " + e.ItemId + ",");
				sb.AppendLine("    \"identifiedDisplayName\": " + _json(e.IdentifiedDisplayName) + ",");
				sb.AppendLine("    \"unidentifiedDisplayName\": " + _json(e.UnidentifiedDisplayName) + ",");
				sb.AppendLine("    \"identifiedResourceName\": " + _json(e.IdentifiedResourceName) + ",");
				sb.AppendLine("    \"unidentifiedResourceName\": " + _json(e.UnidentifiedResourceName) + ",");
				sb.AppendLine("    \"identifiedDescription\": " + _json(e.IdentifiedDescription) + ",");
				sb.AppendLine("    \"slotCount\": " + (e.SlotCount?.ToString() ?? "null") + ",");
				sb.AppendLine("    \"classNum\": " + (e.ClassNum?.ToString() ?? "null") + ",");
				sb.AppendLine("    \"costumeViewId\": " + (e.CostumeViewId?.ToString() ?? "null") + ",");
				sb.AppendLine("    \"costume\": " + (e.Costume ? "true" : "false") + ",");
				sb.AppendLine("    \"isValid\": " + (e.IsValid ? "true" : "false") + ",");
				sb.Append("    \"issues\": " + _jsonArray(e.Issues));
				sb.AppendLine();
				sb.Append(i < list.Count - 1 ? "  }," : "  }");
			}

			sb.AppendLine("]");
			return sb.ToString();
		}

		private static string _jsonArray(List<string> items) {
			if (items == null || items.Count == 0)
				return "[]";

			return "[" + string.Join(", ", items.Select(_json)) + "]";
		}

		private static string _json(string value) {
			if (value == null)
				return "null";

			return "\"" + value
				.Replace("\\", "\\\\")
				.Replace("\"", "\\\"")
				.Replace("\r", "\\r")
				.Replace("\n", "\\n") + "\"";
		}
	}
}
