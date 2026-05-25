using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using GRF.Core;
using GRF.IO;
using GRF.Threading;

namespace GRFEditor.Core.GrfCompare {
	public sealed class GrfCompareService {
		private sealed class GrfFileSnapshot {
			public string RelativePath { get; set; }
			public int DecompressedSize { get; set; }
			public FileEntry Entry { get; set; }
		}

		public GrfCompareResult Compare(
			GrfHolder grfA,
			GrfHolder grfB,
			FileHashService hashService = null,
			IProgress progress = null,
			CancellationToken cancellationToken = default(CancellationToken)) {
			_validateGrf(grfA, "Grf A");
			_validateGrf(grfB, "Grf B");

			var hashCache = hashService ?? new FileHashService();
			var mapA = _buildSnapshotMap(grfA);
			var mapB = _buildSnapshotMap(grfB);

			var result = new GrfCompareResult {
				ComparedAtUtc = DateTime.UtcNow,
				GrfAPath = grfA.FileName ?? "",
				GrfBPath = grfB.FileName ?? "",
			};

			var removedCandidates = new List<GrfFileSnapshot>();
			var addedCandidates = new List<GrfFileSnapshot>();
			int totalSteps = mapA.Count + mapB.Count;
			int processed = 0;

			foreach (string path in mapA.Keys.Except(mapB.Keys, StringComparer.OrdinalIgnoreCase)) {
				ThrowIfCancelled(cancellationToken);
				_reportProgress(progress, ref processed, totalSteps);
				removedCandidates.Add(mapA[path]);
			}

			foreach (string path in mapB.Keys.Except(mapA.Keys, StringComparer.OrdinalIgnoreCase)) {
				ThrowIfCancelled(cancellationToken);
				_reportProgress(progress, ref processed, totalSteps);
				addedCandidates.Add(mapB[path]);
			}

			foreach (string path in mapA.Keys.Intersect(mapB.Keys, StringComparer.OrdinalIgnoreCase)) {
				ThrowIfCancelled(cancellationToken);
				_reportProgress(progress, ref processed, totalSteps);

				var snapA = mapA[path];
				var snapB = mapB[path];

				if (snapA.DecompressedSize != snapB.DecompressedSize) {
					result.Entries.Add(_entrySamePathDifferentContent(snapA, snapB));
					continue;
				}

				string md5A = hashCache.GetDecompressedMd5(grfA, snapA.Entry);
				string md5B = hashCache.GetDecompressedMd5(grfB, snapB.Entry);

				if (String.Equals(md5A, md5B, StringComparison.OrdinalIgnoreCase))
					result.Entries.Add(_entrySame(path, snapA, snapB, md5A));
				else
					result.Entries.Add(_entryModified(path, snapA, snapB, md5A, md5B));
			}

			_pairBySizeAndHash(grfA, grfB, hashCache, removedCandidates, addedCandidates, result, progress, ref processed, totalSteps);

			foreach (var snap in removedCandidates.OrderBy(s => s.RelativePath, StringComparer.OrdinalIgnoreCase))
				result.Entries.Add(_entryRemoved(snap));

			foreach (var snap in addedCandidates.OrderBy(s => s.RelativePath, StringComparer.OrdinalIgnoreCase))
				result.Entries.Add(_entryAdded(snap));

			result.Entries = result.Entries
				.OrderBy(e => e.RelativePath ?? "", StringComparer.OrdinalIgnoreCase)
				.ThenBy(e => e.Status)
				.ToList();

			_reportProgress(progress, ref processed, totalSteps, forceComplete: true);
			return result;
		}

		public static int GetDecompressedSize(FileEntry entry) {
			return FileHashService._decompressedSize(entry);
		}

		private static void _pairBySizeAndHash(
			GrfHolder grfA,
			GrfHolder grfB,
			FileHashService hashCache,
			List<GrfFileSnapshot> removedCandidates,
			List<GrfFileSnapshot> addedCandidates,
			GrfCompareResult result,
			IProgress progress,
			ref int processed,
			int totalSteps) {
			var removedBySize = _groupBySize(removedCandidates);
			var addedBySize = _groupBySize(addedCandidates);

			foreach (int size in removedBySize.Keys.Intersect(addedBySize.Keys)) {
				var removedList = removedBySize[size];
				var addedList = addedBySize[size];
				int ri = 0;

				while (ri < removedList.Count && addedList.Count > 0) {
					var snapA = removedList[ri];
					string md5A = hashCache.GetDecompressedMd5(grfA, snapA.Entry);
					int matchIndex = -1;

					for (int ai = 0; ai < addedList.Count; ai++) {
						var snapB = addedList[ai];

						if (String.Equals(snapA.RelativePath, snapB.RelativePath, StringComparison.OrdinalIgnoreCase))
							continue;

						string md5B = hashCache.GetDecompressedMd5(grfB, snapB.Entry);

						if (String.Equals(md5A, md5B, StringComparison.OrdinalIgnoreCase)) {
							matchIndex = ai;
							break;
						}
					}

					_reportProgress(progress, ref processed, totalSteps);

					if (matchIndex < 0) {
						ri++;
						continue;
					}

					var matchedB = addedList[matchIndex];
					result.Entries.Add(_entrySameContentDifferentPath(snapA, matchedB, md5A));
					removedCandidates.Remove(snapA);
					addedCandidates.Remove(matchedB);
					removedList.RemoveAt(ri);
					addedList.RemoveAt(matchIndex);
				}
			}
		}

		private static Dictionary<int, List<GrfFileSnapshot>> _groupBySize(List<GrfFileSnapshot> snapshots) {
			var groups = new Dictionary<int, List<GrfFileSnapshot>>();

			foreach (var snap in snapshots) {
				if (!groups.ContainsKey(snap.DecompressedSize))
					groups[snap.DecompressedSize] = new List<GrfFileSnapshot>();

				groups[snap.DecompressedSize].Add(snap);
			}

			foreach (var list in groups.Values)
				list.Sort((a, b) => StringComparer.OrdinalIgnoreCase.Compare(a.RelativePath, b.RelativePath));

			return groups;
		}

		private static Dictionary<string, GrfFileSnapshot> _buildSnapshotMap(GrfHolder grf) {
			var map = new Dictionary<string, GrfFileSnapshot>(StringComparer.OrdinalIgnoreCase);

			foreach (var entry in grf.FileTable.Entries) {
				if (entry.IsRemoved)
					continue;

				string path = GrfPath.CleanGrfPath(entry.RelativePath);

				if (String.IsNullOrWhiteSpace(path))
					continue;

				map[path] = new GrfFileSnapshot {
					RelativePath = path,
					DecompressedSize = GetDecompressedSize(entry),
					Entry = entry,
				};
			}

			return map;
		}

		private static void _validateGrf(GrfHolder grf, string label) {
			if (grf == null)
				throw new ArgumentNullException(nameof(grf));

			if (!grf.IsOpened)
				throw new InvalidOperationException(label + " must be an opened GRF.");
		}

		private static void _reportProgress(IProgress progress, ref int processed, int total, bool forceComplete = false) {
			if (progress == null)
				return;

			if (!forceComplete)
				processed++;

			if (total <= 0) {
				progress.Progress = forceComplete ? 100f : 0f;
				return;
			}

			progress.Progress = forceComplete
				? 100f
				: Math.Min(99f, processed / (float) total * 100f);
		}

		private static void ThrowIfCancelled(CancellationToken cancellationToken) {
			if (cancellationToken.IsCancellationRequested)
				throw new OperationCanceledException();
		}

		private static GrfCompareEntry _entrySame(string path, GrfFileSnapshot a, GrfFileSnapshot b, string md5) {
			return new GrfCompareEntry {
				Status = GrfCompareStatus.Same,
				RelativePath = path,
				PathInA = a.RelativePath,
				PathInB = b.RelativePath,
				SizeDecompressedA = a.DecompressedSize,
				SizeDecompressedB = b.DecompressedSize,
				Md5A = md5,
				Md5B = md5,
			};
		}

		private static GrfCompareEntry _entryModified(string path, GrfFileSnapshot a, GrfFileSnapshot b, string md5A, string md5B) {
			return new GrfCompareEntry {
				Status = GrfCompareStatus.Modified,
				RelativePath = path,
				PathInA = a.RelativePath,
				PathInB = b.RelativePath,
				SizeDecompressedA = a.DecompressedSize,
				SizeDecompressedB = b.DecompressedSize,
				Md5A = md5A,
				Md5B = md5B,
			};
		}

		private static GrfCompareEntry _entrySamePathDifferentContent(GrfFileSnapshot a, GrfFileSnapshot b) {
			return new GrfCompareEntry {
				Status = GrfCompareStatus.SamePathDifferentContent,
				RelativePath = a.RelativePath,
				PathInA = a.RelativePath,
				PathInB = b.RelativePath,
				SizeDecompressedA = a.DecompressedSize,
				SizeDecompressedB = b.DecompressedSize,
			};
		}

		private static GrfCompareEntry _entrySameContentDifferentPath(GrfFileSnapshot a, GrfFileSnapshot b, string md5) {
			return new GrfCompareEntry {
				Status = GrfCompareStatus.SameContentDifferentPath,
				RelativePath = a.RelativePath,
				PathInA = a.RelativePath,
				PathInB = b.RelativePath,
				SizeDecompressedA = a.DecompressedSize,
				SizeDecompressedB = b.DecompressedSize,
				Md5A = md5,
				Md5B = md5,
			};
		}

		private static GrfCompareEntry _entryRemoved(GrfFileSnapshot snap) {
			return new GrfCompareEntry {
				Status = GrfCompareStatus.Removed,
				RelativePath = snap.RelativePath,
				PathInA = snap.RelativePath,
				SizeDecompressedA = snap.DecompressedSize,
			};
		}

		private static GrfCompareEntry _entryAdded(GrfFileSnapshot snap) {
			return new GrfCompareEntry {
				Status = GrfCompareStatus.Added,
				RelativePath = snap.RelativePath,
				PathInB = snap.RelativePath,
				SizeDecompressedB = snap.DecompressedSize,
			};
		}
	}
}
