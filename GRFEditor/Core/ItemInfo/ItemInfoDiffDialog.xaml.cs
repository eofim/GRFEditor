using System;
using System.Windows;
using TokeiLibrary.WPF.Styles;

namespace GRFEditor.Core.ItemInfo {
	public partial class ItemInfoDiffDialog : TkWindow {
		public ItemInfoDiffDialog(string diffText, string targetPath, string backupNote)
			: base("Preview iteminfo", "settings.ico", SizeToContent.Manual, ResizeMode.CanResize) {
			InitializeComponent();
			_textHint.Text = "Arquivo: " + (targetPath ?? "")
				+ (string.IsNullOrEmpty(backupNote) ? "" : Environment.NewLine + backupNote)
				+ Environment.NewLine
				+ "Linhas removidas (-) e adicionadas (+). O GRF não é salvo automaticamente.";
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
