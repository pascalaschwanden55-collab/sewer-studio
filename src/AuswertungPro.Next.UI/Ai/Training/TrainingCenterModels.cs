using System;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Domain.Ai.Training;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AuswertungPro.Next.UI.Ai.Training;

// Phase 5.3: TrainingCaseStatus + SelfTrainingEntryResult + TrainingCase + TrainingCenterState
// nach Application/Ai/Training/TrainingCenterModels.cs migriert. Hier nur noch
// TrainingCaseViewModel (MVVM-Wrapper fuer Bindings) + UI-Visualisierungs-Models.

/// <summary>
/// MVVM-Wrapper um den POCO `Application.Ai.Training.TrainingCase`. Spiegelt
/// die fuer XAML-Bindings noetigen Properties (CaseId, FolderPath, VideoPath,
/// ProtocolPath, Status, CreatedUtc, Rohrmaterial, NennweiteMm, Profil) und
/// feuert PropertyChanged bei Status/Stammdaten-Aenderungen.
///
/// Property-Namen bleiben unveraendert zur frueheren MVVM-Klasse, damit alle
/// XAML-Bindings (DataGrid, SelectedCase.CaseId etc.) ohne Aenderung weiter
/// funktionieren.
/// </summary>
public sealed class TrainingCaseViewModel : ObservableObject
{
    /// <summary>Underlying POCO — wird in JSON serialisiert und an Services gegeben.</summary>
    public TrainingCase Model { get; }

    public TrainingCaseViewModel(TrainingCase model)
    {
        Model = model ?? throw new ArgumentNullException(nameof(model));
    }

    public TrainingCaseViewModel() : this(new TrainingCase()) { }

    public string CaseId => Model.CaseId;
    public string FolderPath => Model.FolderPath;
    public string VideoPath => Model.VideoPath;
    public string ProtocolPath => Model.ProtocolPath;
    public DateTime CreatedUtc => Model.CreatedUtc;

    public TrainingCaseStatus Status
    {
        get => Model.Status;
        set
        {
            if (Model.Status == value) return;
            Model.Status = value;
            OnPropertyChanged();
        }
    }

    public string? Rohrmaterial
    {
        get => Model.Rohrmaterial;
        set
        {
            if (Model.Rohrmaterial == value) return;
            Model.Rohrmaterial = value;
            OnPropertyChanged();
        }
    }

    public int? NennweiteMm
    {
        get => Model.NennweiteMm;
        set
        {
            if (Model.NennweiteMm == value) return;
            Model.NennweiteMm = value;
            OnPropertyChanged();
        }
    }

    public string? Profil
    {
        get => Model.Profil;
        set
        {
            if (Model.Profil == value) return;
            Model.Profil = value;
            OnPropertyChanged();
        }
    }

    public override string ToString() => Model.CaseId;
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
