using System;

namespace AuswertungPro.Next.Application.Dossiers;

/// <summary>
/// Die Thementitel, an denen im Programm etwas haengt.
///
/// Sie standen als Zeichenketten an drei Stellen: in der Umstellung, in der
/// Bearbeitungsregel und im Zusammensetzer der Bauteilliste. Wer einen Titel
/// umbenennt, brach damit stillschweigend die Verknuepfung — die Liste blieb
/// dann einfach aus, ohne Meldung.
///
/// Die zwei Mengen unten sind bewusst VERSCHIEDEN und keine Kopie voneinander:
/// <list type="bullet">
/// <item><see cref="WithComponentButton"/> — hier bietet der Editor den Knopf
/// zum Einfuegen an. Auch bei der Kostenschaetzung, wo man die Liste von Hand
/// setzen darf.</item>
/// <item><see cref="WithComponentImport"/> — hier kopiert ein ausdruecklicher
/// Importknopf die ganze Liste als frei bearbeitbaren Text. Die
/// Kostenschaetzung gehoert nicht dazu.</item>
/// </list>
/// </summary>
public static class DossierTopicTitles
{
    public const string Schaeden = "Schäden";
    public const string Sanierungskonzept = "Sanierungskonzept";
    public const string Kostenschaetzung = "Kostenschätzung";

    /// <summary>Themen mit Einfuegeknopf fuer die Bauteilliste.</summary>
    public static readonly string[] WithComponentButton =
        [Schaeden, Sanierungskonzept, Kostenschaetzung];

    /// <summary>Themen mit ausdruecklichem Import der ganzen Bauteilliste.</summary>
    public static readonly string[] WithComponentImport =
        [Schaeden, Sanierungskonzept];

    /// <summary>
    /// Wahr, wenn der Titel mit einem der Namen beginnt. Verglichen wird der
    /// Anfang, damit „Schäden Pz. 30" ebenso zaehlt wie „Schäden".
    /// </summary>
    public static bool Matches(string[] titles, string? title)
    {
        ArgumentNullException.ThrowIfNull(titles);

        var titel = (title ?? string.Empty).Trim();
        if (titel.Length == 0)
            return false;

        return Array.Exists(titles,
            kandidat => titel.StartsWith(kandidat, StringComparison.OrdinalIgnoreCase));
    }
}
