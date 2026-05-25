using System.Windows;
using TokeiLibrary.WPF.Styles;

namespace GRFEditor.Tools.CustomAccessory {
	public partial class CustomAccessoryManagerDiffDialog : TkWindow {
		public CustomAccessoryManagerDiffDialog(string diffText)
			: base("Preview das alterações", "settings.ico", SizeToContent.Manual, ResizeMode.CanResize) {
			InitializeComponent();
			_textDiff.Text = diffText ?? "";
		}

		private void _buttonApply_Click(object sender, RoutedEventArgs e) {
			DialogResult = true;
		}

		private void _buttonCancel_Click(object sender, RoutedEventArgs e) {
			DialogResult = false;
		}
	}
}
