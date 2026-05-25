using System;
using System.IO;
using System.Windows;
using GRFEditor.ApplicationConfiguration;
using GRFEditor.Core.ProjectProfiles;
using TokeiLibrary.WPF.Styles;
using Utilities.Extension;
using Utilities.Services;

namespace GRFEditor.Tools.CustomAccessory {
	public partial class CustomAccessoryBatchImportReportDialog : TkWindow {
		private readonly CustomAccessoryBatchImportResult _importResult;

		public CustomAccessoryBatchImportReportDialog(CustomAccessoryBatchImportResult importResult)
			: base("Relatório de importação", "convert.png", SizeToContent.Manual, ResizeMode.CanResize) {
			_importResult = importResult ?? new CustomAccessoryBatchImportResult();
			InitializeComponent();

			_textSummary.Text = CustomAccessoryBatchImportResult.FormatSummary(_importResult);
			_textReport.Text = _importResult.ReportText;
		}

		private void _buttonExportPreview_Click(object sender, RoutedEventArgs e) {
			try {
				string exportFolder = ActiveProjectProfile.GetExportFolderPath();
				string initialDirectory = !string.IsNullOrEmpty(exportFolder) && Directory.Exists(exportFolder)
					? exportFolder
					: _importResult.SourceFolder;

				string path = PathRequest.SaveFileEditor(
					"filter", "Import preview (*.csv)|*.csv",
					"fileName", "accessory-import-preview.csv",
					"initialDirectory", initialDirectory);

				if (path == null)
					return;

				if (!path.IsExtension(".csv"))
					path += ".csv";

				CustomAccessoryManagerExport.WriteUtf8(path,
					CustomAccessoryManagerExport.ToImportPreviewCsv(_importResult.ManagerEntries));

				OpeningService.FileOrFolder(path);
			}
			catch (Exception ex) {
				MessageBox.Show(this, ex.Message, "Exportar CSV", MessageBoxButton.OK, MessageBoxImage.Error);
			}
		}

		private void _buttonLoad_Click(object sender, RoutedEventArgs e) {
			DialogResult = true;
		}

		private void _buttonClose_Click(object sender, RoutedEventArgs e) {
			DialogResult = false;
		}
	}
}
