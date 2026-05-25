using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using GRF.Core;
using GRFEditor.ApplicationConfiguration;
using TokeiLibrary.WPF;

namespace GRFEditor.Tools.CustomAccessory {
	public enum CustomAccessoryBeforeSaveAction {
		ContinueSave,
		CancelSave,
	}

	public static class CustomAccessorySaveHelper {
		private static readonly HashSet<string> KnownSprites = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

		public static void ResetKnownSprites() {
			KnownSprites.Clear();
		}

		public static void SnapshotSprites(GrfHolder grf) {
			ResetKnownSprites();

			foreach (var path in CustomAccessoryLuaService.FindSpritePathsInGrf(grf))
				KnownSprites.Add(path);
		}

		public static List<string> FindCandidateSprites(GrfHolder grf) {
			return CustomAccessoryLuaService.FindCandidateSprites(grf, KnownSprites);
		}

		public static CustomAccessoryBeforeSaveAction TryPromptBeforeSave(GrfHolder grf, Window owner) {
			if (owner != null && !owner.Dispatcher.CheckAccess()) {
				return (CustomAccessoryBeforeSaveAction)owner.Dispatcher.Invoke(
					new Func<CustomAccessoryBeforeSaveAction>(() => TryPromptBeforeSave(grf, owner)));
			}

			if (!GrfEditorConfiguration.CustomAccessoryPromptOnSave)
				return CustomAccessoryBeforeSaveAction.ContinueSave;

			if (grf == null || grf.IsClosed)
				return CustomAccessoryBeforeSaveAction.ContinueSave;

			var locations = CustomAccessoryLubLocations.Resolve(grf);
			if (!locations.IsValid)
				return CustomAccessoryBeforeSaveAction.ContinueSave;

			var newSprites = FindCandidateSprites(grf);
			if (newSprites.Count == 0)
				return CustomAccessoryBeforeSaveAction.ContinueSave;

			var modeDescription = GrfEditorConfiguration.CustomAccessoryDetectionMode == CustomAccessoryNewSpriteDetectionMode.MissingFromLua
				? "sem entrada completa em accessoryid.lub e accname.lub (novo ou incompleto)"
				: "adicionados desde que o GRF foi aberto (ou desde o último aviso)";

			var message = "Foram detectados " + newSprites.Count + " sprite(s) na pasta configurada (" + modeDescription + ").\n\n"
				+ "Deseja gerar ou atualizar accessoryid.lub e accname.lub antes de salvar o GRF?";

			var result = MessageBox.Show(owner, message, "Custom items (Lua)", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);

			if (result == MessageBoxResult.Cancel)
				return CustomAccessoryBeforeSaveAction.CancelSave;

			if (result == MessageBoxResult.Yes) {
				var dialog = new CustomAccessoryWizardDialog(grf, newSprites) { Owner = owner };
				WindowProvider.ShowWindow(dialog, owner);

				if (dialog.DialogResult == true)
					AcknowledgeCandidates(grf);
			}
			else if (result == MessageBoxResult.No) {
				AcknowledgeCandidates(grf);
			}

			return CustomAccessoryBeforeSaveAction.ContinueSave;
		}

		private static void AcknowledgeCandidates(GrfHolder grf) {
			if (GrfEditorConfiguration.CustomAccessoryDetectionMode != CustomAccessoryNewSpriteDetectionMode.AddedSinceSnapshot)
				return;

			foreach (var path in CustomAccessoryLuaService.FindSpritePathsInGrf(grf))
				KnownSprites.Add(path);
		}

		public static string GetEmptyCandidatesMessage() {
			if (GrfEditorConfiguration.CustomAccessoryDetectionMode == CustomAccessoryNewSpriteDetectionMode.MissingFromLua)
				return "Todos os sprites da pasta configurada já possuem entrada completa em accessoryid.lub e accname.lub.";

			return "Nenhum sprite novo foi detectado desde que o GRF foi aberto (modo: arquivos adicionados).";
		}
	}
}
