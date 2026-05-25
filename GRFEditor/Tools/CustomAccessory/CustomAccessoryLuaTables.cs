using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GRF;
using GRF.FileFormats.LubFormat.Preset;
using Utilities;
using Utilities.Services;

namespace GRFEditor.Tools.CustomAccessory {
	public class CustomAccessoryLuaTables {
		public Dictionary<string, int> AccessoryIds { get; private set; }
		public Dictionary<string, string> Accnames { get; private set; }
		public string LoadStatusMessage { get; private set; }
		public bool UsedTextFallback { get; private set; }

		public static CustomAccessoryLuaTables Load(string accessoryIdPath, string accnamePath) {
			var tables = new CustomAccessoryLuaTables {
				AccessoryIds = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase),
				Accnames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
			};

			var statusParts = new List<string>();

			if (string.IsNullOrEmpty(accessoryIdPath) || !File.Exists(accessoryIdPath)) {
				tables.LoadStatusMessage = "accessoryid.lub não encontrado; próximo viewId sugerido: 1.";
				return tables;
			}

			if (string.IsNullOrEmpty(accnamePath) || !File.Exists(accnamePath))
				statusParts.Add("accname.lub não encontrado.");

			bool parserSucceeded = false;
			if (File.Exists(accnamePath)) {
				try {
					MultiType accnameBytes = File.ReadAllBytes(accnamePath);
					MultiType accessoryIdBytes = File.ReadAllBytes(accessoryIdPath);
					var accnameData = new AccnameLubData(accnameBytes, accessoryIdBytes);

					MergeAccessoryIds(tables.AccessoryIds, accnameData.AccessoryId);
					MergeAccnames(tables.Accnames, accnameData.Accname);
					parserSucceeded = true;

					if (tables.AccessoryIds.Count == 0)
						statusParts.Add("Parser AccnameLubData retornou accessoryid.lub sem IDs.");
					if (tables.Accnames.Count == 0)
						statusParts.Add("Parser AccnameLubData retornou accname.lub sem nomes.");
				}
				catch (Exception ex) {
					statusParts.Add("Parser AccnameLubData falhou: " + ex.Message);
				}
			}

			if (tables.AccessoryIds.Count == 0) {
				try {
					var fallbackIds = ParseAccessoryIdsFromTextFallback(ReadText(accessoryIdPath));
					if (fallbackIds.Count > 0) {
						MergeAccessoryIds(tables.AccessoryIds, fallbackIds);
						tables.UsedTextFallback = true;
						statusParts.Add("accessoryid: " + fallbackIds.Count + " ID(s) via fallback de texto.");
					}
					else if (parserSucceeded)
						statusParts.Add("accessoryid.lub sem IDs reconhecidos (parser e fallback).");
					else
						statusParts.Add("accessoryid.lub vazio ou ilegível; fallback não encontrou IDs.");
				}
				catch (Exception ex) {
					statusParts.Add("Fallback accessoryid.lub falhou: " + ex.Message);
				}
			}

			if (File.Exists(accnamePath) && tables.Accnames.Count == 0) {
				try {
					var fallbackNames = ParseAccnamesFromTextFallback(ReadText(accnamePath));
					if (fallbackNames.Count > 0) {
						MergeAccnames(tables.Accnames, fallbackNames);
						tables.UsedTextFallback = true;
						statusParts.Add("accname: " + fallbackNames.Count + " nome(s) via fallback de texto.");
					}
					else if (parserSucceeded)
						statusParts.Add("accname.lub sem entradas reconhecidas (parser e fallback).");
				}
				catch (Exception ex) {
					statusParts.Add("Fallback accname.lub falhou: " + ex.Message);
				}
			}

			statusParts.Add("Próximo viewId sugerido: " + tables.GetNextViewId() + ".");
			tables.LoadStatusMessage = string.Join(" ", statusParts);
			return tables;
		}

		public static Dictionary<string, int> ParseAccessoryIdsFromTextFallback(string text) {
			var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
			if (string.IsNullOrEmpty(text))
				return result;

			foreach (var line in text.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n')) {
				var trimmed = line.Trim();
				if (trimmed.Length == 0 || trimmed.StartsWith("--"))
					continue;

				var match = CustomAccessoryLuaPatterns.AccessoryIdLineRegex.Match(line);
				if (!match.Success)
					continue;

				int id;
				if (!int.TryParse(match.Groups["id"].Value, out id) || id <= 0)
					continue;

				var name = CustomAccessoryNaming.NormalizeConstantName(match.Groups["name"].Value);
				if (!result.ContainsKey(name))
					result[name] = id;
			}

			return result;
		}

		public static Dictionary<string, string> ParseAccnamesFromTextFallback(string text) {
			var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
			if (string.IsNullOrEmpty(text))
				return result;

			foreach (var line in text.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n')) {
				var trimmed = line.Trim();
				if (trimmed.Length == 0 || trimmed.StartsWith("--"))
					continue;

				var match = CustomAccessoryLuaPatterns.AccnameLineRegex.Match(line);
				if (!match.Success)
					continue;

				var name = CustomAccessoryNaming.NormalizeConstantName(match.Groups["name"].Value);
				var value = UnescapeLuaString(match.Groups["value"].Value);
				if (string.IsNullOrEmpty(value))
					continue;

				if (!result.ContainsKey(name))
					result[name] = value;
			}

			return result;
		}

		public bool HasAccessoryConstant(string constantName) {
			return AccessoryIds.ContainsKey(NormalizeKey(constantName));
		}

		public bool HasAccnameEntry(string constantName) {
			return Accnames.ContainsKey(NormalizeKey(constantName));
		}

		public bool HasCompleteEntry(string constantName) {
			return HasAccessoryConstant(constantName) && HasAccnameEntry(constantName);
		}

		public bool HasConstant(string constantName) {
			return HasAccessoryConstant(constantName);
		}

		public CustomAccessoryEntryStatus GetEntryStatus(string constantName) {
			var key = NormalizeKey(constantName);
			bool hasId = AccessoryIds.ContainsKey(key);
			bool hasName = Accnames.ContainsKey(key);

			if (hasId && hasName)
				return CustomAccessoryEntryStatus.Existing;

			if (!hasId && !hasName)
				return CustomAccessoryEntryStatus.New;

			if (hasId)
				return CustomAccessoryEntryStatus.IncompleteMissingAccname;

			return CustomAccessoryEntryStatus.IncompleteMissingAccessoryId;
		}

		public bool TryGetAccessoryId(string constantName, out int viewId) {
			return AccessoryIds.TryGetValue(NormalizeKey(constantName), out viewId);
		}

		public bool TryGetAccname(string constantName, out string displayName) {
			return Accnames.TryGetValue(NormalizeKey(constantName), out displayName);
		}

		public int GetNextViewId() {
			if (AccessoryIds == null || AccessoryIds.Count == 0)
				return 1;

			int max = 0;
			foreach (var id in AccessoryIds.Values) {
				if (id > max)
					max = id;
			}

			return max > 0 ? max + 1 : 1;
		}

		public string FindConstantForViewId(int viewId, string exceptConstantName = null) {
			foreach (var pair in AccessoryIds) {
				if (pair.Value != viewId)
					continue;

				if (!string.IsNullOrEmpty(exceptConstantName) &&
				    string.Equals(pair.Key, NormalizeKey(exceptConstantName), StringComparison.OrdinalIgnoreCase))
					continue;

				return pair.Key;
			}

			return null;
		}

		private static string NormalizeKey(string constantName) {
			return CustomAccessoryNaming.NormalizeConstantName(constantName);
		}

		private static void MergeAccessoryIds(Dictionary<string, int> target, Dictionary<string, int> source) {
			if (source == null)
				return;

			foreach (var pair in source) {
				var key = NormalizeKey(pair.Key);
				if (!target.ContainsKey(key))
					target[key] = pair.Value;
			}
		}

		private static void MergeAccnames(Dictionary<string, string> target, Dictionary<string, string> source) {
			if (source == null)
				return;

			foreach (var pair in source) {
				var key = NormalizeKey(pair.Key);
				var value = NormalizeAccnameValue(pair.Value);
				if (string.IsNullOrEmpty(value))
					continue;

				if (!target.ContainsKey(key))
					target[key] = value;
			}
		}

		private static string NormalizeAccnameValue(string value) {
			if (string.IsNullOrEmpty(value))
				return "";

			value = value.Trim();
			if (value.StartsWith("\"", StringComparison.Ordinal) && value.EndsWith("\"", StringComparison.Ordinal) && value.Length >= 2)
				value = value.Substring(1, value.Length - 2);

			return UnescapeLuaString(value);
		}

		private static string UnescapeLuaString(string value) {
			if (value == null)
				return "";

			return value.Replace("\\\"", "\"").Replace("\\\\", "\\");
		}

		private static string ReadText(string path) {
			return EncodingService.DisplayEncoding.GetString(File.ReadAllBytes(path));
		}
	}
}
