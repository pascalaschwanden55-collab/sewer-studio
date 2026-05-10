using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LibVLCSharp.Shared;

namespace AuswertungPro.Next.UI.Views.Windows;

// CodingModeWindow Frame-Capture-Helfer (Slice 8a.2.3 + Robustifizierung
// Slice 8a.6.A 2026-05-10): VLC-Snapshot (Source-Frame) mit klar
// definiertem Fallback, State-Pruefung und Validierung.
//
// Slice 8a.6.A-Kernpunkte:
//   1. State-Pruefung: TakeSnapshot nur wenn Player Playing/Paused ist.
//   2. Klare Fallback-Bedingung: WPF-Render nur wenn TakeSnapshot keine
//      Datei liefert — NICHT wenn TakeSnapshot ein leeres/schwarzes Bild
//      liefert (Fallback haette gleiches Problem).
//   3. IsFrameValid (statisch testbar): Mindest-Aufloesung +
//      Helligkeits-Spread-Check. Schwarze Frames werden als "nicht
//      verfuegbar" gemeldet statt an SAM/Qwen weitergereicht.
//   4. SetStatusSafe-Hint statt stilles null.
public partial class CodingModeWindow
{
    /// <summary>Aktuellen Videoframe als PNG extrahieren. Gibt null zurueck
    /// wenn Capture nicht moeglich oder Frame ungueltig — User wird ueber
    /// Status-Leiste informiert.</summary>
    private async Task<byte[]?> CaptureCurrentFrameAsync()
    {
        // 1. State-Pruefung — Player muss bereit sein.
        if (_player == null || !_videoReady)
        {
            SetStatusSafe("Frame-Capture nicht moeglich: Video noch nicht bereit");
            return null;
        }

        var state = TryGetPlayerStateSafe();
        if (state != VLCState.Playing && state != VLCState.Paused)
        {
            SetStatusSafe($"Frame-Capture nicht moeglich (Player-Status: {state})");
            return null;
        }

        // 2. Primaer: VLC TakeSnapshot (echter Source-Frame).
        var snapshot = await TryTakeSnapshotAsync();
        if (snapshot != null)
        {
            if (IsFrameValid(snapshot))
            {
                TryCacheFrameDimensions(snapshot);
                return snapshot;
            }
            // TakeSnapshot lieferte ein Bild, aber zu uniform — z.B.
            // schwarzer Frame waehrend Codec-Wechsel. Fallback bringt
            // nichts (RenderTargetBitmap auf HwndHost liefert oft
            // genauso schwarz). Direkt Fehler melden.
            SetStatusSafe("Frame-Capture: Bild zu uniform (schwarz/weiss) — bitte Video laufen lassen");
            return null;
        }

        // 3. Fallback nur wenn TakeSnapshot komplett scheiterte (keine
        //    Datei). RenderTargetBitmap auf VideoView/HwndHost liefert oft
        //    schwarz, aber wenigstens fuer den Container mit OverlayCanvas
        //    funktioniert es. Bei VLC-HwndHost-Inhalt ist Erfolg ungewiss
        //    — deshalb auch hier Validierung.
        try
        {
            var fallback = await Dispatcher.InvokeAsync(() => RenderVideoViewToPng());
            if (fallback != null && IsFrameValid(fallback))
            {
                TryCacheFrameDimensions(fallback);
                return fallback;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[CodingMode Capture] WPF-Render-Fallback fehlgeschlagen: {ex.Message}");
        }

        SetStatusSafe("Frame-Capture fehlgeschlagen — kein gueltiger Frame verfuegbar");
        return null;
    }

    /// <summary>VLC TakeSnapshot mit Wartelogik. Returnt PNG-Bytes wenn
    /// Datei innerhalb von ~1.5s erscheint und ueber 100 Bytes hat, sonst
    /// null.</summary>
    private async Task<byte[]?> TryTakeSnapshotAsync()
    {
        var tmpDir = Path.GetTempPath();
        var snapFile = Path.Combine(tmpDir, $"sewerstudio_snap_{Guid.NewGuid():N}.png");
        try
        {
            _player!.TakeSnapshot(0, snapFile, 0, 0);
            for (int i = 0; i < 30; i++)
            {
                await Task.Delay(50);
                if (File.Exists(snapFile) && new FileInfo(snapFile).Length > 100)
                    break;
            }
            if (File.Exists(snapFile) && new FileInfo(snapFile).Length > 100)
            {
                return await File.ReadAllBytesAsync(snapFile);
            }
            return null;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[CodingMode Capture] VLC-Snapshot Fehler: {ex.Message}");
            return null;
        }
        finally
        {
            try { if (File.Exists(snapFile)) File.Delete(snapFile); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[CodingMode Capture] tmp-cleanup: {ex.Message}"); }
        }
    }

    private VLCState TryGetPlayerStateSafe()
    {
        try { return _player?.State ?? VLCState.NothingSpecial; }
        catch { return VLCState.NothingSpecial; }
    }

    /// <summary>Pruefen ob ein PNG-Bytes-Array einen brauchbaren Frame
    /// enthaelt. Statisch, testbar — keine Window-State-Abhaengigkeit.
    /// Heuristik:
    ///   - Mindest-Bytes (anti-truncated PNG)
    ///   - Mindest-Aufloesung 32x32
    ///   - Helligkeits-Spread auf Mittellinie >= 20 (von 255)
    ///     -> verhindert komplett-schwarze/-weisse/-uniforme Frames.</summary>
    internal static bool IsFrameValid(byte[]? pngBytes)
    {
        if (pngBytes == null || pngBytes.Length < 200) return false;

        try
        {
            using var ms = new MemoryStream(pngBytes);
            var decoder = BitmapDecoder.Create(
                ms,
                BitmapCreateOptions.PreservePixelFormat,
                BitmapCacheOption.OnLoad);
            if (decoder.Frames.Count == 0) return false;

            var frame = decoder.Frames[0];
            int w = frame.PixelWidth;
            int h = frame.PixelHeight;
            if (w < 32 || h < 32) return false;

            int bytesPerPixel = (frame.Format.BitsPerPixel + 7) / 8;
            if (bytesPerPixel < 1) return false;

            int stride = (w * frame.Format.BitsPerPixel + 7) / 8;
            var pixels = new byte[stride * h];
            frame.CopyPixels(pixels, stride, 0);

            // Sampling: 100 Pixel auf der Mittellinie quer durchs Bild.
            const int sampleCount = 100;
            int midY = h / 2;
            byte minVal = 255, maxVal = 0;
            for (int i = 0; i < sampleCount; i++)
            {
                int x = (i * (w - 1)) / (sampleCount - 1);
                int idx = midY * stride + x * bytesPerPixel;
                if (idx + 2 >= pixels.Length) continue;

                byte c1 = pixels[idx];
                byte c2 = bytesPerPixel >= 2 ? pixels[idx + 1] : c1;
                byte c3 = bytesPerPixel >= 3 ? pixels[idx + 2] : c1;
                byte luma = (byte)((c1 + c2 + c3) / 3);

                if (luma < minVal) minVal = luma;
                if (luma > maxVal) maxVal = luma;
            }

            return (maxVal - minVal) >= 20;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Frame-Dimensionen aus PNG-Bytes lesen und cachen. Best-effort:
    /// Failure ist still, weil Capture-Pfad robust bleiben soll.</summary>
    private void TryCacheFrameDimensions(byte[] pngBytes)
    {
        if (pngBytes == null || pngBytes.Length < 100) return;
        if (_videoFrameWidthCache > 0 && _videoFrameHeightCache > 0) return;

        try
        {
            using var ms = new MemoryStream(pngBytes);
            var decoder = BitmapDecoder.Create(
                ms,
                BitmapCreateOptions.PreservePixelFormat,
                BitmapCacheOption.OnLoad);
            if (decoder.Frames.Count > 0)
            {
                _videoFrameWidthCache = decoder.Frames[0].PixelWidth;
                _videoFrameHeightCache = decoder.Frames[0].PixelHeight;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[CodingMode Capture] TryCacheFrameDimensions: {ex.Message}");
        }
    }

    /// <summary>
    /// Rendert das aktuelle VideoView (oder den umgebenden Container) als PNG-Byte-Array.
    /// Nutzt RenderTargetBitmap - liefert das, was WPF anzeigt (inkl. Letterbox).
    /// Bei VLC-HwndHost liefert das oft schwarz, dann ist der OverlayCanvas-Container
    /// der bessere Capture-Punkt.
    /// </summary>
    private byte[]? RenderVideoViewToPng()
    {
        FrameworkElement? target = null;
        try
        {
            target = OverlayCanvas?.Parent as FrameworkElement;
            if (target == null) target = OverlayCanvas;
            if (target == null) return null;

            int w = (int)Math.Max(target.ActualWidth, 1);
            int h = (int)Math.Max(target.ActualHeight, 1);
            var rtb = new RenderTargetBitmap(
                w, h, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(target);

            var enc = new PngBitmapEncoder();
            enc.Frames.Add(BitmapFrame.Create(rtb));
            using var ms = new MemoryStream();
            enc.Save(ms);
            return ms.ToArray();
        }
        catch
        {
            return null;
        }
    }
}
