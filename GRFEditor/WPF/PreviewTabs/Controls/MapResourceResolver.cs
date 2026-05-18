using GRF;
using GRF.FileFormats.GndFormat;
using GRF.Threading;
using GRF.FileFormats.LubFormat;
using GRF.FileFormats.RsmFormat;
using GRF.FileFormats.RswFormat;
using GRF.FileFormats.StrFormat;
using GRF.IO;
using GRFEditor.ApplicationConfiguration;
using Lua;
using Lua.Structure;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Utilities.Extension;
using Utilities.Services;

namespace GRFEditor.WPF.PreviewTabs.Controls {
	public class MapResourcePath {
		public string ParentDirectory;
		public string NodeDisplayPath;
		public string RelativePath;

		public MapResourcePath(string parentDirectory, string nodeDisplayPath) {
			ParentDirectory = parentDirectory;
			NodeDisplayPath = nodeDisplayPath;
			RelativePath = GrfPath.Combine(ParentDirectory, NodeDisplayPath);
		}
	}

	public class MapResourceCollector {
		public static readonly string[] SupportedExtensions = { ".rsm", ".rsm2", ".gat", ".rsw", ".gnd", ".str" };

		public static bool IsSupportedFile(string relativePath) {
			return SupportedExtensions.Any(p => relativePath.GetExtension() == p);
		}

		public static List<string> NormalizeMapFileSelections(IEnumerable<string> relativePaths) {
			var preferred = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
			var priority = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) {
				{ ".rsw", 0 },
				{ ".gnd", 1 },
				{ ".gat", 2 },
				{ ".str", 3 },
				{ ".rsm", 4 },
				{ ".rsm2", 5 },
			};

			foreach (string relativePath in relativePaths) {
				if (!IsSupportedFile(relativePath))
					continue;

				string key = _getMapDedupKey(relativePath);
				string ext = relativePath.GetExtension();
				int rank = priority.ContainsKey(ext) ? priority[ext] : 99;

				if (!preferred.ContainsKey(key)) {
					preferred[key] = relativePath;
				}
				else {
					string existingExt = preferred[key].GetExtension();
					int existingRank = priority.ContainsKey(existingExt) ? priority[existingExt] : 99;

					if (rank < existingRank)
						preferred[key] = relativePath;
				}
			}

			return preferred.Values.OrderBy(p => Path.GetFileNameWithoutExtension(p), StringComparer.OrdinalIgnoreCase).ToList();
		}

		private static string _getMapDedupKey(string relativePath) {
			string ext = relativePath.GetExtension();

			if (ext == ".rsm" || ext == ".rsm2" || ext == ".str")
				return relativePath;

			string mapName = Path.GetFileNameWithoutExtension(relativePath);
			string directory = Path.GetDirectoryName(relativePath);

			if (String.IsNullOrEmpty(directory))
				return mapName;

			return directory + "\\" + mapName;
		}

		public static List<MapResourcePath> GetMapRootPaths(string fileName) {
			string grfPath = Path.GetDirectoryName(fileName);
			string mapFile = Path.GetFileNameWithoutExtension(fileName);
			var roots = new List<MapResourcePath>();

			if (fileName.IsExtension(".rsm")) {
				roots.Add(new MapResourcePath(grfPath, mapFile + ".rsm"));
			}
			else if (fileName.IsExtension(".rsm2")) {
				roots.Add(new MapResourcePath(grfPath, mapFile + ".rsm2"));
			}
			else if (fileName.IsExtension(".str")) {
				roots.Add(new MapResourcePath(grfPath, mapFile + ".str"));
			}
			else {
				roots.Add(new MapResourcePath(@"data\", mapFile + ".gnd"));
				roots.Add(new MapResourcePath(@"data\", mapFile + ".rsw"));
				roots.Add(new MapResourcePath(@"data\", mapFile + ".gat"));

				if (GrfEditorConfiguration.Resources.MultiGrf.Exists(@"data\luafiles514\lua files\effecttool\" + mapFile + ".lub")) {
					roots.Add(new MapResourcePath(@"data\luafiles514\lua files\effecttool\", mapFile + ".lub"));
				}
			}

			return roots;
		}

		public static HashSet<string> CollectRelativePaths(IEnumerable<string> mapFileNames, IProgress progress = null, Func<bool> cancelToken = null) {
			var resolver = new MapResourceResolver();
			var normalized = NormalizeMapFileSelections(mapFileNames);
			var collected = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

			for (int mapIndex = 0; mapIndex < normalized.Count; mapIndex++) {
				if (cancelToken != null && cancelToken())
					break;

				foreach (MapResourcePath root in GetMapRootPaths(normalized[mapIndex])) {
					_collectRecursive(resolver, root, collected, visited, cancelToken);
				}

				if (progress != null && normalized.Count > 0)
					progress.Progress = (float)(mapIndex + 1) / normalized.Count * 50f;
			}

			return collected;
		}

		private static void _collectRecursive(MapResourceResolver resolver, MapResourcePath resource, HashSet<string> collected, HashSet<string> visited, Func<bool> cancelToken) {
			if (cancelToken != null && cancelToken())
				return;

			string relativePath = resource.RelativePath;

			if (!visited.Add(relativePath))
				return;

			var multiGrf = GrfEditorConfiguration.Resources.MultiGrf;

			if (!multiGrf.Exists(relativePath))
				return;

			collected.Add(relativePath);

			try {
				foreach (MapResourcePath child in resolver.GetMapResources(resource)) {
					_collectRecursive(resolver, child, collected, visited, cancelToken);
				}
			}
			catch {
			}
		}
	}

	public class MapResourceResolver {
		public List<MapResourcePath> GetMapResources(MapResourcePath resource) {
			List<MapResourcePath> resources = new List<MapResourcePath>();
			string grfPath = resource.RelativePath;

			switch (resource.NodeDisplayPath.GetExtension()) {
				case ".rsm":
				case ".rsm2":
					_addRsmResources(resources, grfPath);
					break;
				case ".gat":
					// Attempt to read file
					if (GrfEditorConfiguration.Resources.MultiGrf.GetData(grfPath) == null) {
						throw new Exception("Cannot read the file. It is either encrypted or corrupted.");
					}
					break;
				case ".gnd":
					_addGndResources(resources, grfPath);
					break;
				case ".rsw":
					_addRswResources(resources, grfPath);
					break;
				case ".str":
					_addStrResources(resources, resource.ParentDirectory, grfPath);
					break;
				case ".lub":
					_addLubEffectToolResources(resources, resource.ParentDirectory, grfPath);
					break;
			}

			return resources;
		}

		private void _addStrResources(List<MapResourcePath> resources, string parentDirectory, string grfPath) {
			Str str = new Str(GrfEditorConfiguration.Resources.MultiGrf.GetData(grfPath));

			foreach (string texture in str.Textures) {
				resources.Add(new MapResourcePath(parentDirectory, texture));
			}
		}

		private void _addLubEffectToolResources(List<MapResourcePath> resources, string parentDirectory, string grfPath) {
			var lubData = GrfEditorConfiguration.Resources.MultiGrf.GetData(grfPath);
			
			if (lubData == null)
				throw new Exception("Cannot read the file. It is either encrypted or corrupted.");

			if (Lub.IsCompiled(lubData)) {
				Lub lub = new Lub(lubData);
				var text = lub.Decompile();
				lubData = EncodingService.DisplayEncoding.GetBytes(text);
			}

			SimplifiedLuaElement lua;

			using (LuaReader reader = new LuaReader(new MemoryStream(lubData))) {
				lua = reader.ReadSimplified();
			}

			HashSet<string> textures = new HashSet<string>();

			// Look through emitter
			foreach (var entry in lua.KeyValues) {
				if (entry.Key.Contains("_emitterInfo")) {
					foreach (var emitterEntry in entry.Value.KeyValues) {
						var lubEffect = emitterEntry.Value.KeyValues;

						if (lubEffect.ContainsKey("texture")) {
							textures.Add(lubEffect["texture"].Value.Trim('\"', '[', ']').Replace("\\\\", "\\"));
						}	
					}
				}
			}

			foreach (string texture in textures) {
				resources.Add(new MapResourcePath(@"data\texture\", texture));
			}
		}

		private void _addRswResources(List<MapResourcePath> resources, string grfPath) {
			Rsw rsw = new Rsw(GrfEditorConfiguration.Resources.MultiGrf.GetData(grfPath));

			foreach (string model in rsw.ModelResources.Distinct()) {
				resources.Add(new MapResourcePath(@"data\model\", model));
			}
		}

		private void _addGndResources(List<MapResourcePath> resources, string grfPath) {
			var dataEntry = ((MultiType)GrfEditorConfiguration.Resources.MultiGrf.GetData(grfPath)).GetByteReader();
			GndHeader gndHeader = new GndHeader(dataEntry);

			for (int i = 0; i < gndHeader.TextureCount; i++) {
				resources.Add(new MapResourcePath(@"data\texture\", dataEntry.String(gndHeader.TexturePathSize, '\0')));
			}
		}

		private void _addRsmResources(List<MapResourcePath> resources, string grfPath) {
			var byteData = GrfEditorConfiguration.Resources.MultiGrf.GetData(grfPath);

			var binaryReader = ((MultiType)byteData).GetBinaryReader();
			RsmHeader rsmHeader = new RsmHeader(binaryReader);
			List<string> textures = new List<string>();

			if (rsmHeader.Version < 2.0) {
				binaryReader.Forward(8);

				if (rsmHeader.Version >= 1.4) {
					binaryReader.Forward(1);
				}

				binaryReader.Forward(16);
				var count = binaryReader.Int32();

				for (int i = 0; i < count; i++) {
					textures.Add(binaryReader.String(40, '\0'));
				}
			}
			else {
				binaryReader.Position = 0;
				Rsm rsm2 = new Rsm(binaryReader);

				textures.AddRange(rsm2.Textures);

				foreach (var mesh in rsm2.Meshes) {
					textures.AddRange(mesh.Textures);
				}

				textures.Distinct();
			}

			foreach (string texture in textures) {
				resources.Add(new MapResourcePath(@"data\texture\", texture));
			}
		}
	}
}
