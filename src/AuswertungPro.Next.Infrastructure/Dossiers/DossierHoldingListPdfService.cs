using System.Globalization;

using AuswertungPro.Next.Application.Dossiers;
using AuswertungPro.Next.Application.Reports;
using AuswertungPro.Next.Domain.Models;

using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace AuswertungPro.Next.Infrastructure.Dossiers;

/// <summary>
/// Erzeugt die Haltungsliste eines Eigentuemerdossiers aus den bereits ueber
/// stabile Haltungs-IDs aufgeloesten Projektdaten. Der Dienst schreibt keine Datei.
/// </summary>
public sealed class DossierHoldingListPdfService : IDossierHoldingListPdfService
{
    private const string BrandBlue = "#005C84";
    private const string TextColor = "#1F2937";
    private const string MutedTextColor = "#64748B";
    private const string BorderColor = "#CBD5E1";
    private const string HeaderBackground = "#E5EEF3";
    private const string SoftBackground = "#F3F6F8";
    private const string AlternateRowBackground = "#FAFBFC";
    private const string MissingText = "nicht erfasst";

    private static readonly CultureInfo NumberCulture = CultureInfo.GetCultureInfo("de-DE");
    private readonly string _templateAssetFolder;

    public DossierHoldingListPdfService(string? templateAssetFolder = null)
    {
        _templateAssetFolder = string.IsNullOrWhiteSpace(templateAssetFolder)
            ? Path.Combine(AppContext.BaseDirectory, "Export_Vorlage")
            : Path.GetFullPath(templateAssetFolder);
    }

    public byte[] CreatePdf(DossierHoldingListPdfModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(model.Holdings);

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
                    .FontSize(9.25f)
                    .FontColor(TextColor));

                page.Header().Element(container => ComposeHeader(container, logo, coatOfArms));
                page.Content().PaddingTop(12).Column(column =>
                {
                    column.Spacing(9);
                    column.Item().Element(ComposeTitle);
                    column.Item().Element(container => ComposeMetadata(container, model));
                    column.Item().Element(container => ComposeSummary(container, model));

                    column.Item()
                        .PaddingTop(2)
                        .Text("Leitungen im Eigentümerdossier")
                        .FontSize(12)
                        .Bold()
                        .FontColor(BrandBlue);

                    if (model.Holdings.Count == 0)
                    {
                        column.Item().Element(ComposeEmptyList);
                    }
                    else
                    {
                        column.Item().Element(container => ComposeHoldingTable(
                            container,
                            model.Holdings));
                    }

                    column.Item().Element(ComposeLegend);
                    column.Item().Element(container => ComposeExplanation(
                        container,
                        model.MissingHoldingCount));
                });

                page.Footer().Element(ComposeFooter);
            });
        }).GeneratePdf();
    }

    internal static string ResolveConditionSpan(IEnumerable<DossierHoldingLine> holdings)
    {
        ArgumentNullException.ThrowIfNull(holdings);

        var values = holdings
            .Select(line => NormalizeConditionValue(line.ConditionClass))
            .Where(value => value is not null)
            .Select(value => value!.Value)
            .ToList();

        if (values.Count == 0)
            return MissingText;

        var minimum = values.Min();
        var maximum = values.Max();
        return minimum == maximum
            ? $"Z{minimum}"
            : $"Z{minimum} bis Z{maximum}";
    }

    internal static (string Background, string Foreground)? ResolveConditionColors(string? value)
    {
        var normalized = NormalizeConditionValue(value);
        if (normalized is null)
            return null;

        var colors = ProtocolPdfExporter.ResolveZustandsklassenFarbe(
            normalized.Value.ToString(CultureInfo.InvariantCulture));
        return colors is null
            ? null
            : (colors.Value.Hintergrund, colors.Value.Schrift);
    }

    internal static string FormatLength(double? value)
        => value is null
            ? MissingText
            : value.Value.ToString("0.00", NumberCulture) + " m";

    internal static string FormatTotalLength(IEnumerable<DossierHoldingLine> holdings)
    {
        ArgumentNullException.ThrowIfNull(holdings);
        var values = holdings
            .Where(line => line.LengthMeters is not null)
            .Select(line => line.LengthMeters!.Value)
            .ToList();
        return values.Count == 0 ? MissingText : FormatLength(values.Sum());
    }

    private static void ComposeTitle(IContainer container)
    {
        container
            .Background(BrandBlue)
            .PaddingHorizontal(15)
            .PaddingVertical(10)
            .Column(title =>
            {
                title.Item()
                    .Text("Haltungsliste")
                    .FontSize(19)
                    .Bold()
                    .FontColor(Colors.White);
                title.Item()
                    .PaddingTop(2)
                    .Text("Ergänzung zum Eigentümerdossier")
                    .FontSize(10)
                    .FontColor("#DDECF2");
            });
    }

    private static void ComposeMetadata(
        IContainer container,
        DossierHoldingListPdfModel model)
    {
        container
            .Border(1)
            .BorderColor("#D8E2E8")
            .Background(SoftBackground)
            .PaddingHorizontal(12)
            .PaddingVertical(9)
            .Row(row =>
            {
                ComposeMetadataItem(
                    row.RelativeItem(1.2f).PaddingRight(12),
                    "EIGENTÜMER",
                    Display(model.OwnerName));
                ComposeMetadataItem(
                    row.RelativeItem(1.45f).PaddingRight(12),
                    "LIEGENSCHAFT",
                    Display(model.PropertyAddress));
                ComposeMetadataItem(
                    row.RelativeItem(0.72f),
                    "STAND",
                    model.Stand.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture));
            });
    }

    private static void ComposeMetadataItem(
        IContainer container,
        string label,
        string value)
    {
        container.Column(column =>
        {
            column.Item().Text(label).FontSize(7.5f).SemiBold().FontColor(MutedTextColor);
            column.Item().PaddingTop(2).Text(value).FontSize(10).SemiBold();
        });
    }

    private static void ComposeSummary(
        IContainer container,
        DossierHoldingListPdfModel model)
    {
        container.Row(row =>
        {
            ComposeSummaryItem(
                row.RelativeItem().PaddingRight(7),
                "Haltungen",
                model.Holdings.Count.ToString(CultureInfo.InvariantCulture));
            ComposeSummaryItem(
                row.RelativeItem().PaddingHorizontal(3.5f),
                "Gesamtlänge",
                FormatTotalLength(model.Holdings));
            ComposeSummaryItem(
                row.RelativeItem().PaddingLeft(7),
                "Zustandsspanne",
                ResolveConditionSpan(model.Holdings));
        });
    }

    private static void ComposeSummaryItem(
        IContainer container,
        string label,
        string value)
    {
        container
            .Border(1)
            .BorderColor(BorderColor)
            .PaddingHorizontal(10)
            .PaddingVertical(7)
            .Row(row =>
            {
                row.RelativeItem().Text(label).FontColor(MutedTextColor);
                row.AutoItem().Text(value).Bold().FontColor(BrandBlue);
            });
    }

    private static void ComposeEmptyList(IContainer container)
    {
        container
            .Border(1)
            .BorderColor(BorderColor)
            .Background(AlternateRowBackground)
            .Padding(12)
            .Text("Diesem Eigentümerdossier sind keine Haltungen zugeordnet.")
            .FontColor(MutedTextColor);
    }

    private static void ComposeHoldingTable(
        IContainer container,
        IReadOnlyList<DossierHoldingLine> holdings)
    {
        static IContainer HeaderCell(IContainer cell)
            => cell
                .Background(HeaderBackground)
                .PaddingHorizontal(6)
                .PaddingVertical(6)
                .AlignMiddle();

        static IContainer BodyCell(IContainer cell, int index)
            => cell
                .MinHeight(38)
                .Background(index % 2 == 0 ? Colors.White : AlternateRowBackground)
                .BorderBottom(0.6f)
                .BorderColor(BorderColor)
                .PaddingHorizontal(6)
                .PaddingVertical(7)
                .AlignMiddle();

        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.ConstantColumn(28);
                columns.ConstantColumn(135);
                columns.ConstantColumn(70);
                columns.ConstantColumn(52);
                columns.ConstantColumn(55);
                columns.ConstantColumn(55);
                columns.RelativeColumn();
            });

            table.Header(header =>
            {
                header.Cell().Element(HeaderCell).Text("Nr.").SemiBold().FontColor(BrandBlue);
                header.Cell().Element(HeaderCell).Text("Haltung").SemiBold().FontColor(BrandBlue);
                header.Cell().Element(HeaderCell).Text("Material").SemiBold().FontColor(BrandBlue);
                header.Cell().Element(HeaderCell).AlignCenter().Text("DN mm").SemiBold().FontColor(BrandBlue);
                header.Cell().Element(HeaderCell).AlignRight().Text("Länge").SemiBold().FontColor(BrandBlue);
                header.Cell().Element(HeaderCell).AlignCenter().Text("Zustand").SemiBold().FontColor(BrandBlue);
                header.Cell().Element(HeaderCell).Text("Nutzungsart").SemiBold().FontColor(BrandBlue);
            });

            for (var index = 0; index < holdings.Count; index++)
            {
                var line = holdings[index];
                table.Cell().Element(cell => BodyCell(cell, index))
                    .Text((index + 1).ToString(CultureInfo.InvariantCulture));
                table.Cell().Element(cell => BodyCell(cell, index))
                    .Text(Display(line.HoldingName)).SemiBold();
                table.Cell().Element(cell => BodyCell(cell, index))
                    .Text(Display(line.PipeMaterial));
                table.Cell().Element(cell => BodyCell(cell, index)).AlignCenter()
                    .Text(Display(line.NominalDiameterMm));
                table.Cell().Element(cell => BodyCell(cell, index)).AlignRight()
                    .Text(FormatLength(line.LengthMeters));
                table.Cell().Element(cell => BodyCell(cell, index))
                    .Element(cell => ComposeCondition(cell, line.ConditionClass));
                table.Cell().Element(cell => BodyCell(cell, index))
                    .Element(cell => ComposeUsageType(cell, line.UsageType));
            }
        });
    }

    private static void ComposeCondition(IContainer container, string? value)
    {
        var normalized = NormalizeConditionValue(value);
        var colors = ResolveConditionColors(value);
        if (normalized is null || colors is null)
        {
            container
                .Background("#EEF1F3")
                .PaddingHorizontal(3)
                .PaddingVertical(5)
                .AlignCenter()
                .Text(MissingText)
                .FontSize(6.75f)
                .FontColor(MutedTextColor);
            return;
        }

        container
            .Background(colors.Value.Background)
            .PaddingHorizontal(4)
            .PaddingVertical(5)
            .AlignCenter()
            .Text("Z" + normalized.Value.ToString(CultureInfo.InvariantCulture))
            .Bold()
            .FontColor(colors.Value.Foreground);
    }

    private static void ComposeUsageType(IContainer container, string? value)
    {
        var display = NutzungsartVokabular.Normalisieren(value);
        var colors = NutzungsartReportColors.Resolve(display);

        container
            .Background(colors.Light)
            .BorderLeft(4)
            .BorderColor(colors.Accent)
            .PaddingHorizontal(5)
            .PaddingVertical(5)
            .Text(Display(display))
            .FontSize(6.75f)
            .SemiBold()
            .FontColor(colors.Accent);
    }

    private static void ComposeLegend(IContainer container)
    {
        container
            .Border(1)
            .BorderColor("#B8CBD5")
            .PaddingHorizontal(10)
            .PaddingVertical(8)
            .Column(legend =>
            {
                legend.Item().Text("Farblegende").FontSize(11).Bold().FontColor(BrandBlue);
                legend.Item().PaddingTop(5).Row(row =>
                {
                    row.ConstantItem(82).Text("Zustandsklasse").SemiBold();
                    foreach (var definition in DossierConditionClassDefinitions.All)
                    {
                        var colors = ResolveConditionColors(definition.Value)!.Value;
                        row.RelativeItem().PaddingRight(4).Column(item =>
                        {
                            item.Item()
                                .Background(colors.Background)
                                .PaddingVertical(3)
                                .AlignCenter()
                                .Text(definition.Code)
                                .FontSize(8)
                                .SemiBold()
                                .FontColor(colors.Foreground);
                            item.Item().PaddingTop(2).AlignCenter()
                                .Text(definition.Name.ToLowerInvariant())
                                .FontSize(6.25f)
                                .FontColor(MutedTextColor);
                        });
                    }
                });

                legend.Item().PaddingTop(6).Row(row =>
                {
                    row.ConstantItem(82).Text("Nutzungsart").SemiBold();
                    ComposeUsageLegendItem(row.RelativeItem().PaddingRight(4), "Schmutzabwasser");
                    ComposeUsageLegendItem(row.RelativeItem().PaddingRight(4), "Niederschlagsabwasser");
                    ComposeUsageLegendItem(row.RelativeItem().PaddingRight(4), "Mischabwasser");
                    ComposeUsageLegendItem(row.RelativeItem(), "Andere / unbekannt");
                });
            });
    }

    private static void ComposeUsageLegendItem(IContainer container, string value)
    {
        var colors = NutzungsartReportColors.Resolve(value);
        container
            .Background(colors.Light)
            .BorderLeft(4)
            .BorderColor(colors.Accent)
            .PaddingHorizontal(5)
            .PaddingVertical(4)
            .Text(value)
            .FontSize(6.75f)
            .SemiBold()
            .FontColor(colors.Accent);
    }

    private static void ComposeExplanation(IContainer container, int missingHoldingCount)
    {
        container
            .Border(1)
            .BorderColor("#B8CBD5")
            .Background("#EEF5F8")
            .PaddingHorizontal(10)
            .PaddingVertical(8)
            .Column(note =>
            {
                note.Item()
                    .Text("Darstellung im fertigen Dossier")
                    .FontSize(10.5f)
                    .Bold()
                    .FontColor(BrandBlue);
                note.Item().PaddingTop(3).Text(
                        "Die Liste wird aus den dem Eigentümer zugeordneten Haltungen aufgebaut. "
                        + "Material, Durchmesser, Länge, Zustandsklasse und Nutzungsart stammen aus "
                        + "den gespeicherten Leitungsdaten. Nicht vorhandene Angaben werden als "
                        + "„nicht erfasst“ ausgewiesen.")
                    .FontSize(8)
                    .FontColor(MutedTextColor);

                if (missingHoldingCount > 0)
                {
                    var text = missingHoldingCount == 1
                        ? "Eine gespeicherte Haltungszuordnung ist im aktuellen Projekt nicht mehr vorhanden."
                        : $"{missingHoldingCount} gespeicherte Haltungszuordnungen sind im aktuellen Projekt nicht mehr vorhanden.";
                    note.Item().PaddingTop(4).Text(text).FontSize(8).SemiBold().FontColor("#9A6700");
                }
            });
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

    private static void ComposeFooter(IContainer container)
    {
        container.Column(footer =>
        {
            footer.Item().LineHorizontal(0.6f).LineColor(BorderColor);
            footer.Item().PaddingTop(4).Row(row =>
            {
                row.RelativeItem()
                    .Text("Haltungsliste zum Eigentümerdossier")
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
            // Die Liste bleibt auch ohne optionale Bildmarken vollstaendig.
            return null;
        }
    }

    private static int? NormalizeConditionValue(string? value)
    {
        var text = (value ?? string.Empty).Trim();
        if (text.StartsWith('Z') || text.StartsWith('z'))
            text = text[1..].Trim();

        return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            && parsed is >= 0 and <= 4
                ? parsed
                : null;
    }

    private static string Display(string? value)
        => string.IsNullOrWhiteSpace(value) ? MissingText : value.Trim();
}
