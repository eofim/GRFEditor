using System.Collections.Generic;
using System.Linq;
using System.Windows;
using TokeiLibrary.WPF.Styles;

namespace GRFEditor.Core.ItemInfo {
	public partial class ItemInfoCsvPreviewDialog : TkWindow {
		public ItemInfoCsvImportResult ImportResult { get; private set; }

		public ItemInfoCsvPreviewDialog(ItemInfoCsvImportResult importResult)
			: base("Prévia CSV iteminfo", "settings.ico", SizeToContent.Manual, ResizeMode.CanResize) {
			ImportResult = importResult;
			InitializeComponent();
			_textReport.Text = importResult?.BuildPreviewReport() ?? "";
			_gridPreview.ItemsSource = importResult?.Rows ?? new List<ItemInfoCsvImportRow>();
		}

		private void _buttonSelectValid_Click(object sender, RoutedEventArgs e) {
			if (ImportResult?.Rows == null)
				return;

			foreach (var row in ImportResult.Rows) {
				row.Selected = row.CanApply;
			}

			_gridPreview.Items.Refresh();
		}

		private void _buttonLoad_Click(object sender, RoutedEventArgs e) {
			if (ImportResult == null || !ImportResult.HeaderValid) {
				MessageBox.Show(this, "Corrija o cabeçalho do CSV antes de continuar.",
					"CSV", MessageBoxButton.OK, MessageBoxImage.Warning);
				return;
			}

			if (!ImportResult.Rows.Any(r => r.Selected && r.CanApply)) {
				MessageBox.Show(this, "Nenhuma linha válida selecionada para aplicar.",
					"CSV", MessageBoxButton.OK, MessageBoxImage.Information);
				return;
			}

			DialogResult = true;
		}

		private void _buttonCancel_Click(object sender, RoutedEventArgs e) {
			DialogResult = false;
		}
	}
}
