using System;
using System.Collections.Generic;
using GRFEditor.Core.ProjectProfiles;

namespace GRFEditor.Core.RagnarokValidation {
	public static class RagnarokValidationIgnoredRules {
		public static IReadOnlyList<string> GetActiveIgnoredRules() {
			var profile = ActiveProjectProfile.Current;
			if (profile?.IgnoredValidationRules == null || profile.IgnoredValidationRules.Count == 0)
				return Array.Empty<string>();

			return profile.IgnoredValidationRules;
		}

		public static bool IsIgnored(RagnarokValidationIssue issue) {
			return IsIgnored(issue, GetActiveIgnoredRules());
		}

		public static bool IsIgnored(RagnarokValidationIssue issue, IReadOnlyList<string> ignoredRules) {
			if (issue == null || ignoredRules == null || ignoredRules.Count == 0)
				return false;

			foreach (string rule in ignoredRules) {
				if (String.IsNullOrWhiteSpace(rule))
					continue;

				string trimmed = rule.Trim();

				if (!String.IsNullOrEmpty(issue.RuleId)
					&& String.Equals(trimmed, issue.RuleId, StringComparison.OrdinalIgnoreCase))
					return true;

				const string categoryPrefix = "category:";
				if (trimmed.StartsWith(categoryPrefix, StringComparison.OrdinalIgnoreCase)) {
					string categoryName = trimmed.Substring(categoryPrefix.Length).Trim();
					if (String.Equals(issue.Category.ToString(), categoryName, StringComparison.OrdinalIgnoreCase))
						return true;

					continue;
				}

				const string messagePrefix = "message:";
				if (trimmed.StartsWith(messagePrefix, StringComparison.OrdinalIgnoreCase)) {
					string fragment = trimmed.Substring(messagePrefix.Length).Trim();
					if (!String.IsNullOrEmpty(fragment)
						&& issue.Message != null
						&& issue.Message.IndexOf(fragment, StringComparison.OrdinalIgnoreCase) >= 0)
						return true;

					continue;
				}

				if (String.Equals(trimmed, issue.Category.ToString(), StringComparison.OrdinalIgnoreCase))
					return true;

				if (issue.Message != null && issue.Message.IndexOf(trimmed, StringComparison.OrdinalIgnoreCase) >= 0)
					return true;
			}

			return false;
		}
	}
}
