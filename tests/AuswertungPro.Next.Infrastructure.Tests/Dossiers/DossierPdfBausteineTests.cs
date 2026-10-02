using System.Text;

using AuswertungPro.Next.Infrastructure.Dossiers;

using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

using UglyToad.PdfPig;

namespace AuswertungPro.Next.Infrastructure.Tests.Dossiers;

/// <summary>
/// Die gemeinsamen Bausteine der Dossier-PDF-Dienste (B5, Deepscan 02.10.2026): Kopf, Zustandszelle,
/// Metadaten. Gerendert in ein kleines Dokument und mit PdfPig gelesen.
/// </summary>
public sealed class DossierPdfBausteineTests
{
    [Fact]
    public void Kopf_ohne_Logo_zeigt_Schriftzug_und_optionalen_Ersatztext_fuer_das_Wappen()
    {
        var mitErsatz = Text(c => DossierPdfBausteine.ComposeHeader(c, null, null, 70, "SCHACHTLISTE"));
        Assert.Contains("ABWASSER URI", mitErsatz, StringComparison.Ordinal);
        Assert.Contains("SCHACHTLISTE", mitErsatz, StringComparison.Ordinal);

        var ohneErsatz = Text(c => DossierPdfBausteine.ComposeHeader(c, null, null));
        Assert.Contains("ABWASSER URI", ohneErsatz, StringComparison.Ordinal);
        Assert.DoesNotContain("SCHACHTLISTE", ohneErsatz, StringComparison.Ordinal);
    }

    [Fact]
    public void Zustandszelle_zeigt_Klasse_oder_nicht_erfasst()
    {
        Assert.Contains("Z3", Text(c => DossierPdfBausteine.ComposeCondition(c, "3")), StringComparison.Ordinal);
        Assert.Contains("nicht erfasst", Text(c => DossierPdfBausteine.ComposeCondition(c, "")), StringComparison.Ordinal);
        Assert.Contains("nicht erfasst", Text(c => DossierPdfBausteine.ComposeCondition(c, "Z9")), StringComparison.Ordinal);
    }

    [Fact]
    public void Metadaten_zeigen_Eigentuemer_Liegenschaft_Stand_und_Fehlwert()
    {
        var text = Text(c => DossierPdfBausteine.ComposeMetadata(c, " Heinz Müller ", null, new DateTime(2026, 8, 28)));

        Assert.Contains("EIGENTÜMER", text, StringComparison.Ordinal);
        Assert.Contains("Heinz Müller", text, StringComparison.Ordinal);
        Assert.Contains("LIEGENSCHAFT", text, StringComparison.Ordinal);
        Assert.Contains("nicht erfasst", text, StringComparison.Ordinal);
        Assert.Contains("28.08.2026", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Farben_der_Zustandsklasse_kommen_aus_der_Berichtspalette()
    {
        Assert.Equal(("#FF0000", "#FFFFFF"), DossierPdfBausteine.ResolveConditionColors("0"));
        Assert.Null(DossierPdfBausteine.ResolveConditionColors("Z5"));
    }

    private static string Text(Action<IContainer> baustein)
    {
        QuestPDF.Settings.License = LicenseType.Community;
        var pdf = Document.Create(document => document.Page(page =>
        {
            page.Size(QuestPDF.Helpers.PageSizes.A4);
            page.Margin(30);
            page.Content().Element(baustein);
        })).GeneratePdf();

        using var doc = PdfDocument.Open(pdf);
        var sb = new StringBuilder();
        foreach (var page in doc.GetPages())
            sb.Append(page.Text).Append(' ');
        return sb.ToString();
    }
}
