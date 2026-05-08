// AuswertungPro – Video-Selbsttraining Phase 2
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using AuswertungPro.Next.Application.Imaging;
using Microsoft.Extensions.Logging;

namespace AuswertungPro.Next.Infrastructure.Ai.Training.Services;

/// <summary>
/// Leichtgewichtiger Qualitaetsfilter fuer Video-Frames.
/// Prueft Schaerfe (Laplace-Varianz), Helligkeit und Duplikate (dHash).
/// Kein NuGet noetig — nutzt WPF BitmapSource fuer Bildoperationen.
///
/// Phase 3.4: Min-Luminance ist adaptiv. Wenn in den letzten 50 Frames
/// mehr als 30 % wegen "zu dunkel" verworfen wurden (typisch bei dunklen
/// Beton-Rohren mit Wassersohle), wird die Schwelle temporaer auf
/// <see cref="MinLuminanceFloor"/> gesenkt. Faellt die Reject-Rate wieder
/// unter 15 %, wird <see cref="MinLuminance"/> wiederhergestellt (Hysterese).
/// </summary>
public sealed class FrameQualityFilter
{
    // Schwellen (empirisch, konfigurierbar)
    private readonly double _minLaplacianVariance;
    private readonly double _maxLuminance;
    private readonly int _maxHammingDistance;
    private readonly ILogger? _logger;

    // Adaptive Helligkeitsschwelle
    /// <summary>Standard-Mindesthelligkeit (Default 30). Wird beim Init gesetzt.</summary>
    public double MinLuminance { get; init; } = 30.0;
    /// <summary>Untergrenze fuer adaptive Absenkung (Default 20).</summary>
    public double MinLuminanceFloor { get; init; } = 20.0;

    // Adaptive State (lock-geschuetzt fuer Thread-Sicherheit, da Pipeline parallel laufen kann)
    private const int AdaptiveWindowSize = 50;
    private const double LowerThresholdRate = 0.30;   // > 30 % dunkel -> Floor aktivieren
    private const double RestoreThresholdRate = 0.15; // < 15 % dunkel -> Default wiederherstellen
    private readonly object _adaptiveLock = new();
    private readonly Queue<bool> _recentDarkRejections = new(AdaptiveWindowSize);
    private int _darkRejectsInWindow;
    private double _currentMinLuminance;
    private bool _adaptiveLowered;

    private ulong? _lastHash;

    // Verwerfungs-Statistik (Telemetrie)
    private int _totalFrames;
    private int _rejectedDark;
    private int _rejectedBright;
    private int _rejectedBlurry;
    private int _rejectedDuplicate;

    /// <summary>Verwerfungs-Statistik als Zusammenfassung (fuer Logging nach Video-Ende).</summary>
    public string GetRejectionSummary()
    {
        var accepted = _totalFrames - _rejectedDark - _rejectedBright - _rejectedBlurry - _rejectedDuplicate;
        var pct = _totalFrames > 0 ? (double)accepted / _totalFrames * 100 : 0;
        return $"Frames: {_totalFrames} total, {accepted} akzeptiert ({pct:F0}%), " +
               $"verworfen: {_rejectedDark} dunkel, {_rejectedBright} hell, {_rejectedBlurry} unscharf, {_rejectedDuplicate} Duplikat";
    }

    public FrameQualityFilter(
        double minLaplacianVariance = 100.0,
        double minLuminance = 30.0,
        double maxLuminance = 240.0,
        int maxHammingDistance = 5,
        ILogger? logger = null,
        double minLuminanceFloor = 20.0)
    {
        _minLaplacianVariance = minLaplacianVariance;
        MinLuminance = minLuminance;
        MinLuminanceFloor = minLuminanceFloor;
        _maxLuminance = maxLuminance;
        _maxHammingDistance = maxHammingDistance;
        _logger = logger;
        _currentMinLuminance = minLuminance;
    }

    /// <summary>
    /// Prueft ob ein Frame akzeptabel ist (Schaerfe + Helligkeit + kein Duplikat).
    /// Aktualisiert den internen Hash fuer die naechste Duplikat-Pruefung.
    /// </summary>
    public bool IsAcceptable(byte[] pngBytes)
    {
        if (pngBytes is null || pngBytes.Length < 100) return false;

        byte[] grayscale;
        int width, height;

        try
        {
            (grayscale, width, height) = DecodeToGrayscale(pngBytes);
        }
        catch
        {
            return false; // Defektes Bild
        }

        if (width < 8 || height < 8) return false;

        Interlocked.Increment(ref _totalFrames);

        // 1. Helligkeit pruefen — Schwelle ist adaptiv (siehe TrackDarkRejection)
        var luminance = ComputeMeanLuminance(grayscale);
        double effectiveMin = Volatile.Read(ref _currentMinLuminance);
        bool tooDark = luminance < effectiveMin;
        TrackDarkRejection(tooDark);
        if (tooDark)
        {
            Interlocked.Increment(ref _rejectedDark);
            _logger?.LogInformation("Frame verworfen: zu dunkel (Helligkeit {Lum:F1} < {Min:F1})",
                luminance, effectiveMin);
            return false;
        }
        if (luminance > _maxLuminance)
        {
            Interlocked.Increment(ref _rejectedBright);
            _logger?.LogInformation("Frame verworfen: zu hell (Helligkeit {Lum:F1} > {Max})",
                luminance, _maxLuminance);
            return false;
        }

        // 2. Schaerfe pruefen (Laplacian-Varianz)
        var sharpness = ComputeLaplacianVariance(grayscale, width, height);
        if (sharpness < _minLaplacianVariance)
        {
            Interlocked.Increment(ref _rejectedBlurry);
            _logger?.LogInformation("Frame verworfen: zu unscharf (Schaerfe {Sharp:F1} < {Min:F1})",
                sharpness, _minLaplacianVariance);
            return false;
        }

        // 3. Duplikat pruefen (dHash)
        var hash = ComputeDHash(grayscale, width, height);
        if (_lastHash.HasValue && HammingDistance(hash, _lastHash.Value) <= _maxHammingDistance)
        {
            Interlocked.Increment(ref _rejectedDuplicate);
            _logger?.LogDebug("Frame verworfen: Duplikat (Hamming={Dist})",
                HammingDistance(hash, _lastHash.Value));
            return false;
        }

        _lastHash = hash;
        return true;
    }

    /// <summary>Setzt Hash + Statistik zurueck (z.B. bei neuem Video). Loggt Zusammenfassung.</summary>
    public void Reset()
    {
        if (_totalFrames > 0)
            _logger?.LogInformation("[FrameQualityFilter] {Summary}", GetRejectionSummary());
        _lastHash = null;
        _totalFrames = 0;
        _rejectedDark = 0;
        _rejectedBright = 0;
        _rejectedBlurry = 0;
        _rejectedDuplicate = 0;

        // Adaptive State zuruecksetzen (z.B. bei neuer Haltung)
        lock (_adaptiveLock)
        {
            _recentDarkRejections.Clear();
            _darkRejectsInWindow = 0;
            _currentMinLuminance = MinLuminance;
            _adaptiveLowered = false;
        }
    }

    /// <summary>
    /// Aktualisiert das gleitende 50-Frame-Fenster mit dem aktuellen Dunkel-Reject-Status.
    /// Senkt die Mindesthelligkeit auf <see cref="MinLuminanceFloor"/> wenn die Reject-Rate
    /// 30 % uebersteigt; stellt <see cref="MinLuminance"/> wieder her, sobald die Rate
    /// unter 15 % faellt (Hysterese gegen Flattern).
    /// Thread-sicher: Pipeline kann parallel auf den Filter zugreifen.
    /// </summary>
    private void TrackDarkRejection(bool isDarkReject)
    {
        double newThreshold;
        bool changed;
        bool loweredNow;
        double rate;

        lock (_adaptiveLock)
        {
            _recentDarkRejections.Enqueue(isDarkReject);
            if (isDarkReject) _darkRejectsInWindow++;

            if (_recentDarkRejections.Count > AdaptiveWindowSize)
            {
                if (_recentDarkRejections.Dequeue()) _darkRejectsInWindow--;
            }

            // Erst nach gefuelltem Fenster anpassen — bei kurzen Sequenzen bleibt die Default-Schwelle.
            if (_recentDarkRejections.Count < AdaptiveWindowSize)
                return;

            rate = (double)_darkRejectsInWindow / _recentDarkRejections.Count;

            if (!_adaptiveLowered && rate > LowerThresholdRate)
            {
                _adaptiveLowered = true;
                _currentMinLuminance = MinLuminanceFloor;
                newThreshold = MinLuminanceFloor;
                changed = true;
                loweredNow = true;
            }
            else if (_adaptiveLowered && rate < RestoreThresholdRate)
            {
                _adaptiveLowered = false;
                _currentMinLuminance = MinLuminance;
                newThreshold = MinLuminance;
                changed = true;
                loweredNow = false;
            }
            else
            {
                return; // Keine Aenderung
            }
        }

        if (changed)
        {
            if (loweredNow)
            {
                _logger?.LogInformation(
                    "FrameQualityFilter: Helligkeitsschwelle adaptiv auf {Value} gesenkt (dunkle-Reject-Rate {Rate:P})",
                    newThreshold, rate);
            }
            else
            {
                _logger?.LogInformation(
                    "FrameQualityFilter: Helligkeitsschwelle auf {Value} zurueckgesetzt (dunkle-Reject-Rate {Rate:P})",
                    newThreshold, rate);
            }
        }
    }

    /// <summary>Berechnet die mittlere Helligkeit eines Grayscale-Bildes.</summary>
    internal static double ComputeMeanLuminance(byte[] grayscale)
    {
        if (grayscale.Length == 0) return 0;
        long sum = 0;
        for (int i = 0; i < grayscale.Length; i++)
            sum += grayscale[i];
        return (double)sum / grayscale.Length;
    }

    /// <summary>
    /// Berechnet die Laplacian-Varianz als Schaerfe-Mass.
    /// Hoeherer Wert = schaerfer. Typisch: &lt;100 = unscharf, &gt;300 = scharf.
    /// </summary>
    internal static double ComputeLaplacianVariance(byte[] grayscale, int width, int height)
    {
        if (width < 3 || height < 3) return 0;

        // 3x3 Laplacian Kernel: [0,1,0 / 1,-4,1 / 0,1,0]
        long sum = 0;
        long sumSq = 0;
        int count = 0;

        for (int y = 1; y < height - 1; y++)
        {
            for (int x = 1; x < width - 1; x++)
            {
                int idx = y * width + x;
                int laplacian =
                    grayscale[idx - width] +        // oben
                    grayscale[idx - 1] +            // links
                    -4 * grayscale[idx] +           // mitte
                    grayscale[idx + 1] +            // rechts
                    grayscale[idx + width];          // unten

                sum += laplacian;
                sumSq += (long)laplacian * laplacian;
                count++;
            }
        }

        if (count == 0) return 0;
        double mean = (double)sum / count;
        double variance = (double)sumSq / count - mean * mean;
        return variance;
    }

    /// <summary>
    /// Berechnet einen 64-bit Differenz-Hash (dHash) fuer Duplikat-Erkennung.
    /// Robust gegen Skalierung und leichte Helligkeitsaenderungen.
    /// </summary>
    internal static ulong ComputeDHash(byte[] grayscale, int width, int height)
    {
        // Auf 9x8 herunterskalieren (9 Spalten × 8 Zeilen → 8×8 = 64 Vergleiche)
        var small = DownscaleGrayscale(grayscale, width, height, 9, 8);

        ulong hash = 0;
        for (int y = 0; y < 8; y++)
        {
            for (int x = 0; x < 8; x++)
            {
                int idx = y * 9 + x;
                if (small[idx] < small[idx + 1])
                    hash |= 1UL << (y * 8 + x);
            }
        }

        return hash;
    }

    /// <summary>Berechnet die Hamming-Distanz zwischen zwei Hashes.</summary>
    internal static int HammingDistance(ulong a, ulong b)
    {
        ulong xor = a ^ b;
        int count = 0;
        while (xor != 0)
        {
            count += (int)(xor & 1);
            xor >>= 1;
        }
        return count;
    }

    /// <summary>Dekodiert PNG-Bytes in ein Grayscale-Array.</summary>
    private static (byte[] Grayscale, int Width, int Height) DecodeToGrayscale(byte[] pngBytes)
    {
        // Phase 5.3 Sub-A: WPF-Imaging-Adapter (frueher BitmapDecoder).
        var img = ImagePixelDecoderProvider.TryDecode(pngBytes);
        if (img is null) return (Array.Empty<byte>(), 0, 0);

        int w = img.Width;
        int h = img.Height;
        var pixels = img.Bgra32Pixels;

        // Grayscale (Luminance-Formel: 0.299R + 0.587G + 0.114B)
        var gray = new byte[w * h];
        for (int i = 0; i < w * h; i++)
        {
            int pi = i * 4;
            gray[i] = (byte)(0.114 * pixels[pi] + 0.587 * pixels[pi + 1] + 0.299 * pixels[pi + 2]);
        }

        return (gray, w, h);
    }

    /// <summary>Bilineare Herunterskalierung eines Grayscale-Bildes.</summary>
    private static byte[] DownscaleGrayscale(byte[] src, int srcW, int srcH, int dstW, int dstH)
    {
        var dst = new byte[dstW * dstH];
        double scaleX = (double)srcW / dstW;
        double scaleY = (double)srcH / dstH;

        for (int dy = 0; dy < dstH; dy++)
        {
            for (int dx = 0; dx < dstW; dx++)
            {
                // Mittelwert im Quellblock
                int sx0 = (int)(dx * scaleX);
                int sy0 = (int)(dy * scaleY);
                int sx1 = Math.Min((int)((dx + 1) * scaleX), srcW);
                int sy1 = Math.Min((int)((dy + 1) * scaleY), srcH);

                long sum = 0;
                int count = 0;
                for (int sy = sy0; sy < sy1; sy++)
                {
                    for (int sx = sx0; sx < sx1; sx++)
                    {
                        sum += src[sy * srcW + sx];
                        count++;
                    }
                }

                dst[dy * dstW + dx] = count > 0 ? (byte)(sum / count) : (byte)0;
            }
        }

        return dst;
    }
}
