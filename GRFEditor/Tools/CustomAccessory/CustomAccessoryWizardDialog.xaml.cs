using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Windows;
using GRF.Core;
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

			foreach (var entry in CustomAccessoryLuaService.BuildEntriesFromSprites(paths, _tables))
				_entries.Add(entry);

			_gridEntries.ItemsSource = _entries;

			if (!_lubLocations.IsValid) {
				_textStatus.Text = _lubLocations.GetMissingFilesMessage();
				MessageBox.Show(this, _lubLocations.GetMissingFilesMessage(), "Arquivos Lua", MessageBoxButton.OK, MessageBoxImage.Warning);
			}
			else {
				_textStatus.Text = _entries.Count + " item(ns) carregado(s). " + _lubLocations.GetSourceDescription();
			}
		}

		private void _buttonAutoViewIds_Click(object sender, RoutedEventArgs e) {
			var nextId = _tables.GetNextViewId();
			foreach (var entry in _entries.Where(p => p.Selected && p.IsNew).OrderBy(p => p.ConstantName)) {
				entry.ViewId = nextId++;
			}

			_textStatus.Text = "ViewIds atribuídos para itens novos selecionados.";
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

		private void _buttonSave_Click(object sender, RoutedEventArgs e) {
			var selected = _entries.Where(p => p.Selected).ToList();
			if (selected.Count == 0) {
				MessageBox.Show(this, "Nenhum item selecionado.", "Gravar", MessageBoxButton.OK, MessageBoxImage.Information);
				return;
			}

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
				foreach (var entry in selected)
					entry.IsNew = false;

				_textStatus.Text = selected.Count + " item(ns) gravado(s).";
				MessageBox.Show(this, _lubLocations.GetSuccessMessage(),
					"Sucesso", MessageBoxButton.OK, MessageBoxImage.Information);
				DialogResult = true;
			}
			catch (Exception ex) {
				MessageBox.Show(this, ex.Message, "Erro ao gravar", MessageBoxButton.OK, MessageBoxImage.Error);
			}
		}

		private void _buttonCancel_Click(object sender, RoutedEventArgs e) {
			DialogResult = false;
		}
	}
}
