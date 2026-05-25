using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using ErrorManager;
using GRF.Core;
using GRFEditor.ApplicationConfiguration;
using GRFEditor.Core.ProjectProfiles;
using GRFEditor.Tools.CustomAccessory;
using TokeiLibrary;
using TokeiLibrary.WPF;
using TokeiLibrary.WPF.Styles;
using Utilities.Extension;

namespace GRFEditor.Core.ItemInfo {
	public partial class ItemInfoBuilderDialog : TkWindow {
		private readonly GrfHolder _grfHolder;
		private readonly ObservableCollection<ItemInfoCsvImportRow> _batchRows = new ObservableCollection<ItemInfoCsvImportRow>();
		private ItemInfoCsvImportResult _lastCsvImport;

		public ItemInfoBuilderDialog(GrfHolder grfHolder)
			: base("ItemInfo builder", "settings.ico", SizeToContent.Manual, ResizeMode.CanResize) {
			_grfHolder = grfHolder;
			InitializeComponent();
			Owner = WpfUtilities.TopWindow;

			_gridBatch.ItemsSource = _batchRows;
			_loadItemInfoPathHint();
		}

		private void _loadItemInfoPathHint() {
			string path = ActiveProjectProfile.GetItemInfoPath();
			if (!string.IsNullOrEmpty(path))
				_pbItemInfoPath.Text = path;
		}

		private ItemInfoEntry _buildEntryFromForm() {
			int itemId;
			int.TryParse(_tbItemId.Text?.Trim(), out itemId);

			int slot = 0;
			int.TryParse(_tbSlotCount.Text?.Trim(), out slot);

			int? classNum = null;
			int cn;
			if (int.TryParse(_tbClassNum.Text?.Trim(), out cn) && cn > 0)
				classNum = cn;

			string desc = _tbDescription.Text?.Trim();
			var entry = new ItemInfoEntry {
				ItemId = itemId,
				IdentifiedDisplayName = _tbIdentifiedDisplay.Text?.Trim(),
				UnidentifiedDisplayName = _tbUnidentifiedDisplay.Text?.Trim(),
				IdentifiedResourceName = _tbIdentifiedResource.Text?.Trim(),
				UnidentifiedResourceName = _tbUnidentifiedResource.Text?.Trim(),
				IdentifiedDescription = desc,
				UnidentifiedDescription = desc,
				SlotCount = slot,
				ClassNum = classNum,
				CostumeViewId = classNum,
				Costume = true,
			};

			entry.RecomputeValidity();
			return entry;
		}

		private void _applyEntryToForm(ItemInfoEntry entry) {
			if (entry == null)
				return;

			_tbItemId.Text = entry.ItemId > 0 ? entry.ItemId.ToString() : "";
			_tbIdentifiedDisplay.Text = entry.IdentifiedDisplayName ?? "";
			_tbUnidentifiedDisplay.Text = entry.UnidentifiedDisplayName ?? "";
			_tbIdentifiedResource.Text = entry.IdentifiedResourceName ?? "";
			_tbUnidentifiedResource.Text = entry.UnidentifiedResourceName ?? "";
			_tbDescription.Text = entry.IdentifiedDescription ?? "";
			_tbSlotCount.Text = (entry.SlotCount ?? 0).ToString();
			_tbClassNum.Text = entry.EffectiveViewId?.ToString() ?? "";
		}

		private ItemInfoValidationOptions _buildValidationOptions() {
			var options = new ItemInfoValidationOptions {
				Grf = _grfHolder != null && _grfHolder.IsOpened ? _grfHolder : null,
				ClientDataFolder = ActiveProjectProfile.GetClientFolderPath(),
				ValidateTextures = true,
			};

			var locations = CustomAccessoryLubLocations.Resolve(_grfHolder);
			if (locations.IsValid)
				options.KnownViewIds = ItemInfoService.LoadKnownViewIdsFromAccessoryId(_grfHolder, locations.EditAccessoryIdPath);

			return options;
		}

		private void _validateImagePaths(ItemInfoEntry entry, IList<string> issues) {
			string itemImg = _pbItemImage.Text?.Trim();
			if (!string.IsNullOrEmpty(itemImg) && !File.Exists(itemImg))
				issues.Add("Item image não encontrado: " + itemImg);

			string collImg = _pbCollectionImage.Text?.Trim();
			if (!string.IsNullOrEmpty(collImg) && !File.Exists(collImg))
				issues.Add("Collection image não encontrado: " + collImg);
		}

		private string _getTargetItemInfoPath(bool promptIfMissing) {
			string path = _pbItemInfoPath.Text?.Trim();
			if (!string.IsNullOrEmpty(path) && File.Exists(path))
				return path;

			if (!promptIfMissing)
				return null;

			path = PathRequest.OpenFileEditor(
				"filter", "Lua files|*.lub;*.lua",
				"fileName", "iteminfo.lua");

			if (!string.IsNullOrEmpty(path) && File.Exists(path))
				_pbItemInfoPath.Text = path;

			return path;
		}

		private void _buttonGenerate_Click(object sender, RoutedEventArgs e) {
			try {
				var entry = _buildEntryFromForm();
				if (entry.ItemId <= 0) {
					MessageBox.Show(this, "Informe um ItemId válido.", "Gerar", MessageBoxButton.OK, MessageBoxImage.Warning);
					return;
				}

				if (!entry.EffectiveViewId.HasValue || entry.EffectiveViewId.Value <= 0) {
					MessageBox.Show(this, "Informe ClassNum / ViewId.", "Gerar", MessageBoxButton.OK, MessageBoxImage.Warning);
					return;
				}

				_tbGeneratedBlock.Text = ItemInfoService.GenerateLuaBlock(entry);
				_textSummary.Text = "Bloco gerado para ItemId " + entry.ItemId + ".";
			}
			catch (Exception ex) {
				ErrorHandler.HandleException(ex);
			}
		}

		private void _buttonValidate_Click(object sender, RoutedEventArgs e) {
			try {
				if (_tabs.SelectedIndex == 1)
					_validateBatch();
				else
					_validateSingle();
			}
			catch (Exception ex) {
				ErrorHandler.HandleException(ex);
			}
		}

		private void _validateSingle() {
			var entry = _buildEntryFromForm();
			_validateEntry(entry);

			_tbValidation.Text = entry.IsValid
				? "Validação OK."
				: string.Join(Environment.NewLine, entry.Issues);

			_textSummary.Text = entry.IsValid
				? "Entrada válida."
				: entry.Issues.Count + " problema(s) encontrado(s).";
		}

		private void _validateEntry(ItemInfoEntry entry) {
			entry.Issues.Clear();
			var options = _buildValidationOptions();
			var summary = ItemInfoService.ValidateEntries(new[] { entry }, options);
			_validateImagePaths(entry, entry.Issues);

			foreach (string g in summary.GlobalIssues)
				entry.AddIssue(g);

			entry.RecomputeValidity();
		}

		private void _validateBatch() {
			if (_batchRows.Count == 0) {
				MessageBox.Show(this, "Importe um CSV primeiro.", "Validar", MessageBoxButton.OK, MessageBoxImage.Information);
				return;
			}

			_runBatchValidation();
			_updateBatchReport();

			int applicable = _batchRows.Count(r => r.CanApply);
			int invalid = _batchRows.Count(r => !r.CanApply);
			_textSummary.Text = string.Format("Lote: {0} aplicáveis, {1} inválidas.", applicable, invalid);
		}

		private void _runBatchValidation() {
			if (_lastCsvImport == null && _batchRows.Count > 0) {
				_lastCsvImport = new ItemInfoCsvImportResult();
				_lastCsvImport.Rows.AddRange(_batchRows);
			}

			if (_lastCsvImport == null)
				return;

			string itemInfoPath = _getTargetItemInfoPath(false);
			ItemInfoCsvBatchValidator.Validate(_lastCsvImport, itemInfoPath, _buildValidationOptions());
			_gridBatch.Items.Refresh();
		}

		private void _updateBatchReport() {
			if (_lastCsvImport != null)
				_textBatchReport.Text = _lastCsvImport.BuildPreviewReport();
			else if (_batchRows.Count == 0)
				_textBatchReport.Text = "";
			else
				_textBatchReport.Text = _batchRows.Count + " linha(s). Selecionadas aplicáveis: "
					+ _batchRows.Count(r => r.Selected && r.CanApply);
		}

		private void _buttonCopy_Click(object sender, RoutedEventArgs e) {
			try {
				string text = _tbGeneratedBlock.Text;
				if (string.IsNullOrWhiteSpace(text)) {
					_buttonGenerate_Click(sender, e);
					text = _tbGeneratedBlock.Text;
				}

				if (string.IsNullOrWhiteSpace(text)) {
					MessageBox.Show(this, "Gere o bloco Lua primeiro.", "Copiar", MessageBoxButton.OK, MessageBoxImage.Information);
					return;
				}

				Clipboard.SetDataObject(text);
				_textSummary.Text = "Bloco copiado para a área de transferência.";
			}
			catch (Exception ex) {
				ErrorHandler.HandleException(ex);
			}
		}

		private void _buttonSaveFile_Click(object sender, RoutedEventArgs e) {
			try {
				string block = _tbGeneratedBlock.Text;
				if (string.IsNullOrWhiteSpace(block)) {
					_buttonGenerate_Click(sender, e);
					block = _tbGeneratedBlock.Text;
				}

				if (string.IsNullOrWhiteSpace(block)) {
					MessageBox.Show(this, "Nada para salvar.", "Salvar", MessageBoxButton.OK, MessageBoxImage.Warning);
					return;
				}

				string folder = ActiveProjectProfile.GetExportFolderPath();
				string path = folder != null && Directory.Exists(folder)
					? PathRequest.SaveFileEditor("filter", "Lua (*.lua)|*.lua", "fileName", "iteminfo_snippet.lua", "initialDirectory", folder)
					: PathRequest.SaveFileEditor("filter", "Lua (*.lua)|*.lua", "fileName", "iteminfo_snippet.lua");

				if (path == null)
					return;

				File.WriteAllText(path, block, new UTF8Encoding(true));
				Utilities.Services.OpeningService.FileOrFolder(path);
			}
			catch (Exception ex) {
				ErrorHandler.HandleException(ex);
			}
		}

		private void _buttonInsert_Click(object sender, RoutedEventArgs e) {
			try {
				if (_tabs.SelectedIndex == 1) {
					_insertBatch();
					return;
				}

				_insertSingle();
			}
			catch (Exception ex) {
				ErrorHandler.HandleException(ex);
			}
		}

		private void _insertSingle() {
			var entry = _buildEntryFromForm();
			if (entry.ItemId <= 0) {
				MessageBox.Show(this, "ItemId inválido.", "Inserir", MessageBoxButton.OK, MessageBoxImage.Warning);
				return;
			}

			_validateEntry(entry);
			if (!entry.IsValid && entry.Issues.Count > 0) {
				if (MessageBox.Show(this,
					"Há erros de validação. Inserir mesmo assim?",
					"Inserir",
					MessageBoxButton.YesNo,
					MessageBoxImage.Warning) != MessageBoxResult.Yes)
					return;
			}

			string path = _getTargetItemInfoPath(true);
			if (string.IsNullOrEmpty(path))
				return;

			string before = ItemInfoInsertService.ReadItemInfoText(path) ?? "";
			string after = ItemInfoInsertService.PreviewUpsert(before, entry);
			string diff = ItemInfoInsertService.BuildFileDiff(before, after);

			var dialog = new ItemInfoDiffDialog(
				diff,
				path,
				"Será criado backup .bak antes de aplicar.") { Owner = this };

			if (dialog.ShowDialog() != true)
				return;

			var result = ItemInfoInsertService.ApplyUpsert(path, entry);
			MessageBox.Show(this,
				"iteminfo atualizado." + Environment.NewLine
				+ "Backup: " + (result.BackupPath ?? "(nenhum)") + Environment.NewLine
				+ "O GRF não foi salvo automaticamente.",
				"Sucesso",
				MessageBoxButton.OK,
				MessageBoxImage.Information);

			_textSummary.Text = "ItemId " + entry.ItemId + " inserido em " + path;
		}

		private void _insertBatch() {
			if (_batchRows.Count == 0) {
				MessageBox.Show(this, "Importe um CSV primeiro.", "Inserir", MessageBoxButton.OK, MessageBoxImage.Information);
				return;
			}

			_runBatchValidation();

			var toApply = _batchRows.Where(r => r.Selected && r.CanApply).Select(r => r.Entry).ToList();
			if (toApply.Count == 0) {
				MessageBox.Show(this,
					"Nenhuma linha válida selecionada. Linhas inválidas ou duplicadas no CSV não são aplicadas.",
					"Inserir",
					MessageBoxButton.OK,
					MessageBoxImage.Warning);
				return;
			}

			int skipped = _batchRows.Count(r => r.Selected && !r.CanApply);
			if (skipped > 0) {
				if (MessageBox.Show(this,
					skipped + " linha(s) selecionada(s) são inválidas e serão ignoradas. Continuar com "
					+ toApply.Count + " linha(s)?",
					"Inserir",
					MessageBoxButton.YesNo,
					MessageBoxImage.Question) != MessageBoxResult.Yes)
					return;
			}

			string path = _getTargetItemInfoPath(true);
			if (string.IsNullOrEmpty(path))
				return;

			string before = ItemInfoInsertService.ReadItemInfoText(path) ?? "";
			string after = before;
			foreach (var entry in toApply.OrderBy(e => e.ItemId))
				after = ItemInfoInsertService.PreviewUpsert(after, entry);

			string diff = ItemInfoInsertService.BuildFileDiff(before, after);
			var dialog = new ItemInfoDiffDialog(
				diff,
				path,
				"Aplicando " + toApply.Count + " bloco(s) válido(s). Backup .bak será criado.") { Owner = this };

			if (dialog.ShowDialog() != true)
				return;

			var result = ItemInfoInsertService.ApplyUpsertMany(path, toApply);
			MessageBox.Show(this,
				toApply.Count + " bloco(s) aplicados." + Environment.NewLine
				+ "Backup: " + (result.BackupPath ?? "(nenhum)") + Environment.NewLine
				+ "O GRF não foi salvo automaticamente.",
				"Sucesso",
				MessageBoxButton.OK,
				MessageBoxImage.Information);

			_textSummary.Text = toApply.Count + " bloco(s) inseridos em " + path;
		}

		private void _buttonExportJson_Click(object sender, RoutedEventArgs e) {
			try {
				string json;
				string defaultName;

				if (_tabs.SelectedIndex == 1 && _batchRows.Count > 0) {
					json = ItemInfoBuilderExport.ToJson(_batchRows.Where(r => r.Selected).Select(r => r.Entry));
					defaultName = "iteminfo-batch.json";
				}
				else {
					json = ItemInfoBuilderExport.ToJson(_buildEntryFromForm());
					defaultName = "iteminfo-entry.json";
				}

				string folder = ActiveProjectProfile.GetExportFolderPath();
				string path = folder != null && Directory.Exists(folder)
					? PathRequest.SaveFileEditor("filter", "JSON (*.json)|*.json", "fileName", defaultName, "initialDirectory", folder)
					: PathRequest.SaveFileEditor("filter", "JSON (*.json)|*.json", "fileName", defaultName);

				if (path == null)
					return;

				if (!path.IsExtension(".json"))
					path += ".json";

				File.WriteAllText(path, json, new UTF8Encoding(true));
				Utilities.Services.OpeningService.FileOrFolder(path);
			}
			catch (Exception ex) {
				ErrorHandler.HandleException(ex);
			}
		}

		private void _buttonImportCsv_Click(object sender, RoutedEventArgs e) {
			try {
				string path = PathRequest.OpenFileEditor("filter", "CSV (*.csv)|*.csv");
				if (string.IsNullOrEmpty(path))
					return;

				var import = ItemInfoBuilderCsvImporter.Import(path);
				if (!import.HeaderValid) {
					MessageBox.Show(this,
						string.Join(Environment.NewLine, import.Messages.Concat(import.MissingHeaders.Select(h => "Falta: " + h))),
						"Cabeçalho CSV inválido",
						MessageBoxButton.OK,
						MessageBoxImage.Warning);
					return;
				}

				string itemInfoPath = _getTargetItemInfoPath(false);
				ItemInfoCsvBatchValidator.Validate(import, itemInfoPath, _buildValidationOptions());

				var preview = new ItemInfoCsvPreviewDialog(import) { Owner = this };
				if (preview.ShowDialog() != true)
					return;

				_lastCsvImport = import;
				_batchRows.Clear();
				foreach (var row in import.Rows)
					_batchRows.Add(row);

				_updateBatchReport();
				_textSummary.Text = import.SelectedApplicableCount + " linha(s) selecionada(s) para aplicar (de "
					+ import.ApplicableCount + " válidas).";
				_tabs.SelectedIndex = 1;
			}
			catch (Exception ex) {
				ErrorHandler.HandleException(ex);
			}
		}

		private void _buttonSelectAllValid_Click(object sender, RoutedEventArgs e) {
			foreach (var row in _batchRows)
				row.Selected = row.CanApply;

			_gridBatch.Items.Refresh();
			_updateBatchReport();
		}

		private void _buttonSelectNone_Click(object sender, RoutedEventArgs e) {
			foreach (var row in _batchRows)
				row.Selected = false;

			_gridBatch.Items.Refresh();
			_updateBatchReport();
		}

		private void _buttonClose_Click(object sender, RoutedEventArgs e) {
			Close();
		}
	}
}
