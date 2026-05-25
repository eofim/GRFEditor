using System.IO;
using System.Windows;
using TokeiLibrary.WPF.Styles;

namespace GRFEditor.Tools.CustomAccessory {
	public partial class CustomAccessoryLubPathsDialog : TkWindow {
		public CustomAccessoryLubPathsDialog()
			: base("Arquivos Lua", "settings.ico", SizeToContent.Height, ResizeMode.NoResize) {
			InitializeComponent();
		}

		public string AccessoryIdPath => _pbAccessoryId?.Text?.Trim();

		public string AccnamePath => _pbAccname?.Text?.Trim();

		private void _buttonOk_Click(object sender, RoutedEventArgs e) {
			if (string.IsNullOrEmpty(AccessoryIdPath) || !File.Exists(AccessoryIdPath)) {
				MessageBox.Show(this, "Selecione um accessoryid.lub válido.", "Arquivos Lua",
					MessageBoxButton.OK, MessageBoxImage.Warning);
				return;
			}

			if (string.IsNullOrEmpty(AccnamePath) || !File.Exists(AccnamePath)) {
				MessageBox.Show(this, "Selecione um accname.lub válido.", "Arquivos Lua",
					MessageBoxButton.OK, MessageBoxImage.Warning);
				return;
			}

			DialogResult = true;
		}

		private void _buttonCancel_Click(object sender, RoutedEventArgs e) {
			DialogResult = false;
		}
	}
}
