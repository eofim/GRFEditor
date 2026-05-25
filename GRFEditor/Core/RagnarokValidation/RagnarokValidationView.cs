using System;
using System.ComponentModel;
using ErrorManager;
using TokeiLibrary;

namespace GRFEditor.Core.RagnarokValidation {
	public class RagnarokValidationView : INotifyPropertyChanged {
		private bool _isSelectedForFix;

		public RagnarokValidationView(RagnarokValidationIssue issue) {
			Issue = issue ?? new RagnarokValidationIssue();

			Severity = Issue.Severity.ToString();
			Category = Issue.Category.ToString();
			RelativePath = Issue.RelativePath ?? "";
			Message = Issue.Message ?? "";
			SuggestedFix = Issue.SuggestedFix ?? "";
			CanAutoFix = Issue.CanAutoFix ? "Yes" : "No";
			DataImage = _getSeverityImage(Issue.Severity);
			SeverityStyleKey = Issue.Severity.ToString();
		}

		public event PropertyChangedEventHandler PropertyChanged;

		public RagnarokValidationIssue Issue { get; private set; }
		public string Severity { get; private set; }
		public string Category { get; private set; }
		public string RelativePath { get; private set; }
		public string Message { get; private set; }
		public string SuggestedFix { get; private set; }
		public string CanAutoFix { get; private set; }
		public object DataImage { get; private set; }
		public string SeverityStyleKey { get; private set; }

		public bool CanSelectForFix => Issue.CanAutoFix;

		public bool IsSelectedForFix {
			get { return _isSelectedForFix; }
			set {
				if (_isSelectedForFix == value)
					return;

				_isSelectedForFix = value;
				OnPropertyChanged(nameof(IsSelectedForFix));
			}
		}

		public string ToolTipRelativePath => RelativePath;
		public string ToolTipMessage => Message;
		public string ToolTipSuggestedFix => SuggestedFix;
		public bool Default => SeverityStyleKey == RagnarokValidationSeverity.Info.ToString();

		public string ToCopyLine() {
			return Severity + "\t" + Category + "\t" + RelativePath + "\t" + Message + "\t" + SuggestedFix + "\t" + CanAutoFix;
		}

		public override string ToString() {
			return ToCopyLine();
		}

		protected void OnPropertyChanged(string propertyName) {
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}

		private static object _getSeverityImage(RagnarokValidationSeverity severity) {
			try {
				switch (severity) {
					case RagnarokValidationSeverity.Critical:
					case RagnarokValidationSeverity.Error:
						return ApplicationManager.PreloadResourceImage("error16.png");
					case RagnarokValidationSeverity.Warning:
						return ApplicationManager.PreloadResourceImage("warning16.png");
					default:
						return ApplicationManager.PreloadResourceImage("help.png");
				}
			}
			catch (Exception err) {
				ErrorHandler.HandleException(err, ErrorLevel.Low);
				return null;
			}
		}
	}
}
