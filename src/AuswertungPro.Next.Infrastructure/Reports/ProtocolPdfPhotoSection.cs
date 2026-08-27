using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using static AuswertungPro.Next.Application.Reports.ProtocolPdfObservationText;
using static AuswertungPro.Next.Application.Reports.ProtocolPdfValueFormatting;

namespace AuswertungPro.Next.Application.Reports;

/// <summary>
/// Sammelt, nummeriert und zeichnet die Fotoseiten des Haltungsprotokolls.
/// </summary>
internal static class ProtocolPdfPhotoSection
{
    internal sealed record PhotoItem(ProtocolEntry Entry, string Path);

    internal static List<PhotoItem> BuildItems(
        IProtocolPdfAssetResolver assets,
        IReadOnlyList<ProtocolEntry> entries,
        string projectRootAbs,
        int maxPhotosPerEntry,
        string? preferredFolder = null)
    {
        var items = new List<PhotoItem>();
        var resolveCache = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        var usedPhotoPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in entries)
        {
            if (entry.FotoPaths is null || entry.FotoPaths.Count == 0)
                continue;

            var resolved = assets.ResolvePhotoPaths(
                entry.FotoPaths,
                projectRootAbs,
                maxPhotosPerEntry,
                resolveCache,
                preferredFolder);
            foreach (var path in resolved)
            {
                if (!usedPhotoPaths.Add(NormalizeExportPhotoPathKey(path)))
                    continue;

                items.Add(new PhotoItem(entry, path));
            }
        }

        return items;
    }

    internal static IReadOnlyDictionary<ProtocolEntry, string> BuildNumberMap(
        IReadOnlyList<PhotoItem> photoItems)
    {
        var map = new Dictionary<ProtocolEntry, List<int>>();
        for (var i = 0; i < photoItems.Count; i++)
        {
            var entry = photoItems[i].Entry;
            if (!map.TryGetValue(entry, out var list))
            {
                list = new List<int>();
                map[entry] = list;
            }

            list.Add(i + 1);
        }

        return map.ToDictionary(pair => pair.Key, pair => string.Join(",", pair.Value));
    }

    internal static string ResolveNumberText(
        ProtocolEntry entry,
        IReadOnlyDictionary<ProtocolEntry, string>? photoNumbers)
    {
        if (photoNumbers is null)
            return BuildObservationPhotoText(entry);

        return photoNumbers.TryGetValue(entry, out var numbers) ? numbers : "-";
    }

    internal static void Compose(
        ColumnDescriptor column,
        IReadOnlyList<PhotoItem> photoItems,
        Project project,
        HaltungRecord record,
        string inspectionDate,
        string holdingLabel,
        HaltungsprotokollPdfOptions options,
        IProtocolPdfAssetResolver assets,
        string brand = "#7A8A94",
        string? pageTitle = null,
        bool startOnCurrentPage = false)
    {
        if (photoItems.Count == 0)
            return;

        var title = string.IsNullOrWhiteSpace(pageTitle)
            ? string.IsNullOrWhiteSpace(holdingLabel)
                ? $"Haltungsinspektion - {inspectionDate}"
                : $"Haltungsinspektion - {inspectionDate} - {holdingLabel}"
            : pageTitle;
        var headerItems = BuildHeaderTable(project, record, inspectionDate, holdingLabel);

        var layout = ProtocolPdfPhotoLayout.Resolve(options.PhotosPerPage);
        var perPage = layout.PhotosPerPage;
        var perRow = layout.Columns;
        var photoHeight = ResolvePhotoHeight(layout, options);
        var photoIndex = 1;

        for (var offset = 0; offset < photoItems.Count; offset += perPage)
        {
            // Im Haltungsprotokoll folgen die Fotoseiten auf die Befundtabelle und brauchen
            // deshalb auch vor der ersten Gruppe einen Umbruch. Das Haltungsdossier oeffnet
            // dafuer bereits eine eigene, noch leere Seite - dort wuerde der Umbruch eine
            // leere Seite erzeugen.
            if (offset > 0 || !startOnCurrentPage)
                column.Item().PageBreak();
            column.Item().Element(container =>
                ProtocolPdfExporter.ComposeTitleBar(container, title, options.Subtitle, brand));
            column.Item().PaddingTop(2).Element(container => ComposeHeaderTable(container, headerItems, brand));

            var pageItems = photoItems.Skip(offset).Take(perPage).ToList();
            var rowCount = (int)Math.Ceiling(pageItems.Count / (double)perRow);

            column.Item().PaddingTop(6).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    for (var i = 0; i < perRow; i++)
                        columns.RelativeColumn();
                });

                var cellIndex = 0;
                for (var row = 0; row < rowCount; row++)
                {
                    for (var columnIndex = 0; columnIndex < perRow; columnIndex++)
                    {
                        if (cellIndex < pageItems.Count)
                        {
                            var item = pageItems[cellIndex];
                            var currentIndex = photoIndex++;
                            table.Cell().Element(cell => ComposePhotoCell(cell, item, currentIndex, layout, options, assets));
                            cellIndex++;
                        }
                        else
                        {
                            table.Cell().Height(photoHeight + ProtocolPdfPhotoLayout.CaptionHeight);
                        }
                    }
                }
            });
        }
    }

    private static IReadOnlyList<(string Label, string? Value)> BuildHeaderTable(
        Project project,
        HaltungRecord record,
        string inspectionDate,
        string holdingLabel)
    {
        var location = GetMeta(project, "Gemeinde");
        var street = record.GetFieldValue("Strasse");
        if (string.IsNullOrWhiteSpace(street))
            street = GetMeta(project, "Strasse");

        return new List<(string, string?)>
        {
            ("Ort", location),
            ("Strasse", street),
            ("Datum", inspectionDate),
            ("Haltung", holdingLabel),
            ("Nr.", record.GetFieldValue("NR"))
        };
    }

    private static void ComposeHeaderTable(
        IContainer container,
        IReadOnlyList<(string Label, string? Value)> items,
        string brand)
    {
        if (items.Count == 0)
            return;

        var light = ProtocolPdfExporter.ResolveNutzungsartBrandLight(brand);
        container.Border(0.5f).BorderColor("#D1D5DB").Row(row =>
        {
            row.ConstantItem(3).Background(brand);
            row.RelativeItem()
                .Background(light)
                .PaddingVertical(2)
                .PaddingHorizontal(6)
                .AlignMiddle()
                .Text(text =>
                {
                    for (var i = 0; i < items.Count; i++)
                    {
                        if (i > 0)
                            text.Span("  |  ").FontSize(7.5f).FontColor("#9CA3AF");
                        text.Span(items[i].Label + ": ").FontSize(7.5f).FontColor("#6B7280");
                        text.Span(NormalizeValue(items[i].Value)).FontSize(8).SemiBold().FontColor("#1F2937");
                    }
                });
        });
    }

    private static void ComposePhotoCell(
        IContainer container,
        PhotoItem item,
        int index,
        ProtocolPdfPhotoLayout layout,
        HaltungsprotokollPdfOptions options,
        IProtocolPdfAssetResolver assets)
    {
        // Fuer den bisherigen Zwei-Foto-Weg bleiben die oeffentlichen Groessenoptionen
        // wirksam. Die neuen Mehrspalten-Anordnungen verwenden ihre geprueften Festmasse.
        var photoWidth = layout.PhotosPerPage == ProtocolPdfPhotoLayout.DefaultPhotosPerPage
            ? Math.Max(220f, Math.Min(options.PhotoWidth, 500f))
            : layout.PhotoWidth;
        var photoHeight = ResolvePhotoHeight(layout, options);
        container.AlignCenter().Width(photoWidth).Padding(4).Column(column =>
        {
            var bytes = assets.ReadAllBytes(item.Path);
            var imageAdded = false;
            if (bytes is { Length: > 0 })
            {
                try
                {
                    column.Item().Height(photoHeight)
                        .AlignCenter()
                        .AlignMiddle()
                        .Image(bytes)
                        .FitArea();
                    imageAdded = true;
                }
                catch (QuestPDF.Drawing.Exceptions.DocumentComposeException)
                {
                    // Eine einzelne kaputte Bilddatei darf den gesamten PDF-Export nicht stoppen.
                }
            }

            if (!imageAdded)
            {
                column.Item().Height(photoHeight)
                    .Background("#F5F5F5")
                    .AlignMiddle()
                    .AlignCenter()
                    .Text("Bild fehlt")
                    .FontSize(9)
                    .FontColor(Colors.Grey.Darken2);
            }

            var line1 = BuildPhotoCaptionLine1(item.Entry, index);
            if (!string.IsNullOrWhiteSpace(line1))
                column.Item().PaddingTop(2).AlignCenter().Text(line1).FontSize(9);

            var line2 = BuildPhotoCaptionLine2(item.Entry);
            if (!string.IsNullOrWhiteSpace(line2))
                column.Item().AlignCenter().Text(line2).FontSize(9);
        });
    }

    private static float ResolvePhotoHeight(
        ProtocolPdfPhotoLayout layout,
        HaltungsprotokollPdfOptions options)
        => layout.PhotosPerPage == ProtocolPdfPhotoLayout.DefaultPhotosPerPage
            ? options.PhotoHeight
            : layout.PhotoHeight;

    private static string NormalizeExportPhotoPathKey(string path)
        => Path.GetFullPath(path)
            .Trim()
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
}
