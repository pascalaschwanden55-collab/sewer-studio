namespace AuswertungPro.Next.Infrastructure.Import.Xtf.Sia405;

/// <summary>Wie eine Haltung zu ihrem Kanal gefunden wurde.</summary>
internal enum Sia405KanalBezug
{
    /// <summary>Kein Kanal: Verweis fehlt oder zeigt ins Leere, und kein Kanal traegt denselben Namen.</summary>
    Fehlt = 0,

    /// <summary>Ueber <c>AbwasserbauwerkRef</c>.</summary>
    Verweis = 1,

    /// <summary>Ohne gueltigen Verweis ueber die gleiche Bezeichnung.</summary>
    Bezeichnung = 2
}

/// <summary>
/// Eine Haltung mit aufgeloesten Bezuegen. Was sich nicht aufloesen liess, bleibt hier
/// sichtbar leer (<c>null</c> beziehungsweise <see cref="Sia405KanalBezug.Fehlt"/>) und
/// wird nicht durch einen geratenen Wert ersetzt.
/// </summary>
internal sealed record Sia405HaltungMitBezuegen(
    Sia405HaltungObjekt Haltung,
    string Haltungsname,
    Sia405KanalObjekt? Kanal,
    Sia405KanalBezug KanalBezug,
    Sia405Rohrprofil? Rohrprofil,
    bool RohrprofilVerweisOhneZiel,
    string? EigentuemerAusVerweis,
    string? Datenherr,
    string? Datenlieferant,
    string? SchachtOben,
    string? SchachtUnten);

/// <summary>
/// Schritt 2 des SIA405-Haltungsimports: loest die Verweise einer Haltung gegen den
/// gelesenen <see cref="Sia405Bestand"/> auf — Kanal, Rohrprofil, Organisationen und die
/// Schachtnamen an beiden Enden. Keine Feldwerte deuten, nichts ins Projekt schreiben;
/// das macht <c>Sia405HaltungAbbildung</c>.
/// </summary>
internal static class Sia405Beziehungen
{
    /// <summary>
    /// Alle Haltungen in Dateireihenfolge. Eine Haltung ohne eigenen Namen und ohne Kanal
    /// mit Namen faellt heute still weg; sie liesse sich im Projekt keiner Zeile zuordnen.
    /// </summary>
    public static List<Sia405HaltungMitBezuegen> Loese(Sia405Bestand bestand)
    {
        ArgumentNullException.ThrowIfNull(bestand);

        var ergebnis = new List<Sia405HaltungMitBezuegen>();
        foreach (var haltung in bestand.Haltungen.Values)
        {
            var (kanal, kanalBezug) = FindeKanal(bestand, haltung);

            var haltungsname = !string.IsNullOrWhiteSpace(haltung.Bezeichnung) ? haltung.Bezeichnung : (kanal?.Bezeichnung ?? "");
            if (string.IsNullOrWhiteSpace(haltungsname))
                continue;

            Sia405Rohrprofil? rohrprofil = null;
            var rohrprofilOhneZiel = false;
            if (!string.IsNullOrWhiteSpace(haltung.RohrprofilRef))
            {
                if (bestand.Rohrprofile.TryGetValue(haltung.RohrprofilRef, out var profil))
                    rohrprofil = profil;
                else
                    rohrprofilOhneZiel = true;
            }

            // Die Organisationen haengen am Kanal. Ohne Kanal gibt es keine.
            string? eigentuemer = null, datenherr = null, datenlieferant = null;
            if (kanal is not null)
            {
                eigentuemer = Organisation(bestand, kanal.EigentuemerRef);
                datenherr = Organisation(bestand, kanal.DatenherrRef);
                datenlieferant = Organisation(bestand, kanal.DatenlieferantRef);
            }

            ergebnis.Add(new Sia405HaltungMitBezuegen(
                haltung,
                haltungsname,
                kanal,
                kanalBezug,
                rohrprofil,
                rohrprofilOhneZiel,
                eigentuemer,
                datenherr,
                datenlieferant,
                SchachtName(bestand, haltung.VonRef, haltungsname, oben: true),
                SchachtName(bestand, haltung.NachRef, haltungsname, oben: false)));
        }

        return ergebnis;
    }

    private static (Sia405KanalObjekt? Kanal, Sia405KanalBezug Bezug) FindeKanal(Sia405Bestand bestand, Sia405HaltungObjekt haltung)
    {
        if (!string.IsNullOrWhiteSpace(haltung.KanalRef) && bestand.Kanaele.TryGetValue(haltung.KanalRef, out var ueberVerweis))
            return (ueberVerweis, Sia405KanalBezug.Verweis);
        if (!string.IsNullOrWhiteSpace(haltung.Bezeichnung) && bestand.KanaeleNachBezeichnung.TryGetValue(haltung.Bezeichnung, out var ueberName))
            return (ueberName, Sia405KanalBezug.Bezeichnung);
        return (null, Sia405KanalBezug.Fehlt);
    }

    /// <summary>Die Bezeichnung der verwiesenen Organisation, oder <c>null</c>.</summary>
    private static string? Organisation(Sia405Bestand bestand, string? kennung)
        => bestand.Organisationen.TryGetValue(kennung ?? "", out var name) ? name : null;

    /// <summary>
    /// Der Schachtname an einem Haltungsende. Der Abwasserknoten IST der Schacht; die
    /// Bezeichnung des Haltungspunkts ist nur ein technischer Name. Im Kantonsexport
    /// heisst er "u-80401_von", im SewerStudio-Export "&lt;Haltung&gt;_von" — beides sind
    /// keine Schachtnummern. Die umgekehrte Reihenfolge schrieb genau diese Namen in
    /// "Schacht_oben".
    /// </summary>
    private static string? SchachtName(Sia405Bestand bestand, string? punktKennung, string haltungsname, bool oben)
    {
        if (string.IsNullOrWhiteSpace(punktKennung)) return null;
        if (!bestand.Haltungspunkte.TryGetValue(punktKennung, out var punkt)) return null;

        if (!string.IsNullOrWhiteSpace(punkt.AbwassernetzelementRef)
            && bestand.Abwasserknoten.TryGetValue(punkt.AbwassernetzelementRef, out var knoten))
            return knoten;

        // Ohne Knoten: Heisst der Punkt "<Haltung>_von" / "<Haltung>_nach" und der
        // Haltungsname ist "oben-unten", dann steckt der Schacht im Haltungsnamen.
        // So kam beim Rueckimport eines SewerStudio-Exports "78998-79002_nach" in
        // "Schacht_unten" an, obwohl "79002" gemeint war.
        var ausHaltung = SchachtAusHaltungsname(punkt.Bezeichnung, haltungsname, oben);
        if (ausHaltung is not null) return ausHaltung;

        return string.IsNullOrWhiteSpace(punkt.Bezeichnung) ? null : punkt.Bezeichnung;
    }

    private static string? SchachtAusHaltungsname(string? punktname, string haltungsname, bool oben)
    {
        var punkt = (punktname ?? "").Trim();
        var name = (haltungsname ?? "").Trim();
        var erwartet = name + (oben ? "_von" : "_nach");
        if (name.Length == 0 || !string.Equals(punkt, erwartet, StringComparison.OrdinalIgnoreCase))
            return null;

        var trenner = name.IndexOf('-');
        if (trenner <= 0 || trenner >= name.Length - 1 || name.IndexOf('-', trenner + 1) >= 0)
            return null;

        return oben ? name[..trenner] : name[(trenner + 1)..];
    }
}
