using System;
using System.Collections.Generic;
using AuswertungPro.Next.Application.Ai.Training;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AuswertungPro.Next.UI.Ai.Training;

public enum TrainingCaseStatus
{
    New = 0,
    Approved = 1,
    Rejected = 2
}

public partial class TrainingCase : ObservableObject
{
    [ObservableProperty] private string _caseId = "";
    [ObservableProperty] private string _folderPath = "";
    [ObservableProperty] private string _videoPath = "";
    [ObservableProperty] private string _protocolPath = "";
    [ObservableProperty] private DateTime? _inspectionDate;
    [ObservableProperty] private TrainingCaseStatus _status = TrainingCaseStatus.New;
    [ObservableProperty] private DateTime _createdUtc = DateTime.UtcNow;

    public override string ToString() => CaseId;
}

public sealed class TrainingCenterState
{
    public List<TrainingCase> Cases { get; set; } = new();
    public List<string> RootFolders { get; set; } = new();
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
}

// ── Visualisierungs-Models fuer Selbsttraining ──

/// <summary>Ein Ergebnis-Eintrag im Selbsttraining-Verlauf.</summary>
public sealed class SelfTrainingEntryResult
{
    public int Index { get; init; }
    public string VsaCode { get; init; } = "";
    public double Meter { get; init; }
    public MatchLevel Level { get; init; }
    public string Summary { get; init; } = "";
}

/// <summary>Code-Verteilung: wie oft ein VSA-Code mit welchem Ergebnis erkannt wurde.</summary>
public partial class CodeDistributionEntry : ObservableObject
{
    public string Code { get; init; } = "";
    [ObservableProperty] private int _total;
    [ObservableProperty] private int _exact;
    [ObservableProperty] private int _partial;
    [ObservableProperty] private int _mismatch;
    [ObservableProperty] private int _noFindings;

    public double MatchRate => Total > 0 ? (double)Exact / Total : 0;

    partial void OnTotalChanged(int value) => OnPropertyChanged(nameof(MatchRate));
    partial void OnExactChanged(int value) => OnPropertyChanged(nameof(MatchRate));
}
