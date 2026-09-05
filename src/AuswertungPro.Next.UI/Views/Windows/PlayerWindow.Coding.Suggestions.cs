using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.UseCases.CodingSuggestions;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Ai.Coding;
using AuswertungPro.Next.UI.Helpers;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Views.Windows;

/// <summary>
/// KI-Vorschlaege im Codiermodus: Vorabdurchlauf im Hintergrund, Karte im
/// Seitenpanel, Marker auf der Zeitleiste. Entscheidungen (was Bestaetigen tut)
/// liegen in CodingSuggestionConfirmPolicy; hier wird nur verdrahtet.
/// </summary>
public partial class PlayerWindow
{
    private readonly CodingSuggestionsOwner _codingSuggestions = new();
    private SuggestionMarkerController? _suggestionMarkers;
    private CancellationTokenSource? _suggestionScanCts;
    private bool _suggestionCardBound;

    private SuggestionMarkerController SuggestionMarkers
        => _suggestionMarkers ??= new SuggestionMarkerController(
            SuggestionMarkerCanvas,
            () => PlayerSliderTrackBounds.Resolve(PositionSlider, SuggestionMarkerCanvas),
            () => _playerTimelineHost.DurationSeconds,
            SeekToSuggestionSeconds);

    /// <summary>Vierter Hintergrundschritt beim Eintritt in den Codiermodus.</summary>
    private void StartSuggestionScan()
    {
        CancelSuggestionScan();
        BindSuggestionCard();

        var settings = _protocolContext.Settings;
        var provider = _protocolContext.LegacyServiceProvider;
        var videoPath = _codingSessionHost.VideoPath;
        var haltung = _codingSessionHost.HaltungName ?? _protocolContext.HaltungId ?? string.Empty;

        if (provider is null || string.IsNullOrWhiteSpace(videoPath))
        {
            _codingSuggestions.Fail("Kein Video oder keine Dienste im Codiermodus.");
            return;
        }

        var cts = new CancellationTokenSource();
        _suggestionScanCts = cts;
        _codingSuggestions.BeginScan();
        RunSuggestionScanAsync(
                provider.CodingSuggestionScan,
                new CodingSuggestionScanRequest(videoPath, haltung, settings?.CodingSuggestionsEnabled ?? true),
                cts)
            .SafeFireAndForget("CodingSuggestionScan");
    }

    private async Task RunSuggestionScanAsync(
        ICodingSuggestionScanService service,
        CodingSuggestionScanRequest request,
        CancellationTokenSource cts)
    {
        try
        {
            // Erst die KI-Bereitschaft (startet bei Bedarf den Sidecar), dann der Durchlauf.
            await _codingPipelineHealthController.InitializeAsync();
            cts.Token.ThrowIfCancellationRequested();

            var fortschritt = new Progress<int>(p =>
            {
                if (ReferenceEquals(_suggestionScanCts, cts))
                    _codingSuggestions.SetPercent(p);
            });
            var set = await service.ScanAsync(request, cts.Token, fortschritt);

            if (!ReferenceEquals(_suggestionScanCts, cts))
                return; // ein spaeterer Codiermodus hat uebernommen
            _codingSuggestions.Apply(set);
            SuggestionMarkers.Build(_codingSuggestions.Rows);
        }
        catch (OperationCanceledException)
        {
            // Codiermodus verlassen oder Fenster geschlossen — kein Hinweis, nur aufraeumen.
            if (ReferenceEquals(_suggestionScanCts, cts))
                _codingSuggestions.Clear();
        }
        catch (Exception ex)
        {
            if (ReferenceEquals(_suggestionScanCts, cts))
                _codingSuggestions.Fail(ex.Message);
        }
        finally
        {
            // Kein Dispose: CancelSuggestionScan darf spaeter noch Cancel rufen, ohne
            // auf einen bereits freigegebenen Token zu treffen. Eine Quelle ohne Timer
            // haelt keine Systemressourcen.
            if (ReferenceEquals(_suggestionScanCts, cts))
                _suggestionScanCts = null;
        }
    }

    /// <summary>Beim Verlassen des Codiermodus und beim Schliessen: Durchlauf stoppen, Karte leeren.</summary>
    private void CancelSuggestionScan()
    {
        var cts = _suggestionScanCts;
        _suggestionScanCts = null;
        cts?.Cancel();
        _codingSuggestions.Clear();
        _suggestionMarkers?.Clear();
    }

    private void BindSuggestionCard()
    {
        if (_suggestionCardBound)
            return;
        _suggestionCardBound = true;

        var panel = CodingSidePanelControl;
        panel.LstSuggestions.ItemsSource = _codingSuggestions.Rows;
        panel.TxtSuggestionHeader.SetBinding(System.Windows.Controls.TextBlock.TextProperty,
            new Binding(nameof(CodingSuggestionsOwner.HeaderText)) { Source = _codingSuggestions });
        panel.TxtSuggestionHint.SetBinding(System.Windows.Controls.TextBlock.TextProperty,
            new Binding(nameof(CodingSuggestionsOwner.HintText)) { Source = _codingSuggestions });
        panel.TxtSuggestionHint.SetBinding(VisibilityProperty,
            new Binding(nameof(CodingSuggestionsOwner.HintText))
            {
                Source = _codingSuggestions,
                Converter = new StringToVisibilityConverter()
            });
    }

    private CodingSuggestionRow? SelectedSuggestionRow
        => CodingSidePanelControl.LstSuggestions.SelectedItem as CodingSuggestionRow;

    private void SeekToSuggestionSeconds(double seconds)
    {
        _playerPlaybackControlHost.SetPause(true);
        _playerTimelineHost.SeekMilliseconds((long)Math.Round(seconds * 1000.0));
        _codingNavigationPendingState.Set(true);
    }

    private void SuggestionSeek_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedSuggestionRow is { } row)
            SeekToSuggestionSeconds(row.TimeSeconds);
    }

    private void SuggestionReject_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedSuggestionRow is not { } row) return;
        _codingSuggestions.Reject(row);
        SuggestionMarkers.Build(_codingSuggestions.Rows);
    }

    private void SuggestionConfirm_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedSuggestionRow is not { IsConfirmed: false } row) return;
        var session = _codingSessionRuntimeOwner.Service;
        var record = _protocolContext.HaltungRecord;
        if (session is null || record is null) return;

        SeekToSuggestionSeconds(row.TimeSeconds);
        var videoTime = TimeSpan.FromSeconds(row.TimeSeconds);
        var aktiveCodes = session.Events
            .Where(ev => !ev.Entry.IsDeleted)
            .Select(ev => ev.Entry.Code ?? string.Empty)
            .ToList();
        var laengeVorhanden = !string.IsNullOrWhiteSpace(record.GetFieldValue(FieldKeys.HoldingLengthMeters));

        var plan = CodingSuggestionConfirmPolicy.Plan(row.Suggestion, _codingSuggestions.MeterTrack, aktiveCodes, laengeVorhanden);

        switch (plan.Action)
        {
            case CodingSuggestionConfirmAction.AlreadyPresent:
                _codingSuggestions.Confirm(row);
                _protocolContext.Dialogs.Info(plan.Hinweis, "KI-Vorschlag");
                break;

            case CodingSuggestionConfirmAction.OpenCodeWindow:
                if (ConfirmBendSuggestion(plan, videoTime, session))
                    _codingSuggestions.Confirm(row);
                break;

            case CodingSuggestionConfirmAction.CreateBoundaryEvent:
                if (plan.ProposeLength && plan.Meter is { } laenge)
                {
                    var text = laenge.ToString("0.00", CultureInfo.GetCultureInfo("de-DE"));
                    if (_protocolContext.Dialogs.Confirm($"Länge {text} m aus dem Video als Haltungslänge übernehmen?", "Haltungslänge"))
                        record.SetFieldValue(FieldKeys.HoldingLengthMeters, laenge.ToString("F2", CultureInfo.InvariantCulture), FieldSource.Protocol, userEdited: false);
                }

                var meter = plan.Meter ?? _codingOsdMeterController.LastMeter ?? _codingSessionHost.CurrentMeter;
                var beschreibung = plan.Code == "BCD" ? "Rohranfang" : "Rohrende";
                var created = CodingSelectedCodeEventWorkflow.Create(
                    plan.Code, beschreibung, meter, videoTime, null, session, CodingCaptureSnapshot);
                CodingEventCreationPostWorkflow.Apply(
                    created,
                    _codingSidePanelControllers.EventCreationPostActions,
                    new CodingEventCreationPostOptions(SelectCreatedEvent: true, ClearSelectedCode: false));
                _codingSuggestions.Confirm(row);
                break;
        }

        SuggestionMarkers.Build(_codingSuggestions.Rows);
    }

    /// <summary>Bogen: Codierfenster mit BCC vorgewaehlt; der Mensch waehlt die Richtung.</summary>
    private bool ConfirmBendSuggestion(CodingSuggestionConfirmPlan plan, TimeSpan videoTime, ICodingSessionService session)
    {
        var entry = CodingExplorerEntryFactory.CreateSeed(null, videoTime, suggestedCode: plan.Code);
        entry.MeterStart = plan.Meter;
        entry.MeterEnd = plan.Meter;

        var service = CodingCodeExplorerServiceCreationWorkflow.Create(
            CreateVsaCodeExplorerViewModel,
            _protocolContext.CodeUsage,
            _protocolContext.LegacyServiceProvider);
        var angenommen = _codingOverlayInputVisibilityController.Run(() =>
            service.TryEdit(entry, plan.Meter, videoTime, _codingSessionHost.VideoPath, videoTime, this, CreateVsaCodeExplorerLiveSnapshotProvider()));
        if (!angenommen)
            return false;

        var photoPath = CodingCaptureSnapshot(entry);
        CodingProtocolEntryPhotoPathAppender.AddIfPresent(entry, photoPath);
        var created = CodingManualEventAppender.Apply(entry, null, session);
        CodingEventCreationPostWorkflow.Apply(
            created,
            _codingSidePanelControllers.EventCreationPostActions,
            new CodingEventCreationPostOptions(SelectCreatedEvent: true, ClearSelectedCode: false));
        return true;
    }

    /// <summary>Leerer Text = Hinweiszeile ausblenden.</summary>
    private sealed class StringToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => string.IsNullOrWhiteSpace(value as string) ? Visibility.Collapsed : Visibility.Visible;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
