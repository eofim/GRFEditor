namespace GRFEditor.Tools.CustomAccessory {
	public enum CustomAccessoryNewSpriteDetectionMode {
		/// <summary>Sprite na pasta configurada sem constante ACCESSORY_* nos Lua.</summary>
		MissingFromLua = 0,

		/// <summary>Arquivo .spr que não existia no snapshot ao abrir/salvar o GRF (ex.: adicionado antes de salvar).</summary>
		AddedSinceSnapshot = 1,
	}
}
