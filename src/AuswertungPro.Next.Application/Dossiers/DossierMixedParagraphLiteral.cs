using System;

namespace AuswertungPro.Next.Application.Dossiers;

/// <summary>
/// Die Beschriftung eines Absatzes, in dem auch ein Platzhalter steht.
///
/// In der Vorlage ist „Datum: {{Datum}}" ein einziger Textlauf. Der
/// Textersetzer liess solche Absätze bewusst aus — eine Zeile mit Platzhalter
/// gehörte dem Feld —, und die Vorschau bot sie deshalb gar nicht erst an.
/// Gemessen an der Vorlage betraf das fünf Beschriftungen auf den ersten zwei
/// Seiten. Sie sind der Rest, der von „jeder Text bearbeitbar" noch fehlte.
///
/// Angeboten wird nur der eindeutige Fall: genau ein zusammenhängendes
/// Textstück neben den Platzhaltern. „Von {{A}} bis {{B}}" hätte zwei Stellen,
/// und welche gemeint ist, lässt sich nicht entscheiden — solche Absätze
/// bleiben gesperrt statt zu raten.
/// </summary>
public static class DossierMixedParagraphLiteral
{
    private const string Anfang = "{{";
    private const string Ende = "}}";

    /// <summary>
    /// Die Beschriftung als Schlüssel — oder <c>null</c>, wenn dieser Absatz
    /// keine eindeutige hat. Ohne Platzhalter ist der bisherige Weg zuständig,
    /// der den ganzen Absatz ersetzt.
    /// </summary>
    public static string? Schluessel(string? absatzText)
        => Bereich(absatzText) is { } bereich
            ? absatzText!.Substring(bereich.Start, bereich.Length)
            : null;

    /// <summary>
    /// Wo die Beschriftung im Absatztext steht — bereits ohne die Leerzeichen
    /// zum Platzhalter hin. Sie bleiben stehen, sonst klebte eine eigene
    /// Beschriftung am Wert.
    /// </summary>
    public static (int Start, int Length)? Bereich(string? absatzText)
    {
        if (string.IsNullOrEmpty(absatzText) || !absatzText.Contains(Anfang, StringComparison.Ordinal))
            return null;

        (int Start, int Length)? gefunden = null;
        var stueckStart = 0;
        var i = 0;

        while (i < absatzText.Length)
        {
            var offen = absatzText.IndexOf(Anfang, i, StringComparison.Ordinal);
            if (offen < 0)
                break;

            var zu = absatzText.IndexOf(Ende, offen + Anfang.Length, StringComparison.Ordinal);
            if (zu < 0)
                break;

            if (!Nimm(absatzText, stueckStart, offen, ref gefunden))
                return null;

            i = zu + Ende.Length;
            stueckStart = i;
        }

        return Nimm(absatzText, stueckStart, absatzText.Length, ref gefunden)
            ? gefunden
            : null;
    }

    /// <summary>
    /// Nimmt ein Textstück auf. Falsch, sobald es ein zweites gäbe — dann ist
    /// der Absatz nicht eindeutig und wird gar nicht angeboten.
    /// </summary>
    private static bool Nimm(
        string text,
        int von,
        int bis,
        ref (int Start, int Length)? gefunden)
    {
        var start = von;
        var ende = bis;

        while (start < ende && char.IsWhiteSpace(text[start]))
            start++;

        while (ende > start && char.IsWhiteSpace(text[ende - 1]))
            ende--;

        if (ende <= start)
            return true;

        if (gefunden is not null)
            return false;

        gefunden = (start, ende - start);
        return true;
    }
}
