using System.Globalization;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.Schacht;

/// <summary>
/// Die eine Regel fuer die Schachtmasse. Sie leben in genau zwei Zahlenfeldern,
/// <see cref="FieldKeys.ShaftDimension1Mm"/> und <see cref="FieldKeys.ShaftDimension2Mm"/>,
/// beide in Millimetern. Rund heisst beide gleich (600 / 600), oval oder eckig heisst zwei
/// verschiedene Werte (1100 / 900). So fuehrt es SIA405 am <c>Normschacht</c>, und so gibt
/// es der Benutzer seit 2026-09-03 auch ein.
///
/// Das aeltere Textfeld <c>Dimension</c> ("600 mm", "1100 x 900 mm") und das von WinCan
/// und SchachtPro geschriebene <c>Durchmesser</c> sind abgeloest: Jeder Import schreibt
/// die zwei Zahlen, und ein Bestandsprojekt wird beim Laden umgestellt. Der Anlass war
/// messbar: Im Bestand trugen 61 Schaechte nur den Text und 2 die Zahlen, weil alle
/// Importe den Text schrieben und nur Handeingabe und QGIS-Nachfuellen die Zahlen.
/// Export und Anzeige zeigten dadurch verschiedene Werte fuer denselben Schacht.
///
/// Reine Feldlogik ohne Dateizugriff und ohne UI.
/// </summary>
public static class SchachtMasse
{
    /// <summary>Die abgeloesten Textfelder, in der Reihenfolge ihres Vorrangs.</summary>
    public static readonly IReadOnlyList<string> AlteTextfelder = ["Dimension", "Durchmesser"];

    /// <summary>
    /// Zwei Masse aus einem Text, wie ihn PDF-, WinCan- und SchachtPro-Import lieferten:
    /// "600 mm", "1100 x 900 mm", "800", "0.60/1.00". Ein Wert allein gilt fuer beide
    /// Richtungen (rund). <c>null</c> heisst: keine brauchbare Angabe.
    /// </summary>
    public static (string Dimension1, string Dimension2)? Lies(string? text)
    {
        var (erstes, zweites) = SiaAbmessung.NachMillimeterPaar(text);
        return Paar(erstes, zweites);
    }

    /// <summary>
    /// Zwei Masse aus zwei Rohwerten, etwa <c>Dimension1</c>/<c>Dimension2</c> der XTF
    /// oder <c>ns_dimension1</c>/<c>ns_dimension2</c> der QGIS-Kopie. Fehlt eines, gilt
    /// das vorhandene fuer beide, so schreibt es auch der Kantonsexport.
    /// </summary>
    public static (string Dimension1, string Dimension2)? AusZwei(string? erstes, string? zweites)
        => Paar(SiaAbmessung.NachMillimeter(erstes), SiaAbmessung.NachMillimeter(zweites));

    /// <summary>
    /// Schreibt ein Paar in die zwei Felder des Datensatzes, unter der Schreibweise, die
    /// der Datensatz bereits fuehrt. Eine Handeingabe bleibt dabei stehen; mit
    /// <paramref name="nurLeere"/> werden nur leere Felder gefuellt.
    /// </summary>
    /// <returns>True, wenn mindestens ein Feld tatsaechlich geschrieben wurde.</returns>
    public static bool Schreibe(
        SchachtRecord record,
        (string Dimension1, string Dimension2)? masse,
        FieldSource source,
        bool userEdited,
        bool nurLeere = false)
    {
        ArgumentNullException.ThrowIfNull(record);
        if (masse is null)
            return false;

        var eins = SchachtFeldnamen.Feld(record, FieldKeys.ShaftDimension1Mm);
        var zwei = SchachtFeldnamen.Feld(record, FieldKeys.ShaftDimension2Mm);

        if (nurLeere)
        {
            // Beide oder keines: Ein halbes Paar waere eine falsche Aussage ueber die Form.
            if (!string.IsNullOrWhiteSpace(record.GetFieldValue(eins))
                || !string.IsNullOrWhiteSpace(record.GetFieldValue(zwei)))
            {
                return false;
            }

            var a = record.FuelleLeeresFeld(eins, masse.Value.Dimension1, source);
            var b = record.FuelleLeeresFeld(zwei, masse.Value.Dimension2, source);
            return a || b;
        }

        var ergebnis1 = record.SetFieldValue(eins, masse.Value.Dimension1, source, userEdited);
        var ergebnis2 = record.SetFieldValue(zwei, masse.Value.Dimension2, source, userEdited);
        return ergebnis1 == FeldSchreibErgebnis.Geschrieben || ergebnis2 == FeldSchreibErgebnis.Geschrieben;
    }

    /// <summary>
    /// Uebernimmt die alten Textfelder eines Bestandsdatensatzes in die zwei Zahlenfelder
    /// und entfernt sie danach. Sind die Zahlenfelder schon gefuellt, gewinnen sie; der
    /// Text wird dann nur entfernt. Ein Text, der sich nicht lesen laesst, bleibt stehen:
    /// Er verschwindet nicht still, sondern bleibt sichtbar, bis jemand ihn deutet.
    ///
    /// Die Herkunft wandert mit: Eine Handeingabe im Text bleibt eine Handeingabe in den
    /// Zahlen, ein Importwert bleibt ein Importwert. Sonst wuerde die XTF-Revision
    /// ploetzlich Felder schreiben, die der Mensch nie bearbeitet hat.
    /// </summary>
    /// <returns>True, wenn sich am Datensatz etwas geaendert hat.</returns>
    public static bool UebernimmAlteTextfelder(SchachtRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        var geaendert = false;
        var eins = SchachtFeldnamen.Feld(record, FieldKeys.ShaftDimension1Mm);
        var zwei = SchachtFeldnamen.Feld(record, FieldKeys.ShaftDimension2Mm);

        foreach (var gemeint in AlteTextfelder)
        {
            foreach (var feld in SchachtFeldnamen.Schreibweisen(record, gemeint).ToList())
            {
                if (!record.Fields.ContainsKey(feld))
                    continue;

                var text = (record.GetFieldValue(feld) ?? "").Trim();
                if (text.Length > 0)
                {
                    var masse = Lies(text);
                    if (masse is null)
                        continue;

                    var zahlenLeer = string.IsNullOrWhiteSpace(record.GetFieldValue(eins))
                                     && string.IsNullOrWhiteSpace(record.GetFieldValue(zwei));
                    if (zahlenLeer)
                    {
                        var herkunft = record.FieldMeta.TryGetValue(feld, out var meta) ? meta : null;
                        var quelle = herkunft?.Source ?? FieldSource.Legacy;
                        var vonHand = herkunft?.UserEdited ?? false;
                        record.SetFieldValue(eins, masse.Value.Dimension1, quelle, vonHand);
                        record.SetFieldValue(zwei, masse.Value.Dimension2, quelle, vonHand);
                    }
                }

                record.Fields.Remove(feld);
                record.FieldMeta.Remove(feld);
                geaendert = true;
            }
        }

        return geaendert;
    }

    /// <summary>Dasselbe fuer alle Schaechte eines Projekts.</summary>
    /// <returns>Die Zahl der geaenderten Datensaetze.</returns>
    public static int UebernimmAlteTextfelder(IEnumerable<SchachtRecord>? schaechte)
        => schaechte?.Count(UebernimmAlteTextfelder) ?? 0;

    private static (string Dimension1, string Dimension2)? Paar(int? erstes, int? zweites)
    {
        erstes ??= zweites;
        zweites ??= erstes;
        if (erstes is not > 0 || zweites is not > 0)
            return null;

        return (
            erstes.Value.ToString(CultureInfo.InvariantCulture),
            zweites.Value.ToString(CultureInfo.InvariantCulture));
    }
}
