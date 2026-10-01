using System.Globalization;
using System.Text;
using AuswertungPro.Next.Application.Costs;
using AuswertungPro.Next.Infrastructure.Export.Excel;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace AuswertungPro.Next.Application.Dashboard;

public static class ProjectPreviewPdfBuilder
{
    private static readonly CultureInfo Ch = CultureInfo.GetCultureInfo("de-CH");

    private const string Brand = "#2563EB";
    private const string TextColor = "#0F172A";
    private const string MutedText = "#475569";
    private const string SubtleText = "#64748B";
    private const string Border = "#CBD5E1";
    private const string Track = "#CBD5E1";
    private const string CardBackground = "#FFFFFF";
    private const string SoftBackground = "#F8FAFC";

    private static readonly IReadOnlyDictionary<string, string> ZustandColors =
        ExcelReportStyle.Zustandsklassen
            .ToDictionary(
                rule => rule.Wert,
                rule => $"#{rule.Farbe[2..]}",
                StringComparer.Ordinal)
            .Append(new KeyValuePair<string, string>("ohne", "#9CA3AF"))
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);

    private static readonly string[] AccentPalette =
    [
        "#2D8CC8",
        "#70AD47",
        "#FF6B00",
        "#7E57C2",
        "#009688",
        "#C62828",
        "#455A64",
        "#8E6C32"
    ];

    public static byte[] Build(ProjectPreview preview)
    {
        ArgumentNullException.ThrowIfNull(preview);
        QuestPDF.Settings.License = LicenseType.Community;

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(24);
                page.Size(PageSizes.A4);
                page.DefaultTextStyle(x => x.FontSize(9).FontColor(TextColor));

                page.Header().Element(c => ComposeReportHeader(c, preview, "Haltungen"));
                page.Content().PaddingTop(10).Element(c => ComposeHaltungenPage(c, preview));
                page.Footer().Element(ComposeFooter);
            });

            container.Page(page =>
            {
                page.Margin(24);
                page.Size(PageSizes.A4);
                page.DefaultTextStyle(x => x.FontSize(9).FontColor(TextColor));

                page.Header().Element(c => ComposeReportHeader(c, preview, "Schächte"));
                page.Content().PaddingTop(10).Element(c => ComposeSchaechtePage(c, preview));
                page.Footer().Element(ComposeFooter);
            });
        }).GeneratePdf();
    }

    private static void ComposeHaltungenPage(IContainer container, ProjectPreview preview)
    {
        var stats = preview.Statistics;

        container.Column(col =>
        {
            col.Spacing(9);

            col.Item().Element(c => ComposeMetadataCard(c, preview, BuildProjectMetadata(preview)));

            col.Item().Row(row =>
            {
                row.RelativeItem().Element(c => ComposeMetricCard(c, FormatNumber(stats.HoldingCount), "Haltungen", Brand));
                row.ConstantItem(8);
                row.RelativeItem().Element(c => ComposeMetricCard(c, $"{stats.TotalLengthMeters.ToString("N0", Ch)} m", "Gesamtlänge", TextColor));
                row.ConstantItem(8);
                row.RelativeItem().Element(c => ComposeMetricCard(
                    c,
                    $"{FormatNumber(stats.SanierenHaltungen)} / {FormatNumber(stats.HaltungenGesamt)}",
                    "Haltungen sanieren",
                    TextColor));
            });

            col.Item().Element(c => ComposeConditionCard(
                c,
                "Zustand Haltungen",
                stats.Haltungen.Buckets,
                stats.Haltungen.Total,
                "Haltungen"));

            col.Item().Element(c => ComposeDamageCard(c, stats.TopSchaeden));
            col.Item().Element(c => ComposeDnCostCard(c, stats.HaltungDnCosts));

            // Kosten und Verfahren nebeneinander: spart die halbe Hoehe und haelt den
            // Ausdruck bei zwei Seiten. ShowEntire verhindert, dass ein Block mitten
            // durchbricht (der Kostenblock stand sonst zerrissen auf zwei Seiten).
            col.Item().ShowEntire().Row(row =>
            {
                row.RelativeItem().Element(c => ComposeKostenCard(c, stats));

                if (stats.HasVerfahren)
                {
                    row.ConstantItem(8);
                    row.RelativeItem().Element(c => ComposeVerfahrenCard(c, stats.Sanierungsverfahren));
                }
            });
        });
    }

    /// <summary>
    /// Sanierungskosten gegliedert nach Haltungen und Schaechten. Alle Betraege sind
    /// Nettobetraege — die MWST kommt erst in der Kostenzusammenstellung dazu.
    /// Deshalb steht "ohne MWST" ausdruecklich in der Ueberschrift.
    /// </summary>
    private static void ComposeKostenCard(IContainer container, DashboardStatistics stats)
    {
        container
            .Border(0.7f)
            .BorderColor(Border)
            .Background(CardBackground)
            .Padding(13)
            .Column(col =>
            {
                col.Spacing(6);
                col.Item().Text("Sanierungskosten (ohne MWST)").FontSize(11).SemiBold().FontColor(TextColor);

                col.Item().Element(c => ComposeKostenZeile(c, "Haltungen", stats.HaltungSanierungsKosten, false));
                col.Item().Element(c => ComposeKostenZeile(c, "Schächte", stats.SchachtSanierungsKosten, false));

                col.Item().PaddingTop(3).BorderTop(0.7f).BorderColor(Border).PaddingTop(5)
                    .Element(c => ComposeKostenZeile(c, "Total", stats.TotalCost, true));
            });
    }

    private static void ComposeKostenZeile(IContainer container, string label, decimal betrag, bool hervorheben)
    {
        container.Row(row =>
        {
            var textStyle = row.RelativeItem().AlignMiddle().Text(label).FontSize(9);
            if (hervorheben)
                textStyle.SemiBold().FontColor(TextColor);
            else
                textStyle.FontColor(MutedText);

            var betragStyle = row.ConstantItem(95).AlignRight().AlignMiddle()
                .Text(FormatCurrency(betrag)).FontSize(9);
            if (hervorheben)
                betragStyle.Bold().FontColor(TextColor);
            else
                betragStyle.FontColor(TextColor);
        });
    }

    /// <summary>
    /// Mengen der Sanierungsverfahren (Liner, Kurzliner, Manschetten) aus den
    /// ausgewaehlten Kostenzeilen der Haltungen. Schachtpositionen sind bewusst
    /// nicht enthalten — sonst stuenden Rohrmeter und Schachtstueck in einer Zeile.
    /// </summary>
    private static void ComposeVerfahrenCard(
        IContainer container,
        IReadOnlyList<RehabilitationQuantity> verfahren)
    {
        container
            .Border(0.7f)
            .BorderColor(Border)
            .Background(CardBackground)
            .Padding(13)
            .Column(col =>
            {
                col.Spacing(6);
                col.Item().Text("Sanierungsverfahren").FontSize(11).SemiBold().FontColor(TextColor);

                foreach (var eintrag in verfahren)
                {
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().AlignMiddle().Text(eintrag.Label).FontSize(8.5f).FontColor(TextColor);
                        row.ConstantItem(80).AlignRight().AlignMiddle()
                            .Text(eintrag.QtyText).FontSize(8.5f).SemiBold().FontColor(TextColor);
                        row.ConstantItem(70).AlignRight().AlignMiddle()
                            .Text(eintrag.NetText).FontSize(8.5f).FontColor(MutedText);
                    });
                }

                col.Item().PaddingTop(2)
                    .Text("Mengen aus den ausgewählten Kostenzeilen der Haltungen (CHF ohne MWST).")
                    .FontSize(7).FontColor(SubtleText);
            });
    }

    private static void ComposeSchaechtePage(IContainer container, ProjectPreview preview)
    {
        var stats = preview.Statistics;
        var schachtDringend = CountConditions(stats.Schaechte.Buckets, "0", "1");
        var schachtUnbekannt = CountConditions(stats.Schaechte.Buckets, "ohne");

        container.Column(col =>
        {
            col.Spacing(9);

            col.Item().Element(c => ComposeMetadataCard(c, preview, BuildLocationMetadata(preview)));

            col.Item().Row(row =>
            {
                row.RelativeItem().Element(c => ComposeMetricCard(c, FormatNumber(stats.SchachtCount), "Schächte", TextColor));
                row.ConstantItem(8);
                row.RelativeItem().Element(c => ComposeMetricCard(
                    c,
                    $"{FormatNumber(stats.SchaechteSanierenJa)} / {FormatNumber(stats.SchaechteGesamt)}",
                    "Schächte sanieren",
                    TextColor));
                row.ConstantItem(8);
                row.RelativeItem().Element(c => ComposeMetricCard(
                    c,
                    FormatCurrency(stats.SchachtSanierungsKosten),
                    "Kosten Schachtsanierung (ohne MWST)",
                    TextColor));
            });

            col.Item().Element(c => ComposeConditionCard(
                c,
                "Zustand Schächte",
                stats.Schaechte.Buckets,
                stats.Schaechte.Total,
                "Schächte"));

            col.Item().Element(c => ComposeSchachtSummaryCard(
                c,
                stats.SchachtCount,
                schachtDringend,
                schachtUnbekannt,
                stats.SchaechteSanierenJa));

            col.Item().Row(row =>
            {
                row.RelativeItem().Element(c => ComposeMetricCard(c, FormatNumber(schachtDringend), "Dringend Schächte (Z0/Z1)", "#EF0000"));
                row.ConstantItem(8);
                row.RelativeItem().Element(c => ComposeMetricCard(c, FormatNumber(schachtUnbekannt), "Zustand unbekannt (ZU)", "#475569"));
                row.ConstantItem(8);
                row.RelativeItem().Element(c => ComposeMetricCard(c, FormatCurrency(stats.TotalCost), "Sanierungskosten gesamt (ohne MWST)", TextColor));
            });
        });
    }

    private static void ComposeReportHeader(IContainer container, ProjectPreview preview, string scope)
    {
        container.Column(col =>
        {
            col.Item().Row(row =>
            {
                row.RelativeItem().Column(left =>
                {
                    left.Item().Text("Projektübersicht").FontSize(18).Bold().FontColor(TextColor);
                    left.Item().PaddingTop(2).Text(NonEmpty(preview.Name, "Projekt")).FontSize(10).FontColor(MutedText);
                });

                row.AutoItem().AlignRight().Column(right =>
                {
                    right.Item().AlignRight().Text(scope).FontSize(10).SemiBold().FontColor(Brand);
                    right.Item().AlignRight().Text($"Erstellt {DateTime.Now:dd.MM.yyyy HH:mm}").FontSize(7).FontColor(SubtleText);
                });
            });

            col.Item().PaddingTop(7).LineHorizontal(0.6f).LineColor(Border);
        });
    }

    private static void ComposeFooter(IContainer container)
    {
        container.Column(footer =>
        {
            footer.Item().LineHorizontal(0.5f).LineColor(Border);
            footer.Item().PaddingTop(3).Row(row =>
            {
                row.RelativeItem().Text("SewerStudio Cockpit").FontSize(7).FontColor(SubtleText);
                row.AutoItem().Text(x =>
                {
                    x.DefaultTextStyle(t => t.FontSize(7).FontColor(SubtleText));
                    x.Span("Seite ");
                    x.CurrentPageNumber();
                    x.Span(" von ");
                    x.TotalPages();
                });
            });
        });
    }

    private static void ComposeMetadataCard(
        IContainer container,
        ProjectPreview preview,
        IReadOnlyList<(string Label, string Value)> items)
    {
        container
            .Border(0.7f)
            .BorderColor(Border)
            .Background(CardBackground)
            .Padding(12)
            .Column(col =>
            {
                col.Spacing(8);

                col.Item().Row(row =>
                {
                    row.RelativeItem().Text(NonEmpty(preview.Auftraggeber, NonEmpty(preview.Gemeinde, preview.Name)))
                        .FontSize(11)
                        .SemiBold()
                        .FontColor(TextColor);

                    if (!string.IsNullOrWhiteSpace(preview.ModifiedAtDisplay) && preview.ModifiedAtDisplay != "-")
                        row.AutoItem().Text($"Geändert {preview.ModifiedAtDisplay}").FontSize(7).FontColor(SubtleText);
                });

                if (items.Count == 0)
                {
                    col.Item().Text("Keine Stammdaten hinterlegt.").FontSize(8).FontColor(SubtleText);
                    return;
                }

                col.Item().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn();
                        columns.RelativeColumn();
                        columns.RelativeColumn();
                    });

                    foreach (var item in items)
                    {
                        table.Cell().PaddingRight(8).PaddingBottom(4).Column(cell =>
                        {
                            cell.Item().Text(item.Label).FontSize(7).FontColor(SubtleText);
                            cell.Item().Text(item.Value).FontSize(8.5f).FontColor(TextColor);
                        });
                    }
                });
            });
    }

    private static void ComposeMetricCard(IContainer container, string value, string label, string valueColor)
    {
        container
            .MinHeight(58)
            .Border(0.7f)
            .BorderColor(Border)
            .Background(CardBackground)
            .PaddingVertical(12)
            .PaddingHorizontal(13)
            .Column(col =>
            {
                col.Item().Text(value).FontSize(17).Bold().FontColor(valueColor);
                col.Item().PaddingTop(2).Text(label).FontSize(8.5f).FontColor(MutedText);
            });
    }

    private static void ComposeConditionCard(
        IContainer container,
        string title,
        IReadOnlyList<ZustandBucket> buckets,
        int total,
        string centerLabel)
    {
        container
            .Border(0.7f)
            .BorderColor(Border)
            .Background(CardBackground)
            .Padding(13)
            .Column(col =>
            {
                col.Spacing(9);
                col.Item().Text(title).FontSize(11).SemiBold().FontColor(TextColor);

                col.Item().Row(row =>
                {
                    row.ConstantItem(128).Height(128).Svg(BuildDonutSvg(buckets, total, centerLabel)).FitArea();
                    row.ConstantItem(22);
                    row.RelativeItem().PaddingTop(4).Element(c => ComposeConditionBars(c, buckets));
                });
            });
    }

    private static void ComposeConditionBars(IContainer container, IReadOnlyList<ZustandBucket> buckets)
    {
        container.Column(col =>
        {
            col.Spacing(7);

            foreach (var bucket in buckets)
            {
                var color = ResolveZustandColor(bucket.Key);
                col.Item().Row(row =>
                {
                    row.ConstantItem(56).Row(label =>
                    {
                        label.ConstantItem(7).Height(7).AlignMiddle().Background(color);
                        label.ConstantItem(5);
                        label.RelativeItem().AlignMiddle().Text(bucket.Label).FontSize(8).FontColor(TextColor);
                    });
                    row.ConstantItem(10);
                    row.RelativeItem().AlignMiddle().Element(c => ComposeProgressBar(c, bucket.Percent, color, bucket.Count > 0));
                    row.ConstantItem(8);
                    row.ConstantItem(35).AlignRight().Text(bucket.Count.ToString("N0", Ch)).FontSize(8).FontColor(MutedText);
                });
            }
        });
    }

    private static void ComposeDamageCard(IContainer container, IReadOnlyList<DashboardBucket> buckets)
    {
        container
            .Border(0.7f)
            .BorderColor(Border)
            .Background(CardBackground)
            .Padding(13)
            .Column(col =>
            {
                col.Spacing(8);
                col.Item().Text("Häufigste Schäden").FontSize(11).SemiBold().FontColor(TextColor);

                if (buckets.Count == 0)
                {
                    col.Item().Element(c => ComposeEmptyPanel(c, "Keine Schäden in der Vorschau vorhanden."));
                    return;
                }

                for (var i = 0; i < buckets.Count; i++)
                {
                    var bucket = buckets[i];
                    var color = AccentPalette[i % AccentPalette.Length];
                    col.Item().Row(row =>
                    {
                        row.ConstantItem(92).AlignMiddle().Text(bucket.Label).FontSize(7.5f).FontColor(TextColor);
                        row.RelativeItem().AlignMiddle().Element(c => ComposeProgressBar(c, bucket.Percent, color, true));
                        row.ConstantItem(10);
                        row.ConstantItem(42).AlignRight().Text($"{bucket.Percent.ToString("N1", Ch)}%").FontSize(7.5f).FontColor(TextColor);
                    });
                }
            });
    }

    private static void ComposeDnCostCard(IContainer container, IReadOnlyList<DashboardCostBucket> buckets)
    {
        container
            .Border(0.7f)
            .BorderColor(Border)
            .Background(CardBackground)
            .Padding(13)
            .Column(col =>
            {
                col.Spacing(8);
                col.Item().Text("Haltungskosten nach DN (ohne MWST)").FontSize(11).SemiBold().FontColor(TextColor);

                if (buckets.Count == 0)
                {
                    col.Item().Element(c => ComposeEmptyPanel(c, "Keine DN-Kosten in der Vorschau vorhanden."));
                    return;
                }

                var visible = buckets.Take(12).ToList();
                var maxCost = visible.Max(b => b.Cost);

                col.Item().Height(112).Row(row =>
                {
                    for (var i = 0; i < visible.Count; i++)
                    {
                        var bucket = visible[i];
                        row.RelativeItem().Element(c => ComposeDnBar(c, bucket, maxCost));

                        if (i < visible.Count - 1)
                            row.ConstantItem(5);
                    }
                });

                if (visible.Count < buckets.Count)
                    col.Item().Text($"+ {buckets.Count - visible.Count:N0} weitere DN-Gruppen").FontSize(7).FontColor(SubtleText);
            });
    }

    private static void ComposeDnBar(IContainer container, DashboardCostBucket bucket, decimal maxCost)
    {
        var fillHeight = maxCost <= 0m
            ? 2f
            : (float)Math.Max(2d, Math.Round((double)(bucket.Cost / maxCost) * 78d, 1));
        var color = ResolveCostColor(bucket.Percent);

        container.Column(col =>
        {
            col.Item().Height(18).AlignCenter().Text(bucket.Cost.ToString("N0", Ch)).FontSize(6.5f).FontColor(MutedText);
            col.Item().Height(80).AlignBottom().Element(c =>
                c.Height(fillHeight).Background(bucket.Cost > 0m ? color : "#E2E8F0"));
            col.Item().PaddingTop(4).AlignCenter().Text(bucket.Label).FontSize(6.7f).FontColor(TextColor);
        });
    }

    private static void ComposeSchachtSummaryCard(
        IContainer container,
        int schachtCount,
        int schachtDringend,
        int schachtUnbekannt,
        int schaechteSanierenJa)
    {
        var rows = new[]
        {
            (Label: "Dringend (Z0/Z1)", Value: schachtDringend, Color: "#EF0000"),
            (Label: "Zustand unbekannt (ZU)", Value: schachtUnbekannt, Color: "#9CA3AF"),
            (Label: "Sanieren: Ja", Value: schaechteSanierenJa, Color: Brand),
            (Label: "Sanieren: Nein", Value: Math.Max(0, schachtCount - schaechteSanierenJa), Color: "#64748B")
        };

        container
            .Border(0.7f)
            .BorderColor(Border)
            .Background(CardBackground)
            .Padding(13)
            .Column(col =>
            {
                col.Spacing(8);
                col.Item().Text("Schacht-Zusammenfassung").FontSize(11).SemiBold().FontColor(TextColor);

                foreach (var rowData in rows)
                {
                    var percent = Percent(rowData.Value, schachtCount);
                    col.Item().Row(row =>
                    {
                        row.ConstantItem(115).AlignMiddle().Text(rowData.Label).FontSize(8).FontColor(TextColor);
                        row.RelativeItem().AlignMiddle().Element(c => ComposeProgressBar(c, percent, rowData.Color, rowData.Value > 0));
                        row.ConstantItem(8);
                        row.ConstantItem(55).AlignRight().Text(FormatNumber(rowData.Value)).FontSize(8).FontColor(TextColor);
                    });
                }
            });
    }

    private static void ComposeProgressBar(IContainer container, double percent, string color, bool hasValue)
    {
        var filled = (float)Math.Clamp(percent, 0.1d, 100d);
        var empty = (float)Math.Clamp(100d - percent, 0.1d, 100d);

        container.Height(5).Row(row =>
        {
            row.RelativeItem(filled).Background(hasValue ? color : Track);
            row.RelativeItem(empty).Background(Track);
        });
    }

    private static void ComposeEmptyPanel(IContainer container, string text)
    {
        container
            .Background(SoftBackground)
            .Border(0.5f)
            .BorderColor("#E2E8F0")
            .Padding(10)
            .Text(text)
            .FontSize(8)
            .FontColor(SubtleText);
    }

    private static IReadOnlyList<(string Label, string Value)> BuildProjectMetadata(ProjectPreview preview)
    {
        var items = new List<(string Label, string Value)>();
        Add(items, "Auftraggeber", preview.Auftraggeber);
        Add(items, "Gemeinde", preview.Gemeinde);
        Add(items, "Zone", preview.Zone);
        Add(items, "Strasse", preview.Strasse);
        Add(items, "Bearbeiter", preview.Bearbeiter);
        Add(items, "Inspektionsdatum", preview.Inspektionsdatum);
        return items;
    }

    private static IReadOnlyList<(string Label, string Value)> BuildLocationMetadata(ProjectPreview preview)
    {
        var items = new List<(string Label, string Value)>();
        Add(items, "Gemeinde", preview.Gemeinde);
        Add(items, "Strasse", preview.Strasse);
        Add(items, "Zone", preview.Zone);
        Add(items, "Inspektionsdatum", preview.Inspektionsdatum);
        Add(items, "Auftrag-Nr", preview.AuftragNr);
        Add(items, "Firma", preview.Firma);
        return items.Count == 0 ? BuildProjectMetadata(preview) : items;
    }

    private static string BuildDonutSvg(IReadOnlyList<ZustandBucket> buckets, int total, string centerLabel)
    {
        const double center = 60d;
        const double radius = 40d;
        const double stroke = 22d;
        var circumference = 2d * Math.PI * radius;
        var offset = 0d;
        var svg = new StringBuilder();

        svg.Append("<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 120 120'>");
        svg.Append("<rect width='120' height='120' fill='white'/>");
        svg.Append($"<circle cx='{Svg(center)}' cy='{Svg(center)}' r='{Svg(radius)}' fill='none' stroke='#E2E8F0' stroke-width='{Svg(stroke)}'/>");

        if (total > 0)
        {
            foreach (var bucket in buckets.Where(b => b.Count > 0))
            {
                var length = circumference * bucket.Count / total;
                var gap = Math.Min(1.2d, length * 0.18d);
                var visibleLength = Math.Max(0d, length - gap);
                svg.Append(
                    $"<circle cx='{Svg(center)}' cy='{Svg(center)}' r='{Svg(radius)}' fill='none' " +
                    $"stroke='{ResolveZustandColor(bucket.Key)}' stroke-width='{Svg(stroke)}' " +
                    $"stroke-dasharray='{Svg(visibleLength)} {Svg(circumference - visibleLength)}' " +
                    $"stroke-dashoffset='{Svg(-offset)}' transform='rotate(-90 {Svg(center)} {Svg(center)})'/>");
                offset += length;
            }
        }

        svg.Append("<circle cx='60' cy='60' r='25' fill='white'/>");
        svg.Append($"<text x='60' y='57' text-anchor='middle' font-size='18' font-weight='700' fill='{TextColor}' font-family='Arial, sans-serif'>{total.ToString("N0", Ch)}</text>");
        svg.Append($"<text x='60' y='72' text-anchor='middle' font-size='8' fill='{MutedText}' font-family='Arial, sans-serif'>{EscapeSvg(centerLabel)}</text>");
        svg.Append("</svg>");

        return svg.ToString();
    }

    private static string ResolveZustandColor(string key)
        => ZustandColors.TryGetValue(key, out var color) ? color : "#9CA3AF";

    private static string ResolveCostColor(double percent)
    {
        if (percent >= 45d)
            return "#A9B42D";
        if (percent >= 20d)
            return "#FFF200";
        if (percent > 0d)
            return "#2563EB";
        return "#E2E8F0";
    }

    private static int CountConditions(IReadOnlyList<ZustandBucket> buckets, params string[] keys)
    {
        var keySet = keys.ToHashSet(StringComparer.Ordinal);
        return buckets.Where(b => keySet.Contains(b.Key)).Sum(b => b.Count);
    }

    private static double Percent(int value, int total)
        => total <= 0 ? 0d : Math.Round(value * 100d / total, 1);

    private static string FormatNumber(int value)
        => value.ToString("N0", Ch);

    private static string FormatCurrency(decimal value)
        => $"{value.ToString("N0", Ch)} CHF";

    private static string NonEmpty(string? value, string fallback)
    {
        var text = (value ?? string.Empty).Trim();
        return text.Length == 0 ? fallback : text;
    }

    private static void Add(List<(string Label, string Value)> items, string label, string? value)
    {
        var text = (value ?? string.Empty).Trim();
        if (text.Length > 0)
            items.Add((label, text));
    }

    private static string Svg(double value)
        => value.ToString("0.###", CultureInfo.InvariantCulture);

    private static string EscapeSvg(string value)
        => value
            .Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal)
            .Replace("'", "&apos;", StringComparison.Ordinal);
}
