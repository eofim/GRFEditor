using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace GRFEditor.Core.ItemInfo {
	internal static class ItemInfoTextParser {
		private static readonly Regex BlockStartRegex = new Regex(
			@"\[\s*(?<id>\d+)\s*\]\s*=\s*\{",
			RegexOptions.Compiled | RegexOptions.CultureInvariant);

		private static readonly Regex StringFieldRegex = new Regex(
			@"(?<name>identifiedDisplayName|unidentifiedDisplayName|identifiedResourceName|unidentifiedResourceName)\s*=\s*(?:""(?<dq>(?:\\.|[^""\\])*)""|'(?<sq>(?:\\.|[^'\\])*)'|(?<bare>[A-Za-z0-9_]+))\s*,?",
			RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

		private static readonly Regex IntFieldRegex = new Regex(
			@"(?<name>slotCount|ClassNum|View|costumeViewId)\s*=\s*(?<value>\d+)",
			RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

		private static readonly Regex CostumeFlagRegex = new Regex(
			@"\bcostume\s*=\s*(?<v>true|false)",
			RegexOptions.Compiled | RegexOptions.IgnoreCase);

		private static readonly Regex DescriptionBlockRegex = new Regex(
			@"(?<name>identifiedDescriptionName|unidentifiedDescriptionName)\s*=\s*\{",
			RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

		private static readonly Regex QuotedStringRegex = new Regex(
			@"""(?<s>(?:\\.|[^""\\])*)""",
			RegexOptions.Compiled);

		public static void Parse(string text, ItemInfoParseResult result) {
			if (string.IsNullOrEmpty(text)) {
				result.ParseMessages.Add("Conteúdo iteminfo vazio.");
				return;
			}

			text = text.Replace("\r\n", "\n").Replace("\r", "\n");
			var matches = BlockStartRegex.Matches(text);
			result.BlocksFound = matches.Count;

			if (matches.Count == 0) {
				result.UsedHeuristicFallback = true;
				_parseHeuristicLines(text, result);
				return;
			}

			var itemIdCounts = new Dictionary<int, int>();

			foreach (Match match in matches) {
				int itemId;
				if (!int.TryParse(match.Groups["id"].Value, out itemId) || itemId <= 0) {
					result.ParseMessages.Add("Bloco com ItemId inválido ignorado.");
					continue;
				}

				int braceStart = match.Index + match.Length - 1;
				int braceEnd = _findClosingBrace(text, braceStart);
				if (braceEnd < 0) {
					result.ParseMessages.Add("ItemId " + itemId + ": bloco '{' sem fechamento.");
					continue;
				}

				string block = text.Substring(braceStart, braceEnd - braceStart + 1);
				var entry = _parseBlock(itemId, block);
				entry.SourceBlock = block.Trim();
				result.Entries.Add(entry);
				result.BlocksParsed++;

				if (!itemIdCounts.ContainsKey(itemId))
					itemIdCounts[itemId] = 0;
				itemIdCounts[itemId]++;
			}

			foreach (var pair in itemIdCounts.Where(p => p.Value > 1)) {
				foreach (var entry in result.Entries.Where(e => e.ItemId == pair.Key))
					entry.AddIssue("ItemId duplicado no arquivo (" + pair.Value + " blocos).");
			}

			foreach (var entry in result.Entries)
				entry.RecomputeValidity();
		}

		private static ItemInfoEntry _parseBlock(int itemId, string block) {
			var entry = new ItemInfoEntry {
				ItemId = itemId,
				IsValid = true,
			};

			foreach (Match m in StringFieldRegex.Matches(block)) {
				string value = _pickString(m);
				string name = m.Groups["name"].Value.ToLowerInvariant();

				switch (name) {
					case "identifieddisplayname":
						entry.IdentifiedDisplayName = value;
						break;
					case "unidentifieddisplayname":
						entry.UnidentifiedDisplayName = value;
						break;
					case "identifiedresourcename":
						entry.IdentifiedResourceName = value;
						break;
					case "unidentifiedresourcename":
						entry.UnidentifiedResourceName = value;
						break;
				}
			}

			foreach (Match m in IntFieldRegex.Matches(block)) {
				int value;
				if (!int.TryParse(m.Groups["value"].Value, out value))
					continue;

				string name = m.Groups["name"].Value.ToLowerInvariant();
				switch (name) {
					case "slotcount":
						entry.SlotCount = value;
						break;
					case "classnum":
						entry.ClassNum = value;
						entry.CostumeViewId = value;
						break;
					case "view":
					case "costumeviewid":
						entry.CostumeViewId = value;
						if (!entry.ClassNum.HasValue)
							entry.ClassNum = value;
						break;
				}
			}

			var costumeMatch = CostumeFlagRegex.Match(block);
			if (costumeMatch.Success)
				entry.Costume = costumeMatch.Groups["v"].Value.Equals("true", StringComparison.OrdinalIgnoreCase);

			_applyDescriptions(block, entry);

			if (entry.SlotCount == null)
				entry.SlotCount = 0;

			if (string.IsNullOrWhiteSpace(entry.IdentifiedDisplayName)
				&& string.IsNullOrWhiteSpace(entry.UnidentifiedDisplayName))
				entry.AddIssue("Nenhum displayName encontrado no bloco.");

			return entry;
		}

		private static void _applyDescriptions(string block, ItemInfoEntry entry) {
			foreach (Match m in DescriptionBlockRegex.Matches(block)) {
				int openBrace = m.Index + m.Length - 1;
				int closeBrace = _findClosingBrace(block, openBrace);
				if (closeBrace < 0)
					continue;

				string section = block.Substring(openBrace, closeBrace - openBrace + 1);
				var lines = new List<string>();
				foreach (Match qs in QuotedStringRegex.Matches(section))
					lines.Add(_unescape(qs.Groups["s"].Value));

				string joined = string.Join(Environment.NewLine, lines);
				string name = m.Groups["name"].Value.ToLowerInvariant();

				if (name == "identifieddescriptionname")
					entry.IdentifiedDescription = joined;
				else
					entry.UnidentifiedDescription = joined;
			}
		}

		private static void _parseHeuristicLines(string text, ItemInfoParseResult result) {
			var lines = text.Split('\n');
			ItemInfoEntry current = null;

			foreach (var raw in lines) {
				var line = raw.Trim();
				if (line.Length == 0 || line.StartsWith("--"))
					continue;

				var start = BlockStartRegex.Match(line);
				if (start.Success) {
					if (current != null) {
						current.RecomputeValidity();
						result.Entries.Add(current);
						result.BlocksParsed++;
					}

					int itemId;
					if (int.TryParse(start.Groups["id"].Value, out itemId) && itemId > 0)
						current = new ItemInfoEntry { ItemId = itemId, IsValid = true, SlotCount = 0 };
					else
						current = null;

					continue;
				}

				if (current == null)
					continue;

				foreach (Match m in StringFieldRegex.Matches(line))
					_applyStringField(current, m);

				foreach (Match m in IntFieldRegex.Matches(line))
					_applyIntField(current, m);
			}

			if (current != null) {
				current.RecomputeValidity();
				result.Entries.Add(current);
				result.BlocksParsed++;
			}

			if (result.Entries.Count > 0)
				result.ParseMessages.Add("Parse heurístico por linhas (" + result.Entries.Count + " entradas).");
		}

		private static void _applyStringField(ItemInfoEntry entry, Match m) {
			string value = _pickString(m);
			switch (m.Groups["name"].Value.ToLowerInvariant()) {
				case "identifieddisplayname": entry.IdentifiedDisplayName = value; break;
				case "unidentifieddisplayname": entry.UnidentifiedDisplayName = value; break;
				case "identifiedresourcename": entry.IdentifiedResourceName = value; break;
				case "unidentifiedresourcename": entry.UnidentifiedResourceName = value; break;
			}
		}

		private static void _applyIntField(ItemInfoEntry entry, Match m) {
			int value;
			if (!int.TryParse(m.Groups["value"].Value, out value))
				return;

			switch (m.Groups["name"].Value.ToLowerInvariant()) {
				case "slotcount": entry.SlotCount = value; break;
				case "classnum":
					entry.ClassNum = value;
					entry.CostumeViewId = value;
					break;
				case "view":
				case "costumeviewid":
					entry.CostumeViewId = value;
					if (!entry.ClassNum.HasValue)
						entry.ClassNum = value;
					break;
			}
		}

		private static string _pickString(Match m) {
			if (m.Groups["dq"].Success)
				return _unescape(m.Groups["dq"].Value);
			if (m.Groups["sq"].Success)
				return _unescape(m.Groups["sq"].Value);
			if (m.Groups["bare"].Success)
				return m.Groups["bare"].Value;

			return "";
		}

		private static string _unescape(string value) {
			if (string.IsNullOrEmpty(value))
				return "";

			return value.Replace("\\\"", "\"").Replace("\\\\", "\\").Replace("\\n", "\n");
		}

		private static int _findClosingBrace(string text, int openBraceIndex) {
			if (openBraceIndex < 0 || openBraceIndex >= text.Length || text[openBraceIndex] != '{')
				return -1;

			int depth = 0;
			bool inString = false;
			char stringChar = '\0';

			for (int i = openBraceIndex; i < text.Length; i++) {
				char c = text[i];

				if (inString) {
					if (c == '\\' && i + 1 < text.Length) {
						i++;
						continue;
					}

					if (c == stringChar)
						inString = false;

					continue;
				}

				if (c == '"' || c == '\'') {
					inString = true;
					stringChar = c;
					continue;
				}

				if (c == '{')
					depth++;
				else if (c == '}') {
					depth--;
					if (depth == 0)
						return i;
				}
			}

			return -1;
		}
	}
}
