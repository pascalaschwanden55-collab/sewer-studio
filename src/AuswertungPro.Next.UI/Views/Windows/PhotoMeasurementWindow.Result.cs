using System;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Views.Windows;

// PhotoMeasurementWindow Result-Block: Overlay ins Foto einbrennen + Ok-Button-
// Logik (Geometrie/Tool/Subject -> PhotoMeasurementResult). Aus dem Hauptdatei
// extrahiert (Slice 5c).
public partial class PhotoMeasurementWindow
{
    // ═══════════════════════════════════════════════
    // Overlay ins Foto einbrennen (DPI-korrekt)
    // ═══════════════════════════════════════════════

    private string? BurnOverlayToPhoto()
    {
        if (PhotoImage.Source is not BitmapSource bmpSrc) return null;

        var r = GetImageRenderedRect(PhotoImage);
        if (r.Width <= 0 || r.Height <= 0) return null;

        // In ORIGINALAUFLOESUNG rendern (nicht Display-Groesse)
        int outW = bmpSrc.PixelWidth;
        int outH = bmpSrc.PixelHeight;
        if (outW <= 0 || outH <= 0) return null; // Bild hat keine gueltige Groesse

        var rtb = new RenderTargetBitmap(outW, outH, 96, 96, PixelFormats.Pbgra32);

        var dv = new DrawingVisual();
        using (var dc = dv.RenderOpen())
        {
            // 1. Original-Foto in voller Aufloesung
            dc.DrawImage(bmpSrc, new Rect(0, 0, outW, outH));

            // 2. Canvas-Overlay hochskalieren: Display-Bereich → Originalaufloesung
            double scaleX = outW / r.Width;
            double scaleY = outH / r.Height;

            // Nur den gerenderten Bildbereich des Canvas nehmen (Letterbox-Offset abziehen)
            var vb = new VisualBrush(OverlayCanvas)
            {
                Viewbox = new Rect(r.X, r.Y, r.Width, r.Height),
                ViewboxUnits = BrushMappingMode.Absolute,
                Stretch = Stretch.Fill
            };
            dc.DrawRectangle(vb, null, new Rect(0, 0, outW, outH));
        }
        rtb.Render(dv);

        // PNG speichern
        var outPath = System.IO.Path.ChangeExtension(_photoPath, null) + "_overlay.png";
        var enc = new PngBitmapEncoder();
        enc.Frames.Add(BitmapFrame.Create(rtb));
        using var fs = File.Create(outPath);
        enc.Save(fs);
        return outPath;
    }

    // ═══════════════════════════════════════════════
    // OK / Abbrechen
    // ═══════════════════════════════════════════════

    private void BtnOk_Click(object sender, RoutedEventArgs e)
    {
        // Foto-Assistent: Geometrie/Parameters/VSA-Code aus den drei neuen Werkzeugen ableiten
        if (IsPhotoAssistantActive)
        {
            _currentGeometry ??= new OverlayGeometry();
            ApplyPhotoAssistantToOverlay(_currentGeometry);
        }

        string? overlayPath = null;
        if (_currentGeometry != null)
        {
            try
            {
                overlayPath = BurnOverlayToPhoto();
            }
            catch (Exception ex)
            {
                // Overlay-Export fehlgeschlagen → trotzdem Ergebnis zurueckgeben (ohne Overlay-Foto)
                TxtStatus.Text = $"Overlay-Export fehlgeschlagen: {ex.Message}";
            }
        }

        // Foto-Assistent: zusaetzliches Schablonen-PNG fuer FotoPaths
        if (IsPhotoAssistantActive)
        {
            try
            {
                var measuredPath = CapturePhotoWithOverlay(_photoPath);
                if (!string.IsNullOrWhiteSpace(measuredPath) && string.IsNullOrWhiteSpace(overlayPath))
                    overlayPath = measuredPath;
            }
            catch { /* best-effort */ }
        }

        Result = new PhotoMeasurementResult
        {
            Geometry = _currentGeometry,
            OverlayPhotoPath = overlayPath,
            Confirmed = true,
            UpdatedCalibration = _calibration
        };
        // V4.3: Werkzeug-Metadaten (Value, Einheit, Tool, Subject) fuer VsaFinding-Schreibpfad
        PopulateResultMetadata(Result);

        // Foto-Assistent: VSA-Code-Vorschlag + Parameters in Result einbetten
        if (IsPhotoAssistantActive)
        {
            var (code, descr, parameters) = GetPhotoAssistantSuggestion();
            Result.MeasurementTool ??= _paActive switch
            {
                PaTool.Deformation => "Deformation",
                PaTool.BendAngle => "Bogen/Knick",
                PaTool.Lateral => "Anschluss",
                _ => Result.MeasurementTool
            };
            Result.SuggestedVsaCode = code;
            Result.SuggestedVsaDescription = descr;
            Result.PhotoAssistantParameters = parameters;
        }

        DialogResult = true;
        Close();
    }

    /// <summary>
    /// V4.3 — befuellt PhotoMeasurementResult mit Werkzeug-Herkunft und Einheit,
    /// damit der Consumer (CodingModeWindow etc.) die Werte in VsaFinding schreiben kann.
    /// </summary>
    private void PopulateResultMetadata(PhotoMeasurementResult r)
    {
        switch (_activeTool)
        {
            case PhotoTool.Ruler:
                r.MeasurementTool = "Lineal";
                if (_currentGeometry?.Q1Mm is double ruler)
                {
                    r.Value1 = ruler.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);
                    r.Unit1 = "mm";
                }
                break;

            case PhotoTool.PipeSurface:
                r.MeasurementTool = "Rohroberflaeche";
                if (_currentGeometry?.Q1Mm is double surface)
                {
                    r.Value1 = surface.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);
                    r.Unit1 = "mm";
                }
                if (_currentGeometry?.Q2Mm is double chord)
                {
                    r.Value2 = chord.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);
                    r.Unit2 = "mm Sehne";
                }
                break;

            case PhotoTool.CrackWidth:
                r.MeasurementTool = "Rissbreite";
                if (_currentGeometry?.Q1Mm is double crack)
                {
                    r.Value1 = crack.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);
                    r.Unit1 = "mm";
                }
                break;

            case PhotoTool.JointOffset:
                r.MeasurementTool = "Muffenversatz";
                if (_currentGeometry?.Q1Mm is double joint)
                {
                    r.Value1 = joint.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);
                    r.Unit1 = "mm";
                }
                if (_currentGeometry?.FillPercent is double jointPct)
                {
                    r.Value2 = jointPct.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);
                    r.Unit2 = "%";
                }
                break;

            case PhotoTool.Connection:
                // Anschluss: Linie wird als H x B interpretiert — aktuell nur 1 Wert
                // (die 2. Dimension wird im CodingModeWindow ergaenzt falls vorhanden).
                r.MeasurementTool = "Anschluss";
                if (_currentGeometry?.Q1Mm is double conn)
                {
                    r.Value1 = conn.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);
                    r.Unit1 = "mm";
                }
                break;

            case PhotoTool.LevelWater:
                r.MeasurementTool = "Wasserstand";
                if (_currentGeometry?.FillPercent is double w)
                {
                    r.Value1 = w.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);
                    r.Unit1 = "%";
                }
                break;

            case PhotoTool.LevelDeposit:
                r.MeasurementTool = "Ablagerung";
                if (_currentGeometry?.FillPercent is double d)
                {
                    r.Value1 = d.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);
                    r.Unit1 = "%";
                }
                break;

            case PhotoTool.LevelObstacle:
                r.MeasurementTool = "Hindernis";
                if (_currentGeometry?.FillPercent is double o)
                {
                    r.Value1 = o.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);
                    r.Unit1 = "%";
                }
                break;

            case PhotoTool.Deformation:
                r.MeasurementTool = "Deformation";
                if (_currentGeometry?.FillPercent is double def)
                {
                    r.Value1 = def.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);
                    r.Unit1 = "%";
                }
                break;

            case PhotoTool.CrossSection:
                r.MeasurementTool = "Querschnitt";
                r.MeasurementSubject = _crossSectionSubject ?? "Sonstige";
                if (_currentGeometry?.FillPercent is double cs)
                {
                    r.Value1 = cs.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);
                    r.Unit1 = "%";
                }
                break;

            case PhotoTool.Lateral:
                r.MeasurementTool = "Abzweig";
                if (_currentGeometry?.ArcDegrees is double lat)
                {
                    r.Value1 = lat.ToString("F0", System.Globalization.CultureInfo.InvariantCulture);
                    r.Unit1 = "°";
                }
                break;

            case PhotoTool.Bend:
                r.MeasurementTool = "Bogen";
                if (_currentGeometry?.ArcDegrees is double bend)
                {
                    r.Value1 = bend.ToString("F0", System.Globalization.CultureInfo.InvariantCulture);
                    r.Unit1 = "°";
                }
                break;

            case PhotoTool.RingBBox:
                r.MeasurementTool = "Ringriss";
                // Value1 = Anzahl generierter BBoxes (12); Einheit = "BBoxes" fuer Klarheit
                if (_currentGeometry?.Points is { Count: > 0 } pts)
                {
                    r.Value1 = pts.Count.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    r.Unit1 = "BBoxes";
                }
                break;

            case PhotoTool.Calibration:
            case PhotoTool.MarkRect:
                // Kein Messwert — Kalibrierung ist interne Skala, MarkRect ist KI-Training-BBox
                break;
        }
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
