using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.Application.Reports;

// Photo-Block des Protokoll-PDF: Foto-Tabellen, Foto-Cells, Caption-Builder
// und Foto-Nummerierung. Aus ProtocolPdfExporter.cs extrahiert (Slice 1b).
// Methoden bleiben private partial class — Aufrufe aus dem Haupt-Composer
// funktionieren unveraendert.
public sealed partial class ProtocolPdfExporter
{
    private sealed record PhotoItem(ProtocolEntry Entry, string Path);

    private static IReadOnlyList<(string Label, string? Value)> BuildPhotoHeaderTable(
        Project project,
        HaltungRecord record,
        string inspectionDate,
        string holdingLabel)
    {
        var ort = GetMeta(project, "Gemeinde");
        var strasse = record.GetFieldValue("Strasse");
        if (string.IsNullOrWhiteSpace(strasse))
            strasse = GetMeta(project, "Strasse");

        return new List<(string, string?)>
        {
            ("Ort", ort),
            ("Strasse", strasse),
            ("Datum", inspectionDate),
            ("Haltung", holdingLabel),
            ("Nr.", record.GetFieldValue("NR"))
        };
    }

    private static void ComposePhotoHeaderTable(IContainer container, IReadOnlyList<(string Label, string? Value)> items, string brand = "#7A8A94")
    {
        if (items.Count == 0)
            return;

        var light = ResolveNutzungsartBrandLight(brand);

        // Kompakter Einzeilen-Header mit Akzentlinie
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

    private static void ComposePhotoCell(IContainer container, PhotoItem item, int index, HaltungsprotokollPdfOptions options)
    {
        var photoWidth = Math.Max(180f, Math.Min(options.PhotoWidth, 500f));

        container.AlignCenter().Width(photoWidth).Padding(3)
            .Border(0.5f).BorderColor("#D1D5DB")
            .Background("#FFFFFF")
            .Column(col =>
            {
                var bytes = SafeReadAllBytes(item.Path);
                if (bytes is null || bytes.Length == 0)
                {
                    col.Item().Height(options.PhotoHeight)
                        .Background("#F5F5F5")
                        .AlignMiddle()
                        .AlignCenter()
                        .Text("Bild fehlt")
                        .FontSize(9)
                        .FontColor(Colors.Grey.Darken2);
                }
                else
                {
                    col.Item().Height(options.PhotoHeight)
                        .AlignCenter()
                        .AlignMiddle()
                        .Image(bytes)
                        .FitArea();
                }

                // Caption-Bereich mit dezenter Hintergrundtoenung
                col.Item().PaddingTop(3).Background("#F9FAFB").Padding(4).Column(cap =>
                {
                    var line1 = BuildPhotoCaptionLine1(item.Entry, index);
                    if (!string.IsNullOrWhiteSpace(line1))
                        cap.Item().AlignCenter().Text(line1).FontSize(8.5f).SemiBold().FontColor("#1F2937");

                    var line2 = BuildPhotoCaptionLine2(item.Entry);
                    if (!string.IsNullOrWhiteSpace(line2))
                        cap.Item().AlignCenter().Text(line2).FontSize(8).FontColor("#4B5563");
                });
            });
    }

    private static void ComposePhotosSection(
        ColumnDescriptor col,
        IReadOnlyList<PhotoItem> photoItems,
        Project project,
        HaltungRecord record,
        string inspectionDate,
        string holdingLabel,
        HaltungsprotokollPdfOptions options,
        string brand = "#7A8A94",
        string? pageTitle = null)
    {
        if (photoItems.Count == 0)
            return;

        var title = string.IsNullOrWhiteSpace(pageTitle)
            ? (string.IsNullOrWhiteSpace(holdingLabel)
                ? $"Haltungsinspektion - {inspectionDate}"
                : $"Haltungsinspektion - {inspectionDate} - {holdingLabel}")
            : pageTitle;
        var headerItems = BuildPhotoHeaderTable(project, record, inspectionDate, holdingLabel);

        // Layout aus Options (Default: 4 Fotos pro Seite, 2x2 Grid)
        var perPage = Math.Max(1, options.PhotosPerPage);
        var perRow = Math.Max(1, options.PhotosPerRow);
        var photoIndex = 1;
        var captionHeight = 36f;

        for (var offset = 0; offset < photoItems.Count; offset += perPage)
        {
            col.Item().PageBreak();
            col.Item().Element(c => ComposeTitleBar(c, title, options.Subtitle, brand));
            col.Item().PaddingTop(2).Element(c => ComposePhotoHeaderTable(c, headerItems, brand));

            var pageItems = photoItems.Skip(offset).Take(perPage).ToList();
            var rowCount = (int)Math.Ceiling(pageItems.Count / (double)perRow);

            col.Item().PaddingTop(6).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    for (var i = 0; i < perRow; i++)
                        columns.RelativeColumn();
                });

                var cellIndex = 0;
                for (var row = 0; row < rowCount; row++)
                {
                    for (var colIndex = 0; colIndex < perRow; colIndex++)
                    {
                        if (cellIndex < pageItems.Count)
                        {
                            var item = pageItems[cellIndex];
                            var currentIndex = photoIndex++;
                            table.Cell().Element(cell => ComposePhotoCell(cell, item, currentIndex, options));
                            cellIndex++;
                        }
                        else
                        {
                            table.Cell().Height(options.PhotoHeight + captionHeight);
                        }
                    }
                }
            });
        }
    }

    private static string BuildObservationPhotoText(ProtocolEntry entry)
    {
        if (entry.FotoPaths is null || entry.FotoPaths.Count == 0)
            return "-";
        return entry.FotoPaths.Count.ToString(CultureInfo.InvariantCulture);
    }

    private static string BuildPhotoTitle(ProtocolEntry entry)
    {
        var code = string.IsNullOrWhiteSpace(entry.Code) ? "-" : entry.Code.Trim();
        var description = entry.Beschreibung?.Trim();
        if (!string.IsNullOrWhiteSpace(description))
            return $"{code} – {description}";
        return code;
    }

    private static string BuildPhotoMeta(ProtocolEntry entry)
    {
        var parts = new List<string>();
        var meter = BuildObservationMeterText(entry);
        if (!string.IsNullOrWhiteSpace(meter) && meter != "-")
        {
            var label = entry.IsStreckenschaden ? "Strecke" : "Meter";
            parts.Add($"{label} {meter} m");
        }

        var time = BuildObservationTimeText(entry);
        if (!string.IsNullOrWhiteSpace(time))
            parts.Add(time);

        return string.Join(" | ", parts);
    }

    private static string BuildPhotoCaptionLine1(ProtocolEntry entry, int index)
    {
        var line = $"{index}.";
        var time = BuildPhotoTimeText(entry);
        var meter = BuildObservationMeterStartText(entry);

        if (!string.IsNullOrWhiteSpace(time))
            line += $" {time}";
        if (!string.IsNullOrWhiteSpace(meter) && meter != "-")
            line += string.IsNullOrWhiteSpace(time) ? $" {meter} m" : $", {meter} m";

        return line.Trim();
    }

    private static string BuildPhotoCaptionLine2(ProtocolEntry entry)
    {
        var code = string.IsNullOrWhiteSpace(entry.Code) ? "" : entry.Code.Trim();
        var desc = entry.Beschreibung?.Trim();
        if (string.IsNullOrWhiteSpace(desc))
            desc = BuildParameterShortText(entry);
        if (string.IsNullOrWhiteSpace(desc))
            desc = entry.CodeMeta?.Notes?.Trim();

        if (!string.IsNullOrWhiteSpace(desc))
            desc = Shorten(desc, 90);

        if (!string.IsNullOrWhiteSpace(code) && !string.IsNullOrWhiteSpace(desc))
            return $"{code} {desc}";
        if (!string.IsNullOrWhiteSpace(code))
            return code;
        return desc ?? string.Empty;
    }

    private static string BuildPhotoTimeText(ProtocolEntry entry)
    {
        if (!string.IsNullOrWhiteSpace(entry.Mpeg))
            return entry.Mpeg.Trim();
        if (entry.Zeit.HasValue)
            return FormatTime(entry.Zeit.Value);
        return string.Empty;
    }

    private static List<PhotoItem> BuildPhotoItems(
        IReadOnlyList<ProtocolEntry> entries,
        string projectRootAbs,
        int maxPhotosPerEntry)
    {
        var items = new List<PhotoItem>();
        var resolveCache = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in entries)
        {
            if (entry.FotoPaths is null || entry.FotoPaths.Count == 0)
                continue;

            var resolved = ResolvePhotoPaths(entry.FotoPaths, projectRootAbs, maxPhotosPerEntry, resolveCache);
            foreach (var path in resolved)
                items.Add(new PhotoItem(entry, path));
        }

        return items;
    }

    private static IReadOnlyDictionary<ProtocolEntry, string> BuildPhotoNumberMap(IReadOnlyList<PhotoItem> photoItems)
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

        return map.ToDictionary(kv => kv.Key, kv => string.Join(",", kv.Value));
    }

    private static string ResolvePhotoNumberText(
        ProtocolEntry entry,
        IReadOnlyDictionary<ProtocolEntry, string>? photoNumbers)
    {
        if (photoNumbers is null)
            return BuildObservationPhotoText(entry);

        if (photoNumbers.TryGetValue(entry, out var numbers))
            return numbers;

        return "-";
    }
}
