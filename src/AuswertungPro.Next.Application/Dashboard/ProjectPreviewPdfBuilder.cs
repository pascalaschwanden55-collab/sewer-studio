using System.Globalization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace AuswertungPro.Next.Application.Dashboard;

public static class ProjectPreviewPdfBuilder
{
    private static readonly CultureInfo Ch = CultureInfo.GetCultureInfo("de-CH");

    public static byte[] Build(ProjectPreview preview)
    {
        ArgumentNullException.ThrowIfNull(preview);
        QuestPDF.Settings.License = LicenseType.Community;

        var stats = preview.Statistics;
        var brand = "#2563EB";

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(28);
                page.Size(PageSizes.A4);
                page.DefaultTextStyle(x => x.FontSize(9).FontColor("#111827"));

                page.Header().Column(header =>
                {
                    header.Item().Text("Projektvorschau").FontSize(17).Bold();
                    header.Item().PaddingTop(2).Text(preview.Name).FontSize(11).FontColor("#4B5563");
                    if (!string.IsNullOrWhiteSpace(preview.Path))
                        header.Item().Text(preview.Path).FontSize(7).FontColor("#6B7280");
                    header.Item().PaddingTop(5).LineHorizontal(0.5f).LineColor("#D1D5DB");
                });

                page.Content().PaddingTop(10).Column(col =>
                {
                    col.Spacing(8);

                    col.Item().Element(c => ComposeSection(c, "Stammdaten", brand));
                    col.Item().Element(c => ComposeKeyValueTable(c, BuildMetadata(preview)));

                    col.Item().Element(c => ComposeSection(c, "Kennzahlen", brand));
                    col.Item().Element(c => ComposeKeyValueTable(c, BuildSummary(preview)));

                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Column(left =>
                        {
                            left.Item().Element(c => ComposeSection(c, "Zustand Haltungen", brand));
                            left.Item().Element(c => ComposeConditionTable(c, stats.Haltungen.Buckets));
                        });
                        row.ConstantItem(10);
                        row.RelativeItem().Column(right =>
                        {
                            right.Item().Element(c => ComposeSection(c, "Zustand Schaechte", brand));
                            right.Item().Element(c => ComposeConditionTable(c, stats.Schaechte.Buckets));
                        });
                    });

                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Column(left =>
                        {
                            left.Item().Element(c => ComposeSection(c, "Haeufigste Schaeden", brand));
                            left.Item().Element(c => ComposeBucketTable(c, stats.TopSchaeden, "Schaden"));
                        });
                        row.ConstantItem(10);
                        row.RelativeItem().Column(right =>
                        {
                            right.Item().Element(c => ComposeSection(c, "Haltungskosten nach DN", brand));
                            right.Item().Element(c => ComposeCostTable(c, stats.HaltungDnCosts));
                        });
                    });
                });

                page.Footer().Column(footer =>
                {
                    footer.Item().LineHorizontal(0.5f).LineColor("#D1D5DB");
                    footer.Item().PaddingTop(3).Row(row =>
                    {
                        row.RelativeItem().Text($"Erstellt: {DateTime.Now:dd.MM.yyyy HH:mm}")
                            .FontSize(8).FontColor(Colors.Grey.Darken2);
                        row.AutoItem().Text(x =>
                        {
                            x.DefaultTextStyle(t => t.FontSize(8).FontColor(Colors.Grey.Darken2));
                            x.Span("Seite ");
                            x.CurrentPageNumber();
                            x.Span(" von ");
                            x.TotalPages();
                        });
                    });
                });
            });
        }).GeneratePdf();
    }

    private static IReadOnlyList<(string Label, string Value)> BuildMetadata(ProjectPreview preview)
    {
        var items = new List<(string Label, string Value)>();
        Add(items, "Auftraggeber", preview.Auftraggeber);
        Add(items, "Gemeinde", preview.Gemeinde);
        Add(items, "Zone", preview.Zone);
        Add(items, "Strasse", preview.Strasse);
        Add(items, "Bearbeiter", preview.Bearbeiter);
        Add(items, "Inspektionsdatum", preview.Inspektionsdatum);
        Add(items, "Auftrag-Nr", preview.AuftragNr);
        Add(items, "Firma", preview.Firma);
        return items.Count == 0 ? [("Stammdaten", "Nicht definiert")] : items;
    }

    private static IReadOnlyList<(string Label, string Value)> BuildSummary(ProjectPreview preview)
    {
        var stats = preview.Statistics;
        return
        [
            ("Haltungen", stats.HoldingCount.ToString("N0", Ch)),
            ("Schaechte", stats.SchachtCount.ToString("N0", Ch)),
            ("Gesamtlaenge", $"{stats.TotalLengthMeters:N0} m"),
            ("Sanierungskosten", $"{stats.TotalCost.ToString("N0", Ch)} CHF"),
            ("Haltungen sanieren", $"{stats.SanierenHaltungen:N0} / {stats.HaltungenGesamt:N0}"),
            ("Schaechte mit Massnahmen", stats.SchaechteMitMassnahmen.ToString("N0", Ch)),
            ("Dringend (Z0/Z1)", stats.DringendCount.ToString("N0", Ch)),
            ("Zustand unbekannt (ZU)", stats.OhneZustandCount.ToString("N0", Ch))
        ];
    }

    private static void ComposeSection(IContainer container, string title, string brand)
    {
        container.Border(0.5f).BorderColor("#D1D5DB").Row(row =>
        {
            row.ConstantItem(3).Background(brand);
            row.RelativeItem()
                .Background("#EFF6FF")
                .PaddingVertical(4)
                .PaddingHorizontal(8)
                .Text(title)
                .FontSize(10)
                .Bold();
        });
    }

    private static void ComposeKeyValueTable(IContainer container, IReadOnlyList<(string Label, string Value)> items)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.ConstantColumn(115);
                columns.RelativeColumn();
                columns.ConstantColumn(115);
                columns.RelativeColumn();
            });

            for (var i = 0; i < items.Count; i += 2)
            {
                var left = items[i];
                var right = i + 1 < items.Count ? items[i + 1] : (Label: "", Value: "");
                ComposePair(table, left);
                ComposePair(table, right);
            }
        });
    }

    private static void ComposePair(TableDescriptor table, (string Label, string Value) pair)
    {
        table.Cell().Element(DataCell).Text(pair.Label).SemiBold();
        table.Cell().Element(DataCell).Text(pair.Value);
    }

    private static void ComposeConditionTable(IContainer container, IReadOnlyList<ZustandBucket> buckets)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn();
                columns.ConstantColumn(50);
                columns.ConstantColumn(50);
            });
            ComposeHeader(table, "Klasse", "Anzahl", "%");

            foreach (var bucket in buckets)
            {
                table.Cell().Element(DataCell).Text(bucket.Label);
                table.Cell().Element(DataCell).AlignRight().Text(bucket.Count.ToString("N0", Ch));
                table.Cell().Element(DataCell).AlignRight().Text(bucket.Percent.ToString("N1", Ch));
            }
        });
    }

    private static void ComposeBucketTable(IContainer container, IReadOnlyList<DashboardBucket> buckets, string labelHeader)
    {
        if (buckets.Count == 0)
        {
            container.Element(EmptyCell).Text("Keine Schaeden vorhanden.");
            return;
        }

        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn();
                columns.ConstantColumn(50);
                columns.ConstantColumn(50);
            });
            ComposeHeader(table, labelHeader, "Anzahl", "%");

            foreach (var bucket in buckets)
            {
                table.Cell().Element(DataCell).Text(bucket.Label);
                table.Cell().Element(DataCell).AlignRight().Text(bucket.Count.ToString("N0", Ch));
                table.Cell().Element(DataCell).AlignRight().Text(bucket.Percent.ToString("N1", Ch));
            }
        });
    }

    private static void ComposeCostTable(IContainer container, IReadOnlyList<DashboardCostBucket> buckets)
    {
        if (buckets.Count == 0)
        {
            container.Element(EmptyCell).Text("Keine Kosten vorhanden.");
            return;
        }

        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn();
                columns.ConstantColumn(45);
                columns.ConstantColumn(75);
                columns.ConstantColumn(45);
            });
            ComposeHeader(table, "DN", "Anzahl", "CHF", "%");

            foreach (var bucket in buckets)
            {
                table.Cell().Element(DataCell).Text(bucket.Label);
                table.Cell().Element(DataCell).AlignRight().Text(bucket.Count.ToString("N0", Ch));
                table.Cell().Element(DataCell).AlignRight().Text(bucket.Cost.ToString("N0", Ch));
                table.Cell().Element(DataCell).AlignRight().Text(bucket.Percent.ToString("N1", Ch));
            }
        });
    }

    private static void ComposeHeader(TableDescriptor table, params string[] labels)
    {
        foreach (var label in labels)
            table.Cell().Element(HeaderCell).Text(label).SemiBold();
    }

    private static IContainer HeaderCell(IContainer container)
        => container.Background("#F3F4F6").BorderBottom(0.5f).BorderColor("#D1D5DB").PaddingVertical(4).PaddingHorizontal(4);

    private static IContainer DataCell(IContainer container)
        => container.BorderBottom(0.5f).BorderColor("#E5E7EB").PaddingVertical(3).PaddingHorizontal(4);

    private static IContainer EmptyCell(IContainer container)
        => container.Border(0.5f).BorderColor("#E5E7EB").Padding(8);

    private static void Add(List<(string Label, string Value)> items, string label, string? value)
    {
        var text = (value ?? string.Empty).Trim();
        if (text.Length > 0)
            items.Add((label, text));
    }
}
