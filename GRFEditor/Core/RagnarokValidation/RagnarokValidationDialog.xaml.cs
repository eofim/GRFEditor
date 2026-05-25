using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.IO;
using System.Windows.Data;
using ErrorManager;
using GRF.Core;
using GRF.Threading;
using GRFEditor.ApplicationConfiguration;
using GRFEditor.Core.ProjectProfiles;
using AsyncOperation = GrfToWpfBridge.Application.AsyncOperation;
using TokeiLibrary;
using Utilities.Extension;
using TokeiLibrary.WPF;
using TokeiLibrary.WPF.Styles;
using TokeiLibrary.WPF.Styles.ListView;

namespace GRFEditor.Core.RagnarokValidation {
	public partial class RagnarokValidationDialog : TkWindow {
		private const string FilterAll = "(All)";

		private readonly AsyncOperation _asyncOperation;
		private readonly GrfHolder _grfHolder;
		private readonly RagnarokValidationService _validationService = new RagnarokValidationService();
		private readonly RagnarokValidationAutoFixService _autoFixService = new RagnarokValidationAutoFixService();
		private readonly List<RagnarokValidationView> _allViews = new List<RagnarokValidationView>();
		private List<RagnarokValidationView> _filteredViews = new List<RagnarokValidationView>();
		private RagnarokValidationResult _lastResult;
		private bool _validationStarted;
		private int _ignoredByProfileCount;

		public RagnarokValidationDialog(GrfHolder grfHolder)
			: base("Ragnarok client validation", "validity.ico", SizeToContent.Manual, ResizeMode.CanResize) {
			_grfHolder = grfHolder;
			InitializeComponent();

			_asyncOperation = new AsyncOperation(_progressBar);
			Owner = WpfUtilities.TopWindow;

			_initFilters();
			_initListView();
			_setReportControlsEnabled(false);
			_progressBar.SetSpecialState(TkProgressBar.ProgressStatus.Finished);
		}

		private void _initFilters() {
			_comboSeverity.Items.Add(FilterAll);
			foreach (RagnarokValidationSeverity severity in Enum.GetValues(typeof(RagnarokValidationSeverity)))
				_comboSeverity.Items.Add(severity.ToString());
			_comboSeverity.SelectedIndex = 0;

			_comboCategory.Items.Add(FilterAll);
			foreach (RagnarokValidationCategory category in Enum.GetValues(typeof(RagnarokValidationCategory)))
				_comboCategory.Items.Add(category.ToString());
			_comboCategory.SelectedIndex = 0;
		}

		private void _initListView() {
			ListViewDataTemplateHelper.GenerateListViewTemplateNew(_listViewIssues, new ListViewDataTemplateHelper.GeneralColumnInfo[] {
				new ListViewDataTemplateHelper.ImageColumnInfo { Header = "", DisplayExpression = "DataImage", SearchGetAccessor = "Severity", FixedWidth = 22, MaxHeight = 16 },
				new ListViewDataTemplateHelper.GeneralColumnInfo { Header = "Severity", DisplayExpression = "Severity", FixedWidth = 72, TextAlignment = TextAlignment.Center, ToolTipBinding = "Severity" },
				new ListViewDataTemplateHelper.GeneralColumnInfo { Header = "Category", DisplayExpression = "Category", FixedWidth = 120, TextAlignment = TextAlignment.Left, ToolTipBinding = "Category" },
				new ListViewDataTemplateHelper.RangeColumnInfo { Header = "Relative path", DisplayExpression = "RelativePath", SearchGetAccessor = "RelativePath", FixedWidth = 220, TextAlignment = TextAlignment.Left, ToolTipBinding = "ToolTipRelativePath", MinWidth = 120 },
				new ListViewDataTemplateHelper.GeneralColumnInfo { Header = "Message", DisplayExpression = "Message", IsFill = true, TextAlignment = TextAlignment.Left, ToolTipBinding = "ToolTipMessage" },
				new ListViewDataTemplateHelper.GeneralColumnInfo { Header = "Suggested fix", DisplayExpression = "SuggestedFix", FixedWidth = 180, TextAlignment = TextAlignment.Left, ToolTipBinding = "ToolTipSuggestedFix" },
				new ListViewDataTemplateHelper.GeneralColumnInfo { Header = "Auto-fix", DisplayExpression = "CanAutoFix", FixedWidth = 56, TextAlignment = TextAlignment.Center, ToolTipBinding = "CanAutoFix" },
			}, new DefaultListViewComparer<RagnarokValidationView>(), new string[] {
				"Critical", "{DynamicResource CellBrushRemoved}",
				"Error", "{DynamicResource CellBrushEncrypted}",
				"Warning", "{DynamicResource CellBrushCustomCompression}",
				"Info", "{StaticResource TextForeground}",
				"Default", "{StaticResource TextForeground}",
			});

			_addFixCheckboxColumn();
		}

		private void _addFixCheckboxColumn() {
			var gridView = _listViewIssues.View as GridView;
			if (gridView == null)
				return;

			var checkTemplate = new DataTemplate();
			var checkFactory = new FrameworkElementFactory(typeof(CheckBox));
			checkFactory.SetValue(CheckBox.HorizontalAlignmentProperty, HorizontalAlignment.Center);
			checkFactory.SetValue(CheckBox.VerticalAlignmentProperty, VerticalAlignment.Center);
			checkFactory.SetBinding(CheckBox.IsCheckedProperty, new Binding(nameof(RagnarokValidationView.IsSelectedForFix)) {
				Mode = BindingMode.TwoWay,
				UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
			});
			checkFactory.SetBinding(CheckBox.IsEnabledProperty, new Binding(nameof(RagnarokValidationView.CanSelectForFix)));
			checkTemplate.VisualTree = checkFactory;

			gridView.Columns.Insert(0, new GridViewColumn {
				Header = "Fix",
				Width = 36,
				CellTemplate = checkTemplate,
			});
		}

		private void _window_Loaded(object sender, RoutedEventArgs e) {
			if (_validationStarted)
				return;

			_validationStarted = true;

			if (ActiveProjectProfile.HasActive) {
				string profileLine = "Active profile: " + ActiveProjectProfile.DisplayName;
				int ignoredRuleCount = RagnarokValidationIgnoredRules.GetActiveIgnoredRules().Count;
				if (ignoredRuleCount > 0)
					profileLine += " (" + ignoredRuleCount + " ignored rule(s))";

				_textSummary.Text = profileLine;

				ActiveProjectProfile.ConfirmContinueWithInvalidPaths(
					this,
					"Ragnarok client validation",
					ActiveProjectProfile.GetPathWarningsForTool(
						p => p.MainGrfPath,
						p => p.AccessoryIdPath,
						p => p.AccNamePath,
						p => p.ItemInfoPath));
			}

			_startValidation();
		}

		protected override void OnClosing(CancelEventArgs e) {
			_asyncOperation.Cancel();
			base.OnClosing(e);
		}

		private void _startValidation() {
			if (_asyncOperation.IsRunning)
				return;

			if (_grfHolder == null || !_grfHolder.IsOpened) {
				_textSummary.Text = "No GRF is opened.";
				_progressBar.SetSpecialState(TkProgressBar.ProgressStatus.Finished);
				return;
			}

			_setReportControlsEnabled(false);
			_textSummary.Text = "Validating GRF for Ragnarok client issues...";
			_allViews.Clear();
			_filteredViews.Clear();
			_listViewIssues.ItemsSource = null;

			_asyncOperation.SetAndRunOperation(
				prog => _lastResult = _validationService.Validate(_grfHolder, prog),
				_onValidationFinished);
		}

		private void _onValidationFinished(object state) {
			try {
				var result = _lastResult ?? new RagnarokValidationResult();

				this.Dispatch(delegate {
					_allViews.Clear();
					_allViews.AddRange(result.Issues.Select(p => new RagnarokValidationView(p)));
					_applyFilters();

					if (result.WasCancelled) {
						_textSummary.Text = String.Format(
							"Validation cancelled. {0} issue(s) collected before cancel ({1} shown).",
							result.TotalCount,
							_filteredViews.Count);
						_progressBar.SetSpecialState(TkProgressBar.ProgressStatus.ErrorsDetected);
					}
					else {
						string summary = String.Format(
							"{0} issue(s) found — {1} critical, {2} error, {3} warning, {4} info ({5} shown after filters).",
							result.TotalCount,
							result.CountBySeverity(RagnarokValidationSeverity.Critical),
							result.CountBySeverity(RagnarokValidationSeverity.Error),
							result.CountBySeverity(RagnarokValidationSeverity.Warning),
							result.CountBySeverity(RagnarokValidationSeverity.Info),
							_filteredViews.Count);

						if (_ignoredByProfileCount > 0)
							summary += " " + _ignoredByProfileCount + " hidden by active profile ignored rules.";

						if (ActiveProjectProfile.HasActive)
							summary = "Profile: " + ActiveProjectProfile.DisplayName + ". " + summary;

						_textSummary.Text = summary;
						_progressBar.SetSpecialState(result.HasErrors
							? TkProgressBar.ProgressStatus.ErrorsDetected
							: TkProgressBar.ProgressStatus.Finished);
					}

					_setReportControlsEnabled(true);
				});
			}
			catch (Exception err) {
				ErrorHandler.HandleException(err);
			}
		}

		private void _setReportControlsEnabled(bool enabled) {
			_buttonSelectFixable.IsEnabled = enabled;
			_buttonClearFixSelection.IsEnabled = enabled;
			_buttonApplyFixes.IsEnabled = enabled;
			_buttonCopySelected.IsEnabled = enabled;
			_buttonCopyAll.IsEnabled = enabled;
			_buttonExportCsv.IsEnabled = enabled;
			_buttonExportJson.IsEnabled = enabled;
			_comboSeverity.IsEnabled = enabled;
			_comboCategory.IsEnabled = enabled;
			_textSearch.IsEnabled = enabled;
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
			string severityFilter = _comboSeverity.SelectedItem as string;
			string categoryFilter = _comboCategory.SelectedItem as string;
			string search = _textSearch.Text ?? "";

			var afterProfileIgnore = _allViews.Where(v => !RagnarokValidationIgnoredRules.IsIgnored(v.Issue)).ToList();
			_ignoredByProfileCount = _allViews.Count - afterProfileIgnore.Count;
			IEnumerable<RagnarokValidationView> query = afterProfileIgnore;

			if (!String.IsNullOrEmpty(severityFilter) && severityFilter != FilterAll)
				query = query.Where(p => p.Severity == severityFilter);

			if (!String.IsNullOrEmpty(categoryFilter) && categoryFilter != FilterAll)
				query = query.Where(p => p.Category == categoryFilter);

			if (!String.IsNullOrWhiteSpace(search)) {
				query = query.Where(p =>
					(p.RelativePath != null && p.RelativePath.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0) ||
					(p.Message != null && p.Message.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0) ||
					(p.SuggestedFix != null && p.SuggestedFix.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0));
			}

			_filteredViews = query.ToList();
			_listViewIssues.ItemsSource = _filteredViews;

			if (_lastResult != null && !_asyncOperation.IsRunning) {
				_textSummary.Text = String.Format(
					"{0} issue(s) total — {1} shown (filters active).",
					_lastResult.TotalCount,
					_filteredViews.Count);
			}
		}

		private void _buttonSelectFixable_Click(object sender, RoutedEventArgs e) {
			foreach (var view in _allViews) {
				if (!view.CanSelectForFix)
					continue;

				if (view.Issue.FixKind == RagnarokValidationFixKind.RemoveEmptyFile)
					continue;

				view.IsSelectedForFix = true;
			}
		}

		private void _buttonClearFixSelection_Click(object sender, RoutedEventArgs e) {
			foreach (var view in _allViews)
				view.IsSelectedForFix = false;
		}

		private void _buttonApplyFixes_Click(object sender, RoutedEventArgs e) {
			try {
				if (_asyncOperation.IsRunning) {
					ErrorHandler.HandleException("Wait for validation to finish first.", ErrorLevel.Low);
					return;
				}

				if (_grfHolder.IsBusy) {
					ErrorHandler.HandleException("The GRF is busy. Wait for the current operation to finish.", ErrorLevel.Low);
					return;
				}

				var selected = _allViews.Where(p => p.IsSelectedForFix && p.CanSelectForFix).ToList();
				if (selected.Count == 0) {
					ErrorHandler.HandleException("Select at least one fixable issue using the Fix column.", ErrorLevel.Low);
					return;
				}

				string summary = RagnarokValidationAutoFixService.BuildConfirmationSummary(selected);
				string message =
					"Apply the selected safe fixes to the opened GRF?\r\n\r\n" +
					summary +
					"\r\n\r\nThe GRF will not be saved automatically. You can undo changes with Edit > Undo.";

				if (!ErrorHandler.YesNoRequest(message, "Apply selected fixes"))
					return;

				var fixResult = _autoFixService.Apply(_grfHolder, selected);

				string resultMessage = String.Format(
					"Applied fixes: {0} removed, {1} renamed, {2} skipped.",
					fixResult.RemovedCount,
					fixResult.RenamedCount,
					fixResult.SkippedCount);

				if (fixResult.Messages.Count > 0)
					resultMessage += "\r\n\r\n" + String.Join("\r\n", fixResult.Messages.Take(8).ToArray());

				resultMessage += "\r\n\r\nRe-running validation...";
				WindowProvider.ShowDialog(resultMessage, "Fixes applied", MessageBoxButton.OK);

				_startValidation();
			}
			catch (Exception err) {
				ErrorHandler.HandleException(err);
			}
		}

		private void _buttonCopySelected_Click(object sender, RoutedEventArgs e) {
			try {
				var items = _listViewIssues.SelectedItems.Cast<RagnarokValidationView>().ToList();

				if (items.Count == 0) {
					ErrorHandler.HandleException("Select one or more issues first.", ErrorLevel.Low);
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
				if (_filteredViews.Count == 0) {
					ErrorHandler.HandleException("There are no issues to copy.", ErrorLevel.Low);
					return;
				}

				_copyToClipboard(_filteredViews);
			}
			catch (Exception err) {
				ErrorHandler.HandleException(err);
			}
		}

		private static void _copyToClipboard(IEnumerable<RagnarokValidationView> views) {
			string header = "Severity\tCategory\tRelativePath\tMessage\tSuggestedFix\tCanAutoFix";
			string body = String.Join(Environment.NewLine, views.Select(p => p.ToCopyLine()));
			Clipboard.SetDataObject(header + Environment.NewLine + body);
		}

		private void _buttonExportCsv_Click(object sender, RoutedEventArgs e) {
			_exportFile("Ragnarok validation (*.csv)|*.csv", "csv", RagnarokValidationExport.ToCsv);
		}

		private void _buttonExportJson_Click(object sender, RoutedEventArgs e) {
			_exportFile("Ragnarok validation (*.json)|*.json", "json", RagnarokValidationExport.ToJson);
		}

		private void _exportFile(string filter, string extension, Func<IEnumerable<RagnarokValidationView>, string> serialize) {
			try {
				if (_filteredViews.Count == 0) {
					ErrorHandler.HandleException("There are no issues to export.", ErrorLevel.Low);
					return;
				}

				string exportFolder = ActiveProjectProfile.GetExportFolderPath();
				string initialDirectory = !String.IsNullOrEmpty(exportFolder) && Directory.Exists(exportFolder)
					? exportFolder
					: null;

				string path = initialDirectory == null
					? PathRequest.SaveFileEditor("filter", filter, "fileName", "ragnarok-validation." + extension)
					: PathRequest.SaveFileEditor("filter", filter, "fileName", "ragnarok-validation." + extension, "initialDirectory", initialDirectory);

				if (path == null)
					return;

				if (!path.IsExtension("." + extension))
					path += "." + extension;

				RagnarokValidationExport.WriteUtf8(path, serialize(_filteredViews));
				Utilities.Services.OpeningService.FileOrFolder(path);
			}
			catch (Exception err) {
				ErrorHandler.HandleException(err);
			}
		}

		private void _buttonClose_Click(object sender, RoutedEventArgs e) {
			Close();
		}
	}
}
