using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace AuswertungPro.Next.UI.Views.Windows;

// CodingModeWindow Frame-Capture-Helfer (Slice 8a.2.3): VLC-Snapshot
// (Source-Frame) mit WPF-RenderTargetBitmap-Fallback (visuelle Ebene
// inkl. Overlays). Selbstaendige PNG-Bytestream-Erzeugung — keine
// Geschaeftslogik, kein Session-State. Aus dem Hauptdatei extrahiert.
public partial class CodingModeWindow
{
    /// <summary>
    /// Aktuellen Videoframe als PNG extrahieren (ueber VLC Snapshot).
    /// </summary>
    private async Task<byte[]?> CaptureCurrentFrameAsync()
    {
        // 1. Versuch: VLC TakeSnapshot (echter Source-Frame, beste Qualitaet).
        //    Funktioniert nur bei bereitem Player; scheitert oft still bei laufendem Video
        //    auf manchen VLC-Versionen, deshalb robuster Fallback weiter unten.
        if (_player != null && _videoReady)
        {
            var tmpDir = Path.GetTempPath();
            var snapFile = Path.Combine(tmpDir, $"sewerstudio_snap_{Guid.NewGuid():N}.png");
            try
            {
                _player.TakeSnapshot(0, snapFile, 0, 0);
                // Warten bis Datei geschrieben (max ~1.5s)
                for (int i = 0; i < 30; i++)
                {
                    await Task.Delay(50);
                    if (File.Exists(snapFile) &&
                        new FileInfo(snapFile).Length > 100)
                        break;
                }
                if (File.Exists(snapFile)
                    && new FileInfo(snapFile).Length > 100)
                {
                    var bytes = await File.ReadAllBytesAsync(snapFile);
                    try { File.Delete(snapFile); } catch (Exception _bestEffortEx) { System.Diagnostics.Debug.WriteLine($"[best-effort] {_bestEffortEx.Message}"); }
                    return bytes;
                }
                try { if (File.Exists(snapFile)) File.Delete(snapFile); } catch (Exception _bestEffortEx) { System.Diagnostics.Debug.WriteLine($"[best-effort] {_bestEffortEx.Message}"); }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CodingMode Capture] VLC-Snapshot Fehler: {ex.Message}");
                try { if (File.Exists(snapFile)) File.Delete(snapFile); } catch (Exception _bestEffortEx) { System.Diagnostics.Debug.WriteLine($"[best-effort] {_bestEffortEx.Message}"); }
            }
        }

        // 2. Fallback: RenderTargetBitmap auf VideoView. Ergibt das was der User sieht
        //    (nicht den Original-Frame, aber identische BBox-Koordinaten - das reicht
        //    fuer SAM/Qwen-Klassifikation). Dasselbe Verfahren wie ImageAnnotation,
        //    nur direkt aus dem WPF-Visual-Tree.
        try
        {
            var fallback = await Dispatcher.InvokeAsync(() => RenderVideoViewToPng());
            if (fallback != null && fallback.Length > 100)
                return fallback;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[CodingMode Capture] WPF-Render-Fallback fehlgeschlagen: {ex.Message}");
        }

        return null;
    }

    /// <summary>
    /// Rendert das aktuelle VideoView (oder den umgebenden Container) als PNG-Byte-Array.
    /// Nutzt RenderTargetBitmap - liefert das, was WPF anzeigt (inkl. Letterbox).
    /// Bei VLC-HwndHost liefert das oft schwarz, dann ist der OverlayCanvas-Container
    /// der bessere Capture-Punkt.
    /// </summary>
    private byte[]? RenderVideoViewToPng()
    {
        // Wir rendern den ueberordneten Container der OverlayCanvas - das umfasst
        // sowohl das Video-Layer als auch die Overlays. Bei VLC-HwndHost-Pixeln
        // (die WPF nicht sieht) bleibt der Hintergrund schwarz, aber die OverlayCanvas
        // (bbox + UI) ist enthalten.
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
