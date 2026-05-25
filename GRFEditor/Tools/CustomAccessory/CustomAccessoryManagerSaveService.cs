using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using GRF.Core;
using Utilities.Services;

namespace GRFEditor.Tools.CustomAccessory {
	public static class CustomAccessoryManagerSaveService {
		public static bool TryEnsureLubLocations(GrfHolder grf, Window owner, out CustomAccessoryLubLocations locations) {
			locations = CustomAccessoryLubLocations.Resolve(grf);

			if (locations.IsValid)
				return true;

			var dialog = new CustomAccessoryLubPathsDialog { Owner = owner };
			if (dialog.ShowDialog() != true)
				return false;

			locations.SetManualEditPaths(dialog.AccessoryIdPath, dialog.AccnamePath);

			if (!locations.IsValid) {
				MessageBox.Show(owner,
					"Selecione accessoryid.lub e accname.lub válidos.",
					"Arquivos Lua",
					MessageBoxButton.OK,
					MessageBoxImage.Warning);
				return false;
			}

			return true;
		}

		public static string BuildDiffPreview(
			CustomAccessoryLubLocations locations,
			IList<CustomAccessoryEntry> entries) {
			var accessoryBefore = _readText(locations.EditAccessoryIdPath);
			var accnameBefore = _readText(locations.EditAccnamePath);

			var accessoryAfter = CustomAccessoryLuaWriter.PreviewAccessoryIdContent(accessoryBefore, entries);
			var accnameAfter = CustomAccessoryLuaWriter.PreviewAccnameContent(accnameBefore, entries);

			var sb = new StringBuilder();
			sb.Append(CustomAccessoryLuaWriter.BuildLineDiffPreview("accessoryid.lub", accessoryBefore, accessoryAfter));
			sb.AppendLine();
			sb.Append(CustomAccessoryLuaWriter.BuildLineDiffPreview("accname.lub", accnameBefore, accnameAfter));
			sb.AppendLine();
			sb.AppendLine("Destino de edição:");
			sb.AppendLine("  " + locations.EditAccessoryIdPath);
			sb.AppendLine("  " + locations.EditAccnamePath);
			return sb.ToString();
		}

		public static IList<string> CreateBackups(CustomAccessoryLubLocations locations) {
			var created = new List<string>();
			created.Add(_backupFile(locations.EditAccessoryIdPath));
			created.Add(_backupFile(locations.EditAccnamePath));
			return created;
		}

		public static void ApplySave(
			IList<CustomAccessoryEntry> entries,
			CustomAccessoryLubLocations locations,
			GrfHolder grf,
			CustomAccessoryManagerSaveMode mode) {
			if (locations == null || !locations.IsValid)
				throw new InvalidOperationException(locations?.GetMissingFilesMessage() ?? "Arquivos Lua inválidos.");

			var selected = entries.Where(p => p != null && p.Selected).ToList();
			if (selected.Count == 0)
				return;

			CustomAccessoryLuaService.RefreshEntriesFromLuaFiles(selected, locations);

			CreateBackups(locations);

			CustomAccessoryLuaWriter.ApplyEntries(
				locations.EditAccessoryIdPath,
				locations.EditAccnamePath,
				selected);

			CustomAccessoryLuaWriter.ValidateWrittenEntries(
				locations.EditAccessoryIdPath,
				locations.EditAccnamePath,
				selected);

			switch (mode) {
				case CustomAccessoryManagerSaveMode.EditFilesOnly:
					break;

				case CustomAccessoryManagerSaveMode.GrfInternal:
					if (grf == null || grf.IsClosed)
						throw new InvalidOperationException("Abra um GRF para aplicar entradas no container.");
					locations.CommitToGrf(grf);
					break;

				case CustomAccessoryManagerSaveMode.ExternalDisk:
					locations.CommitToExternalDisk();
					break;

				case CustomAccessoryManagerSaveMode.GrfAndExternalDisk:
					if (grf == null || grf.IsClosed)
						throw new InvalidOperationException("Abra um GRF para aplicar entradas no container.");
					locations.CommitToGrf(grf);
					locations.CommitToExternalDisk();
					break;

				default:
					throw new ArgumentOutOfRangeException(nameof(mode), mode, null);
			}
		}

		private static string _readText(string path) {
			return EncodingService.DisplayEncoding.GetString(File.ReadAllBytes(path));
		}

		private static string _backupFile(string path) {
			if (string.IsNullOrEmpty(path) || !File.Exists(path))
				return null;

			var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
			var backupPath = path + "." + stamp + ".bak";
			File.Copy(path, backupPath, overwrite: false);
			return backupPath;
		}
	}
}
