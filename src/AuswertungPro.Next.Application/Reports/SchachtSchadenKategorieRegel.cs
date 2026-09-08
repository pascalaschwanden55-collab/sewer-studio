using System.Text.RegularExpressions;

namespace AuswertungPro.Next.Application.Reports;

/// <summary>
/// Leitet die Schadenssymbol-Kategorie eines Schacht-Protokolleintrags ab (fuer
/// <see cref="DamageSymbolRenderer"/>).
///
/// Was der Code tut: <see cref="Bestimme"/> nimmt zuerst
/// <see cref="DamageSymbolClassifier.ResolveDamageSymbolCategory"/> (VSA-Codepraefix, z.B.
/// "BAB" -> "crack"). Liefert das eine echte Kategorie, gilt sie — der Text-Fallback wird dann
/// GAR NICHT ausgefuehrt. Nur wenn der Code KEINE VSA-Kategorie ergibt UND selbst einer der
/// bekannten Bauteilnamen aus <see cref="SchachtBauteilNamen"/> ist (also erkennbar aus dem
/// PDF-Schachtprotokollimport stammt, der dort den Bauteilnamen statt eines VSA-Codes
/// eintraegt — z.B. "Konus", "Bankett"), wird zusaetzlich <paramref name="beschreibung"/> nach
/// einer geschlossenen Liste bekannter Schadensformulierungen durchsucht
/// (<c>SchachtProtocolParser.GetDamageCandidatesForComponent</c>). Jeder Treffer verlangt eine
/// echte Wortgrenze VOR dem Begriff (kein Treffer mitten in einem fremden Wort, z.B. waere
/// "Xriss" kein Riss) — bewusst OHNE feste Grenze danach, weil deutsche Pluralformen
/// ("Ablagerung"/"Ablagerungen") ihre Endung direkt an den Stamm haengen. Zusaetzlich darf der
/// Begriff nicht unmittelbar von "kein"/"keine"/"nicht"/"ohne" eingeleitet sein (sonst waere
/// "kein Riss festgestellt" faelschlich "crack"). Bei einem beliebigen anderen Code (freier
/// Text, unbekannter Wert) bleibt es bei "default" — der Text-Fallback greift dann gar nicht,
/// selbst wenn die Beschreibung zufaellig ein bekanntes Wort enthaelt.
///
/// GRENZE (bewusst, kein stilles Wissen): Ein Schadenstext ausserhalb der bekannten Liste
/// (freier Handtext, ein neues Formular) bleibt generisch — eine geratene Kategorie waere ein
/// erfundenes fachliches Urteil. Auch innerhalb der bekannten Liste bleiben Woerter ohne
/// eindeutige Entsprechung in <see cref="DamageSymbolClassifier"/> bewusst generisch:
/// "klemmt" (Deckel klemmt), "Überdeckt"/"Ueberdeckt" (Schacht verdeckt/nicht auffindbar),
/// "fehlt"/"zu kurz" (Steigeisen/Tauchbogen), "defekt" (zu unspezifisch), "Fugen mangelhaft
/// verputzt" und "Mangelhaft ausgebildet" (keine eindeutige Struktur-/Oberflaechen-/
/// Betriebszuordnung). Diese Liste ist damit KEINE vollstaendige Bauteil-Schadenslogik,
/// sondern nur die sicher zuordenbare Teilmenge.
/// </summary>
public static class SchachtSchadenKategorieRegel
{
    private static readonly string[] Negationen = ["kein", "keine", "nicht", "ohne"];

    /// <summary>
    /// Bestimmt die Symbolkategorie. Ein VSA-Code hat Vorrang (liefert er eine echte Kategorie,
    /// also nicht "default"); erst wenn der Code keine bekannte Kategorie ergibt UND selbst ein
    /// bekannter Bauteilname des PDF-Schachtprotokollimports ist, greift der Text-Fallback aus
    /// <paramref name="beschreibung"/>.
    /// </summary>
    public static string Bestimme(string? code, string? beschreibung)
    {
        var ausCode = DamageSymbolClassifier.ResolveDamageSymbolCategory(code);
        if (ausCode != "default")
            return ausCode;

        if (!SchachtBauteilNamen.IstBekannt(code))
            return "default";

        return AusBeschreibung(beschreibung) ?? "default";
    }

    /// <summary>
    /// Kategorie aus dem freien Schadenstext des PDF-Schachtprotokollimports. Nur die dort
    /// bekannten, eindeutig zuordenbaren Formulierungen werden gedeutet — mit Wortgrenze und
    /// Negationswaechter; alles andere bleibt <c>null</c> (generisch) — siehe Klassendokumentation.
    /// </summary>
    private static string? AusBeschreibung(string? text)
    {
        var wert = (text ?? string.Empty).Trim();
        if (wert.Length == 0)
            return null;

        if (Enthaelt(wert, "riss") || Enthaelt(wert, "gerissen"))
            return "crack";
        if (Enthaelt(wert, "ausgebrochen"))
            return "break";
        if (Enthaelt(wert, "infiltration"))
            return "infiltration";
        if (Enthaelt(wert, "verkalkung"))
            return "incrustation";
        if (Enthaelt(wert, "ablagerung"))
            return "deposit";
        if (Enthaelt(wert, "mangelhaft eingebunden"))
            return "offset";
        if (Enthaelt(wert, "korrodiert") || Enthaelt(wert, "verrostet"))
            return "surface";
        if (Enthaelt(wert, "lose"))
            return "offset";

        return null;
    }

    /// <summary>
    /// True, wenn <paramref name="begriff"/> mit einer echten Wortgrenze VOR sich (kein
    /// Teiltreffer mitten in einem fremden Wort, Pluralformen danach bleiben aber erlaubt) in
    /// <paramref name="text"/> vorkommt UND nicht unmittelbar (durch Leerraum getrennt) von
    /// "kein"/"keine"/"nicht"/"ohne" eingeleitet wird.
    /// </summary>
    private static bool Enthaelt(string text, string begriff)
    {
        // \b VOR dem Begriff verhindert einen Treffer mitten in einem fremden Wort (z.B. "Xriss"
        // waere kein Riss). KEINE Wortgrenze DANACH: Die Kandidatenliste des PDF-Imports fuehrt
        // Formen wie "Ablagerung" und "Ablagerungen" nebeneinander — ein deutsches Wort haengt
        // seine Endung direkt an den Wortstamm, eine strenge Endgrenze wuerde genau diese
        // Pluralform wieder verwerfen. Das vorangestellte negative Lookbehind lehnt einen Treffer
        // ab, dem unmittelbar (durch Leerraum getrennt) "kein"/"keine"/"nicht"/"ohne" vorausgeht.
        var negationsPraefix = string.Join("|", Negationen);
        var muster = $@"(?<!\b(?:{negationsPraefix})\s+)\b{Regex.Escape(begriff)}";
        return Regex.IsMatch(text, muster, RegexOptions.IgnoreCase);
    }
}
