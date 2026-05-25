using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace GRFEditor.Tools.CustomAccessory {
	public static class CustomAccessoryItemInfoGenerator {
		public static void WriteFiles(string outputFolder, IEnumerable<CustomAccessoryEntry> entries) {
			if (string.IsNullOrWhiteSpace(outputFolder))
				throw new ArgumentException("Pasta de destino não definida.");

			var list = entries.Where(p => p != null && p.Selected && p.ItemId > 0).OrderBy(p => p.ItemId).ToList();
			if (list.Count == 0)
				throw new InvalidOperationException("Nenhum item com ItemId válido selecionado.");

			Directory.CreateDirectory(outputFolder);

			var itemInfoPath = Path.Combine(outputFolder, "iteminfo.lua");
			var itemDbPath = Path.Combine(outputFolder, "item_db.yml");

			File.WriteAllText(itemInfoPath, GenerateItemInfoLua(list), Encoding.UTF8);
			File.WriteAllText(itemDbPath, GenerateItemDbYaml(list), Encoding.UTF8);
		}

		public static string GenerateItemInfoLua(IList<CustomAccessoryEntry> entries) {
			var sb = new StringBuilder();
			sb.AppendLine("-- Gerado pelo GRFEditor (CustomAccessory)");
			sb.AppendLine("-- Formato compatível com tabela iteminfo / Costumes.lua");
			sb.AppendLine();

			foreach (var entry in entries)
				sb.Append(GenerateCostumeBlock(entry));

			return sb.ToString();
		}

		public static string GenerateItemDbYaml(IList<CustomAccessoryEntry> entries) {
			var sb = new StringBuilder();
			sb.AppendLine("# Gerado pelo GRFEditor (CustomAccessory)");
			sb.AppendLine("Body:");
			sb.AppendLine();

			foreach (var entry in entries)
				sb.Append(GenerateItemDbEntry(entry));

			return sb.ToString();
		}

		private static string GenerateCostumeBlock(CustomAccessoryEntry entry) {
			string aegisName;
			string displayName;
			string resourceName;
			CustomAccessoryItemInfoNaming.GetItemNames(entry, out aegisName, out displayName, out resourceName);

			var escapedDisplay = EscapeLuaString(displayName);
			var sb = new StringBuilder();

			sb.AppendLine("\t[" + entry.ItemId + "] = {");
			sb.AppendLine("\t\tunidentifiedDisplayName = \"" + escapedDisplay + "\",");
			sb.AppendLine("\t\tunidentifiedResourceName = \"" + resourceName + "\",");
			sb.AppendLine("\t\tunidentifiedDescriptionName = { \"...\" },");
			sb.AppendLine("\t\tidentifiedDisplayName = \"" + escapedDisplay + "\",");
			sb.AppendLine("\t\tidentifiedResourceName = \"" + resourceName + "\",");
			sb.AppendLine("\t\tidentifiedDescriptionName = {");
			sb.AppendLine("\t\t\t\"Oops, este visual ainda não tem uma descrição.\",");
			sb.AppendLine("\t\t\t\"^ffffff_^000000\",");
			sb.AppendLine("\t\t\t\"Tipo: ^777777Visual^000000\",");
			sb.AppendLine("\t\t\t\"Equipa em: ^777777Topo^000000\",");
			sb.AppendLine("\t\t\t\"Peso: ^7777770^000000\",");
			sb.AppendLine("\t\t\t\"Nível Necessário: ^7777771^000000\",");
			sb.AppendLine("\t\t\t\"Classes: ^777777Todas^000000\"");
			sb.AppendLine("\t\t},");
			sb.AppendLine("\t\tslotCount = 0,");
			sb.AppendLine("\t\tClassNum = " + entry.ViewId + ",");
			sb.AppendLine("\t\tcostume = true");
			sb.AppendLine("\t},");
			sb.AppendLine();

			return sb.ToString();
		}

		private static string GenerateItemDbEntry(CustomAccessoryEntry entry) {
			string aegisName;
			string displayName;
			string resourceName;
			CustomAccessoryItemInfoNaming.GetItemNames(entry, out aegisName, out displayName, out resourceName);

			var sb = new StringBuilder();
			sb.AppendLine("  - Id: " + entry.ItemId);
			sb.AppendLine("    AegisName: " + aegisName);
			sb.AppendLine("    Name: " + displayName);
			sb.AppendLine("    Type: Armor");
			sb.AppendLine("    Buy: 20");
			sb.AppendLine("    Weight: 10");
			sb.AppendLine("    Classes:");
			sb.AppendLine("      All_Third: true");
			sb.AppendLine("      Normal: true");
			sb.AppendLine("      Upper: true");
			sb.AppendLine("      Baby: true");
			sb.AppendLine("    Locations:");
			sb.AppendLine("      Costume_Head_Mid: true");
			sb.AppendLine("    ArmorLevel: 1");
			sb.AppendLine("    EquipLevelMin: 1");
			sb.AppendLine("    View: " + entry.ViewId);
			sb.AppendLine();

			return sb.ToString();
		}

		private static string EscapeLuaString(string value) {
			if (value == null)
				return "";

			return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
		}
	}
}
