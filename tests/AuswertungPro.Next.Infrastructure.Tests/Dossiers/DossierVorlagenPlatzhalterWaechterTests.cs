using System.Text.RegularExpressions;
using AuswertungPro.Next.Application.Dashboard;
using AuswertungPro.Next.Application.Dossiers;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Models.Dossiers;
using AuswertungPro.Next.Infrastructure.Dossiers;
using DocumentFormat.OpenXml.Packaging;

namespace AuswertungPro.Next.Infrastructure.Tests.Dossiers;

/// <summary>
/// Die ausgelieferte Wordvorlage und der Fueller muessen dieselben Namen verwenden.
/// Ein Platzhalter, den niemand fuellt, bleibt im fertigen Dossier still leer -
/// ohne Fehlermeldung. Genau so entstand der Eindruck, "Inhalte fehlen".
/// </summary>
public sealed class DossierVorlagenPlatzhalterWaechterTests
{
    /// <summary>Ohne Gegenstueck im Fueller, aber bewusst so: Bild und Wiederholmarken.</summary>
    private static readonly HashSet<string> Sonderfaelle = new(StringComparer.OrdinalIgnoreCase)
    {
        "@Uebersichtsplan", "@Logo", "@Wappen",
        "#Eigentuemer", "#Themen", "#Aenderungen", "#Haltungen"
    };

    [Fact]
    public void Jeder_Platzhalter_der_Vorlage_wird_auch_gefuellt()
    {
        var gefuellt = BekannteFeldnamen();
        var offen = VorlagenPlatzhalter()
            .Where(name => !Sonderfaelle.Contains(name))
            .Where(name => !gefuellt.Contains(name))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        Assert.True(
            offen.Count == 0,
            "Diese Platzhalter stehen in der Vorlage, werden aber von niemandem gefuellt "
            + "und bleiben im Dossier still leer: " + string.Join(", ", offen));
    }

    [Theory]
    [InlineData("#Themen")]
    [InlineData("#Eigentuemer")]
    [InlineData("#Aenderungen")]
    [InlineData("@Uebersichtsplan")]
    [InlineData("Aktennotiz")]
    [InlineData("Rueckmeldung")]
    [InlineData("Gebiet_Ort")]
    public void Die_tragenden_Platzhalter_stehen_in_der_Vorlage(string name)
    {
        // Wer die Vorlage austauscht, verliert sonst ganze Kapitel, ohne es zu merken.
        Assert.Contains(name, VorlagenPlatzhalter());
    }

    [Fact]
    public void Die_Vorlage_enthaelt_keine_Kundendaten()
    {
        // Eine Vorlage mit einem echten Plan oder Namen darin geht an JEDEN
        // Eigentuemer. Groesse ist dafuer der zuverlaessigste Hinweis.
        var datei = new FileInfo(VorlagenPfad());

        Assert.True(
            datei.Length < 300 * 1024,
            $"Die Vorlage ist {datei.Length / 1024} KB gross. Steckt ein echter Plan "
            + "oder ein Kundenfoto darin? Der Uebersichtsplan gehoert als "
            + "{{@Uebersichtsplan}} hinein, nicht als Bild.");
    }

    private static HashSet<string> BekannteFeldnamen()
    {
        var request = new DossierExportRequest(
            new Project(),
            "",
            new DossierAreaSettings
            {
                Topics = [new DossierTopicRow { Title = "Ausgangslage", Text = "x" }]
            },
            new DossierDefinition
            {
                Owners = [new DossierOwnerRow { Name = "x" }],
                Changes = [new DossierChangeRow { Version = "A" }]
            },
            new DossierSnapshot(
                Guid.NewGuid(),
                "Testliegenschaft",
                [],
                [],
                LeereStatistik()),
            "");

        var namen = new HashSet<string>(
            DossierWordTemplateExportService.BuildValues(request).Keys,
            StringComparer.OrdinalIgnoreCase);

        foreach (var zeile in DossierWordTemplateExportService.BuildTopicRows(request.Area, request.Dossier))
            namen.UnionWith(zeile.Keys);
        foreach (var zeile in DossierWordTemplateExportService.BuildOwnerRows(request.Dossier))
            namen.UnionWith(zeile.Keys);
        foreach (var zeile in DossierWordTemplateExportService.BuildChangeRows(request.Dossier))
            namen.UnionWith(zeile.Keys);

        return namen;
    }

    private static DashboardStatistics LeereStatistik()
    {
        var verteilung = new ZustandVerteilung(Array.Empty<ZustandBucket>());
        return new DashboardStatistics(
            0, 0, 0, 0,
            verteilung,
            verteilung,
            Array.Empty<DashboardBucket>(),
            Array.Empty<DashboardCostBucket>(),
            0, 0, 0, 0, 0);
    }

    private static HashSet<string> VorlagenPlatzhalter()
    {
        using var doc = WordprocessingDocument.Open(VorlagenPfad(), false);
        var text = string.Concat(
            new[] { doc.MainDocumentPart!.Document.InnerText }
                .Concat(doc.MainDocumentPart.HeaderParts.Select(p => p.Header.InnerText))
                .Concat(doc.MainDocumentPart.FooterParts.Select(p => p.Footer.InnerText)));

        return Regex.Matches(text, @"\{\{([^}]{1,60})\}\}")
            .Select(m => m.Groups[1].Value.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static string VorlagenPfad()
        => Path.Combine(
            TestRepoPaths.RepoRoot(),
            "Export_Vorlage",
            DossierWordTemplate.TemplateFileName);
}
