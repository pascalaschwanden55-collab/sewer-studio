using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using AuswertungPro.Next.Application.UseCases.BendSuggestions;
using AuswertungPro.Next.Application.UseCases.CodingSuggestions;

namespace AuswertungPro.Next.UI.Player;

/// <summary>
/// Zustand der Karte "KI-Vorschlaege": Zeilen, Kopftext, Hinweis, Meterspur.
/// Bestaetigen graut aus (die Zeile bleibt als Beleg), Ablehnen entfernt.
/// </summary>
public sealed class CodingSuggestionsOwner : INotifyPropertyChanged
{
    private const string Titel = "KI-VORSCHLÄGE";

    private string _headerText = Titel;
    private string _hintText = string.Empty;
    private bool _isScanning;

    public ObservableCollection<CodingSuggestionRow> Rows { get; } = new();

    public IReadOnlyList<MeterTrackPoint> MeterTrack { get; private set; } = Array.Empty<MeterTrackPoint>();

    public string HeaderText { get => _headerText; private set => Set(ref _headerText, value); }
    public string HintText { get => _hintText; private set => Set(ref _hintText, value); }
    public bool IsScanning { get => _isScanning; private set => Set(ref _isScanning, value); }

    public int OpenCount => Rows.Count(r => !r.IsConfirmed);

    public void BeginScan()
    {
        Clear();
        IsScanning = true;
        SetPercent(0);
    }

    public void SetPercent(int percent)
    {
        if (!IsScanning) return;
        HeaderText = $"KI prüft Video … {Math.Clamp(percent, 0, 100)} %";
    }

    public void Apply(CodingSuggestionSet set)
    {
        ArgumentNullException.ThrowIfNull(set);
        IsScanning = false;
        Rows.Clear();
        foreach (var s in set.Suggestions.OrderBy(s => s.PeakTimeSeconds))
            Rows.Add(new CodingSuggestionRow(s));
        MeterTrack = set.MeterTrack;
        HintText = string.Join(" · ", new[]
        {
            Hinweis("Bogen", set.BogenTeil),
            Hinweis("Rohranfang/Rohrende", set.AnfangEndeTeil)
        }.Where(t => t.Length > 0));
        RefreshHeader();
    }

    public void Fail(string grund)
    {
        IsScanning = false;
        HeaderText = "KI-Vorschläge nicht verfügbar";
        HintText = grund ?? string.Empty;
    }

    public void Confirm(CodingSuggestionRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        row.IsConfirmed = true;
        RefreshHeader();
    }

    public void Reject(CodingSuggestionRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        Rows.Remove(row);
        RefreshHeader();
    }

    public void Clear()
    {
        Rows.Clear();
        MeterTrack = Array.Empty<MeterTrackPoint>();
        IsScanning = false;
        HintText = string.Empty;
        HeaderText = Titel;
    }

    private void RefreshHeader()
    {
        HeaderText = Rows.Count == 0 ? Titel : $"{Titel} ({OpenCount})";
        OnPropertyChanged(nameof(OpenCount));
    }

    private static string Hinweis(string teil, CodingSuggestionPartState state)
        => state.Status == CodingSuggestionPartStatus.Bereit ? string.Empty : $"{teil}: {state.Grund}";

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        OnPropertyChanged(name);
    }

    private void OnPropertyChanged(string? name)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
