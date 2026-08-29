using AuswertungPro.Next.Application.Dossiers;
using AuswertungPro.Next.Infrastructure.Export.Excel;

using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace AuswertungPro.Next.Infrastructure.Dossiers;

/// <summary>
/// Erzeugt den festen, einseitigen Erklaeranhang fuer jedes Eigentuemerdossier.
/// Inhalt und Farben stammen aus denselben fachlichen Quellen wie Tabelle,
/// Excel-Bericht und Dossier-Bauteilliste.
/// </summary>
public sealed class DossierConditionClassPdfService : IDossierConditionClassPdfService
{
    private const string BrandBlue = "#005C84";
    private const string TextColor = "#1F2937";
    private const string MutedTextColor = "#4B5563";
    private const string BorderColor = "#D1D5DB";
    private const string SoftBackground = "#F3F6F8";

    private readonly string _templateAssetFolder;
    private readonly Lazy<byte[]> _pdf;

    internal static IDossierConditionClassPdfService Shared { get; } =
        new DossierConditionClassPdfService();

    public DossierConditionClassPdfService(string? templateAssetFolder = null)
    {
        _templateAssetFolder = string.IsNullOrWhiteSpace(templateAssetFolder)
            ? Path.Combine(AppContext.BaseDirectory, "Export_Vorlage")
            : Path.GetFullPath(templateAssetFolder);
        _pdf = new Lazy<byte[]>(
            BuildPdf,
            System.Threading.LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public byte[] CreatePdf()
        => (byte[])_pdf.Value.Clone();

    private byte[] BuildPdf()
    {
        QuestPDF.Settings.License = LicenseType.Community;

        var logo = TryReadAsset(DossierWordTemplateExportService.LogoFileName);
        var coatOfArms = TryReadAsset(DossierWordTemplateExportService.CoatOfArmsFileName);

        return Document.Create(document =>
        {
            document.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.MarginHorizontal(34);
                page.MarginVertical(28);
                page.DefaultTextStyle(style => style
                    .FontFamily("Arial")
                    .FontSize(9.5f)
                    .FontColor(TextColor));

                page.Header().Element(container => ComposeHeader(container, logo, coatOfArms));
                page.Content().PaddingTop(12).Column(column =>
                {
                    column.Spacing(7);

                    column.Item().Element(container => ComposeTitle(
                        container,
                        DossierConditionClassDefinitions.PdfHeading,
                        DossierConditionClassDefinitions.PdfSubtitle));

                    column.Item()
                        .Background(SoftBackground)
                        .Border(1)
                        .BorderColor("#DCE5EA")
                        .Padding(12)
                        .Column(introduction =>
                        {
                            introduction.Item().Text(
                                    "Die Zustandsklasse beschreibt den zusammengefassten baulichen und "
                                    + "betrieblichen Zustand eines Entwässerungsobjekts. Z0 kennzeichnet "
                                    + "den schlechtesten, Z4 den besten Zustand.")
                                .SemiBold();
                            introduction.Item().PaddingTop(4).Text(
                                    "Ein Schadenscode beschreibt dagegen einen einzelnen Befund. "
                                    + "Die Zustandsklasse fasst die Bewertung aller relevanten Befunde zusammen.")
                                .FontColor(MutedTextColor);
                        });

                    foreach (var definition in DossierConditionClassDefinitions.All)
                    {
                        column.Item().Element(container => ComposeClassRow(container, definition));
                    }

                    column.Item().Element(ComposeClassificationBasis);

                    column.Item()
                        .Border(1)
                        .BorderColor("#B8CBD5")
                        .Background("#F7FAFC")
                        .PaddingHorizontal(10)
                        .PaddingVertical(8)
                        .Column(note =>
                        {
                            note.Item().Text("Wichtig für die Einordnung").Bold().FontColor(BrandBlue);
                            note.Item().PaddingTop(3).Text(
                                "- " + DossierConditionClassDefinitions.NotCalculatedNote);
                            note.Item().PaddingTop(2).Text(
                                "- Einzelbefunde und konkrete Massnahmen stehen im jeweiligen Untersuchungsprotokoll.");
                        });
                });

                page.Footer().Element(container => ComposeFooter(
                    container,
                    "Erklärblatt zum Eigentümerdossier | Z0 = schlechtester, Z4 = bester Zustand"));
            });
        }).GeneratePdf();
    }

    private static void ComposeTitle(
        IContainer container,
        string heading,
        string subtitle)
    {
        container
            .Background(BrandBlue)
            .PaddingHorizontal(15)
            .PaddingVertical(10)
            .Column(title =>
            {
                title.Item()
                    .Text(heading)
                    .FontSize(19)
                    .Bold()
                    .FontColor(Colors.White);
                title.Item()
                    .PaddingTop(2)
                    .Text(subtitle)
                    .FontSize(10)
                    .FontColor("#DDECF2");
            });
    }

    private static void ComposeFooter(IContainer container, string line)
    {
        container.Column(footer =>
        {
            footer.Item().LineHorizontal(0.6f).LineColor(BorderColor);
            footer.Item().PaddingTop(4).Row(row =>
            {
                row.RelativeItem()
                    .Text(line)
                    .FontSize(7.5f)
                    .FontColor(MutedTextColor);
                row.AutoItem().Text(text =>
                {
                    text.DefaultTextStyle(style => style.FontSize(7.5f).FontColor(MutedTextColor));
                    text.Span("Seite ");
                    text.CurrentPageNumber();
                    text.Span(" von ");
                    text.TotalPages();
                });
            });
            footer.Item()
                .Height(2)
                .Text(DossierConditionClassDefinitions.PdfRequiredPageMarker)
                .FontSize(1)
                .FontColor(Colors.White);
        });
    }

    internal static (string Background, string Foreground) ResolveColors(string value)
    {
        var rule = ExcelReportStyle.Zustandsklassen.FirstOrDefault(candidate =>
            string.Equals(candidate.Wert, value, StringComparison.Ordinal));

        if (string.IsNullOrWhiteSpace(rule.Farbe))
            throw new ArgumentOutOfRangeException(nameof(value), value, "Unbekannte Zustandsklasse.");

        var background = "#" + rule.Farbe[^6..];
        var foreground = value is "0" or "1" ? "#FFFFFF" : TextColor;
        return (background, foreground);
    }

    private static void ComposeHeader(
        IContainer container,
        byte[]? logo,
        byte[]? coatOfArms)
    {
        container.Row(row =>
        {
            row.ConstantItem(150).Height(48).AlignLeft().AlignMiddle().Element(left =>
            {
                if (logo is not null)
                    left.Image(logo).FitArea();
                else
                    left.Text("ABWASSER URI").FontSize(13).Bold().FontColor(BrandBlue);
            });

            row.RelativeItem();

            row.ConstantItem(42).Height(48).AlignRight().AlignMiddle().Element(right =>
            {
                if (coatOfArms is not null)
                    right.Image(coatOfArms).FitArea();
            });
        });
    }

    private static void ComposeClassRow(
        IContainer container,
        DossierConditionClassDefinition definition)
    {
        var colors = ResolveColors(definition.Value);

        container
            .MinHeight(61)
            .Border(1)
            .BorderColor(BorderColor)
            .Row(row =>
            {
                row.ConstantItem(62)
                    .Background(colors.Background)
                    .AlignCenter()
                    .AlignMiddle()
                    .Text(definition.Code)
                    .FontSize(17)
                    .Bold()
                    .FontColor(colors.Foreground);

                row.RelativeItem().PaddingHorizontal(10).PaddingVertical(8).Column(text =>
                {
                    text.Item().Text(definition.Name).FontSize(10.5f).Bold();
                    text.Item().PaddingTop(2).Text(definition.Description)
                        .FontSize(8.5f)
                        .FontColor(MutedTextColor);
                });

                row.ConstantItem(138)
                    .Background("#F8FAFC")
                    .PaddingHorizontal(8)
                    .PaddingVertical(7)
                    .Column(orientation =>
                    {
                        orientation.Item()
                            .Text("Zeitraum (Orientierung)")
                            .FontSize(7.25f)
                            .FontColor("#6B7280");
                        orientation.Item().PaddingTop(2).Text(definition.Orientation)
                            .FontSize(definition.Value == "4" ? 7.5f : 8f)
                            .SemiBold()
                            .LineHeight(1.08f);
                    });
            });
    }

    private static void ComposeClassificationBasis(IContainer container)
    {
        container
            .Border(1)
            .BorderColor("#B8CBD5")
            .Background("#EEF5F8")
            .PaddingHorizontal(10)
            .PaddingVertical(8)
            .Column(basis =>
            {
                basis.Item()
                    .Text(DossierConditionClassDefinitions.ClassificationBasisHeading)
                    .Bold()
                    .FontColor(BrandBlue);

                foreach (var note in DossierConditionClassDefinitions.ClassificationBasisNotes)
                {
                    basis.Item()
                        .PaddingTop(2)
                        .Text("- " + note)
                        .FontSize(8.25f);
                }

                basis.Item()
                    .PaddingTop(4)
                    .Text(DossierConditionClassDefinitions.ClassificationBasisSource)
                    .FontSize(7.5f)
                    .Italic()
                    .FontColor(MutedTextColor);
            });
    }

    private byte[]? TryReadAsset(string fileName)
    {
        try
        {
            var path = Path.Combine(_templateAssetFolder, fileName);
            return File.Exists(path) ? File.ReadAllBytes(path) : null;
        }
        catch
        {
            // Das Erklaerblatt bleibt auch ohne optionale Bildmarke vollstaendig.
            return null;
        }
    }
}
