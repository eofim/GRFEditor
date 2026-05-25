using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using GRFEditor.Core.AccessoryScanner;

namespace GRFEditor.Tools.CustomAccessory {
	public sealed class CustomAccessoryBatchImportResult {
		public string SourceFolder { get; set; }
		public string CsvPath { get; set; }
		public AccessoryScanResult ScanResult { get; set; }
		public List<CustomAccessoryManagerEntry> ManagerEntries { get; } = new List<CustomAccessoryManagerEntry>();
		public List<string> OrphanActPaths { get; } = new List<string>();
		public List<string> ReportLines { get; } = new List<string>();
		public List<string> CsvUnmatchedRows { get; } = new List<string>();
		public CustomAccessoryManagerSaveValidation ImportValidation { get; set; }

		public string ReportText => string.Join(Environment.NewLine, ReportLines);

		public int SprCount { get; set; }
		public int PairedActCount { get; set; }
		public int MissingActCount { get; set; }

		public void AppendValidationToReport() {
			if (ImportValidation == null)
				return;

			if (ImportValidation.Errors.Count > 0) {
				ReportLines.Add("");
				ReportLines.Add("Erros de validação (impedem gravação Lua):");
				ReportLines.AddRange(ImportValidation.Errors);
			}

			if (ImportValidation.ViewIdWarnings.Count > 0) {
				ReportLines.Add("");
				ReportLines.Add("Avisos de viewId (confirmação necessária ao gravar):");
				ReportLines.AddRange(ImportValidation.ViewIdWarnings);
			}
		}

		public static string FormatSummary(CustomAccessoryBatchImportResult result) {
			if (result == null)
				return "";

			var sb = new StringBuilder();
			sb.AppendLine("Pasta: " + (result.SourceFolder ?? ""));
			if (!string.IsNullOrEmpty(result.CsvPath))
				sb.AppendLine("CSV: " + result.CsvPath);

			sb.AppendLine(string.Format(
				".spr: {0} | com .act: {1} | sem .act: {2} | .act órfãos: {3}",
				result.SprCount,
				result.PairedActCount,
				result.MissingActCount,
				result.OrphanActPaths.Count));

			int selected = result.ManagerEntries.Count(e => e.Selected);
			sb.AppendLine("Linhas na grade: " + result.ManagerEntries.Count + " | selecionadas para gravar: " + selected);

			if (result.CsvUnmatchedRows.Count > 0)
				sb.AppendLine("Linhas CSV sem .spr correspondente: " + result.CsvUnmatchedRows.Count);

			return sb.ToString();
		}
	}
}
