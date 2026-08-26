using System.Collections.Generic;

namespace AuswertungPro.Next.Application.Dossiers.Preview;

/// <summary>
/// Die Vorschau eines Dossiers, Seite fuer Seite. Reine Struktur: kein Word,
/// keine Oberflaeche, keine Dateien.
///
/// Alle Masse sind Bildpunkte bei 96 dpi. Sie stammen aus der Vorlage selbst —
/// Seitenformat, Raender, Spaltenbreiten, Abstaende, Schriftgroessen und die
/// Lage der schwebenden Kaesten des Deckblatts. Dadurch zeigt die Vorschau die
/// Seite so, wie sie im Dokument steht, und nicht eine Nachbildung.
///
/// Die Platzhalter bleiben als Platzhalter stehen. Erst beim Zeichnen wird ein
/// Wert eingesetzt — nur so weiss das Fenster, welche Stelle zu welchem Feld
/// gehoert.
/// </summary>
public sealed record DossierPreviewDocument(IReadOnlyList<DossierPreviewPage> Pages);

/// <summary>Vier Kantenmasse in Bildpunkten.</summary>
public readonly record struct DossierPreviewEdges(
    double Left, double Top, double Right, double Bottom)
{
    public static DossierPreviewEdges Zero => new(0, 0, 0, 0);

    public static DossierPreviewEdges All(double wert) => new(wert, wert, wert, wert);
}

/// <summary>Blattformat und Satzspiegel.</summary>
public sealed record DossierPreviewGeometry(
    double WidthPx,
    double HeightPx,
    DossierPreviewEdges Margin);

/// <summary>
/// Eine Seite. Die Grenzen sind die Seitenumbrueche der Vorlage und der Beginn
/// eines Kapitels; die echte Seitenzahl kaeme erst aus dem Umbruch in Word.
/// </summary>
public sealed record DossierPreviewPage(
    int Number,
    string Title,
    DossierPreviewGeometry Geometry,
    IReadOnlyList<DossierPreviewBlock> Blocks,
    IReadOnlyList<string> FieldKeys);

public enum DossierPreviewAlignment
{
    Left,
    Center,
    Right,
    Justify
}

/// <summary>Ein Baustein im Textfluss.</summary>
public abstract record DossierPreviewBlock;

/// <summary>Absatzformat, wie es in der Vorlage steht.</summary>
public sealed record DossierPreviewParagraphFormat(
    double SpaceBeforePx,
    double SpaceAfterPx,
    double? LineHeightPx,
    DossierPreviewEdges Indent,
    DossierPreviewAlignment Alignment,
    bool IsHeading,
    bool IsTitle = false,

    /// <summary>
    /// Eine Zeile des Inhaltsverzeichnisses. Word rechnet Nummer und Seitenzahl;
    /// der Titel dazwischen kann im Dossier eine eigene Fassung erhalten.
    /// </summary>
    bool IsTableOfContentsEntry = false)
{
    public static DossierPreviewParagraphFormat Default { get; } = new(
        0, 0, null, DossierPreviewEdges.Zero, DossierPreviewAlignment.Left, false);
}

/// <summary>Zeichenformat eines Textstuecks.</summary>
public sealed record DossierPreviewRunFormat(
    string FontFamily,
    double FontSizePx,
    bool Bold,
    bool Italic,
    bool Underline,
    string? ColorHex)
{
    public static DossierPreviewRunFormat Default { get; } = new(
        "Arial", 14.67, false, false, false, null);
}

/// <summary>
/// Ein Absatz aus einem oder mehreren Stuecken.
///
/// <paramref name="Anchored"/> sind die schwebenden Kaesten, die Word an genau
/// diesen Absatz haengt. Ihre Hoehe zaehlt Word ab dem Absatz — deshalb gehoeren
/// sie hierher und nicht an die Seite. Ein LEERER Absatz bleibt erhalten: er
/// traegt im Dokument den senkrechten Abstand.
/// </summary>
public sealed record DossierPreviewParagraph(
    IReadOnlyList<DossierPreviewRun> Runs,
    DossierPreviewParagraphFormat Format,
    IReadOnlyList<DossierPreviewFloating>? Anchored = null,
    DossierPreviewTocEntry? TocEntry = null) : DossierPreviewBlock
{
    public IReadOnlyList<DossierPreviewFloating> Floating
        => Anchored ?? System.Array.Empty<DossierPreviewFloating>();
}

/// <summary>
/// Die drei getrennten Teile einer echten Word-Verzeichniszeile. Diese
/// Trennung verhindert, dass eine Seitenzahl versehentlich Teil des
/// bearbeitbaren Titels wird.
/// </summary>
public sealed record DossierPreviewTocEntry(
    string Number,
    string Title,
    string PageNumber);

/// <summary>
/// Ein Textstueck. Entweder fester Text der Vorlage oder ein Platzhalter.
/// Genau eines von beiden ist gesetzt.
/// </summary>
public sealed record DossierPreviewRun(
    string? Text,
    string? FieldKey,
    DossierPreviewRunFormat Format)
{
    public static DossierPreviewRun Literal(string text, DossierPreviewRunFormat format)
        => new(text, null, format);

    public static DossierPreviewRun Field(string key, DossierPreviewRunFormat format)
        => new(null, key, format);

    public bool IsField => FieldKey is not null;
}

/// <summary>Eine Tabellenzelle mit ihren Absaetzen und ihrem Rahmen.</summary>
public sealed record DossierPreviewTableCell(
    IReadOnlyList<DossierPreviewParagraph> Paragraphs,
    DossierPreviewEdges Padding,
    DossierPreviewEdges Borders,
    string? ShadingHex,
    int GridSpan);

public sealed record DossierPreviewTableRow(
    IReadOnlyList<DossierPreviewTableCell> Cells,
    double? MinimumHeightPx = null);

/// <summary>
/// Eine Tabelle mit den Spaltenbreiten der Vorlage.
/// <paramref name="RepeatKey"/> ist gesetzt, wenn die Vorlage eine
/// Wiederholzeile fuehrt; <paramref name="RepeatTemplate"/> ist deren Bauplan,
/// damit jede erzeugte Zeile dasselbe Aussehen bekommt.
///
/// <paramref name="RepeatIndex"/> ist die STELLE, an der die erzeugten Zeilen
/// stehen — sie muessen dort erscheinen, wo die Vorlage sie fuehrt. In der
/// Informationstabelle folgen darunter noch die Aktennotiz und die
/// Rueckmeldung; angehaengt statt eingesetzt stuenden sie in falscher Ordnung.
/// </summary>
public sealed record DossierPreviewTable(
    IReadOnlyList<double> ColumnWidthsPx,
    double IndentPx,
    IReadOnlyList<DossierPreviewTableRow> Rows,
    string? RepeatKey,
    IReadOnlyList<string> RepeatCellKeys,
    DossierPreviewTableRow? RepeatTemplate,
    int RepeatIndex = -1) : DossierPreviewBlock;

/// <summary>Ein fest eingebettetes Bild, zum Beispiel Logo oder Wappen.</summary>
public sealed record DossierPreviewPicture(
    byte[] Bytes,
    double WidthPx,
    double HeightPx) : DossierPreviewBlock;

/// <summary>
/// Eine Bildstelle, die erst beim Erzeugen gefuellt wird.
///
/// Nur die BREITE steht fest — sie ist dieselbe, die der Export setzt. Die
/// Hoehe ergibt sich aus dem Seitenverhaeltnis des tatsaechlichen Bildes; eine
/// hier erfundene Hoehe waere eine andere als im fertigen Dossier.
/// </summary>
public sealed record DossierPreviewImage(
    string FieldKey,
    double WidthPx) : DossierPreviewBlock;

/// <summary>
/// Ein schwebendes Objekt: Textkasten, Bild oder Rahmen des Deckblatts.
/// <paramref name="LeftPx"/> zaehlt ab dem Blattrand, <paramref name="TopPx"/>
/// ab dem Absatz, an dem das Objekt haengt — so wie Word es fuehrt.
/// </summary>
public sealed record DossierPreviewFloating(
    double LeftPx,
    double TopPx,
    double WidthPx,
    double HeightPx,
    IReadOnlyList<DossierPreviewBlock> Blocks,
    double BorderWidthPx,
    string? BorderColorHex,
    string? FillHex);
