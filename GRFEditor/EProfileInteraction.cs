using System;
using System.Windows;
using GRFEditor.Core.ProjectProfiles;

namespace GRFEditor {
	partial class EditorMainWindow {
		private void _loadActiveProfileIndicator() {
			_refreshActiveProfileIndicator();
			Activated += (s, e) => _refreshActiveProfileIndicator();
			ActiveProjectProfile.Changed += (s, e) => _refreshActiveProfileIndicator();
		}

		private void _refreshActiveProfileIndicator() {
			if (_textActiveProfile == null)
				return;

			string name = ActiveProjectProfile.DisplayName;

			if (String.IsNullOrEmpty(name)) {
				_textActiveProfile.Visibility = Visibility.Collapsed;
				_textActiveProfile.Text = "";
				return;
			}

			_textActiveProfile.Text = "Profile: " + name;
			_textActiveProfile.ToolTip = ActiveProjectProfile.GetStatusToolTip();
			_textActiveProfile.Visibility = Visibility.Visible;
		}
	}
}
