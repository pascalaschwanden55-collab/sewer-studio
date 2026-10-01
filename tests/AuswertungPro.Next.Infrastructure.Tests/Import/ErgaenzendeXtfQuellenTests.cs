using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.Import;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Import;
using AuswertungPro.Next.Infrastructure.Import.WinCan;

namespace AuswertungPro.Next.Infrastructure.Tests.Import;

/// <summary>
/// Arbeitspaket 9 des Uebergabeplans vom 2026-09-05: Quellenwege anschliessen.
///
/// Gemessen an allen drei IBAK-Projekten des Bestands (2026-09-05):
///
///   Goeschenen Unterdorfstrasse: Der IBAK-Weg liefert 86 Haltungen, 0 Schaechte und
///     2 Videolinks. Die danebenliegenden XTF tragen 4 weitere Haltungen, 71 Schaechte,
///     86 Videolinks und 425 Fotos — gelesen wurden sie nie.
///   Erstfeld Jagdmatt: gar keine XTF. Der IBAK-Weg ist die einzige Quelle.
///   Buerglen Gosmergasse: nur eine Organisationsliste mit 2206 Eintraegen und keinem
///     einzigen Fachdatensatz.
///
/// Der IBAK-Weg wird deshalb ERGAENZT, nicht ersetzt.
/// </summary>
public sealed class ErgaenzendeXtfQuellenTests
{
    private sealed class AufzeichnenderXtfImport : IXtfImportService
    {
        public List<IReadOnlyList<string>> Aufrufe { get; } = new();

        public Result<ImportStats> ImportXtfFiles(
            IEnumerable<string> xtfPaths, Project project, ImportRunContext? ctx = null)
        {
            var pfade = xtfPaths.ToList();
            Aufrufe.Add(pfade);
            return Result<ImportStats>.Success(
                new ImportStats(0, 0, 0, 0, 0, Array.Empty<string>()));
        }
    }

    [Fact]
    public void IbakOrdner_LiestInspektionsUndKatasterXtfZusaetzlich()
    {
        MitIbakOrdner((quelle, projektOrdner) =>
        {
            SchreibeInspektionsXtf(quelle, "inspektion.xtf", "1000-2000");
            SchreibeKatasterXtf(quelle, "kataster_SIA405.xtf", "1000");

            var xtf = new AufzeichnenderXtfImport();
            new ProjectImportOrchestrator(xtf, new WinCanDbImportService())
                .Import(quelle, projektOrdner, new Project());

            var ergaenzung = Assert.Single(xtf.Aufrufe);
            Assert.Equal(2, ergaenzung.Count);
            Assert.Contains(ergaenzung, p => p.EndsWith("inspektion.xtf", StringComparison.Ordinal));
            Assert.Contains(ergaenzung, p => p.EndsWith("kataster_SIA405.xtf", StringComparison.Ordinal));
        });
    }

    [Fact]
    public void ReineOrganisationsliste_WirdNichtGelesen()
    {
        MitIbakOrdner((quelle, projektOrdner) =>
        {
            File.WriteAllText(Path.Combine(quelle, "vsa_organisationen.xtf"), """
<?xml version="1.0" encoding="UTF-8"?>
<TRANSFER xmlns="http://www.interlis.ch/INTERLIS2.3">
  <HEADERSECTION SENDER="Test" VERSION="2.3">
    <MODELS><MODEL NAME="SIA405_Base_Abwasser_LV95" /></MODELS>
  </HEADERSECTION>
  <DATASECTION>
    <SIA405_Base_Abwasser_LV95.Administration BID="B1">
      <SIA405_Base_Abwasser_LV95.Administration.Organisation TID="O1"><Bezeichnung>Abwasser Uri</Bezeichnung></SIA405_Base_Abwasser_LV95.Administration.Organisation>
    </SIA405_Base_Abwasser_LV95.Administration>
  </DATASECTION>
</TRANSFER>
""");

            var xtf = new AufzeichnenderXtfImport();
            new ProjectImportOrchestrator(xtf, new WinCanDbImportService())
                .Import(quelle, projektOrdner, new Project());

            Assert.Empty(xtf.Aufrufe);
        });
    }

    [Fact]
    public void WinCanOrdner_WirdBewusstNichtErgaenzt()
    {
        // Bei WinCan ist kein solcher Bedarf gemessen, und ein zusaetzlicher XTF-Lauf
        // koennte dort gepruefte Werte verschieben.
        var wurzel = Path.Combine(Path.GetTempPath(), $"ap9-wincan-{Guid.NewGuid():N}");
        var quelle = Path.Combine(wurzel, "quelle");
        var projektOrdner = Path.Combine(wurzel, "projekt");
        Directory.CreateDirectory(Path.Combine(quelle, "DB"));
        Directory.CreateDirectory(projektOrdner);
        File.WriteAllText(Path.Combine(quelle, "DB", "projekt.db3"), "keine echte Datenbank");
        SchreibeInspektionsXtf(quelle, "inspektion.xtf", "1000-2000");

        try
        {
            var xtf = new AufzeichnenderXtfImport();
            var ergebnis = new ProjectImportOrchestrator(xtf, new WinCanDbImportService())
                .Import(quelle, projektOrdner, new Project());

            Assert.Equal(KanalExportFormat.WinCan, ergebnis.Format);
            Assert.Empty(xtf.Aufrufe);
        }
        finally
        {
            try { Directory.Delete(wurzel, recursive: true); } catch { }
        }
    }

    [Fact]
    public void IkasOrdner_LiestSeineHauptquelleNichtEinZweitesMal()
    {
        var wurzel = Path.Combine(Path.GetTempPath(), $"ap9-ikas-{Guid.NewGuid():N}");
        var quelle = Path.Combine(wurzel, "quelle");
        var projektOrdner = Path.Combine(wurzel, "projekt");
        Directory.CreateDirectory(quelle);
        Directory.CreateDirectory(projektOrdner);
        File.WriteAllText(Path.Combine(quelle, "export.xtf"), """
<?xml version="1.0" encoding="UTF-8"?>
<TRANSFER xmlns="http://www.interlis.ch/INTERLIS2.3">
  <HEADERSECTION SENDER="Test" VERSION="2.3">
    <MODELS><MODEL NAME="VSA_KEK_2020_LV95" /></MODELS>
  </HEADERSECTION>
  <DATASECTION>
    <VSA_KEK_2020_LV95.KEK BID="B1">
      <VSA_KEK_2020_LV95.KEK.Untersuchung TID="U1"><Bezeichnung>1000-2000</Bezeichnung></VSA_KEK_2020_LV95.KEK.Untersuchung>
    </VSA_KEK_2020_LV95.KEK>
  </DATASECTION>
</TRANSFER>
""");

        try
        {
            var xtf = new AufzeichnenderXtfImport();
            var ergebnis = new ProjectImportOrchestrator(xtf, new WinCanDbImportService())
                .Import(quelle, projektOrdner, new Project());

            Assert.Equal(KanalExportFormat.Ikas, ergebnis.Format);
            // Genau ein Aufruf: die Hauptquelle. Keine Ergaenzung derselben Datei.
            var aufruf = Assert.Single(xtf.Aufrufe);
            Assert.EndsWith("export.xtf", Assert.Single(aufruf), StringComparison.Ordinal);
        }
        finally
        {
            try { Directory.Delete(wurzel, recursive: true); } catch { }
        }
    }

    // ---------------------------------------------------------------------

    /// <summary>Ein Ordner, den die Erkennung ueber das IBAK/KIAS-Dateimuster findet.</summary>
    private static void MitIbakOrdner(Action<string, string> pruefung)
    {
        var wurzel = Path.Combine(Path.GetTempPath(), $"ap9-ibak-{Guid.NewGuid():N}");
        var quelle = Path.Combine(wurzel, "quelle");
        var projektOrdner = Path.Combine(wurzel, "projekt");
        Directory.CreateDirectory(Path.Combine(quelle, "Film"));
        Directory.CreateDirectory(projektOrdner);
        File.WriteAllText(Path.Combine(quelle, "Arizona.fdb"), string.Empty);
        File.WriteAllText(Path.Combine(quelle, "Film", "Daten.txt"), "dummy-daten");

        try { pruefung(quelle, projektOrdner); }
        finally { try { Directory.Delete(wurzel, recursive: true); } catch { } }
    }

    private static void SchreibeInspektionsXtf(string ordner, string name, string haltung)
        => File.WriteAllText(Path.Combine(ordner, name), $"""
<?xml version="1.0" encoding="UTF-8"?>
<TRANSFER xmlns="http://www.interlis.ch/INTERLIS2.3">
  <HEADERSECTION SENDER="Test" VERSION="2.3">
    <MODELS><MODEL NAME="VSA_KEK" VERSION="15.07.2008" /></MODELS>
  </HEADERSECTION>
  <DATASECTION>
    <VSA_KEK.KEK BID="B1">
      <VSA_KEK.KEK.Untersuchung TID="U1"><Bezeichnung>{haltung}</Bezeichnung></VSA_KEK.KEK.Untersuchung>
    </VSA_KEK.KEK>
  </DATASECTION>
</TRANSFER>
""");

    private static void SchreibeKatasterXtf(string ordner, string name, string schacht)
        => File.WriteAllText(Path.Combine(ordner, name), $"""
<?xml version="1.0" encoding="UTF-8"?>
<TRANSFER xmlns="http://www.interlis.ch/INTERLIS2.3">
  <HEADERSECTION SENDER="Test" VERSION="2.3">
    <MODELS><MODEL NAME="SIA405_Abwasser" /></MODELS>
  </HEADERSECTION>
  <DATASECTION>
    <SIA405_Abwasser.Abwasser BID="B1">
      <SIA405_Abwasser.Abwasser.Normschacht TID="N1"><Bezeichnung>{schacht}</Bezeichnung></SIA405_Abwasser.Abwasser.Normschacht>
    </SIA405_Abwasser.Abwasser>
  </DATASECTION>
</TRANSFER>
""");
}
