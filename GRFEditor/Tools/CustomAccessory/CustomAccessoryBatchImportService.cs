using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GRF.Core;
using GRFEditor.Core.AccessoryScanner;
using Utilities.Extension;

namespace GRFEditor.Tools.CustomAccessory {
	public static class CustomAccessoryBatchImportService {
		public static CustomAccessoryBatchImportResult Import(
			string folder,
			string csvPath,
			GrfHolder grf,
			CustomAccessoryLuaTables tables,
			string accessoryIdPath,
			string accnamePath) {
			if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
				throw new DirectoryNotFoundException("Pasta de importação inválida: " + folder);

			var result = new CustomAccessoryBatchImportResult {
				SourceFolder = Path.GetFullPath(folder),
				CsvPath = string.IsNullOrWhiteSpace(csvPath) ? null : Path.GetFullPath(csvPath),
			};

			var sprPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			var actPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			_collectFromFolder(result.SourceFolder, sprPaths, actPaths);

			result.OrphanActPaths.AddRange(_findOrphanActs(sprPaths, actPaths));
			result.SprCount = sprPaths.Count;
			result.PairedActCount = sprPaths.Count(s => actPaths.Contains(_actPathForSprite(s)));
			result.MissingActCount = result.SprCount - result.PairedActCount;

			var scanner = new AccessoryScannerService();
			var scanInput = new AccessoryScannerInput {
				Grf = grf != null && grf.IsOpened ? grf : null,
				LocalSpriteFolder = result.SourceFolder,
				AccessoryIdPath = accessoryIdPath,
				AccnamePath = accnamePath,
			};

			result.ScanResult = scanner.Scan(scanInput);
			var spriteEntries = result.ScanResult.Entries
				.Where(e => !string.IsNullOrWhiteSpace(e.SpritePath))
				.ToList();

			Dictionary<string, CustomAccessoryBatchImportCsvRow> csvLookup = null;
			var csvRows = new List<CustomAccessoryBatchImportCsvRow>();

			if (!string.IsNullOrWhiteSpace(csvPath) && File.Exists(csvPath)) {
				csvRows = CustomAccessoryBatchImportCsvParser.Parse(csvPath, result.ReportLines);
				csvLookup = CustomAccessoryBatchImportCsvParser.BuildLookup(csvRows);
			}

			int nextViewId = tables != null ? tables.GetNextViewId() : 1;
			var matchedCsvKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

			foreach (var scanEntry in spriteEntries) {
				var row = new CustomAccessoryManagerEntry(scanEntry, grf, result.SourceFolder);
				_applyCsvOverrides(row, csvLookup, matchedCsvKeys, result);
				row.ApplySuggestions(tables, ref nextViewId);
				row.RefreshWriteStatus(tables);
				result.ManagerEntries.Add(row);
			}

			if (csvLookup != null) {
				foreach (var csvRow in csvRows) {
					var key = NormalizeSpriteKey(csvRow.SpriteFile);
					var fileKey = Path.GetFileName(csvRow.SpriteFile.Replace('/', '\\'));
					if (!matchedCsvKeys.Contains(key) && !matchedCsvKeys.Contains(fileKey))
						result.CsvUnmatchedRows.Add("Linha " + csvRow.LineNumber + ": " + csvRow.SpriteFile);
				}
			}

			result.ImportValidation = CustomAccessoryManagerSaveValidator.Validate(result.ManagerEntries, tables);

			_buildReport(result, csvRows.Count);
			result.AppendValidationToReport();

			return result;
		}

		public static string NormalizeSpriteKey(string path) {
			if (string.IsNullOrWhiteSpace(path))
				return "";

			return path.Replace('\\', '/').Trim().TrimStart('/');
		}

		private static void _applyCsvOverrides(
			CustomAccessoryManagerEntry row,
			Dictionary<string, CustomAccessoryBatchImportCsvRow> csvLookup,
			HashSet<string> matchedCsvKeys,
			CustomAccessoryBatchImportResult result) {
			if (csvLookup == null || csvLookup.Count == 0)
				return;

			CustomAccessoryBatchImportCsvRow csvRow;
			if (!_tryMatchCsv(row.SpritePath, csvLookup, out csvRow))
				return;

			matchedCsvKeys.Add(NormalizeSpriteKey(csvRow.SpriteFile));
			matchedCsvKeys.Add(NormalizeSpriteKey(row.SpritePath));
			matchedCsvKeys.Add(Path.GetFileName(csvRow.SpriteFile.Replace('/', '\\')));

			if (!string.IsNullOrWhiteSpace(csvRow.ConstantName))
				row.ConstantName = CustomAccessoryNaming.NormalizeConstantName(csvRow.ConstantName);

			if (!string.IsNullOrWhiteSpace(csvRow.DisplayName))
				row.DisplayName = csvRow.DisplayName;

			if (csvRow.ViewId.HasValue && csvRow.ViewId.Value > 0)
				row.ViewId = csvRow.ViewId.Value;

			if (row.ScanEntry?.Issues != null)
				row.ScanEntry.Issues.Add("Valores aplicados do CSV.");
		}

		private static bool _tryMatchCsv(
			string spritePath,
			Dictionary<string, CustomAccessoryBatchImportCsvRow> csvLookup,
			out CustomAccessoryBatchImportCsvRow csvRow) {
			csvRow = null;
			if (csvLookup == null || string.IsNullOrWhiteSpace(spritePath))
				return false;

			var keys = new[] {
				NormalizeSpriteKey(spritePath),
				Path.GetFileName(spritePath.Replace('/', '\\')),
			};

			foreach (var key in keys) {
				if (!string.IsNullOrEmpty(key) && csvLookup.TryGetValue(key, out csvRow))
					return true;
			}

			return false;
		}

		private static void _buildReport(CustomAccessoryBatchImportResult result, int csvRowCount) {
			result.ReportLines.Clear();
			result.ReportLines.Add("=== Relatório de importação em lote ===");
			result.ReportLines.Add(CustomAccessoryBatchImportResult.FormatSummary(result));
			result.ReportLines.Add("");

			result.ReportLines.Add("--- Pareamento .spr / .act ---");
			result.ReportLines.Add(string.Format(
				"Total .spr: {0} | pareados: {1} | .act ausente: {2}",
				result.SprCount,
				result.PairedActCount,
				result.MissingActCount));

			foreach (var entry in result.ManagerEntries.Where(e => e.ScanStatus == AccessoryScanStatus.MissingAct)) {
				result.ReportLines.Add("  Sem .act: " + entry.SpritePath);
			}

			if (result.OrphanActPaths.Count > 0) {
				result.ReportLines.Add("");
				result.ReportLines.Add("--- .act sem .spr correspondente ---");
				foreach (var act in result.OrphanActPaths.OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
					result.ReportLines.Add("  " + act);
			}

			if (csvRowCount > 0) {
				result.ReportLines.Add("");
				result.ReportLines.Add("--- CSV ---");
				result.ReportLines.Add("Linhas lidas: " + csvRowCount);
				if (result.CsvUnmatchedRows.Count > 0) {
					result.ReportLines.Add("Sem .spr na pasta:");
					result.ReportLines.AddRange(result.CsvUnmatchedRows.Select(p => "  " + p));
				}
			}

			result.ReportLines.Add("");
			result.ReportLines.Add("--- Status na grade ---");
			foreach (AccessoryScanStatus status in Enum.GetValues(typeof(AccessoryScanStatus))) {
				int count = result.ManagerEntries.Count(e => e.ScanStatus == status);
				if (count > 0)
					result.ReportLines.Add("  " + status + ": " + count);
			}

			int ready = result.ManagerEntries.Count(e => e.Selected && e.CanWriteToLua && e.ViewId > 0
				&& !string.IsNullOrWhiteSpace(e.ConstantName));
			result.ReportLines.Add("");
			result.ReportLines.Add("Prontos para gravar Lua (selecionados): " + ready);
			result.ReportLines.Add("Arquivos NÃO foram copiados para o GRF nesta etapa.");
		}

		private static void _collectFromFolder(string rootFolder, HashSet<string> sprPaths, HashSet<string> actPaths) {
			string root = Path.GetFullPath(rootFolder);

			foreach (string file in Directory.GetFiles(root, "*.*", SearchOption.AllDirectories)) {
				string ext = Path.GetExtension(file);
				if (!ext.IsExtension(".spr", ".act"))
					continue;

				string relative = _toRelativePath(root, file).Replace('\\', '/');

				if (ext.Equals(".spr", StringComparison.OrdinalIgnoreCase))
					sprPaths.Add(relative);
				else
					actPaths.Add(relative);
			}
		}

		private static List<string> _findOrphanActs(HashSet<string> sprPaths, HashSet<string> actPaths) {
			var orphans = new List<string>();

			foreach (string act in actPaths) {
				string spr = _sprPathForAct(act);
				if (!sprPaths.Contains(spr))
					orphans.Add(act);
			}

			return orphans;
		}

		private static string _actPathForSprite(string sprPath) {
			string normalized = sprPath.Replace('\\', '/');
			int lastDot = normalized.LastIndexOf('.');
			if (lastDot < 0)
				return normalized + ".act";

			return normalized.Substring(0, lastDot) + ".act";
		}

		private static string _sprPathForAct(string actPath) {
			string normalized = actPath.Replace('\\', '/');
			int lastDot = normalized.LastIndexOf('.');
			if (lastDot < 0)
				return normalized + ".spr";

			return normalized.Substring(0, lastDot) + ".spr";
		}

		private static string _toRelativePath(string root, string fullPath) {
			string relative = fullPath.Substring(root.Length)
				.TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
			return relative.Replace('\\', '/');
		}
	}
}
