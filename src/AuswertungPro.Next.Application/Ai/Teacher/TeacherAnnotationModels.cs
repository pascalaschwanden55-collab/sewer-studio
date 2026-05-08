using System;
using System.Collections.Generic;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.Ai.Teacher;

/// <summary>
/// Lehrer-Annotation: Manuell markierter Bereich auf einem Video-Frame
/// mit zugewiesenem VSA-Code. Append-only — kein Update, kein Delete.
/// Dient als Gold-Standard fuer KI-Training (YOLO, Few-Shot, Retrieval).
/// </summary>
public sealed class TeacherAnnotation
{
    /// <summary>Eindeutige ID der Annotation.</summary>
    public string AnnotationId { get; set; } = Guid.NewGuid().ToString("N")[..12];

    /// <summary>Zeitstempel der Erfassung.</summary>
    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;

    // --- VSA-Code ---
    public string VsaCode { get; set; } = "";
    public string Beschreibung { get; set; } = "";
    public int? Severity { get; set; }

    // --- Position im Kanal ---
    public double MeterPosition { get; set; }
    public TimeSpan VideoTimestamp { get; set; }
    public string? HaltungName { get; set; }
    public string? VideoPath { get; set; }

    // --- Haltungs-Kontext (aus HaltungRecord uebernommen) ---
    /// <summary>Rohrmaterial (z.B. "Polypropylen", "Beton", "PVC", "Steinzeug").
    /// Hilft beim Filtern des Trainingsdatensatzes pro Material und beim
    /// kontextuellen Embedding fuer KB-Retrieval (gleicher Schaden in
    /// gleichem Material zaehlt mehr).</summary>
    public string? Rohrmaterial { get; set; }

    /// <summary>Nennweite in mm (z.B. 185, 250, 300).</summary>
    public int? DnMm { get; set; }

    /// <summary>Profil-Form (z.B. "Kreisprofil", "Eiform"). Aus dem OSD oder
    /// dem Stammdaten-Feld der Haltung.</summary>
    public string? Profil { get; set; }

    /// <summary>Nutzungsart (z.B. "Schmutzabwasser", "Mischabwasser",
    /// "Regenwasser") aus dem HaltungRecord.</summary>
    public string? Nutzungsart { get; set; }

    /// <summary>Frame-spezifische Material-Farbvariante (Override fuer
    /// <see cref="Rohrmaterial"/>). Wichtig weil das HaltungRecord nur eine
    /// Material-Bezeichnung pro Haltung haelt, ein PP-Rohr aber 4
    /// Farb-Varianten haben kann (gruen/grau/orange/schwarz) und sich das
    /// Material innerhalb einer Haltung sogar wechseln kann (z.B.
    /// PP-Hausanschluss zu Beton-Hauptkanal).
    ///
    /// Standardwerte: "PP gruen", "PP grau", "PP orange", "PP schwarz",
    /// "Beton", "PVC orange", "PVC grau", "Steinzeug", "GFK", "andere".
    /// Null = kein Override, das uebergeordnete <see cref="Rohrmaterial"/>
    /// gilt.</summary>
    public string? MaterialFarbVariante { get; set; }

    // --- Geometrie (normiert 0.0-1.0) ---
    public OverlayToolType ToolType { get; set; }
    public List<NormalizedPoint> Points { get; set; } = new();

    /// <summary>BoundingBox in normalisiertem Format: x_center, y_center, width, height (alle 0.0-1.0).</summary>
    public NormalizedBoundingBox BoundingBox { get; set; } = new();

    /// <summary>Uhrposition des Zentrums (0.0-12.0).</summary>
    public double? ClockPosition { get; set; }

    // --- Bilder ---

    /// <summary>Pfad zum vollen Frame (unbearbeitetes Rohbild).</summary>
    public string? FullFramePath { get; set; }

    /// <summary>Pfad zum ausgeschnittenen Bereich (Crop).</summary>
    public string? CroppedRegionPath { get; set; }

    /// <summary>Pfad zur YOLO-Annotation (.txt).</summary>
    public string? YoloAnnotationPath { get; set; }

    /// <summary>SAM-Segmentierungsmaske als Run-Length-Encoding.</summary>
    public string? MaskRle { get; set; }

    /// <summary>Bildgroesse der Maske (Breite).</summary>
    public int? MaskWidth { get; set; }

    /// <summary>Bildgroesse der Maske (Hoehe).</summary>
    public int? MaskHeight { get; set; }

    // --- Masse (aus Kalibrierung) ---
    public double? WidthMm { get; set; }
    public double? HeightMm { get; set; }
}

// NormalizedBoundingBox ist jetzt in AuswertungPro.Next.Application.Ai definiert (einheitliches Modell).
