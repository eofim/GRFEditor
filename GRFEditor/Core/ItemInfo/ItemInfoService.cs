using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using GRF.Core;
using GRF.IO;
using GRFEditor.Core.ProjectProfiles;
using GRFEditor.Tools.GrfValidation;
using Utilities.Services;

namespace GRFEditor.Core.ItemInfo {
	public static class ItemInfoService {
		public static ItemInfoParseResult ParseFile(string filePath) {
			var result = new ItemInfoParseResult { SourcePath = filePath ?? "" };

			if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath)) {
				result.ParseMessages.Add("Arquivo iteminfo não encontrado: " + filePath);
				return result;
			}

			try {
				string text = File.ReadAllText(filePath, EncodingService.DisplayEncoding);
				ItemInfoTextParser.Parse(text, result);
			}
			catch (Exception ex) {
				result.ParseMessages.Add("Falha ao ler iteminfo: " + ex.Message);
			}

			return result;
		}

		public static ItemInfoParseResult ParseText(string text, string sourceLabel = null) {
			var result = new ItemInfoParseResult { SourcePath = sourceLabel ?? "(texto)" };
			ItemInfoTextParser.Parse(text ?? "", result);
			return result;
		}

		public static ItemInfoParseResult ParseFromGrf(GrfHolder grf, string grfRelativePath) {
			var result = new ItemInfoParseResult { SourcePath = grfRelativePath ?? "" };

			string text = ItemInfoFileReader.ReadText(grf, grfRelativePath);
			if (text == null) {
				result.ParseMessages.Add("iteminfo não encontrado no GRF ou em disco: " + grfRelativePath);
				return result;
			}

			ItemInfoTextParser.Parse(text, result);
			return result;
		}

		public static ItemInfoParseResult ParseFromProfileOrGrf(GrfHolder grf, string configuredPath = null) {
			string path = !string.IsNullOrWhiteSpace(configuredPath)
				? configuredPath
				: ActiveProjectProfile.GetItemInfoPath();

			if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
				return ParseFile(path);

			if (grf != null && grf.IsOpened) {
				string grfPath = FindItemInfoPathInGrf(grf);
				if (!string.IsNullOrEmpty(grfPath))
					return ParseFromGrf(grf, grfPath);
			}

			var result = new ItemInfoParseResult { SourcePath = path ?? "" };
			result.ParseMessages.Add("iteminfo.lua/lub não encontrado no perfil nem no GRF.");
			return result;
		}

		public static string FindItemInfoPathInGrf(GrfHolder grf) {
			if (grf == null || !grf.IsOpened)
				return null;

			foreach (string fileName in new[] { "iteminfo.lub", "iteminfo.lua" }) {
				foreach (string file in grf.FileTable.GetFiles("", "*", SearchOption.AllDirectories)) {
					if (file.EndsWith(fileName, StringComparison.OrdinalIgnoreCase))
						return GrfPath.CleanGrfPath(file);
				}
			}

			return null;
		}

		public static HashSet<int> LoadKnownViewIdsFromAccessoryId(GrfHolder grf, string accessoryIdPath) {
			var known = new HashSet<int>();
			string text = ItemInfoFileReader.ReadText(grf, accessoryIdPath);
			if (string.IsNullOrEmpty(text))
				return known;

			var tables = Tools.CustomAccessory.CustomAccessoryLuaTables.ParseAccessoryIdsFromTextFallback(text);
			foreach (var id in tables.Values) {
				if (id > 0)
					known.Add(id);
			}

			return known;
		}

		public static ItemInfoValidationSummary ValidateEntries(
			IList<ItemInfoEntry> entries,
			ItemInfoValidationOptions options) {
			var summary = new ItemInfoValidationSummary();
			options = options ?? new ItemInfoValidationOptions();
			_applyDefaultTexturePaths(options);

			if (entries == null || entries.Count == 0) {
				summary.GlobalIssues.Add("Nenhuma entrada para validar.");
				return summary;
			}

			summary.TotalEntries = entries.Count;

			if (options.ValidateDuplicateItemIds)
				_validateDuplicateItemIds(entries, summary);

			if (options.ValidateDuplicateViewIds)
				_validateDuplicateViewIds(entries, summary);

			if (options.ValidateMissingViewIds && options.KnownViewIds != null && options.KnownViewIds.Count > 0)
				_validateMissingViewIds(entries, options, summary);

			if (options.ValidateTextures)
				_validateTextures(entries, options, summary);

			foreach (var entry in entries) {
				if (entry.ItemId <= 0)
					entry.AddIssue("ItemId inválido.");

				entry.RecomputeValidity();
				if (entry.IsValid)
					summary.ValidEntries++;
				else
					summary.InvalidEntries++;
			}

			return summary;
		}

		public static ItemInfoValidationSummary ValidateParseResult(
			ItemInfoParseResult parseResult,
			ItemInfoValidationOptions options) {
			if (parseResult == null) {
				var empty = new ItemInfoValidationSummary();
				empty.GlobalIssues.Add("ParseResult nulo.");
				return empty;
			}

			return ValidateEntries(parseResult.Entries, options);
		}

		public static string GenerateLuaBlock(ItemInfoEntry entry, bool includeCostumeFlag = true) {
			if (entry == null || entry.ItemId <= 0)
				throw new ArgumentException("ItemInfoEntry com ItemId inválido.");

			var display = entry.IdentifiedDisplayName ?? entry.UnidentifiedDisplayName ?? "";
			var unDisplay = entry.UnidentifiedDisplayName ?? display;
			var res = entry.IdentifiedResourceName ?? entry.UnidentifiedResourceName ?? "";
			var unRes = entry.UnidentifiedResourceName ?? res;
			int slot = entry.SlotCount ?? 0;
			int classNum = entry.ClassNum ?? entry.CostumeViewId ?? 0;

			if (classNum <= 0)
				throw new InvalidOperationException("ClassNum / CostumeViewId é obrigatório para gerar o bloco.");

			var sb = new StringBuilder();
			sb.AppendLine("\t[" + entry.ItemId + "] = {");
			sb.AppendLine("\t\tunidentifiedDisplayName = \"" + _escapeLua(unDisplay) + "\",");
			sb.AppendLine("\t\tunidentifiedResourceName = \"" + _escapeLua(unRes) + "\",");
			_appendDescriptionBlock(sb, "\t\t", "unidentifiedDescriptionName",
				entry.UnidentifiedDescription ?? entry.IdentifiedDescription);
			sb.AppendLine("\t\tidentifiedDisplayName = \"" + _escapeLua(display) + "\",");
			sb.AppendLine("\t\tidentifiedResourceName = \"" + _escapeLua(res) + "\",");
			_appendDescriptionBlock(sb, "\t\t", "identifiedDescriptionName", entry.IdentifiedDescription);
			sb.AppendLine("\t\tslotCount = " + slot + ",");
			sb.AppendLine("\t\tClassNum = " + classNum + ",");

			if (includeCostumeFlag && (entry.Costume || entry.CostumeViewId.HasValue))
				sb.AppendLine("\t\tcostume = true");

			sb.AppendLine("\t},");
			sb.AppendLine();
			return sb.ToString();
		}

		public static string GenerateLuaBlocks(IEnumerable<ItemInfoEntry> entries, bool includeCostumeFlag = true) {
			var sb = new StringBuilder();
			sb.AppendLine("-- Gerado pelo GRFEditor (ItemInfoService)");
			sb.AppendLine();

			foreach (var entry in entries.Where(e => e != null && e.ItemId > 0).OrderBy(e => e.ItemId))
				sb.Append(GenerateLuaBlock(entry, includeCostumeFlag));

			return sb.ToString();
		}

		public static ItemInfoEntry CreateNewEntry(
			int itemId,
			int costumeViewId,
			string identifiedDisplayName,
			string identifiedResourceName,
			int slotCount = 0) {
			var entry = new ItemInfoEntry {
				ItemId = itemId,
				CostumeViewId = costumeViewId,
				ClassNum = costumeViewId,
				IdentifiedDisplayName = identifiedDisplayName,
				UnidentifiedDisplayName = identifiedDisplayName,
				IdentifiedResourceName = identifiedResourceName,
				UnidentifiedResourceName = identifiedResourceName,
				SlotCount = slotCount,
				Costume = true,
				IsValid = true,
			};

			entry.IdentifiedDescription = "Oops, este visual ainda não tem uma descrição.";
			entry.UnidentifiedDescription = entry.IdentifiedDescription;
			entry.RecomputeValidity();
			return entry;
		}

		public static string ExportReportText(
			ItemInfoParseResult parseResult,
			ItemInfoValidationSummary validation = null) {
			var sb = new StringBuilder();
			sb.AppendLine("=== Relatório iteminfo ===");
			sb.AppendLine("Fonte: " + (parseResult?.SourcePath ?? ""));
			sb.AppendLine();

			if (parseResult != null) {
				sb.AppendLine("--- Parse ---");
				sb.AppendLine("Blocos encontrados: " + parseResult.BlocksFound);
				sb.AppendLine("Blocos interpretados: " + parseResult.BlocksParsed);
				sb.AppendLine("Entradas: " + parseResult.Entries.Count);
				sb.AppendLine("Válidas (parse): " + parseResult.ValidCount);
				sb.AppendLine("Heurística: " + (parseResult.UsedHeuristicFallback ? "sim" : "não"));

				foreach (string msg in parseResult.ParseMessages)
					sb.AppendLine("  " + msg);

				sb.AppendLine();
				sb.AppendLine("--- Entradas com problemas ---");
				foreach (var entry in parseResult.Entries.Where(e => !e.IsValid || e.Issues.Count > 0)
					.OrderBy(e => e.ItemId)) {
					sb.AppendLine("ItemId " + entry.ItemId + ":");
					foreach (string issue in entry.Issues)
						sb.AppendLine("  - " + issue);
				}
			}

			if (validation != null) {
				sb.AppendLine();
				sb.AppendLine("--- Validação ---");
				sb.AppendLine("Total: " + validation.TotalEntries);
				sb.AppendLine("Válidas: " + validation.ValidEntries);
				sb.AppendLine("Inválidas: " + validation.InvalidEntries);
				sb.AppendLine("ItemId duplicados: " + validation.DuplicateItemIdCount);
				sb.AppendLine("ViewId duplicados: " + validation.DuplicateViewIdCount);
				sb.AppendLine("ViewId inexistentes: " + validation.MissingViewIdCount);
				sb.AppendLine("Texturas ausentes: " + validation.MissingTextureCount);

				foreach (string issue in validation.GlobalIssues)
					sb.AppendLine("  " + issue);
			}

			return sb.ToString();
		}

		public static string ExportReportCsv(IEnumerable<ItemInfoEntry> entries) {
			var sb = new StringBuilder();
			sb.AppendLine("ItemId,ClassNum,CostumeViewId,IdentifiedDisplayName,IdentifiedResourceName,SlotCount,IsValid,Issues");

			foreach (var entry in entries ?? Enumerable.Empty<ItemInfoEntry>()) {
				sb.Append(entry.ItemId);
				sb.Append(',');
				sb.Append(entry.ClassNum?.ToString() ?? "");
				sb.Append(',');
				sb.Append(entry.CostumeViewId?.ToString() ?? "");
				sb.Append(',');
				sb.Append(_csv(entry.IdentifiedDisplayName));
				sb.Append(',');
				sb.Append(_csv(entry.IdentifiedResourceName));
				sb.Append(',');
				sb.Append(entry.SlotCount?.ToString() ?? "");
				sb.Append(',');
				sb.Append(entry.IsValid ? "true" : "false");
				sb.Append(',');
				sb.Append(_csv(string.Join("; ", entry.Issues)));
				sb.AppendLine();
			}

			return sb.ToString();
		}

		public static void WriteReportFiles(
			string outputFolder,
			ItemInfoParseResult parseResult,
			ItemInfoValidationSummary validation) {
			if (string.IsNullOrWhiteSpace(outputFolder))
				throw new ArgumentException("Pasta de saída não definida.");

			Directory.CreateDirectory(outputFolder);
			File.WriteAllText(
				Path.Combine(outputFolder, "iteminfo-report.txt"),
				ExportReportText(parseResult, validation),
				new UTF8Encoding(true));
			File.WriteAllText(
				Path.Combine(outputFolder, "iteminfo-entries.csv"),
				ExportReportCsv(parseResult?.Entries),
				new UTF8Encoding(true));
		}

		private static void _validateDuplicateItemIds(IList<ItemInfoEntry> entries, ItemInfoValidationSummary summary) {
			foreach (var group in entries.Where(e => e.ItemId > 0).GroupBy(e => e.ItemId).Where(g => g.Count() > 1)) {
				summary.DuplicateItemIdCount++;
				foreach (var entry in group)
					entry.AddIssue("ItemId " + group.Key + " duplicado na lista (" + group.Count() + " entradas).");
			}
		}

		private static void _validateDuplicateViewIds(IList<ItemInfoEntry> entries, ItemInfoValidationSummary summary) {
			var viewOwners = new Dictionary<int, List<int>>();

			foreach (var entry in entries) {
				int? viewId = entry.EffectiveViewId;
				if (!viewId.HasValue || viewId.Value <= 0)
					continue;

				List<int> owners;
				if (!viewOwners.TryGetValue(viewId.Value, out owners)) {
					owners = new List<int>();
					viewOwners[viewId.Value] = owners;
				}

				if (!owners.Contains(entry.ItemId))
					owners.Add(entry.ItemId);
			}

			foreach (var pair in viewOwners.Where(p => p.Value.Count > 1)) {
				summary.DuplicateViewIdCount++;
				string itemList = string.Join(", ", pair.Value);
				foreach (var entry in entries.Where(e => e.EffectiveViewId == pair.Key))
					entry.AddIssue("ClassNum/ViewId " + pair.Key + " duplicado entre ItemIds: " + itemList + ".");
			}
		}

		private static void _validateMissingViewIds(
			IList<ItemInfoEntry> entries,
			ItemInfoValidationOptions options,
			ItemInfoValidationSummary summary) {
			foreach (var entry in entries) {
				int? viewId = entry.EffectiveViewId;
				if (!viewId.HasValue || viewId.Value <= 0)
					continue;

				if (!options.KnownViewIds.Contains(viewId.Value)) {
					summary.MissingViewIdCount++;
					entry.AddIssue("ClassNum/ViewId " + viewId.Value + " não existe em accessoryid.lub.");
				}
			}
		}

		private static void _validateTextures(
			IList<ItemInfoEntry> entries,
			ItemInfoValidationOptions options,
			ItemInfoValidationSummary summary) {
			bool canCheck = (options.Grf != null && options.Grf.IsOpened)
				|| (!string.IsNullOrWhiteSpace(options.ClientDataFolder) && Directory.Exists(options.ClientDataFolder));

			if (!canCheck) {
				summary.GlobalIssues.Add("Validação de texturas ignorada (sem GRF aberto nem pasta do cliente).");
				return;
			}

			foreach (var entry in entries) {
				if (options.RequireIdentifiedResourceForTextures
					&& string.IsNullOrWhiteSpace(entry.IdentifiedResourceName)
					&& string.IsNullOrWhiteSpace(entry.UnidentifiedResourceName)) {
					entry.AddIssue("ResourceName ausente para validar textura.");
					continue;
				}

				var resources = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
				if (!string.IsNullOrWhiteSpace(entry.IdentifiedResourceName))
					resources.Add(entry.IdentifiedResourceName);
				if (!string.IsNullOrWhiteSpace(entry.UnidentifiedResourceName))
					resources.Add(entry.UnidentifiedResourceName);

				foreach (string resource in resources) {
					bool collectionOk = !options.CheckCollectionTexture
						|| _textureExists(resource, options.CollectionTextureRelativePath, options);
					bool itemOk = !options.CheckItemIconTexture
						|| _textureExists(resource, options.ItemTextureRelativePath, options);

					if (!collectionOk || !itemOk) {
						summary.MissingTextureCount++;
						var missing = new List<string>();
						if (!collectionOk)
							missing.Add("collection");
						if (!itemOk)
							missing.Add("item");
						entry.AddIssue("Textura não encontrada (" + string.Join(", ", missing) + "): " + resource);
					}
				}
			}
		}

		private static bool _textureExists(string resourceName, string relativeFolder, ItemInfoValidationOptions options) {
			if (string.IsNullOrWhiteSpace(resourceName) || string.IsNullOrWhiteSpace(relativeFolder))
				return false;

			string fileName = _resourceToBmpFileName(resourceName);
			string relativePath = GrfPath.Combine(relativeFolder, fileName);
			relativePath = GrfPath.CleanGrfPath(relativePath);

			if (options.Grf != null && options.Grf.IsOpened && options.Grf.FileTable.ContainsFile(relativePath))
				return true;

			if (!string.IsNullOrWhiteSpace(options.ClientDataFolder)) {
				string diskPath = Path.Combine(options.ClientDataFolder, relativePath.Replace('/', Path.DirectorySeparatorChar));
				if (File.Exists(diskPath))
					return true;
			}

			return false;
		}

		private static string _resourceToBmpFileName(string resourceName) {
			var name = resourceName.Trim();
			if (name.StartsWith("_", StringComparison.Ordinal))
				name = name.Substring(1);

			return name + ".bmp";
		}

		private static void _applyDefaultTexturePaths(ItemInfoValidationOptions options) {
			if (!string.IsNullOrWhiteSpace(options.CollectionTextureRelativePath)
				&& !string.IsNullOrWhiteSpace(options.ItemTextureRelativePath))
				return;

			var defaults = new ValidateContentReader();
			if (string.IsNullOrWhiteSpace(options.CollectionTextureRelativePath))
				options.CollectionTextureRelativePath = defaults.TextureCollectionPath;
			if (string.IsNullOrWhiteSpace(options.ItemTextureRelativePath))
				options.ItemTextureRelativePath = defaults.TextureItemPath;
		}

		private static void _appendDescriptionBlock(StringBuilder sb, string indent, string fieldName, string description) {
			sb.AppendLine(indent + fieldName + " = {");

			if (string.IsNullOrWhiteSpace(description)) {
				sb.AppendLine(indent + "\t\"...\",");
			}
			else {
				foreach (var line in description.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n')) {
					if (string.IsNullOrWhiteSpace(line))
						continue;
					sb.AppendLine(indent + "\t\"" + _escapeLua(line.Trim()) + "\",");
				}
			}

			sb.AppendLine(indent + "},");
		}

		private static string _escapeLua(string value) {
			if (value == null)
				return "";

			return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
		}

		private static string _csv(string value) {
			if (string.IsNullOrEmpty(value))
				return "\"\"";

			if (value.IndexOfAny(new[] { '"', ',', '\r', '\n' }) < 0)
				return value;

			return "\"" + value.Replace("\"", "\"\"") + "\"";
		}
	}
}
