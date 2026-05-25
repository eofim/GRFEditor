using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GRF.Core;
using GRFEditor.ApplicationConfiguration;

namespace GRFEditor.Tools.CustomAccessory {
	public static class CustomAccessoryLuaService {
		public static List<CustomAccessoryEntry> BuildEntriesFromSprites(
			IEnumerable<string> spritePaths,
			CustomAccessoryLuaTables tables,
			int? startViewId = null) {
			var entries = new List<CustomAccessoryEntry>();
			var nextId = startViewId.HasValue && startViewId.Value > 0
				? startViewId.Value
				: tables.GetNextViewId();

			foreach (var spritePath in spritePaths.Distinct(StringComparer.OrdinalIgnoreCase)) {
				var fileName = Path.GetFileNameWithoutExtension(spritePath.Replace('/', '\\'));
				string constantName;
				string displayName;
				CustomAccessoryNaming.FromSpriteFileName(fileName, out constantName, out displayName);

				var status = tables.GetEntryStatus(constantName);
				var entry = new CustomAccessoryEntry {
					SpritePath = spritePath,
					ConstantName = constantName,
					DisplayName = displayName,
					Status = status,
					Selected = true,
				};

				ApplyExistingLuaData(entry, tables, ref nextId);
				entries.Add(entry);
			}

			return entries;
		}

		private static void ApplyExistingLuaData(CustomAccessoryEntry entry, CustomAccessoryLuaTables tables, ref int nextId) {
			int existingId;
			string existingDisplayName;

			switch (entry.Status) {
				case CustomAccessoryEntryStatus.Existing:
					entry.IsNew = false;
					if (tables.TryGetAccessoryId(entry.ConstantName, out existingId))
						entry.ViewId = existingId;
					if (tables.TryGetAccname(entry.ConstantName, out existingDisplayName))
						entry.DisplayName = existingDisplayName;
					break;

				case CustomAccessoryEntryStatus.IncompleteMissingAccname:
					entry.IsNew = false;
					if (tables.TryGetAccessoryId(entry.ConstantName, out existingId))
						entry.ViewId = existingId;
					break;

				case CustomAccessoryEntryStatus.IncompleteMissingAccessoryId:
					entry.IsNew = false;
					if (tables.TryGetAccname(entry.ConstantName, out existingDisplayName))
						entry.DisplayName = existingDisplayName;
					entry.ViewId = nextId++;
					break;

				default:
					entry.IsNew = true;
					entry.ViewId = nextId++;
					break;
			}
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

			results = CustomAccessorySpritePaths.FilterDuplicateUnprefixedSprites(results);
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
			if (!locations.IsValid)
				return results;

			var tables = CustomAccessoryLuaTables.Load(locations.EditAccessoryIdPath, locations.EditAccnamePath);

			foreach (var path in FindSpritePathsInGrf(grf)) {
				if (!tables.HasCompleteEntry(CustomAccessoryNaming.FromSpritePath(path)))
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

			RefreshEntriesFromLuaFiles(selected, locations);

			CustomAccessoryLuaWriter.ApplyEntries(
				locations.EditAccessoryIdPath,
				locations.EditAccnamePath,
				selected);

			CustomAccessoryLuaWriter.ValidateWrittenEntries(
				locations.EditAccessoryIdPath,
				locations.EditAccnamePath,
				selected);

			if (grf == null || grf.IsClosed) {
				if (!locations.IsGrfPrimary && locations.CanWriteToGrf) {
					throw new InvalidOperationException(
						"É necessário um GRF aberto para atualizar accessoryid.lub e accname.lub dentro do container.");
				}
			}

			locations.Commit(grf);
		}

		public static void RefreshEntriesFromLuaFiles(List<CustomAccessoryEntry> selected, CustomAccessoryLubLocations locations) {
			var tables = CustomAccessoryLuaTables.Load(locations.EditAccessoryIdPath, locations.EditAccnamePath);

			foreach (var entry in selected) {
				CustomAccessoryLuaWriter.NormalizeEntryNames(entry);
				entry.Status = tables.GetEntryStatus(entry.ConstantName);

				int existingId;
				if ((entry.Status == CustomAccessoryEntryStatus.Existing
						|| entry.Status == CustomAccessoryEntryStatus.IncompleteMissingAccname)
					&& tables.TryGetAccessoryId(entry.ConstantName, out existingId))
					entry.ViewId = existingId;

				string existingDisplayName;
				if (tables.TryGetAccname(entry.ConstantName, out existingDisplayName))
					entry.DisplayName = existingDisplayName;

				entry.IsNew = entry.Status == CustomAccessoryEntryStatus.New;
			}
		}

	}
}
