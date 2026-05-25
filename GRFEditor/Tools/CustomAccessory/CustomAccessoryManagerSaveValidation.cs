using System;
using System.Collections.Generic;
using System.Linq;

namespace GRFEditor.Tools.CustomAccessory {
	public sealed class CustomAccessoryManagerSaveValidation {
		public List<string> Errors { get; } = new List<string>();
		public List<string> ViewIdWarnings { get; } = new List<string>();

		public bool HasErrors => Errors.Count > 0;
		public bool HasViewIdWarnings => ViewIdWarnings.Count > 0;
		public bool CanProceed => !HasErrors;
	}

	public static class CustomAccessoryManagerSaveValidator {
		public static CustomAccessoryManagerSaveValidation Validate(
			IList<CustomAccessoryManagerEntry> selected,
			CustomAccessoryLuaTables tables) {
			var result = new CustomAccessoryManagerSaveValidation();
			if (selected == null || selected.Count == 0) {
				result.Errors.Add("Nenhum item selecionado.");
				return result;
			}

			var normalizedNames = new Dictionary<string, CustomAccessoryManagerEntry>(StringComparer.OrdinalIgnoreCase);

			foreach (var row in selected) {
				if (!row.CanWriteToLua) {
					result.Errors.Add(row.SpritePath + ": status \"" + row.Status + "\" não permite gravação em Lua.");
					continue;
				}

				if (String.IsNullOrWhiteSpace(row.ConstantName)) {
					result.Errors.Add(row.SpritePath + ": ConstantName vazio.");
					continue;
				}

				if (row.ViewId <= 0) {
					result.Errors.Add(row.ConstantName + ": viewId inválido.");
					continue;
				}

				var constant = CustomAccessoryNaming.NormalizeConstantName(row.ConstantName);
				CustomAccessoryManagerEntry otherRow;
				if (normalizedNames.TryGetValue(constant, out otherRow)) {
					result.Errors.Add("ConstantName duplicado na seleção: " + constant
						+ " (" + otherRow.SpritePath + " e " + row.SpritePath + ").");
				}
				else {
					normalizedNames[constant] = row;
				}

				if (tables != null) {
					if (row.WriteStatus == CustomAccessoryEntryStatus.New && tables.HasConstant(constant)) {
						result.Errors.Add(constant + " já existe em accessoryid.lub (ConstantName duplicado).");
					}

					int existingIdForConstant;
					if (tables.TryGetAccessoryId(constant, out existingIdForConstant)
						&& existingIdForConstant != row.ViewId) {
						var otherConstant = tables.FindConstantForViewId(row.ViewId, constant);
						if (otherConstant != null) {
							result.ViewIdWarnings.Add(constant + " usa viewId " + row.ViewId
								+ ", já ocupado por " + otherConstant + ".");
						}
						else {
							result.ViewIdWarnings.Add(constant + ": alterando viewId de "
								+ existingIdForConstant + " para " + row.ViewId + ".");
						}
					}
					else {
						var occupant = tables.FindConstantForViewId(row.ViewId, constant);
						if (occupant != null) {
							result.ViewIdWarnings.Add(constant + " (viewId " + row.ViewId
								+ ") conflita com " + occupant + " já presente em accessoryid.lub.");
						}
					}
				}
			}

			foreach (var group in selected.Where(p => p.ViewId > 0).GroupBy(p => p.ViewId).Where(g => g.Count() > 1)) {
				var names = String.Join(", ", group.Select(p => p.ConstantName));
				result.ViewIdWarnings.Add("viewId " + group.Key + " repetido entre: " + names + ".");
			}

			return result;
		}
	}
}
