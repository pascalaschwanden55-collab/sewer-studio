namespace AuswertungPro.Next.Application.Ai.Workbench;

/// <summary>
/// Ein zu pruefendes Bild samt Kontext fuer den Pruefplatz.
/// <see cref="FramePath"/> ist Pflicht; die Haltungsdaten sind optional (z. B. bei losen Fotos).
/// Sind <see cref="HaltungName"/>/<see cref="VideoPath"/> bekannt, schliesst der Save-Weg
/// damit die QuarantineOrigin-Luecke am Teacher-Kandidaten.
/// </summary>
public sealed record WorkbenchItem(
    string FramePath,
    string CaseId,                    // Haltungskennung oder "foto_<yyyyMMdd>_<lfd>"
    double MeterStart,
    double MeterEnd,
    string? HaltungName,              // wenn bekannt: schliesst die QuarantineOrigin-Luecke
    string? VideoPath,
    int? PipeDiameterMm);

/// <summary>Codevorschlag der KI zu einer gezogenen Box.</summary>
public sealed record WorkbenchSuggestion(
    IReadOnlyList<WorkbenchCodeCandidate> Candidates,   // absteigend nach Confidence
    bool FrameUsable,                 // false = Quality-Gate des Sidecars (unscharf/dunkel)
    string QualityReason,
    bool IsBend);                     // Bogen-Veto-Signal

/// <summary>Ein einzelner Codekandidat mit Herkunft.</summary>
/// <param name="Quelle">"cls" = YOLO-Klassifikator, "kb" = aehnlicher gepruefter KB-Fall.</param>
public sealed record WorkbenchCodeCandidate(string VsaCode, double Confidence, string Quelle);

/// <summary>
/// Segmentierungsergebnis der SAM-Box. Bewusst UI-frei (kein WPF-Typ in der Application-Schicht) —
/// die RLE-Maske reicht als transportables Format.
/// </summary>
public sealed record WorkbenchSegmentation(
    string? MaskRle,
    int MaskImageWidth,
    int MaskImageHeight,
    double? AreaPercent,
    string StatusText,
    bool Degraded);

/// <summary>Entscheidung des Menschen zu einer Box.</summary>
public sealed record WorkbenchDecision(
    string VsaCode,                   // finaler Code (bei Akzeptieren = Vorschlag)
    bool WasCorrected,                // true wenn vom Top-Vorschlag abgewichen
    string Beschreibung,              // >= 10 Zeichen (UI generiert Vorlage, editierbar)
    double? ClockPosition,
    int? Severity,
    string ConfirmedByUser);

/// <summary>Ergebnis des Speicherns einer geprueften Box.</summary>
public sealed record WorkbenchSaveResult(
    bool Saved,
    string? RefusalReason,            // gesetzt bei Eval-Abweisung oder Validierungsfehler
    string? SampleId,
    string KbIndexState,              // "Indexed" | "Skipped" | "Error" | "-"
    string? TeacherAnnotationId);
