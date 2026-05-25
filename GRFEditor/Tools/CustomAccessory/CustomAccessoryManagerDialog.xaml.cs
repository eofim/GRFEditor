using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using ErrorManager;
using GRF.Core;
using GRFEditor.ApplicationConfiguration;
using GRFEditor.Core.AccessoryScanner;
using GRFEditor.Core.ProjectProfiles;
using AsyncOperation = GrfToWpfBridge.Application.AsyncOperation;
using TokeiLibrary;
using TokeiLibrary.WPF;
using TokeiLibrary.WPF.Styles;
using Utilities.Extension;
using Utilities.Services;

namespace GRFEditor.Tools.CustomAccessory {
	public partial class CustomAccessoryManagerDialog : TkWindow {
		private const string FilterAll = "(All)";

		private readonly GrfHolder _grfHolder;
		private readonly AccessoryScannerService _scannerService = new AccessoryScannerService();
		private readonly AsyncOperation _asyncOperation;
		private readonly List<CustomAccessoryManagerEntry> _allEntries = new List<CustomAccessoryManagerEntry>();
		private ICollectionView _entriesView;

		private AccessoryScanResult _lastResult;
		private CustomAccessoryBatchImportResult _lastBatchImportResult;
		private CancellationTokenSource _cancellationSource;
		private string _localSpriteFolder;
		private CustomAccessoryLuaTables _luaTables;

		public CustomAccessoryManagerDialog(GrfHolder grfHolder)
			: base("Custom accessories manager", "convert.png", SizeToContent.Manual, ResizeMode.CanResize) {
			_grfHolder = grfHolder;
			InitializeComponent();

			_asyncOperation = new AsyncOperation(_progressBar);
			_asyncOperation.Cancelling += _asyncOperation_Cancelling;
			Owner = WpfUtilities.TopWindow;

			_entriesView = CollectionViewSource.GetDefaultView(_allEntries);
			_entriesView.Filter = _entryFilter;
			_gridEntries.ItemsSource = _entriesView;

			_initFilters();
			_updateLubHint();
			_updateSaveModeAvailability();
			_progressBar.SetSpecialState(TkProgressBar.ProgressStatus.Finished);
		}

		protected override void OnClosing(CancelEventArgs e) {
			_asyncOperation.Cancel();
			_cancellationSource?.Cancel();
			base.OnClosing(e);
		}

		private void _initFilters() {
			_comboStatus.Items.Add(FilterAll);

			foreach (AccessoryScanStatus status in Enum.GetValues(typeof(AccessoryScanStatus)))
				_comboStatus.Items.Add(status.ToString());

			_comboStatus.SelectedIndex = 0;
		}

		private bool _entryFilter(object item) {
			var row = item as CustomAccessoryManagerEntry;
			if (row == null)
				return false;

			string statusFilter = _comboStatus.SelectedItem as string;
			if (!String.IsNullOrEmpty(statusFilter) && statusFilter != FilterAll && row.Status != statusFilter)
				return false;

			string search = _textSearch?.Text ?? "";
			if (String.IsNullOrWhiteSpace(search))
				return true;

			return (row.SpritePath != null && row.SpritePath.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0)
				|| (row.ActPath != null && row.ActPath.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0)
				|| (row.ConstantName != null && row.ConstantName.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0)
				|| (row.DisplayName != null && row.DisplayName.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0)
				|| (row.Issues != null && row.Issues.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0);
		}

		private void _updateLubHint() {
			var locations = CustomAccessoryLubLocations.Resolve(_grfHolder);
			string id = !String.IsNullOrEmpty(locations.EditAccessoryIdPath)
				? locations.EditAccessoryIdPath
				: "(accessoryid.lub not found)";
			string name = !String.IsNullOrEmpty(locations.EditAccnamePath)
				? locations.EditAccnamePath
				: "(accname.lub not found)";

			_textLubSources.Text = "Lua: " + id + "  |  " + name;
		}

		private void _reloadLuaTables() {
			var locations = CustomAccessoryLubLocations.Resolve(_grfHolder);
			_luaTables = locations.IsValid
				? CustomAccessoryLuaTables.Load(locations.EditAccessoryIdPath, locations.EditAccnamePath)
				: null;
		}

		private void _updateSaveModeAvailability() {
			bool grfOpen = _grfHolder != null && _grfHolder.IsOpened;
			bool external = GrfEditorConfiguration.CustomAccessoryAlsoWriteToDisk;

			_radioGrfInternal.IsEnabled = grfOpen;
			_radioGrfAndDisk.IsEnabled = grfOpen && external;
			_radioExternalDisk.IsEnabled = external;

			if (!grfOpen && (_radioGrfInternal.IsChecked == true || _radioGrfAndDisk.IsChecked == true))
				_radioEditOnly.IsChecked = true;

			if (!external && (_radioExternalDisk.IsChecked == true || _radioGrfAndDisk.IsChecked == true))
				_radioEditOnly.IsChecked = true;
		}

		private CustomAccessoryManagerSaveMode _getSaveMode() {
			if (_radioGrfInternal.IsChecked == true)
				return CustomAccessoryManagerSaveMode.GrfInternal;

			if (_radioExternalDisk.IsChecked == true)
				return CustomAccessoryManagerSaveMode.ExternalDisk;

			if (_radioGrfAndDisk.IsChecked == true)
				return CustomAccessoryManagerSaveMode.GrfAndExternalDisk;

			return CustomAccessoryManagerSaveMode.EditFilesOnly;
		}

		private void _saveMode_Changed(object sender, RoutedEventArgs e) {
			if (!IsLoaded)
				return;

			_updateSaveModeAvailability();
		}

		private bool _tryGetInitialViewId(out int? startViewId, bool showError = false) {
			startViewId = null;
			var text = _textInitialViewId?.Text?.Trim() ?? "";

			if (String.IsNullOrEmpty(text))
				return true;

			int parsed;
			if (!int.TryParse(text, out parsed) || parsed <= 0) {
				if (showError) {
					MessageBox.Show(this,
						"ID inicial deve ser um inteiro positivo.",
						"ID inicial",
						MessageBoxButton.OK,
						MessageBoxImage.Warning);
				}

				return false;
			}

			startViewId = parsed;
			return true;
		}

		private void _buttonScanGrf_Click(object sender, RoutedEventArgs e) {
			if (_grfHolder == null || !_grfHolder.IsOpened) {
				ErrorHandler.HandleException("Open a GRF first to scan container files.", ErrorLevel.Low);
				return;
			}

			_startScan(scanGrf: true, newFolder: null);
		}

		private void _buttonScanFolder_Click(object sender, RoutedEventArgs e) {
			string folder = PathRequest.FolderEditor();

			if (String.IsNullOrEmpty(folder))
				return;

			_localSpriteFolder = folder;
			_startScan(scanGrf: false, newFolder: folder);
		}

		private void _buttonBatchImport_Click(object sender, RoutedEventArgs e) {
			try {
				if (_asyncOperation.IsRunning) {
					ErrorHandler.HandleException("Aguarde a operação atual ou cancele-a.", ErrorLevel.Low);
					return;
				}

				string folder = PathRequest.FolderEditor();
				if (String.IsNullOrEmpty(folder))
					return;

				string csvPath = null;
				var csvChoice = MessageBox.Show(this,
					"Deseja carregar um CSV opcional com colunas SpriteFile, ConstantName, DisplayName e ViewId?",
					"Importar lote",
					MessageBoxButton.YesNoCancel,
					MessageBoxImage.Question);

				if (csvChoice == MessageBoxResult.Cancel)
					return;

				if (csvChoice == MessageBoxResult.Yes) {
					csvPath = PathRequest.OpenFileEditor(
						"filter", "CSV (*.csv)|*.csv|Todos (*.*)|*.*",
						"initialDirectory", folder);

					if (String.IsNullOrEmpty(csvPath))
						return;
				}

				_localSpriteFolder = folder;
				var locations = CustomAccessoryLubLocations.Resolve(_grfHolder);
				string accessoryIdPath = locations.EditAccessoryIdPath ?? locations.AccessoryIdGrfPath;
				string accnamePath = locations.EditAccnamePath ?? locations.AccnameGrfPath;

				_setDataControlsEnabled(false);
				_textSummary.Text = "Importando lote de " + folder + "...";
				_progressBar.Progress = 0;

				_cancellationSource?.Dispose();
				_cancellationSource = new CancellationTokenSource();
				var importFolder = folder;
				var importCsv = csvPath;

				_asyncOperation.SetAndRunOperation(
					prog => {
						var tables = locations.IsValid
							? CustomAccessoryLuaTables.Load(locations.EditAccessoryIdPath, locations.EditAccnamePath)
							: null;

						_lastBatchImportResult = CustomAccessoryBatchImportService.Import(
							importFolder,
							importCsv,
							_grfHolder,
							tables,
							accessoryIdPath,
							accnamePath);
					},
					_onBatchImportFinished);
			}
			catch (Exception err) {
				ErrorHandler.HandleException(err);
				_setDataControlsEnabled(_allEntries.Count > 0);
			}
		}

		private void _onBatchImportFinished(object state) {
			try {
				this.Dispatch(delegate {
					var importResult = _lastBatchImportResult;
					_setDataControlsEnabled(true);
					_progressBar.SetSpecialState(TkProgressBar.ProgressStatus.Finished);

					if (importResult == null) {
						_textSummary.Text = "Importação em lote concluída sem resultado.";
						return;
					}

					var reportDialog = new CustomAccessoryBatchImportReportDialog(importResult) { Owner = this };
					if (reportDialog.ShowDialog() != true)
						return;

					_applyBatchImportResult(importResult);
				});
			}
			catch (Exception err) {
				ErrorHandler.HandleException(err);
				this.Dispatch(delegate {
					_setDataControlsEnabled(_allEntries.Count > 0);
					_progressBar.SetSpecialState(TkProgressBar.ProgressStatus.ErrorsDetected);
				});
			}
		}

		private void _applyBatchImportResult(CustomAccessoryBatchImportResult importResult) {
			_reloadLuaTables();
			_lastResult = importResult.ScanResult;
			_allEntries.Clear();
			_allEntries.AddRange(importResult.ManagerEntries);
			_entriesView.Refresh();
			_updateLubHint();

			int shown = _allEntries.Count(_entryFilter);
			int selected = _allEntries.Count(e => e.Selected);
			_textSummary.Text = String.Format(
				"Importação: {0} itens na grade ({1} selecionados, {2} visíveis). Arquivos não copiados ao GRF.",
				_allEntries.Count,
				selected,
				shown);

			if (importResult.ImportValidation != null && importResult.ImportValidation.HasErrors)
				_textSummary.Text += " Validação: " + importResult.ImportValidation.Errors.Count + " erro(s).";
		}

		private void _buttonExportImportPreview_Click(object sender, RoutedEventArgs e) {
			try {
				var rows = _allEntries.ToList();
				if (rows.Count == 0) {
					ErrorHandler.HandleException("Não há linhas para exportar.", ErrorLevel.Low);
					return;
				}

				string exportFolder = ActiveProjectProfile.GetExportFolderPath();
				string initialDirectory = !String.IsNullOrEmpty(exportFolder) && Directory.Exists(exportFolder)
					? exportFolder
					: (!String.IsNullOrEmpty(_localSpriteFolder) && Directory.Exists(_localSpriteFolder)
						? _localSpriteFolder
						: null);

				string path = initialDirectory == null
					? PathRequest.SaveFileEditor("filter", "Import preview (*.csv)|*.csv", "fileName", "accessory-import-preview.csv")
					: PathRequest.SaveFileEditor("filter", "Import preview (*.csv)|*.csv", "fileName", "accessory-import-preview.csv", "initialDirectory", initialDirectory);

				if (path == null)
					return;

				if (!path.IsExtension(".csv"))
					path += ".csv";

				CustomAccessoryManagerExport.WriteUtf8(path, CustomAccessoryManagerExport.ToImportPreviewCsv(rows));
				OpeningService.FileOrFolder(path);
			}
			catch (Exception err) {
				ErrorHandler.HandleException(err);
			}
		}

		private void _startScan(bool scanGrf, string newFolder) {
			try {
				if (_asyncOperation.IsRunning) {
					ErrorHandler.HandleException("Wait for the current scan to finish or cancel it.", ErrorLevel.Low);
					return;
				}

				bool hasGrf = scanGrf && _grfHolder != null && _grfHolder.IsOpened;
				bool hasLocal = !String.IsNullOrWhiteSpace(newFolder) && Directory.Exists(newFolder);

				if (!hasGrf && !hasLocal) {
					ErrorHandler.HandleException("Select Scan GRF (with GRF open) or Scan folder.", ErrorLevel.Low);
					return;
				}

				if (hasLocal)
					_localSpriteFolder = newFolder;

				var locations = CustomAccessoryLubLocations.Resolve(_grfHolder);
				string accessoryIdPath = locations.EditAccessoryIdPath ?? locations.AccessoryIdGrfPath;
				string accnamePath = locations.EditAccnamePath ?? locations.AccnameGrfPath;

				if (String.IsNullOrWhiteSpace(accessoryIdPath) && String.IsNullOrWhiteSpace(accnamePath))
					ErrorHandler.HandleException(
						"accessoryid.lub / accname.lub not found. Scan will list sprites; use manual paths when saving.",
						ErrorLevel.Low);

				var input = new AccessoryScannerInput {
					Grf = _grfHolder != null && _grfHolder.IsOpened ? _grfHolder : null,
					LocalSpriteFolder = hasLocal ? _localSpriteFolder : null,
					AccessoryIdPath = accessoryIdPath,
					AccnamePath = accnamePath,
				};

				_setDataControlsEnabled(false);
				_textSummary.Text = "Scanning accessories...";
				_progressBar.Progress = 0;

				_cancellationSource?.Dispose();
				_cancellationSource = new CancellationTokenSource();

				_asyncOperation.SetAndRunOperation(
					prog => _lastResult = _scannerService.Scan(input, prog, _cancellationSource.Token),
					_onScanFinished);
			}
			catch (Exception err) {
				ErrorHandler.HandleException(err);
				_setDataControlsEnabled(_allEntries.Count > 0);
			}
		}

		private void _onScanFinished(object state) {
			try {
				this.Dispatch(delegate {
					_setDataControlsEnabled(_lastResult != null);

					if (_lastResult == null) {
						_textSummary.Text = "Scan finished without a result.";
						_progressBar.SetSpecialState(TkProgressBar.ProgressStatus.ErrorsDetected);
						return;
					}

					_reloadLuaTables();
					_allEntries.Clear();
					_allEntries.AddRange(CustomAccessoryManagerEntry.FromScanResult(
						_lastResult, _grfHolder, _lastResult.LocalSpriteFolder, _luaTables));

					_entriesView.Refresh();
					_updateLubHint();

					int shown = _allEntries.Count(_entryFilter);
					_textSummary.Text = String.Format(
						"{0} entries — {1} new, {2} existing, {3} with issues ({4} shown). GRF not saved.",
						_lastResult.Entries.Count,
						_lastResult.CountByStatus(AccessoryScanStatus.New),
						_lastResult.CountByStatus(AccessoryScanStatus.Existing),
						_lastResult.Entries.Count(en => en.Issues.Count > 0),
						shown);

					foreach (string message in _lastResult.Messages)
						_textSummary.Text += " " + message;

					_progressBar.SetSpecialState(TkProgressBar.ProgressStatus.Finished);
				});
			}
			catch (OperationCanceledException) {
				this.Dispatch(delegate {
					_setDataControlsEnabled(_allEntries.Count > 0);
					_textSummary.Text = "Scan cancelled.";
					_progressBar.SetSpecialState(TkProgressBar.ProgressStatus.ErrorsDetected);
				});
			}
			catch (Exception err) {
				ErrorHandler.HandleException(err);
				this.Dispatch(delegate {
					_setDataControlsEnabled(_allEntries.Count > 0);
					_progressBar.SetSpecialState(TkProgressBar.ProgressStatus.ErrorsDetected);
				});
			}
		}

		private void _asyncOperation_Cancelling(object state) {
			_cancellationSource?.Cancel();
			this.Dispatch(delegate {
				_textSummary.Text = "Cancelling scan...";
			});
		}

		private void _filters_Changed(object sender, SelectionChangedEventArgs e) {
			if (!IsLoaded || _asyncOperation.IsRunning)
				return;

			_entriesView?.Refresh();
			_updateSummaryAfterFilter();
		}

		private void _textSearch_TextChanged(object sender, TextChangedEventArgs e) {
			if (!IsLoaded || _asyncOperation.IsRunning)
				return;

			_entriesView?.Refresh();
			_updateSummaryAfterFilter();
		}

		private void _updateSummaryAfterFilter() {
			if (_lastResult == null || _asyncOperation.IsRunning)
				return;

			int shown = _allEntries.Count(_entryFilter);
			_textSummary.Text = String.Format(
				"{0} entries total — {1} shown (filters active). GRF not saved.",
				_lastResult.Entries.Count,
				shown);
		}

		private void _buttonSuggest_Click(object sender, RoutedEventArgs e) {
			var selected = _gridEntries.SelectedItems.Cast<CustomAccessoryManagerEntry>().ToList();
			if (selected.Count == 0)
				selected = _allEntries.Where(p => p.Selected).ToList();

			if (selected.Count == 0) {
				MessageBox.Show(this, "Selecione ao menos um item.", "Sugestões",
					MessageBoxButton.OK, MessageBoxImage.Information);
				return;
			}

			_reloadLuaTables();
			int nextId = _luaTables != null ? _luaTables.GetNextViewId() : 1;
			if (_tryGetInitialViewId(out int? startId) && startId.HasValue)
				nextId = startId.Value;

			foreach (var row in selected)
				row.ApplySuggestions(_luaTables, ref nextId);

			_gridEntries.Items.Refresh();
			_textSummary.Text = "Sugestões aplicadas a " + selected.Count + " item(ns).";
		}

		private void _buttonAutoViewIds_Click(object sender, RoutedEventArgs e) {
			if (!_tryGetInitialViewId(out int? startViewId, true))
				return;

			_reloadLuaTables();
			var nextId = startViewId ?? (_luaTables != null ? _luaTables.GetNextViewId() : 1);

			var targets = _allEntries
				.Where(p => p.Selected && (p.ScanStatus == AccessoryScanStatus.New
					|| p.ScanStatus == AccessoryScanStatus.MissingAccessoryId
					|| p.ScanStatus == AccessoryScanStatus.MissingAccName))
				.OrderBy(p => p.ConstantName)
				.ToList();

			foreach (var row in targets)
				row.ViewId = nextId++;

			_gridEntries.Items.Refresh();
			_textSummary.Text = "ViewIds sequenciais atribuídos a " + targets.Count + " item(ns).";
		}

		private List<CustomAccessoryEntry> _getSelectedForSave() {
			return _allEntries
				.Where(p => p.Selected)
				.Select(p => {
					p.RefreshWriteStatus(_luaTables);
					return p.ToCustomAccessoryEntry();
				})
				.ToList();
		}

		private bool _validateAndConfirmViewIds(IList<CustomAccessoryEntry> selected, CustomAccessoryManagerSaveValidation validation) {
			if (!validation.HasViewIdWarnings)
				return true;

			var message = "Avisos de viewId:" + Environment.NewLine + Environment.NewLine
				+ String.Join(Environment.NewLine, validation.ViewIdWarnings.Take(12))
				+ (validation.ViewIdWarnings.Count > 12
					? Environment.NewLine + "... e mais " + (validation.ViewIdWarnings.Count - 12) + "."
					: "")
				+ Environment.NewLine + Environment.NewLine
				+ "Nunca sobrescreva um viewId existente sem confirmar. Deseja continuar?";

			return MessageBox.Show(this, message, "Colisão de viewId",
				MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes;
		}

		private void _buttonPreviewDiff_Click(object sender, RoutedEventArgs e) {
			_tryPreviewOrSave(previewOnly: true);
		}

		private void _buttonSaveLua_Click(object sender, RoutedEventArgs e) {
			_tryPreviewOrSave(previewOnly: false);
		}

		private void _tryPreviewOrSave(bool previewOnly) {
			try {
				var managerRows = _allEntries.Where(p => p.Selected).ToList();
				if (managerRows.Count == 0) {
					MessageBox.Show(this, "Marque ao menos um item para gravar.", "Lua",
						MessageBoxButton.OK, MessageBoxImage.Information);
					return;
				}

				if (!CustomAccessoryManagerSaveService.TryEnsureLubLocations(_grfHolder, this, out var locations))
					return;

				_reloadLuaTables();
				foreach (var row in managerRows)
					row.RefreshWriteStatus(_luaTables);

				var selected = managerRows.Select(p => p.ToCustomAccessoryEntry()).ToList();
				var validation = CustomAccessoryManagerSaveValidator.Validate(managerRows, _luaTables);

				if (!validation.CanProceed) {
					MessageBox.Show(this,
						String.Join(Environment.NewLine, validation.Errors.Take(20)),
						"Validação",
						MessageBoxButton.OK,
						MessageBoxImage.Warning);
					return;
				}

				if (!_validateAndConfirmViewIds(selected, validation))
					return;

				var diff = CustomAccessoryManagerSaveService.BuildDiffPreview(locations, selected);

				if (previewOnly) {
					var previewDialog = new CustomAccessoryManagerDiffDialog(diff) { Owner = this };
					previewDialog.ShowDialog();
					return;
				}

				var diffDialog = new CustomAccessoryManagerDiffDialog(diff) { Owner = this };
				if (diffDialog.ShowDialog() != true)
					return;

				var mode = _getSaveMode();
				CustomAccessoryManagerSaveService.ApplySave(selected, locations, _grfHolder, mode);

				foreach (var row in managerRows) {
					row.RefreshWriteStatus(_luaTables);
					row.Selected = false;
				}

				_gridEntries.Items.Refresh();
				_updateLubHint();

				var backupNote = "Backups .bak criados junto aos arquivos de edição.";
				MessageBox.Show(this,
					locations.GetSuccessMessage() + Environment.NewLine + Environment.NewLine + backupNote
					+ Environment.NewLine + "O GRF não foi salvo automaticamente.",
					"Gravação concluída",
					MessageBoxButton.OK,
					MessageBoxImage.Information);
			}
			catch (Exception err) {
				ErrorHandler.HandleException(err);
			}
		}

		private IEnumerable<CustomAccessoryManagerEntry> _filteredEntries() {
			return _allEntries.Where(_entryFilter);
		}

		private void _buttonExportCsv_Click(object sender, RoutedEventArgs e) {
			_exportFile("Custom accessories (*.csv)|*.csv", "csv",
				() => CustomAccessoryManagerExport.ToCsv(_filteredEntries()));
		}

		private void _buttonExportJson_Click(object sender, RoutedEventArgs e) {
			_exportFile("Custom accessories (*.json)|*.json", "json",
				() => CustomAccessoryManagerExport.ToJson(_lastResult, _filteredEntries()));
		}

		private void _exportFile(string filter, string extension, Func<string> serialize) {
			try {
				var rows = _filteredEntries().ToList();
				if (rows.Count == 0) {
					ErrorHandler.HandleException("There are no rows to export.", ErrorLevel.Low);
					return;
				}

				string exportFolder = ActiveProjectProfile.GetExportFolderPath();
				string initialDirectory = !String.IsNullOrEmpty(exportFolder) && Directory.Exists(exportFolder)
					? exportFolder
					: null;

				string path = initialDirectory == null
					? PathRequest.SaveFileEditor("filter", filter, "fileName", "custom-accessories." + extension)
					: PathRequest.SaveFileEditor("filter", filter, "fileName", "custom-accessories." + extension, "initialDirectory", initialDirectory);

				if (path == null)
					return;

				if (!path.IsExtension("." + extension))
					path += "." + extension;

				CustomAccessoryManagerExport.WriteUtf8(path, serialize());
				OpeningService.FileOrFolder(path);
			}
			catch (Exception err) {
				ErrorHandler.HandleException(err);
			}
		}

		private void _buttonCopySelected_Click(object sender, RoutedEventArgs e) {
			try {
				var items = _gridEntries.SelectedItems.Cast<CustomAccessoryManagerEntry>().ToList();

				if (items.Count == 0) {
					ErrorHandler.HandleException("Select one or more rows first.", ErrorLevel.Low);
					return;
				}

				_copyToClipboard(items);
			}
			catch (Exception err) {
				ErrorHandler.HandleException(err);
			}
		}

		private void _buttonCopyAll_Click(object sender, RoutedEventArgs e) {
			try {
				var items = _filteredEntries().ToList();
				if (items.Count == 0) {
					ErrorHandler.HandleException("There are no rows to copy.", ErrorLevel.Low);
					return;
				}

				_copyToClipboard(items);
			}
			catch (Exception err) {
				ErrorHandler.HandleException(err);
			}
		}

		private static void _copyToClipboard(IEnumerable<CustomAccessoryManagerEntry> views) {
			const string header = "SpritePath\tActPath\tConstantName\tViewId\tDisplayName\tStatus\tIssues";
			string body = String.Join(Environment.NewLine, views.Select(v => v.ToCopyLine()));
			Clipboard.SetDataObject(header + Environment.NewLine + body);
		}

		private void _buttonClose_Click(object sender, RoutedEventArgs e) {
			if (_asyncOperation.IsRunning) {
				_asyncOperation.Cancel();
				return;
			}

			Close();
		}

		private void _setDataControlsEnabled(bool enabled) {
			_buttonExportCsv.IsEnabled = enabled;
			_buttonExportJson.IsEnabled = enabled;
			_buttonExportImportPreview.IsEnabled = enabled;
			_buttonCopySelected.IsEnabled = enabled;
			_buttonCopyAll.IsEnabled = enabled;
			_buttonSuggest.IsEnabled = enabled;
			_buttonAutoViewIds.IsEnabled = enabled;
			_buttonPreviewDiff.IsEnabled = enabled;
			_buttonSaveLua.IsEnabled = enabled;
			_comboStatus.IsEnabled = enabled;
			_textSearch.IsEnabled = enabled;
			_textInitialViewId.IsEnabled = enabled;
			_buttonScanGrf.IsEnabled = !_asyncOperation.IsRunning;
			_buttonScanFolder.IsEnabled = !_asyncOperation.IsRunning;
			_buttonBatchImport.IsEnabled = !_asyncOperation.IsRunning;
		}
	}
}
