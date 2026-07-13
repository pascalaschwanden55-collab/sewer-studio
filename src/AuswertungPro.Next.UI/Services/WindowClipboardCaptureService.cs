using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using AuswertungPro.Next.Application.Common;

namespace AuswertungPro.Next.UI.Services;

public static class WindowClipboardCaptureService
{
    private const int Srccopy = 0x00CC0020;

    [DllImport("user32.dll")]
    private static extern IntPtr GetDC(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleDC(IntPtr hdc);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int w, int h);

    [DllImport("gdi32.dll")]
    private static extern IntPtr SelectObject(IntPtr hdc, IntPtr h);

    [DllImport("gdi32.dll")]
    private static extern bool BitBlt(IntPtr hdcDest, int x, int y, int w, int h, IntPtr hdcSrc, int sx, int sy, int rop);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr h);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteDC(IntPtr hdc);

    public static bool TryCopyWindowToClipboard(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);

        IntPtr screenDc = IntPtr.Zero, memDc = IntPtr.Zero, bitmap = IntPtr.Zero;
        try
        {
            var source = PresentationSource.FromVisual(window);
            var scaleX = source?.CompositionTarget?.TransformToDevice.M11 ?? 1.0;
            var scaleY = source?.CompositionTarget?.TransformToDevice.M22 ?? 1.0;
            var topLeft = window.PointToScreen(new Point(0, 0));
            var x = (int)Math.Round(topLeft.X);
            var y = (int)Math.Round(topLeft.Y);
            var width = (int)Math.Round(window.ActualWidth * scaleX);
            var height = (int)Math.Round(window.ActualHeight * scaleY);
            if (width <= 0 || height <= 0)
                return false;

            screenDc = GetDC(IntPtr.Zero);
            memDc = CreateCompatibleDC(screenDc);
            bitmap = CreateCompatibleBitmap(screenDc, width, height);
            var old = SelectObject(memDc, bitmap);
            BitBlt(memDc, 0, 0, width, height, screenDc, x, y, Srccopy);
            SelectObject(memDc, old);

            var image = Imaging.CreateBitmapSourceFromHBitmap(
                bitmap,
                IntPtr.Zero,
                Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());
            image.Freeze();
            Clipboard.SetImage(image);
            return true;
        }
        catch (Exception ex)
        {
            BestEffort.ReportWarning("[Screen] Fensteraufnahme fehlgeschlagen: " + ex.Message);
            return false;
        }
        finally
        {
            if (bitmap != IntPtr.Zero) DeleteObject(bitmap);
            if (memDc != IntPtr.Zero) DeleteDC(memDc);
            if (screenDc != IntPtr.Zero) ReleaseDC(IntPtr.Zero, screenDc);
        }
    }
}
