using System.Globalization;

using AuswertungPro.Next.Application.Dossiers;
using AuswertungPro.Next.Application.Reports;

using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace AuswertungPro.Next.Infrastructure.Dossiers;

/// <summary>
/// Gemeinsame Bausteine der Dossier-PDF-Dienste (Haltungsliste, Schachtliste, Zustandsklassen):
/// Seitenkopf mit Logo und Wappen, Zustandsklassen-Zelle und Metadaten-Kasten. Diese Teile sollen in
/// allen Dossier-PDFs gleich aussehen und stehen deshalb an einer Stelle (Deepscan 02.10.2026, B5).
/// </summary>
internal static class DossierPdfBausteine
{
    internal const string BrandBlue = "#005C84";
    internal const string MutedTextColor = "#64748B";
    internal const string SoftBackground = "#F3F6F8";
    internal const string MissingText = "nicht erfasst";

    /// <summary>Wert fuer Anzeigefelder: leer wird zu "nicht erfasst", sonst getrimmt.</summary>
    internal static string Display(string? value)
        => string.IsNullOrWhiteSpace(value) ? MissingText : value.Trim();

    /// <summary>Hintergrund- und Schriftfarbe der Zustandsklasse; null bei unbekanntem Wert.</summary>
    internal static (string Background, string Foreground)? ResolveConditionColors(string? value)
    {
        var normalized = DossierConditionClassValue.Normalize(value);
        if (normalized is null)
            return null;

        var colors = ProtocolPdfExporter.ResolveZustandsklassenFarbe(
            normalized.Value.ToString(CultureInfo.InvariantCulture));
        return colors is null
            ? null
            : (colors.Value.Hintergrund, colors.Value.Schrift);
    }

    /// <summary>
    /// Seitenkopf: Logo links (ohne Logo der Schriftzug "ABWASSER URI"), Wappen rechts. Fehlt das Wappen,
    /// steht an seiner Stelle der optionale Ersatztext (Schachtliste: "SCHACHTLISTE").
    /// </summary>
    internal static void ComposeHeader(
        IContainer container,
        byte[]? logo,
        byte[]? coatOfArms,
        float coatOfArmsWidth = 42,
        string? coatOfArmsFallbackText = null)
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

            row.ConstantItem(coatOfArmsWidth).Height(48).AlignRight().AlignMiddle().Element(right =>
            {
                if (coatOfArms is not null)
                    right.Image(coatOfArms).FitArea();
                else if (coatOfArmsFallbackText is not null)
                    right.Text(coatOfArmsFallbackText)
                        .FontSize(7.5f)
                        .SemiBold()
                        .FontColor(MutedTextColor);
            });
        });
    }

    /// <summary>Zustandsklassen-Zelle der Listen: farbig "Z" + Klasse, ohne gueltigen Wert grau "nicht erfasst".</summary>
    internal static void ComposeCondition(IContainer container, string? value)
    {
        var normalized = DossierConditionClassValue.Normalize(value);
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

    /// <summary>Kasten mit Eigentuemer, Liegenschaft und Stand.</summary>
    internal static void ComposeMetadata(
        IContainer container,
        string? ownerName,
        string? propertyAddress,
        DateTime stand)
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
                    Display(ownerName));
                ComposeMetadataItem(
                    row.RelativeItem(1.45f).PaddingRight(12),
                    "LIEGENSCHAFT",
                    Display(propertyAddress));
                ComposeMetadataItem(
                    row.RelativeItem(0.72f),
                    "STAND",
                    stand.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture));
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
}
