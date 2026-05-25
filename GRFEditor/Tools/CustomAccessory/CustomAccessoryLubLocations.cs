using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using GRF.Core;
using GRF.IO;
using GRFEditor.ApplicationConfiguration;

namespace GRFEditor.Tools.CustomAccessory {
	public sealed class CustomAccessoryLubLocations {
		public const string AccessoryIdFileName = "accessoryid.lub";
		public const string AccnameFileName = "accname.lub";

		public const string DefaultAccessoryIdGrfPath = @"data\luafiles514\lua files\datainfo\accessoryid.lub";
		public const string DefaultAccnameGrfPath = @"data\luafiles514\lua files\datainfo\accname.lub";

		public static readonly string[] AccessoryIdDiskRelativeCandidates = {
			@"data\luafiles514\lua files\datainfo\accessoryid.lub",
			@"luafiles514\lua files\datainfo\accessoryid.lub",
		};

		public static readonly string[] AccnameDiskRelativeCandidates = {
			@"data\luafiles514\lua files\datainfo\accname.lub",
			@"luafiles514\lua files\datainfo\accname.lub",
		};

		public string EditAccessoryIdPath { get; private set; }
		public string EditAccnamePath { get; private set; }
		public string AccessoryIdGrfPath { get; private set; }
		public string AccnameGrfPath { get; private set; }
		public string ExternalAccessoryIdPath { get; private set; }
		public string ExternalAccnamePath { get; private set; }
		public bool IsGrfPrimary { get; private set; }

		public bool WritesToExternalDisk {
			get { return GrfEditorConfiguration.CustomAccessoryAlsoWriteToDisk; }
		}

		public bool HasExternalDiskTargets {
			get {
				return !string.IsNullOrEmpty(ExternalAccessoryIdPath) && !string.IsNullOrEmpty(ExternalAccnamePath);
			}
		}

		public bool CanWriteToGrf {
			get { return !string.IsNullOrEmpty(AccessoryIdGrfPath) || IsGrfPrimary; }
		}

		public bool IsValid {
			get {
				return !string.IsNullOrEmpty(EditAccessoryIdPath) && File.Exists(EditAccessoryIdPath)
					&& !string.IsNullOrEmpty(EditAccnamePath) && File.Exists(EditAccnamePath);
			}
		}

		public static string NormalizeGrfInternalPath(string path) {
			if (string.IsNullOrWhiteSpace(path))
				return null;

			return GrfPath.CleanGrfPath(path);
		}

		public static CustomAccessoryLubLocations Resolve(GrfHolder grf = null) {
			var locations = new CustomAccessoryLubLocations();
			var grfOpen = grf != null && !grf.IsClosed;

			string accessoryIdInGrf = grfOpen ? FindExactGrfEntryPath(grf, AccessoryIdFileName) : null;
			string accnameInGrf = grfOpen ? FindExactGrfEntryPath(grf, AccnameFileName) : null;

			if (!string.IsNullOrEmpty(accessoryIdInGrf) && !string.IsNullOrEmpty(accnameInGrf)) {
				locations.IsGrfPrimary = true;
				locations.AccessoryIdGrfPath = accessoryIdInGrf;
				locations.AccnameGrfPath = accnameInGrf;
				locations.EditAccessoryIdPath = ExtractFromGrf(grf, accessoryIdInGrf);
				locations.EditAccnamePath = ExtractFromGrf(grf, accnameInGrf);
				locations.ResolveExternalDiskTargets();
				return locations;
			}

			var accessoryIdDisk = ResolveDiskOnlyPath(
				GrfEditorConfiguration.CustomAccessoryIdLubPath,
				AccessoryIdDiskRelativeCandidates);
			var accnameDisk = ResolveDiskOnlyPath(
				GrfEditorConfiguration.CustomAccessoryAccnameLubPath,
				AccnameDiskRelativeCandidates);

			if (!string.IsNullOrEmpty(accessoryIdDisk) && File.Exists(accessoryIdDisk)
				&& !string.IsNullOrEmpty(accnameDisk) && File.Exists(accnameDisk)) {
				locations.IsGrfPrimary = false;
				locations.EditAccessoryIdPath = accessoryIdDisk;
				locations.EditAccnamePath = accnameDisk;
				locations.ExternalAccessoryIdPath = accessoryIdDisk;
				locations.ExternalAccnamePath = accnameDisk;

				if (grfOpen) {
					locations.AccessoryIdGrfPath = accessoryIdInGrf ?? NormalizeGrfInternalPath(DefaultAccessoryIdGrfPath);
					locations.AccnameGrfPath = accnameInGrf ?? NormalizeGrfInternalPath(DefaultAccnameGrfPath);
				}

				return locations;
			}

			locations.EditAccessoryIdPath = accessoryIdDisk;
			locations.EditAccnamePath = accnameDisk;
			return locations;
		}

		public void Commit(GrfHolder grf) {
			if (!IsValid)
				throw new InvalidOperationException(GetMissingFilesMessage());

			if (grf != null && !grf.IsClosed)
				CommitToGrf(grf);

			CommitToExternalDisk();
		}

		public void CommitToGrf(GrfHolder grf) {
			if (grf == null || grf.IsClosed)
				throw new InvalidOperationException("Não há GRF aberto para aplicar accessoryid.lub e accname.lub.");

			if (!IsValid)
				throw new InvalidOperationException(GetMissingFilesMessage());

			var accessoryIdTarget = ResolveCommitGrfPath(grf, AccessoryIdGrfPath, DefaultAccessoryIdGrfPath, AccessoryIdFileName);
			var accnameTarget = ResolveCommitGrfPath(grf, AccnameGrfPath, DefaultAccnameGrfPath, AccnameFileName);

			byte[] accessoryIdBytes = ReadEditFileBytes(EditAccessoryIdPath, AccessoryIdFileName);
			byte[] accnameBytes = ReadEditFileBytes(EditAccnamePath, AccnameFileName);

			try {
				grf.Commands.AddFile(accessoryIdTarget, accessoryIdBytes);
			}
			catch (Exception ex) {
				throw new InvalidOperationException(
					"Falha ao adicionar " + AccessoryIdFileName + " ao GRF em:\n" + accessoryIdTarget + "\n" + ex.Message,
					ex);
			}

			try {
				grf.Commands.AddFile(accnameTarget, accnameBytes);
			}
			catch (Exception ex) {
				throw new InvalidOperationException(
					"Falha ao adicionar " + AccnameFileName + " ao GRF em:\n" + accnameTarget + "\n" + ex.Message,
					ex);
			}

			AccessoryIdGrfPath = accessoryIdTarget;
			AccnameGrfPath = accnameTarget;
			IsGrfPrimary = true;
		}

		public void CommitToExternalDisk() {
			if (!WritesToExternalDisk || !IsValid)
				return;

			EnsureExternalDiskTargetsConfigured();

			CopyToExternal(EditAccessoryIdPath, ExternalAccessoryIdPath, AccessoryIdFileName);
			CopyToExternal(EditAccnamePath, ExternalAccnamePath, AccnameFileName);
		}

		public string GetSourceDescription() {
			if (!IsValid)
				return "";

			if (IsGrfPrimary)
				return "Fonte: GRF aberto (cache temporário)" + (WritesToExternalDisk ? " (+ cópia em disco)" : "");

			if (!string.IsNullOrEmpty(AccessoryIdGrfPath))
				return "Fonte: disco (será adicionado/atualizado no GRF ao gravar)" + (WritesToExternalDisk ? "" : "");

			return "Fonte: arquivos em disco";
		}

		public string GetSuccessMessage() {
			var lines = new List<string>();

			if (IsGrfPrimary || !string.IsNullOrEmpty(AccessoryIdGrfPath)) {
				lines.Add("Alterações aplicadas ao GRF aberto (pendentes até o próximo salvamento).");
				lines.Add("Caminhos: " + (AccessoryIdGrfPath ?? NormalizeGrfInternalPath(DefaultAccessoryIdGrfPath)));
				lines.Add("         " + (AccnameGrfPath ?? NormalizeGrfInternalPath(DefaultAccnameGrfPath)));
			}

			if (WritesToExternalDisk && HasExternalDiskTargets) {
				lines.Add("");
				lines.Add("Cópia em disco:");
				lines.Add(ExternalAccessoryIdPath);
				lines.Add(ExternalAccnamePath);
			}
			else if (!IsGrfPrimary && string.IsNullOrEmpty(AccessoryIdGrfPath)) {
				lines.Add("Arquivos atualizados:");
				lines.Add(EditAccessoryIdPath);
				lines.Add(EditAccnamePath);
			}

			return string.Join(Environment.NewLine, lines.Where(p => p != null));
		}

		public string GetMissingFilesMessage() {
			var missing = new List<string>();
			if (string.IsNullOrEmpty(EditAccessoryIdPath) || !File.Exists(EditAccessoryIdPath))
				missing.Add(AccessoryIdFileName);
			if (string.IsNullOrEmpty(EditAccnamePath) || !File.Exists(EditAccnamePath))
				missing.Add(AccnameFileName);

			if (missing.Count == 0)
				return "";

			return string.Join(" e ", missing.ToArray()) + " não encontrado(s).\n\n"
				+ "Adicione accessoryid.lub e accname.lub ao GRF em data\\luafiles514\\lua files\\datainfo\\, "
				+ "ou ative \"Também gravar em disco\" e aponte os caminhos em Configurações → Custom accessories.";
		}

		private void ResolveExternalDiskTargets() {
			ExternalAccessoryIdPath = ResolveExternalPath(AccessoryIdFileName,
				GrfEditorConfiguration.CustomAccessoryIdLubPath,
				AccessoryIdDiskRelativeCandidates);
			ExternalAccnamePath = ResolveExternalPath(AccnameFileName,
				GrfEditorConfiguration.CustomAccessoryAccnameLubPath,
				AccnameDiskRelativeCandidates);
		}

		private void EnsureExternalDiskTargetsConfigured() {
			var missing = new List<string>();

			if (string.IsNullOrWhiteSpace(ExternalAccessoryIdPath))
				missing.Add(AccessoryIdFileName);

			if (string.IsNullOrWhiteSpace(ExternalAccnamePath))
				missing.Add(AccnameFileName);

			if (missing.Count == 0)
				return;

			throw new InvalidOperationException(
				"A opção \"Também gravar em disco\" está ativa, mas o caminho externo não foi definido para:\n"
				+ string.Join("\n", missing)
				+ "\n\nDefina os caminhos em Configurações → Custom accessories, "
				+ "ou coloque os arquivos em data\\luafiles514\\lua files\\datainfo\\ relativo ao diretório do editor.");
		}

		private static string ResolveCommitGrfPath(GrfHolder grf, string knownPath, string defaultPath, string fileName) {
			var exact = FindExactGrfEntryPath(grf, fileName);
			if (!string.IsNullOrEmpty(exact))
				return exact;

			return NormalizeGrfInternalPath(knownPath ?? defaultPath);
		}

		private static string ResolveExternalPath(string fileName, string configuredPath, string[] diskRelativeCandidates) {
			if (!string.IsNullOrWhiteSpace(configuredPath))
				return configuredPath;

			return FindOnDisk(diskRelativeCandidates);
		}

		private static string ResolveDiskOnlyPath(string configuredPath, string[] diskRelativeCandidates) {
			if (!string.IsNullOrWhiteSpace(configuredPath))
				return configuredPath;

			return FindOnDisk(diskRelativeCandidates);
		}

		private static byte[] ReadEditFileBytes(string editPath, string label) {
			try {
				if (string.IsNullOrEmpty(editPath) || !File.Exists(editPath))
					throw new FileNotFoundException("Arquivo de edição não encontrado: " + label, editPath);

				var bytes = File.ReadAllBytes(editPath);
				if (bytes == null || bytes.Length == 0)
					throw new InvalidOperationException(label + " está vazio após a edição.");

				return bytes;
			}
			catch (IOException ex) {
				throw new IOException("Falha ao ler " + label + " do cache de edição:\n" + editPath + "\n" + ex.Message, ex);
			}
		}

		private static void CopyToExternal(string sourcePath, string targetPath, string label) {
			try {
				if (string.IsNullOrWhiteSpace(targetPath))
					throw new InvalidOperationException("Caminho externo de " + label + " não definido.");

				if (string.IsNullOrEmpty(sourcePath) || !File.Exists(sourcePath))
					throw new FileNotFoundException("Arquivo de edição não encontrado para copiar " + label + ".", sourcePath);

				var directory = Path.GetDirectoryName(targetPath);
				if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
					Directory.CreateDirectory(directory);

				File.Copy(sourcePath, targetPath, true);
			}
			catch (Exception ex) when (!(ex is InvalidOperationException)) {
				throw new IOException("Falha ao copiar " + label + " para disco:\n" + targetPath + "\n" + ex.Message, ex);
			}
		}

		private static string FindOnDisk(string[] relativeCandidates) {
			foreach (var start in GetSearchRoots()) {
				var dir = start;
				for (int depth = 0; depth < 12 && !string.IsNullOrEmpty(dir); depth++) {
					foreach (var relative in relativeCandidates) {
						var full = Path.Combine(dir, relative);
						if (File.Exists(full))
							return full;
					}

					dir = Path.GetDirectoryName(dir);
				}
			}

			return null;
		}

		private static IEnumerable<string> GetSearchRoots() {
			var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

			string add(string path) {
				if (string.IsNullOrEmpty(path))
					return null;

				path = Path.GetFullPath(path);
				return seen.Add(path) ? path : null;
			}

			var cwd = add(Directory.GetCurrentDirectory());
			if (cwd != null)
				yield return cwd;

			var entry = Assembly.GetEntryAssembly();
			if (entry != null) {
				var fromExe = add(Path.GetDirectoryName(entry.Location));
				if (fromExe != null)
					yield return fromExe;
			}
		}

		private static string FindExactGrfEntryPath(GrfHolder grf, string fileName) {
			if (grf == null || grf.IsClosed)
				return null;

			string bestPath = null;
			int bestScore = -1;

			foreach (var entry in grf.FileTable.Entries) {
				if (entry.IsRemoved)
					continue;

				if (!string.Equals(GrfPath.GetFileName(entry.RelativePath), fileName, StringComparison.OrdinalIgnoreCase))
					continue;

				var path = entry.RelativePath;
				int score = 0;

				if (path.IndexOf(@"datainfo\", StringComparison.OrdinalIgnoreCase) >= 0
					|| path.IndexOf("datainfo/", StringComparison.OrdinalIgnoreCase) >= 0)
					score += 100;

				if (path.IndexOf(@"luafiles514\", StringComparison.OrdinalIgnoreCase) >= 0
					|| path.IndexOf("luafiles514/", StringComparison.OrdinalIgnoreCase) >= 0)
					score += 50;

				if (score > bestScore) {
					bestScore = score;
					bestPath = NormalizeGrfInternalPath(path);
				}
			}

			return bestPath;
		}

		private static FileEntry TryGetGrfEntry(GrfHolder grf, string grfRelativePath) {
			var cleanPath = NormalizeGrfInternalPath(grfRelativePath);
			if (string.IsNullOrEmpty(cleanPath))
				return null;

			if (grf.FileTable.ContainsFile(cleanPath))
				return grf.FileTable[cleanPath];

			foreach (var entry in grf.FileTable.Entries) {
				if (entry.IsRemoved)
					continue;

				if (string.Equals(NormalizeGrfInternalPath(entry.RelativePath), cleanPath, StringComparison.OrdinalIgnoreCase))
					return entry;
			}

			return null;
		}

		private static string ExtractFromGrf(GrfHolder grf, string grfRelativePath) {
			var entry = TryGetGrfEntry(grf, grfRelativePath);
			if (entry == null) {
				throw new FileNotFoundException(
					"Não foi possível localizar " + GrfPath.GetFileName(grfRelativePath) + " no GRF em:\n" + grfRelativePath,
					grfRelativePath);
			}

			byte[] data;
			try {
				data = entry.GetDecompressedData();
			}
			catch (Exception ex) {
				throw new IOException(
					"Falha ao ler " + GrfPath.GetFileName(grfRelativePath) + " do GRF:\n" + entry.RelativePath + "\n" + ex.Message,
					ex);
			}

			if (data == null || data.Length == 0)
				throw new InvalidOperationException(entry.RelativePath + " está vazio no GRF.");

			var cacheDir = Path.Combine(GrfEditorConfiguration.TempPath, "CustomAccessoryLub");
			Directory.CreateDirectory(cacheDir);

			var cacheName = GrfPath.GetFileName(grf.FileName) + "_" + entry.RelativePath.Replace('\\', '_').Replace('/', '_');
			var diskPath = Path.Combine(cacheDir, cacheName);

			try {
				File.WriteAllBytes(diskPath, data);
			}
			catch (Exception ex) {
				throw new IOException("Falha ao gravar cache temporário de " + entry.RelativePath + ":\n" + diskPath + "\n" + ex.Message, ex);
			}

			return diskPath;
		}
	}
}
