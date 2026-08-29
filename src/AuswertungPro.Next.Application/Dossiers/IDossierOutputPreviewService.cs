using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace AuswertungPro.Next.Application.Dossiers;

/// <summary>Ein einzelnes Wort samt seiner Lage auf einer PDF-Seite.</summary>
public sealed record DossierOutputPreviewWord(
    string Text,
    double Left,
    double Bottom,
    double Right,
    double Top);

/// <summary>Geometrie und lesbarer Text einer echten Ausgabeseite.</summary>
public sealed record DossierOutputPreviewPage(
    int Number,
    double Width,
    double Height,
    string Text,
    IReadOnlyList<DossierOutputPreviewWord> Words,
    bool IsAttachment = false)
{
    /// <summary>
    /// Diese Beilage ist eine automatisch erzeugte Seite des festen
    /// Erklaeranhangs und kein Kundenoriginal.
    /// </summary>
    public bool IsConditionClassExplanation { get; init; }
}

/// <summary>Die fertige PDF-Ansicht der kurzlebigen Vorschau.</summary>
public sealed record DossierOutputPreviewResult(
    bool Success,
    byte[]? PdfBytes,
    IReadOnlyList<DossierOutputPreviewPage> Pages,
    string Message,

    /// <summary>
    /// Die benannten Ziele der erzeugten PDF: je fuellbare Stelle Seite und
    /// Position, aus den unsichtbaren Word-Textmarken. Damit erkennt die
    /// Vorschau ein Feld exakt statt an seinem Text. Leer bedeutet: der
    /// bisherige Weg ueber den Text gilt unveraendert weiter.
    /// </summary>
    IReadOnlyList<Preview.DossierPdfFieldAnchor>? Anchors = null);

/// <summary>
/// Erzeugt eine echte Ausgabeansicht aus demselben Word-Weg wie der Export.
/// Kundenordner und bereits erzeugte Dateien bleiben dabei unverändert.
/// </summary>
public interface IDossierOutputPreviewService
{
    Task<DossierOutputPreviewResult> CreateAsync(
        DossierExportRequest request,
        CancellationToken ct = default);
}

/// <summary>
/// Sammelt die zur aktuellen Auswahl gehoerenden Protokolle ausschliesslich
/// in einen kurzlebigen Vorschauordner. Der echte Dossierordner wird nur gelesen.
/// </summary>
public interface IDossierPreviewAttachmentService
{
    Task<DossierAttachmentResult> CollectIntoTemporaryAsync(
        DossierExportRequest request,
        string temporaryDossierFolder,
        CancellationToken ct = default);
}
