using System;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Dashboard;

namespace AuswertungPro.Next.Application.UseCases.Uebersicht;

/// <summary>Aussenverbindungen des Vorschau-PDF; alles Fachliche bleibt im UseCase.</summary>
/// <param name="BaueVorschau">
/// Liefert die druckbare Projektvorschau oder null, wenn es nichts zu drucken gibt
/// (dann ist die Meldung bereits gezeigt worden oder <paramref name="BaueVorschau"/> lieferte
/// bewusst nichts).
/// </param>
/// <param name="FrageZiel">Dateidialog; leer bedeutet abgebrochen.</param>
/// <param name="BaueBytes">Der PDF-Erzeuger (Infrastructure).</param>
/// <param name="Schreibe">Schreibt die Bytes an den gewaehlten Pfad.</param>
public sealed record ProjektVorschauPdfActions(
    Func<ProjectPreview?> BaueVorschau,
    Func<string, string?> FrageZiel,
    Func<ProjectPreview, Task<byte[]>> BaueBytes,
    Func<string, byte[], Task> Schreibe);

/// <summary>Wie der Lauf ausgegangen ist.</summary>
public enum ProjektVorschauPdfStatus
{
    /// <summary>Es gab nichts zu drucken.</summary>
    KeineVorschau = 0,
    /// <summary>Der Benutzer hat den Dateidialog abgebrochen.</summary>
    Abgebrochen = 1,
    /// <summary>Die Datei steht am genannten Pfad.</summary>
    Geschrieben = 2,
    /// <summary>Beim Erzeugen oder Schreiben ist ein Fehler aufgetreten.</summary>
    Fehler = 3
}

public sealed record ProjektVorschauPdfErgebnis(ProjektVorschauPdfStatus Status, string Pfad, Exception? Fehler)
{
    public static ProjektVorschauPdfErgebnis KeineVorschau { get; } = new(ProjektVorschauPdfStatus.KeineVorschau, string.Empty, null);
    public static ProjektVorschauPdfErgebnis Abgebrochen { get; } = new(ProjektVorschauPdfStatus.Abgebrochen, string.Empty, null);
}

/// <summary>
/// Der eine Weg zum Vorschau-PDF eines Projekts. Nova-Fixwelle F3: Die neue Uebersichtsseite
/// bekommt denselben Knopf wie die klassische Uebersicht — beide rufen diesen UseCase, damit es
/// nicht zwei Fassungen desselben Ablaufs gibt.
///
/// Der Dateiname ist Teil der Fachlichkeit und liegt deshalb hier.
/// </summary>
public static class ProjektVorschauPdfUseCase
{
    public static async Task<ProjektVorschauPdfErgebnis> AusfuehrenAsync(ProjektVorschauPdfActions actions)
    {
        ArgumentNullException.ThrowIfNull(actions);

        var vorschau = actions.BaueVorschau();
        if (vorschau is null)
            return ProjektVorschauPdfErgebnis.KeineVorschau;

        var ziel = actions.FrageZiel(Dateiname(vorschau.Name));
        if (string.IsNullOrWhiteSpace(ziel))
            return ProjektVorschauPdfErgebnis.Abgebrochen;

        try
        {
            var bytes = await actions.BaueBytes(vorschau).ConfigureAwait(false);
            await actions.Schreibe(ziel, bytes).ConfigureAwait(false);
            return new ProjektVorschauPdfErgebnis(ProjektVorschauPdfStatus.Geschrieben, ziel, null);
        }
        catch (Exception ex)
        {
            return new ProjektVorschauPdfErgebnis(ProjektVorschauPdfStatus.Fehler, ziel, ex);
        }
    }

    /// <summary>"Projektvorschau_&lt;Projekt&gt;_20260907.pdf" mit dateisicherem Projektnamen.</summary>
    public static string Dateiname(string? projektName, DateTime? jetzt = null)
    {
        var sicher = Dateiteil(string.IsNullOrWhiteSpace(projektName) ? "Projekt" : projektName);
        return $"Projektvorschau_{sicher}_{(jetzt ?? DateTime.Now):yyyyMMdd}.pdf";
    }

    private static string Dateiteil(string? wert)
    {
        var text = (wert ?? string.Empty).Trim();
        if (text.Length == 0)
            return "Projekt";

        // Leerzeichen bleiben stehen: Der Projektname soll im Dateinamen lesbar bleiben.
        foreach (var zeichen in System.IO.Path.GetInvalidFileNameChars())
            text = text.Replace(zeichen, '_');
        return string.IsNullOrWhiteSpace(text) ? "Projekt" : text;
    }
}
