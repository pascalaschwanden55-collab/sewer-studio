using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AuswertungPro.Next.UI.Ai.Training
{
    public enum TrainingSampleStatus { New, Approved, Rejected }

    public sealed partial class TrainingSample : ObservableObject
    {
        public string SampleId { get; set; } = string.Empty;
        public string CaseId { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public string Beschreibung { get; set; } = string.Empty;
        public double MeterStart { get; set; }
        public double MeterEnd { get; set; }
        public bool IsStreckenschaden { get; set; }
        public double TimeSeconds { get; set; }
        public double? DetectedMeter { get; set; }
        public string MeterSource { get; set; } = string.Empty;
        public string FramePath { get; set; } = string.Empty;
        public TrainingSampleStatus Status { get; set; } = TrainingSampleStatus.New;
        public DateTime? ExportedUtc { get; set; }
        public string Notes { get; set; } = string.Empty;
        public double? TruthMeterCenter { get; set; }
        public double? OdsDeltaMeters { get; set; }
        public bool HasOsdMismatch { get; set; }
        public string Signature { get; set; } = string.Empty;
        public int FrameIndex { get; set; }
    }
}
