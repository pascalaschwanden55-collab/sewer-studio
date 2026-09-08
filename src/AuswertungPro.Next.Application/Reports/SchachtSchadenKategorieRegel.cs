namespace AuswertungPro.Next.Application.Reports;

/// <summary>
/// Leitet die Schadenssymbol-Kategorie eines Schacht-Protokolleintrags ab (fuer
/// <see cref="DamageSymbolRenderer"/>).
///
/// Ein VSA-Code (etwa aus dem VSA-KEK-XTF-Import) geht ueber
/// <see cref="DamageSymbolClassifier.ResolveDamageSymbolCategory"/>. Der PDF-Schachtprotokoll-
/// import (<c>SchachtProtocolApplier</c>) traegt dort aber keinen VSA-Code, sondern den
/// BAUTEILNAMEN ("Konus", "Bankett") als <c>Code</c> — <see cref="DamageSymbolClassifier"/>
/// faellt bei einem Bauteilnamen immer auf die generische Kategorie ("default") zurueck, und
/// ohne diese Regel zeigten alle Schaeden desselben Schachts dasselbe Diamant-Symbol
/// (Review-Befund: Riss, schadhafter Anschluss und Ablagerung sahen im Bild gleich aus).
///
/// Diese Regel versucht deshalb ZUSAETZLICH eine Kategorie aus dem freien Schadenstext
/// (<c>ProtocolEntry.Beschreibung</c>) abzuleiten — aber NUR fuer die geschlossene, aus
/// <c>SchachtProtocolParser.GetDamageCandidatesForComponent</c> bekannte Wortliste dieses
/// Importwegs.
///
/// GRENZE (bewusst, kein stilles Wissen): Ein Schadenstext ausserhalb dieser Liste (freier
/// Handtext, ein neues Formular) bleibt generisch — eine geratene Kategorie waere ein
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
    /// <summary>
    /// Bestimmt die Symbolkategorie. Ein VSA-Code hat Vorrang (liefert er eine echte Kategorie,
    /// also nicht "default"); erst wenn der Code keine bekannte Kategorie ergibt, greift der
    /// Text-Fallback aus <paramref name="beschreibung"/>.
    /// </summary>
    public static string Bestimme(string? code, string? beschreibung)
    {
        var ausCode = DamageSymbolClassifier.ResolveDamageSymbolCategory(code);
        if (ausCode != "default")
            return ausCode;

        return AusBeschreibung(beschreibung) ?? "default";
    }

    /// <summary>
    /// Kategorie aus dem freien Schadenstext des PDF-Schachtprotokollimports. Nur die dort
    /// bekannten, eindeutig zuordenbaren Formulierungen werden gedeutet; alles andere bleibt
    /// <c>null</c> (generisch) — siehe Klassendokumentation.
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

    private static bool Enthaelt(string text, string suchbegriff)
        => text.Contains(suchbegriff, StringComparison.OrdinalIgnoreCase);
}
