using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.Ai;

/// <summary>
/// Exportiert Lehrer-Annotationen als YOLO-Format-Dateien:
///  - Volles Frame-Bild (images/)
///  - Ausgeschnittener Bereich (crops/)
///  - YOLO-Annotation .txt (labels/)
/// </summary>
public interface ITrainingAnnotationExportService
{
    /// <summary>
    /// Exportiert ein annotiertes Frame als Trainingsdaten.
    /// </summary>
    /// <param name="sourceFramePath">Pfad zum Quell-Frame (PNG/JPG).</param>
    /// <param name="bbox">BoundingBox in normalisiertem Format (0.0-1.0).</param>
    /// <param name="vsaCode">VSA-Schadensklassifikation.</param>
    /// <param name="classId">YOLO-Klassen-ID.</param>
    /// <param name="baseName">Basis-Dateiname (ohne Extension).</param>
    /// <param name="ct">CancellationToken.</param>
    /// <returns>Ergebnis mit Pfaden zu den exportierten Dateien.</returns>
    Task<TrainingAnnotationResult> ExportAsync(
        string sourceFramePath,
        NormalizedBoundingBox bbox,
        string vsaCode,
        int classId,
        string baseName,
        CancellationToken ct = default);
}

/// <summary>
/// BoundingBox im YOLO-Format (normalisiert 0.0-1.0).
/// Einheitliches Modell fuer Export-Service und Teacher-Annotationen.
/// </summary>
public sealed class NormalizedBoundingBox
{
    public double XCenter { get; set; }
    public double YCenter { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }

    /// <summary>YOLO-Zeile: "class_id x_center y_center width height".</summary>
    public string ToYoloLine(int classId)
        => $"{classId} {XCenter:F6} {YCenter:F6} {Width:F6} {Height:F6}";

    /// <summary>Berechnet BoundingBox aus einer Liste normierter Punkte.</summary>
    public static NormalizedBoundingBox FromPoints(IReadOnlyList<NormalizedPoint> points)
    {
        if (points.Count == 0) return new NormalizedBoundingBox();

        double minX = double.MaxValue, minY = double.MaxValue;
        double maxX = double.MinValue, maxY = double.MinValue;

        foreach (var p in points)
        {
            if (p.X < minX) minX = p.X;
            if (p.Y < minY) minY = p.Y;
            if (p.X > maxX) maxX = p.X;
            if (p.Y > maxY) maxY = p.Y;
        }

        return new NormalizedBoundingBox
        {
            XCenter = (minX + maxX) / 2.0,
            YCenter = (minY + maxY) / 2.0,
            Width = maxX - minX,
            Height = maxY - minY
        };
    }
}

/// <summary>
/// Ergebnis eines Trainings-Exports.
/// </summary>
public sealed class TrainingAnnotationResult
{
    public string FullFramePath { get; set; } = "";
    public string CroppedRegionPath { get; set; } = "";
    public string YoloAnnotationPath { get; set; } = "";
    public bool Success { get; set; }
    public string? Error { get; set; }
}
