using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using ErrorManager;
using GRF.Core;
using GRFEditor.ApplicationConfiguration;
using GRFEditor.Core.ProjectProfiles;
using AsyncOperation = GrfToWpfBridge.Application.AsyncOperation;
using GrfToWpfBridge.Application;
using TokeiLibrary;
using Utilities.Extension;
using TokeiLibrary.WPF;
using TokeiLibrary.WPF.Styles;
using TokeiLibrary.WPF.Styles.ListView;
using Utilities.Services;

namespace GRFEditor.Core.GrfCompare {
	public partial class GrfCompareDialog : TkWindow, IDisposable {
		private const string FilterAll = "(All)";

		private readonly AsyncOperation _asyncOperation;
		private readonly GrfHolder _grfOld = new GrfHolder();
		private readonly GrfHolder _grfNew = new GrfHolder();
		private readonly GrfCompareService _compareService = new GrfCompareService();
		private readonly GrfCompareFileExportService _fileExportService = new GrfCompareFileExportService();
		private readonly FileHashService _hashService = new FileHashService();
		private readonly List<GrfCompareView> _allViews = new List<GrfCompareView>();
		private List<GrfCompareView> _filteredViews = new List<GrfCompareView>();

		private GrfCompareResult _lastResult;
		private GrfCompareFileExportResult _lastExportResult;
		private CancellationTokenSource _cancellationSource;
		private string _pathOld;
		private string _pathNew;

		public GrfCompareDialog()
			: base("Compare GRFs", "diff.png", SizeToContent.Manual, ResizeMode.CanResize) {
			InitializeComponent();

			_asyncOperation = new AsyncOperation(_progressBar);
			_asyncOperation.Cancelling += _asyncOperation_Cancelling;
			Owner = WpfUtilities.TopWindow;

			_initFilters();
			_initListView();
			_progressBar.SetSpecialState(TkProgressBar.ProgressStatus.Finished);
			_applyActiveProfileDefaults();
		}

		public void Dispose() {
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		protected virtual void Dispose(bool disposing) {
			if (!disposing)
				return;

			_cancellationSource?.Dispose();
			_cancellationSource = null;
			_grfOld?.Dispose();
			_grfNew?.Dispose();
		}

		protected override void OnClosing(CancelEventArgs e) {
			_asyncOperation.Cancel();
			_cancellationSource?.Cancel();
			base.OnClosing(e);
		}

		private void _initFilters() {
			_comboStatus.Items.Add(FilterAll);

			foreach (GrfCompareStatus status in Enum.GetValues(typeof(GrfCompareStatus)))
				_comboStatus.Items.Add(status.ToString());

			_comboStatus.SelectedIndex = 0;
		}

		private void _initListView() {
			ListViewDataTemplateHelper.GenerateListViewTemplateNew(_listCompare, new ListViewDataTemplateHelper.GeneralColumnInfo[] {
				new ListViewDataTemplateHelper.RangeColumnInfo {
					Header = "Relative path",
					DisplayExpression = "RelativePath",
					SearchGetAccessor = "RelativePath",
					FixedWidth = 260,
					TextAlignment = TextAlignment.Left,
					ToolTipBinding = "RelativePath",
					MinWidth = 140,
				},
				new ListViewDataTemplateHelper.GeneralColumnInfo {
					Header = "Status",
					DisplayExpression = "Status",
					FixedWidth = 160,
					TextAlignment = TextAlignment.Left,
					ToolTipBinding = "Status",
				},
				new ListViewDataTemplateHelper.GeneralColumnInfo {
					Header = "Size A",
					DisplayExpression = "SizeA",
					FixedWidth = 80,
					TextAlignment = TextAlignment.Right,
					ToolTipBinding = "SizeA",
				},
				new ListViewDataTemplateHelper.GeneralColumnInfo {
					Header = "Size B",
					DisplayExpression = "SizeB",
					FixedWidth = 80,
					TextAlignment = TextAlignment.Right,
					ToolTipBinding = "SizeB",
				},
				new ListViewDataTemplateHelper.GeneralColumnInfo {
					Header = "Hash A",
					DisplayExpression = "HashA",
					FixedWidth = 220,
					TextAlignment = TextAlignment.Left,
					ToolTipBinding = "HashA",
				},
				new ListViewDataTemplateHelper.GeneralColumnInfo {
					Header = "Hash B",
					DisplayExpression = "HashB",
					IsFill = true,
					TextAlignment = TextAlignment.Left,
					ToolTipBinding = "HashB",
				},
			}, new DefaultListViewComparer<GrfCompareView>(), new string[] {
				"Same", "{StaticResource TextForeground}",
				"Added", "{DynamicResource CellBrushAdded}",
				"Removed", "{DynamicResource CellBrushRemoved}",
				"Modified", "{DynamicResource CellBrushEncrypted}",
				"SameContentDifferentPath", "{DynamicResource CellBrushCustomCompression}",
				"SamePathDifferentContent", "{DynamicResource CellBrushEncrypted}",
				"Default", "{StaticResource TextForeground}",
			});
		}

		private void _applyActiveProfileDefaults() {
			if (!ActiveProjectProfile.HasActive)
				return;

			ActiveProjectProfile.ConfirmContinueWithInvalidPaths(
				this,
				"Compare GRFs",
				ActiveProjectProfile.GetPathWarningsForTool(p => p.MainGrfPath));

			string mainGrf = ActiveProjectProfile.GetMainGrfPath();

			if (!String.IsNullOrEmpty(mainGrf) && File.Exists(mainGrf)) {
				if (String.IsNullOrWhiteSpace(_pbGrfNew.Text))
					_pbGrfNew.Text = mainGrf;
			}
		}

		private void _grfPath_Changed(object sender, EventArgs e) {
			if (File.Exists(_pbGrfOld.Text)) {
				GrfEditorConfiguration.AppLastPath = _pbGrfOld.Text;
				_pathOld = _pbGrfOld.Text;
			}

			if (File.Exists(_pbGrfNew.Text)) {
				GrfEditorConfiguration.AppLastPath = _pbGrfNew.Text;
				_pathNew = _pbGrfNew.Text;
			}
		}

		private void _buttonCompare_Click(object sender, RoutedEventArgs e) {
			try {
				if (_asyncOperation.IsRunning) {
					ErrorHandler.HandleException("A compare operation is already running. Cancel it first.", ErrorLevel.Low);
					return;
				}

				if (!File.Exists(_pathOld) || !File.Exists(_pathNew)) {
					ErrorHandler.HandleException("Select valid GRF A (old) and GRF B (new) files.", ErrorLevel.Low);
					return;
				}

				_grfOld.Close();
				_grfOld.Open(_pathOld);

				_grfNew.Close();
				_grfNew.Open(_pathNew);

				if (_grfOld.Header.FoundErrors) {
					ErrorHandler.HandleException("GRF A (old) contains errors and cannot be compared.", ErrorLevel.Low);
					return;
				}

				if (_grfNew.Header.FoundErrors) {
					ErrorHandler.HandleException("GRF B (new) contains errors and cannot be compared.", ErrorLevel.Low);
					return;
				}

				_hashService.Clear();
				_setExportControlsEnabled(false);
				_setCompareControlsEnabled(false);
				_allViews.Clear();
				_filteredViews.Clear();
				_listCompare.ItemsSource = null;
				_textSummary.Text = "Comparing GRFs (read-only)...";
				_progressBar.Progress = 0;

				_cancellationSource?.Dispose();
				_cancellationSource = new CancellationTokenSource();

				_asyncOperation.SetAndRunOperation(
					prog => _lastResult = _compareService.Compare(
						_grfOld,
						_grfNew,
						_hashService,
						prog,
						_cancellationSource.Token),
					_onCompareFinished);
			}
			catch (Exception err) {
				ErrorHandler.HandleException(err);
				_setCompareControlsEnabled(true);
			}
		}

		private void _onCompareFinished(object state) {
			try {
				this.Dispatch(delegate {
					_setCompareControlsEnabled(true);

					if (_lastResult == null) {
						_textSummary.Text = "Compare finished without a result.";
						_progressBar.SetSpecialState(TkProgressBar.ProgressStatus.ErrorsDetected);
						return;
					}

					_allViews.Clear();
					_allViews.AddRange(_lastResult.Entries.Select(e => new GrfCompareView(e)));
					_applyFilters();

					_textSummary.Text = String.Format(
						"{0} entries — {1} same, {2} added, {3} removed, {4} modified, {5} moved, {6} same path/different content ({7} shown).",
						_lastResult.Entries.Count,
						_lastResult.SameCount,
						_lastResult.AddedCount,
						_lastResult.RemovedCount,
						_lastResult.ModifiedCount,
						_lastResult.SameContentDifferentPathCount,
						_lastResult.SamePathDifferentContentCount,
						_filteredViews.Count);

					_progressBar.SetSpecialState(TkProgressBar.ProgressStatus.Finished);
					_setExportControlsEnabled(true);
				});
			}
			catch (OperationCanceledException) {
				this.Dispatch(delegate {
					_setCompareControlsEnabled(true);
					_textSummary.Text = "Compare cancelled.";
					_progressBar.SetSpecialState(TkProgressBar.ProgressStatus.ErrorsDetected);
				});
			}
			catch (Exception err) {
				ErrorHandler.HandleException(err);
				this.Dispatch(delegate {
					_setCompareControlsEnabled(true);
					_progressBar.SetSpecialState(TkProgressBar.ProgressStatus.ErrorsDetected);
				});
			}
		}

		private void _asyncOperation_Cancelling(object state) {
			_cancellationSource?.Cancel();
			this.Dispatch(delegate {
				_textSummary.Text = "Cancelling compare...";
			});
		}

		private void _filters_Changed(object sender, SelectionChangedEventArgs e) {
			if (!IsLoaded || _asyncOperation.IsRunning)
				return;

			_applyFilters();
		}

		private void _textSearch_TextChanged(object sender, TextChangedEventArgs e) {
			if (!IsLoaded || _asyncOperation.IsRunning)
				return;

			_applyFilters();
		}

		private void _applyFilters() {
			string statusFilter = _comboStatus.SelectedItem as string;
			string search = _textSearch.Text ?? "";
			IEnumerable<GrfCompareView> query = _allViews;

			if (!String.IsNullOrEmpty(statusFilter) && statusFilter != FilterAll)
				query = query.Where(v => v.Status == statusFilter);

			if (!String.IsNullOrWhiteSpace(search))
				query = query.Where(v =>
					(v.RelativePath != null && v.RelativePath.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0)
					|| (v.PathInA != null && v.PathInA.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0)
					|| (v.PathInB != null && v.PathInB.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0));

			_filteredViews = query.ToList();
			_listCompare.ItemsSource = _filteredViews;

			if (_lastResult != null && !_asyncOperation.IsRunning) {
				_textSummary.Text = String.Format(
					"{0} entries total — {1} shown (filters active).",
					_lastResult.Entries.Count,
					_filteredViews.Count);
			}
		}

		private void _buttonExportCsv_Click(object sender, RoutedEventArgs e) {
			_exportFile("GRF compare (*.csv)|*.csv", "csv", () => GrfCompareExport.ToCsv(_filteredViews));
		}

		private void _buttonExportJson_Click(object sender, RoutedEventArgs e) {
			_exportFile("GRF compare (*.json)|*.json", "json", () => GrfCompareExport.ToJson(_lastResult, _filteredViews));
		}

		private void _buttonExportChangedTxt_Click(object sender, RoutedEventArgs e) {
			_exportFile("Changed files list (*.txt)|*.txt", "txt",
				() => GrfCompareExport.ToChangedFilesTxt(_lastResult, _filteredViews.Where(v => v.StatusValue != GrfCompareStatus.Same)));
		}

		private void _exportFile(string filter, string extension, Func<string> serialize) {
			try {
				if (_filteredViews.Count == 0) {
					ErrorHandler.HandleException("There are no rows to export.", ErrorLevel.Low);
					return;
				}

				if (extension == "txt") {
					var changed = _filteredViews.Where(v => v.StatusValue != GrfCompareStatus.Same).ToList();

					if (changed.Count == 0) {
						ErrorHandler.HandleException("No changed entries in the current filter.", ErrorLevel.Low);
						return;
					}
				}

				string exportFolder = ActiveProjectProfile.GetExportFolderPath();
				string initialDirectory = !String.IsNullOrEmpty(exportFolder) && Directory.Exists(exportFolder)
					? exportFolder
					: null;

				string path = initialDirectory == null
					? PathRequest.SaveFileEditor("filter", filter, "fileName", "grf-compare." + extension)
					: PathRequest.SaveFileEditor("filter", filter, "fileName", "grf-compare." + extension, "initialDirectory", initialDirectory);

				if (path == null)
					return;

				if (!path.IsExtension("." + extension))
					path += "." + extension;

				GrfCompareExport.WriteUtf8(path, serialize());
				OpeningService.FileOrFolder(path);
			}
			catch (Exception err) {
				ErrorHandler.HandleException(err);
			}
		}

		private void _buttonClose_Click(object sender, RoutedEventArgs e) {
			if (_asyncOperation.IsRunning) {
				_asyncOperation.Cancel();
				return;
			}

			Close();
		}

		private void _buttonExportAdded_Click(object sender, RoutedEventArgs e) {
			_startFileExport(GrfCompareFileExportMode.AddedFiles);
		}

		private void _buttonExportModified_Click(object sender, RoutedEventArgs e) {
			_startFileExport(GrfCompareFileExportMode.ModifiedFiles);
		}

		private void _buttonExportAddedModified_Click(object sender, RoutedEventArgs e) {
			_startFileExport(GrfCompareFileExportMode.AddedAndModifiedFiles);
		}

		private void _buttonExportRemovedList_Click(object sender, RoutedEventArgs e) {
			_startFileExport(GrfCompareFileExportMode.RemovedFilesList);
		}

		private void _buttonExportFullReport_Click(object sender, RoutedEventArgs e) {
			_startFileExport(GrfCompareFileExportMode.FullDifferenceReport);
		}

		private void _startFileExport(GrfCompareFileExportMode mode) {
			try {
				if (_asyncOperation.IsRunning) {
					ErrorHandler.HandleException("Wait for the current operation to finish or cancel it first.", ErrorLevel.Low);
					return;
				}

				if (_lastResult == null || _allViews.Count == 0) {
					ErrorHandler.HandleException("Run compare first.", ErrorLevel.Low);
					return;
				}

				if (!_grfNew.IsOpened || !_grfOld.IsOpened) {
					ErrorHandler.HandleException("GRF files are not loaded. Run compare again.", ErrorLevel.Low);
					return;
				}

				int jobCount = _countExportJobs(mode);

				if (jobCount == 0 && mode != GrfCompareFileExportMode.RemovedFilesList) {
					ErrorHandler.HandleException("There are no files to export for this action.", ErrorLevel.Low);
					return;
				}

				string exportFolder = ActiveProjectProfile.GetExportFolderPath();
				string initialDirectory = !String.IsNullOrEmpty(exportFolder) && Directory.Exists(exportFolder)
					? exportFolder
					: GrfEditorConfiguration.AppLastPath;

				string folder = PathRequest.FolderEditor("initialDirectory", initialDirectory);

				if (String.IsNullOrEmpty(folder))
					return;

				if (!_confirmOverwrite(mode, folder, out bool allowOverwrite))
					return;

				_setExportControlsEnabled(false);
				_setCompareControlsEnabled(false);
				_textSummary.Text = "Exporting files...";
				_progressBar.Progress = 0;

				_cancellationSource?.Dispose();
				_cancellationSource = new CancellationTokenSource();
				var exportMode = mode;

				_asyncOperation.SetAndRunOperation(
					prog => _lastExportResult = _fileExportService.Export(
						_grfOld,
						_grfNew,
						_lastResult,
						_allViews,
						exportMode,
						folder,
						allowOverwrite,
						prog,
						_cancellationSource.Token),
					_onFileExportFinished);
			}
			catch (Exception err) {
				ErrorHandler.HandleException(err);
				_setExportControlsEnabled(_lastResult != null);
				_setCompareControlsEnabled(true);
			}
		}

		private void _onFileExportFinished(object state) {
			try {
				var exportResult = _lastExportResult;

				this.Dispatch(delegate {
					_setCompareControlsEnabled(true);
					_setExportControlsEnabled(_lastResult != null);
					_progressBar.SetSpecialState(TkProgressBar.ProgressStatus.Finished);

					if (exportResult == null) {
						_textSummary.Text = "Export finished without a result.";
						return;
					}

					_showExportSummary(exportResult);
					_textSummary.Text = String.Format(
						"Export finished — {0} file(s) written to {1}.",
						exportResult.ExportedCount,
						exportResult.DestinationPath);
				});
			}
			catch (OperationCanceledException) {
				this.Dispatch(delegate {
					_setCompareControlsEnabled(true);
					_setExportControlsEnabled(_lastResult != null);
					_textSummary.Text = "Export cancelled.";
					_progressBar.SetSpecialState(TkProgressBar.ProgressStatus.ErrorsDetected);
				});
			}
			catch (Exception err) {
				ErrorHandler.HandleException(err);
				this.Dispatch(delegate {
					_setCompareControlsEnabled(true);
					_setExportControlsEnabled(_lastResult != null);
					_progressBar.SetSpecialState(TkProgressBar.ProgressStatus.ErrorsDetected);
				});
			}
		}

		private int _countExportJobs(GrfCompareFileExportMode mode) {
			switch (mode) {
				case GrfCompareFileExportMode.AddedFiles:
					return _allViews.Count(v => v.StatusValue == GrfCompareStatus.Added);
				case GrfCompareFileExportMode.ModifiedFiles:
					return _allViews.Count(v => v.StatusValue == GrfCompareStatus.Modified
						|| v.StatusValue == GrfCompareStatus.SamePathDifferentContent);
				case GrfCompareFileExportMode.AddedAndModifiedFiles:
					return _allViews.Count(v => v.StatusValue == GrfCompareStatus.Added
						|| v.StatusValue == GrfCompareStatus.Modified
						|| v.StatusValue == GrfCompareStatus.SamePathDifferentContent
						|| v.StatusValue == GrfCompareStatus.SameContentDifferentPath);
				case GrfCompareFileExportMode.RemovedFilesList:
					return _allViews.Any(v => v.StatusValue == GrfCompareStatus.Removed) ? 1 : 0;
				case GrfCompareFileExportMode.FullDifferenceReport:
					return _allViews.Count(v => v.StatusValue != GrfCompareStatus.Same
						&& v.StatusValue != GrfCompareStatus.Removed);
				default:
					return 0;
			}
		}

		private bool _confirmOverwrite(GrfCompareFileExportMode mode, string folder, out bool allowOverwrite) {
			allowOverwrite = true;

			var planned = GrfCompareFileExportService.GetPlannedOutputPaths(mode, _allViews, folder)
				.Where(File.Exists)
				.Distinct(StringComparer.OrdinalIgnoreCase)
				.ToList();

			if (planned.Count == 0)
				return true;

			MessageBoxResult answer = WindowProvider.ShowDialog(
				planned.Count + " file(s) already exist in the destination.\n\nYes = overwrite existing files\nNo = skip existing files\nCancel = abort export",
				"Overwrite files?",
				MessageBoxButton.YesNoCancel);

			if (answer == MessageBoxResult.Cancel)
				return false;

			allowOverwrite = answer == MessageBoxResult.Yes;
			return true;
		}

		private void _showExportSummary(GrfCompareFileExportResult result) {
			var sb = new System.Text.StringBuilder();
			sb.AppendLine("Export: " + _exportModeTitle(result.Mode));
			sb.AppendLine("Destination: " + result.DestinationPath);
			sb.AppendLine("Files exported: " + result.ExportedCount);

			if (result.WrittenReportFiles.Count > 0)
				sb.AppendLine("Reports written: " + result.WrittenReportFiles.Count);

			sb.AppendLine("Ignored (skipped): " + result.IgnoredCount);
			sb.AppendLine("Errors: " + result.ErrorCount);

			if (result.WrittenReportFiles.Count > 0) {
				sb.AppendLine();
				sb.AppendLine("Reports:");

				foreach (string path in result.WrittenReportFiles.Take(8))
					sb.AppendLine("  " + path);
			}

			if (result.IgnoredFiles.Count > 0) {
				sb.AppendLine();
				sb.AppendLine("Ignored files:");

				foreach (string path in result.IgnoredFiles.Take(12))
					sb.AppendLine("  " + path);

				if (result.IgnoredFiles.Count > 12)
					sb.AppendLine("  ... and " + (result.IgnoredFiles.Count - 12) + " more");
			}

			if (result.Errors.Count > 0) {
				sb.AppendLine();
				sb.AppendLine("Errors:");

				foreach (string error in result.Errors.Take(12))
					sb.AppendLine("  " + error);

				if (result.Errors.Count > 12)
					sb.AppendLine("  ... and " + (result.Errors.Count - 12) + " more");
			}

			WindowProvider.ShowDialog(sb.ToString().TrimEnd(), "Export summary", MessageBoxButton.OK);

			if (result.ErrorCount == 0)
				OpeningService.FileOrFolder(result.DestinationPath);
		}

		private static string _exportModeTitle(GrfCompareFileExportMode mode) {
			switch (mode) {
				case GrfCompareFileExportMode.AddedFiles: return "Added files";
				case GrfCompareFileExportMode.ModifiedFiles: return "Modified files";
				case GrfCompareFileExportMode.AddedAndModifiedFiles: return "Added + modified files";
				case GrfCompareFileExportMode.RemovedFilesList: return "Removed files list";
				case GrfCompareFileExportMode.FullDifferenceReport: return "Full difference report";
				default: return mode.ToString();
			}
		}

		private void _setExportControlsEnabled(bool enabled) {
			_buttonExportCsv.IsEnabled = enabled;
			_buttonExportJson.IsEnabled = enabled;
			_buttonExportChangedTxt.IsEnabled = enabled;
			_buttonExportAdded.IsEnabled = enabled;
			_buttonExportModified.IsEnabled = enabled;
			_buttonExportAddedModified.IsEnabled = enabled;
			_buttonExportRemovedList.IsEnabled = enabled;
			_buttonExportFullReport.IsEnabled = enabled;
			_comboStatus.IsEnabled = enabled;
			_textSearch.IsEnabled = enabled;
		}

		private void _setCompareControlsEnabled(bool enabled) {
			_buttonCompare.IsEnabled = enabled;
			_pbGrfOld.IsEnabled = enabled;
			_pbGrfNew.IsEnabled = enabled;
		}
	}
}
