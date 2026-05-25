using System.Text.RegularExpressions;

namespace GRFEditor.Tools.CustomAccessory {
	internal static class CustomAccessoryLuaPatterns {
		public static readonly Regex AccessoryIdLineRegex = new Regex(
			@"^\s*(?<name>ACCESSORY_[A-Za-z0-9_]+)\s*=\s*(?<id>\d+)\s*,?\s*(?:--.*)?$",
			RegexOptions.Compiled | RegexOptions.IgnoreCase);

		public static readonly Regex AccnameLineRegex = new Regex(
			@"^\s*\[ACCESSORY_IDs\.(?<name>ACCESSORY_[A-Za-z0-9_]+)\]\s*=\s*""(?<value>(?:\\.|[^""])*)""\s*,?\s*(?:--.*)?$",
			RegexOptions.Compiled | RegexOptions.IgnoreCase);
	}
}
