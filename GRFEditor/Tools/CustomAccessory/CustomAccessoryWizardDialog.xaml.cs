using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Windows;
using GRF.Core;
using GRFEditor.Core.ProjectProfiles;
using TokeiLibrary.WPF;
using TokeiLibrary.WPF.Styles;

namespace GRFEditor.Tools.CustomAccessory {
	public partial class CustomAccessoryWizardDialog : TkWindow {
		private readonly ObservableCollection<CustomAccessoryEntry> _entries = new ObservableCollection<CustomAccessoryEntry>();
		private readonly CustomAccessoryLuaTables _tables;
		private readonly CustomAccessoryLubLocations _lubLocations;
		private readonly GrfHolder _grf;

		public CustomAccessoryWizardDialog(GrfHolder grf, IList<string> spritePaths)
			: base("Custom accessory Lua", "settings.ico", SizeToContent.Manual, ResizeMode.CanResize) {
			InitializeComponent();
			_grf = grf;
			_lubLocations = CustomAccessoryLubLocations.Resolve(grf);
			_tables = CustomAccessoryLuaTables.Load(
				_lubLocations.EditAccessoryIdPath,
				_lubLocations.EditAccnamePath);

			var paths = spritePaths != null && spritePaths.Count > 0
				? spritePaths
				: CustomAccessoryLuaService.FindSpritePathsInGrf(grf);

			int? startViewId;
			TryGetInitialViewId(out startViewId);

			foreach (var entry in CustomAccessoryLuaService.BuildEntriesFromSprites(paths, _tables, startViewId))
				_entries.Add(entry);

			_gridEntries.ItemsSource = _entries;

			int? profileViewId = ActiveProjectProfile.GetLastUsedViewId();
			if (profileViewId.HasValue && _textInitialViewId != null && String.IsNullOrWhiteSpace(_textInitialViewId.Text))
				_textInitialViewId.Text = profileViewId.Value.ToString();

			ActiveProjectProfile.ConfirmContinueWithInvalidPaths(
				this,
				"Custom accessory Lua",
				ActiveProjectProfile.GetPathWarningsForTool(p => p.AccessoryIdPath, p => p.AccNamePath, p => p.ClientFolderPath));

			if (!_lubLocations.IsValid) {
				_textStatus.Text = _lubLocations.GetMissingFilesMessage();
				MessageBox.Show(this, _lubLocations.GetMissingFilesMessage(), "Arquivos Lua", MessageBoxButton.OK, MessageBoxImage.Warning);
			}
			else {
				UpdateStatusText(_entries.Count + " item(ns) carregado(s). " + _lubLocations.GetSourceDescription());
			}
		}

		private void UpdateStatusText(string actionMessage) {
			var loadInfo = string.IsNullOrWhiteSpace(_tables.LoadStatusMessage)
				? ""
				: _tables.LoadStatusMessage + " ";

			_textStatus.Text = loadInfo + actionMessage;
		}

		private bool TryGetInitialViewId(out int? startViewId, bool showError = false) {
			startViewId = null;
			var text = _textInitialViewId != null ? _textInitialViewId.Text.Trim() : "";

			if (string.IsNullOrEmpty(text))
				return true;

			int parsed;
			if (!int.TryParse(text, out parsed) || parsed <= 0) {
				if (showError) {
					MessageBox.Show(this,
						"ID inicial deve ser um inteiro positivo.",
						"ID inicial",
						MessageBoxButton.OK,
						MessageBoxImage.Warning);
				}

				return false;
			}

			startViewId = parsed;
			return true;
		}

		private void _buttonAutoViewIds_Click(object sender, RoutedEventArgs e) {
			int? startViewId;
			if (!TryGetInitialViewId(out startViewId, true))
				return;

			var nextId = startViewId ?? _tables.GetNextViewId();
			foreach (var entry in _entries.Where(p => p.Selected && p.Status == CustomAccessoryEntryStatus.New).OrderBy(p => p.ConstantName)) {
				entry.ViewId = nextId++;
			}

			var baseInfo = startViewId.HasValue
				? "a partir de " + startViewId.Value
				: "sequenciais (maxId + 1)";
			UpdateStatusText("ViewIds atribuídos para itens novos selecionados " + baseInfo + ".");
		}

		private void _buttonSuggestOpenAi_Click(object sender, RoutedEventArgs e) {
			var selected = _entries.Where(p => p.Selected).ToList();
			if (selected.Count == 0) {
				MessageBox.Show(this, "Selecione ao menos um item.", "OpenAI", MessageBoxButton.OK, MessageBoxImage.Information);
				return;
			}

			_buttonSuggestOpenAi.IsEnabled = false;
			_textStatus.Text = "Consultando OpenAI...";

			var worker = new BackgroundWorker();
			worker.DoWork += (s, args) => {
				var errors = new List<string>();
				foreach (var entry in selected) {
					string error;
					if (!OpenAiAccessoryGenerator.TrySuggest(entry, out error))
						errors.Add(entry.SpritePath + ": " + error);
				}
				args.Result = errors;
			};
			worker.RunWorkerCompleted += (s, args) => {
				_buttonSuggestOpenAi.IsEnabled = true;
				var errors = args.Result as List<string>;
				if (errors != null && errors.Count > 0) {
					_textStatus.Text = errors.Count + " erro(s).";
					MessageBox.Show(this, string.Join(Environment.NewLine, errors.Take(8)), "OpenAI", MessageBoxButton.OK, MessageBoxImage.Warning);
				}
				else {
					_textStatus.Text = "Sugestões aplicadas.";
					_gridEntries.Items.Refresh();
				}
			};
			worker.RunWorkerAsync();
		}

		private bool ConfirmViewIdCollisions(IList<CustomAccessoryEntry> selected) {
			var warnings = new List<string>();
			var tables = CustomAccessoryLuaTables.Load(
				_lubLocations.EditAccessoryIdPath,
				_lubLocations.EditAccnamePath);

			foreach (var entry in selected) {
				var existingConstant = tables.FindConstantForViewId(entry.ViewId, entry.ConstantName);
				if (existingConstant != null) {
					warnings.Add(entry.ConstantName + " (viewId " + entry.ViewId + ") já usado por " + existingConstant);
				}
			}

			foreach (var group in selected.GroupBy(p => p.ViewId).Where(g => g.Count() > 1)) {
				var names = string.Join(", ", group.Select(p => p.ConstantName));
				warnings.Add("viewId " + group.Key + " repetido entre: " + names);
			}

			if (warnings.Count == 0)
				return true;

			var message = "Há colisão de viewId com IDs já existentes em accessoryid.lub ou entre os itens selecionados:"
				+ Environment.NewLine + Environment.NewLine
				+ string.Join(Environment.NewLine, warnings.Take(12))
				+ (warnings.Count > 12 ? Environment.NewLine + "... e mais " + (warnings.Count - 12) + "." : "")
				+ Environment.NewLine + Environment.NewLine
				+ "Deseja gravar mesmo assim?";

			return MessageBox.Show(this, message, "Colisão de viewId",
				MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes;
		}

		private void _buttonSave_Click(object sender, RoutedEventArgs e) {
			var selected = _entries.Where(p => p.Selected).ToList();
			if (selected.Count == 0) {
				MessageBox.Show(this, "Nenhum item selecionado.", "Gravar", MessageBoxButton.OK, MessageBoxImage.Information);
				return;
			}

			if (!TryGetInitialViewId(out _, true))
				return;

			CustomAccessoryLuaService.RefreshEntriesFromLuaFiles(selected, _lubLocations);

			if (!ConfirmViewIdCollisions(selected))
				return;

			foreach (var entry in selected) {
				if (string.IsNullOrWhiteSpace(entry.ConstantName)) {
					MessageBox.Show(this, "Há itens sem nome de constante ACCESSORY_.", "Gravar", MessageBoxButton.OK, MessageBoxImage.Warning);
					return;
				}

				if (entry.ViewId <= 0) {
					MessageBox.Show(this, "Há itens com viewId inválido.", "Gravar", MessageBoxButton.OK, MessageBoxImage.Warning);
					return;
				}
			}

			if (!_lubLocations.IsValid) {
				MessageBox.Show(this, _lubLocations.GetMissingFilesMessage(), "Erro ao gravar", MessageBoxButton.OK, MessageBoxImage.Error);
				return;
			}

			try {
				CustomAccessoryLuaService.WriteEntries(selected, _lubLocations, _grf);
				foreach (var entry in selected) {
					entry.Status = CustomAccessoryEntryStatus.Existing;
					entry.IsNew = false;
				}

				UpdateStatusText(selected.Count + " item(ns) gravado(s).");
				MessageBox.Show(this, _lubLocations.GetSuccessMessage(),
					"Sucesso", MessageBoxButton.OK, MessageBoxImage.Information);
				DialogResult = true;
			}
			catch (Exception ex) {
				MessageBox.Show(this, ex.Message, "Erro ao gravar", MessageBoxButton.OK, MessageBoxImage.Error);
			}
		}

		private void _buttonGenerateItemInfo_Click(object sender, RoutedEventArgs e) {
			var selected = _entries.Where(p => p.Selected).ToList();
			if (selected.Count == 0) {
				MessageBox.Show(this, "Selecione ao menos um item.", "Gerar iteminfo", MessageBoxButton.OK, MessageBoxImage.Information);
				return;
			}

			foreach (var entry in selected) {
				if (entry.ViewId <= 0) {
					MessageBox.Show(this, "Há itens selecionados com ViewId inválido. Atribua viewIds antes de gerar iteminfo.", "Gerar iteminfo", MessageBoxButton.OK, MessageBoxImage.Warning);
					return;
				}
			}

			var dialog = new CustomAccessoryItemInfoDialog(selected) { Owner = this };
			WindowProvider.ShowWindow(dialog, this);
		}

		private void _buttonCancel_Click(object sender, RoutedEventArgs e) {
			DialogResult = false;
		}
	}
}
