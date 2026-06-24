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
            DetectionOverlayCleaner.ClearFindings(CodingFindingsList);
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
            DetectionOverlayCleaner.ClearFindingsAndCanvas(DetectionCanvas, CodingFindingsList);
            return;
        }

        // Warmup-Puffer nachtraeglich verarbeiten (erste Ready-Transition)
        var warmupSelection = CodingWarmupResultBufferPolicy.Select(result, _pendingWarmupResult);
        if (warmupSelection.ShouldClearPending)
            _pendingWarmupResult = null;
        result = warmupSelection.Result;

        // â”€â”€ Ab hier: Frame ist bereit fuer Analyse â”€â”€

        // OSD-Meterstand uebernehmen (Defense-in-Depth: nochmals Plausibilitaet pruefen)
        var acceptedOsdMeter = CodingOsdMeterStateWorkflow.FromDetectionResult(result);
        if (_codingVm != null && acceptedOsdMeter.HasValue)
        {
            ApplyCodingOsdMeterState(acceptedOsdMeter.Value);
            _codingSessionService?.MoveToMeter(acceptedOsdMeter.Value.Meter);
        }

        // â”€â”€ Findings filtern: VSA-Validierung + Deduplizierung â”€â”€
        // Eine einzige gefilterte Liste fuer UI, Overlays und Event-Erstellung.
        var currentMeter = ResolveCodingMeterForFrame(result.TimestampSeconds, result.MeterReading);
        var validFindings = FilterValidFindings(result.Findings, currentMeter);

        if (validFindings.Count == 0)
        {
            var noDamageText = LiveDetectionDisplayPolicy.BuildCodingNoDamageStatusText(result.MeterReading);
            SetCodingAiState(noDamageText, PlayerStatusColors.Success, "Schritt 3 von 3: Overlay aktualisiert");
            DetectionOverlayCleaner.ClearFindingsAndCanvas(DetectionCanvas, CodingFindingsList);
            return;
        }

        var findingsText = LiveDetectionDisplayPolicy.BuildCodingFindingsStatusText(result.MeterReading, validFindings.Count);
        SetCodingAiState(findingsText, PlayerStatusColors.Success, "Schritt 3 von 3: Overlay und Events");
        CodingFindingsList.ItemsSource = AiFindingDisplayItemFactory.ForFindings(validFindings);

        // Vor dem Hinzufuegen pruefen, welche Befunde schon bekannt/abgehandelt sind
        // (durch ein bestehendes Event abgedeckt). Nur NEUE bekommen eine Box — sonst
        // tauchen akzeptierte Befunde bei jeder erneuten Analyse wieder als Box auf.
        var findingsToDraw = CodingNewFindingOverlaySelector.Select(
            validFindings,
            currentMeter,
            IsFindingAlreadyKnown);

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
            DetectionOverlayCleaner.ClearVisuals(DetectionCanvas, DetectionOverlayGrid);
        }
    }

}
