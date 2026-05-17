using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.Diagnostics;
using AuswertungPro.Next.Application.Ai.QualityGate;
using AuswertungPro.Next.Application.Ai.Vision;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai;
using AppProtocol = AuswertungPro.Next.Application.Protocol;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    private const double CodingFullScanFrameStepSeconds = 1.0;
    private CancellationTokenSource? _codingBulkScanCts;
    private bool _codingBulkScanRunning;

    private async void CodingFullVideoScan_Click(object sender, RoutedEventArgs e)
    {
        if (_codingBulkScanRunning)
        {
            _codingBulkScanCts?.Cancel();
            SetCodingAiState("Vollscan wird abgebrochen", Color.FromRgb(0x94, 0xA3, 0xB8),
                "aktueller Frame wird noch beendet...");
            return;
        }

        await RunCodingFullVideoScanAsync();
    }

    private async Task RunCodingFullVideoScanAsync()
    {
        if (_codingVm == null || _codingSessionService == null || _haltungRecord == null)
            return;

        _codingBulkScanRunning = true;
        _codingBulkScanCts?.Cancel();
        _codingBulkScanCts?.Dispose();
        _codingBulkScanCts = new CancellationTokenSource();

        var previousAnalyzeEnabled = BtnCodingAnalyze.IsEnabled;
        var previousFullScanContent = BtnCodingFullScan.Content;
        var previousLiveChecked = BtnCodingLiveAi.IsChecked == true;

        try
        {
            _player.SetPause(true);
            if (previousLiveChecked)
            {
                BtnCodingLiveAi.IsChecked = false;
                CodingLiveAi_Click(BtnCodingLiveAi, new RoutedEventArgs());
            }

            BtnCodingAnalyze.IsEnabled = false;
            BtnCodingFullScan.Content = "Scan abbrechen";
            ResetCodingScanPreview("Vollscan startet...");
            SetCodingAiState("Video komplett scannen", Color.FromRgb(0xF5, 0x9E, 0x0B),
                $"FrameStep {CodingFullScanFrameStepSeconds:0.0}s", pulse: true);

            var cfg = AuswertungPro.Next.Application.Ai.AiRuntimeConfigProvider.Load();
            if (!cfg.Enabled)
            {
                SetCodingAiState("Kuenstliche Intelligenz deaktiviert",
                    Color.FromRgb(0x94, 0xA3, 0xB8), "Vollscan abgebrochen");
                return;
            }

            var allowedCodes = App.Resolve<AppProtocol.ICodeCatalogProvider>().AllowedCodes();
            if (allowedCodes is not { Count: > 0 })
            {
                SetCodingAiState("VSA-Code-Katalog leer",
                    Color.FromRgb(0xEF, 0x44, 0x44), "Vollscan abgebrochen");
                return;
            }

            var timeout = cfg.OllamaRequestTimeout > TimeSpan.Zero
                ? cfg.OllamaRequestTimeout
                : TimeSpan.FromMinutes(30);

            using var http = new HttpClient { Timeout = timeout };
            var allowedSet = new HashSet<string>(allowedCodes, StringComparer.OrdinalIgnoreCase);
            var plausibility = new RuleBasedAiSuggestionPlausibilityService(allowedSet);
            var pipeline = new VideoAnalysisPipelineService(cfg, plausibility, http);

            var haltungId = _haltungId
                ?? _haltungRecord.GetFieldValue("Haltungsname")
                ?? _haltungRecord.Id.ToString();

            var request = new PipelineRequest(
                HaltungId: haltungId,
                VideoPath: _videoPath,
                AllowedCodes: allowedCodes,
                FrameStepSeconds: CodingFullScanFrameStepSeconds,
                DedupWindowFrames: 3);

            var progress = new Progress<PipelineProgress>(p =>
            {
                UpdateCodingScanPreview(p);
                var frameText = p.FramesDone.HasValue && p.FramesTotal.HasValue
                    ? $"{p.FramesDone.Value}/{p.FramesTotal.Value}"
                    : $"{p.PercentInPhase:0}%";
                SetCodingAiState("Video komplett scannen", Color.FromRgb(0xF5, 0x9E, 0x0B),
                    $"{frameText} | {TrimStatus(p.Status)}", pulse: true);
            });

            var result = await pipeline.RunAsync(request, progress, _codingBulkScanCts.Token)
                .ConfigureAwait(true);

            if (!result.IsSuccess)
            {
                SetCodingAiState("Vollscan fehlgeschlagen",
                    Color.FromRgb(0xEF, 0x44, 0x44), TrimStatus(result.Error));
                return;
            }

            var added = AddBulkScanResultsToCodingEvents(result);
            var rawCount = result.Detections?.Count ?? 0;
            SetCodingAiState(
                added > 0 ? $"{added} KI-Befunde aus Vollscan" : "Vollscan ohne neue KI-Befunde",
                added > 0 ? Color.FromRgb(0x22, 0xC5, 0x5E) : Color.FromRgb(0x94, 0xA3, 0xB8),
                $"{rawCount} Rohdetektionen | Step {CodingFullScanFrameStepSeconds:0.0}s");
            TxtCodingScanPreviewStatus.Text = added > 0
                ? $"Fertig: {added} Befund(e), {rawCount} Rohdetektionen"
                : $"Fertig: keine neuen Befunde, {rawCount} Rohdetektionen";
        }
        catch (OperationCanceledException)
        {
            SetCodingAiState("Vollscan abgebrochen", Color.FromRgb(0x94, 0xA3, 0xB8),
                $"Modell: {CompactModelName(_codingAiModelName)}");
        }
        catch (Exception ex)
        {
            SetCodingAiState("Vollscan Fehler", Color.FromRgb(0xEF, 0x44, 0x44),
                TrimStatus(ex.Message));
        }
        finally
        {
            StopCodingAiPulse();
            _codingBulkScanRunning = false;
            BtnCodingAnalyze.IsEnabled = previousAnalyzeEnabled;
            BtnCodingFullScan.Content = previousFullScanContent;
        }
    }

    private void ResetCodingScanPreview(string status)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.Invoke(() => ResetCodingScanPreview(status));
            return;
        }

        CodingScanPreviewPanel.Visibility = Visibility.Visible;
        ImgCodingScanPreview.Source = null;
        TxtCodingScanPreviewStatus.Text = status;
        TxtCodingScanPreviewFindings.Text = string.Empty;
    }

    private void UpdateCodingScanPreview(PipelineProgress progress)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.Invoke(() => UpdateCodingScanPreview(progress));
            return;
        }

        CodingScanPreviewPanel.Visibility = Visibility.Visible;
        TxtCodingScanPreviewStatus.Text = BuildScanPreviewStatus(progress);
        TxtCodingScanPreviewFindings.Text = BuildScanPreviewFindings(progress.LiveFindings);

        if (progress.FramePreviewPng is not { Length: > 0 })
            return;

        try
        {
            ImgCodingScanPreview.Source = LoadScanPreviewBitmap(progress.FramePreviewPng);
        }
        catch
        {
            // Preview is diagnostic-only; a corrupt frame must not stop the scan.
        }
    }

    private static string BuildScanPreviewStatus(PipelineProgress progress)
    {
        var frameText = progress.FramesDone.HasValue && progress.FramesTotal.HasValue
            ? $"Frame {progress.FramesDone.Value}/{progress.FramesTotal.Value}"
            : $"{progress.PercentInPhase:0}%";

        var timeText = progress.TimestampSeconds.HasValue
            ? $" | {TimeSpan.FromSeconds(Math.Max(0, progress.TimestampSeconds.Value)):mm\\:ss}"
            : string.Empty;

        return $"{frameText}{timeText} | {TrimStatus(progress.Status)}";
    }

    private static string BuildScanPreviewFindings(IReadOnlyList<LiveFrameFinding>? findings)
    {
        if (findings is not { Count: > 0 })
            return "keine Befunde im aktuellen Frame";

        return string.Join(", ", findings
            .Take(4)
            .Select(f => string.IsNullOrWhiteSpace(f.VsaCodeHint)
                ? f.Label
                : $"{f.VsaCodeHint}: {f.Label}"));
    }

    private static BitmapImage LoadScanPreviewBitmap(byte[] pngBytes)
    {
        using var stream = new MemoryStream(pngBytes);
        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.StreamSource = stream;
        image.EndInit();
        image.Freeze();
        return image;
    }

    private int AddBulkScanResultsToCodingEvents(PipelineResult result)
    {
        if (_codingVm == null || _codingSessionService == null)
            return 0;

        var mapped = result.MappedEntries
            .Where(e => !string.IsNullOrWhiteSpace(e.SuggestedCode))
            .OrderBy(e => e.Detection.MeterStart)
            .ToList();

        var added = 0;
        foreach (var mappedEntry in mapped)
        {
            var code = mappedEntry.SuggestedCode!.Trim();
            var det = mappedEntry.Detection;
            var meter = Math.Round(Math.Max(0, det.MeterStart), 2);

            if (_rejectedFindings.Contains(MakeRejectionKey(code, meter)))
                continue;

            var finding = ToLiveFinding(det, code);
            if (IsDuplicateBulkScanEvent(code, meter, finding))
                continue;

            var entry = BuildBulkScanProtocolEntry(mappedEntry, code, meter);
            var overlay = BuildBulkScanOverlay(finding);
            var codingEvent = _codingSessionService.AddEvent(entry, overlay);
            codingEvent.AiContext = new CodingEventAiContext
            {
                SuggestedCode = code,
                Confidence = mappedEntry.Confidence,
                Reason = BuildBulkScanReason(mappedEntry),
                Decision = mappedEntry.QualityGateResult?.IsGreen == true || mappedEntry.Confidence >= 0.85
                    ? CodingUserDecision.Accepted
                    : CodingUserDecision.Ignored
            };

            AiDiagnosticsRecorderProvider.Current.Record(new AiDiagnosticEvent
            {
                Stage = AiDiagnosticStage.EventCreated,
                Source = nameof(AddBulkScanResultsToCodingEvents),
                Summary = $"{code} @ {meter:F2}m (Vollscan) conf={mappedEntry.Confidence:F2}",
                Metadata = new Dictionary<string, string>
                {
                    ["code"] = code,
                    ["meter"] = meter.ToString("F2", CultureInfo.InvariantCulture),
                    ["confidence"] = mappedEntry.Confidence.ToString("F2", CultureInfo.InvariantCulture),
                    ["source"] = "bulk-video-scan",
                    ["frame_path"] = det.FramePath ?? ""
                }
            });

            added++;
        }

        if (added > 0)
        {
            RefreshCodingEventsList();
            UpdateToolBadge();
        }

        return added;
    }

    private bool IsDuplicateBulkScanEvent(string code, double meter, LiveFrameFinding finding)
    {
        if (_codingVm == null || _codingSessionService == null)
            return true;

        if ((string.Equals(code, "BCD", StringComparison.OrdinalIgnoreCase)
             || string.Equals(code, "BCE", StringComparison.OrdinalIgnoreCase))
            && (_codingSessionService.ActiveSession?.Events.Any(e => CodesMatchForDedup(e.Entry.Code, code)) == true
                || _codingVm.Events.Any(e => CodesMatchForDedup(e.Entry.Code, code))))
        {
            return true;
        }

        return _codingVm.Events.Any(e =>
            CodesMatchForDedup(e.Entry.Code, code) &&
            IsAlreadyCovered(e, meter, finding));
    }

    private ProtocolEntry BuildBulkScanProtocolEntry(
        MappedProtocolEntry mappedEntry,
        string code,
        double meter)
    {
        var det = mappedEntry.Detection;
        var meterEnd = det.MeterEnd > det.MeterStart ? det.MeterEnd : det.MeterStart;
        var isStrecke = meterEnd - det.MeterStart > 0.05
            || AuswertungPro.Next.Application.CodeCatalog.VsaCodeTree.IsStreckenschadenCode(code);

        var entry = new ProtocolEntry
        {
            Source = ProtocolEntrySource.Ai,
            Code = code,
            Beschreibung = LookupVsaLabel(code) ?? det.FindingLabel,
            MeterStart = meter,
            MeterEnd = Math.Round(Math.Max(meter, meterEnd), 2),
            IsStreckenschaden = isStrecke,
            Zeit = det.TimestampSeconds.HasValue
                ? TimeSpan.FromSeconds(Math.Max(0, det.TimestampSeconds.Value))
                : null,
            Ai = new ProtocolEntryAiMeta
            {
                SuggestedCode = code,
                Confidence = mappedEntry.Confidence,
                Reason = mappedEntry.Reason,
                Flags = mappedEntry.Warnings.ToList(),
                Accepted = mappedEntry.QualityGateResult?.IsGreen == true,
                FinalCode = code,
                SuggestedAt = DateTimeOffset.UtcNow
            }
        };

        ApplyBulkScanQuantification(entry, det, code);
        if (!string.IsNullOrWhiteSpace(det.FramePath))
            entry.FotoPaths.Add(det.FramePath!);

        return entry;
    }

    private static LiveFrameFinding ToLiveFinding(RawVideoDetection det, string code)
        => new(
            Label: det.FindingLabel,
            Severity: ParseDetectionSeverity(det.Severity),
            PositionClock: det.PositionClock,
            ExtentPercent: det.ExtentPercent,
            VsaCodeHint: code,
            HeightMm: det.HeightMm,
            WidthMm: det.WidthMm,
            IntrusionPercent: det.IntrusionPercent,
            CrossSectionReductionPercent: det.CrossSectionReductionPercent,
            DiameterReductionMm: det.DiameterReductionMm,
            BboxX1: det.BboxX1,
            BboxY1: det.BboxY1,
            BboxX2: det.BboxX2,
            BboxY2: det.BboxY2);

    private static int ParseDetectionSeverity(string? severity)
    {
        if (int.TryParse(severity, NumberStyles.Integer, CultureInfo.InvariantCulture, out var numeric))
            return Math.Clamp(numeric, 1, 5);

        return severity?.Trim().ToLowerInvariant() switch
        {
            "high" => 4,
            "mid" or "medium" => 3,
            "low" => 2,
            _ => 2
        };
    }

    private static OverlayGeometry? BuildBulkScanOverlay(LiveFrameFinding finding)
    {
        if (!finding.BboxX1.HasValue || !finding.BboxY1.HasValue
            || !finding.BboxX2.HasValue || !finding.BboxY2.HasValue)
            return null;

        var x1 = finding.BboxX1.Value;
        var y1 = finding.BboxY1.Value;
        var x2 = finding.BboxX2.Value;
        var y2 = finding.BboxY2.Value;

        return new OverlayGeometry
        {
            ToolType = OverlayToolType.Rectangle,
            Points = new List<NormalizedPoint>
            {
                new(Math.Min(x1, x2), Math.Min(y1, y2)),
                new(Math.Max(x1, x2), Math.Min(y1, y2)),
                new(Math.Max(x1, x2), Math.Max(y1, y2)),
                new(Math.Min(x1, x2), Math.Max(y1, y2))
            }
        };
    }

    private static void ApplyBulkScanQuantification(
        ProtocolEntry entry,
        RawVideoDetection det,
        string code)
    {
        void EnsureMeta()
        {
            entry.CodeMeta ??= new ProtocolEntryCodeMeta { Code = code };
        }

        if (!string.IsNullOrWhiteSpace(det.PositionClock))
        {
            EnsureMeta();
            entry.CodeMeta!.Parameters["vsa.uhr.von"] = det.PositionClock!;
        }

        if (det.ExtentPercent is > 0)
        {
            EnsureMeta();
            entry.CodeMeta!.Parameters["vsa.umfang.prozent"] =
                det.ExtentPercent.Value.ToString(CultureInfo.InvariantCulture);
        }

        if (det.HeightMm is > 0)
        {
            EnsureMeta();
            entry.CodeMeta!.Parameters["vsa.hoehe.mm"] =
                det.HeightMm.Value.ToString(CultureInfo.InvariantCulture);
        }

        if (det.WidthMm is > 0)
        {
            EnsureMeta();
            entry.CodeMeta!.Parameters["vsa.breite.mm"] =
                det.WidthMm.Value.ToString(CultureInfo.InvariantCulture);
        }

        if (det.CrossSectionReductionPercent is > 0)
        {
            EnsureMeta();
            entry.CodeMeta!.Parameters["vsa.querschnitt.prozent"] =
                det.CrossSectionReductionPercent.Value.ToString(CultureInfo.InvariantCulture);
        }
        else if (det.IntrusionPercent is > 0)
        {
            EnsureMeta();
            entry.CodeMeta!.Parameters["vsa.querschnitt.prozent"] =
                det.IntrusionPercent.Value.ToString(CultureInfo.InvariantCulture);
        }

        if (det.DiameterReductionMm is > 0)
        {
            EnsureMeta();
            entry.CodeMeta!.Parameters["vsa.durchmesserreduktion.mm"] =
                det.DiameterReductionMm.Value.ToString(CultureInfo.InvariantCulture);
        }
    }

    private static string BuildBulkScanReason(MappedProtocolEntry mappedEntry)
    {
        var reason = mappedEntry.Reason;
        if (string.IsNullOrWhiteSpace(reason))
            reason = mappedEntry.Detection.FindingLabel;

        return $"Vollscan: {reason}";
    }
}
