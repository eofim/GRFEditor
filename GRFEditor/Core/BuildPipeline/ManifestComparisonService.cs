using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;

namespace GRFEditor.Core.BuildPipeline {
	[DataContract]
	internal sealed class BuildChangelogPathMove {
		[DataMember] public string PreviousPath { get; set; }
		[DataMember] public string CurrentPath { get; set; }
		[DataMember] public string Md5 { get; set; }
		[DataMember] public string Sha1 { get; set; }
	}

	[DataContract]
	internal sealed class BuildChangelogDocument {
		[DataMember] public string GrfFileName { get; set; }
		[DataMember] public DateTime GeneratedAtUtc { get; set; }
		[DataMember] public bool HasPreviousManifest { get; set; }
		[DataMember] public string PreviousManifestPath { get; set; }
		[DataMember] public string CurrentManifestPath { get; set; }
		[DataMember] public List<string> Added { get; set; }
		[DataMember] public List<string> Removed { get; set; }
		[DataMember] public List<string> Changed { get; set; }
		[DataMember] public List<string> Unchanged { get; set; }
		[DataMember] public List<BuildChangelogPathMove> SameHashDifferentPath { get; set; }
	}

	internal static class ManifestComparisonService {
		public static BuildManifestDocument LoadFromFile(string filePath) {
			if (String.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
				return null;

			try {
				string json = File.ReadAllText(filePath, Encoding.UTF8);
				return BuildPipelineJson.Read<BuildManifestDocument>(json);
			}
			catch {
				return null;
			}
		}

		public static BuildChangelogDocument Compare(
			BuildManifestDocument current,
			BuildManifestDocument previous,
			string previousManifestPath = null,
			string currentManifestPath = null) {
			var changelog = new BuildChangelogDocument {
				GrfFileName = current?.GrfFileName ?? previous?.GrfFileName ?? "",
				GeneratedAtUtc = DateTime.UtcNow,
				HasPreviousManifest = previous != null,
				PreviousManifestPath = previousManifestPath ?? "",
				CurrentManifestPath = currentManifestPath ?? "",
				Added = new List<string>(),
				Removed = new List<string>(),
				Changed = new List<string>(),
				Unchanged = new List<string>(),
				SameHashDifferentPath = new List<BuildChangelogPathMove>(),
			};

			if (current == null || previous == null)
				return changelog;

			var prevMap = _toPathMap(previous.Files);
			var currMap = _toPathMap(current.Files);

			var addedCandidates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			var removedCandidates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

			foreach (string path in currMap.Keys.Except(prevMap.Keys, StringComparer.OrdinalIgnoreCase))
				addedCandidates.Add(path);

			foreach (string path in prevMap.Keys.Except(currMap.Keys, StringComparer.OrdinalIgnoreCase))
				removedCandidates.Add(path);

			foreach (string path in currMap.Keys.Intersect(prevMap.Keys, StringComparer.OrdinalIgnoreCase)) {
				if (_entriesEqual(prevMap[path], currMap[path]))
					changelog.Unchanged.Add(path);
				else
					changelog.Changed.Add(path);
			}

			_reclassifyMovesByHash(prevMap, currMap, addedCandidates, removedCandidates, changelog);

			changelog.Added = addedCandidates.OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToList();
			changelog.Removed = removedCandidates.OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToList();
			changelog.Unchanged.Sort(StringComparer.OrdinalIgnoreCase);
			changelog.Changed.Sort(StringComparer.OrdinalIgnoreCase);
			changelog.SameHashDifferentPath = changelog.SameHashDifferentPath
				.OrderBy(m => m.PreviousPath, StringComparer.OrdinalIgnoreCase)
				.ToList();

			return changelog;
		}

		private static Dictionary<string, BuildManifestEntry> _toPathMap(List<BuildManifestEntry> files) {
			var map = new Dictionary<string, BuildManifestEntry>(StringComparer.OrdinalIgnoreCase);

			if (files == null)
				return map;

			foreach (var file in files) {
				if (String.IsNullOrWhiteSpace(file?.RelativePath))
					continue;

				map[file.RelativePath] = file;
			}

			return map;
		}

		private static bool _entriesEqual(BuildManifestEntry a, BuildManifestEntry b) {
			if (a == null || b == null)
				return false;

			return a.Size == b.Size
				&& a.CompressedSize == b.CompressedSize
				&& String.Equals(a.Extension ?? "", b.Extension ?? "", StringComparison.OrdinalIgnoreCase)
				&& String.Equals(a.Md5 ?? "", b.Md5 ?? "", StringComparison.OrdinalIgnoreCase)
				&& String.Equals(a.Sha1 ?? "", b.Sha1 ?? "", StringComparison.OrdinalIgnoreCase);
		}

		private static void _reclassifyMovesByHash(
			IDictionary<string, BuildManifestEntry> prevMap,
			IDictionary<string, BuildManifestEntry> currMap,
			HashSet<string> addedCandidates,
			HashSet<string> removedCandidates,
			BuildChangelogDocument changelog) {
			var removedByHash = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
			var addedByHash = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

			foreach (string path in removedCandidates) {
				BuildManifestEntry entry;
				if (!prevMap.TryGetValue(path, out entry))
					continue;

				string hashKey = _hashKey(entry);
				if (hashKey == null)
					continue;

				if (!removedByHash.ContainsKey(hashKey))
					removedByHash[hashKey] = new List<string>();

				removedByHash[hashKey].Add(path);
			}

			foreach (string path in addedCandidates) {
				BuildManifestEntry entry;
				if (!currMap.TryGetValue(path, out entry))
					continue;

				string hashKey = _hashKey(entry);
				if (hashKey == null)
					continue;

				if (!addedByHash.ContainsKey(hashKey))
					addedByHash[hashKey] = new List<string>();

				addedByHash[hashKey].Add(path);
			}

			foreach (string hashKey in removedByHash.Keys.Intersect(addedByHash.Keys, StringComparer.OrdinalIgnoreCase)) {
				var prevPaths = new List<string>(removedByHash[hashKey]);
				var currPaths = new List<string>(addedByHash[hashKey]);

				while (prevPaths.Count > 0 && currPaths.Count > 0) {
					string prevPath = prevPaths[0];
					string currPath = currPaths.FirstOrDefault(c =>
						!String.Equals(c, prevPath, StringComparison.OrdinalIgnoreCase)) ?? currPaths[0];

					if (String.Equals(prevPath, currPath, StringComparison.OrdinalIgnoreCase)) {
						prevPaths.RemoveAt(0);
						currPaths.Remove(currPath);
						continue;
					}

					BuildManifestEntry prevEntry = prevMap[prevPath];
					changelog.SameHashDifferentPath.Add(new BuildChangelogPathMove {
						PreviousPath = prevPath,
						CurrentPath = currPath,
						Md5 = prevEntry.Md5,
						Sha1 = prevEntry.Sha1,
					});

					removedCandidates.Remove(prevPath);
					addedCandidates.Remove(currPath);
					prevPaths.RemoveAt(0);
					currPaths.Remove(currPath);
				}
			}
		}

		private static string _hashKey(BuildManifestEntry entry) {
			if (entry == null)
				return null;

			if (!String.IsNullOrWhiteSpace(entry.Md5))
				return "md5:" + entry.Md5.Trim().ToLowerInvariant();

			if (!String.IsNullOrWhiteSpace(entry.Sha1))
				return "sha1:" + entry.Sha1.Trim().ToLowerInvariant();

			return null;
		}
	}
}
