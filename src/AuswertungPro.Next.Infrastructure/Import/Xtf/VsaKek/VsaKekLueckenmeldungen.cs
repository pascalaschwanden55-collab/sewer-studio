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

        return meldungen;
    }

    private static void Warnung(List<ImportMessage> meldungen, string text)
        => meldungen.Add(new ImportMessage { Level = "Warn", Context = Kontext, Message = text });
}
