using System.Collections.Generic;
using System.Linq;

namespace GRFEditor.Core.ItemInfo {
	public sealed class ItemInfoCsvImportResult {
		public string CsvPath { get; set; }
		public bool HeaderValid { get; set; }
		public List<string> Messages { get; } = new List<string>();
		public List<string> MissingHeaders { get; } = new List<string>();
		public List<ItemInfoCsvImportRow> Rows { get; } = new List<ItemInfoCsvImportRow>();

		public int DuplicateItemIdCount { get; set; }
		public int ExistsInItemInfoCount { get; set; }
		public int MissingViewIdCount { get; set; }
		public int ApplicableCount => Rows.Count(r => r.CanApply);
		public int SelectedApplicableCount => Rows.Count(r => r.Selected && r.CanApply);

		public string BuildPreviewReport() {
			var lines = new List<string>();
			lines.Add("=== Prévia importação CSV ===");
			lines.Add("Arquivo: " + (CsvPath ?? ""));
			lines.Add("Cabeçalho válido: " + (HeaderValid ? "sim" : "não"));

			if (MissingHeaders.Count > 0)
				lines.Add("Colunas ausentes: " + string.Join(", ", MissingHeaders));

			lines.Add(string.Format(
				"Linhas: {0} | aplicáveis: {1} | duplicadas no CSV: {2} | já no iteminfo: {3} | ViewId ausente: {4}",
				Rows.Count,
				ApplicableCount,
				DuplicateItemIdCount,
				ExistsInItemInfoCount,
				MissingViewIdCount));

			foreach (string msg in Messages)
				lines.Add("  " + msg);

			lines.Add("");
			lines.Add("--- Linhas com problemas ---");
			foreach (var row in Rows.Where(r => !r.CanApply).OrderBy(r => r.LineNumber)) {
				lines.Add("  L" + row.LineNumber + " ItemId " + row.ItemId + ": " + row.IssuesText);
			}

			return string.Join(System.Environment.NewLine, lines);
		}
	}
}
