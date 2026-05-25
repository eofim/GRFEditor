using System;
using System.Collections.Generic;
using GRF.Core;
using GRF.IO;
using Utilities.Hash;

namespace GRFEditor.Core.GrfCompare {
	/// <summary>
	/// Caches MD5 hashes of decompressed GRF file entries.
	/// </summary>
	public sealed class FileHashService {
		private readonly Dictionary<string, string> _cache =
			new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		private readonly Md5Hash _md5 = new Md5Hash();

		public void Clear() {
			_cache.Clear();
		}

		public string GetDecompressedMd5(GrfHolder grf, FileEntry entry) {
			if (entry == null)
				throw new ArgumentNullException(nameof(entry));

			string cacheKey = _buildCacheKey(grf, entry);
			string cached;

			if (_cache.TryGetValue(cacheKey, out cached))
				return cached;

			string hash = _md5.ComputeHash(entry.GetDecompressedData());
			_cache[cacheKey] = hash;
			return hash;
		}

		public bool TryGetCached(GrfHolder grf, FileEntry entry, out string md5) {
			md5 = null;

			if (entry == null)
				return false;

			return _cache.TryGetValue(_buildCacheKey(grf, entry), out md5);
		}

		internal static int _decompressedSize(FileEntry entry) {
			if (entry == null)
				return 0;

			if (entry.NewSizeDecompressed > 0)
				return entry.NewSizeDecompressed;

			return entry.SizeDecompressed;
		}

		private static string _buildCacheKey(GrfHolder grf, FileEntry entry) {
			string grfId = grf?.FileName;

			if (String.IsNullOrEmpty(grfId))
				grfId = "grf:" + (grf?.GetHashCode() ?? 0);

			string path = GrfPath.CleanGrfPath(entry.RelativePath);
			int size = _decompressedSize(entry);
			return grfId + "|" + path + "|" + size;
		}
	}
}
