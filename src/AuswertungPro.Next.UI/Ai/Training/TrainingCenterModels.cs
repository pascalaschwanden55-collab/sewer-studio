using System;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Domain.Ai.Training;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AuswertungPro.Next.UI.Ai.Training;

// Phase 5.3: TrainingCaseStatus + SelfTrainingEntryResult nach Application/Ai/Training/TrainingCenterModels.cs.

public partial class TrainingCase : ObservableObject
{
    [ObservableProperty] private string _caseId = "";
    [ObservableProperty] private string _folderPath = "";
    [ObservableProperty] private string _videoPath = "";
    [ObservableProperty] private string _protocolPath = "";
    [ObservableProperty] private TrainingCaseStatus _status = TrainingCaseStatus.New;
    [ObservableProperty] private DateTime _createdUtc = DateTime.UtcNow;

    /// <summary>Rohrmaterial aus PDF/Import (z.B. "Polyethylen", "Beton"). Null wenn unbekannt.</summary>
    public string? Rohrmaterial { get; set; }

    /// <summary>Nennweite in mm aus PDF/Import. Null wenn unbekannt.</summary>
    public int? NennweiteMm { get; set; }

    /// <summary>Rohrprofil-Text (z.B. "Kreisprofil 300mm"). Null wenn unbekannt.</summary>
    public string? Profil { get; set; }

    public override string ToString() => CaseId;
}

public sealed class TrainingCenterState
{
    public List<TrainingCase> Cases { get; set; } = new();
    public List<string> RootFolders { get; set; } = new();
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
}

// ── Visualisierungs-Models fuer Selbsttraining ──

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
