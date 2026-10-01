using System;
using System.IO;
using System.Linq;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Import;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace AuswertungPro.Next.Infrastructure.Tests.Import;

/// <summary>
/// Arbeitspaket 7 des Uebergabeplans vom 2026-09-05: Haltungsprotokolle vollstaendig
/// verteilen.
///
/// Vorher (Stand c1021e76e) wurde aus dem Archiv genau EIN Protokoll gesplittet. Ein
/// Ordner mit einem Einzelprotokoll fuer Haltung A und einem Sammelprotokoll fuer B und C
/// liess B und C leer.
/// </summary>
public sealed class SammelprotokollVerteilungTests
{
    [Fact]
    public void SchachtprotokollMitMassen_StartetKeineHaltungsverteilung()
    {
        MitOrdnern((archiv, projektOrdner) =>
        {
            SchreibePdf(archiv, "10051.pdf", "SCHACHTPRO Projekt: Test Datum: 13.08.2026",
                "Schachtprotokoll Schacht Nr. 10051", "STAMMDATEN & SKIZZE", "Tiefe 0.90 Durchmesser 0.80");
            var service = new KanalImportDistributionService();
            Assert.Null(service.SelectPrimaryProtocolPdf(archiv));
            var result = Verteile(NeuesProjekt(), projektOrdner, archiv);
            Assert.Equal(0, result.OriginalProtocolsDistributed);
            Assert.Equal(0, result.Errors);
        });
    }

    [Fact]
    public void EinzelUndZweiSammelprotokolle_VersorgenAlleHaltungen()
    {
        MitOrdnern((archiv, projektOrdner) =>
        {
            SchreibeProtokoll(archiv, "einzel_A.pdf", ("1000-2000", "02.07.2020"));
            SchreibeProtokoll(archiv, "sammel_BC.pdf", ("3000-4000", "02.07.2020"), ("5000-6000", "02.07.2020"));
            SchreibeProtokoll(archiv, "sammel_D.pdf", ("7000-8000", "02.07.2020"));

            var projekt = NeuesProjekt();
            var ergebnis = Verteile(projekt, projektOrdner, archiv);

            var versorgt = projekt.Data
                .Where(r => !string.IsNullOrWhiteSpace(r.GetFieldValue(FieldKeys.PdfPath)))
                .Select(r => r.GetFieldValue(FieldKeys.HoldingName))
                .OrderBy(n => n, StringComparer.Ordinal)
                .ToList();

            Assert.Equal(["1000-2000", "3000-4000", "5000-6000", "7000-8000"], versorgt);
            Assert.Equal(4, ergebnis.OriginalProtocolsDistributed);
        });
    }

    [Fact]
    public void PlanUndDichtheitspruefung_WerdenNichtAlsProtokollVerteilt()
    {
        MitOrdnern((archiv, projektOrdner) =>
        {
            SchreibeProtokoll(archiv, "protokoll.pdf", ("1000-2000", "02.07.2020"));
            SchreibePdf(archiv, "uebersichtsplan.pdf",
                "Situationsplan Gemeinde", "Massstab 1:500", "Legende");
            SchreibePdf(archiv, "dichtheit.pdf",
                "Dichtheitspruefung nach SIA 190", "von Schacht: 100", "nach Schacht: 200", "Pruefdruck");

            var projekt = NeuesProjekt();
            var ergebnis = Verteile(projekt, projektOrdner, archiv);

            var record = Assert.Single(projekt.Data);
            Assert.Equal("1000-2000", record.GetFieldValue(FieldKeys.HoldingName));
            Assert.Contains("1000-2000", record.GetFieldValue(FieldKeys.PdfPath), StringComparison.Ordinal);
        });
    }

    [Fact]
    public void VorhandeneVerknuepfung_WirdVomSammelprotokollNichtUeberholt()
    {
        MitOrdnern((archiv, projektOrdner) =>
        {
            SchreibeProtokoll(archiv, "sammel.pdf", ("1000-2000", "02.07.2020"));

            var projekt = NeuesProjekt();
            var record = new HaltungRecord();
            record.SetFieldValue(FieldKeys.HoldingName, "1000-2000", FieldSource.Legacy, userEdited: false);
            record.SetFieldValue(
                FieldKeys.PdfPath,
                @"Haltungen_Verteilt\1000-2000\20200702_1000-2000.pdf",
                FieldSource.Legacy,
                userEdited: false);
            projekt.Data.Add(record);

            var ergebnis = Verteile(projekt, projektOrdner, archiv);

            // Die Verknuepfung aus dem Einzelprotokoll bleibt …
            Assert.Equal(@"Haltungen_Verteilt\1000-2000\20200702_1000-2000.pdf",
                record.GetFieldValue(FieldKeys.PdfPath));
            // … und der zusaetzliche Auszug wird benannt statt still abgelegt.
            Assert.Contains(ergebnis.Messages,
                m => m.Contains("bereits aus einem Einzelprotokoll versorgt", StringComparison.Ordinal));
        });
    }

    [Fact]
    public void DateireihenfolgeAendertDasErgebnisNicht()
    {
        string[] Versorge(params string[] namen)
        {
            string[] ergebnis = [];
            MitOrdnern((archiv, projektOrdner) =>
            {
                foreach (var name in namen)
                {
                    var haltung = name.StartsWith("a_", StringComparison.Ordinal) ? "1000-2000" : "3000-4000";
                    SchreibeProtokoll(archiv, name, (haltung, "02.07.2020"));
                }

                var projekt = NeuesProjekt();
                Verteile(projekt, projektOrdner, archiv);
                ergebnis = projekt.Data
                    .Where(r => !string.IsNullOrWhiteSpace(r.GetFieldValue(FieldKeys.PdfPath)))
                    .Select(r => r.GetFieldValue(FieldKeys.HoldingName))
                    .OrderBy(n => n, StringComparer.Ordinal)
                    .ToArray();
            });
            return ergebnis;
        }

        Assert.Equal(Versorge("a_eins.pdf", "b_zwei.pdf"), Versorge("b_zwei.pdf", "a_eins.pdf"));
    }

    // ---------------------------------------------------------------------

    private static KanalImportDistributor.Result Verteile(
        Project projekt, string projektOrdner, string archiv)
        => new KanalImportDistributionService().Distribute(
            projekt,
            projektOrdner,
            archivedPdfDir: archiv,
            sourceVideoDir: Path.Combine(projektOrdner, "keine-videos"),
            splitPdf: true,
            primaryProtocolPdf: null,
            fileStaging: null);

    /// <summary>
    /// Ohne Gemeinde findet die Verteilung keinen Zielordner — dieselbe Voraussetzung wie
    /// in den bestehenden Verteilungstests.
    /// </summary>
    private static Project NeuesProjekt()
    {
        var projekt = new Project();
        projekt.Metadata["Gemeinde"] = "Altdorf";
        return projekt;
    }

    private static void MitOrdnern(Action<string, string> pruefung)
    {
        var wurzel = Path.Combine(Path.GetTempPath(), $"ap7-protokoll-{Guid.NewGuid():N}");
        var archiv = Path.Combine(wurzel, "Importdateien", "PDF");
        var projektOrdner = Path.Combine(wurzel, "projekt");
        Directory.CreateDirectory(archiv);
        Directory.CreateDirectory(projektOrdner);

        try { pruefung(archiv, projektOrdner); }
        finally { try { Directory.Delete(wurzel, recursive: true); } catch { } }
    }

    /// <summary>Ein PDF mit je einer Haltungsinspektions-Titelseite pro Haltung.</summary>
    private static void SchreibeProtokoll(
        string ordner, string dateiname, params (string Haltung, string Datum)[] haltungen)
    {
        using var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);

        foreach (var (haltung, datum) in haltungen)
        {
            var page = builder.AddPage(PageSize.A4);
            page.AddText($"Haltungsinspektion - {datum} - {haltung}", 14, new PdfPoint(40, 780), font);
            page.AddText("Leitungsbericht", 12, new PdfPoint(40, 740), font);
        }

        File.WriteAllBytes(Path.Combine(ordner, dateiname), builder.Build());
    }

    private static void SchreibePdf(string ordner, string dateiname, params string[] zeilen)
    {
        using var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        var page = builder.AddPage(PageSize.A4);
        var y = 780m;
        foreach (var zeile in zeilen)
        {
            page.AddText(zeile, 12, new PdfPoint(40, y), font);
            y -= 18;
        }

        File.WriteAllBytes(Path.Combine(ordner, dateiname), builder.Build());
    }
}
