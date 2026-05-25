using System;
using System.Collections.Generic;
using System.Linq;

namespace GRFEditor.Core.GrfCompare {
	public sealed class GrfCompareResult {
		public DateTime ComparedAtUtc { get; set; }
		public string GrfAPath { get; set; }
		public string GrfBPath { get; set; }
		public List<GrfCompareEntry> Entries { get; set; } = new List<GrfCompareEntry>();

		public int SameCount => _count(GrfCompareStatus.Same);
		public int AddedCount => _count(GrfCompareStatus.Added);
		public int RemovedCount => _count(GrfCompareStatus.Removed);
		public int ModifiedCount => _count(GrfCompareStatus.Modified);
		public int SameContentDifferentPathCount => _count(GrfCompareStatus.SameContentDifferentPath);
		public int SamePathDifferentContentCount => _count(GrfCompareStatus.SamePathDifferentContent);

		private int _count(GrfCompareStatus status) {
			return Entries?.Count(e => e.Status == status) ?? 0;
		}
	}
}
