using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using ErrorManager;
using GRF.Core;
using GRF.Threading;
using GRFEditor.ApplicationConfiguration;
using GRFEditor.Core.ProjectProfiles;
using AsyncOperation = GrfToWpfBridge.Application.AsyncOperation;
using TokeiLibrary;
using TokeiLibrary.WPF;
using TokeiLibrary.WPF.Styles;
using Utilities;
using Utilities.Services;

namespace GRFEditor.Core.BuildPipeline {
	public partial class BuildPipelineDialog : TkWindow {
		private sealed class StepDisplayItem {
			public string StepKey { get; set; }
			public string DisplayName { get; set; }
			public string Status { get; set; }
			public string Result { get; set; }
			public string Detail { get; set; }
		}

		private readonly GrfHolder _grfHolder;
		private readonly BuildPipelineService _pipelineService = new BuildPipelineService();
		private readonly AsyncOperation _asyncOperation;
		private readonly ObservableCollection<StepDisplayItem> _stepItems = new ObservableCollection<StepDisplayItem>();

		private BuildPipelineResult _lastResult;
		private CancellationTokenSource _cancellationSource;
		private Dictionary<string, StepDisplayItem> _stepByKey = new Dictionary<string, StepDisplayItem>(StringComparer.OrdinalIgnoreCase);

		public BuildPipelineDialog(GrfHolder grfHolder)
			: base("Build pipeline", "pack.png", SizeToContent.Manual, ResizeMode.CanResize) {
			_grfHolder = grfHolder;
			InitializeComponent();

			_asyncOperation = new AsyncOperation(_progressBar);
			_asyncOperation.Cancelling += _asyncOperation_Cancelling;
			Owner = WpfUtilities.TopWindow;

			_listSteps.ItemsSource = _stepItems;
			_progressBar.SetSpecialState(TkProgressBar.ProgressStatus.Finished);
			_updatePreviousManifestUi();

			Loaded += _window_Loaded;
		}

		protected override void OnClosing(CancelEventArgs e) {
			_asyncOperation.Cancel();
			_cancellationSource?.Cancel();
			base.OnClosing(e);
		}

		private void _window_Loaded(object sender, RoutedEventArgs e) {
			bool hasActive = ActiveProjectProfile.HasActive;
			_radioActiveProfile.IsChecked = hasActive;
			_radioManualProfile.IsChecked = !hasActive;
			_applyProfileSourceUi();
		}

		private void _changelogOption_Changed(object sender, RoutedEventArgs e) {
			if (!IsLoaded)
				return;

			_updatePreviousManifestUi();
		}

		private void _updatePreviousManifestUi() {
			bool changelogEnabled = _cbChangelog.IsChecked == true;
			_pbPreviousManifest.IsEnabled = changelogEnabled && _buttonRun.IsEnabled;
		}

		private void _profileSource_Changed(object sender, RoutedEventArgs e) {
			if (!IsLoaded)
				return;

			_applyProfileSourceUi();
		}

		private void _applyProfileSourceUi() {
			bool useActive = _radioActiveProfile.IsChecked == true;

			_tbOutputDirectory.IsEnabled = !useActive;
			_buttonBrowseOutput.IsEnabled = !useActive;

			if (useActive) {
				if (ActiveProjectProfile.HasActive) {
					_tbOutputDirectory.Text = ActiveProjectProfile.GetExportFolderPath()
						?? ActiveProjectProfile.GetPatchOutputFolderPath()
						?? ProjectProfileBuildContext.GetPatchOutputDirectory();

					_textProfileHint.Text = "Using active profile: " + ActiveProjectProfile.DisplayName
						+ ". Output folder and build rules come from the profile.";
				}
				else {
					_textProfileHint.Text = "No active project profile. Select Manual or set a profile in Tools → Project profiles.";
					_tbOutputDirectory.Text = ProjectProfileBuildContext.GetPatchOutputDirectory();
				}

				var options = BuildPipelineOptions.FromActiveProfile(_grfHolder);
				_cbRemoveJunk.IsChecked = options.RemoveJunkFiles;
			}
			else {
				_textProfileHint.Text = "Manual mode: output folder is taken from the path above. Profile name is optional in reports.";
				if (String.IsNullOrWhiteSpace(_tbOutputDirectory.Text))
					_tbOutputDirectory.Text = ProjectProfileBuildContext.GetPatchOutputDirectory();
			}
		}

		private void _buttonBrowseOutput_Click(object sender, RoutedEventArgs e) {
			string folder = PathRequest.FolderEditor();
			if (!String.IsNullOrEmpty(folder))
				_tbOutputDirectory.Text = folder;
		}

		private void _buttonRun_Click(object sender, RoutedEventArgs e) {
			try {
				if (_asyncOperation.IsRunning) {
					ErrorHandler.HandleException("A pipeline run is already in progress.", ErrorLevel.Low);
					return;
				}

				if (_grfHolder == null || !_grfHolder.IsOpened) {
					ErrorHandler.HandleException("Open a GRF first.", ErrorLevel.Low);
					return;
				}

				if (!_cbValidate.IsChecked == true && !_cbRemoveJunk.IsChecked == true && !_cbManifest.IsChecked == true
					&& !_cbChangelog.IsChecked == true && !_cbHashes.IsChecked == true && !_cbExportReport.IsChecked == true) {
					ErrorHandler.HandleException("Select at least one pipeline step.", ErrorLevel.Low);
					return;
				}

				string outputDir = _tbOutputDirectory.Text.Trim();
				if (String.IsNullOrEmpty(outputDir)) {
					ErrorHandler.HandleException("Output folder is required.", ErrorLevel.Low);
					return;
				}

				if (_cbRemoveJunk.IsChecked == true && _cbValidate.IsChecked != true) {
					ErrorHandler.HandleException("Enable Validate files when using Remove junk files.", ErrorLevel.Low);
					return;
				}

				BuildPipelineOptions options = _buildOptionsFromUi(outputDir);

				if (options.RemoveJunkFiles) {
					var confirm = MessageBox.Show(
						this,
						"The pipeline will remove junk/system files from the open GRF (in memory only).\n\n"
						+ "The GRF will NOT be saved automatically. You must save manually if you want to keep changes.\n\n"
						+ "Continue?",
						"Remove junk files",
						MessageBoxButton.YesNo,
						MessageBoxImage.Warning);

					if (confirm != MessageBoxResult.Yes)
						return;

					options.ApplyGrfModifications = true;
				}

				_prepareStepList(options);
				_tbFinalResult.Text = "";
				_setExportButtonsEnabled(false);
				_textSummary.Text = "Running build pipeline...";
				_setRunEnabled(false);

				_cancellationSource?.Dispose();
				_cancellationSource = new CancellationTokenSource();

				_asyncOperation.SetAndRunOperation(
					prog => _lastResult = _pipelineService.Run(
						options,
						prog,
						_cancellationSource.Token,
						_onStepStarting,
						_onStepCompleted),
					_onPipelineFinished);
			}
			catch (Exception err) {
				ErrorHandler.HandleException(err);
				_setRunEnabled(true);
			}
		}

		private BuildPipelineOptions _buildOptionsFromUi(string outputDir) {
			bool useActive = _radioActiveProfile.IsChecked == true;

			var options = useActive && ActiveProjectProfile.HasActive
				? BuildPipelineOptions.FromActiveProfile(_grfHolder)
				: new BuildPipelineOptions { Grf = _grfHolder };

			options.OutputDirectory = outputDir;
			options.ProfileName = useActive ? ActiveProjectProfile.DisplayName : null;
			options.StopOnValidationErrors = _cbStopOnErrors.IsChecked == true;

			options.RunValidation = _cbValidate.IsChecked == true;
			options.RemoveJunkFiles = _cbRemoveJunk.IsChecked == true;
			options.ApplyGrfModifications = false;
			options.NormalizePaths = false;

			options.GenerateManifest = _cbManifest.IsChecked == true;
			options.GenerateChangelog = _cbChangelog.IsChecked == true;
			options.PreviousManifestPath = options.GenerateChangelog
				? (_pbPreviousManifest.Text ?? "").Trim()
				: null;
			options.GenerateHashes = _cbHashes.IsChecked == true;
			options.ExportBuildReport = _cbExportReport.IsChecked == true;
			options.ExportManifestCsv = _cbExportManifestCsv.IsChecked == true;

			return options;
		}

		private void _prepareStepList(BuildPipelineOptions options) {
			_stepItems.Clear();
			_stepByKey.Clear();

			if (_cbValidate.IsChecked == true)
				_addStepItem(BuildPipelineService.StepValidateRagnarokClientFiles, "Validate files");

			if (options.RemoveJunkFiles)
				_addStepItem(BuildPipelineService.StepRemoveJunkFiles, "Remove junk files");

			if (options.GenerateManifest)
				_addStepItem(BuildPipelineService.StepGenerateManifest, "Generate manifest");

			if (options.GenerateChangelog)
				_addStepItem(BuildPipelineService.StepGenerateChangelog, "Generate changelog");

			if (options.GenerateHashes)
				_addStepItem(BuildPipelineService.StepGenerateHashes, "Generate hashes");

			if (options.ExportBuildReport)
				_addStepItem(BuildPipelineService.StepExportBuildReport, "Export report");
		}

		private void _addStepItem(string key, string displayName) {
			var item = new StepDisplayItem {
				StepKey = key,
				DisplayName = displayName,
				Status = "Pending",
				Result = "",
				Detail = "",
			};
			_stepByKey[key] = item;
			_stepItems.Add(item);
		}

		private void _onStepStarting(string stepName) {
			this.Dispatch(delegate {
				StepDisplayItem item;
				if (_stepByKey.TryGetValue(stepName, out item)) {
					item.Status = "Running";
					item.Detail = "";
					_listSteps.Items.Refresh();
				}

				_textSummary.Text = "Running: " + _friendlyStepName(stepName);
			});
		}

		private void _onStepCompleted(BuildStepResult step) {
			if (step == null)
				return;

			this.Dispatch(delegate {
				StepDisplayItem item;
				if (!_stepByKey.TryGetValue(step.Name, out item))
					item = _stepByKey.Values.FirstOrDefault(i => String.Equals(i.StepKey, step.Name, StringComparison.OrdinalIgnoreCase));

				if (item == null)
					return;

				item.Status = "Done";
				item.Result = step.Success ? "OK" : "Failed";
				item.Detail = _formatStepDetail(step);
				_listSteps.Items.Refresh();
			});
		}

		private void _onPipelineFinished(object state) {
			try {
				this.Dispatch(delegate {
					_setRunEnabled(true);
					_progressBar.SetSpecialState(_lastResult != null && _lastResult.Success
						? TkProgressBar.ProgressStatus.Finished
						: TkProgressBar.ProgressStatus.ErrorsDetected);

					if (_lastResult == null) {
						_textSummary.Text = "Pipeline finished without a result.";
						return;
					}

					_tbFinalResult.Text = BuildPipelineReportWriter.ToPlainText(_lastResult);
					_textSummary.Text = _lastResult.Success
						? "Pipeline completed successfully."
						: "Pipeline completed with issues (" + _lastResult.Severity + ").";

					_setExportButtonsEnabled(true);

					if (!String.IsNullOrEmpty(_lastResult.ReportJsonPath) || !String.IsNullOrEmpty(_lastResult.ReportTextPath))
						_textSummary.Text += " Reports written to output folder.";
				});
			}
			catch (Exception err) {
				ErrorHandler.HandleException(err);
			}
		}

		private void _asyncOperation_Cancelling(object state) {
			_cancellationSource?.Cancel();
			this.Dispatch(delegate {
				_textSummary.Text = "Cancelling pipeline...";
			});
		}

		private void _buttonExportJson_Click(object sender, RoutedEventArgs e) {
			_exportReport(isJson: true);
		}

		private void _buttonExportTxt_Click(object sender, RoutedEventArgs e) {
			_exportReport(isJson: false);
		}

		private void _exportReport(bool isJson) {
			try {
				if (_lastResult == null) {
					ErrorHandler.HandleException("Run the pipeline first.", ErrorLevel.Low);
					return;
				}

				string sourcePath = isJson ? _lastResult.ReportJsonPath : _lastResult.ReportTextPath;
				string filter = isJson
					? "Build report JSON (*.json)|*.json"
					: "Build report text (*.txt)|*.txt";
				string defaultName = isJson ? "build-report.json" : "build-report.txt";

				if (!String.IsNullOrEmpty(sourcePath) && File.Exists(sourcePath)) {
					string savePath = PathRequest.SaveFileEditor(
						"filter", filter,
						"fileName", defaultName,
						"initialDirectory", Path.GetDirectoryName(sourcePath));

					if (savePath == null)
						return;

					File.Copy(sourcePath, savePath, true);
					OpeningService.FileOrFolder(savePath);
					return;
				}

				string path = PathRequest.SaveFileEditor("filter", filter, "fileName", defaultName);
				if (path == null)
					return;

				string content = isJson
					? BuildPipelineReportWriter.ToJson(_lastResult)
					: BuildPipelineReportWriter.ToPlainText(_lastResult);

				File.WriteAllText(path, content, new System.Text.UTF8Encoding(true));
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

		private void _setRunEnabled(bool enabled) {
			_buttonRun.IsEnabled = enabled;
			_cbValidate.IsEnabled = enabled;
			_cbRemoveJunk.IsEnabled = enabled;
			_cbManifest.IsEnabled = enabled;
			_cbChangelog.IsEnabled = enabled;
			_cbHashes.IsEnabled = enabled;
			_cbExportReport.IsEnabled = enabled;
			_cbExportManifestCsv.IsEnabled = enabled;
			_cbStopOnErrors.IsEnabled = enabled;
			_radioActiveProfile.IsEnabled = enabled;
			_radioManualProfile.IsEnabled = enabled;
			_updatePreviousManifestUi();
			if (enabled)
				_applyProfileSourceUi();
		}

		private void _setExportButtonsEnabled(bool enabled) {
			_buttonExportJson.IsEnabled = enabled;
			_buttonExportTxt.IsEnabled = enabled;
		}

		private static string _friendlyStepName(string stepName) {
			switch (stepName) {
				case BuildPipelineService.StepValidateRagnarokClientFiles: return "Validate files";
				case BuildPipelineService.StepRemoveJunkFiles: return "Remove junk files";
				case BuildPipelineService.StepGenerateManifest: return "Generate manifest";
				case BuildPipelineService.StepGenerateChangelog: return "Generate changelog";
				case BuildPipelineService.StepGenerateHashes: return "Generate hashes";
				case BuildPipelineService.StepExportBuildReport: return "Export report";
				default: return stepName;
			}
		}

		private static string _formatStepDetail(BuildStepResult step) {
			var parts = new List<string>();
			if (step.FilesAffected.Count > 0)
				parts.Add(step.FilesAffected.Count + " file(s)");

			if (step.Warnings.Count > 0)
				parts.Add(step.Warnings.Count + " warning(s)");

			if (step.Errors.Count > 0)
				parts.Add(step.Errors.Count + " error(s)");

			if (parts.Count == 0)
				return step.Success ? "Completed" : "Failed";

			return String.Join(", ", parts);
		}
	}
}
