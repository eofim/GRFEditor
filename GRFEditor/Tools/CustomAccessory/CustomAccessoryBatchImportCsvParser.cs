using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace GRFEditor.Tools.CustomAccessory {
	public static class CustomAccessoryBatchImportCsvParser {
		public static readonly string[] ExpectedHeaders = { "SpriteFile", "ConstantName", "DisplayName", "ViewId" };

		public static List<CustomAccessoryBatchImportCsvRow> Parse(string csvPath, IList<string> parseMessages) {
			var rows = new List<CustomAccessoryBatchImportCsvRow>();
			if (string.IsNullOrWhiteSpace(csvPath) || !File.Exists(csvPath))
				return rows;

			var lines = File.ReadAllLines(csvPath);
			if (lines.Length == 0) {
				parseMessages?.Add("CSV vazio: " + csvPath);
				return rows;
			}

			int startLine = 0;
			var header = _splitCsvLine(lines[0]);
			int spriteIdx = -1, constantIdx = -1, displayIdx = -1, viewIdIdx = -1;

			if (_looksLikeHeader(header)) {
				for (int i = 0; i < header.Count; i++) {
					var col = (header[i] ?? "").Trim();
					if (col.Equals("SpriteFile", StringComparison.OrdinalIgnoreCase))
						spriteIdx = i;
					else if (col.Equals("ConstantName", StringComparison.OrdinalIgnoreCase))
						constantIdx = i;
					else if (col.Equals("DisplayName", StringComparison.OrdinalIgnoreCase))
						displayIdx = i;
					else if (col.Equals("ViewId", StringComparison.OrdinalIgnoreCase))
						viewIdIdx = i;
				}

				if (spriteIdx < 0) {
					parseMessages?.Add("CSV sem coluna SpriteFile no cabeçalho.");
					return rows;
				}

				startLine = 1;
			}
			else {
				spriteIdx = 0;
				constantIdx = header.Count > 1 ? 1 : -1;
				displayIdx = header.Count > 2 ? 2 : -1;
				viewIdIdx = header.Count > 3 ? 3 : -1;
			}

			for (int lineNum = startLine; lineNum < lines.Length; lineNum++) {
				var line = lines[lineNum];
				if (string.IsNullOrWhiteSpace(line))
					continue;

				var cols = _splitCsvLine(line);
				if (cols.Count == 0 || cols.All(string.IsNullOrWhiteSpace))
					continue;

				string spriteFile = _getColumn(cols, spriteIdx);
				if (string.IsNullOrWhiteSpace(spriteFile)) {
					parseMessages?.Add("Linha " + (lineNum + 1) + ": SpriteFile vazio, ignorada.");
					continue;
				}

				int? viewId = null;
				if (viewIdIdx >= 0) {
					string viewText = _getColumn(cols, viewIdIdx);
					if (!string.IsNullOrWhiteSpace(viewText)) {
						int parsed;
						if (!int.TryParse(viewText.Trim(), out parsed) || parsed <= 0)
							parseMessages?.Add("Linha " + (lineNum + 1) + ": ViewId inválido \"" + viewText + "\".");
						else
							viewId = parsed;
					}
				}

				rows.Add(new CustomAccessoryBatchImportCsvRow {
					LineNumber = lineNum + 1,
					SpriteFile = spriteFile.Trim(),
					ConstantName = constantIdx >= 0 ? _getColumn(cols, constantIdx) : null,
					DisplayName = displayIdx >= 0 ? _getColumn(cols, displayIdx) : null,
					ViewId = viewId,
				});
			}

			return rows;
		}

		public static Dictionary<string, CustomAccessoryBatchImportCsvRow> BuildLookup(IEnumerable<CustomAccessoryBatchImportCsvRow> rows) {
			var lookup = new Dictionary<string, CustomAccessoryBatchImportCsvRow>(StringComparer.OrdinalIgnoreCase);

			foreach (var row in rows ?? Enumerable.Empty<CustomAccessoryBatchImportCsvRow>()) {
				if (string.IsNullOrWhiteSpace(row.SpriteFile))
					continue;

				_addKey(lookup, CustomAccessoryBatchImportService.NormalizeSpriteKey(row.SpriteFile), row);
				_addKey(lookup, Path.GetFileName(row.SpriteFile.Replace('/', '\\')), row);
			}

			return lookup;
		}

		private static void _addKey(
			IDictionary<string, CustomAccessoryBatchImportCsvRow> lookup,
			string key,
			CustomAccessoryBatchImportCsvRow row) {
			if (string.IsNullOrWhiteSpace(key))
				return;

			CustomAccessoryBatchImportCsvRow existing;
			if (lookup.TryGetValue(key, out existing) && !ReferenceEquals(existing, row))
				return;

			lookup[key] = row;
		}

		private static bool _looksLikeHeader(List<string> cols) {
			return cols.Any(c => (c ?? "").Trim().Equals("SpriteFile", StringComparison.OrdinalIgnoreCase));
		}

		private static string _getColumn(List<string> cols, int index) {
			if (index < 0 || index >= cols.Count)
				return null;

			var value = cols[index];
			return value == null ? null : value.Trim();
		}

		private static List<string> _splitCsvLine(string line) {
			var result = new List<string>();
			if (line == null)
				return result;

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
