using System;
using System.Collections.Generic;
using System.Linq;

namespace GRFEditor.Core.RagnarokValidation {
	public class RagnarokValidationResult {
		public RagnarokValidationResult() {
			Issues = new List<RagnarokValidationIssue>();
			StartedAt = DateTime.UtcNow;
		}

		public List<RagnarokValidationIssue> Issues { get; private set; }
		public DateTime StartedAt { get; private set; }
		public DateTime? CompletedAt { get; private set; }
		public bool WasCancelled { get; private set; }

		public int TotalCount => Issues.Count;

		public int CountBySeverity(RagnarokValidationSeverity severity) {
			return Issues.Count(p => p.Severity == severity);
		}

		public int CountByCategory(RagnarokValidationCategory category) {
			return Issues.Count(p => p.Category == category);
		}

		public bool HasErrors =>
			Issues.Any(p => p.Severity == RagnarokValidationSeverity.Error || p.Severity == RagnarokValidationSeverity.Critical);

		public void Add(RagnarokValidationIssue issue) {
			if (issue != null)
				Issues.Add(issue);
		}

		internal void MarkCompleted(bool cancelled) {
			CompletedAt = DateTime.UtcNow;
			WasCancelled = cancelled;
		}
	}
}
