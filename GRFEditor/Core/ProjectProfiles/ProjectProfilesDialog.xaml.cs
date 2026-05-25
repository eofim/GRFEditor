using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using ErrorManager;
using GrfToWpfBridge.Application;
using TokeiLibrary;
using TokeiLibrary.WPF.Styles;
using Utilities;
using Utilities.Services;

namespace GRFEditor.Core.ProjectProfiles {
	public partial class ProjectProfilesDialog : TkWindow {
		private sealed class ProfileListItem {
			public ProfileListItem(ProjectProfile profile, bool isActive) {
				Profile = profile;
				IsActive = isActive;
				DisplayName = isActive ? profile.Name + "  (active)" : profile.Name;
			}

			public ProjectProfile Profile { get; private set; }
			public bool IsActive { get; private set; }
			public string DisplayName { get; private set; }
		}

		private readonly ProjectProfileService _service = new ProjectProfileService();
		private List<ProjectProfile> _profiles = new List<ProjectProfile>();
		private ProjectProfile _editingBase;
		private string _loadedProfileName;
		private int _profileEncodingCodepage = 1252;
		private bool _suppressSelectionChange;
		private bool _suppressDirtyTracking;
		private bool _isDirty;

		public ProjectProfilesDialog() {
			InitializeComponent();
			Title = "Project profiles";
		}

		private void _window_Loaded(object sender, RoutedEventArgs e) {
			try {
				_initEncodingPicker();

				_refreshProfileList(selectActive: true);

				if (_profiles.Count == 0)
					_newProfileForm();
			}
			catch (Exception err) {
				ErrorHandler.HandleException(err);
			}
		}

		private void _refreshProfileList(bool selectActive) {
			_profiles = _service.LoadProfiles();
			string activeName = _service.GetActiveProfileName();

			var items = _profiles
				.Select(p => new ProfileListItem(p, !String.IsNullOrEmpty(activeName)
					&& String.Equals(p.Name, activeName, StringComparison.OrdinalIgnoreCase)))
				.ToList();

			_suppressSelectionChange = true;
			_listProfiles.ItemsSource = items;
			_suppressSelectionChange = false;

			if (!selectActive || items.Count == 0)
				return;

			ProfileListItem activeItem = items.FirstOrDefault(i => i.IsActive) ?? items[0];

			_suppressSelectionChange = true;
			_listProfiles.SelectedItem = activeItem;
			_suppressSelectionChange = false;

			_loadProfileIntoForm(activeItem.Profile);
		}

		private void _listProfiles_SelectionChanged(object sender, SelectionChangedEventArgs e) {
			if (_suppressSelectionChange)
				return;

			if (!_confirmDiscardChanges())
				return;

			var item = _listProfiles.SelectedItem as ProfileListItem;

			if (item == null) {
				_newProfileForm();
				return;
			}

			_loadProfileIntoForm(item.Profile.Clone());
		}

		private void _buttonNew_Click(object sender, RoutedEventArgs e) {
			if (!_confirmDiscardChanges())
				return;

			_listProfiles.SelectedItem = null;
			_newProfileForm();
		}

		private void _buttonDuplicate_Click(object sender, RoutedEventArgs e) {
			try {
				if (!_confirmDiscardChanges())
					return;

				ProjectProfile source = _readProfileFromForm();

				if (String.IsNullOrWhiteSpace(source.Name) && _listProfiles.SelectedItem == null) {
					ErrorHandler.HandleException("Select a profile to duplicate or enter a name first.", ErrorLevel.Low);
					return;
				}

				if (String.IsNullOrWhiteSpace(source.Name)) {
					var selected = _listProfiles.SelectedItem as ProfileListItem;

					if (selected == null)
						return;

					source = selected.Profile.Clone();
				}

				var copy = source.Clone();
				copy.Name = _generateUniqueName(source.Name + " (copy)");
				copy.CreatedAt = DateTime.UtcNow;
				copy.UpdatedAt = copy.CreatedAt;

				_loadProfileIntoForm(copy);
				_listProfiles.SelectedItem = null;
				_updatePathWarnings();
			}
			catch (Exception err) {
				ErrorHandler.HandleException(err);
			}
		}

		private void _buttonDelete_Click(object sender, RoutedEventArgs e) {
			try {
				string nameToDelete = _loadedProfileName;

				if (String.IsNullOrWhiteSpace(nameToDelete)) {
					var selected = _listProfiles.SelectedItem as ProfileListItem;

					if (selected != null)
						nameToDelete = selected.Profile.Name;
				}

				if (String.IsNullOrWhiteSpace(nameToDelete)) {
					ErrorHandler.HandleException("Select a saved profile to delete.", ErrorLevel.Low);
					return;
				}

				var result = MessageBox.Show(
					this,
					"Delete profile \"" + nameToDelete + "\"?\n\nThis cannot be undone.",
					"Delete profile",
					MessageBoxButton.YesNo,
					MessageBoxImage.Warning);

				if (result != MessageBoxResult.Yes)
					return;

				_service.DeleteProfile(nameToDelete);
				_isDirty = false;
				ActiveProjectProfile.NotifyChanged();
				_refreshProfileList(selectActive: true);
				_newProfileForm();
			}
			catch (Exception err) {
				ErrorHandler.HandleException(err);
			}
		}

		private void _buttonValidatePaths_Click(object sender, RoutedEventArgs e) {
			_updatePathWarnings();
		}

		private void _buttonSetActive_Click(object sender, RoutedEventArgs e) {
			try {
				ProjectProfile profile = _readProfileFromForm();

				if (String.IsNullOrWhiteSpace(profile.Name)) {
					ErrorHandler.HandleException("Profile name is required.", ErrorLevel.Low);
					return;
				}

				var existing = _service.TryGetProfile(profile.Name);

				if (existing == null) {
					ErrorHandler.HandleException("Save the profile before setting it as active.", ErrorLevel.Low);
					return;
				}

				if (_isDirty) {
					var saveFirst = MessageBox.Show(
						this,
						"The profile has unsaved changes. Save before setting as active?",
						"Unsaved changes",
						MessageBoxButton.YesNoCancel,
						MessageBoxImage.Question);

					if (saveFirst == MessageBoxResult.Cancel)
						return;

					if (saveFirst == MessageBoxResult.Yes && !_saveCurrentProfile())
						return;
				}

				var confirm = MessageBox.Show(
					this,
					"Set \"" + profile.Name + "\" as the active project profile?\n\n"
					+ "Only the active profile setting is updated. The GRF will not be opened and the current session is unchanged.",
					"Set active profile",
					MessageBoxButton.YesNo,
					MessageBoxImage.Question);

				if (confirm != MessageBoxResult.Yes)
					return;

				_service.SetActiveProfile(profile.Name);
				_isDirty = false;
				ActiveProjectProfile.NotifyChanged();
				_refreshProfileList(selectActive: true);
				_selectProfileByName(profile.Name);
			}
			catch (Exception err) {
				ErrorHandler.HandleException(err);
			}
		}

		private void _buttonSave_Click(object sender, RoutedEventArgs e) {
			_saveCurrentProfile();
		}

		private void _buttonClose_Click(object sender, RoutedEventArgs e) {
			if (_confirmDiscardChanges())
				Close();
		}

		private bool _saveCurrentProfile() {
			try {
				ProjectProfile profile = _readProfileFromForm();

				if (String.IsNullOrWhiteSpace(profile.Name)) {
					ErrorHandler.HandleException("Profile name is required.", ErrorLevel.Low);
					return false;
				}

				string newName = profile.Name.Trim();
				profile.Name = newName;

				bool nameConflict = _profiles.Any(p =>
					String.Equals(p.Name, newName, StringComparison.OrdinalIgnoreCase)
					&& !String.Equals(p.Name, _loadedProfileName ?? "", StringComparison.OrdinalIgnoreCase));

				if (nameConflict) {
					ErrorHandler.HandleException("A profile with this name already exists.", ErrorLevel.Low);
					return false;
				}

				_service.SaveProfile(profile);

				if (!String.IsNullOrWhiteSpace(_loadedProfileName)
					&& !String.Equals(_loadedProfileName, newName, StringComparison.OrdinalIgnoreCase))
					_service.DeleteProfile(_loadedProfileName);

				_loadedProfileName = newName;
				_isDirty = false;
				ActiveProjectProfile.NotifyChanged();
				_refreshProfileList(selectActive: false);
				_selectProfileByName(newName);
				_updatePathWarnings();

				return true;
			}
			catch (Exception err) {
				ErrorHandler.HandleException(err);
				return false;
			}
		}

		private void _newProfileForm() {
			var profile = new ProjectProfile {
				Name = _generateUniqueName("New profile"),
				EncodingName = "1252",
				LastUsedViewId = 0,
			};

			_loadProfileIntoForm(profile);
			_loadedProfileName = null;
		}

		private void _loadProfileIntoForm(ProjectProfile profile) {
			_suppressDirtyTracking = true;

			profile.EnsureDefaults();
			_editingBase = profile.Clone();
			_loadedProfileName = _profiles.Any(p =>
				String.Equals(p.Name, profile.Name, StringComparison.OrdinalIgnoreCase))
				? profile.Name
				: null;

			_tbName.Text = profile.Name ?? "";
			_pbMainGrf.Text = profile.MainGrfPath ?? "";
			_pbDataFolder.Text = profile.DataFolderPath ?? "";
			_pbExportFolder.Text = profile.ExportFolderPath ?? "";
			_pbPatchOutput.Text = profile.PatchOutputFolderPath ?? "";
			_pbClientFolder.Text = profile.ClientFolderPath ?? "";
			_pbAccessoryId.Text = profile.AccessoryIdPath ?? "";
			_pbAccName.Text = profile.AccNamePath ?? "";
			_pbItemInfo.Text = profile.ItemInfoPath ?? "";
			_tbLastViewId.Text = profile.LastUsedViewId.ToString(CultureInfo.InvariantCulture);

			_profileEncodingCodepage = _parseEncodingCodepage(profile.EncodingName);
			_initEncodingPicker();

			_isDirty = false;
			_suppressDirtyTracking = false;
			_updatePathWarnings();
		}

		private ProjectProfile _readProfileFromForm() {
			int viewId;

			if (!Int32.TryParse(_tbLastViewId.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out viewId))
				viewId = 0;

			var profile = _editingBase != null ? _editingBase.Clone() : new ProjectProfile();
			profile.Name = _tbName.Text.Trim();
			profile.MainGrfPath = _nullIfWhiteSpace(_pbMainGrf.Text);
			profile.DataFolderPath = _nullIfWhiteSpace(_pbDataFolder.Text);
			profile.ExportFolderPath = _nullIfWhiteSpace(_pbExportFolder.Text);
			profile.PatchOutputFolderPath = _nullIfWhiteSpace(_pbPatchOutput.Text);
			profile.ClientFolderPath = _nullIfWhiteSpace(_pbClientFolder.Text);
			profile.AccessoryIdPath = _nullIfWhiteSpace(_pbAccessoryId.Text);
			profile.AccNamePath = _nullIfWhiteSpace(_pbAccName.Text);
			profile.ItemInfoPath = _nullIfWhiteSpace(_pbItemInfo.Text);
			profile.EncodingName = _profileEncodingCodepage.ToString(CultureInfo.InvariantCulture);
			profile.LastUsedViewId = viewId;
			return profile;
		}

		private void _updatePathWarnings() {
			var warnings = ProjectProfilePathValidator.Validate(_readProfileFromForm());

			if (warnings.Count == 0) {
				_textPathWarnings.Text = "No path issues found for the current fields.";
				_textPathWarnings.Foreground = (System.Windows.Media.Brush)FindResource("TextForeground");
				return;
			}

			_textPathWarnings.Text = String.Join(Environment.NewLine, warnings);
		}

		private void _pathField_Changed(object sender, EventArgs e) {
			_markDirty();
			_updatePathWarnings();
		}

		private void _editorField_Changed(object sender, RoutedEventArgs e) {
			_markDirty();
		}

		private void _editorField_Changed(object sender, TextChangedEventArgs e) {
			_markDirty();
		}

		private void _comboEncoding_EncodingChanged(object sender, EncodingArgs args) {
			_markDirty();
		}

		private void _initEncodingPicker() {
			_comboEncoding.EncodingChanged -= _comboEncoding_EncodingChanged;
			_comboEncoding.Init(
				null,
				new TypeSetting<int>(v => _profileEncodingCodepage = v, () => _profileEncodingCodepage),
				new TypeSetting<Encoding>(v => { }, () => Encoding.GetEncoding(_profileEncodingCodepage)));
			_comboEncoding.EncodingChanged += _comboEncoding_EncodingChanged;
		}

		private void _markDirty() {
			if (_suppressDirtyTracking)
				return;

			_isDirty = true;
		}

		private bool _confirmDiscardChanges() {
			if (!_isDirty)
				return true;

			var result = MessageBox.Show(
				this,
				"Discard unsaved changes to this profile?",
				"Unsaved changes",
				MessageBoxButton.YesNo,
				MessageBoxImage.Question);

			if (result == MessageBoxResult.Yes) {
				_isDirty = false;
				return true;
			}

			_suppressSelectionChange = true;

			if (!String.IsNullOrWhiteSpace(_loadedProfileName)) {
				var item = (_listProfiles.ItemsSource as IEnumerable<ProfileListItem>)
					?.FirstOrDefault(i => String.Equals(i.Profile.Name, _loadedProfileName, StringComparison.OrdinalIgnoreCase));

				_listProfiles.SelectedItem = item;
			}

			_suppressSelectionChange = false;
			return false;
		}

		private void _selectProfileByName(string profileName) {
			var item = (_listProfiles.ItemsSource as IEnumerable<ProfileListItem>)
				?.FirstOrDefault(i => String.Equals(i.Profile.Name, profileName, StringComparison.OrdinalIgnoreCase));

			if (item == null)
				return;

			_suppressSelectionChange = true;
			_listProfiles.SelectedItem = item;
			_suppressSelectionChange = false;
			_loadProfileIntoForm(item.Profile.Clone());
		}

		private string _generateUniqueName(string baseName) {
			var names = new HashSet<string>(_profiles.Select(p => p.Name), StringComparer.OrdinalIgnoreCase);
			string candidate = baseName;
			int suffix = 2;

			while (names.Contains(candidate)) {
				candidate = baseName + " (" + suffix + ")";
				suffix++;
			}

			return candidate;
		}

		private static string _nullIfWhiteSpace(string value) {
			return String.IsNullOrWhiteSpace(value) ? null : value.Trim();
		}

		private static int _parseEncodingCodepage(string encodingName) {
			if (String.IsNullOrWhiteSpace(encodingName))
				return 1252;

			if (Int32.TryParse(encodingName.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int codepage)) {
				try {
					Encoding.GetEncoding(codepage);
					return codepage;
				}
				catch {
					return 1252;
				}
			}

			try {
				return Encoding.GetEncoding(encodingName.Trim()).CodePage;
			}
			catch {
				return 1252;
			}
		}
	}
}
