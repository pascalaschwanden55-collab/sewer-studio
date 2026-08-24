using System.Collections.Generic;

namespace AuswertungPro.Next.Application.Dossiers.Preview;

/// <summary>
/// Die Vorschau eines Dossiers, Seite fuer Seite. Reine Struktur: kein Word,
/// keine Oberflaeche, keine Dateien.
///
/// Das Modell entsteht aus der ECHTEN Vorlage und behaelt die Platzhalter als
/// solche. Erst beim Zeichnen wird ein Wert eingesetzt. Nur so weiss das
/// Fenster, welche Stelle zu welchem Feld gehoert — und kann sie hervorheben.
/// </summary>
public sealed record DossierPreviewDocument(IReadOnlyList<DossierPreviewPage> Pages);

/// <summary>
/// Eine Seite. Die Grenzen sind die Seitenumbrueche der Vorlage und der Beginn
/// eines Kapitels; die echte Seitenzahl kaeme erst aus dem Umbruch in Word.
/// </summary>
public sealed record DossierPreviewPage(
    int Number,
    string Title,
    IReadOnlyList<DossierPreviewBlock> Blocks,
    IReadOnlyList<string> FieldKeys);

/// <summary>Wie ein Absatz dargestellt wird.</summary>
public enum DossierPreviewStyle
{
    Normal,
    Title,
    Heading,
    Small
}

/// <summary>Ein Baustein einer Seite.</summary>
public abstract record DossierPreviewBlock;

/// <summary>Ein Absatz aus einem oder mehreren Stuecken.</summary>
public sealed record DossierPreviewParagraph(
    DossierPreviewStyle Style,
    IReadOnlyList<DossierPreviewRun> Runs) : DossierPreviewBlock;

/// <summary>
/// Eine Tabelle. <paramref name="RepeatKey"/> ist gesetzt, wenn die Vorlage
/// eine Wiederholzeile fuehrt ("Themen", "Eigentuemer", "Aenderungen") — dann
/// entstehen die Datenzeilen erst beim Zeichnen aus den aktuellen Daten.
/// </summary>
public sealed record DossierPreviewTable(
    IReadOnlyList<string> HeaderCells,
    IReadOnlyList<IReadOnlyList<DossierPreviewRun>> FixedRowCells,
    string? RepeatKey,
    IReadOnlyList<string> RepeatCellKeys) : DossierPreviewBlock;

/// <summary>Eine Bildstelle, zum Beispiel der Uebersichtsplan.</summary>
public sealed record DossierPreviewImage(string FieldKey) : DossierPreviewBlock;

/// <summary>
/// Ein Textstueck. Entweder fester Text der Vorlage oder ein Platzhalter.
/// Genau eines von beiden ist gesetzt.
/// </summary>
public sealed record DossierPreviewRun(string? Text, string? FieldKey)
{
    public static DossierPreviewRun Literal(string text) => new(text, null);

    public static DossierPreviewRun Field(string key) => new(null, key);

    public bool IsField => FieldKey is not null;
}
