namespace GRFEditor.Core.RagnarokValidation {
	internal static class RagnarokValidationStrings {
		public const string MissingSprForAct = "ACT file has no matching SPR with the same base name.";
		public const string MissingActForSpr = "SPR file has no matching ACT with the same base name.";
		public const string EmptyFile = "File is empty (0 bytes).";
		public const string JunkDb = "Hidden thumbnail database file; remove from the GRF.";
		public const string JunkThumbsDb = "Windows Thumbs.db file; remove from the GRF.";
		public const string JunkDesktopIni = "Windows Desktop.ini file; remove from the GRF.";
		public const string JunkSvn = "Subversion metadata file; remove from the GRF.";
		public const string RootFile = "File is at the root of the container; the Ragnarok client expects files under data\\.";
		public const string SuspiciousPathChars = "Path contains suspicious or invalid characters for client file lookup.";
		public const string PathSlashNormalization = "Path uses forward slashes; the client expects backslashes in GRF paths.";
		public const string DuplicatePath = "The same path appears {0} times in the file table.";
		public const string UnknownExtensionInFolder = "Unexpected extension '{0}' in folder '{1}' for client resources.";

		public const string FixRemoveFile = "Remove this entry from the GRF.";
		public const string FixAddSpr = "Add the matching SPR file or remove the orphan ACT.";
		public const string FixAddAct = "Add the matching ACT file or remove the unused SPR.";
		public const string FixMoveToData = "Move the file under data\\ (e.g. data\\...).";
		public const string FixRenamePath = "Rename the path to use only safe ASCII characters and backslashes.";
		public const string FixNormalizeSlashes = "Rename the path to use backslashes instead of forward slashes.";
		public const string FixDeduplicate = "Remove duplicate entries, keeping a single copy of the path.";
		public const string FixUseKnownExtension = "Use a standard Ragnarok extension for this folder or move the file elsewhere.";

		public const string AccessoryDuplicateConstant = "Duplicate ACCESSORY_ constant '{0}' in {1}.";
		public const string AccessoryDuplicateViewId = "Duplicate ViewID {0} used by {1} and {2} in {3}.";
		public const string AccessoryMissingAccname = "ACCESSORY_ constant defined in accessoryid but missing in accname.";
		public const string AccessoryMissingAccessoryId = "ACCESSORY_ constant defined in accname but missing in accessoryid.";
		public const string AccessorySpriteNotFound = "No matching .spr found in the GRF for this accessory constant (heuristic).";
		public const string AccessoryIteminfoViewId = "iteminfo references ViewID/ClassNum {0} which is not defined in accessoryid.lub.";
		public const string AccessorySuspiciousLine = "Line looks like accessory data but does not match the expected format.";
		public const string AccessoryNonNumericId = "ACCESSORY_ assignment uses a non-numeric ViewID.";
		public const string AccessoryIdsOutOfOrder = "ViewIDs appear out of ascending order in several places (may be intentional).";

		public const string FixSyncAccname = "Add the matching accname entry or remove the accessoryid line.";
		public const string FixSyncAccessoryId = "Add the matching accessoryid entry or remove the accname line.";
		public const string FixAddSprite = "Add the costume .spr/.act under data\\sprite or fix the constant name.";
		public const string FixIteminfoViewId = "Set ClassNum/View to a valid ViewID from accessoryid.lub.";
		public const string FixReviewLuaLine = "Review and fix the Lua/Lub line syntax.";
	}
}
