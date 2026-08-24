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

    [Fact]
    public void Zwei_Textfelder_im_selben_Absatz_bleiben_getrennt()
    {
        // Das Deckblatt der Dossiervorlage besteht aus Textfeldern. Word legt
        // sie als Absaetze INNERHALB eines Absatzes ab. Wird der aeussere Absatz
        // mitgefuellt, laufen die Texte aller Felder in einem einzigen Run
        // zusammen und die uebrigen Felder werden geleert — das Deckblatt waere
        // zerstoert.
        using var stream = new MemoryStream();
        using (var document = WordprocessingDocument.Create(
                   stream, WordprocessingDocumentType.Document))
        {
            var mainPart = document.AddMainDocumentPart();
            mainPart.Document = new Document();
            var body = mainPart.Document.AppendChild(new Body());
            body.InnerXml = TextfeldAbsatz;

            DocxPlaceholderFiller.Fill(document, new Dictionary<string, string>
            {
                ["Links"] = "Gebietstitel",
                ["Rechts"] = "Parzelle 30"
            });
            mainPart.Document.Save();
        }

        stream.Position = 0;
        using var wieder = WordprocessingDocument.Open(stream, false);
        var felder = wieder.MainDocumentPart!.Document.Body!
            .Descendants<TextBoxContent>()
            .Select(f => string.Concat(f.Descendants<Text>().Select(t => t.Text)))
            .ToList();

        Assert.Equal(2, felder.Count);
        Assert.Equal("Gebietstitel", felder[0]);
        Assert.Equal("Parzelle 30", felder[1]);
    }

    private const string TextfeldAbsatz = """
        <w:p xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main">
          <w:r>
            <w:pict xmlns:v="urn:schemas-microsoft-com:vml">
              <v:shape><v:textbox><w:txbxContent>
                <w:p><w:r><w:t>{{Links}}</w:t></w:r></w:p>
              </w:txbxContent></v:textbox></v:shape>
            </w:pict>
          </w:r>
          <w:r>
            <w:pict xmlns:v="urn:schemas-microsoft-com:vml">
              <v:shape><v:textbox><w:txbxContent>
                <w:p><w:r><w:t>{{Rechts}}</w:t></w:r></w:p>
              </w:txbxContent></v:textbox></v:shape>
            </w:pict>
          </w:r>
        </w:p>
        """;

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
    public void Der_Ort_steht_auf_dem_Deckblatt_nur_einmal()
    {
        // Die Vorlage hat fuer Strasse und Ort ZWEI Zeilen. Liefert die
        // Adresszeile beides, steht der Ort zweimal untereinander.
        var werte = DossierWordTemplateExportService.BuildValues(BuildScenario().Request);

        Assert.Equal("Brämenhofstatt 3+4+7+8", werte["Adresse_Zeile"]);
        Assert.Equal("6472 Erstfeld", werte["Ort_Zeile"]);
        Assert.DoesNotContain("Erstfeld", werte["Adresse_Zeile"], StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("439", "Parzelle 439")]
    [InlineData("  439 ", "Parzelle 439")]
    [InlineData("439, 440", "Parzellen 439, 440")]
    [InlineData("762+756", "Parzellen 762+756")]
    [InlineData("439a", "Parzelle 439a")]
    [InlineData("", "")]
    public void Die_Parzellenzeile_zaehlt_richtig(string eingabe, string erwartet)
    {
        var (request, _) = BuildScenario();
        request.Dossier.ParcelNumbers = eingabe;

        Assert.Equal(erwartet, DossierWordTemplateExportService.BuildValues(request)["Parzellen_Zeile"]);
    }

    [Fact]
    public async Task Die_neuen_Deckblattfelder_erscheinen_im_fertigen_Dossier()
    {
        var (request, templatePath) = BuildScenario();
        request.Area.AreaLocation = "6472 Musterdorf";
        request.Area.ProjectNumber = "AWU 2026-042";
        request.Area.DrawnBy = "Pa";
        request.Dossier.FileNote = "Altdorf, 24. August 2026";

        var service = new DossierWordTemplateExportService(() => templatePath);
        var result = await service.ExportAsync(request);

        Assert.True(result.Success, result.Message);
        var text = ReadDocumentText(result.FilePath!);

        Assert.Contains("6472 Musterdorf", text, StringComparison.Ordinal);
        Assert.Contains("AWU 2026-042", text, StringComparison.Ordinal);
        Assert.Contains("Altdorf, 24. August 2026", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Die_Themen_stammen_aus_Gebiet_und_Dossier()
    {
        var (request, templatePath) = BuildScenario();
        request.Area.Topics.Clear();
        request.Area.Topics.Add(new DossierTopicRow { Title = "Unternehmer", Text = "Musterbau AG" });
        request.Area.Topics.Add(new DossierTopicRow { Title = "Bemerkungen", Text = "Standardtext" });
        request.Dossier.Topics.Clear();
        request.Dossier.Topics.Add(new DossierTopicRow { Title = "Bemerkungen", Text = "Hier anders" });
        request.Dossier.Topics.Add(new DossierTopicRow { Title = "Schäden Pz. 30", Text = "Leitung undicht" });

        var service = new DossierWordTemplateExportService(() => templatePath);
        var result = await service.ExportAsync(request);

        Assert.True(result.Success, result.Message);
        var text = ReadDocumentText(result.FilePath!);

        Assert.Contains("Musterbau AG", text, StringComparison.Ordinal);
        Assert.Contains("Hier anders", text, StringComparison.Ordinal);
        Assert.Contains("Schäden Pz. 30", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Standardtext", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Das_Aenderungswesen_zeigt_jede_erfasste_Zeile()
    {
        var (request, templatePath) = BuildScenario();
        request.Dossier.Changes.Clear();
        request.Dossier.Changes.Add(new DossierChangeRow
        {
            Version = "1", Date = "09.04.2026", Visum = "Pa", Change = "Erstausgabe"
        });
        request.Dossier.Changes.Add(new DossierChangeRow
        {
            Version = "2", Date = "24.08.2026", Visum = "Pa", Change = "Kosten ergänzt"
        });

        var service = new DossierWordTemplateExportService(() => templatePath);
        var result = await service.ExportAsync(request);

        Assert.True(result.Success, result.Message);
        var text = ReadDocumentText(result.FilePath!);

        Assert.Contains("Erstausgabe", text, StringComparison.Ordinal);
        Assert.Contains("Kosten ergänzt", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Die_Schaechte_der_Liegenschaft_stehen_im_Dossier()
    {
        var (request, templatePath) = BuildScenario();
        request.Dossier.ShaftNumbers = new List<string> { "36051" };

        // Der Schacht muss im Projekt vorhanden sein — sonst gehoert er nicht
        // ins Dossier.
        var schacht = new SchachtRecord();
        schacht.SetFieldValue("Schachtnummer", "36051");
        request.Project.SchaechteData.Add(schacht);

        var snapshot = DossierSnapshotBuilder.Build(request.Dossier, request.Project, null);
        var mitSchacht = request with { Snapshot = snapshot };

        var service = new DossierWordTemplateExportService(() => templatePath);
        var result = await service.ExportAsync(mitSchacht);

        Assert.True(result.Success, result.Message);
        var text = ReadDocumentText(result.FilePath!);

        Assert.Contains("36051", text, StringComparison.Ordinal);
        Assert.Contains("Schacht", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Ein_Schacht_den_das_Projekt_nicht_kennt_erscheint_nicht()
    {
        // Lieber eine kurze Liste als ein erfundener Schacht im Brief.
        var (request, templatePath) = BuildScenario();
        request.Dossier.ShaftNumbers = new List<string> { "99999" };

        var snapshot = DossierSnapshotBuilder.Build(request.Dossier, request.Project, null);
        var mitSchacht = request with { Snapshot = snapshot };

        var service = new DossierWordTemplateExportService(() => templatePath);
        var result = await service.ExportAsync(mitSchacht);

        Assert.True(result.Success, result.Message);
        Assert.DoesNotContain("99999", ReadDocumentText(result.FilePath!), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Die_ausgewaehlten_Leitungen_stehen_als_eigenes_Kapitel_im_Dossier()
    {
        // Das Originaldossier kennt dieses Kapitel nicht — ohne es faenden
        // Leitung, Laenge und Kosten nirgends Platz.
        var (request, templatePath) = BuildScenario();

        var service = new DossierWordTemplateExportService(() => templatePath);
        var result = await service.ExportAsync(request);

        Assert.True(result.Success, result.Message);
        var text = ReadDocumentText(result.FilePath!);

        Assert.Contains("Betroffene Leitungen", text, StringComparison.Ordinal);
        Assert.Contains("36080-36086", text, StringComparison.Ordinal);
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

        // Beide Zeilen muessen wirklich EIGENE Tabellenzeilen sein — faellt
        // beides in eine Zeile zusammen, bliebe der Test oben trotzdem gruen.
        Assert.Equal(3, CountOwnerTableRows(result.FilePath!)); // Kopfzeile + 2 Datenzeilen
    }

    /// <summary>Zaehlt die Zeilen der Eigentuemertabelle, gefunden ueber ihre Kopfzeile "Haus Nr.".</summary>
    private static int CountOwnerTableRows(string path)
    {
        using var document = WordprocessingDocument.Open(path, false);
        var body = document.MainDocumentPart!.Document.Body!;

        var table = body.Descendants<Table>().Single(t => t.Descendants<Text>()
            .Any(text => text.Text.Contains("Haus Nr.", StringComparison.Ordinal)));

        return table.Elements<TableRow>().Count();
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
    public async Task Gefuellte_Eigentuemerfelder_speisen_das_Deckblatt_auch_wenn_es_Tabellenzeilen_gibt()
    {
        // Pascal kuerzt den Namen in der Tabelle sinnvoll ("Lubag AG"), die
        // klassischen Felder tragen weiterhin Name und Adresse. Beides muss
        // auf dem Deckblatt stehen, nicht nur der gekuerzte Tabellenname.
        var (request, templatePath) = BuildScenario();
        request.Dossier.OwnerName = "Lubag AG";
        request.Dossier.OwnerAddress = "Landenbergstrasse 34, 6005 Luzern";
        request.Dossier.Owners.Clear();
        request.Dossier.Owners.Add(new DossierOwnerRow
        {
            HouseNumber = "3+4+7+8",
            ParcelNumber = "762+756",
            Name = "Lubag AG"
        });

        var service = new DossierWordTemplateExportService(() => templatePath);
        var result = await service.ExportAsync(request);

        var text = ReadDocumentText(result.FilePath!);

        Assert.Contains("Lubag AG", text, StringComparison.Ordinal);
        Assert.Contains("Landenbergstrasse 34, 6005 Luzern", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Nur_Tabellenzeilen_ohne_klassische_Felder_speisen_ebenfalls_das_Deckblatt()
    {
        var (request, templatePath) = BuildScenario();
        request.Dossier.OwnerName = "";
        request.Dossier.OwnerAddress = "";
        request.Dossier.Owners.Clear();
        request.Dossier.Owners.Add(new DossierOwnerRow
        {
            HouseNumber = "3",
            ParcelNumber = "170",
            Name = "Martin Muster"
        });
        request.Dossier.Owners.Add(new DossierOwnerRow
        {
            HouseNumber = "4",
            ParcelNumber = "171",
            Name = "Anna Gisler"
        });

        var service = new DossierWordTemplateExportService(() => templatePath);
        var result = await service.ExportAsync(request);

        var text = ReadDocumentText(result.FilePath!);

        Assert.Contains("Martin Muster", text, StringComparison.Ordinal);
        Assert.Contains("Anna Gisler", text, StringComparison.Ordinal);
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
    public async Task Leere_Autoren_bleiben_leer_statt_den_Windows_Benutzernamen_zu_zeigen()
    {
        // "Lieber leer als falsch": auf diesem Rechner heisst der Benutzer
        // "Besitzer" — das gehoert nicht in ein Dokument fuer den Eigentuemer.
        var (request, templatePath) = BuildScenario();
        request.Area.Authors = "";

        var service = new DossierWordTemplateExportService(() => templatePath);
        var result = await service.ExportAsync(request);

        var text = ReadDocumentText(result.FilePath!);

        Assert.DoesNotContain(Environment.UserName, text, StringComparison.Ordinal);
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

        // Die Datei entsteht trotzdem — aber Pascal darf nicht erst beim
        // Eigentuemer merken, dass Kapitel 1 leer geblieben ist.
        Assert.True(result.Success, result.Message);
        Assert.Contains("Übersichtsplan", result.Message, StringComparison.Ordinal);

        var text = ReadDocumentText(result.FilePath!);
        Assert.DoesNotContain("{{", text, StringComparison.Ordinal);
        Assert.Contains("Erstfeld West", text, StringComparison.Ordinal);
    }

    private static string AusgelieferteVorlage()
    {
        var wurzel = new AuswertungPro.Next.Infrastructure.Backup.RepositoryRootFileLocator()
            .Locate(AppContext.BaseDirectory);
        Assert.NotNull(wurzel);

        var pfad = Path.Combine(wurzel!, "Export_Vorlage", DossierWordTemplate.TemplateFileName);
        Assert.True(File.Exists(pfad), $"'{pfad}' fehlt.");
        return pfad;
    }

    private (DossierExportRequest Request, string TemplatePath) BuildScenario(
        bool withHoldings = true)
    {
        // Geprueft wird gegen die WIRKLICH ausgelieferte Vorlage. Eine im Test
        // nachgebaute Datei wuerde einen Weg beweisen, den das Programm nie geht.
        var templatePath = Path.Combine(_root, "Vorlage", DossierWordTemplate.TemplateFileName);
        Directory.CreateDirectory(Path.GetDirectoryName(templatePath)!);
        File.Copy(AusgelieferteVorlage(), templatePath);

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

/// <summary>
/// Waechter ueber die ausgelieferte Datei "Export_Vorlage/Eigentuemerdossier.docx".
///
/// Sie ist von Hand in Word gestaltet und wird nicht aus Code erzeugt. Der Test
/// nagelt deshalb JEDE sichtbare Zeile fest. Das hat zwei Gruende: eine
/// verrutschte oder geloeschte Platzhalterzeile faellt sofort auf, und es kann
/// kein Personendatum unbemerkt in die Vorlage geraten — sie stammt aus einem
/// echten Kundendossier.
///
/// Wird die Vorlage bewusst geaendert, ist diese Liste mit anzupassen.
/// </summary>
/// <summary>
/// Waechter ueber die ausgelieferte Datei "Export_Vorlage/Eigentuemerdossier.docx".
///
/// Sie ist von Hand in Word gestaltet und wird nicht aus Code erzeugt. Der Test
/// nagelt deshalb JEDE sichtbare Zeile fest. Das hat zwei Gruende: eine
/// verrutschte oder geloeschte Platzhalterzeile faellt sofort auf, und es kann
/// kein Personendatum unbemerkt in die Vorlage geraten — sie stammt aus einem
/// echten Kundendossier.
///
/// Wird die Vorlage bewusst geaendert, ist diese Liste mit anzupassen.
/// </summary>
/// <summary>
/// Waechter ueber die ausgelieferte Datei "Export_Vorlage/Eigentuemerdossier.docx".
///
/// Sie ist von Hand in Word gestaltet und wird nicht aus Code erzeugt. Der Test
/// nagelt deshalb JEDE sichtbare Zeile fest. Das hat zwei Gruende: eine
/// verrutschte oder geloeschte Platzhalterzeile faellt sofort auf, und es kann
/// kein Personendatum unbemerkt in die Vorlage geraten — sie stammt aus einem
/// echten Kundendossier.
///
/// Wird die Vorlage bewusst geaendert, ist diese Liste mit anzupassen.
/// </summary>
public sealed class AusgelieferteDossierWordVorlageTests
{
    /// <summary>
    /// Jede nicht leere Zeile der Vorlage, in Dokumentreihenfolge. Die
    /// Deckblatt-Textfelder liegen doppelt in der Datei (Word legt zu jedem
    /// Feld eine Rueckfallfassung ab); unmittelbare Wiederholungen sind
    /// deshalb zusammengefasst.
    /// </summary>
    private static readonly string[] ErwarteteZeilen =
        {
            "{{Gebietstitel}}",
            "{{Gebiet_Ort}}",
            "{{Gebietstitel}}",
            "{{Gebiet_Ort}}",
            "Eigentümerdossier",
            "{{Parzellen_Zeile}}",
            "{{Eigentuemer_Block}}",
            "{{Adresse_Zeile}}",
            "{{Ort_Zeile}}",
            "{{Parzellen_Zeile}}",
            "{{Eigentuemer_Block}}",
            "{{Adresse_Zeile}}",
            "{{Ort_Zeile}}",
            "Datum: {{Datum}}",
            "Revision: {{Revision}}",
            "Proj. Nr. AWU  : {{Projekt_Nr}}",
            "Gez          :",
            "{{Gezeichnet}}",
            "Version",
            "Datum",
            "Visum",
            "Art der Änderung",
            "{{#Aenderungen}}{{Version}}",
            "{{Datum}}",
            "{{Visum}}",
            "{{Aenderung}}",
            "Erstellungsdatum: {{Datum_Lang}}",
            "Autoren: {{Autoren}}",
            "Inhaltsverzeichnis",
            "1.Übersichtsplan Werkleitungen3",
            "2.Eigentumsverhältnisse4",
            "3.Betroffene Leitungen4",
            "4.Informationen Baustelle5",
            "Übersichtsplan Werkleitungen",
            "{{@Uebersichtsplan}}",
            "Eigentumsverhältnisse",
            "Haus Nr.",
            "Pz. Nr.",
            "Eigentümer",
            "{{#Eigentuemer}}{{Haus_Nr}}",
            "{{Pz_Nr}}",
            "{{Eigentuemer_Zelle}}",
            "Betroffene Leitungen",
            "{{Haltungen_Text}}",
            "{{Haltungen_Summe}}",
            "{{Schaechte_Text}}",
            "Informationen Sanierung",
            "Thema",
            "Bemerkungen",
            "{{#Themen}}{{Thema}}",
            "{{Text}}",
            "Für die Aktennotiz",
            "{{Aktennotiz}}",
            "Rückmeldung / Einverständnis Eigentümer",
            "{{Rueckmeldung}}",
            "……………………………………………………………………                       ………………………………………………….……………………………………………",
            "Ort/Datum                               Unterschrift(en)",
        };

    private static string VorlagenPfad()
    {
        var wurzel = new AuswertungPro.Next.Infrastructure.Backup.RepositoryRootFileLocator()
            .Locate(AppContext.BaseDirectory);
        Assert.NotNull(wurzel);

        var pfad = Path.Combine(wurzel!, "Export_Vorlage", DossierWordTemplate.TemplateFileName);
        Assert.True(File.Exists(pfad), $"'{pfad}' fehlt.");
        return pfad;
    }

    private static List<string> Zeilen(OpenXmlElement wurzel)
        => wurzel.Descendants<Paragraph>()
            .Where(p => !p.Descendants<Paragraph>().Any())
            .Select(p => string.Concat(p.Descendants<Text>().Select(t => t.Text)).Trim())
            .Where(t => t.Length > 0)
            .ToList();

    [Fact]
    public void Die_Vorlage_traegt_genau_die_erwarteten_Zeilen()
    {
        using var document = WordprocessingDocument.Open(VorlagenPfad(), false);

        var zeilen = Zeilen(document.MainDocumentPart!.Document.Body!);

        var entdoppelt = new List<string>();
        foreach (var zeile in zeilen)
        {
            if (entdoppelt.Count == 0 || entdoppelt[^1] != zeile)
                entdoppelt.Add(zeile);
        }

        Assert.Equal(ErwarteteZeilen, entdoppelt);
    }

    [Fact]
    public void Die_Fusszeile_traegt_nur_den_Platzhalter()
    {
        using var document = WordprocessingDocument.Open(VorlagenPfad(), false);

        var fuss = document.MainDocumentPart!.FooterParts
            .SelectMany(f => Zeilen(f.Footer))
            .ToList();

        Assert.Contains("{{Fusszeile}}", fuss);
        Assert.DoesNotContain(fuss, z => z.Contains("Parzelle", StringComparison.Ordinal));
    }

    [Fact]
    public void Die_Vorlage_nennt_keinen_Verfasser_aus_dem_Kundendokument()
    {
        using var document = WordprocessingDocument.Open(VorlagenPfad(), false);

        Assert.Equal("SewerStudio", document.PackageProperties.Creator);
        Assert.Equal("SewerStudio", document.PackageProperties.LastModifiedBy);
    }

    [Fact]
    public void Logo_und_Wappen_bleiben_fest_eingebettet()
    {
        using var document = WordprocessingDocument.Open(VorlagenPfad(), false);
        var mainPart = document.MainDocumentPart!;

        // Logo und Wappen schweben frei auf dem Deckblatt. Als Bildmarke
        // eingesetzt wuerden sie ihre Position verlieren, weil ein nachtraeglich
        // eingefuegtes Bild im Textfluss sitzt. Nur der Uebersichtsplan ist eine
        // Bildmarke — deshalb bleiben genau zwei Bilder in der Datei.
        var namen = mainPart.Document.Body!
            .Descendants<DocumentFormat.OpenXml.Drawing.Wordprocessing.DocProperties>()
            .Select(d => d.Name?.Value ?? string.Empty)
            .ToList();

        Assert.Contains("Logo", namen);
        Assert.Contains("Wappen", namen);
        Assert.Equal(2, mainPart.ImageParts.Count());
    }
}
