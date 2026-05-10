using System.IO;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Views.Windows;

// Slice 8a Auto-Kalibrierung-Wiring Step 1 + 2 —
// PNG-Decoder + einmaliger Auto-Kalibrierungs-Trigger.
// Mini-ADR: docs/adrs/2026-05-10-slice-8a-auto-kalibrierung.md
//
// Step 3 verdrahtet TryAutoCalibrateOnceAsync im LiveLoop nach dem
// Frame-Readiness-Gate.
public partial class CodingModeWindow
{
    /// <summary>true wenn Auto-Kalibrierung erfolgreich war ODER
    /// das Versuchs-Limit erreicht ist. Verhindert weitere Auto-Kal-
    /// Aufrufe pro Frame; manuell darf der User trotzdem jederzeit
    /// kalibrieren.</summary>
    private bool _calibrationAutoTried;

    /// <summary>Slice 8a.6.D 2026-05-10: Anzahl bisheriger Auto-Kal-Versuche
    /// mit gueltigem Bitmap aber Algorithmus-Failure. Vorher wurde bereits
    /// nach dem ersten Versuch (ggf. mit defektem ersten Frame) aufgegeben.
    /// Jetzt: bis zu MaxAutoCalibrationAttempts Versuche.</summary>
    private int _calibrationAutoAttempts;
    private const int MaxAutoCalibrationAttempts = 5;

    /// <summary>Decodiert PNG-Bytes (wie sie CaptureCurrentFrameAsync liefert)
    /// in eine BitmapSource fuer AutoCalibrationService.TryAutoCalibrate.
    /// Returns null wenn pngBytes null/empty/korrupt sind — wirft nicht.</summary>
    internal static BitmapSource? DecodePngToBitmap(byte[]? pngBytes)
    {
        if (pngBytes is null || pngBytes.Length == 0) return null;
        try
        {
            using var stream = new MemoryStream(pngBytes);
            var decoder = new PngBitmapDecoder(
                stream,
                BitmapCreateOptions.PreservePixelFormat,
                BitmapCacheOption.OnLoad);
            if (decoder.Frames.Count == 0) return null;
            var frame = decoder.Frames[0];
            frame.Freeze();
            return frame;
        }
        catch
        {
            // Korrupte / nicht-PNG-Bytes — silently null statt Throw,
            // damit der Auto-Calibration-Pfad einen erkennbaren Fehler-
            // Indikator hat ohne den Live-Loop zu kippen.
            return null;
        }
    }

    /// <summary>Versucht genau einmal pro Coding-Modus-Session aus dem
    /// aktuellen Frame eine Pipe-Calibration abzuleiten. Frueh-Returns
    /// fuer "schon kalibriert", "schon versucht", "DN fehlt", "PNG kaputt",
    /// "Algo liefert null". Bei Erfolg wird die Calibration via
    /// _overlayService.SetCalibration gesetzt und der Status-Text
    /// aktualisiert.
    ///
    /// Hinweis: Die zurueckgelieferte PipeCalibration setzt
    /// `WasManuallyCalibrated=true` — bestehende Eigenheit des
    /// AutoCalibrationService (Field-Name irrefuehrend, markiert
    /// "Calibration gueltig", nicht "User-gesetzt"). Der Wert wird
    /// unveraendert uebernommen; Rename ist Folge-Slice falls noetig.</summary>
    private async Task TryAutoCalibrateOnceAsync(byte[]? pngBytes)
    {
        if (_calibrationAutoTried) return;
        if (_overlayService.IsCalibrated) return;
        if (_haltung is null) return;
        if (!_haltung.Fields.TryGetValue("DN_mm", out var dnStr) ||
            !int.TryParse(dnStr, out var dn) || dn <= 0) return;

        // Slice 8a.6.D (2026-05-10): Limit-Check vor dem Versuch — wenn
        // wir das Maximum erreicht haben, geben wir auf und melden dem
        // User. Vorher wurde nach dem ERSTEN Versuch aufgegeben, auch
        // wenn der Frame defekt war.
        if (_calibrationAutoAttempts >= MaxAutoCalibrationAttempts)
        {
            _calibrationAutoTried = true;
            await Dispatcher.InvokeAsync(() =>
                TxtCalibrationStatus.Text = "Auto-Kalibrierung fehlgeschlagen — bitte manuell kalibrieren");
            return;
        }

        // Bitmap-Dekode-Failure zaehlt NICHT als Versuch — das war meist
        // ein Capture-Problem (schwarzer Frame, defektes PNG), nicht der
        // Algorithmus. Naechster Frame bekommt eine neue Chance.
        var bitmap = DecodePngToBitmap(pngBytes);
        if (bitmap is null) return;

        // Echter Versuch — Algorithmus hat ein Bild bekommen.
        _calibrationAutoAttempts++;

        var result = AutoCalibrationService.TryAutoCalibrate(bitmap, dn);
        if (result is null)
        {
            // Algorithmus hat keine plausible Kante gefunden. Naechster
            // Frame versucht erneut, bis Limit erreicht.
            return;
        }

        // Erfolg: nicht mehr versuchen.
        _calibrationAutoTried = true;
        await Dispatcher.InvokeAsync(() =>
        {
            _overlayService.SetCalibration(result);
            TxtCalibrationStatus.Text = $"Auto-kalibriert: DN {dn} mm";
        });
    }
}
