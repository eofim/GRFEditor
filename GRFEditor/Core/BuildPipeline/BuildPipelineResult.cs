using System;
using System.Collections.Generic;
using System.Linq;
using GRFEditor.Core.RagnarokValidation;

namespace GRFEditor.Core.BuildPipeline {
	public class BuildPipelineResult {
		public BuildPipelineResult() {
			StartedAt = DateTime.UtcNow;
			Steps = new List<BuildStepResult>();
			Messages = new List<string>();
		}

		public DateTime StartedAt { get; private set; }
		public DateTime? FinishedAt { get; private set; }
		public bool Success { get; private set; }
		public BuildSeverity Severity { get; private set; }
		public string OutputDirectory { get; set; }
		public string ProfileName { get; set; }
		public string GrfFileName { get; set; }

		public string ManifestFilePath { get; set; }
		public string ManifestCsvFilePath { get; set; }
		public string ChangelogFilePath { get; set; }
		public string ChangelogTextPath { get; set; }
		public string HashesFilePath { get; set; }
		public string ReportJsonPath { get; set; }
		public string ReportTextPath { get; set; }

		public List<BuildStepResult> Steps { get; private set; }
		public List<string> Messages { get; private set; }

		public RagnarokValidationResult ValidationResult { get; set; }

		public BuildStepResult GetStep(string name) {
			return Steps.FirstOrDefault(s => String.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase));
		}

		internal void MarkCompleted(bool success, BuildSeverity severity) {
			FinishedAt = DateTime.UtcNow;
			Success = success;
			Severity = severity;
		}
	}
}
