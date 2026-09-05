using AuswertungPro.Next.Application.Export;
using AuswertungPro.Next.Domain.Models;
using System.Linq;
using ClosedXML.Excel;

namespace AuswertungPro.Next.Infrastructure.Tests.Export;

/// <summary>
/// Welche Felder in den Excel-Bericht gelangen, entscheidet die Kopfzeile der Vorlage —
/// nicht <see cref="FieldCatalog.ColumnOrder"/>. Ein neues Feld im Programm darf den
/// Bericht deshalb weder erweitern noch verschieben.
///
/// Der Test haelt den Bestand fest. Am 2026-09-02 sind vier Felder fuer die revidierte
/// XTF dazugekommen (Verbindungsart, Bettung/Umhuellung, Profiltyp, lichte Breite);
/// die Vorgabe dazu war ausdruecklich: die Spalten, die heute in die Excel gelangen,
/// bleiben unveraendert.
/// </summary>
public sealed class ExcelSpaltenBestandTests
{
    private static string VorlageHaltungen()
        => Path.Combine(TestPaths.FindSolutionRoot(), "Export_Vorlage", "Haltungen.xlsx");

    /// <summary>
    /// Die Kopfzeile der ausgelieferten Vorlage, wortwoertlich. Ein zusaetzlicher oder
    /// fehlender Kopf faellt hier auf, bevor ein Kunde einen verschobenen Bericht bekommt.
    /// </summary>
    private static readonly string[] ErwarteteKoepfe =
    [
        "NR.", "Haltungsname (ID)", "Strasse", "Rohrmaterial", "DN mm", "Nutzungsart",
        "Haltungslänge m", "Inspektionsrichtung", "Primäre Schäden", "Zustandsklasse",
        "Prüfungsresultat", "Sanieren Ja/Nein", "Empfohlene Sanierungsmassnahmen",
        "Kosten", "Eigentümer", "Ausgeführt durch", "Bemerkungen", "Link",
        "Renovierung Inliner Stk.", "Renovierung Inliner m", "Anschlüsse verpressen",
        "Reparatur Manschette", "Linerendmanschette LEM", "Reparatur Kurzliner",
        "Erneuerung Neubau m", "offen/abgeschlossen", "Datum/Jahr"
    ];

    [Fact]
    public void Die_vier_neuen_XTF_Felder_stehen_nicht_im_Bericht()
    {
        var koepfe = LiesKopfzeile();

        foreach (var feld in new[]
                 {
                     FieldKeys.ConnectionType, FieldKeys.BeddingEncasement,
                     FieldKeys.ProfileType, FieldKeys.ClearWidthMm,
                     FieldKeys.HierarchicalFunction,
                     FieldKeys.OperatingStatus, FieldKeys.RehabilitationNeed,
                     FieldKeys.HydraulicFunction, FieldKeys.PositionAccuracy,
                     FieldKeys.ConstructionYear, FieldKeys.GrossCost,
                     FieldKeys.CadastreObjectId, FieldKeys.GeonisId, FieldKeys.DataOwner,
                     FieldKeys.DataSupplier, FieldKeys.CadastreOrganisation,
                     FieldKeys.CadastreLastChange, FieldKeys.CadastreUpdatedAt
                 })
        {
            var label = FieldCatalog.Get(feld).Label;
            Assert.DoesNotContain(feld, koepfe, StringComparer.OrdinalIgnoreCase);
            Assert.DoesNotContain(label, koepfe, StringComparer.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void Die_Kopfzeile_der_Vorlage_ist_unveraendert()
    {
        var koepfe = LiesKopfzeile();

        Assert.Equal(string.Join(" | ", ErwarteteKoepfe), string.Join(" | ", koepfe));
    }

    private static List<string> LiesKopfzeile()
    {
        var pfad = VorlageHaltungen();
        Assert.True(File.Exists(pfad), $"Vorlage fehlt: {pfad}");

        using var mappe = new XLWorkbook(pfad);
        var blatt = mappe.Worksheets.First();

        var koepfe = new List<string>();
        for (var spalte = 1; spalte <= 60; spalte++)
        {
            var text = (blatt.Cell(ExcelVorlagenLayout.KopfZeile, spalte).GetString() ?? "").Trim();
            if (text.Length == 0)
                continue;

            koepfe.Add(text);
        }

        return koepfe;
    }
}
