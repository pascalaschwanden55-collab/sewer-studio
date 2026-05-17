using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using AuswertungPro.Next.Application.Ai.Diagnostics;

namespace AuswertungPro.Next.UI.Views.Windows;

/// <summary>
/// Cockpit-Fenster fuer die AI-Pipeline-Diagnose. Pollt den
/// <see cref="AiDiagnosticsRecorderProvider"/> alle 1s und zeigt die letzten
/// Events mit Stage-/Drop-Reason-Filter und Detail-Panel.
///
/// Reine UI-View — Recorder-Logik liegt in Application. Diese Klasse macht
/// nur drei Dinge: pollen, filtern, anzeigen.
/// </summary>
public partial class AiDiagnosticsWindow : Window
{
    private readonly DispatcherTimer _timer;
    private readonly ObservableCollection<EventRow> _rows = new();
    private string? _stageFilter;
    private string? _reasonFilter;

    public AiDiagnosticsWindow()
    {
        InitializeComponent();

        BuildStageFilterItems();
        BuildReasonFilterItems();
        GridEvents.ItemsSource = _rows;

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (_, _) => Refresh();
        _timer.Start();

        Refresh();
    }

    protected override void OnClosed(EventArgs e)
    {
        _timer.Stop();
        base.OnClosed(e);
    }

    private void BuildStageFilterItems()
    {
        CmbStageFilter.Items.Clear();
        CmbStageFilter.Items.Add(new ComboBoxItem { Content = "(alle)", Tag = null });
        foreach (var s in new[]
        {
            AiDiagnosticStage.QwenRaw,
            AiDiagnosticStage.QwenMapped,
            AiDiagnosticStage.QwenSuppressed,
            AiDiagnosticStage.QwenError,
            AiDiagnosticStage.YoloRaw,
            AiDiagnosticStage.YoloError,
            AiDiagnosticStage.MultiModelRaw,
            AiDiagnosticStage.CodingFilterDrop,
            AiDiagnosticStage.PipeAxisGeometry,
            AiDiagnosticStage.EventCreated,
        })
        {
            CmbStageFilter.Items.Add(new ComboBoxItem { Content = s, Tag = s });
        }
        CmbStageFilter.SelectedIndex = 0;
    }

    private void BuildReasonFilterItems()
    {
        CmbReasonFilter.Items.Clear();
        CmbReasonFilter.Items.Add(new ComboBoxItem { Content = "(alle)", Tag = null });
        foreach (var r in new[]
        {
            AiDiagnosticDropReason.CodeResolverNull,
            AiDiagnosticDropReason.DedupBcd,
            AiDiagnosticDropReason.DedupBce,
            AiDiagnosticDropReason.DedupExisting,
            AiDiagnosticDropReason.RejectedByUser,
            AiDiagnosticDropReason.ViewTypeSuppressed,
            AiDiagnosticDropReason.QualityGateRed,
            AiDiagnosticDropReason.ZoneDepth,
            AiDiagnosticDropReason.KunststoffFilter,
            AiDiagnosticDropReason.FrameNotReady,
            AiDiagnosticDropReason.EmptyFrame,
        })
        {
            CmbReasonFilter.Items.Add(new ComboBoxItem { Content = r, Tag = r });
        }
        CmbReasonFilter.SelectedIndex = 0;
    }

    private void Refresh()
    {
        var snapshot = AiDiagnosticsRecorderProvider.Current.Snapshot(500);

        IEnumerable<AiDiagnosticEvent> filtered = snapshot;
        if (!string.IsNullOrEmpty(_stageFilter))
            filtered = filtered.Where(e => e.Stage == _stageFilter);
        if (!string.IsNullOrEmpty(_reasonFilter))
            filtered = filtered.Where(e => e.DroppedReason == _reasonFilter);

        var filteredList = filtered as IReadOnlyList<AiDiagnosticEvent>
                           ?? filtered.ToList();

        // Audit 2026-05-13 M7: Selection per stabilem Key (TimestampUtc) merken
        // und differential gegen den Snapshot updaten — kein Clear+Rebuild mehr.
        var selectedKey = (GridEvents.SelectedItem as EventRow)?.Original.TimestampUtc;

        AiDiagnosticsRowsDiff.Apply(
            _rows, filteredList,
            keyOf: r => r.Original.TimestampUtc,
            toRow: EventRow.From);

        TxtCount.Text = $"{_rows.Count} / {snapshot.Count} Einträge";

        if (selectedKey is { } key)
        {
            var match = _rows.FirstOrDefault(r => r.Original.TimestampUtc == key);
            if (match != null) GridEvents.SelectedItem = match;
        }
    }

    private void CmbStageFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _stageFilter = (CmbStageFilter.SelectedItem as ComboBoxItem)?.Tag as string;
        Refresh();
    }

    private void CmbReasonFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _reasonFilter = (CmbReasonFilter.SelectedItem as ComboBoxItem)?.Tag as string;
        Refresh();
    }

    private void BtnLive_Click(object sender, RoutedEventArgs e)
    {
        if (BtnLive.IsChecked == true)
        {
            _timer.Start();
            BtnLive.Content = "🔴 Live (1s)";
        }
        else
        {
            _timer.Stop();
            BtnLive.Content = "⏸ Pausiert";
        }
    }

    private void BtnRefresh_Click(object sender, RoutedEventArgs e) => Refresh();

    private void BtnClear_Click(object sender, RoutedEventArgs e)
    {
        AiDiagnosticsRecorderProvider.Current.Clear();
        Refresh();
    }

    private void GridEvents_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var row = GridEvents.SelectedItem as EventRow;
        TxtRawOutput.Text = row?.Original.RawOutput ?? "";

        LstMetadata.Items.Clear();
        if (row?.Original.Metadata is { Count: > 0 } meta)
        {
            foreach (var kvp in meta)
                LstMetadata.Items.Add($"{kvp.Key} = {kvp.Value}");
        }
    }

    /// <summary>
    /// View-Adapter — DataGrid bindet darauf. Vermeidet eine direkte
    /// Abhaengigkeit der XAML auf <see cref="DateTimeOffset"/>-Formatierung.
    /// </summary>
    private sealed class EventRow
    {
        public string TimeLocal { get; init; } = "";
        public string Stage { get; init; } = "";
        public string Source { get; init; } = "";
        public string Model { get; init; } = "";
        public string LatencyText { get; init; } = "";
        public string Summary { get; init; } = "";
        public string DroppedReason { get; init; } = "";
        public AiDiagnosticEvent Original { get; init; } = default!;

        public static EventRow From(AiDiagnosticEvent e) => new()
        {
            TimeLocal = e.TimestampUtc.ToLocalTime().ToString("HH:mm:ss.fff"),
            Stage = e.Stage,
            Source = e.Source,
            Model = e.Model ?? "",
            LatencyText = e.LatencyMs.HasValue ? $"{e.LatencyMs.Value:F0}ms" : "",
            Summary = e.Summary,
            DroppedReason = e.DroppedReason ?? "",
            Original = e,
        };
    }
}
