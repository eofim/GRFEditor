using System;
using System.Collections.Generic;
using System.Linq;
using GRF.Core;
using GRFEditor.Core.AccessoryScanner;

namespace GRFEditor.Tools.CustomAccessory {
	public sealed class CustomAccessoryManagerEntry {
		private object _previewImage;

		public CustomAccessoryManagerEntry(AccessoryScanEntry scanEntry, GrfHolder grf, string localSpriteRoot) {
			ScanEntry = scanEntry ?? new AccessoryScanEntry();
			_grf = grf;
			_localSpriteRoot = localSpriteRoot ?? "";
			Selected = _defaultSelected(ScanStatus);

			if (!String.IsNullOrWhiteSpace(ScanEntry.ConstantName))
				ConstantName = ScanEntry.ConstantName;

			if (ScanEntry.ViewId.HasValue && ScanEntry.ViewId.Value > 0)
				ViewId = ScanEntry.ViewId.Value;

			if (!String.IsNullOrWhiteSpace(ScanEntry.DisplayName))
				DisplayName = ScanEntry.DisplayName;
		}

		private readonly GrfHolder _grf;
		private readonly string _localSpriteRoot;

		public AccessoryScanEntry ScanEntry { get; }

		public bool Selected { get; set; }

		public string SpritePath => ScanEntry.SpritePath ?? "";

		public string ActPath => ScanEntry.ActPath ?? "";

		public string ConstantName { get; set; }

		public int ViewId { get; set; }

		public string DisplayName { get; set; }

		public AccessoryScanStatus ScanStatus => ScanEntry.Status;

		public string Status => ScanStatus.ToString();

		public string StatusDisplay {
			get {
				switch (WriteStatus) {
					case CustomAccessoryEntryStatus.Existing:
						return "Existente (Lua)";
					case CustomAccessoryEntryStatus.IncompleteMissingAccessoryId:
						return "Falta accessoryid";
					case CustomAccessoryEntryStatus.IncompleteMissingAccname:
						return "Falta accname";
					default:
						return Status;
				}
			}
		}

		public CustomAccessoryEntryStatus WriteStatus { get; private set; } = CustomAccessoryEntryStatus.New;

		public string Issues => ScanEntry.Issues == null || ScanEntry.Issues.Count == 0
			? ""
			: String.Join("; ", ScanEntry.Issues);

		public string ToolTipIssues => Issues;

		public object PreviewImage {
			get {
				if (_previewImage == null)
					_previewImage = CustomAccessoryManagerPreview.GetPreview(_grf, _localSpriteRoot, SpritePath);

				return _previewImage;
			}
		}

		public bool CanWriteToLua {
			get {
				switch (ScanStatus) {
					case AccessoryScanStatus.New:
					case AccessoryScanStatus.MissingAct:
					case AccessoryScanStatus.MissingAccessoryId:
					case AccessoryScanStatus.MissingAccName:
					case AccessoryScanStatus.Existing:
					case AccessoryScanStatus.DuplicateViewId:
					case AccessoryScanStatus.DuplicateConstant:
						return true;
					default:
						return false;
				}
			}
		}

		public static List<CustomAccessoryManagerEntry> FromScanResult(
			AccessoryScanResult result,
			GrfHolder grf,
			string localSpriteRoot,
			CustomAccessoryLuaTables tables) {
			var list = new List<CustomAccessoryManagerEntry>();
			if (result?.Entries == null)
				return list;

			int nextId = tables != null ? tables.GetNextViewId() : 1;

			foreach (var scan in result.Entries) {
				var row = new CustomAccessoryManagerEntry(scan, grf, localSpriteRoot);
				row.ApplySuggestions(tables, ref nextId);
				row.RefreshWriteStatus(tables);
				list.Add(row);
			}

			return list;
		}

		public void ApplySuggestions(CustomAccessoryLuaTables tables, ref int nextViewId) {
			if (String.IsNullOrWhiteSpace(ConstantName) && !String.IsNullOrWhiteSpace(SpritePath)) {
				string constantName;
				string displayName;
				var fileName = System.IO.Path.GetFileNameWithoutExtension(SpritePath.Replace('/', '\\'));
				CustomAccessoryNaming.FromSpriteFileName(fileName, out constantName, out displayName);
				ConstantName = constantName;

				if (String.IsNullOrWhiteSpace(DisplayName))
					DisplayName = displayName;
			}

			ConstantName = CustomAccessoryNaming.NormalizeConstantName(ConstantName ?? "");

			if (String.IsNullOrWhiteSpace(DisplayName) && !String.IsNullOrWhiteSpace(ConstantName))
				DisplayName = CustomAccessoryNaming.NormalizeDisplayName(ConstantName);

			if (tables == null)
				return;

			int existingId;
			string existingDisplay;

			if (tables.TryGetAccessoryId(ConstantName, out existingId) && existingId > 0)
				ViewId = existingId;
			else if (ViewId <= 0)
				ViewId = nextViewId++;

			if (tables.TryGetAccname(ConstantName, out existingDisplay) && !String.IsNullOrWhiteSpace(existingDisplay))
				DisplayName = existingDisplay;
		}

		public void RefreshWriteStatus(CustomAccessoryLuaTables tables) {
			if (tables == null || String.IsNullOrWhiteSpace(ConstantName)) {
				WriteStatus = CustomAccessoryEntryStatus.New;
				return;
			}

			WriteStatus = tables.GetEntryStatus(ConstantName);
		}

		public CustomAccessoryEntry ToCustomAccessoryEntry() {
			var entry = new CustomAccessoryEntry {
				SpritePath = SpritePath,
				ConstantName = ConstantName,
				ViewId = ViewId,
				DisplayName = DisplayName,
				Status = WriteStatus,
				Selected = Selected,
				IsNew = WriteStatus == CustomAccessoryEntryStatus.New,
			};

			CustomAccessoryLuaWriter.NormalizeEntryNames(entry);
			return entry;
		}

		public string ToCopyLine() {
			return String.Join("\t", new[] {
				SpritePath,
				ActPath,
				ConstantName ?? "",
				ViewId > 0 ? ViewId.ToString() : "",
				DisplayName ?? "",
				Status,
				Issues,
			});
		}

		private static bool _defaultSelected(AccessoryScanStatus status) {
			switch (status) {
				case AccessoryScanStatus.New:
				case AccessoryScanStatus.MissingAct:
				case AccessoryScanStatus.MissingAccessoryId:
				case AccessoryScanStatus.MissingAccName:
				case AccessoryScanStatus.DuplicateViewId:
				case AccessoryScanStatus.DuplicateConstant:
					return true;
				default:
					return false;
			}
		}
	}
}
