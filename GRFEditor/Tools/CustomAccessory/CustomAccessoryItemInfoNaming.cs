using System;

namespace GRFEditor.Tools.CustomAccessory {
	public static class CustomAccessoryItemInfoNaming {
		public static void GetItemNames(CustomAccessoryEntry entry, out string aegisName, out string displayName, out string resourceName) {
			var slug = BuildResourceSlug(entry);
			resourceName = slug;
			aegisName = "_" + slug;
			displayName = slug.Replace('_', ' ');
		}

		public static string BuildResourceSlug(CustomAccessoryEntry entry) {
			var display = entry != null ? (entry.DisplayName ?? "").Trim() : "";
			if (string.IsNullOrEmpty(display)) {
				var part = CustomAccessoryNaming.NormalizeConstantName(entry != null ? entry.ConstantName : "");
				if (part.StartsWith("ACCESSORY_", StringComparison.OrdinalIgnoreCase))
					part = part.Substring("ACCESSORY_".Length);
				display = "_" + part;
			}

			if (!display.StartsWith("_", StringComparison.Ordinal))
				display = "_" + display;

			var slug = display.TrimStart('_').ToUpperInvariant();
			slug = CustomAccessoryNaming.SanitizeIdentifierPart(slug).ToUpperInvariant();
			if (string.IsNullOrEmpty(slug))
				slug = "UNKNOWN";

			return slug;
		}
	}
}
