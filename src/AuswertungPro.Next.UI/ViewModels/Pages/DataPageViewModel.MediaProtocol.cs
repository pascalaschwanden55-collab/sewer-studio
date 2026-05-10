using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.Vision;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.Infrastructure.Import.Xtf;
using AuswertungPro.Next.Infrastructure.Media;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Dialogs;
using AuswertungPro.Next.UI.ViewModels.Windows;
using AuswertungPro.Next.UI.Views;
using AuswertungPro.Next.UI.Views.Windows;

namespace AuswertungPro.Next.UI.ViewModels.Pages;

// Video-Wiedergabe + Protokoll-Workflow + Pfad-Aufloesung:
// - PlayVideo, RelinkVideo, EnsureVideoPath, SaveVideoLink, OpenMediaSearchWindow
// - OpenProtocol, SyncObservationsToHoldingFields + Mapping-Helper
//   (BuildPrimaryDamageLines, BuildFindings, FindingFingerprint,
//    NormalizeCodeToken, ResolveFindingEffectiveCode, AreCodesCompatible,
//    AreMetersClose, NormalizeInlineText, GetCodeMetaParameter, FormatNullableDouble)
// - OpenVideoAiPipeline + CloneProtocolEntry
// - EnsureProtocolPath + TryResolve/TryFind/PickBest-Helfer + Stored-Path-Resolver
// - OpenHydraulikPanel
public sealed partial class DataPageViewModel
{
    private void PlayVideo(HaltungRecord? record)
    {
        if (record is null)
            return;

        var path = EnsureVideoPath(record);
        if (string.IsNullOrWhiteSpace(path))
            return;

        try
        {
            var options = new PlayerWindowOptions(
                EnableHardwareDecoding: App.Resolve<AppSettings>().VideoHwDecoding,
                DropLateFrames: App.Resolve<AppSettings>().VideoDropLateFrames,
                SkipFrames: App.Resolve<AppSettings>().VideoSkipFrames,
                FileCachingMs: App.Resolve<AppSettings>().VideoFileCachingMs,
                NetworkCachingMs: App.Resolve<AppSettings>().VideoNetworkCachingMs,
                CodecThreads: App.Resolve<AppSettings>().VideoCodecThreads,
                VideoOutput: App.Resolve<AppSettings>().VideoOutput);

            // Build damage overlay markers from protocol entries
            PlayerDamageOverlayData? damageOverlay = null;
            var lengthStr = record.GetFieldValue("Haltungslaenge_m");
            if (double.TryParse(lengthStr?.Replace(',', '.'),
                    NumberStyles.Float, CultureInfo.InvariantCulture, out var pipeLength)
                && pipeLength > 0)
            {
                var markers = new System.Collections.Generic.List<DamageMarkerInfo>();

                if (record.Protocol?.Current?.Entries is { Count: > 0 } entries)
                {
                    foreach (var e in entries.Where(e => !e.IsDeleted && e.MeterStart.HasValue))
                    {
                        markers.Add(new DamageMarkerInfo(
                            e.Code ?? "",
                            e.Beschreibung,
                            e.MeterStart!.Value,
                            e.MeterEnd,
                            e.IsStreckenschaden));
                    }
                }
                else if (record.VsaFindings is { Count: > 0 } findings)
                {
                    foreach (var f in findings)
                    {
                        var mStart = f.MeterStart ?? f.SchadenlageAnfang;
                        if (mStart is null) continue;
                        var mEnd = f.MeterEnd ?? f.SchadenlageEnde;
                        markers.Add(new DamageMarkerInfo(
                            f.KanalSchadencode?.Trim() ?? "",
                            f.Raw,
                            mStart.Value,
                            mEnd,
                            mEnd.HasValue && mEnd.Value > mStart.Value));
                    }
                }

                if (markers.Count > 0)
                    damageOverlay = new PlayerDamageOverlayData(pipeLength, markers);
            }

            // Slice 8a.5 Sub-Slice 2 (2026-05-10): DataPage oeffnet direkt
            // das CodingModeWindow statt des PlayerWindow. User-Wunsch:
            // "Videoplayer und Codiermodus sind dasselbe Fenster" — beim Video-
            // Oeffnen sofort im Codiermodus landen.
            //
            // Funktions-Delta gegenueber PlayerWindow-Pfad (kommt in spaeteren
            // Sub-Slices zurueck):
            // - damageOverlay (Schaden-Marker am Slider): heute weg, Codier-
            //   modus zeigt stattdessen seine PipeGraphTimeline.
            // - PlayerWindowOptions (HW-Decoding etc.): heute weg, Codier-
            //   modus nutzt eigene LibVLC-Args.
            // - haltungId-Parameter: heute weg, record.Id ist im Codiermodus
            //   ueber den HaltungRecord verfuegbar.
            //
            // _ = options + damageOverlay + record.Id explizit verworfen,
            // damit kein "unused"-Warning entsteht und die Build-Pipeline
            // nicht meckert. Wird in Phase 8a.5.2 (DamageOverlay-Port nach
            // CodingMode) wieder genutzt.
            _ = options;
            _ = damageOverlay;
            var window = new CodingModeWindow(record, path);
            // Slice 8a.5 Sub-Slice 2 (2026-05-10): MainWindow als Owner +
            // ShowDialog. Top-Level-Window mit Show() bewirkte einen WPF-
            // Mouse-Routing-Bug: BBox-Drawing auf der OverlayCanvas im
            // Codiermodus reagierte nicht. Owner+ShowDialog stellt die gleiche
            // Window-Hierarchie her wie der frueher PlayerWindow→CodingMode-Pfad.
            var mainWin = System.Windows.Application.Current?.MainWindow;
            if (mainWin != null && !ReferenceEquals(mainWin, window))
                window.Owner = mainWin;
            var result = window.ShowDialog();

            // Bug-Fix 2026-05-10: CompletedProtocol-Synchronisation. Frueher
            // Bridge-Pfad (PlayerWindow.CodingApply.CodingMode_Click) hat bei
            // erfolgreichem Abschluss record.Protocol/ModifiedAtUtc gesetzt
            // und Primaere_Schaeden gespiegelt. In der ersten Version dieses
            // Direkt-Pfads (Sub-Slice 2) war das vergessen — Codierung wurde
            // gespeichert aber DataPage zeigte den alten Stand.
            if (result == true && window.CompletedProtocol != null)
            {
                record.Protocol = window.CompletedProtocol;
                record.ModifiedAtUtc = DateTime.UtcNow;
                SyncCodingToPrimaryDamages(record, window.CompletedProtocol);
            }
        }
        catch (Exception ex)
        {
            var logPath = TryWriteVideoStartErrorLog(ex, path);
            var nativeHint = ex.Message.Contains("native side", StringComparison.OrdinalIgnoreCase)
                ? "\n\nHinweis: Bitte pruefen, ob 'VideoLAN.LibVLC.Windows' fuer dieses Projekt/Plattform installiert ist."
                : string.Empty;
            var msg = logPath is null
                ? $"Video konnte nicht gestartet werden:\n{ex.Message}{nativeHint}\n\n(Details: ex.ToString() nicht gespeichert)"
                : $"Video konnte nicht gestartet werden:\n{ex.Message}{nativeHint}\n\nDetails gespeichert in:\n{logPath}";
            _dialogs.ShowMessage(msg, "Video", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>Spiegelt das Codiermodus-Protokoll auf das Primaere_Schaeden-Feld
    /// des HaltungRecord. Logik dupliziert aus PlayerWindow.ImportProtocol.cs;
    /// gehoert in eine eigene Application-Layer-Datei wenn der PlayerWindow-
    /// Pfad spaeter ganz weg ist (Phase 8a.5.4).</summary>
    private static void SyncCodingToPrimaryDamages(HaltungRecord record, ProtocolDocument doc)
    {
        var entries = doc.Current?.Entries?
            .Where(e => !e.IsDeleted && !string.IsNullOrWhiteSpace(e.Code))
            .ToList();
        if (entries == null || entries.Count == 0)
        {
            record.SetFieldValue("Primaere_Schaeden", "", FieldSource.Manual, userEdited: true);
            record.ModifiedAtUtc = DateTime.UtcNow;
            return;
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var lines = new List<string>();
        foreach (var entry in entries)
        {
            var code = (entry.Code ?? "").Trim();
            if (string.IsNullOrWhiteSpace(code)) continue;

            var meter = entry.MeterStart ?? entry.MeterEnd;
            var meterKey = meter.HasValue ? meter.Value.ToString("F2") : "";
            if (!seen.Add($"{code.ToUpperInvariant()}|{meterKey}")) continue;

            var parts = new List<string>();
            if (meter.HasValue) parts.Add($"{meter.Value:0.00}m");
            parts.Add(code);
            if (!string.IsNullOrWhiteSpace(entry.Beschreibung))
                parts.Add(entry.Beschreibung.Trim().Replace("\r", "").Replace("\n", " "));

            if (entry.CodeMeta?.Parameters != null)
            {
                if (entry.CodeMeta.Parameters.TryGetValue("vsa.q1", out var q1) && !string.IsNullOrWhiteSpace(q1))
                    parts.Add($"Q1={q1}");
                if (entry.CodeMeta.Parameters.TryGetValue("vsa.q2", out var q2) && !string.IsNullOrWhiteSpace(q2))
                    parts.Add($"Q2={q2}");
            }

            lines.Add(string.Join(" ", parts));
        }

        record.SetFieldValue("Primaere_Schaeden", string.Join("\n", lines),
            FieldSource.Manual, userEdited: true);
        record.ModifiedAtUtc = DateTime.UtcNow;
    }

    private void OpenProtocol(HaltungRecord? record)
    {
        if (record is null)
            return;

        var projectFolder = string.IsNullOrWhiteSpace(App.Resolve<AppSettings>().LastProjectPath)
            ? null
            : Path.GetDirectoryName(App.Resolve<AppSettings>().LastProjectPath);

        var resolvedVideoPath = ResolveExistingPath(record.GetFieldValue("Link"));
        var dlg = new AuswertungPro.Next.UI.Views.ProtocolObservationsWindow(
            record,
            _shell.Project,
            resolvedVideoPath,
            projectFolder,
            markDirty: () =>
            {
                _shell.Project.ModifiedAtUtc = DateTime.UtcNow;
                _shell.Project.Dirty = true;
                ScheduleAutoSave();
            });
        dlg.Owner = System.Windows.Application.Current?.MainWindow;
        App.Resolve<IDialogService>().ShowDialog(dlg);

        // Protokoll-Änderungen in die Haltungsfelder zurückschreiben.
        SyncObservationsToHoldingFields(record);

        if (Selected?.Id == record.Id)
            RefreshSelectedProtocolEntries();
    }

    public void SyncObservationsToHoldingFields(HaltungRecord? record, bool showStatus = false)
    {
        if (record is null)
            return;

        var entries = record.Protocol?.Current?.Entries?
            .Where(e => !e.IsDeleted && !string.IsNullOrWhiteSpace(e.Code))
            .ToList();
        if (entries is null)
            return;

        var changed = false;

        var primaryLines = BuildPrimaryDamageLinesFromProtocolEntries(entries);
        var primaryText = XtfPrimaryDamageFormatter.DeduplicateText(string.Join("\n", primaryLines));
        var currentPrimary = record.GetFieldValue("Primaere_Schaeden") ?? string.Empty;
        if (!string.Equals(currentPrimary, primaryText, StringComparison.Ordinal))
        {
            record.SetFieldValue("Primaere_Schaeden", primaryText, FieldSource.Manual, userEdited: true);
            changed = true;
        }

        var mergedFindings = BuildFindingsFromProtocolEntries(entries, record.VsaFindings);
        if (HasFindingChanges(record.VsaFindings, mergedFindings))
        {
            record.VsaFindings = mergedFindings;
            changed = true;
        }

        if (!changed)
            return;

        _shell.Project.ModifiedAtUtc = DateTime.UtcNow;
        _shell.Project.Dirty = true;
        RefreshRecordInGrid(record);

        if (Selected?.Id == record.Id)
            RefreshSelectedProtocolEntries();

        ScheduleAutoSave();
        if (showStatus)
            _shell.SetStatus("Beobachtungen in Haltungen-Feldern aktualisiert");
    }

    private static List<string> BuildPrimaryDamageLinesFromProtocolEntries(IEnumerable<ProtocolEntry> entries)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var lines = new List<string>();
        foreach (var entry in entries)
        {
            var code = (entry.Code ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(code))
                continue;

            // Deduplicate by code + meter position
            var meter = entry.MeterStart ?? entry.MeterEnd;
            var meterKey = meter.HasValue ? meter.Value.ToString("F2") : "";
            var key = $"{code.ToUpperInvariant()}|{meterKey}";
            if (!seen.Add(key))
                continue;

            var parts = new List<string>();
            if (meter.HasValue)
                parts.Add($"{meter.Value:0.00}m");

            parts.Add(code);

            var description = NormalizeInlineText(entry.Beschreibung);
            if (!string.IsNullOrWhiteSpace(description))
                parts.Add(description);

            var q1 = GetCodeMetaParameter(entry, "Quantifizierung1", "vsa.q1");
            var q2 = GetCodeMetaParameter(entry, "Quantifizierung2", "vsa.q2");
            if (!string.IsNullOrWhiteSpace(q1))
                parts.Add($"Q1={q1}");
            if (!string.IsNullOrWhiteSpace(q2))
                parts.Add($"Q2={q2}");

            lines.Add(string.Join(" ", parts.Where(p => !string.IsNullOrWhiteSpace(p))));
        }

        return lines;
    }

    private static List<VsaFinding> BuildFindingsFromProtocolEntries(
        IReadOnlyList<ProtocolEntry> entries,
        IReadOnlyList<VsaFinding>? existingFindings)
    {
        var existing = existingFindings ?? Array.Empty<VsaFinding>();
        var list = new List<VsaFinding>(entries.Count);

        foreach (var entry in entries)
        {
            var code = (entry.Code ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(code))
                continue;

            var meterStart = entry.MeterStart;
            var meterEnd = entry.MeterEnd;
            var q1 = GetCodeMetaParameter(entry, "Quantifizierung1", "vsa.q1");
            var q2 = GetCodeMetaParameter(entry, "Quantifizierung2", "vsa.q2");
            var photo = entry.FotoPaths?.FirstOrDefault(p => !string.IsNullOrWhiteSpace(p));

            var template = existing.FirstOrDefault(f =>
                AreCodesCompatible(code, f.KanalSchadencode) && AreMetersClose(meterStart, f.MeterStart ?? f.SchadenlageAnfang, 0.15));

            var finding = new VsaFinding
            {
                KanalSchadencode = code,
                Raw = (entry.Beschreibung ?? string.Empty).Trim(),
                MeterStart = meterStart,
                MeterEnd = meterEnd,
                SchadenlageAnfang = meterStart,
                SchadenlageEnde = meterEnd,
                Quantifizierung1 = q1,
                Quantifizierung2 = q2,
                MPEG = string.IsNullOrWhiteSpace(entry.Mpeg) ? template?.MPEG : entry.Mpeg,
                FotoPath = string.IsNullOrWhiteSpace(photo) ? template?.FotoPath : photo,
                EZD = template?.EZD,
                EZS = template?.EZS,
                EZB = template?.EZB
            };

            if (entry.Zeit.HasValue)
                finding.Timestamp = DateTime.Today.Add(entry.Zeit.Value);
            else
                finding.Timestamp = template?.Timestamp;

            if (entry.IsStreckenschaden && meterStart.HasValue && meterEnd.HasValue && meterEnd.Value >= meterStart.Value)
                finding.LL = meterEnd.Value - meterStart.Value;
            else
                finding.LL = template?.LL;

            list.Add(finding);
        }

        return list;
    }

    private static bool HasFindingChanges(IReadOnlyList<VsaFinding>? oldFindings, IReadOnlyList<VsaFinding> newFindings)
    {
        var oldList = oldFindings ?? Array.Empty<VsaFinding>();
        if (oldList.Count != newFindings.Count)
            return true;

        for (var i = 0; i < oldList.Count; i++)
        {
            if (!string.Equals(BuildFindingFingerprint(oldList[i]), BuildFindingFingerprint(newFindings[i]), StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    private static string BuildFindingFingerprint(VsaFinding finding)
    {
        return string.Join("|",
            NormalizeCodeToken(finding.KanalSchadencode),
            FormatNullableDouble(finding.MeterStart),
            FormatNullableDouble(finding.MeterEnd),
            FormatNullableDouble(finding.SchadenlageAnfang),
            FormatNullableDouble(finding.SchadenlageEnde),
            finding.Raw?.Trim() ?? string.Empty,
            finding.Quantifizierung1?.Trim() ?? string.Empty,
            finding.Quantifizierung2?.Trim() ?? string.Empty,
            finding.MPEG?.Trim() ?? string.Empty,
            finding.FotoPath?.Trim() ?? string.Empty);
    }

    private static string NormalizeCodeToken(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;
        var upper = value.Trim().ToUpperInvariant();
        return Regex.Replace(upper, "[^A-Z0-9]", string.Empty);
    }

    private static string ResolveFindingEffectiveCode(string? code, string? rawDescription)
    {
        var normalizedCode = NormalizeCodeToken(code);
        if (!ContinuousDefectMarkerRegex.IsMatch(normalizedCode) || string.IsNullOrWhiteSpace(rawDescription))
            return normalizedCode;

        var text = rawDescription.Trim();
        if (text.StartsWith("("))
            text = text.Substring(1).TrimStart();

        var match = EmbeddedVsaCodeRegex.Match(text);
        return match.Success ? NormalizeCodeToken(match.Groups[1].Value) : normalizedCode;
    }

    private static bool AreCodesCompatible(string? left, string? right)
    {
        var a = NormalizeCodeToken(left);
        var b = NormalizeCodeToken(right);
        if (a.Length == 0 || b.Length == 0)
            return false;
        return string.Equals(a, b, StringComparison.Ordinal)
               || a.StartsWith(b, StringComparison.Ordinal)
               || b.StartsWith(a, StringComparison.Ordinal);
    }

    private static bool AreMetersClose(double? left, double? right, double tolerance)
    {
        if (!left.HasValue || !right.HasValue)
            return false;
        return Math.Abs(left.Value - right.Value) <= tolerance;
    }

    private static string NormalizeInlineText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var oneLine = string.Join(" ",
            value.Replace("\r\n", "\n")
                 .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                 .Select(s => s.Trim())
                 .Where(s => s.Length > 0));

        return string.Join(" ", oneLine.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    private static string? GetCodeMetaParameter(ProtocolEntry entry, params string[] keys)
    {
        if (entry.CodeMeta?.Parameters is null || keys.Length == 0)
            return null;

        foreach (var key in keys)
        {
            if (entry.CodeMeta.Parameters.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
                return value.Trim();
        }

        return null;
    }

    private static string FormatNullableDouble(double? value)
        => value.HasValue ? value.Value.ToString("0.###", CultureInfo.InvariantCulture) : string.Empty;

    private void OpenVideoAiPipeline(HaltungRecord? record)
    {
        if (record is null) return;

        var videoPath = EnsureVideoPath(record);
        if (string.IsNullOrWhiteSpace(videoPath)) return;

        var allowedCodes = App.Resolve<AuswertungPro.Next.Application.Protocol.ICodeCatalogProvider>().AllowedCodes();
        if (allowedCodes is null || allowedCodes.Count == 0)
        {
            _dialogs.ShowMessage("VSA-Code-Katalog ist leer oder nicht geladen.", "Videoanalyse KI",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var cfg = AuswertungPro.Next.Application.Ai.AiRuntimeConfigProvider.Load();
        if (!cfg.Enabled)
        {
            _dialogs.ShowMessage("KI ist deaktiviert (SEWERSTUDIO_AI_ENABLED=0).", "Videoanalyse KI",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var timeout = cfg.OllamaRequestTimeout > TimeSpan.Zero
            ? cfg.OllamaRequestTimeout
            : TimeSpan.FromMinutes(30);
        using var http = new HttpClient { Timeout = timeout };
        var allowedSet = new HashSet<string>(allowedCodes, StringComparer.OrdinalIgnoreCase);
        var plausibility = new RuleBasedAiSuggestionPlausibilityService(allowedSet);
        // Phase 5.1.B Etappe 3.L: Direkt new() statt Bundle-Methode.
        var pipeline = new Ai.VideoAnalysisPipelineService(cfg, plausibility, http);

        var haltungId = record.GetFieldValue("Haltungsname") ?? record.Id.ToString();
        var request = new PipelineRequest(haltungId, videoPath, allowedCodes);

        var win = new VideoAnalysisPipelineWindow(request, pipeline)
        {
            Owner = System.Windows.Application.Current?.MainWindow
        };

        var ok = App.Resolve<IDialogService>().ShowDialog(win) == true;

        if (ok && win.Result?.IsSuccess == true && win.Result.Document is not null)
        {
            record.Protocol = win.Result.Document;

            _shell.Project.ModifiedAtUtc = DateTime.UtcNow;
            _shell.Project.Dirty = true;

            RefreshRecordInGrid(record);
            if (Selected?.Id == record.Id)
                RefreshSelectedProtocolEntries();

            ScheduleAutoSave();
        }
    }


    private static ProtocolEntry CloneProtocolEntry(ProtocolEntry source)
    {
        var json = JsonSerializer.Serialize(source);
        return JsonSerializer.Deserialize<ProtocolEntry>(json) ?? new ProtocolEntry();
    }

    private string? EnsureProtocolPath(HaltungRecord record)
    {
        var holdingTokens = BuildHoldingTokens(record);

        var resolvedLink = ResolveExistingPath(record.GetFieldValue("Link"));
        var fromLink = TryResolveProtocolFromLink(resolvedLink, holdingTokens);
        if (!string.IsNullOrWhiteSpace(fromLink))
            return fromLink;

        var initial = !string.IsNullOrWhiteSpace(App.Resolve<AppSettings>().LastVideoSourceFolder)
            ? App.Resolve<AppSettings>().LastVideoSourceFolder
            : !string.IsNullOrWhiteSpace(App.Resolve<AppSettings>().LastVideoFolder)
                ? App.Resolve<AppSettings>().LastVideoFolder
            : App.Resolve<AppSettings>().LastProjectPath is null
                ? null
                : Path.GetDirectoryName(App.Resolve<AppSettings>().LastProjectPath);

        var fromInitial = TryFindProtocolFromRoot(initial, holdingTokens);
        if (!string.IsNullOrWhiteSpace(fromInitial))
            return fromInitial;

        if (!string.IsNullOrWhiteSpace(App.Resolve<AppSettings>().LastProjectPath))
        {
            var projectDir = Path.GetDirectoryName(App.Resolve<AppSettings>().LastProjectPath);
            if (!string.IsNullOrWhiteSpace(projectDir))
            {
                var fromHoldings = TryFindProtocolFromRoot(Path.Combine(projectDir, "Haltungen"), holdingTokens);
                if (!string.IsNullOrWhiteSpace(fromHoldings))
                    return fromHoldings;

                var fromStored = TryFindProtocolFromStoredPdfFiles(projectDir, holdingTokens);
                if (!string.IsNullOrWhiteSpace(fromStored))
                    return fromStored;
            }
        }

        return null;
    }

    private string? TryResolveProtocolFromLink(string? resolvedLink, IReadOnlyList<string> holdingTokens)
    {
        if (string.IsNullOrWhiteSpace(resolvedLink))
            return null;

        if (string.Equals(Path.GetExtension(resolvedLink), ".pdf", StringComparison.OrdinalIgnoreCase))
            return resolvedLink;

        var folder = Path.GetDirectoryName(resolvedLink);
        if (string.IsNullOrWhiteSpace(folder))
            return null;

        var inSameFolder = TryFindPdfInDirectory(folder, holdingTokens, SearchOption.TopDirectoryOnly);
        if (!string.IsNullOrWhiteSpace(inSameFolder))
            return inSameFolder;

        try
        {
            var parent = Directory.GetParent(folder);
            if (parent is not null && string.Equals(parent.Name, "__UNMATCHED", StringComparison.OrdinalIgnoreCase))
            {
                var gemeindeRoot = parent.Parent?.FullName;
                if (!string.IsNullOrWhiteSpace(gemeindeRoot))
                {
                    var inGemeinde = TryFindProtocolFromRoot(gemeindeRoot, holdingTokens);
                    if (!string.IsNullOrWhiteSpace(inGemeinde))
                        return inGemeinde;
                }
            }
        }
        catch
        {
            // Continue with other lookup strategies.
        }

        return null;
    }

    private string? TryFindProtocolFromRoot(string? rootDir, IReadOnlyList<string> holdingTokens)
    {
        if (string.IsNullOrWhiteSpace(rootDir) || !Directory.Exists(rootDir))
            return null;

        var holdingDir = TryFindHoldingDirectory(rootDir, holdingTokens);
        if (!string.IsNullOrWhiteSpace(holdingDir))
        {
            var inHolding = TryFindPdfInDirectory(holdingDir, holdingTokens, SearchOption.TopDirectoryOnly);
            if (!string.IsNullOrWhiteSpace(inHolding))
                return inHolding;

            var inHoldingRecursive = TryFindPdfInDirectory(holdingDir, holdingTokens, SearchOption.AllDirectories);
            if (!string.IsNullOrWhiteSpace(inHoldingRecursive))
                return inHoldingRecursive;
        }

        return TryFindPdfInDirectory(rootDir, holdingTokens, SearchOption.AllDirectories);
    }

    private string? TryFindProtocolFromStoredPdfFiles(string projectDir, IReadOnlyList<string> holdingTokens)
    {
        if (!_shell.Project.Metadata.TryGetValue("PDF_StoredFiles", out var raw) || string.IsNullOrWhiteSpace(raw))
            return null;

        var candidates = new List<string>();
        foreach (var stored in ParseStoredPathList(raw))
        {
            var resolved = TryResolveStoredPath(projectDir, stored);
            if (string.IsNullOrWhiteSpace(resolved))
                continue;
            if (!string.Equals(Path.GetExtension(resolved), ".pdf", StringComparison.OrdinalIgnoreCase))
                continue;
            candidates.Add(resolved);
        }

        return PickBestPdfCandidate(candidates, holdingTokens);
    }

    private static string? TryFindHoldingDirectory(string rootDir, IReadOnlyList<string> holdingTokens)
    {
        if (holdingTokens.Count == 0)
            return null;

        foreach (var token in holdingTokens)
        {
            var direct = Path.Combine(rootDir, token);
            if (Directory.Exists(direct))
                return direct;
        }

        foreach (var sub in SafeEnumerateDirectories(rootDir))
        {
            if (string.Equals(Path.GetFileName(sub), "__UNMATCHED", StringComparison.OrdinalIgnoreCase))
                continue;

            foreach (var token in holdingTokens)
            {
                var candidate = Path.Combine(sub, token);
                if (Directory.Exists(candidate))
                    return candidate;
            }
        }

        return null;
    }

    private static string? TryFindPdfInDirectory(string directory, IReadOnlyList<string> holdingTokens, SearchOption searchOption)
    {
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
            return null;

        var files = SafeEnumerateFiles(directory, "*.pdf", searchOption);
        return PickBestPdfCandidate(files, holdingTokens);
    }

    private static string? PickBestPdfCandidate(IEnumerable<string> candidates, IReadOnlyList<string> holdingTokens)
    {
        var list = candidates
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (list.Count == 0)
            return null;

        foreach (var token in holdingTokens)
        {
            var expectedSuffix = "_" + token + ".pdf";
            var exact = list
                .Where(path => Path.GetFileName(path).EndsWith(expectedSuffix, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (exact.Count > 0)
                return exact[0];
        }

        return list
            .OrderByDescending(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase)
            .First();
    }

    private static IReadOnlyList<string> SafeEnumerateFiles(string directory, string pattern, SearchOption searchOption)
    {
        try
        {
            return Directory.EnumerateFiles(directory, pattern, searchOption).ToList();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    private static IReadOnlyList<string> SafeEnumerateDirectories(string directory)
    {
        try
        {
            return Directory.EnumerateDirectories(directory).ToList();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    private static IReadOnlyList<string> BuildHoldingTokens(HaltungRecord record)
    {
        var holdingRaw = (record.GetFieldValue("Haltungsname") ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(holdingRaw))
            return Array.Empty<string>();

        var sanitized = SanitizePathSegment(holdingRaw);
        return new[] { sanitized, holdingRaw }
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string SanitizePathSegment(string value)
        => AuswertungPro.Next.Application.Common.ProjectPathResolver.SanitizePathSegment(value);

    private static IReadOnlyList<string> ParseStoredPathList(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return Array.Empty<string>();

        try
        {
            var parsed = JsonSerializer.Deserialize<List<string>>(raw);
            if (parsed is null)
                return Array.Empty<string>();

            return parsed
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .ToList();
        }
        catch
        {
            return raw.Split(';', StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())
                .Where(x => x.Length > 0)
                .ToList();
        }
    }

    private static string? TryResolveStoredPath(string projectDir, string rawPath)
    {
        var path = (rawPath ?? string.Empty).Trim();
        if (path.Length == 0)
            return null;

        try
        {
            string full;
            if (Path.IsPathRooted(path))
            {
                // Absolut: User hat explizit einen externen Pfad gewählt (z.B.
                // Video auf D:\Videoprojekte). Akzeptieren wie zuvor — Path-
                // Traversal ist hier nicht moeglich da kein Combine.
                full = path;
            }
            else
            {
                // Relativ: SEC-C1-Hardening (Audit 2026-04-23). Path.Combine +
                // Path.GetFullPath kürzt `..` zwar mathematisch, lässt aber zu
                // dass der resolved Pfad ausserhalb projectDir liegt
                // (`..\..\..\windows\...`). Daher Containment-Check gegen den
                // projectDir-Root erzwingen.
                var combined = Path.GetFullPath(Path.Combine(projectDir, path));
                var rootWithSep = projectDir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                                  + Path.DirectorySeparatorChar;
                if (!combined.StartsWith(rootWithSep, StringComparison.OrdinalIgnoreCase))
                    return null;
                full = combined;
            }
            return File.Exists(full) ? full : null;
        }
        catch
        {
            return null;
        }
    }

    private static string? TryWriteVideoStartErrorLog(Exception ex, string path)
    {
        try
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var logsDir = Path.Combine(baseDir, "logs");
            Directory.CreateDirectory(logsDir);

            var safeName = Path.GetFileNameWithoutExtension(path);
            foreach (var c in Path.GetInvalidFileNameChars())
                safeName = safeName.Replace(c, '_');
            if (string.IsNullOrWhiteSpace(safeName))
                safeName = "video";

            var file = $"video_start_error_{DateTime.Now:yyyyMMdd_HHmmss}_{safeName}.txt";
            var logPath = Path.Combine(logsDir, file);

            var content =
                $"Time: {DateTime.Now:O}{Environment.NewLine}" +
                $"VideoPath: {path}{Environment.NewLine}" +
                $"Exception:{Environment.NewLine}{ex}{Environment.NewLine}";
            File.WriteAllText(logPath, content);
            return logPath;
        }
        catch
        {
            return null;
        }
    }

    private void RelinkVideo(HaltungRecord? record)
    {
        if (record is null)
            return;

        var initial = !string.IsNullOrWhiteSpace(App.Resolve<AppSettings>().LastVideoSourceFolder)
            ? App.Resolve<AppSettings>().LastVideoSourceFolder
            : !string.IsNullOrWhiteSpace(App.Resolve<AppSettings>().LastVideoFolder)
                ? App.Resolve<AppSettings>().LastVideoFolder
            : App.Resolve<AppSettings>().LastProjectPath is null
                ? null
                : Path.GetDirectoryName(App.Resolve<AppSettings>().LastProjectPath);

        var path = App.Resolve<IDialogService>().OpenFile(
            "Video auswaehlen",
            MediaFileTypes.VideoDialogFilter,
            initial);
        if (string.IsNullOrWhiteSpace(path))
            return;

        var selectedDir = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(selectedDir))
        {
            App.Resolve<AppSettings>().LastVideoSourceFolder = selectedDir;
            App.Resolve<AppSettings>().LastVideoFolder = selectedDir; // legacy compatibility
            App.Resolve<AppSettings>().Save();
        }

        SaveVideoLink(record, path, userEdited: true);
    }

    private string? EnsureVideoPath(HaltungRecord record)
    {
        var resolved = ResolveExistingPath(record.GetFieldValue("Link"));
        if (!string.IsNullOrWhiteSpace(resolved))
        {
            if (!string.Equals(resolved, record.GetFieldValue("Link")?.Trim(), StringComparison.OrdinalIgnoreCase))
                SaveVideoLink(record, resolved, userEdited: false);
            return resolved;
        }

        var initial = !string.IsNullOrWhiteSpace(App.Resolve<AppSettings>().LastVideoSourceFolder)
            ? App.Resolve<AppSettings>().LastVideoSourceFolder
            : !string.IsNullOrWhiteSpace(App.Resolve<AppSettings>().LastVideoFolder)
                ? App.Resolve<AppSettings>().LastVideoFolder
            : App.Resolve<AppSettings>().LastProjectPath is null
                ? null
                : Path.GetDirectoryName(App.Resolve<AppSettings>().LastProjectPath);

        if (!string.IsNullOrWhiteSpace(initial) && Directory.Exists(initial))
        {
            var tool = new VideoSearchTool(initial);
            var res = tool.ResolveForRecord(record);
            if (res.Success && !string.IsNullOrWhiteSpace(res.VideoPath))
                return SaveVideoLink(record, res.VideoPath!, userEdited: false);
        }

        var folder = App.Resolve<IDialogService>().SelectFolder("Video-Ordner auswaehlen", initial);
        if (string.IsNullOrWhiteSpace(folder))
            return null;

        App.Resolve<AppSettings>().LastVideoSourceFolder = folder;
        App.Resolve<AppSettings>().LastVideoFolder = folder; // legacy compatibility
        App.Resolve<AppSettings>().Save();

        var toolManual = new VideoSearchTool(folder);
        var resManual = toolManual.ResolveForRecord(record);
        if (resManual.Success && !string.IsNullOrWhiteSpace(resManual.VideoPath))
            return SaveVideoLink(record, resManual.VideoPath!, userEdited: false);

        _dialogs.ShowMessage(resManual.Message, "Video", MessageBoxButton.OK, MessageBoxImage.Information);

        var manual = App.Resolve<IDialogService>().OpenFile(
            "Video auswaehlen",
            MediaFileTypes.VideoDialogFilter,
            folder);
        if (string.IsNullOrWhiteSpace(manual))
            return null;

        return SaveVideoLink(record, manual, userEdited: true);
    }

    private string SaveVideoLink(HaltungRecord record, string path, bool userEdited)
    {
        record.SetFieldValue("Link", path, FieldSource.Unknown, userEdited: userEdited);
        _shell.Project.ModifiedAtUtc = DateTime.UtcNow;
        _shell.Project.Dirty = true;
        return path;
    }

    public void OpenMediaSearchWindow()
    {
        if (Records.Count == 0)
        {
            _shell.SetStatus("Keine Haltungen vorhanden.");
            return;
        }

        var initial = !string.IsNullOrWhiteSpace(App.Resolve<AppSettings>().LastVideoSourceFolder)
            ? App.Resolve<AppSettings>().LastVideoSourceFolder
            : !string.IsNullOrWhiteSpace(App.Resolve<AppSettings>().LastVideoFolder)
                ? App.Resolve<AppSettings>().LastVideoFolder
                : null;

        var win = new MediaSearchWindow(Records.ToList(), initial);
        win.Owner = System.Windows.Application.Current?.MainWindow;

        if (App.Resolve<IDialogService>().ShowDialog(win) == true && win.Applied)
        {
            _shell.Project.ModifiedAtUtc = DateTime.UtcNow;
            _shell.Project.Dirty = true;
            OnPropertyChanged(nameof(Records));
            _shell.SetStatus($"Medien verlinkt: {win.AppliedVideoCount} Videos, {win.AppliedPdfCount} PDFs, {win.AppliedFotoCount} Fotos");
        }
    }

    private void OpenHydraulikPanel(HaltungRecord? record)
    {
        var vm = new HydraulikPanelViewModel();

        if (record is not null)
        {
            var dn = TryParseDnMm(record.GetFieldValue("DN_mm"));
            var material = record.GetFieldValue("Rohrmaterial");
            vm.LoadFromRecord(dn, material, null);
        }

        var win = new HydraulikPanelWindow(vm);
        win.Owner = System.Windows.Application.Current?.MainWindow;
        App.Resolve<IDialogService>().ShowDialog(win);
    }
}
