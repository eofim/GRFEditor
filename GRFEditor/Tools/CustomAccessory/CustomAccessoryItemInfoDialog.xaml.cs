using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using GRFEditor.ApplicationConfiguration;
using TokeiLibrary.WPF.Styles;

namespace GRFEditor.Tools.CustomAccessory {
	public partial class CustomAccessoryItemInfoDialog : TkWindow {
		private readonly ObservableCollection<CustomAccessoryItemInfoRow> _rows = new ObservableCollection<CustomAccessoryItemInfoRow>();

		public CustomAccessoryItemInfoDialog(IList<CustomAccessoryEntry> entries)
			: base("Gerar iteminfo", "settings.ico", SizeToContent.Manual, ResizeMode.CanResize) {
			InitializeComponent();

			if (entries != null) {
				foreach (var entry in entries)
					_rows.Add(CustomAccessoryItemInfoRow.FromEntry(entry));
			}

			_gridEntries.ItemsSource = _rows;
			_gridEntries.CellEditEnding += (s, ev) => {
				var row = ev.Row.Item as CustomAccessoryItemInfoRow;
				if (row != null) {
					row.RefreshPreview();
					_gridEntries.Items.Refresh();
				}
			};

			var lastFolder = GrfEditorConfiguration.CustomAccessoryItemInfoOutputPath;
			if (!string.IsNullOrEmpty(lastFolder) && Directory.Exists(lastFolder))
				_textOutputFolder.Text = lastFolder;

			_textStatus.Text = _rows.Count + " item(ns).";
		}

		private void _buttonBrowseFolder_Click(object sender, RoutedEventArgs e) {
			var folder = PathRequest.FolderEditor();
			if (!string.IsNullOrEmpty(folder))
				_textOutputFolder.Text = folder;
		}

		private void _buttonAutoItemIds_Click(object sender, RoutedEventArgs e) {
			int? startId;
			if (!TryGetInitialItemId(out startId, true))
				return;

			var nextId = startId ?? 35000;
			foreach (var row in _rows.Where(p => p.Selected).OrderBy(p => p.Entry.ConstantName)) {
				row.ItemId = nextId++;
				row.RefreshPreview();
			}

			_gridEntries.Items.Refresh();
			_textStatus.Text = "ItemIds atribuídos" + (startId.HasValue ? " a partir de " + startId.Value : "") + ".";
		}

		private bool TryGetInitialItemId(out int? startItemId, bool showError = false) {
			startItemId = null;
			var text = _textInitialItemId != null ? _textInitialItemId.Text.Trim() : "";

			if (string.IsNullOrEmpty(text))
				return true;

			int parsed;
			if (!int.TryParse(text, out parsed) || parsed <= 0) {
				if (showError) {
					MessageBox.Show(this,
						"ItemId inicial deve ser um inteiro positivo.",
						"ItemId inicial",
						MessageBoxButton.OK,
						MessageBoxImage.Warning);
				}

				return false;
			}

			startItemId = parsed;
			return true;
		}

		private void _buttonGenerate_Click(object sender, RoutedEventArgs e) {
			var selected = _rows.Where(p => p.Selected).Select(p => p.Entry).ToList();
			if (selected.Count == 0) {
				MessageBox.Show(this, "Selecione ao menos um item.", "Gerar iteminfo", MessageBoxButton.OK, MessageBoxImage.Information);
				return;
			}

			if (!TryGetInitialItemId(out _, true))
				return;

			foreach (var row in _rows.Where(p => p.Selected))
				row.SyncToEntry();

			foreach (var entry in selected) {
				if (entry.ViewId <= 0) {
					MessageBox.Show(this, "Há itens com ViewId inválido.", "Gerar iteminfo", MessageBoxButton.OK, MessageBoxImage.Warning);
					return;
				}

				if (entry.ItemId <= 0) {
					MessageBox.Show(this, "Há itens com ItemId inválido.", "Gerar iteminfo", MessageBoxButton.OK, MessageBoxImage.Warning);
					return;
				}
			}

			foreach (var group in selected.GroupBy(p => p.ItemId).Where(g => g.Count() > 1)) {
				MessageBox.Show(this,
					"ItemId " + group.Key + " repetido entre: " + string.Join(", ", group.Select(p => p.ConstantName)),
					"Gerar iteminfo",
					MessageBoxButton.OK,
					MessageBoxImage.Warning);
				return;
			}

			var folder = _textOutputFolder.Text.Trim();
			if (string.IsNullOrEmpty(folder)) {
				MessageBox.Show(this, "Selecione a pasta de destino.", "Gerar iteminfo", MessageBoxButton.OK, MessageBoxImage.Warning);
				return;
			}

			try {
				CustomAccessoryItemInfoGenerator.WriteFiles(folder, selected);
				GrfEditorConfiguration.CustomAccessoryItemInfoOutputPath = folder;

				var itemInfoPath = Path.Combine(folder, "iteminfo.lua");
				var itemDbPath = Path.Combine(folder, "item_db.yml");

				MessageBox.Show(this,
					"Arquivos gerados:" + Environment.NewLine
					+ itemInfoPath + Environment.NewLine
					+ itemDbPath,
					"Sucesso",
					MessageBoxButton.OK,
					MessageBoxImage.Information);

				_textStatus.Text = selected.Count + " item(ns) gerado(s).";
			}
			catch (Exception ex) {
				MessageBox.Show(this, ex.Message, "Erro ao gerar", MessageBoxButton.OK, MessageBoxImage.Error);
			}
		}

		private void _buttonClose_Click(object sender, RoutedEventArgs e) {
			Close();
		}

		public sealed class CustomAccessoryItemInfoRow {
			public CustomAccessoryEntry Entry { get; private set; }
			public bool Selected { get; set; }
			public string SpritePath { get; private set; }
			public string DisplayName { get; private set; }
			public int ViewId { get; private set; }

			public int ItemId {
				get { return Entry.ItemId; }
				set {
					Entry.ItemId = value;
					RefreshPreview();
				}
			}

			public string AegisPreview { get; private set; }

			public static CustomAccessoryItemInfoRow FromEntry(CustomAccessoryEntry entry) {
				var row = new CustomAccessoryItemInfoRow {
					Entry = entry,
					Selected = entry.Selected,
					SpritePath = entry.SpritePath,
					DisplayName = entry.DisplayName,
					ViewId = entry.ViewId,
				};
				row.RefreshPreview();
				return row;
			}

			public void SyncToEntry() {
				Entry.Selected = Selected;
			}

			public void RefreshPreview() {
				string aegisName;
				string displayName;
				string resourceName;
				CustomAccessoryItemInfoNaming.GetItemNames(Entry, out aegisName, out displayName, out resourceName);
				AegisPreview = aegisName + " / " + resourceName;
			}
		}
	}
}
