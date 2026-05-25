using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using GRF.Core;
using GRF.IO;
using GRF.Threading;
using GRFEditor.Tools.CustomAccessory;
using Utilities.Extension;

namespace GRFEditor.Core.AccessoryScanner {
	public sealed class AccessoryScannerService {
		public AccessoryScanResult Scan(
			AccessoryScannerInput input,
			IProgress progress = null,
			CancellationToken cancellationToken = default(CancellationToken)) {
			if (input == null)
				throw new ArgumentNullException(nameof(input));

			bool hasGrf = input.Grf != null && input.Grf.IsOpened;
			bool hasLocal = !String.IsNullOrWhiteSpace(input.LocalSpriteFolder) && Directory.Exists(input.LocalSpriteFolder);

			if (!hasGrf && !hasLocal)
				throw new InvalidOperationException("Informe um GRF aberto ou uma pasta local de sprites.");

			var result = new AccessoryScanResult {
				ScannedAtUtc = DateTime.UtcNow,
				AccessoryIdSource = input.AccessoryIdPath ?? "",
				AccnameSource = input.AccnamePath ?? "",
				GrfPath = hasGrf ? input.Grf.FileName : "",
				LocalSpriteFolder = hasLocal ? Path.GetFullPath(input.LocalSpriteFolder) : "",
			};

			var lubIndex = AccessoryLubIndex.Load(input.Grf, input.AccessoryIdPath, input.AccnamePath);

			if (lubIndex.AccessoryIds.Count == 0 && lubIndex.Accnames.Count == 0)
				result.Messages.Add("Nenhuma entrada carregada de accessoryid.lub / accname.lub.");

			var sprPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			var actPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

			if (hasGrf)
				_collectFromGrf(input.Grf, sprPaths, actPaths, progress, cancellationToken);

			if (hasLocal)
				_collectFromLocalFolder(input.LocalSpriteFolder, sprPaths, actPaths, progress, cancellationToken);

			var spriteList = CustomAccessorySpritePaths.FilterDuplicateUnprefixedSprites(sprPaths.ToList())
				.OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
				.ToList();

			int total = spriteList.Count + lubIndex.AccessoryIds.Count;
			int processed = 0;
			var scannedConstants = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

			foreach (string sprPath in spriteList) {
				ThrowIfCancelled(cancellationToken);
				_reportProgress(progress, ++processed, total);

				var entry = _scanSprite(sprPath, actPaths, lubIndex);
				result.Entries.Add(entry);
				scannedConstants.Add(entry.ConstantName);
			}

			foreach (string constant in lubIndex.AccessoryIds.Keys
				.Union(lubIndex.Accnames.Keys, StringComparer.OrdinalIgnoreCase)
				.OrderBy(c => c, StringComparer.OrdinalIgnoreCase)) {
				ThrowIfCancelled(cancellationToken);
				_reportProgress(progress, ++processed, total);

				if (scannedConstants.Contains(constant))
					continue;

				var entry = _scanLubOnly(constant, lubIndex);
				result.Entries.Add(entry);
			}

			_reportProgress(progress, total, total, forceComplete: true);

			result.Entries = result.Entries
				.OrderBy(e => e.ConstantName ?? "", StringComparer.OrdinalIgnoreCase)
				.ThenBy(e => e.SpritePath ?? "", StringComparer.OrdinalIgnoreCase)
				.ToList();

			return result;
		}

		private static void _collectFromGrf(
			GrfHolder grf,
			HashSet<string> sprPaths,
			HashSet<string> actPaths,
			IProgress progress,
			CancellationToken cancellationToken) {
			var prefixes = CustomAccessorySpritePaths.GetConfiguredPrefixes().ToList();

			foreach (var file in grf.FileTable.GetFiles("", "*", SearchOption.AllDirectories)) {
				ThrowIfCancelled(cancellationToken);

				string path = CustomAccessorySpritePaths.NormalizeGrfPath(file);

				if (path.EndsWith(".spr", StringComparison.OrdinalIgnoreCase)) {
					if (prefixes.Count == 0 || CustomAccessorySpritePaths.MatchesAnyPrefix(path, prefixes))
						sprPaths.Add(path);
				}
				else if (path.EndsWith(".act", StringComparison.OrdinalIgnoreCase)) {
					if (prefixes.Count == 0 || CustomAccessorySpritePaths.MatchesAnyPrefix(path, prefixes))
						actPaths.Add(path);
				}
			}
		}

		private static void _collectFromLocalFolder(
			string rootFolder,
			HashSet<string> sprPaths,
			HashSet<string> actPaths,
			IProgress progress,
			CancellationToken cancellationToken) {
			string root = Path.GetFullPath(rootFolder);

			foreach (string file in Directory.GetFiles(root, "*.*", SearchOption.AllDirectories)) {
				ThrowIfCancelled(cancellationToken);

				string ext = Path.GetExtension(file);
				if (!ext.IsExtension(".spr", ".act"))
					continue;

				string relative = _toRelativePath(root, file);
				string normalized = relative.Replace('\\', '/');

				if (ext.Equals(".spr", StringComparison.OrdinalIgnoreCase))
					sprPaths.Add(normalized);
				else
					actPaths.Add(normalized);
			}
		}

		private static AccessoryScanEntry _scanSprite(
			string sprPath,
			HashSet<string> actPaths,
			AccessoryLubIndex lubIndex) {
			string constant = CustomAccessoryNaming.FromSpritePath(sprPath);
			string actPath = _actPathForSprite(sprPath);
			bool hasAct = actPaths.Contains(actPath);

			var entry = new AccessoryScanEntry {
				SpritePath = sprPath,
				ActPath = hasAct ? actPath : "",
				ConstantName = constant,
			};

			_applyLubData(entry, lubIndex);
			_applyPathValidation(entry);
			_applyFilePresence(entry, hasAct: hasAct, hasSpr: true);
			_applyDuplicateFlags(entry, lubIndex);
			_finalizeStatus(entry, lubIndex);

			return entry;
		}

		private static AccessoryScanEntry _scanLubOnly(string constant, AccessoryLubIndex lubIndex) {
			var entry = new AccessoryScanEntry {
				ConstantName = CustomAccessoryNaming.NormalizeConstantName(constant),
				Status = AccessoryScanStatus.MissingSpr,
			};

			_applyLubData(entry, lubIndex);
			_applyDuplicateFlags(entry, lubIndex);
			entry.Issues.Add("Constante presente nos .lub, mas nenhum .spr correspondente foi encontrado.");

			if (lubIndex.DuplicateConstants.Contains(entry.ConstantName))
				entry.Status = AccessoryScanStatus.DuplicateConstant;

			if (entry.ViewId.HasValue && lubIndex.DuplicateViewIds.Contains(entry.ViewId.Value))
				entry.Status = AccessoryScanStatus.DuplicateViewId;

			return entry;
		}

		private static void _applyLubData(AccessoryScanEntry entry, AccessoryLubIndex lubIndex) {
			int viewId;
			if (lubIndex.TryGetViewId(entry.ConstantName, out viewId))
				entry.ViewId = viewId;

			string displayName;
			if (lubIndex.TryGetDisplayName(entry.ConstantName, out displayName))
				entry.DisplayName = displayName;
		}

		private static void _applyPathValidation(AccessoryScanEntry entry) {
			if (String.IsNullOrWhiteSpace(entry.SpritePath))
				return;

			var prefixes = CustomAccessorySpritePaths.GetConfiguredPrefixes().ToList();

			if (prefixes.Count > 0 && !CustomAccessorySpritePaths.MatchesAnyPrefix(entry.SpritePath, prefixes)) {
				entry.Status = AccessoryScanStatus.InvalidPath;
				entry.Issues.Add("Caminho fora das pastas de sprite de acessório configuradas.");
			}
			else if (!entry.SpritePath.StartsWith("data/sprite", StringComparison.OrdinalIgnoreCase)
				&& !entry.SpritePath.Contains("/sprite/")
				&& !entry.SpritePath.Contains("\\sprite\\")) {
				entry.Issues.Add("Caminho não segue o padrão data/sprite/ de costumes.");
			}
		}

		private static void _applyFilePresence(AccessoryScanEntry entry, bool hasAct, bool hasSpr) {
			if (!hasAct) {
				entry.Issues.Add("Arquivo .act correspondente não encontrado.");
				if (entry.Status == AccessoryScanStatus.New
					|| entry.Status == AccessoryScanStatus.Existing
					|| entry.Status == AccessoryScanStatus.MissingAccessoryId
					|| entry.Status == AccessoryScanStatus.MissingAccName)
					entry.Status = AccessoryScanStatus.MissingAct;
			}

			if (!hasSpr && entry.Status != AccessoryScanStatus.MissingSpr)
				entry.Status = AccessoryScanStatus.MissingSpr;
		}

		private static void _applyDuplicateFlags(AccessoryScanEntry entry, AccessoryLubIndex lubIndex) {
			if (lubIndex.DuplicateConstants.Contains(entry.ConstantName)) {
				entry.Issues.Add("Constante duplicada em accessoryid.lub.");
				entry.Status = AccessoryScanStatus.DuplicateConstant;
			}

			if (entry.ViewId.HasValue && lubIndex.DuplicateViewIds.Contains(entry.ViewId.Value)) {
				entry.Issues.Add("ViewId duplicado em accessoryid.lub.");
				entry.Status = AccessoryScanStatus.DuplicateViewId;
			}
		}

		private static void _finalizeStatus(AccessoryScanEntry entry, AccessoryLubIndex lubIndex) {
			if (entry.Status == AccessoryScanStatus.InvalidPath
				|| entry.Status == AccessoryScanStatus.DuplicateConstant
				|| entry.Status == AccessoryScanStatus.DuplicateViewId
				|| entry.Status == AccessoryScanStatus.MissingAct
				|| entry.Status == AccessoryScanStatus.MissingSpr)
				return;

			bool hasId = lubIndex.HasAccessoryId(entry.ConstantName);
			bool hasName = lubIndex.HasAccname(entry.ConstantName);

			if (hasId && hasName)
				entry.Status = AccessoryScanStatus.Existing;
			else if (!hasId && !hasName)
				entry.Status = AccessoryScanStatus.New;
			else if (!hasId)
				entry.Status = AccessoryScanStatus.MissingAccessoryId;
			else
				entry.Status = AccessoryScanStatus.MissingAccName;
		}

		private static string _actPathForSprite(string sprPath) {
			string normalized = sprPath.Replace('\\', '/');
			int lastDot = normalized.LastIndexOf('.');

			if (lastDot < 0)
				return normalized + ".act";

			return normalized.Substring(0, lastDot) + ".act";
		}

		private static string _toRelativePath(string root, string fullPath) {
			string relative = fullPath.Substring(root.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
			return relative.Replace('\\', '/');
		}

		private static void _reportProgress(IProgress progress, int current, int total, bool forceComplete = false) {
			if (progress == null)
				return;

			if (total <= 0) {
				progress.Progress = forceComplete ? 100f : 0f;
				return;
			}

			progress.Progress = forceComplete
				? 100f
				: Math.Min(99f, current / (float) total * 100f);
		}

		private static void ThrowIfCancelled(CancellationToken cancellationToken) {
			if (cancellationToken.IsCancellationRequested)
				throw new OperationCanceledException();
		}
	}
}
