using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.QualityGate;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.VsaCatalog;
using AuswertungPro.Next.Infrastructure.Ai;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;
using AuswertungPro.Next.UI.Ai;

using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    private IReadOnlyList<(string Code, string Description, double Meter)>? GatherImportContext()
        => CodingImportContextBuilder.Build(_codingImportEvents);

    private void ShowCodingAiResults(LiveDetection result)
    {
        if (result.Error != null)
        {
            SetCodingAiState($"Fehler: {result.Error}", PlayerStatusColors.Error,
                $"Modell: {LiveDetectionDisplayPolicy.CompactModelName(_codingAiModelName)}");
            CodingFindingsList.ItemsSource = null;
            return;
        }

        // â”€â”€ Zustandsautomat: Einblendung vs. echtes Videobild â”€â”€
        // Zuerst State aktualisieren, dann pruefen ob Frame analysiert werden darf.
        // Gating BEVOR irgendetwas ins UI geschrieben wird.
        UpdateFrameReadiness(result);

        if (!IsFrameReady())
        {
            // Ergebnis puffern statt verwerfen (Warmup-Phase)
            if (result.Findings.Count > 0)
                _pendingWarmupResult = result;

            SetCodingAiState("Dateneinblendung erkannt \u2014 \u00fcbersprungen",
                PlayerStatusColors.Muted,
                $"Warte auf Videobild... (Bild {_codingFrameReadiness.SkippedFrames} von 3)");
            CodingFindingsList.ItemsSource = null;
            DetectionCanvas.Children.Clear();
            return;
        }

        // Warmup-Puffer nachtraeglich verarbeiten (erste Ready-Transition)
        if (_pendingWarmupResult != null)
        {
            var buffered = _pendingWarmupResult;
            _pendingWarmupResult = null;
            // Bestes gepuffertes Ergebnis verwenden wenn aktuelles leer ist
            if (result.Findings.Count == 0 && buffered.Findings.Count > 0)
                result = buffered;
        }

        // â”€â”€ Ab hier: Frame ist bereit fuer Analyse â”€â”€

        // OSD-Meterstand uebernehmen (Defense-in-Depth: nochmals Plausibilitaet pruefen)
        if (result.MeterReading.HasValue && result.MeterReading.Value <= 500 && _codingVm != null)
        {
            _codingLastOsdMeter = result.MeterReading.Value;
            _codingLastOsdTimestampSec = result.TimestampSeconds;
            _codingSessionService?.MoveToMeter(result.MeterReading.Value);
            OsdMeterBadge.Visibility = Visibility.Visible;
            TxtOsdMeter.Text = CodingOsdBadgeDisplayPolicy.BuildMeterText(result.MeterReading.Value);
        }

        // â”€â”€ Findings filtern: VSA-Validierung + Deduplizierung â”€â”€
        // Eine einzige gefilterte Liste fuer UI, Overlays und Event-Erstellung.
        var currentMeter = ResolveCodingMeterForFrame(result.TimestampSeconds, result.MeterReading);
        var validFindings = FilterValidFindings(result.Findings, currentMeter);

        if (validFindings.Count == 0)
        {
            var noDamageText = LiveDetectionDisplayPolicy.BuildCodingNoDamageStatusText(result.MeterReading);
            SetCodingAiState(noDamageText, PlayerStatusColors.Success, "Schritt 3 von 3: Overlay aktualisiert");
            CodingFindingsList.ItemsSource = null;
            DetectionCanvas.Children.Clear();
            return;
        }

        var findingsText = LiveDetectionDisplayPolicy.BuildCodingFindingsStatusText(result.MeterReading, validFindings.Count);
        SetCodingAiState(findingsText, PlayerStatusColors.Success, "Schritt 3 von 3: Overlay und Events");
        CodingFindingsList.ItemsSource = validFindings
            .Select(f => new AiFindingDisplayItem(f)).ToList();

        // Vor dem Hinzufuegen pruefen, welche Befunde schon bekannt/abgehandelt sind
        // (durch ein bestehendes Event abgedeckt). Nur NEUE bekommen eine Box — sonst
        // tauchen akzeptierte Befunde bei jeder erneuten Analyse wieder als Box auf.
        var findingsToDraw = validFindings.Where(f => !IsFindingAlreadyKnown(f, currentMeter)).ToList();

        // KI-Findings als CodingEvents mit AiContext in die Ereignisliste einfuegen
        AddAiFindingsAsEvents(result, validFindings);

        // Nur NEUE Befunde als visuelle Overlays auf dem Videobild anzeigen
        if (findingsToDraw.Count > 0 && !CodingOverlayPopup.IsOpen)
        {
            DetectionOverlayGrid.Visibility = Visibility.Visible;
            RenderDetectionOverlay(findingsToDraw, _player.Time / 1000.0);
            ScheduleDetectionAutoHide();   // verbleibende Boxen nach 3s ausblenden (Liste bleibt)
        }
        else
        {
            // Nichts Neues zu zeigen -> evtl. noch sichtbare Alt-Boxen wegnehmen (Liste bleibt)
            DetectionCanvas.Children.Clear();
            DetectionOverlayGrid.Visibility = Visibility.Collapsed;
        }
    }

}
