using System.Security.Cryptography;
using AuswertungPro.Next.Application.Ai.Training;

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
    int? PipeDiameterMm,
    string? ExistingSampleId = null,  // gesetzt, wenn ein unvollstaendiges Goldsample repariert wird
    string? ExistingCode = null,
    string? ExistingBeschreibung = null,
    string? SuggestedMainCode = null, // unverbindlicher Ordnerhinweis aus dem Gold-Eingang
    bool IsStreckenschaden = false,
    WorkbenchSourceSuggestion? SourceSuggestion = null)
{
    /// <summary>
    /// Sicher aus der Quelle gelesenes Inspektionsdatum. Null bedeutet unbekannt;
    /// aus Datei- oder Ordnernamen wird hier kein Datum geraten.
    /// </summary>
    public DateTime? InspectionDate { get; init; }

    /// <summary>
    /// Herkunft eines geladenen Bestandssamples. Sie verhindert, dass ein
    /// unvollstaendiges PDF-Sample beim Nachsegmentieren zu ManualCoding wird.
    /// </summary>
    public string? ExistingSourceType { get; init; }

    /// <summary>Unveraenderte Herkunftsnotiz eines geladenen Bestandssamples.</summary>
    public string? ExistingNotes { get; init; }

    /// <summary>
    /// Bereits persoenlich gesetzte, weiterhin gueltige Hand-Box. Sie darf fuer eine
    /// erneute SAM-Segmentierung vorausgefuellt werden, bleibt aber bis zur sichtbaren
    /// menschlichen Bestaetigung nur ein Arbeitsvorschlag.
    /// </summary>
    public BoundingBox? ExistingBox { get; init; }

    /// <summary>
    /// Bereits gespeicherte und gegen das echte Bild gepruefte Goldmaske. Eine
    /// Qualitaetspruefung zeigt sie zuerst unveraendert; erst eine neue Hand-Box
    /// ersetzt sie durch einen frischen SAM-Lauf.
    /// </summary>
    public WorkbenchSegmentation? ExistingSegmentation { get; init; }

    /// <summary>Bereits gespeicherte Uhrlage eines Bestandssamples.</summary>
    public double? ExistingClockPosition { get; init; }

    /// <summary>Bereits gespeicherte Schadensstufe eines Bestandssamples.</summary>
    public int? ExistingSeverity { get; init; }

    /// <summary>
    /// Erwarteter SHA-256 des angezeigten Bildstands. Wenn gesetzt, darf der
    /// Speicherweg ausschliesslich genau diese Bildbytes uebernehmen.
    /// </summary>
    public string? ExpectedImageSha256 { get; init; }

    /// <summary>
    /// Bestaetigungsstand beim Laden eines Bestandssamples. Eine zwischenzeitliche
    /// Aenderung in einem anderen Fenster sperrt das Speichern fail-closed.
    /// </summary>
    public DateTimeOffset? ExpectedConfirmedAtUtc { get; init; }
}

/// <summary>
/// Fachliche Vorgabe aus einer externen, bereits codierten Quelle.
/// Sie ist weder ein KI-Treffer noch ein bestehendes Goldsample: Der Mensch muss
/// weiterhin Foto, Code, Hand-Box und SAM-Maske pruefen und persoenlich bestaetigen.
/// </summary>
public sealed record WorkbenchSourceSuggestion(
    string VsaCode,
    string Beschreibung,
    string SourceDocumentName,
    string SourceDocumentSha256,
    int PageNumber,
    string? PhotoId,
    string MatchKind)
{
    /// <summary>Inspektionsdatum aus dem PDF-Protokoll, falls eindeutig vorhanden.</summary>
    public DateTime? InspectionDate { get; init; }
}

/// <summary>
/// Unveraenderliche Momentaufnahme der Originalbildbytes.
/// Der Konstruktor ist absichtlich privat: Bytes und SHA-256 koennen dadurch nie
/// unabhaengig voneinander an den Speicherweg uebergeben werden.
/// </summary>
public sealed class WorkbenchImageSnapshot
{
    private readonly byte[] _imageBytes;

    private WorkbenchImageSnapshot(byte[] imageBytes, string extension)
    {
        _imageBytes = imageBytes;
        Extension = extension;
        Sha256 = Convert.ToHexStringLower(SHA256.HashData(_imageBytes));
    }

    public string Sha256 { get; }

    public string Extension { get; }

    public int ByteLength => _imageBytes.Length;

    public static WorkbenchImageSnapshot Create(byte[] imageBytes, string extension)
    {
        ArgumentNullException.ThrowIfNull(imageBytes);
        if (imageBytes.Length == 0)
            throw new ArgumentException("Die Originalbildbytes duerfen nicht leer sein.", nameof(imageBytes));
        if (string.IsNullOrWhiteSpace(extension))
            throw new ArgumentException("Die Bildendung darf nicht leer sein.", nameof(extension));

        var normalizedExtension = extension.Trim().ToLowerInvariant();
        if (!normalizedExtension.StartsWith('.'))
            normalizedExtension = $".{normalizedExtension}";

        return new WorkbenchImageSnapshot(
            (byte[])imageBytes.Clone(),
            normalizedExtension);
    }

    /// <summary>
    /// Liefert eine Arbeitskopie. Der interne Snapshot bleibt auch dann unveraendert,
    /// wenn ein nachgelagerter Speicher die uebergebenen Bytes veraendern sollte.
    /// </summary>
    public byte[] CopyImageBytes()
        => (byte[])_imageBytes.Clone();
}

/// <summary>Codevorschlag der KI zum aktuell geprueften Foto.</summary>
public sealed record WorkbenchSuggestion(
    IReadOnlyList<WorkbenchCodeCandidate> Candidates,   // absteigend nach Confidence
    bool FrameUsable,                 // false = Quality-Gate des Sidecars (unscharf/dunkel)
    string QualityReason,
    bool IsBend,                      // Bogen-Veto-Signal
    bool ModelAvailable = true,       // false = benoetigter KI-Dienst oder Modell nicht verfuegbar
    string UnavailableReason = "");

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
    bool Degraded,
    int? MaskAreaPixels = null,
    double? Confidence = null,
    string? Label = null);

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
    string? TeacherAnnotationId,
    bool GoldApproved = false,        // true nur nach vollständigem persönlichen Gold-Gate
    string? StoredImageSha256 = null, // bindet weitere Objekte an exakt dieselben Bildbytes
    DateTimeOffset? StoredConfirmedAtUtc = null); // Versionsstand fuer eine sichere Draft-Reparatur
