using System;

using System.Collections.Generic;

using System.Linq;

using System.Windows;

using GRF.Core;

using GRFEditor.ApplicationConfiguration;

using TokeiLibrary.WPF;



namespace GRFEditor.Tools.CustomAccessory {

	public static class CustomAccessoryPostSaveHelper {

		private static readonly HashSet<string> KnownSprites = new HashSet<string>(StringComparer.OrdinalIgnoreCase);



		public static bool PendingPromptAfterLoad { get; set; }



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



		public static void TryPromptAfterSave(GrfHolder grf, Window owner) {
			if (owner != null && !owner.Dispatcher.CheckAccess()) {
				owner.Dispatcher.BeginInvoke(new Action(() => TryPromptAfterSave(grf, owner)));
				return;
			}

			if (!GrfEditorConfiguration.CustomAccessoryPromptOnSave)
				return;

			if (grf == null || grf.IsClosed)
				return;



			var newSprites = FindCandidateSprites(grf);

			if (newSprites.Count == 0)

				return;



			var modeDescription = GrfEditorConfiguration.CustomAccessoryDetectionMode == CustomAccessoryNewSpriteDetectionMode.MissingFromLua

				? "sem entrada em accessoryid.lub / accname.lub"

				: "adicionados desde que o GRF foi aberto (ou desde o último aviso)";



			var message = "Foram detectados " + newSprites.Count + " sprite(s) na pasta configurada (" + modeDescription + ").\n\n"

				+ "Deseja gerar ou atualizar accessoryid.lub e accname.lub?";



			var result = MessageBox.Show(owner, message, "Custom items (Lua)", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);



			if (result == MessageBoxResult.Yes) {

				var dialog = new CustomAccessoryWizardDialog(grf, newSprites) { Owner = owner };

				WindowProvider.ShowWindow(dialog, owner);

			}



			if (GrfEditorConfiguration.CustomAccessoryDetectionMode == CustomAccessoryNewSpriteDetectionMode.AddedSinceSnapshot) {

				foreach (var path in CustomAccessoryLuaService.FindSpritePathsInGrf(grf))

					KnownSprites.Add(path);

			}

		}



		public static string GetEmptyCandidatesMessage() {

			if (GrfEditorConfiguration.CustomAccessoryDetectionMode == CustomAccessoryNewSpriteDetectionMode.MissingFromLua)

				return "Todos os sprites da pasta configurada já possuem entrada nos arquivos Lua.";



			return "Nenhum sprite novo foi detectado desde que o GRF foi aberto (modo: arquivos adicionados).";

		}

	}

}


