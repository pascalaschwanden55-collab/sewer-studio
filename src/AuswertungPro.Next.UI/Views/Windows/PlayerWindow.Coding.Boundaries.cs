using System;
using AuswertungPro.Next.Infrastructure.Ai;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Helpers;
using AuswertungPro.Next.UI.Player;

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
            PlayerTrace.WriteLine(
                $"[BCD-Dedup] EnsureRohranfang: bereits vorhanden (VM={bcdPresence.ViewCount}, Session={bcdPresence.SessionCount})");
            return;
        }
        PlayerTrace.WriteLine(
            $"[BCD-Dedup] EnsureRohranfang: NEU erzeugen bei {currentMeter:F2}m (VM={bcdPresence.ViewCount}, Session={bcdPresence.SessionCount})");

        var startReference = CodingBoundaryImportReferencePolicy.ResolveStart(_codingImportEvents);

        var label = VsaCodeResolver.LookupLabel("BCD") ?? "Rohranfang";
        var draft = CodingBoundaryEventFactory.CreateStart(label, startReference.Meter, startReference.VideoTime);
        // Rohranfang-Foto: NICHT den Videoanfang nehmen (dort laeuft die Dateneinblendung).
        // Bevorzugt den ersten sauberen Frame NACH der Einblendung (FrameReadiness -> Ready)
        // gezielt per ffmpeg greifen; sonst Fallback auf den uebergebenen analysierten Frame.
        analyzedFrameBytes = TryExtractFrameAtSeconds(_codingFrameReadiness.FirstCleanFrameSeconds) ?? analyzedFrameBytes;
        AttachBoundaryAnalyzedFramePhoto(draft.Entry, analyzedFrameBytes);

        CodingBoundaryEventAppender.Apply(draft, startReference.Meter, startReference.VideoTime, _codingSessionService);
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

        CodingBoundaryEventAppender.Apply(draft, endReference.Meter, endReference.VideoTime, _codingSessionService);
        RefreshCodingEventsList();
    }
}
