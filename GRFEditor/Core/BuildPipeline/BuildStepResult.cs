using System;
using System.Collections.Generic;

namespace GRFEditor.Core.BuildPipeline {
	public class BuildStepResult {
		public BuildStepResult(string name) {
			Name = name ?? "";
			StartedAt = DateTime.UtcNow;
			Warnings = new List<string>();
			Errors = new List<string>();
			FilesAffected = new List<string>();
		}

		public string Name { get; private set; }
		public DateTime StartedAt { get; set; }
		public DateTime? FinishedAt { get; set; }
		public bool Success { get; set; }
		public BuildSeverity Severity { get; set; }
		public List<string> Warnings { get; private set; }
		public List<string> Errors { get; private set; }
		public List<string> FilesAffected { get; private set; }

		public void MarkFinished(bool success, BuildSeverity severity = BuildSeverity.Info) {
			FinishedAt = DateTime.UtcNow;
			Success = success;
			Severity = severity;
		}

		public TimeSpan? Duration =>
			FinishedAt.HasValue ? FinishedAt.Value - StartedAt : (TimeSpan?)null;
	}
}
