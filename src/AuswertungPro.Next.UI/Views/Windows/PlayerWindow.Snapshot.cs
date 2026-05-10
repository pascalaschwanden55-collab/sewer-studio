using System;
using System.IO;
using System.Threading.Tasks;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.UI.Views.Windows;

// Phase 6.1.A: Snapshot/Frame-Capture-Methoden aus PlayerWindow.xaml.cs.
// Cluster B5 (Snapshot/Frame-Export) — ~5% des PlayerWindow.
//
// Felder/Members aus dem Hauptpartial: _lastOpened (static), _player, _videoPath.
// Keine eigenen Felder, kein Lifecycle — reine Methoden-Auslagerung.
public partial class PlayerWindow
{
    /// <summary>
    /// Erstellt einen Snapshot vom aktuellen Video-Frame als PNG.
    /// Funktioniert mit jeder Aufloesung (auch FullHD 1920x1080).
    /// </summary>
    public static bool TryTakeSnapshot(out string snapshotPath)
    {
        snapshotPath = string.Empty;
        if (_lastOpened?._player is null || !_lastOpened._player.IsPlaying && _lastOpened._player.Time <= 0)
            return false;

        try
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "SewerStudio_Snapshots");
            Directory.CreateDirectory(tempDir);
            snapshotPath = Path.Combine(tempDir, $"snap_{DateTime.Now:yyyyMMdd_HHmmss}.png");

            // VLC Snapshot: 0 = original Aufloesung (FullHD etc.)
            return _lastOpened.TakeSnapshotSafe(snapshotPath);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// TakeSnapshot mit kurzem Pause-Trick, um D3D11-Deadlock zu vermeiden.
    /// D3D11 haelt die Video-Surface exklusiv gesperrt; kurzes Pausieren gibt sie frei.
    /// </summary>
    private bool TakeSnapshotSafe(string filePath, uint width = 0, uint height = 0)
    {
        var wasPlaying = _player.IsPlaying;
        if (wasPlaying)
        {
            _player.SetPause(true);
            System.Threading.Thread.Sleep(60);
        }
        // VLC-OSD-Anzeige (Dateipfad) vorher deaktivieren, damit der Pfad
        // nicht als Text auf dem Videobild erscheint
        try { _player.SetMarqueeInt(LibVLCSharp.Shared.VideoMarqueeOption.Enable, 0); } catch (Exception _bestEffortEx) { System.Diagnostics.Debug.WriteLine($"[best-effort] {_bestEffortEx.Message}"); }
        var success = _player.TakeSnapshot(0, filePath, width, height);
        if (wasPlaying)
            _player.SetPause(false);
        return success;
    }

    /// <summary>
    /// Erstellt einen Snapshot vom aktuellen Video-Frame und speichert ihn im Projektordner.
    /// </summary>
    private string? CodingCaptureSnapshot(ProtocolEntry entry)
    {
        try
        {
            // Zielverzeichnis: neben dem Video oder im Temp
            var videoDir = !string.IsNullOrEmpty(_videoPath)
                ? Path.GetDirectoryName(_videoPath) ?? Path.GetTempPath()
                : Path.GetTempPath();
            var fotoDir = Path.Combine(videoDir, "Fotos");
            Directory.CreateDirectory(fotoDir);

            var ts = entry.Zeit.HasValue
                ? entry.Zeit.Value.ToString(@"hh\-mm\-ss\-fff")
                : DateTimeOffset.Now.ToString("HHmmss");
            var fileName = $"{entry.Code}_{entry.MeterStart:F2}m_{ts}.png";
            var filePath = Path.Combine(fotoDir, fileName);

            TakeSnapshotSafe(filePath);

            // VLC schreibt asynchron - kurz warten
            for (int i = 0; i < 20; i++)
            {
                System.Threading.Thread.Sleep(50);
                if (File.Exists(filePath) && new FileInfo(filePath).Length > 100)
                    return filePath;
            }

            return File.Exists(filePath) ? filePath : null;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Snapshot-Fehler: {ex.Message}");
            return null;
        }
    }

    /// <summary>VLC-Snapshot als PNG-Bytes extrahieren.</summary>
    private async Task<byte[]?> CaptureSnapshotAsync()
    {
        var tmpDir = Path.GetTempPath();
        var snapFile = Path.Combine(tmpDir, $"sewerstudio_snap_{Guid.NewGuid():N}.png");
        try
        {
            TakeSnapshotSafe(snapFile);
            for (int i = 0; i < 20; i++)
            {
                await Task.Delay(50);
                if (File.Exists(snapFile) && new FileInfo(snapFile).Length > 100)
                    break;
            }
            if (File.Exists(snapFile))
                return await File.ReadAllBytesAsync(snapFile);
            return null;
        }
        finally
        {
            try { if (File.Exists(snapFile)) File.Delete(snapFile); } catch (Exception _bestEffortEx) { System.Diagnostics.Debug.WriteLine($"[best-effort] {_bestEffortEx.Message}"); }
        }
    }
}
