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

		/// <summary>Cópia de trabalho usada pelo editor (cache temporário ou arquivo em disco).</summary>
		public string EditAccessoryIdPath { get; private set; }

		public string EditAccnamePath { get; private set; }

		/// <summary>Caminho dentro do GRF (quando existir ou ao gravar).</summary>
		public string AccessoryIdGrfPath { get; private set; }

		public string AccnameGrfPath { get; private set; }

		/// <summary>Destino opcional em disco (espelho), quando habilitado nas configurações.</summary>
		public string ExternalAccessoryIdPath { get; private set; }

		public string ExternalAccnamePath { get; private set; }

		public bool IsGrfPrimary { get; private set; }

		public bool WritesToExternalDisk {
			get { return GrfEditorConfiguration.CustomAccessoryAlsoWriteToDisk && HasExternalDiskTargets; }
		}

		public bool HasExternalDiskTargets {
			get {
				return !string.IsNullOrEmpty(ExternalAccessoryIdPath) || !string.IsNullOrEmpty(ExternalAccnamePath);
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

		public static CustomAccessoryLubLocations Resolve(GrfHolder grf = null) {
			var locations = new CustomAccessoryLubLocations();
			var grfOpen = grf != null && !grf.IsClosed;

			string accessoryIdInGrf = grfOpen ? FindInGrf(grf, AccessoryIdFileName) : null;
			string accnameInGrf = grfOpen ? FindInGrf(grf, AccnameFileName) : null;

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
					locations.AccessoryIdGrfPath = accessoryIdInGrf ?? DefaultAccessoryIdGrfPath;
					locations.AccnameGrfPath = accnameInGrf ?? DefaultAccnameGrfPath;
				}

				return locations;
			}

			locations.EditAccessoryIdPath = accessoryIdDisk;
			locations.EditAccnamePath = accnameDisk;
			return locations;
		}

		public void Commit(GrfHolder grf) {
			if (!IsValid)
				return;

			CommitToGrf(grf);
			CommitToExternalDisk();
		}

		public void CommitToGrf(GrfHolder grf) {
			if (grf == null || grf.IsClosed || !IsValid)
				return;

			var accessoryIdTarget = AccessoryIdGrfPath ?? DefaultAccessoryIdGrfPath;
			var accnameTarget = AccnameGrfPath ?? DefaultAccnameGrfPath;

			grf.Commands.AddFile(accessoryIdTarget, File.ReadAllBytes(EditAccessoryIdPath));
			grf.Commands.AddFile(accnameTarget, File.ReadAllBytes(EditAccnamePath));

			AccessoryIdGrfPath = accessoryIdTarget;
			AccnameGrfPath = accnameTarget;
			IsGrfPrimary = true;
		}

		public void CommitToExternalDisk() {
			if (!GrfEditorConfiguration.CustomAccessoryAlsoWriteToDisk || !IsValid)
				return;

			CopyToExternal(EditAccessoryIdPath, ExternalAccessoryIdPath);
			CopyToExternal(EditAccnamePath, ExternalAccnamePath);
		}

		public string GetSourceDescription() {
			if (!IsValid)
				return "";

			if (IsGrfPrimary)
				return "Fonte: GRF aberto" + (WritesToExternalDisk ? " (+ cópia em disco)" : "");

			if (!string.IsNullOrEmpty(AccessoryIdGrfPath))
				return "Fonte: disco (será adicionado/atualizado no GRF ao gravar)" + (WritesToExternalDisk ? "" : "");

			return "Fonte: arquivos em disco";
		}

		public string GetSuccessMessage() {
			var lines = new List<string>();

			if (IsGrfPrimary || !string.IsNullOrEmpty(AccessoryIdGrfPath)) {
				lines.Add("Alterações aplicadas ao GRF aberto.");
				lines.Add("Caminhos: " + (AccessoryIdGrfPath ?? DefaultAccessoryIdGrfPath));
				lines.Add("         " + (AccnameGrfPath ?? DefaultAccnameGrfPath));
				lines.Add("");
				lines.Add("Salve o GRF para persistir as mudanças.");
			}

			if (WritesToExternalDisk) {
				lines.Add("");
				lines.Add("Cópia em disco:");
				if (!string.IsNullOrEmpty(ExternalAccessoryIdPath))
					lines.Add(ExternalAccessoryIdPath);
				if (!string.IsNullOrEmpty(ExternalAccnamePath))
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

		private static void CopyToExternal(string sourcePath, string targetPath) {
			if (string.IsNullOrEmpty(targetPath) || string.IsNullOrEmpty(sourcePath) || !File.Exists(sourcePath))
				return;

			var directory = Path.GetDirectoryName(targetPath);
			if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
				Directory.CreateDirectory(directory);

			File.Copy(sourcePath, targetPath, true);
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

		private static string FindInGrf(GrfHolder grf, string fileName) {
			if (grf == null || grf.IsClosed)
				return null;

			string fallback = null;
			foreach (var file in grf.FileTable.GetFiles("", "*", SearchOption.AllDirectories)) {
				if (!string.Equals(Path.GetFileName(file), fileName, StringComparison.OrdinalIgnoreCase))
					continue;

				if (file.IndexOf(@"datainfo\", StringComparison.OrdinalIgnoreCase) >= 0
					|| file.IndexOf(@"datainfo/", StringComparison.OrdinalIgnoreCase) >= 0)
					return file.Replace('/', '\\');

				if (fallback == null)
					fallback = file.Replace('/', '\\');
			}

			return fallback;
		}

		private static string ExtractFromGrf(GrfHolder grf, string grfRelativePath) {
			var entry = grf.FileTable[grfRelativePath];
			var data = entry.GetDecompressedData();

			var cacheDir = Path.Combine(GrfEditorConfiguration.TempPath, "CustomAccessoryLub");
			Directory.CreateDirectory(cacheDir);

			var cacheName = GrfPath.GetFileName(grf.FileName) + "_" + grfRelativePath.Replace('\\', '_').Replace('/', '_');
			var diskPath = Path.Combine(cacheDir, cacheName);

			File.WriteAllBytes(diskPath, data);
			return diskPath;
		}
	}
}
