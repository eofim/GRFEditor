using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GRF.Core;
using GRFEditor.Core.RagnarokValidation;
using GRFEditor.Tools.CustomAccessory;

namespace GRFEditor.Core.AccessoryScanner {
	internal sealed class AccessoryLubIndex {
		public Dictionary<string, int> AccessoryIds { get; } =
			new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

		public Dictionary<string, string> Accnames { get; } =
			new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

		public HashSet<string> DuplicateConstants { get; } =
			new HashSet<string>(StringComparer.OrdinalIgnoreCase);

		public HashSet<int> DuplicateViewIds { get; } = new HashSet<int>();

		public static AccessoryLubIndex Load(GrfHolder grf, string accessoryIdPath, string accnamePath) {
			var index = new AccessoryLubIndex();

			if (_isDiskPath(accessoryIdPath) && File.Exists(accessoryIdPath)) {
				_mergeAccessoryIdText(index, File.ReadAllText(accessoryIdPath));
			}
			else if (!String.IsNullOrWhiteSpace(accessoryIdPath)) {
				string text = RagnarokAccessoryLuaParser.ReadEntryText(grf, accessoryIdPath);
				if (text != null)
					_mergeAccessoryIdText(index, text);
			}

			if (_isDiskPath(accnamePath) && File.Exists(accnamePath)) {
				_mergeAccnameText(index, File.ReadAllText(accnamePath));
			}
			else if (!String.IsNullOrWhiteSpace(accnamePath)) {
				string text = RagnarokAccessoryLuaParser.ReadEntryText(grf, accnamePath);
				if (text != null)
					_mergeAccnameText(index, text);
			}

			if (index.AccessoryIds.Count == 0 && index.Accnames.Count == 0
				&& _isDiskPath(accessoryIdPath) && _isDiskPath(accnamePath)
				&& File.Exists(accessoryIdPath) && File.Exists(accnamePath)) {
				var tables = CustomAccessoryLuaTables.Load(accessoryIdPath, accnamePath);
				_mergeFromTables(index, tables);
			}

			_finalizeDuplicateViewIds(index);
			return index;
		}

		public bool TryGetViewId(string constantName, out int viewId) {
			return AccessoryIds.TryGetValue(CustomAccessoryNaming.NormalizeConstantName(constantName), out viewId);
		}

		public bool TryGetDisplayName(string constantName, out string displayName) {
			return Accnames.TryGetValue(CustomAccessoryNaming.NormalizeConstantName(constantName), out displayName);
		}

		public bool HasAccessoryId(string constantName) {
			return AccessoryIds.ContainsKey(CustomAccessoryNaming.NormalizeConstantName(constantName));
		}

		public bool HasAccname(string constantName) {
			return Accnames.ContainsKey(CustomAccessoryNaming.NormalizeConstantName(constantName));
		}

		private static bool _isDiskPath(string path) {
			return !String.IsNullOrWhiteSpace(path) && Path.IsPathRooted(path);
		}

		private static void _mergeFromTables(AccessoryLubIndex index, CustomAccessoryLuaTables tables) {
			if (tables?.AccessoryIds == null)
				return;

			foreach (var pair in tables.AccessoryIds)
				_addAccessoryId(index, pair.Key, pair.Value);

			if (tables.Accnames == null)
				return;

			foreach (var pair in tables.Accnames)
				_addAccname(index, pair.Key, pair.Value);

			_finalizeDuplicateViewIds(index);
		}

		private static void _mergeAccessoryIdText(AccessoryLubIndex index, string text) {
			foreach (var line in RagnarokAccessoryLuaParser.EnumerateLines(text ?? "")) {
				if (!RagnarokAccessoryLuaParser.IsAccessoryIdLineMatch(line))
					continue;

				var match = CustomAccessoryLuaPatterns.AccessoryIdLineRegex.Match(line);
				if (!match.Success)
					continue;

				int id;
				if (!Int32.TryParse(match.Groups["id"].Value, out id) || id <= 0)
					continue;

				string name = CustomAccessoryNaming.NormalizeConstantName(match.Groups["name"].Value);
				_addAccessoryId(index, name, id);
			}
		}

		private static void _mergeAccnameText(AccessoryLubIndex index, string text) {
			foreach (var line in RagnarokAccessoryLuaParser.EnumerateLines(text ?? "")) {
				var trimmed = line.Trim();
				if (RagnarokAccessoryLuaParser.IsSkippableLine(trimmed))
					continue;

				var match = CustomAccessoryLuaPatterns.AccnameLineRegex.Match(line);
				if (!match.Success)
					continue;

				string name = CustomAccessoryNaming.NormalizeConstantName(match.Groups["name"].Value);
				string value = _unescapeLuaString(match.Groups["value"].Value);

				if (String.IsNullOrEmpty(value))
					continue;

				_addAccname(index, name, value);
			}
		}

		private static void _addAccessoryId(AccessoryLubIndex index, string constantName, int viewId) {
			if (index.AccessoryIds.ContainsKey(constantName))
				index.DuplicateConstants.Add(constantName);
			else
				index.AccessoryIds[constantName] = viewId;
		}

		private static void _addAccname(AccessoryLubIndex index, string constantName, string displayName) {
			if (!index.Accnames.ContainsKey(constantName))
				index.Accnames[constantName] = displayName;
		}

		private static void _finalizeDuplicateViewIds(AccessoryLubIndex index) {
			foreach (var group in index.AccessoryIds.GroupBy(p => p.Value).Where(g => g.Count() > 1)) {
				index.DuplicateViewIds.Add(group.Key);

				foreach (string constant in group.Select(p => p.Key))
					index.DuplicateConstants.Add(constant);
			}
		}

		private static string _unescapeLuaString(string value) {
			if (value == null)
				return "";

			value = value.Trim();
			if (value.StartsWith("\"", StringComparison.Ordinal) && value.EndsWith("\"", StringComparison.Ordinal) && value.Length >= 2)
				value = value.Substring(1, value.Length - 2);

			return value.Replace("\\\"", "\"").Replace("\\\\", "\\");
		}
	}
}
