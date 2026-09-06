using System;

namespace AuswertungPro.Next.Application.UseCases.Import.Quellen;

/// <summary>Welches Bauwerk eine VSA-KEK-Untersuchung beschreibt.</summary>
public enum VsaKekBauteilart
{
    /// <summary>Kein ausreichender Beleg oder widerspruechliche Belege.</summary>
    Unklar = 0,

    /// <summary>Kanalfernsehen einer Haltung.</summary>
    Haltung = 1,

    /// <summary>Begehung eines Schachts.</summary>
    Schacht = 2
}

/// <summary>
/// Die Belege einer einzelnen Untersuchung. Reine Zahlen und Texte — das Lesen der
/// Datei bleibt in der Infrastruktur.
/// </summary>
/// <param name="HatVonBisPunkt">Traegt die Untersuchung <c>vonPunktBezeichnung</c> oder <c>bisPunktBezeichnung</c>?</param>
/// <param name="Erfassungsart">Rohwert aus der XTF, z.B. <c>Kanalfernsehen</c> oder <c>Begehung</c>.</param>
/// <param name="Kanalschaeden">Anzahl zugeordneter <c>Kanalschaden</c>-Elemente.</param>
/// <param name="Normschachtschaeden">Anzahl zugeordneter <c>Normschachtschaden</c>-Elemente.</param>
/// <param name="Bauwerksverweis">
/// Wohin <c>AbwasserbauwerkRef</c> zeigt, falls das Objekt in derselben Datei steht.
/// <see cref="VsaKekBauteilart.Unklar"/> heisst "nicht aufloesbar" — der haeufige Fall,
/// weil WinCan-Exporte die Bauwerke selbst meist nicht mitliefern.
/// </param>
public sealed record VsaKekUntersuchungsBelege(
    bool HatVonBisPunkt,
    string? Erfassungsart,
    int Kanalschaeden,
    int Normschachtschaeden,
    VsaKekBauteilart Bauwerksverweis = VsaKekBauteilart.Unklar)
{
    /// <summary>
    /// Die Bezeichnung der Untersuchung, z.B. <c>327015-2414</c> oder <c>3133</c>.
    /// Wird nur als letzter Rueckfall verwendet — siehe <see cref="VsaKekUntersuchungsart"/>.
    /// </summary>
    public string Bezeichnung { get; init; } = "";
}

/// <summary>Ergebnis samt Begruendung fuer den Importbericht.</summary>
public sealed record VsaKekArtErgebnis(VsaKekBauteilart Art, string Grund);

/// <summary>
/// Entscheidet, ob eine VSA-KEK-Untersuchung eine Haltung oder einen Schacht beschreibt.
///
/// Anlass (Audit 2026-09-05, Andermatt Zone 2.11): Der Leser machte aus JEDER
/// Untersuchung eine Haltung. Die Datei enthaelt 23 Kanalfernseh-Untersuchungen und 24
/// Schachtbegehungen — Schacht 3133 landete mit acht Schachtschaeden in der
/// Haltungsliste.
///
/// Die Datei sagt es an drei Stellen, und diese drei muessen zusammenpassen:
///
/// 1. <b>Bauwerksverweis</b> — steht das Objekt in derselben Datei, ist es die Wahrheit.
/// 2. <b>Schadensart</b> — <c>Kanalschaden</c> gehoert an eine Haltung,
///    <c>Normschachtschaden</c> an einen Schacht.
/// 3. <b>Form der Untersuchung</b> — eine Haltung verbindet zwei Punkte
///    (<c>vonPunkt</c>/<c>bisPunkt</c>); ein Schacht hat keine. Die
///    <c>Erfassungsart</c> unterscheidet Kanalfernsehen von Begehung.
///
/// Widersprechen sich die Belege, bleibt der Fall ausdruecklich offen. Raten waere hier
/// schlimmer als eine Luecke: Ein falsch angelegtes Bauwerk zieht Ordner, Protokolle und
/// spaeter einen Katastereintrag hinter sich her.
///
/// Wichtig: "Keine Schaeden" ist KEIN Beleg gegen ein Bauwerk. Ein schadenfreier Schacht
/// bleibt ein Schacht — dafuer sorgt Beleg 3.
///
/// Reine Rechnung: kein Datei-, Netz- oder UI-Zugriff.
/// </summary>
public static class VsaKekUntersuchungsart
{
    public static VsaKekArtErgebnis Bestimme(VsaKekUntersuchungsBelege belege)
    {
        ArgumentNullException.ThrowIfNull(belege);

        var fuerHaltung = false;
        var fuerSchacht = false;
        var gruende = new System.Collections.Generic.List<string>();

        if (belege.Bauwerksverweis == VsaKekBauteilart.Haltung)
        {
            fuerHaltung = true;
            gruende.Add("Bauwerksverweis zeigt auf einen Kanal/eine Haltung");
        }
        else if (belege.Bauwerksverweis == VsaKekBauteilart.Schacht)
        {
            fuerSchacht = true;
            gruende.Add("Bauwerksverweis zeigt auf einen Normschacht");
        }

        if (belege.Kanalschaeden > 0)
        {
            fuerHaltung = true;
            gruende.Add($"{belege.Kanalschaeden} Kanalschaden/-schaeden");
        }

        if (belege.Normschachtschaeden > 0)
        {
            fuerSchacht = true;
            gruende.Add($"{belege.Normschachtschaeden} Normschachtschaden/-schaeden");
        }

        if (belege.HatVonBisPunkt)
        {
            fuerHaltung = true;
            gruende.Add("von-/bisPunkt vorhanden");
        }

        var erfassung = (belege.Erfassungsart ?? "").Trim();
        if (IstHaltungsErfassung(erfassung))
        {
            fuerHaltung = true;
            gruende.Add($"Erfassungsart {erfassung}");
        }
        else if (IstSchachtErfassung(erfassung))
        {
            fuerSchacht = true;
            gruende.Add($"Erfassungsart {erfassung}");
        }

        // Letzter Rueckfall, und nur wenn sonst gar nichts spricht: Ein Haltungsname
        // nennt zwei Schaechte ("327015-2414"), ein Schachtname ist eine einzelne
        // Nummer ("3133").
        //
        // Warum ueberhaupt? Eine IKAS-Untersuchung kann nur Bezeichnung, Zeitpunkt und
        // eine Videodatei fuehren — ohne Schaeden, ohne Punkte, ohne Erfassungsart.
        // Bis 2026-09-05 wurde daraus immer eine Haltung; sie darf jetzt nicht
        // wegfallen. Die Regel kann also nur weniger raten als vorher, nie mehr.
        //
        // Umgekehrt gilt sie NICHT: Eine einzelne Nummer ist KEIN Beleg fuer einen
        // Schacht. Genau so bleibt der reale Fall "2204" aus Zone 2.11 offen —
        // eine Untersuchung ohne Erfassungsart, ohne Punkte, ohne Schaeden und mit
        // Platzhalterdatum.
        if (!fuerHaltung && !fuerSchacht && NenntZweiPunkte(belege.Bezeichnung))
        {
            return new VsaKekArtErgebnis(
                VsaKekBauteilart.Haltung,
                $"Bezeichnung \"{belege.Bezeichnung.Trim()}\" nennt zwei Schaechte (letzter Rueckfall)");
        }

        if (fuerHaltung && fuerSchacht)
        {
            return new VsaKekArtErgebnis(
                VsaKekBauteilart.Unklar,
                "widerspruechliche Belege: " + string.Join(", ", gruende));
        }

        if (fuerHaltung)
            return new VsaKekArtErgebnis(VsaKekBauteilart.Haltung, string.Join(", ", gruende));

        if (fuerSchacht)
            return new VsaKekArtErgebnis(VsaKekBauteilart.Schacht, string.Join(", ", gruende));

        return new VsaKekArtErgebnis(
            VsaKekBauteilart.Unklar,
            "kein Beleg: weder Bauwerksverweis noch Schaeden, Punkte oder bekannte Erfassungsart");
    }

    /// <summary>
    /// Erfassungsarten, die von innen im Rohr aufnehmen. <c>Spiegelung</c> und
    /// <c>Sonar</c> stehen bewusst NICHT hier: Sie kommen im Bestand nicht vor, und ein
    /// geratener Wert waere schlimmer als ein offener Fall.
    /// </summary>
    private static bool IstHaltungsErfassung(string wert)
        => wert.Equals("Kanalfernsehen", StringComparison.OrdinalIgnoreCase);

    private static bool IstSchachtErfassung(string wert)
        => wert.Equals("Begehung", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Nennt die Bezeichnung zwei durch Bindestrich getrennte Punkte, beide nicht leer?
    /// </summary>
    private static bool NenntZweiPunkte(string? bezeichnung)
    {
        var text = (bezeichnung ?? "").Trim();
        var trenner = text.IndexOf('-', StringComparison.Ordinal);
        return trenner > 0
               && trenner < text.Length - 1
               && text[(trenner + 1)..].Trim().Length > 0;
    }
}
