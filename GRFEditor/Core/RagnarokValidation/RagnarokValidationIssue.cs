using System.Collections.Generic;

namespace GRFEditor.Core.RagnarokValidation {
	public class RagnarokValidationIssue {
		public RagnarokValidationIssue() {
			RelatedFiles = new List<string>();
		}

		public string RuleId { get; set; }
		public RagnarokValidationSeverity Severity { get; set; }
		public RagnarokValidationCategory Category { get; set; }
		public string RelativePath { get; set; }
		public string Message { get; set; }
		public string SuggestedFix { get; set; }
		public bool CanAutoFix { get; set; }
		public RagnarokValidationFixKind FixKind { get; set; }
		public List<string> RelatedFiles { get; set; }
	}
}
