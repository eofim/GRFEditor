using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using GRF.Core;
using TokeiLibrary.WPF.Styles;

namespace GRFEditor.Tools.MapExtractor {
	/// <summary>
	/// Interaction logic for MapExtractorDialog.xaml
	/// </summary>
	public partial class MapExtractorDialog : TkWindow {
		private readonly MapExtractor _mapExtractor;
		private readonly Dictionary<string, GrfHolder> _openedGrfs = new Dictionary<string, GrfHolder>();

		public MapExtractorDialog(GrfHolder grf, string fileName) : this(grf, new[] { fileName }) {
		}

		public MapExtractorDialog(GrfHolder grf, IList<string> fileNames) : base(_getWindowTitle(fileNames), "mapEditor.ico", SizeToContent.Manual, ResizeMode.CanResize) {
			InitializeComponent();

			var normalized = MapExtractor.NormalizeMapFileSelections(fileNames);

			if (normalized.Count == 0)
				normalized = fileNames.ToList();

			_mapExtractor = new MapExtractor(grf, normalized[0]);
			_gridMapExtractor.Children.Add(_mapExtractor);
			_mapExtractor.ReloadMaps(grf, normalized, () => false);

			if (normalized.Count > 3)
				Width = 800;
		}

		private static string _getWindowTitle(IList<string> fileNames) {
			var normalized = MapExtractor.NormalizeMapFileSelections(fileNames);

			if (normalized.Count <= 1) {
				string name = normalized.Count == 1 ? Path.GetFileNameWithoutExtension(normalized[0]) : "map";
				return "Export map files - " + name;
			}

			return "Export map files (" + normalized.Count + " maps)";
		}

		protected override void OnClosing(CancelEventArgs e) {
			_mapExtractor.AsyncOperation.Cancel();

			foreach (GrfHolder grf in _openedGrfs.Values) {
				grf.Close();
			}

			base.OnClosing(e);
		}

		private void _buttonExport_Click(object sender, RoutedEventArgs e) {
			_mapExtractor.Export();
		}

		private void _buttonExportAt_Click(object sender, RoutedEventArgs e) {
			_mapExtractor.ExportAt();
		}
	}
}