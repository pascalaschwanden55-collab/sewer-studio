using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using AuswertungPro.Next.Application.Import;
using AuswertungPro.Next.Application.Lookup;
using AuswertungPro.Next.Application.UseCases;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Import;
using AuswertungPro.Next.Infrastructure.Import.WinCan;
using AuswertungPro.Next.Infrastructure.Import.Xtf;

namespace AuswertungPro.Next.Infrastructure.Tests.Import;

/// <summary>
/// Die Restpunkte aus Arbeitspaket 9 und 10 des Uebergabeplans vom 2026-09-05:
/// Bestandsbilanz, Abbruch und die Trennung im GEONIS-Bericht.
/// </summary>
public sealed class ImportRestpunkteTests
{
    // ---------------------------------------------------------------------
    // AP9: Bestandsbilanz statt einer nackten Haltungszahl
    // ---------------------------------------------------------------------

    [Fact]
    public void DieBilanz_NenntVideosProtokolleUndBefundeGetrennt()
    {
        var projekt = new Project();
        projekt.Data.Add(Haltung("1000-2000", video: "a.mpg", protokoll: "a.pdf", befunde: 3));
        projekt.Data.Add(Haltung("3000-4000", video: null, protokoll: "b.pdf", befunde: 0));
        projekt.Data.Add(Haltung("5000-6000", video: "c.mpg", protokoll: null, befunde: 2, gegenvideo: "c-g.mpg"));
        projekt.SchaechteData.Add(Schacht("1000", protokoll: "s.pdf"));
        projekt.SchaechteData.Add(Schacht("2000", protokoll: null));

        var bilanz = ImportBestandszaehler.Zaehle(projekt);

        Assert.Equal(3, bilanz.Haltungen);
        Assert.Equal(2, bilanz.HaltungenMitVideo);
        Assert.Equal(1, bilanz.HaltungenMitGegenvideo);
        Assert.Equal(2, bilanz.HaltungenMitProtokoll);
        Assert.Equal(2, bilanz.HaltungenMitBefunden);
        Assert.Equal(5, bilanz.Befunde);
        Assert.Equal(2, bilanz.Schaechte);
        Assert.Equal(1, bilanz.SchaechteMitProtokoll);
    }

    [Fact]
    public void DieBilanz_BehauptetNichtDassEtwasFehlt()
    {
        var projekt = new Project();
        projekt.Data.Add(Haltung("1000-2000", video: null, protokoll: null, befunde: 0));

        var zeilen = ImportBestandszaehler.Zaehle(projekt).Berichtszeilen();

        // Sie nennt den Bestand — die Bewertung bleibt beim Menschen.
        Assert.Contains(zeilen, z => z.Contains("Ohne Video: 1", StringComparison.Ordinal));
        Assert.Contains(zeilen, z => z.Contains("sagt diese Zahl NICHT", StringComparison.Ordinal));
    }

    [Fact]
    public void DerImport_LiefertDieBilanzImErgebnis()
    {
        MitQuelle((quelle, projektOrdner) =>
        {
            var projekt = new Project();
            var ergebnis = new ProjectImportOrchestrator(
                new XtfImportServiceAdapter(), new WinCanDbImportService())
                .Import(quelle, projektOrdner, projekt);

            Assert.NotNull(ergebnis.Bestand);
            Assert.Equal(projekt.Data.Count, ergebnis.Bestand!.Haltungen);
            Assert.Contains(ergebnis.Messages, m => m.StartsWith("Haltungen:", StringComparison.Ordinal));
        });
    }

    // ---------------------------------------------------------------------
    // AP9: Abbruch
    // ---------------------------------------------------------------------

    [Fact]
    public void EinAbbruchSignal_StopptDenImport()
    {
        MitQuelle((quelle, projektOrdner) =>
        {
            using var abbruch = new CancellationTokenSource();
            abbruch.Cancel();

            var kontext = new ImportRunContext(
                abbruch.Token, null, new ImportRunLog(), collectionLock: new object());

            // Der lange Weg muss das Signal durchreichen — ein Abbruch ist kein Fehler,
            // er wird weitergeworfen.
            Assert.Throws<OperationCanceledException>(() =>
                new ProjectImportOrchestrator(new XtfImportServiceAdapter(), new WinCanDbImportService())
                    .Import(quelle, projektOrdner, new Project(), kontext));
        });
    }

    // ---------------------------------------------------------------------
    // AP10: Neu-Export gegen echten Abgleich
    // ---------------------------------------------------------------------

    [Theory]
    [InlineData(true, true, true, true, GeonisVerbundStand.AbgleichMoeglich)]
    [InlineData(true, true, true, false, GeonisVerbundStand.NurNeuExport)]
    [InlineData(true, false, true, true, GeonisVerbundStand.NurNeuExport)]
    public void DerVerbundEntscheidetUeberDenAbgleich(
        bool haltung, bool kanal, bool vonPunkt, bool nachPunkt, GeonisVerbundStand erwartet)
    {
        var kennung = KatasterKennung.FuerHaltung(
            "1000-2000", "Altdorf",
            haltung ? "chHALTUNG0000001" : "",
            kanal ? "chKANAL000000001" : null,
            vonPunkt ? "chVON00000000001" : null, null,
            nachPunkt ? "chNACH0000000001" : null, null,
            null, null);

        Assert.Equal(erwartet, GeonisVerbund.Bestimme(kennung, BauteilArt.Haltung));
    }

    [Fact]
    public void OhneKennung_IstDieZuordnungUngeklaert()
        => Assert.Equal(
            GeonisVerbundStand.Ungeklaert,
            GeonisVerbund.Bestimme(null, BauteilArt.Haltung));

    [Fact]
    public void DerBericht_TrenntAbgleichVonNeuExport()
    {
        var bestand = new KatasterKennungBestand(
            BauteilArt.Haltung,
            new Dictionary<string, KatasterKennung>(StringComparer.OrdinalIgnoreCase)
            {
                ["1000-2000"] = KatasterKennung.FuerHaltung(
                    "1000-2000", "Altdorf", "chHALTUNG0000001", "chKANAL000000001",
                    "chVON00000000001", null, "chNACH0000000001", null, null, null),
                ["3000-4000"] = KatasterKennung.FuerHaltung(
                    "3000-4000", "Altdorf", "chHALTUNG0000002", null, null, null, null, null, null, null)
            },
            new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            2,
            "Dezember 2024");

        var plan = KatasterKennungPlanBuilder.BaueFuerHaltungen(
            [NackteHaltung("1000-2000"), NackteHaltung("3000-4000")], bestand);

        Assert.Equal(1, plan.AbgleichMoeglich);
        Assert.Equal(1, plan.NurNeuExport);

        var bericht = KatasterKennungBericht.Schreibe(plan, "x.gpkg");
        Assert.Contains("vollständigem Objektverbund", bericht, StringComparison.Ordinal);
        Assert.Contains("nur für einen Neu-Export", bericht, StringComparison.Ordinal);
        Assert.Contains("neue Objekte statt einer Aktualisierung", bericht, StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------------

    private static HaltungRecord Haltung(
        string name, string? video, string? protokoll, int befunde, string? gegenvideo = null)
    {
        var record = new HaltungRecord();
        record.SetFieldValue(FieldKeys.HoldingName, name, FieldSource.Legacy, userEdited: false);
        if (video is not null)
            record.SetFieldValue(FieldKeys.Link, video, FieldSource.Legacy, userEdited: false);
        if (gegenvideo is not null)
            record.SetFieldValue("Link_G", gegenvideo, FieldSource.Legacy, userEdited: false);
        if (protokoll is not null)
            record.SetFieldValue(FieldKeys.PdfPath, protokoll, FieldSource.Legacy, userEdited: false);
        record.VsaFindings = Enumerable.Range(0, befunde)
            .Select(_ => new VsaFinding { KanalSchadencode = "BAB" })
            .ToList();
        return record;
    }

    private static HaltungRecord NackteHaltung(string name)
    {
        var record = new HaltungRecord();
        record.SetFieldValue(FieldKeys.HoldingName, name, FieldSource.Legacy, userEdited: false);
        return record;
    }

    private static SchachtRecord Schacht(string nummer, string? protokoll)
    {
        var record = new SchachtRecord();
        record.SetFieldValue("Schachtnummer", nummer);
        if (protokoll is not null)
            record.SetFieldValue("PDF_Path", protokoll);
        return record;
    }

    private static void MitQuelle(Action<string, string> pruefung)
    {
        var wurzel = Path.Combine(Path.GetTempPath(), $"rest-{Guid.NewGuid():N}");
        var quelle = Path.Combine(wurzel, "quelle");
        var projektOrdner = Path.Combine(wurzel, "projekt");
        Directory.CreateDirectory(quelle);
        Directory.CreateDirectory(projektOrdner);
        File.WriteAllText(Path.Combine(quelle, "test.xtf"), """
<?xml version="1.0" encoding="UTF-8"?>
<TRANSFER xmlns="http://www.interlis.ch/INTERLIS2.3">
  <HEADERSECTION SENDER="Test" VERSION="2.3">
    <MODELS><MODEL NAME="VSA_KEK_2020_LV95" /></MODELS>
  </HEADERSECTION>
  <DATASECTION>
    <VSA_KEK_2020_LV95.KEK BID="B1">
      <VSA_KEK_2020_LV95.KEK.Untersuchung TID="U1">
        <Bezeichnung>1000-2000</Bezeichnung>
        <vonPunktBezeichnung>1000</vonPunktBezeichnung>
        <bisPunktBezeichnung>2000</bisPunktBezeichnung>
      </VSA_KEK_2020_LV95.KEK.Untersuchung>
    </VSA_KEK_2020_LV95.KEK>
  </DATASECTION>
</TRANSFER>
""");

        try { pruefung(quelle, projektOrdner); }
        finally { try { Directory.Delete(wurzel, recursive: true); } catch { } }
    }
}
