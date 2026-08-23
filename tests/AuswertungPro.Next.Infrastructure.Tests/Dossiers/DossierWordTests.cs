using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

using AuswertungPro.Next.Application.Dossiers;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Models.Dossiers;
using AuswertungPro.Next.Infrastructure.Dossiers;

using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.Dossiers;

public sealed class DocxPlaceholderFillerTests
{
    [Fact]
    public void Ersetzt_einen_Platzhalter_der_ueber_mehrere_Textstuecke_verteilt_ist()
    {
        // Genau dieser Fall entsteht, sobald Word die Vorlage einmal
        // gespeichert hat: der Platzhalter liegt zerlegt in mehreren Runs.
        using var stream = new MemoryStream();
        using (var document = CreateDocument(stream,
                   paragraph => paragraph.Append(
                       Run("{{"), Run("Eigen"), Run("tuemer"), Run("}}"))))
        {
            DocxPlaceholderFiller.Fill(document, new Dictionary<string, string>
            {
                ["Eigentuemer"] = "Lubag AG"
            });
            document.MainDocumentPart!.Document.Save();
        }

        var text = ReadText(stream);
        Assert.Contains("Lubag AG", text, StringComparison.Ordinal);
        Assert.DoesNotContain("{{", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Ein_Platzhalter_ohne_Wert_wird_geleert_statt_stehen_zu_bleiben()
    {
        using var stream = new MemoryStream();
        using (var document = CreateDocument(stream,
                   paragraph => paragraph.Append(Run("Tel.: {{Telefon}}"))))
        {
            DocxPlaceholderFiller.Fill(document, new Dictionary<string, string>());
            document.MainDocumentPart!.Document.Save();
        }

        var text = ReadText(stream);
        Assert.DoesNotContain("{{", text, StringComparison.Ordinal);
        Assert.DoesNotContain("}}", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Mehrzeiliger_Wert_wird_zu_echten_Umbruechen()
    {
        using var stream = new MemoryStream();
        using (var document = CreateDocument(stream,
                   paragraph => paragraph.Append(Run("{{Adresse}}"))))
        {
            DocxPlaceholderFiller.Fill(document, new Dictionary<string, string>
            {
                ["Adresse"] = "Landenbergstrasse 34\n6005 Luzern"
            });
            document.MainDocumentPart!.Document.Save();
        }

        stream.Position = 0;
        using var reopened = WordprocessingDocument.Open(stream, false);
        var body = reopened.MainDocumentPart!.Document.Body!;

        Assert.Contains(body.Descendants<Text>(), t => t.Text.Contains("Landenbergstrasse"));
        Assert.Contains(body.Descendants<Text>(), t => t.Text.Contains("6005 Luzern"));
        Assert.NotEmpty(body.Descendants<Break>());
    }

    [Fact]
    public void ReplacePlaceholders_laesst_unvollstaendige_Klammern_unangetastet()
    {
        var result = DocxPlaceholderFiller.ReplacePlaceholders(
            "Preis {{Betrag}} und {{offen",
            new Dictionary<string, string> { ["Betrag"] = "100" });

        Assert.Equal("Preis 100 und {{offen", result);
    }

    private static WordprocessingDocument CreateDocument(
        MemoryStream stream,
        Action<Paragraph> fill)
    {
        var document = WordprocessingDocument.Create(
            stream, WordprocessingDocumentType.Document);
        var mainPart = document.AddMainDocumentPart();
        mainPart.Document = new Document();
        var body = mainPart.Document.AppendChild(new Body());

        var paragraph = body.AppendChild(new Paragraph());
        fill(paragraph);

        return document;
    }

    private static Run Run(string text)
        => new(new Text(text) { Space = SpaceProcessingModeValues.Preserve });

    private static string ReadText(MemoryStream stream)
    {
        stream.Position = 0;
        using var document = WordprocessingDocument.Open(stream, false);
        return string.Concat(
            document.MainDocumentPart!.Document.Body!
                .Descendants<Text>()
                .Select(t => t.Text));
    }
}

public sealed class DossierWordTemplateExportServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "dossier_tests_" + Guid.NewGuid().ToString("N"));

    public DossierWordTemplateExportServiceTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_root))
                Directory.Delete(_root, recursive: true);
        }
        catch
        {
            // Aufraeumfehler im Test darf den Lauf nicht rot machen.
        }
    }

    [Fact]
    public async Task Erzeugt_ein_Word_ohne_uebrig_gebliebene_Platzhalter()
    {
        var (request, templatePath) = BuildScenario();

        var service = new DossierWordTemplateExportService(() => templatePath);
        var result = await service.ExportAsync(request);

        Assert.True(result.Success, result.Message);
        Assert.NotNull(result.FilePath);

        var text = ReadDocumentText(result.FilePath!);

        // Kein Platzhalter darf beim Eigentuemer auf dem Tisch landen.
        Assert.DoesNotContain("{{", text, StringComparison.Ordinal);
        Assert.DoesNotContain("}}", text, StringComparison.Ordinal);

        Assert.Contains("Lubag AG", text, StringComparison.Ordinal);
        Assert.Contains("762+756", text, StringComparison.Ordinal);
        Assert.Contains("Erstfeld West", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Schreibt_jede_ausgewaehlte_Haltung_als_eigene_Zeile()
    {
        var (request, templatePath) = BuildScenario();

        var service = new DossierWordTemplateExportService(() => templatePath);
        var result = await service.ExportAsync(request);

        var text = ReadDocumentText(result.FilePath!);

        Assert.Contains("36080-36086", text, StringComparison.Ordinal);
        Assert.Contains("33850-7.25390", text, StringComparison.Ordinal);
        Assert.Contains("41.70 m", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Ueberschreibt_eine_vorhandene_Word_Datei_nie()
    {
        var (request, templatePath) = BuildScenario();

        var service = new DossierWordTemplateExportService(() => templatePath);
        var first = await service.ExportAsync(request);
        var second = await service.ExportAsync(request);

        Assert.True(first.Success);
        Assert.True(second.Success);
        Assert.NotEqual(first.FilePath, second.FilePath);
        Assert.True(File.Exists(first.FilePath!));
    }

    [Fact]
    public async Task Fehlende_Vorlage_meldet_sich_klar_und_erzeugt_nichts()
    {
        var (request, _) = BuildScenario();
        var fehlt = Path.Combine(_root, "gibtesnicht.docx");

        var service = new DossierWordTemplateExportService(() => fehlt);
        var result = await service.ExportAsync(request);

        Assert.False(result.Success);
        Assert.Contains("Vorlage", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(File.Exists(Path.Combine(request.TargetFolder,
            DossierFolderPlanner.WordFileName)));
    }

    [Fact]
    public async Task Leeres_Dossier_erzeugt_trotzdem_ein_lesbares_Word()
    {
        var (request, templatePath) = BuildScenario(withHoldings: false);

        var service = new DossierWordTemplateExportService(() => templatePath);
        var result = await service.ExportAsync(request);

        Assert.True(result.Success, result.Message);

        var text = ReadDocumentText(result.FilePath!);
        Assert.DoesNotContain("{{", text, StringComparison.Ordinal);
        Assert.Contains("Keine Leitungen zugeordnet", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Schreibt_jede_Eigentuemerzeile_als_eigene_Tabellenzeile()
    {
        var (request, templatePath) = BuildScenario();
        request.Dossier.Owners.Clear();
        request.Dossier.Owners.Add(new DossierOwnerRow
        {
            HouseNumber = "3",
            ParcelNumber = "170",
            Name = "Martin Muster",
            Phone = "079 858 53 74",
            Occupancy = "Einfamilienhaus"
        });
        request.Dossier.Owners.Add(new DossierOwnerRow
        {
            HouseNumber = "4",
            ParcelNumber = "171",
            Name = "Anna Gisler",
            Mail = "anna.gisler@example.ch"
        });

        var service = new DossierWordTemplateExportService(() => templatePath);
        var result = await service.ExportAsync(request);

        var text = ReadDocumentText(result.FilePath!);

        Assert.Contains("Martin Muster", text, StringComparison.Ordinal);
        Assert.Contains("Anna Gisler", text, StringComparison.Ordinal);
        Assert.Contains("Tel.: 079 858 53 74", text, StringComparison.Ordinal);
        Assert.Contains("Objektbewohner: Einfamilienhaus", text, StringComparison.Ordinal);
        Assert.DoesNotContain("{{", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Ohne_Eigentuemerzeile_bleibt_ein_klarer_Hinweis_statt_eines_Platzhalters()
    {
        var (request, templatePath) = BuildScenario();
        request.Dossier.Owners.Clear();

        var service = new DossierWordTemplateExportService(() => templatePath);
        var result = await service.ExportAsync(request);

        var text = ReadDocumentText(result.FilePath!);

        Assert.Contains("Keine Eigentümerangaben erfasst", text, StringComparison.Ordinal);
        Assert.DoesNotContain("{{", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Nimmt_die_Autoren_des_Gebiets_statt_des_Windows_Benutzers()
    {
        var (request, templatePath) = BuildScenario();
        request.Area.Authors = "Pascal Aschwanden/";

        var service = new DossierWordTemplateExportService(() => templatePath);
        var result = await service.ExportAsync(request);

        var text = ReadDocumentText(result.FilePath!);

        Assert.Contains("Pascal Aschwanden/", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Setzt_Logo_Wappen_und_Uebersichtsplan_als_Bilder_ein()
    {
        var (request, templatePath) = BuildScenario();

        // Logo und Wappen liegen neben der Vorlage.
        var vorlagenOrdner = Path.GetDirectoryName(templatePath)!;
        File.WriteAllBytes(
            Path.Combine(vorlagenOrdner, DossierWordTemplateExportService.LogoFileName),
            TestImages.Png(716, 297));
        File.WriteAllBytes(
            Path.Combine(vorlagenOrdner, DossierWordTemplateExportService.CoatOfArmsFileName),
            TestImages.Png(407, 491));

        var planPfad = Path.Combine(_root, "plan.png");
        File.WriteAllBytes(planPfad, TestImages.Png(1200, 1600));
        request.Dossier.OverviewPlanPath = planPfad;

        var service = new DossierWordTemplateExportService(() => templatePath);
        var result = await service.ExportAsync(request);

        Assert.True(result.Success, result.Message);

        using var document = WordprocessingDocument.Open(result.FilePath!, false);
        Assert.Equal(3, document.MainDocumentPart!.ImageParts.Count());

        var text = ReadDocumentText(result.FilePath!);
        Assert.DoesNotContain("{{", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Fehlende_Bilder_erzeugen_trotzdem_ein_vollstaendiges_Dossier()
    {
        var (request, templatePath) = BuildScenario();
        request.Dossier.OverviewPlanPath = Path.Combine(_root, "gibtesnicht.png");

        var service = new DossierWordTemplateExportService(() => templatePath);
        var result = await service.ExportAsync(request);

        Assert.True(result.Success, result.Message);

        var text = ReadDocumentText(result.FilePath!);
        Assert.DoesNotContain("{{", text, StringComparison.Ordinal);
        Assert.Contains("Erstfeld West", text, StringComparison.Ordinal);
    }

    private (DossierExportRequest Request, string TemplatePath) BuildScenario(
        bool withHoldings = true)
    {
        var templatePath = Path.Combine(_root, "Vorlage", "Eigentuemerdossier.docx");
        DossierWordTemplateBuilder.WriteTo(templatePath);

        var project = new Project();
        var dossier = new DossierDefinition
        {
            Name = "Brämenhofstatt 3+4+7+8",
            ParcelNumbers = "762+756",
            HouseNumbers = "3+4+7+8",
            Address = "Brämenhofstatt",
            PostalCode = "6472",
            Town = "Erstfeld",
            OwnerName = "Lubag AG",
            OwnerAddress = "Landenbergstrasse 34, 6005 Luzern",
            ContactName = "Sandro Sigrist",
            ContactPhone = "041 360 00 50",
            ContactMail = "sandro.sigrist@lubag.ch",
            Occupancy = "Mehrfamilienhaus",
            ConstructionProcess = "Leitung 1 und 7 mittels Inliner sanieren."
        };

        dossier.Owners.Add(new DossierOwnerRow
        {
            HouseNumber = "3+4+7+8",
            ParcelNumber = "762+756",
            Name = "Lubag AG Landenbergstrasse 34, 6005 Luzern",
            Phone = "041 360 00 50",
            Mail = "sandro.sigrist@lubag.ch",
            Occupancy = "Mehrfamilienhaus"
        });

        var costs = new ProjectCostStore();

        if (withHoldings)
        {
            var a = NewHolding("36080-36086", "41.70", "1");
            var b = NewHolding("33850-7.25390", "25.40", "2");
            project.Data.Add(a);
            project.Data.Add(b);
            dossier.HoldingIds.Add(a.Id);
            dossier.HoldingIds.Add(b.Id);

            costs.ByHolding["36080-36086"] = new HoldingCost
            {
                Holding = "36080-36086",
                Total = 28_400m
            };
        }

        var area = new DossierAreaSettings
        {
            AreaTitle = "Sanierung Private Abwasserleitungen Erstfeld West",
            ContactPerson = "Abwasser Uri, Giessenstrasse 46, 6460 Altdorf",
            ExecutionDate = "Herbst 2026/Frühling 2027",
            HouseConnectionText = "Der Zustand der privaten Hausanschlussleitungen wurde beurteilt.",
            StormWaterText = "Gebiet mit Versickerungsmöglichkeiten. Trennsystem vorhanden.",
            ResponseDeadline = "Mitte Dezember 2026",
            FooterLine = "Lubag AG Sanierung Kanalisation, PZ.762+756"
        };

        var snapshot = DossierSnapshotBuilder.Build(dossier, project, costs);
        var targetFolder = Path.Combine(_root, "Projekt", "Dossiers", "Braemenhofstatt");
        Directory.CreateDirectory(Path.Combine(_root, "Projekt"));

        var request = new DossierExportRequest(
            project,
            Path.Combine(_root, "Projekt"),
            area,
            dossier,
            snapshot,
            targetFolder);

        return (request, templatePath);
    }

    private static HaltungRecord NewHolding(string name, string length, string condition)
    {
        var record = new HaltungRecord();
        record.Fields[FieldKeys.HoldingName] = name;
        record.Fields[FieldKeys.HoldingLengthMeters] = length;
        record.Fields[FieldKeys.ConditionClass] = condition;
        record.Fields[FieldKeys.RecommendedRehabilitationMeasures] = "Inliner";
        return record;
    }

    private static string ReadDocumentText(string path)
    {
        using var document = WordprocessingDocument.Open(path, false);
        var mainPart = document.MainDocumentPart!;

        var parts = new List<string>
        {
            string.Concat(mainPart.Document.Body!.Descendants<Text>().Select(t => t.Text))
        };

        foreach (var footer in mainPart.FooterParts)
        {
            parts.Add(string.Concat(
                footer.Footer!.Descendants<Text>().Select(t => t.Text)));
        }

        return string.Join("\n", parts);
    }
}

public sealed class DossierWordTemplateBuilderTests
{
    [Fact]
    public void Die_Vorlage_ist_eine_gueltige_Word_Datei_mit_allen_Platzhaltern()
    {
        var text = ReadTemplateText();

        foreach (var expected in new[]
                 {
                     "{{Gebietstitel}}", "{{Parzellen_Zeile}}", "{{Eigentuemer_Block}}",
                     "{{Revision}}", "{{Datum}}", "{{Autoren}}",
                     "{{@Logo}}", "{{@Wappen}}", "{{@Uebersichtsplan}}",
                     "{{#Eigentuemer}}", "{{Haus_Nr}}", "{{Pz_Nr}}", "{{Eigentuemer_Zelle}}",
                     "{{#Haltungen}}",
                     "{{Ausfuehrungstermin}}", "{{Hausanschluss}}", "{{Meteorwasser}}",
                     "{{Rueckmeldung}}"
                 })
        {
            Assert.Contains(expected, text, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Die_Vorlage_traegt_die_vier_Kapitel_des_Vorbilds()
    {
        var text = ReadTemplateText();

        Assert.Contains("Eigentümerdossier", text, StringComparison.Ordinal);
        Assert.Contains("1.  Übersichtsplan Werkleitungen", text, StringComparison.Ordinal);
        Assert.Contains("2.  Eigentumsverhältnisse", text, StringComparison.Ordinal);
        Assert.Contains("3.  Betroffene Abwasserleitungen", text, StringComparison.Ordinal);
        Assert.Contains("4.  Informationen Sanierung", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Die_Rueckmeldung_steht_in_der_Info_Tabelle_und_nicht_als_eigenes_Kapitel()
    {
        var text = ReadTemplateText();

        Assert.Contains("Rückmeldung / Einverständnis Eigentümer", text, StringComparison.Ordinal);
        Assert.DoesNotContain("5.  Rückmeldung", text, StringComparison.Ordinal);
        Assert.Contains("Unterschrift(en)", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Das_Deckblatt_traegt_keinen_Logo_Hinweistext_mehr()
    {
        var text = ReadTemplateText();

        Assert.DoesNotContain("Logo hier einfügen", text, StringComparison.Ordinal);
        Assert.DoesNotContain("{{Logo_Hinweis}}", text, StringComparison.Ordinal);
    }

    private static string ReadTemplateText()
    {
        var bytes = DossierWordTemplateBuilder.Build();

        using var stream = new MemoryStream(bytes);
        using var document = WordprocessingDocument.Open(stream, false);

        return string.Concat(
            document.MainDocumentPart!.Document.Body!
                .Descendants<Text>()
                .Select(t => t.Text));
    }
}
