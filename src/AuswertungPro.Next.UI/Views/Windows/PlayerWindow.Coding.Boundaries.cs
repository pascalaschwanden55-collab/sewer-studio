using System;
using System.Linq;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Infrastructure.Ai;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Helpers;
using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    /// <summary>
    /// Stellt sicher, dass BCD (Rohranfang) als erster Eintrag existiert.
    /// Meter und Timestamp werden automatisch aus OSD / Video entnommen.
    /// </summary>
    private void EnsureRohranfangExists(double currentMeter, TimeSpan currentVideoTime, byte[]? analyzedFrameBytes, ref bool anyAdded)
    {
        if (_codingVm == null || _codingSessionService == null) return;
        // BCD bereits vorhanden? Alle moeglichen Quellen pruefen
        var vmBcd = _codingVm.Events.Count(e => string.Equals(e.Entry.Code, "BCD", StringComparison.OrdinalIgnoreCase));
        var sessBcd = _codingSessionService.ActiveSession?.Events.Count(e =>
            string.Equals(e.Entry.Code, "BCD", StringComparison.OrdinalIgnoreCase)) ?? 0;
        if (vmBcd > 0 || sessBcd > 0)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[BCD-Dedup] EnsureRohranfang: bereits vorhanden (VM={vmBcd}, Session={sessBcd})");
            return;
        }
        System.Diagnostics.Debug.WriteLine(
            $"[BCD-Dedup] EnsureRohranfang: NEU erzeugen bei {currentMeter:F2}m (VM={vmBcd}, Session={sessBcd})");

        // Rohranfang: OSD-Meter vom Import uebernehmen, sonst 0.00m
        // Videozeit: aus dem Import oder Anfang des Videos
        double rohranfangMeter = 0.0;
        var rohranfangTime = TimeSpan.Zero;

        // Aus Import-Referenz den BCD-Eintrag holen (falls vorhanden)
        var importBcd = _codingImportEvents.FirstOrDefault(e =>
            string.Equals(e.Entry.Code, "BCD", StringComparison.OrdinalIgnoreCase));
        if (importBcd != null)
        {
            rohranfangMeter = importBcd.MeterAtCapture;
            rohranfangTime = importBcd.VideoTimestamp;
        }

        var label = VsaCodeResolver.LookupLabel("BCD") ?? "Rohranfang";
        var draft = CodingBoundaryEventFactory.CreateStart(label, rohranfangMeter, rohranfangTime);
        // Rohranfang-Foto: NICHT den Videoanfang nehmen (dort laeuft die Dateneinblendung).
        // Bevorzugt den ersten sauberen Frame NACH der Einblendung (FrameReadiness -> Ready)
        // gezielt per ffmpeg greifen; sonst Fallback auf den uebergebenen analysierten Frame.
        analyzedFrameBytes = TryExtractFrameAtSeconds(_codingFrameReadiness.FirstCleanFrameSeconds) ?? analyzedFrameBytes;
        AttachBoundaryAnalyzedFramePhoto(draft.Entry, analyzedFrameBytes);

        var ev = _codingSessionService.AddEvent(draft.Entry);
        ev.MeterAtCapture = rohranfangMeter;
        ev.VideoTimestamp = rohranfangTime;
        ev.AiContext = draft.AiContext;
        // Event-Hook (OnSessionEventAdded) fuegt automatisch in _codingVm.Events ein.
        // KEIN explizites _codingVm.Events.Add() - sonst doppelt!
        anyAdded = true;

        // Auto-Kalibrierung bei Rohranfang versuchen (wenn noch nicht kalibriert)
        TryAutoCalibrationFromCurrentFrame().SafeFireAndForget("TryAutoCalibration");
    }

    /// <summary>
    /// Fuegt BCE (Rohrende) als letzten Eintrag ein.
    /// Meter und Timestamp werden automatisch aus OSD / Video entnommen.
    /// Aufgerufen beim Beenden der Codier-Session oder am Videoende.
    /// </summary>
    private void EnsureRohrendeExists(double meterEnd, TimeSpan videoTime, byte[]? analyzedFrameBytes = null)
    {
        if (_codingVm == null || _codingSessionService == null) return;
        // BCE bereits vorhanden?
        if (_codingVm.Events.Any(e => string.Equals(e.Entry.Code, "BCE", StringComparison.OrdinalIgnoreCase)))
            return;
        // Streckenschaeden werden bereits in ExitCodingMode geschlossen (vor diesem Aufruf)

        var rohrEndTime = _player != null
            ? TimeSpan.FromMilliseconds(_player.Time)
            : videoTime;

        // Aus Import-Referenz den BCE-Eintrag holen (falls vorhanden) = verlaessliches Rohrende.
        var importBce = _codingImportEvents.FirstOrDefault(e =>
            string.Equals(e.Entry.Code, "BCE", StringComparison.OrdinalIgnoreCase));

        // Rohrende-Meter absichern: ein kaputter OSD-Meter (z.B. 114 m bei 15.82 m Haltung) wird
        // auf das verlaessliche Ende (Import-BCE / EndMeter) korrigiert statt blind uebernommen.
        double rohrEndMeter = CodingDedupPolicy.ResolvePlausibleEndMeter(
            osdMeter: _codingLastOsdMeter ?? meterEnd,
            importEndMeter: importBce?.MeterAtCapture,
            vmEndMeter: _codingVm.EndMeter);
        if (importBce != null
            && Math.Abs(importBce.MeterAtCapture - rohrEndMeter) < 0.01)
        {
            // Ende stammt aus dem Import -> dessen Videozeit uebernehmen.
            rohrEndTime = importBce.VideoTimestamp;
        }

        var label = VsaCodeResolver.LookupLabel("BCE") ?? "Rohrende";
        var draft = CodingBoundaryEventFactory.CreateEnd(label, rohrEndMeter, rohrEndTime);
        AttachBoundaryAnalyzedFramePhoto(draft.Entry, analyzedFrameBytes);

        var ev = _codingSessionService.AddEvent(draft.Entry);
        ev.MeterAtCapture = rohrEndMeter;
        ev.VideoTimestamp = rohrEndTime;
        ev.AiContext = draft.AiContext;
        RefreshCodingEventsList();
    }

    /// <summary>
    /// Prueft ob offene Streckenschaeden existieren (IsStreckenschaden=true, MeterEnd=null).
    /// Zeigt Dialog mit Liste und bietet an, sie am aktuellen Meter zu schliessen.
    /// Rueckgabe: true = weiter (geschlossen oder ignoriert), false = abgebrochen (User will weiter codieren).
    /// </summary>
    private bool CloseOpenStreckenschaeden(double currentMeter)
    {
        if (_codingVm == null) return true;

        var offene = _codingVm.Events
            .Where(e => e.Entry.IsStreckenschaden && !e.Entry.MeterEnd.HasValue)
            .ToList();

        if (offene.Count == 0) return true;

        // Hinweis-Dialog mit Liste der offenen Streckenschaeden
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Folgende Streckenschäden sind noch offen (kein MeterEnde):");
        sb.AppendLine();
        foreach (var ev in offene)
        {
            sb.AppendLine($"  \u2022 {ev.Entry.Code} \u2013 {ev.Entry.Beschreibung}");
            sb.AppendLine($"    Start: {ev.MeterAtCapture:F2}m");
        }
        sb.AppendLine();
        sb.AppendLine($"Sollen alle offenen Streckenschäden bei {currentMeter:F2}m geschlossen werden?");

        SuspendCodingOverlayInput();
        DialogConfirm result;
        try
        {
            result = DialogHost.Current.ConfirmCancel(
                sb.ToString(),
                "Offene Streckenschäden");
        }
        finally
        {
            ResumeCodingOverlayInput();
        }

        if (result == DialogConfirm.Yes)
        {
            // Alle offenen Streckenschaeden schliessen.
            // MeterEnd = letzte Sichtung (MeterAtCapture) oder aktueller Meter
            foreach (var ev in offene)
            {
                var start = ev.Entry.MeterStart ?? 0;
                ev.Entry.MeterEnd = ev.MeterAtCapture > start
                    ? ev.MeterAtCapture
                    : currentMeter;
                _codingSessionService?.UpdateEvent(ev.EventId, ev.Entry, ev.Overlay);
            }
            RefreshCodingEventsList();
            return true;
        }

        if (result == DialogConfirm.Cancel)
            return false; // User will weiter codieren - Exit abbrechen

        return true; // "Nein" -> weiter ohne Schliessen
    }
}
