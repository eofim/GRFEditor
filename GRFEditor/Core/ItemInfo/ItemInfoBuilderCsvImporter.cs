using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace GRFEditor.Core.ItemInfo {
	public static class ItemInfoBuilderCsvImporter {
		public static readonly string[] RequiredHeaders = {
			"ItemId",
			"IdentifiedDisplayName",
			"UnidentifiedDisplayName",
			"IdentifiedResourceName",
			"UnidentifiedResourceName",
			"Description",
			"SlotCount",
			"ClassNum",
		};

		public static ItemInfoCsvImportResult Import(string csvPath) {
			var result = new ItemInfoCsvImportResult { CsvPath = csvPath };

			if (string.IsNullOrWhiteSpace(csvPath) || !File.Exists(csvPath)) {
				result.Messages.Add("CSV não encontrado.");
				return result;
			}

			var lines = File.ReadAllLines(csvPath);
			if (lines.Length == 0) {
				result.Messages.Add("CSV vazio.");
				return result;
			}

			var header = _split(lines[0]);
			var map = _mapColumns(header);
			result.MissingHeaders.AddRange(RequiredHeaders.Where(h => !map.ContainsKey(h)));

			if (result.MissingHeaders.Count > 0) {
				result.HeaderValid = false;
				result.Messages.Add("Cabeçalho inválido. Colunas obrigatórias: " + string.Join(", ", RequiredHeaders));
				return result;
			}

			result.HeaderValid = true;

			for (int i = 1; i < lines.Length; i++) {
				var line = lines[i];
				if (string.IsNullOrWhiteSpace(line))
					continue;

				int lineNumber = i + 1;
				var cols = _split(line);

				int itemId;
				if (!int.TryParse(_col(cols, map, "itemid"), out itemId) || itemId <= 0) {
					var bad = new ItemInfoEntry();
					bad.AddIssue("ItemId inválido na linha " + lineNumber + ".");
					result.Rows.Add(new ItemInfoCsvImportRow(bad, lineNumber));
					continue;
				}

				int? slot = _parseInt(_col(cols, map, "slotcount"));
				int? classNum = _parseInt(_col(cols, map, "classnum"));
				string desc = _col(cols, map, "description");

				var entry = new ItemInfoEntry {
					ItemId = itemId,
					IdentifiedDisplayName = _col(cols, map, "identifieddisplayname"),
					UnidentifiedDisplayName = _col(cols, map, "unidentifieddisplayname"),
					IdentifiedResourceName = _col(cols, map, "identifiedresourcename"),
					UnidentifiedResourceName = _col(cols, map, "unidentifiedresourcename"),
					IdentifiedDescription = desc,
					UnidentifiedDescription = desc,
					SlotCount = slot ?? 0,
					Costume = true,
				};

				if (classNum.HasValue && classNum.Value > 0) {
					entry.ClassNum = classNum;
					entry.CostumeViewId = classNum;
				}

				entry.RecomputeValidity();
				result.Rows.Add(new ItemInfoCsvImportRow(entry, lineNumber));
			}

			result.Messages.Add(result.Rows.Count + " linha(s) lida(s) do CSV.");
			return result;
		}

		private static Dictionary<string, int> _mapColumns(List<string> header) {
			var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
			for (int i = 0; i < header.Count; i++) {
				var key = (header[i] ?? "").Trim();
				if (!string.IsNullOrEmpty(key) && !map.ContainsKey(key))
					map[key] = i;
			}

			return map;
		}

		private static string _col(List<string> cols, Dictionary<string, int> map, string name) {
			int idx;
			if (map.TryGetValue(name, out idx) && idx >= 0 && idx < cols.Count)
				return (cols[idx] ?? "").Trim();

			return "";
		}

		private static int? _parseInt(string text) {
			int v;
			return int.TryParse(text, out v) ? v : (int?)null;
		}

		private static List<string> _split(string line) {
			var result = new List<string>();
			var sb = new System.Text.StringBuilder();
			bool inQuotes = false;

			for (int i = 0; i < line.Length; i++) {
				char c = line[i];
				if (c == '"') {
					if (inQuotes && i + 1 < line.Length && line[i + 1] == '"') {
						sb.Append('"');
						i++;
					}
					else {
						inQuotes = !inQuotes;
					}

					continue;
				}

				if (c == ',' && !inQuotes) {
					result.Add(sb.ToString());
					sb.Clear();
					continue;
				}

				sb.Append(c);
			}

			result.Add(sb.ToString());
			return result;
		}
	}
}
