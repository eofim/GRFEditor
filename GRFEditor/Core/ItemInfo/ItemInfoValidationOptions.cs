using System.Collections.Generic;
using GRF.Core;

namespace GRFEditor.Core.ItemInfo {
	public sealed class ItemInfoValidationOptions {
		/// <summary>ViewIds conhecidos (ex.: accessoryid.lub) para detectar ClassNum inexistente.</summary>
		public HashSet<int> KnownViewIds { get; set; }

		/// <summary>GRF aberto para checar texturas no container.</summary>
		public GrfHolder Grf { get; set; }

		/// <summary>Pasta do cliente (data/) para checar BMP em disco.</summary>
		public string ClientDataFolder { get; set; }

		/// <summary>Caminho relativo da pasta collection (padrão RO).</summary>
		public string CollectionTextureRelativePath { get; set; }

		/// <summary>Caminho relativo da pasta item (ícone).</summary>
		public string ItemTextureRelativePath { get; set; }

		public bool ValidateDuplicateItemIds { get; set; } = true;
		public bool ValidateDuplicateViewIds { get; set; } = true;
		public bool ValidateMissingViewIds { get; set; } = true;
		public bool ValidateTextures { get; set; } = true;

		public bool RequireIdentifiedResourceForTextures { get; set; } = true;
		public bool CheckItemIconTexture { get; set; } = true;
		public bool CheckCollectionTexture { get; set; } = true;
	}
}
