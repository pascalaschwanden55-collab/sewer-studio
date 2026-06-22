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
        var bcdPresence = CodingBoundaryPresencePolicy.CountExisting(
            _codingVm.Events,
            _codingSessionService.ActiveSession?.Events,
            "BCD");
        if (bcdPresence.Exists)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[BCD-Dedup] EnsureRohranfang: bereits vorhanden (VM={bcdPresence.ViewCount}, Session={bcdPresence.SessionCount})");
            return;
        }
        System.Diagnostics.Debug.WriteLine(
            $"[BCD-Dedup] EnsureRohranfang: NEU erzeugen bei {currentMeter:F2}m (VM={bcdPresence.ViewCount}, Session={bcdPresence.SessionCount})");

        var startReference = CodingBoundaryImportReferencePolicy.ResolveStart(_codingImportEvents);

        var label = VsaCodeResolver.LookupLabel("BCD") ?? "Rohranfang";
        var draft = CodingBoundaryEventFactory.CreateStart(label, startReference.Meter, startReference.VideoTime);
        // Rohranfang-Foto: NICHT den Videoanfang nehmen (dort laeuft die Dateneinblendung).
        // Bevorzugt den ersten sauberen Frame NACH der Einblendung (FrameReadiness -> Ready)
        // gezielt per ffmpeg greifen; sonst Fallback auf den uebergebenen analysierten Frame.
        analyzedFrameBytes = TryExtractFrameAtSeconds(_codingFrameReadiness.FirstCleanFrameSeconds) ?? analyzedFrameBytes;
        AttachBoundaryAnalyzedFramePhoto(draft.Entry, analyzedFrameBytes);

        var ev = _codingSessionService.AddEvent(draft.Entry);
        ev.MeterAtCapture = startReference.Meter;
        ev.VideoTimestamp = startReference.VideoTime;
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
        if (CodingBoundaryPresencePolicy.ExistsInView(_codingVm.Events, "BCE"))
            return;
        // Streckenschaeden werden bereits in ExitCodingMode geschlossen (vor diesem Aufruf)

        var fallbackEndTime = _player != null
            ? TimeSpan.FromMilliseconds(_player.Time)
            : videoTime;

        var endReference = CodingBoundaryImportReferencePolicy.ResolveEnd(
            _codingImportEvents,
            _codingLastOsdMeter,
            meterEnd,
            _codingVm.EndMeter,
            fallbackEndTime);

        var label = VsaCodeResolver.LookupLabel("BCE") ?? "Rohrende";
        var draft = CodingBoundaryEventFactory.CreateEnd(label, endReference.Meter, endReference.VideoTime);
        AttachBoundaryAnalyzedFramePhoto(draft.Entry, analyzedFrameBytes);

        var ev = _codingSessionService.AddEvent(draft.Entry);
        ev.MeterAtCapture = endReference.Meter;
        ev.VideoTimestamp = endReference.VideoTime;
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

        var offene = CodingOpenStretchDamagePolicy.FindOpen(_codingVm.Events);

        if (offene.Count == 0) return true;

        var prompt = CodingOpenStretchDamagePromptBuilder.Build(offene, currentMeter);
        SuspendCodingOverlayInput();
        DialogConfirm result;
        try
        {
            result = DialogHost.Current.ConfirmCancel(
                prompt,
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
                ev.Entry.MeterEnd = CodingOpenStretchDamagePolicy.ResolveCloseMeter(ev, currentMeter);
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
