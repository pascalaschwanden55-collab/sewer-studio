using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.Diagnostics;
using AuswertungPro.Next.Application.Ai.Pipeline;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Application.Ai.Vision;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    private const double CodingImportScanWindowSeconds = 1.5;
    private const double CodingImportScanInnerStepSeconds = 0.75;
    private const double CodingImportScanMinConfidence = 0.45;
    private const double CodingImportScanStrongConfidence = 0.75;

    private static readonly double[] CodingImportScanOffsets =
    [
        0.0,
        -CodingImportScanInnerStepSeconds,
        CodingImportScanInnerStepSeconds,
        -CodingImportScanWindowSeconds,
        CodingImportScanWindowSeconds
    ];

    private CancellationTokenSource? _codingImportScanCts;
    private bool _codingImportScanRunning;

    private async void CodingImportScan_Click(object sender, RoutedEventArgs e)
    {
        if (_codingImportScanRunning)
        {
            _codingImportScanCts?.Cancel();
            SetCodingAiState("Importscan wird abgebrochen", Color.FromRgb(0x94, 0xA3, 0xB8),
                "aktueller Frame wird noch beendet...");
            return;
        }

        await RunCodingImportScanAsync();
    }

    private async Task RunCodingImportScanAsync()
    {
        if (_codingVm == null || _codingSessionService == null)
            return;

        if (_codingBulkScanRunning)
        {
            SetCodingAiState("Vollscan laeuft bereits", Color.FromRgb(0x94, 0xA3, 0xB8),
                "erst abbrechen oder beenden");
            return;
        }

        var targets = BuildImportScanTargets();
        if (targets.Count == 0)
        {
            SetCodingAiState("Importliste leer", Color.FromRgb(0x94, 0xA3, 0xB8),
                "keine pruefbaren Codes");
            return;
        }

        if (_codingEnhancedVision == null)
        {
            SetCodingAiState("KI noch nicht bereit", Color.FromRgb(0xEF, 0x44, 0x44),
                $"Modell: {CompactModelName(_codingAiModelName)}");
            return;
        }

        _codingImportScanRunning = true;
        _codingImportScanCts?.Cancel();
        _codingImportScanCts?.Dispose();
        _codingImportScanCts = new CancellationTokenSource();

        var previousAnalyzeEnabled = BtnCodingAnalyze.IsEnabled;
        var previousFullScanEnabled = BtnCodingFullScan.IsEnabled;
        var previousImportScanContent = BtnCodingImportScan.Content;
        var previousLiveChecked = BtnCodingLiveAi.IsChecked == true;

        var addedHits = new List<ImportScanAddedHit>();
        var checkedCount = 0;
        var confirmedCount = 0;
        var failedFrames = 0;
        var ffmpeg = AuswertungPro.Next.Application.Ai.AiRuntimeConfigProvider.Load().FfmpegPath
            ?? FfmpegLocator.ResolveFfmpeg();

        try
        {
            _player.SetPause(true);
            if (previousLiveChecked)
            {
                BtnCodingLiveAi.IsChecked = false;
                CodingLiveAi_Click(BtnCodingLiveAi, new RoutedEventArgs());
            }

            BtnCodingAnalyze.IsEnabled = false;
            BtnCodingFullScan.IsEnabled = false;
            BtnCodingImportScan.Content = "Importscan abbrechen";

            SetCodingAiState("Importliste pruefen", Color.FromRgb(0xF5, 0x9E, 0x0B),
                $"{targets.Count} Eintraege | +/-{CodingImportScanWindowSeconds:0.0}s", pulse: true);

            for (var i = 0; i < targets.Count; i++)
            {
                _codingImportScanCts.Token.ThrowIfCancellationRequested();

                var target = targets[i];
                checkedCount++;
                LstImportEvents.SelectedItem = target.ImportEvent;
                LstImportEvents.ScrollIntoView(target.ImportEvent);

                SetCodingAiState("Importliste pruefen", Color.FromRgb(0xF5, 0x9E, 0x0B),
                    $"{i + 1}/{targets.Count}: {target.Code} @ {target.Meter:F2}m", pulse: true);

                var hit = await VerifyImportTargetAsync(ffmpeg, target, _codingImportScanCts.Token);
                if (hit == null)
                    continue;

                confirmedCount++;
                var created = await AddImportScanHitAsync(hit, _codingImportScanCts.Token);
                if (created != null)
                    addedHits.Add(new ImportScanAddedHit(created, hit));
                else
                    failedFrames++;
            }

            if (addedHits.Count > 0)
            {
                RefreshCodingEventsList();
                UpdateToolBadge();
                ShowImportScanHit(addedHits[0]);
            }

            SetCodingAiState(
                addedHits.Count > 0
                    ? $"{addedHits.Count} Import-Treffer als KI-Befund"
                    : "Importscan ohne neue KI-Befunde",
                addedHits.Count > 0 ? Color.FromRgb(0x22, 0xC5, 0x5E) : Color.FromRgb(0x94, 0xA3, 0xB8),
                $"{checkedCount} geprueft | {confirmedCount} sichtbar | {failedFrames} ohne Event");
        }
        catch (OperationCanceledException)
        {
            SetCodingAiState("Importscan abgebrochen", Color.FromRgb(0x94, 0xA3, 0xB8),
                $"{checkedCount}/{targets.Count} geprueft");
        }
        catch (Exception ex)
        {
            SetCodingAiState("Importscan Fehler", Color.FromRgb(0xEF, 0x44, 0x44),
                TrimStatus(ex.Message));
        }
        finally
        {
            StopCodingAiPulse();
            _codingImportScanRunning = false;
            BtnCodingAnalyze.IsEnabled = previousAnalyzeEnabled;
            BtnCodingFullScan.IsEnabled = previousFullScanEnabled;
            BtnCodingImportScan.Content = previousImportScanContent;
        }
    }

    private List<ImportScanTarget> BuildImportScanTargets()
    {
        return _codingImportEvents
            .Where(e => !string.IsNullOrWhiteSpace(e.Entry?.Code))
            .Select<CodingEvent, ImportScanTarget?>(e =>
            {
                var hasTime = e.Entry.Zeit.HasValue || e.VideoTimestamp.TotalMilliseconds > 0;
                if (!hasTime)
                    return null;

                var code = e.Entry.Code.Trim().ToUpperInvariant();
                var meter = Math.Round(e.Entry.MeterStart ?? e.Entry.MeterEnd ?? e.MeterAtCapture, 2);
                var centerTime = e.Entry.Zeit ?? e.VideoTimestamp;
                return new ImportScanTarget(
                    e,
                    code,
                    e.Entry.Beschreibung ?? code,
                    meter,
                    ClampVideoTime(centerTime));
            })
            .OfType<ImportScanTarget>()
            .Where(t => t.CenterTime >= TimeSpan.Zero)
            .OrderBy(t => t.Meter)
            .ThenBy(t => t.CenterTime)
            .ToList();
    }

    private async Task<ImportScanHit?> VerifyImportTargetAsync(
        string ffmpeg,
        ImportScanTarget target,
        CancellationToken ct)
    {
        ImportScanSample? bestVisible = null;

        foreach (var offset in CodingImportScanOffsets)
        {
            ct.ThrowIfCancellationRequested();

            var sampleTime = ClampVideoTime(target.CenterTime + TimeSpan.FromSeconds(offset));
            SetCodingAiState("Importliste pruefen", Color.FromRgb(0xF5, 0x9E, 0x0B),
                $"{target.Code} @ {target.Meter:F2}m | t={sampleTime.TotalSeconds:F1}s", pulse: true);

            var frameBytes = await VideoFrameExtractor.TryExtractFramePngAsync(
                ffmpeg, _videoPath, sampleTime, ct).ConfigureAwait(true);

            if (frameBytes == null || frameBytes.Length == 0)
            {
                RecordImportScanDrop(target, sampleTime, AiDiagnosticDropReason.FrameExtractFailed,
                    "Frame konnte nicht extrahiert werden");
                continue;
            }

            var b64 = Convert.ToBase64String(frameBytes);
            DamageVerification verify;
            var started = DateTimeOffset.UtcNow;
            try
            {
                verify = await _codingEnhancedVision!.VerifyCodeAsync(
                    b64, target.Code, target.Meter, target.Description, ct).ConfigureAwait(true);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                RecordImportScanDrop(target, sampleTime, AiDiagnosticDropReason.VerificationError,
                    $"VerifyCode Fehler: {ex.Message}");
                continue;
            }

            RecordImportScanVerification(target, sampleTime, verify,
                (DateTimeOffset.UtcNow - started).TotalMilliseconds);

            if (!verify.Visible)
                continue;

            var sample = new ImportScanSample(target, sampleTime, frameBytes, verify);
            if (bestVisible == null || verify.Confidence > bestVisible.Verification.Confidence)
                bestVisible = sample;

            if (verify.Confidence >= CodingImportScanStrongConfidence)
                break;
        }

        if (bestVisible == null)
        {
            RecordImportScanDrop(target, target.CenterTime, AiDiagnosticDropReason.VerificationNotVisible,
                "Code im Analysefenster nicht sichtbar");
            return null;
        }

        if (bestVisible.Verification.Confidence < CodingImportScanMinConfidence)
        {
            RecordImportScanDrop(target, bestVisible.SampleTime, AiDiagnosticDropReason.VerificationLowConfidence,
                $"sichtbar, aber Konfidenz {bestVisible.Verification.Confidence:F2}");
            return null;
        }

        var framePath = await PersistImportScanFrameAsync(bestVisible, ct).ConfigureAwait(true);
        var evidence = await TryBuildImportScanVisualEvidenceAsync(bestVisible, ct).ConfigureAwait(true);
        return new ImportScanHit(bestVisible, framePath, evidence);
    }

    private async Task<CodingEvent?> AddImportScanHitAsync(ImportScanHit hit, CancellationToken ct)
    {
        if (_codingVm == null || _codingSessionService == null)
            return null;

        var target = hit.Sample.Target;
        var code = target.Code;
        var verification = hit.Sample.Verification;
        var finding = hit.VisualEvidence?.Finding
            ?? BuildImportScanFinding(target, verification);

        if (_rejectedFindings.Contains(MakeRejectionKey(code, target.Meter))
            || IsDuplicateBulkScanEvent(code, target.Meter, finding))
        {
            RecordImportScanDrop(target, hit.Sample.SampleTime, AiDiagnosticDropReason.DedupExisting,
                "bereits durch bestehenden Befund abgedeckt");
            return null;
        }

        var sourceEntry = target.ImportEvent.Entry;
        var isStrecke = sourceEntry.IsStreckenschaden
            || AuswertungPro.Next.Application.CodeCatalog.VsaCodeTree.IsStreckenschadenCode(code);

        var entry = new ProtocolEntry
        {
            Source = ProtocolEntrySource.Ai,
            Code = code,
            Beschreibung = LookupVsaLabel(code) ?? target.Description,
            MeterStart = target.Meter,
            IsStreckenschaden = isStrecke,
            Zeit = hit.Sample.SampleTime,
            CodeMeta = CloneImportScanCodeMeta(sourceEntry.CodeMeta, code),
            Ai = new ProtocolEntryAiMeta
            {
                SuggestedCode = code,
                Confidence = verification.Confidence,
                Reason = BuildImportScanReason(hit),
                Flags = BuildImportScanFlags(hit),
                Accepted = verification.Confidence >= CodingImportScanStrongConfidence,
                FinalCode = code,
                SuggestedAt = DateTimeOffset.UtcNow
            }
        };

        if (sourceEntry.MeterEnd is double meterEnd && meterEnd > target.Meter)
            entry.MeterEnd = Math.Round(meterEnd, 2);

        if (hit.FramePath != null)
            entry.FotoPaths.Add(hit.FramePath);

        if (hit.VisualEvidence?.Quantification != null)
            ApplyQuantificationToEntry(entry, code, hit.VisualEvidence.Quantification);

        var codingEvent = _codingSessionService.AddEvent(entry, hit.VisualEvidence?.Overlay);
        codingEvent.AiContext = new CodingEventAiContext
        {
            SuggestedCode = code,
            Confidence = verification.Confidence,
            Reason = BuildImportScanReason(hit),
            Decision = verification.Confidence >= CodingImportScanStrongConfidence
                ? CodingUserDecision.Accepted
                : CodingUserDecision.Ignored
        };

        AiDiagnosticsRecorderProvider.Current.Record(new AiDiagnosticEvent
        {
            Stage = AiDiagnosticStage.EventCreated,
            Source = nameof(AddImportScanHitAsync),
            Model = _codingAiModelName,
            Summary = $"{code} @ {target.Meter:F2}m (Importscan) conf={verification.Confidence:F2}",
            Metadata = new Dictionary<string, string>
            {
                ["code"] = code,
                ["meter"] = target.Meter.ToString("F2", CultureInfo.InvariantCulture),
                ["sample_time"] = hit.Sample.SampleTime.TotalSeconds.ToString("F2", CultureInfo.InvariantCulture),
                ["confidence"] = verification.Confidence.ToString("F2", CultureInfo.InvariantCulture),
                ["visible"] = verification.Visible.ToString(CultureInfo.InvariantCulture),
                ["frame_path"] = hit.FramePath ?? "",
                ["source"] = "import-target-scan"
            }
        });

        await Task.CompletedTask.ConfigureAwait(true);
        return codingEvent;
    }

    private async Task<ImportScanVisualEvidence?> TryBuildImportScanVisualEvidenceAsync(
        ImportScanSample sample,
        CancellationToken ct)
    {
        if (_codingMultiModel == null)
            return null;

        try
        {
            var dn = _codingOverlayService?.Calibration?.NominalDiameterMm ?? 300;
            var result = await _codingMultiModel.AnalyzeFrameAsync(
                sample.FrameBytes, dn, _codingOverlayService?.Calibration, ct).ConfigureAwait(true);

            if (!result.HasDetections || result.QuantifiedMasks.Count == 0)
                return null;

            var maskIndex = SelectImportScanMaskIndex(result, sample.Target.Code);
            if (maskIndex < 0)
                return null;

            var finding = BuildImportScanFinding(sample.Target, sample.Verification, result, maskIndex);
            var overlay = BuildBulkScanOverlay(finding);
            var quant = maskIndex < result.QuantifiedMasks.Count
                ? result.QuantifiedMasks[maskIndex]
                : null;

            return new ImportScanVisualEvidence(result, maskIndex, overlay, finding, quant);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            AiDiagnosticsRecorderProvider.Current.Record(new AiDiagnosticEvent
            {
                Stage = AiDiagnosticStage.YoloError,
                Source = nameof(TryBuildImportScanVisualEvidenceAsync),
                Summary = $"Importscan Visual Evidence fehlgeschlagen: {ex.Message}",
                Metadata = new Dictionary<string, string>
                {
                    ["code"] = sample.Target.Code,
                    ["meter"] = sample.Target.Meter.ToString("F2", CultureInfo.InvariantCulture)
                }
            });
            return null;
        }
    }

    private void ShowImportScanHit(ImportScanAddedHit added)
    {
        try
        {
            var codingEvent = added.Event;
            _player.SetPause(true);
            if (codingEvent.VideoTimestamp.TotalMilliseconds > 0)
                _player.Time = (long)codingEvent.VideoTimestamp.TotalMilliseconds;

            _codingVm!.SelectedDefect = codingEvent;
            LstCodingEvents.SelectedItem = codingEvent;
            LstCodingEvents.ScrollIntoView(codingEvent);
            UpdateCodingDefectDetailPanel(codingEvent);

            UpdateCodingOverlayViewport();
            ClearTransientCodingCanvas(clearManualOverlay: true, clearSamMasks: true);

            var evidence = added.Hit.VisualEvidence;
            if (evidence != null)
                ShowMultiModelResults(evidence.Result, new HashSet<int> { evidence.MaskIndex });

            if (codingEvent.Overlay != null)
                RenderOverlayGeometry(codingEvent.Overlay, isPreview: false);

            UpdateToolBadge();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ImportScan] Trefferanzeige fehlgeschlagen: {ex.Message}");
        }
    }

    private async Task<string?> PersistImportScanFrameAsync(ImportScanSample sample, CancellationToken ct)
    {
        try
        {
            var dir = FrameStore.GetFramesDir();
            Directory.CreateDirectory(dir);

            var haltung = SanitizeImportScanToken(_haltungId
                ?? _haltungRecord?.GetFieldValue("Haltungsname")
                ?? "haltung");
            var code = SanitizeImportScanToken(sample.Target.Code);
            var meter = sample.Target.Meter.ToString("F2", CultureInfo.InvariantCulture).Replace('.', '_');
            var time = sample.SampleTime.TotalSeconds.ToString("F2", CultureInfo.InvariantCulture).Replace('.', '_');
            var fileName = $"importscan_{haltung}_{code}_{meter}m_{time}s.png";
            var path = Path.Combine(dir, fileName);

            if (!File.Exists(path))
                await File.WriteAllBytesAsync(path, sample.FrameBytes, ct).ConfigureAwait(false);

            return path;
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ImportScan] Frame persistieren fehlgeschlagen: {ex.Message}");
            return null;
        }
    }

    private int SelectImportScanMaskIndex(SingleFrameResult result, string targetCode)
    {
        for (var i = 0; i < result.QuantifiedMasks.Count; i++)
        {
            var inferred = VsaCodeResolver.InferCodeFromLabel(result.QuantifiedMasks[i].Label);
            if (inferred != null && CodesMatchForDedup(inferred, targetCode))
                return i;
        }

        return result.QuantifiedMasks.Count == 1 ? 0 : -1;
    }

    private static LiveFrameFinding BuildImportScanFinding(
        ImportScanTarget target,
        DamageVerification verification)
    {
        return new LiveFrameFinding(
            Label: target.Description,
            Severity: Math.Clamp(verification.Severity ?? ConfidenceToSeverity(verification.Confidence), 1, 5),
            PositionClock: TryReadClock(target.ImportEvent.Entry),
            ExtentPercent: null,
            VsaCodeHint: target.Code);
    }

    private static LiveFrameFinding BuildImportScanFinding(
        ImportScanTarget target,
        DamageVerification verification,
        SingleFrameResult result,
        int maskIndex)
    {
        var quant = maskIndex < result.QuantifiedMasks.Count ? result.QuantifiedMasks[maskIndex] : null;
        var dino = maskIndex < result.DinoDetections.Count ? result.DinoDetections[maskIndex] : null;
        var imgW = result.SamResponse?.ImageWidth ?? 1;
        var imgH = result.SamResponse?.ImageHeight ?? 1;

        return new LiveFrameFinding(
            Label: quant?.Label ?? target.Description,
            Severity: Math.Clamp(verification.Severity ?? (quant != null ? EstimateSeverityFromQuantification(quant) : ConfidenceToSeverity(verification.Confidence)), 1, 5),
            PositionClock: NormalizeClockPosition(quant?.ClockPosition) ?? TryReadClock(target.ImportEvent.Entry),
            ExtentPercent: quant?.ExtentPercent,
            VsaCodeHint: target.Code,
            HeightMm: quant?.HeightMm,
            WidthMm: quant?.WidthMm,
            IntrusionPercent: quant?.IntrusionPercent,
            CrossSectionReductionPercent: quant?.CrossSectionReductionPercent,
            DiameterReductionMm: null,
            BboxX1: dino != null ? dino.X1 / imgW : null,
            BboxY1: dino != null ? dino.Y1 / imgH : null,
            BboxX2: dino != null ? dino.X2 / imgW : null,
            BboxY2: dino != null ? dino.Y2 / imgH : null);
    }

    private static ProtocolEntryCodeMeta? CloneImportScanCodeMeta(
        ProtocolEntryCodeMeta? source,
        string code)
    {
        if (source == null)
            return null;

        return new ProtocolEntryCodeMeta
        {
            Code = string.IsNullOrWhiteSpace(source.Code) ? code : source.Code,
            Parameters = new Dictionary<string, string>(source.Parameters, StringComparer.OrdinalIgnoreCase),
            Severity = source.Severity,
            Count = source.Count,
            Notes = source.Notes,
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }

    private static string? TryReadClock(ProtocolEntry entry)
    {
        if (entry.CodeMeta?.Parameters?.TryGetValue("vsa.uhr.von", out var clock) == true)
            return NormalizeClockPosition(clock);

        return null;
    }

    private TimeSpan ClampVideoTime(TimeSpan time)
    {
        if (time < TimeSpan.Zero)
            return TimeSpan.Zero;

        if (_player.Length > 0)
        {
            var max = TimeSpan.FromMilliseconds(_player.Length);
            if (time > max)
                return max;
        }

        return time;
    }

    private void RecordImportScanVerification(
        ImportScanTarget target,
        TimeSpan sampleTime,
        DamageVerification verification,
        double latencyMs)
    {
        AiDiagnosticsRecorderProvider.Current.Record(new AiDiagnosticEvent
        {
            Stage = AiDiagnosticStage.QwenMapped,
            Source = nameof(VerifyImportTargetAsync),
            Model = _codingAiModelName,
            InputHash = $"{target.Code}@{target.Meter:F2}@{sampleTime.TotalSeconds:F2}",
            Summary = $"{target.Code} @ {target.Meter:F2}m visible={verification.Visible} conf={verification.Confidence:F2}",
            RawOutput = verification.Notes,
            LatencyMs = latencyMs,
            Metadata = new Dictionary<string, string>
            {
                ["code"] = target.Code,
                ["meter"] = target.Meter.ToString("F2", CultureInfo.InvariantCulture),
                ["sample_time"] = sampleTime.TotalSeconds.ToString("F2", CultureInfo.InvariantCulture),
                ["visible"] = verification.Visible.ToString(CultureInfo.InvariantCulture),
                ["confidence"] = verification.Confidence.ToString("F2", CultureInfo.InvariantCulture),
                ["source"] = "import-target-scan"
            }
        });
    }

    private void RecordImportScanDrop(
        ImportScanTarget target,
        TimeSpan sampleTime,
        string reason,
        string summary)
    {
        AiDiagnosticsRecorderProvider.Current.Record(new AiDiagnosticEvent
        {
            Stage = AiDiagnosticStage.CodingFilterDrop,
            Source = nameof(VerifyImportTargetAsync),
            Model = _codingAiModelName,
            InputHash = $"{target.Code}@{target.Meter:F2}@{sampleTime.TotalSeconds:F2}",
            Summary = $"{target.Code} @ {target.Meter:F2}m: {summary}",
            DroppedReason = reason,
            Metadata = new Dictionary<string, string>
            {
                ["code"] = target.Code,
                ["meter"] = target.Meter.ToString("F2", CultureInfo.InvariantCulture),
                ["sample_time"] = sampleTime.TotalSeconds.ToString("F2", CultureInfo.InvariantCulture),
                ["source"] = "import-target-scan"
            }
        });
    }

    private static int ConfidenceToSeverity(double confidence)
        => confidence switch
        {
            >= 0.90 => 5,
            >= 0.75 => 4,
            >= 0.55 => 3,
            _ => 2
        };

    private static string BuildImportScanReason(ImportScanHit hit)
    {
        var notes = hit.Sample.Verification.Notes;
        if (string.IsNullOrWhiteSpace(notes))
            notes = hit.Sample.Target.Description;

        return $"Importscan: {notes}";
    }

    private static List<string> BuildImportScanFlags(ImportScanHit hit)
    {
        var flags = new List<string>
        {
            "import-target-scan",
            $"sample_time={hit.Sample.SampleTime.TotalSeconds:F2}s"
        };

        if (hit.VisualEvidence?.Overlay == null)
            flags.Add("no-segmentation-overlay");

        return flags;
    }

    private static string SanitizeImportScanToken(string value)
    {
        var chars = value
            .Select(ch => char.IsLetterOrDigit(ch) || ch is '_' or '-' ? ch : '_')
            .ToArray();
        var token = new string(chars).Trim('_');
        if (string.IsNullOrWhiteSpace(token))
            token = "x";

        return token.Length <= 48 ? token : token[..48];
    }

    private sealed record ImportScanTarget(
        CodingEvent ImportEvent,
        string Code,
        string Description,
        double Meter,
        TimeSpan CenterTime);

    private sealed record ImportScanSample(
        ImportScanTarget Target,
        TimeSpan SampleTime,
        byte[] FrameBytes,
        DamageVerification Verification);

    private sealed record ImportScanHit(
        ImportScanSample Sample,
        string? FramePath,
        ImportScanVisualEvidence? VisualEvidence);

    private sealed record ImportScanAddedHit(
        CodingEvent Event,
        ImportScanHit Hit);

    private sealed record ImportScanVisualEvidence(
        SingleFrameResult Result,
        int MaskIndex,
        OverlayGeometry? Overlay,
        LiveFrameFinding Finding,
        MaskQuantificationService.QuantifiedMask? Quantification);
}
