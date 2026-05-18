using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GRF.Core;
using GRFEditor.ApplicationConfiguration;

namespace GRFEditor.Tools.CustomAccessory {
	public static class CustomAccessoryLuaService {
		public static List<CustomAccessoryEntry> BuildEntriesFromSprites(IEnumerable<string> spritePaths, CustomAccessoryLuaTables tables) {
			var entries = new List<CustomAccessoryEntry>();
			var nextId = tables.GetNextViewId();

			foreach (var spritePath in spritePaths.Distinct(StringComparer.OrdinalIgnoreCase)) {
				var fileName = Path.GetFileNameWithoutExtension(spritePath);
				string constantName;
				string displayName;
				CustomAccessoryNaming.FromSpriteFileName(fileName, out constantName, out displayName);

				var entry = new CustomAccessoryEntry {
					SpritePath = spritePath,
					ConstantName = constantName,
					DisplayName = displayName,
					ViewId = nextId,
					IsNew = !tables.HasConstant(constantName),
					Selected = true,
				};

				int existingId;
				if (tables.AccessoryIds.TryGetValue(constantName, out existingId)) {
					entry.ViewId = existingId;
					entry.IsNew = false;
				}

				entries.Add(entry);
				if (entry.IsNew)
					nextId++;
			}

			return entries;
		}

		public static List<string> FindSpritePathsInGrf(GrfHolder grf) {
			var results = new List<string>();
			if (grf == null || grf.IsClosed)
				return results;

			var prefixes = CustomAccessorySpritePaths.GetConfiguredPrefixes().ToList();
			var files = grf.FileTable.GetFiles("", "*", SearchOption.AllDirectories);

			foreach (var file in files) {
				var path = CustomAccessorySpritePaths.NormalizeGrfPath(file);
				if (!path.EndsWith(".spr", StringComparison.OrdinalIgnoreCase))
					continue;

				if (prefixes.Count > 0 && !CustomAccessorySpritePaths.MatchesAnyPrefix(path, prefixes))
					continue;

				results.Add(path);
			}

			return results.OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToList();
		}

		public static List<string> FindNewSpritePathsInGrf(GrfHolder grf, HashSet<string> knownSprites) {
			var all = FindSpritePathsInGrf(grf);
			if (knownSprites == null || knownSprites.Count == 0)
				return new List<string>();

			return all.Where(p => !knownSprites.Contains(p)).ToList();
		}

		public static List<string> FindSpritesMissingFromLua(GrfHolder grf) {
			var results = new List<string>();
			if (grf == null || grf.IsClosed)
				return results;

			var locations = CustomAccessoryLubLocations.Resolve(grf);
			var tables = CustomAccessoryLuaTables.Load(locations.EditAccessoryIdPath, locations.EditAccnamePath);

			foreach (var path in FindSpritePathsInGrf(grf)) {
				if (!tables.HasConstant(CustomAccessoryNaming.FromSpritePath(path)))
					results.Add(path);
			}

			return results;
		}

		public static List<string> FindCandidateSprites(GrfHolder grf, HashSet<string> knownSprites) {
			if (GrfEditorConfiguration.CustomAccessoryDetectionMode == CustomAccessoryNewSpriteDetectionMode.MissingFromLua)
				return FindSpritesMissingFromLua(grf);

			return FindNewSpritePathsInGrf(grf, knownSprites);
		}

		public static void WriteEntries(IEnumerable<CustomAccessoryEntry> entries, CustomAccessoryLubLocations locations, GrfHolder grf = null) {
			var selected = entries.Where(p => p != null && p.Selected).ToList();
			if (selected.Count == 0)
				return;

			if (locations == null)
				locations = CustomAccessoryLubLocations.Resolve(grf);

			if (!locations.IsValid)
				throw new FileNotFoundException(locations.GetMissingFilesMessage());

			CustomAccessoryLuaWriter.ApplyEntries(
				locations.EditAccessoryIdPath,
				locations.EditAccnamePath,
				selected);

			locations.Commit(grf);
		}

	}
}
