using AuswertungPro.Next.Application.Export;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.UseCases.Verteilung;

/// <summary>Was verteilt wird.</summary>
public enum VerteilArt
{
    Haltungen,
    Schaechte,
    Dichtheit,
}

/// <summary>Woher die Protokolle kommen: ganzer Ordner oder einzelne Dateien, PDF oder KINS-TXT.</summary>
public enum VerteilQuellenArt
{
    PdfOrdner,
    PdfDateien,
    TxtOrdner,
    TxtDateien,
}

/// <summary>Die gewaehlte Quelle. Genau eines von Ordner oder Dateien ist gefuellt.</summary>
public sealed record VerteilQuelle(VerteilQuellenArt Art, string? Ordner, IReadOnlyList<string> Dateien)
{
    public static VerteilQuelle PdfOrdner(string ordner) => new(VerteilQuellenArt.PdfOrdner, ordner, []);
    public static VerteilQuelle PdfDateien(IReadOnlyList<string> dateien) => new(VerteilQuellenArt.PdfDateien, null, dateien);
    public static VerteilQuelle TxtOrdner(string ordner) => new(VerteilQuellenArt.TxtOrdner, ordner, []);
    public static VerteilQuelle TxtDateien(IReadOnlyList<string> dateien) => new(VerteilQuellenArt.TxtDateien, null, dateien);

    public bool IstTxt => Art is VerteilQuellenArt.TxtOrdner or VerteilQuellenArt.TxtDateien;
    public bool IstEinzeldateien => Art is VerteilQuellenArt.PdfDateien or VerteilQuellenArt.TxtDateien;

    /// <summary>Ordner gesetzt bzw. mindestens eine Datei gewaehlt.</summary>
    public bool IstGewaehlt => IstEinzeldateien
        ? Dateien.Count > 0
        : !string.IsNullOrWhiteSpace(Ordner);
}

/// <summary>
/// Der im Fenster «Verteilen» bestaetigte Auftrag. Er traegt nur, was der Benutzer gewaehlt
/// hat; Zielordner und Verzeichnisbaum bestimmt der Verteilweg wie bisher selbst.
/// </summary>
public sealed record VerteilAuftrag(
    VerteilArt Art,
    DistributionVariant Ablage,
    VerteilQuelle Quelle,
    string? FilmOrdner);

/// <summary>Zustand einer Vorschauzeile. Die Reihenfolge ist ohne Bedeutung.</summary>
public enum VerteilVorschauStatus
{
    /// <summary>Wird abgelegt (Schacht, Dichtheit, Begleit-PDF).</summary>
    Bereit,
    /// <summary>Haltung mit eindeutig gefundenem Film.</summary>
    FilmGefunden,
    /// <summary>Haltung ohne Film: das Protokoll wird abgelegt, der Film fehlt.</summary>
    FilmFehlt,
    /// <summary>Mehrere Filme passen: das Protokoll wird abgelegt, der Film nicht.</summary>
    FilmMehrdeutig,
    /// <summary>Eine inhaltsgleiche Datei liegt bereits im Ziel; es entsteht keine Kopie.</summary>
    SchonVorhanden,
    /// <summary>Kann keinem Objekt zugeordnet werden; wird nicht abgelegt.</summary>
    NichtZugeordnet,
    /// <summary>Ohne teure oder unsichere Vorhersage (z. B. Texterkennung eines Scans).</summary>
    WirdGeprueft,
}

/// <summary>Eine Zeile der Vorschau: ein Protokoll (oder ein Teil eines Sammelberichts).</summary>
public sealed record VerteilVorschauZeile(
    string Protokoll,
    string? Objekt,
    string? Zielordner,
    string? Film,
    VerteilVorschauStatus Status,
    string Hinweis)
{
    /// <summary>Entsteht beim Verteilen voraussichtlich eine neue Ablage?</summary>
    public bool WirdAbgelegt => Status is VerteilVorschauStatus.Bereit
        or VerteilVorschauStatus.FilmGefunden
        or VerteilVorschauStatus.FilmFehlt
        or VerteilVorschauStatus.FilmMehrdeutig
        or VerteilVorschauStatus.WirdGeprueft;

    /// <summary>Muss sich jemand darum kuemmern?</summary>
    public bool BrauchtAufmerksamkeit => Status is VerteilVorschauStatus.FilmFehlt
        or VerteilVorschauStatus.FilmMehrdeutig
        or VerteilVorschauStatus.NichtZugeordnet
        or VerteilVorschauStatus.WirdGeprueft;
}

/// <summary>Anfrage an die schreibfreie Vorschau.</summary>
/// <param name="Zielordner">Wirksamer Hauptordner; null, wenn er erst beim Verteilen abgefragt wird.</param>
/// <param name="Baum">Eingefrorener Verzeichnisbaum wie beim Verteilen (null = ohne Zwischenebenen).</param>
public sealed record VerteilVorschauAnfrage(
    VerteilArt Art,
    DistributionVariant Ablage,
    VerteilQuelle Quelle,
    string? FilmOrdner,
    string? Zielordner,
    DistributionTargetConfig? Baum,
    Project? Projekt);

public sealed record VerteilVorschauFortschritt(int Erledigt, int Gesamt, string? Datei);

/// <summary>Ergebnis der Vorschau samt Kopfzahlen.</summary>
public sealed record VerteilVorschauErgebnis(
    int Dateien,
    IReadOnlyList<VerteilVorschauZeile> Zeilen,
    IReadOnlyList<string> Hinweise)
{
    public static VerteilVorschauErgebnis Leer { get; } = new(0, [], []);

    public int WerdenAbgelegt => Zeilen.Count(z => z.WirdAbgelegt);

    public int BrauchenAufmerksamkeit => Zeilen.Count(z => z.BrauchtAufmerksamkeit);

    /// <summary>«n Dateien · m werden abgelegt · k brauchen Aufmerksamkeit».</summary>
    public string Kopfzeile
        => $"{Dateien} {(Dateien == 1 ? "Datei" : "Dateien")} · {WerdenAbgelegt} "
           + $"{(WerdenAbgelegt == 1 ? "wird" : "werden")} abgelegt · {BrauchenAufmerksamkeit} "
           + $"{(BrauchenAufmerksamkeit == 1 ? "braucht" : "brauchen")} Aufmerksamkeit";
}

/// <summary>
/// Plant eine Verteilung, ohne etwas zu schreiben: keine Datei, kein Ordner, keine
/// Projektaenderung. Was sich nur teuer oder unsicher vorhersagen laesst (Texterkennung
/// eines Scans, Katasterabgleich), meldet sie als «wird beim Verteilen geprüft».
/// </summary>
public interface IVerteilVorschau
{
    VerteilVorschauErgebnis Plane(
        VerteilVorschauAnfrage anfrage,
        IProgress<VerteilVorschauFortschritt>? fortschritt,
        CancellationToken abbruch);
}
