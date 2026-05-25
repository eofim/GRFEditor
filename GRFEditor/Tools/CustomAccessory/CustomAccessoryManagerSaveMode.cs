namespace GRFEditor.Tools.CustomAccessory {
	public enum CustomAccessoryManagerSaveMode {
		/// <summary>Grava apenas nos arquivos de edição (cache/disco de trabalho).</summary>
		EditFilesOnly,
		/// <summary>Aplica ao GRF aberto via AddFile; não chama Save no container.</summary>
		GrfInternal,
		/// <summary>Copia para caminhos externos do perfil/configuração.</summary>
		ExternalDisk,
		/// <summary>GRF interno + cópia em disco externo (quando configurado).</summary>
		GrfAndExternalDisk,
	}
}
