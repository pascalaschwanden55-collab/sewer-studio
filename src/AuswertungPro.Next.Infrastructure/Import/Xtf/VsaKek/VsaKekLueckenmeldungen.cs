using AuswertungPro.Next.Infrastructure.Import.Common;

namespace AuswertungPro.Next.Infrastructure.Import.Xtf.VsaKek;

/// <summary>
/// Meldet, was <see cref="VsaKekBeziehungen"/> keiner Haltung und keinem Schacht zuordnen
/// konnte. Bis 01.10.2026 stand das nur im Zwischenergebnis (<see cref="VsaKekBezuege"/>)
/// und fiel im Importbericht still weg (Befunde 5 und 6 aus AP08). Uebernommen wird
/// davon weiterhin nichts, und kein Zaehler aendert sich — es wird nur gemeldet.
/// </summary>
internal static class VsaKekLueckenmeldungen
{
    private const string Kontext = "XTF";

    public static List<ImportMessage> Erzeuge(VsaKekBezuege bezuege)
    {
        ArgumentNullException.ThrowIfNull(bezuege);

        var meldungen = new List<ImportMessage>();

        // Ohne Bezeichnung laesst sich eine Untersuchung keinem Bauwerk zuordnen. Sie
        // zaehlt in «N Untersuchungen gelesen», erschien bisher aber sonst nirgends.
        foreach (var u in bezuege.OhneBezeichnung)
            Warnung(meldungen, $"Untersuchung ohne Bezeichnung (TID {u.Tid}) nicht übernommen: "
                               + "Sie lässt sich keiner Haltung und keinem Schacht zuordnen.");

        // Verwaist: kein gueltiger Bezug auf eine Untersuchung bzw. einen Kanalschaden.
        Melde(meldungen, "Kanalschäden", "Untersuchungsverweis fehlt oder zeigt ins Leere",
            bezuege.VerwaisteKanalschaeden.Select(k => new Verwaist(
                Kennung(k.Tid),
                $"Kanalschaden (TID {Kennung(k.Tid)}, Code {k.Werte.Schadencode.Trim()}) nicht übernommen: "
                + Verweis("Untersuchungsverweis", k.UntersuchungRef))));

        Melde(meldungen, "Normschachtschäden", "Untersuchungsverweis fehlt oder zeigt ins Leere",
            bezuege.VerwaisteNormschachtschaeden.Select(n => new Verwaist(
                Kennung(n.Tid),
                $"Normschachtschaden (TID {Kennung(n.Tid)}, Code {n.Werte.Schadencode.Trim()}) nicht übernommen: "
                + Verweis("Untersuchungsverweis", n.UntersuchungRef))));

        Melde(meldungen, "Dateien", "Verweis auf Kanalschaden oder Untersuchung fehlt oder zeigt ins Leere",
            bezuege.FotosOhneBefund.Select(d => Datei(d, "Kanalschadenverweis"))
                .Concat(bezuege.VideosOhneUntersuchung.Select(d => Datei(d, "Untersuchungsverweis"))));

        return meldungen;
    }

    /// <summary>Bis zu so vielen Objekten je eine Zeile, darueber eine gebuendelte.</summary>
    private const int EinzelnBis = 10;

    private sealed record Verwaist(string Kennung, string Einzelmeldung);

    /// <summary>
    /// Je Objekt eine Warnung; bei sehr vielen eine einzige mit den ersten Kennungen,
    /// damit der Bericht lesbar bleibt («n verwaiste …: a, b, … (+k weitere)»).
    /// </summary>
    private static void Melde(List<ImportMessage> meldungen, string art, string grund, IEnumerable<Verwaist> objekte)
    {
        var liste = objekte.ToList();
        if (liste.Count <= EinzelnBis)
        {
            foreach (var objekt in liste)
                Warnung(meldungen, objekt.Einzelmeldung);
            return;
        }

        var rest = liste.Count - EinzelnBis;
        Warnung(meldungen, $"{liste.Count} verwaiste {art} nicht übernommen ({grund}): "
                           + string.Join(", ", liste.Take(EinzelnBis).Select(o => o.Kennung))
                           + $" (+{rest} weitere).");
    }

    private static Verwaist Datei(VsaKekDatei d, string verweisart)
    {
        var kennung = string.IsNullOrWhiteSpace(d.Tid) ? d.Bezeichnung : d.Tid;
        return new Verwaist(kennung,
            $"Datei \"{d.Bezeichnung}\" (TID {Kennung(d.Tid)}) nicht übernommen: " + Verweis(verweisart, d.Objekt));
    }

    private static string Verweis(string verweisart, string? ziel)
        => string.IsNullOrWhiteSpace(ziel) ? $"{verweisart} fehlt." : $"{verweisart} {ziel} zeigt ins Leere.";

    private static string Kennung(string? tid) => string.IsNullOrWhiteSpace(tid) ? "ohne TID" : tid;

    private static void Warnung(List<ImportMessage> meldungen, string text)
        => meldungen.Add(new ImportMessage { Level = "Warn", Context = Kontext, Message = text });
}
