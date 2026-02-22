using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.Application.Reports;

public sealed class ProtocolPdfExporter
{
    private const int HaltungsgrafikWidth = 560;
    private const int HaltungsgrafikHeight = 300;
    private const int HaltungsgrafikMarginTop = 14;
    private const int HaltungsgrafikHeaderHeight = 18;
    private const int HaltungsgrafikMarginBottom = 18;
    private const int HaltungsgrafikLineX = 45;
    private const int HaltungsgrafikTableX = 120;
    private const int HaltungsgrafikRightMargin = 12;

    public byte[] BuildPdf(string projectTitle, ProtocolDocument doc, string projectRootAbs)
        => BuildPdf(projectTitle, doc, projectRootAbs, new ProtocolPdfExportOptions());

    public byte[] BuildPdf(string projectTitle, ProtocolDocument doc, string projectRootAbs, ProtocolPdfExportOptions options)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        var entries = doc.Current.Entries
            .Where(e => !e.IsDeleted)
            .ToList();

        var aiSummary = options.ShowAiSummary ? BuildAiSummary(entries, options) : null;

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(25);
                page.Size(PageSizes.A4);
                page.DefaultTextStyle(x => x.FontSize(10));

                page.Header().Column(col =>
                {
                    col.Item().Text("Inspektionsprotokoll").FontSize(18).Bold();
                    col.Item().Text(projectTitle);
                    col.Item().Text($"Haltung: {doc.HaltungId}");
                    col.Item().Text($"Revision: {doc.Current.Comment} / {doc.Current.CreatedAt:dd.MM.yyyy HH:mm}");
                });

                page.Content().Column(col =>
                {
                    col.Item().LineHorizontal(1);

                    if (aiSummary != null)
                    {
                        col.Item().PaddingVertical(6).Column(b =>
                        {
                            b.Item().Text("KI-Zusammenfassung").Bold();
                            b.Item().Text(aiSummary).FontSize(10);
                            b.Item().LineHorizontal(0.5f);
                        });
                    }

                    foreach (var e in entries)
                    {
                        col.Item().PaddingVertical(6).Column(block =>
                        {
                            var rangeLabel = e.IsStreckenschaden ? "Strecke" : "Meter";
                            block.Item().Text($"{e.Code}  @ {rangeLabel} {FmtMeter(e.MeterStart)}–{FmtMeter(e.MeterEnd)}").Bold();

                            var paramText = BuildParameterText(e);
                            if (!string.IsNullOrWhiteSpace(paramText))
                                block.Item().Text(paramText).FontSize(9);

                            if (!string.IsNullOrWhiteSpace(e.Beschreibung))
                                block.Item().Text(e.Beschreibung);

                            if (options.ShowAiHints)
                                ComposeAiHintBlock(block, e, options);

                            if (e.FotoPaths.Count > 0)
                            {
                                block.Item().PaddingTop(4).Row(imgRow =>
                                {
                                    foreach (var rel in e.FotoPaths.Take(3))
                                    {
                                        var abs = Path.Combine(projectRootAbs, rel.Replace('/', Path.DirectorySeparatorChar));
                                        if (!File.Exists(abs))
                                            continue;

                                        imgRow.ConstantItem(170).Height(110)
                                            .Border(1)
                                            .Padding(2)
                                            .Image(File.ReadAllBytes(abs))
                                            .FitArea();
                                    }
                                });
                            }

                            block.Item().LineHorizontal(0.5f);
                        });
                    }
                });

                page.Footer().AlignCenter().Text(x =>
                {
                    x.Span("Seite ");
                    x.CurrentPageNumber();
                    x.Span(" / ");
                    x.TotalPages();
                });
            });
        }).GeneratePdf();
    }

    public byte[] BuildHaltungsprotokollPdf(
        Project project,
        HaltungRecord record,
        ProtocolDocument doc,
        string projectRootAbs,
        HaltungsprotokollPdfOptions? options = null)
    {
        options ??= new HaltungsprotokollPdfOptions();
        QuestPDF.Settings.License = LicenseType.Community;

        var entries = ResolveEntriesForExport(record, doc)
            .OrderBy(e => e.MeterStart ?? e.MeterEnd ?? double.MaxValue)
            .ToList();

        var length = ResolveHoldingLength(record, entries);
        var inspectionDate = ResolveInspectionDate(project, record, doc);
        var brand = "#006E9C";
        var holdingLabel = record.GetFieldValue("Haltungsname");
        if (string.IsNullOrWhiteSpace(holdingLabel))
            holdingLabel = doc.HaltungId;

        var title = string.IsNullOrWhiteSpace(holdingLabel)
            ? $"{options.Title} - {inspectionDate}"
            : $"{options.Title} - {inspectionDate} - {holdingLabel}";

        var photoItems = options.IncludePhotos
            ? BuildPhotoItems(entries, projectRootAbs, options.MaxPhotosPerEntry)
            : new List<PhotoItem>();
        var photoNumberMap = BuildPhotoNumberMap(photoItems);
        var (startNode, endNode) = SplitHoldingNodes(holdingLabel);
        var flowDown = ParseFlowDirection(record.GetFieldValue("Inspektionsrichtung"));

        var svg = options.IncludeHaltungsgrafik && length.HasValue && length.Value > 0
            ? BuildHaltungsgrafikSvg(length.Value, entries, photoNumberMap, startNode, endNode, flowDown)
            : null;

        var headerItems = BuildHaltungsprotokollHeaderTable(project, record, inspectionDate, length, holdingLabel);
        var logoBytes = ResolveLogoBytes(options, projectRootAbs);

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(25);
                page.Size(PageSizes.A4);
                page.DefaultTextStyle(x => x.FontSize(9));

                page.Header().Element(c => ComposeTopHeader(c, logoBytes, options));

                page.Content().Column(col =>
                {
                    col.Spacing(6);

                    col.Item().LineHorizontal(1).LineColor(brand);
                    col.Item().Element(c => ComposeTitleBar(c, title, options.Subtitle, brand));
                    col.Item().Element(c => ComposeHeaderTable(c, headerItems));

                    var remarks = record.GetFieldValue("Bemerkungen");
                    if (!string.IsNullOrWhiteSpace(remarks))
                    {
                        col.Item().PaddingTop(2).Text("Bemerkungen").Bold();
                        col.Item().Text(remarks).FontSize(8);
                    }

                    if (options.IncludeHaltungsgrafik)
                    {
                        col.Item().PaddingTop(4).Text("Haltungsgrafik").Bold();
                        if (!string.IsNullOrWhiteSpace(svg))
                        {
                            var scale = BuildHaltungsgrafikScale(length);
                            if (!string.IsNullOrWhiteSpace(scale.LengthText) || !string.IsNullOrWhiteSpace(scale.ScaleText))
                            {
                                col.Item().Row(row =>
                                {
                                    row.RelativeItem().Text(scale.LengthText ?? "").FontSize(8).FontColor(Colors.Grey.Darken2);
                                    row.AutoItem().Text(scale.ScaleText ?? "").FontSize(8).FontColor(Colors.Grey.Darken2);
                                });
                            }

                            col.Item().Height(HaltungsgrafikHeight).Svg(svg).FitArea();
                        }
                        else
                        {
                            col.Item().Text("Keine Distanzdaten fuer eine Haltungsgrafik vorhanden.");
                        }
                    }

                    if (options.IncludePhotos)
                        ComposePhotosSection(col, photoItems, project, record, inspectionDate, holdingLabel, options);

                    if (options.AiOptimization is { } ai)
                    {
                        col.Item().PaddingTop(10).Border(1).BorderColor("#CCCCCC").Padding(8).Column(aiCol =>
                        {
                            aiCol.Item().Text("KI-gestützte Empfehlung").Bold().FontSize(10);
                            aiCol.Item().PaddingTop(4).Row(row =>
                            {
                                row.AutoItem().Text("Empfohlene Massnahme: ").FontSize(9).Bold();
                                row.RelativeItem().Text(ai.RecommendedMeasure).FontSize(9);
                            });
                            if (!string.IsNullOrWhiteSpace(ai.CostBandText))
                            {
                                aiCol.Item().Row(row =>
                                {
                                    row.AutoItem().Text("Kostenbandbreite: ").FontSize(9).Bold();
                                    row.RelativeItem().Text(ai.CostBandText).FontSize(9);
                                });
                            }
                            aiCol.Item().Row(row =>
                            {
                                row.AutoItem().Text("Konfidenzwert: ").FontSize(9).Bold();
                                row.RelativeItem().Text(ai.Confidence.ToString("P0")).FontSize(9);
                            });
                            if (!string.IsNullOrWhiteSpace(ai.Reasoning))
                            {
                                aiCol.Item().PaddingTop(2).Text("Begründung:").FontSize(9).Bold();
                                aiCol.Item().Text(ai.Reasoning).FontSize(8);
                            }
                            if (!string.IsNullOrWhiteSpace(ai.RiskText))
                            {
                                aiCol.Item().PaddingTop(2).Text("Risiko-Hinweis:").FontSize(9).Bold();
                                aiCol.Item().Text(ai.RiskText).FontSize(8);
                            }
                            aiCol.Item().PaddingTop(4)
                                .Text("KI-gestützte Empfehlung (nicht bindend)")
                                .FontSize(7).Italic().FontColor(Colors.Grey.Medium);
                        });
                    }
                });

                page.Footer().Row(row =>
                {
                    if (!string.IsNullOrWhiteSpace(options.FooterLine))
                    {
                        row.RelativeItem()
                            .Text(options.FooterLine)
                            .FontSize(8)
                            .FontColor(Colors.Grey.Darken2);
                    }

                    row.AutoItem().Text(x =>
                    {
                        x.DefaultTextStyle(t => t.FontSize(8).FontColor(Colors.Grey.Darken2));
                        x.Span("Seite ");
                        x.CurrentPageNumber();
                    });
                });
            });
        }).GeneratePdf();
    }

    public byte[] BuildCsv(ProtocolDocument doc, ProtocolPdfExportOptions? options = null)
    {
        options ??= new ProtocolPdfExportOptions();

        var entries = doc.Current.Entries.Where(e => !e.IsDeleted).ToList();

        var sb = new StringBuilder();
        var delim = options.CsvDelimiter;

        var cols = new List<string>
        {
            "HaltungId",
            "Code",
            "MeterStart",
            "MeterEnd",
            "IsStreckenschaden",
            "Beschreibung",
            "Parameters",
            "FotoCount"
        };
        if (options.CsvIncludeAiColumns)
            cols.AddRange(new[] { "AiSuggestedCode", "AiFinalCode", "AiAccepted", "AiConfidence", "AiReason", "AiFlags" });

        sb.AppendLine(string.Join(delim, cols.Select(EscapeCsv)));

        foreach (var e in entries)
        {
            var row = new List<string>
            {
                doc.HaltungId ?? "",
                e.Code ?? "",
                e.MeterStart?.ToString("0.00") ?? "",
                e.MeterEnd?.ToString("0.00") ?? "",
                e.IsStreckenschaden ? "1" : "0",
                e.Beschreibung ?? "",
                BuildParameterText(e),
                e.FotoPaths?.Count.ToString() ?? "0"
            };

            if (options.CsvIncludeAiColumns)
            {
                var ai = GetMember(e, "Ai");
                row.Add(SafeString(GetMember(ai, "SuggestedCode")) ?? "");
                row.Add(SafeString(GetMember(ai, "FinalCode")) ?? "");
                row.Add(GetBool(ai, "Accepted").ToString());
                row.Add(SafeDouble(GetMember(ai, "Confidence"))?.ToString("0.00") ?? "");
                row.Add(SafeString(GetMember(ai, "Reason")) ?? SafeString(GetMember(ai, "ReasonShort")) ?? "");
                row.Add(JoinFlags(GetMember(ai, "Flags")));
            }

            sb.AppendLine(string.Join(delim, row.Select(EscapeCsv)));
        }

        var utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: options.CsvIncludeBom);
        return utf8.GetBytes(sb.ToString());
    }

    private static List<(string Label, string? Value)> BuildProjectInfo(Project? project, string inspectionDate)
    {
        var items = new List<(string, string?)>
        {
            ("Inspektionsdatum", inspectionDate)
        };

        if (project is not null)
        {
            items.Add(("Gemeinde", GetMeta(project, "Gemeinde")));
            items.Add(("Auftraggeber", GetMeta(project, "Auftraggeber")));
            items.Add(("Auftrag Nr", GetMeta(project, "AuftragNr")));
            items.Add(("Bearbeiter", GetMeta(project, "Bearbeiter")));
            items.Add(("Zone", GetMeta(project, "Zone")));
        }
        return FilterNonEmpty(items);
    }

    private static List<(string Label, string? Value)> BuildHoldingInfo(HaltungRecord record, double? length)
    {
        var lengthText = length.HasValue ? length.Value.ToString("0.00", CultureInfo.InvariantCulture) : record.GetFieldValue("Haltungslaenge_m");
        var items = new List<(string, string?)>
        {
            ("Haltungsname", record.GetFieldValue("Haltungsname")),
            ("Strasse", record.GetFieldValue("Strasse")),
            ("DN mm", record.GetFieldValue("DN_mm")),
            ("Material", record.GetFieldValue("Rohrmaterial")),
            ("Nutzungsart", record.GetFieldValue("Nutzungsart")),
            ("Laenge m", lengthText),
            ("Inspektionsrichtung", record.GetFieldValue("Inspektionsrichtung")),
            ("Zustandsklasse", record.GetFieldValue("Zustandsklasse")),
            ("VSA Zustandsnote D", record.GetFieldValue("VSA_Zustandsnote_D")),
            ("Pruefungsresultat", record.GetFieldValue("Pruefungsresultat")),
            ("Referenzpruefung", record.GetFieldValue("Referenzpruefung")),
            ("Sanieren", record.GetFieldValue("Sanieren_JaNein")),
            ("Eigentuemer", record.GetFieldValue("Eigentuemer")),
            ("Ausgefuehrt durch", record.GetFieldValue("Ausgefuehrt_durch"))
        };
        return FilterNonEmpty(items);
    }

    private static IReadOnlyList<(string Label, string? Value)> BuildHaltungsprotokollHeaderTable(
        Project project,
        HaltungRecord record,
        string inspectionDate,
        double? length,
        string holdingLabel)
    {
        var ort = GetMeta(project, "Gemeinde");
        var strasse = record.GetFieldValue("Strasse");
        if (string.IsNullOrWhiteSpace(strasse))
            strasse = GetMeta(project, "Strasse");

        var projektname = !string.IsNullOrWhiteSpace(project.Description) ? project.Description : project.Name;
        var lengthText = length.HasValue ? length.Value.ToString("0.00", CultureInfo.InvariantCulture) : record.GetFieldValue("Haltungslaenge_m");

        return new List<(string, string?)>
        {
            ("GEP", project.Name),
            ("Projektname", projektname),
            ("Nr.", record.GetFieldValue("NR")),
            ("Ort", ort),
            ("Strasse", strasse),
            ("Datum", inspectionDate),
            ("Haltung", holdingLabel),
            ("Betreiber", GetMeta(project, "Eigentuemer")),
            ("Auftraggeber", GetMeta(project, "Auftraggeber")),
            ("DN [mm]", record.GetFieldValue("DN_mm")),
            ("Material", record.GetFieldValue("Rohrmaterial")),
            ("Rohrlänge [m]", lengthText),
            ("Nutzungsart", record.GetFieldValue("Nutzungsart")),
            ("Inspektionsrichtung", record.GetFieldValue("Inspektionsrichtung")),
            ("Zustandsklasse", record.GetFieldValue("Zustandsklasse")),
            ("VSA Zustandsnote", record.GetFieldValue("VSA_Zustandsnote_D")),
            ("Bearbeiter", GetMeta(project, "Bearbeiter")),
            ("Auftrag Nr.", GetMeta(project, "AuftragNr"))
        };
    }

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

    private static List<(string Label, string? Value)> BuildHoldingSummary(HaltungRecord record, double? length, string inspectionDate)
    {
        var lengthText = length.HasValue ? $"{length.Value:0.00} m" : record.GetFieldValue("Haltungslaenge_m");
        return new List<(string, string?)>
        {
            ("Haltung", record.GetFieldValue("Haltungsname")),
            ("Inspektionsdatum", inspectionDate),
            ("Strasse", record.GetFieldValue("Strasse")),
            ("Inspektionsrichtung", record.GetFieldValue("Inspektionsrichtung")),
            ("DN / Material", BuildDnMaterial(record)),
            ("Nutzungsart", record.GetFieldValue("Nutzungsart")),
            ("Laenge", lengthText),
            ("VSA Zustandsnote", record.GetFieldValue("VSA_Zustandsnote_D"))
        };
    }

    private static void ComposeInfoSection(
        IContainer container,
        string title,
        IReadOnlyList<(string Label, string? Value)> items)
    {
        if (items.Count == 0)
            return;

        container.Border(1)
            .BorderColor(Colors.Grey.Lighten2)
            .Padding(6)
            .Column(col =>
        {
            col.Item().Text(title).Bold().FontColor("#006E9C");
            col.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(115);
                    columns.RelativeColumn();
                    columns.ConstantColumn(115);
                    columns.RelativeColumn();
                });

                for (int i = 0; i < items.Count; i += 2)
                {
                    var left = items[i];
                    var right = i + 1 < items.Count ? items[i + 1] : (Label: "", Value: "");

                    table.Cell().PaddingVertical(2).Text(left.Label).FontSize(9).SemiBold();
                    table.Cell().PaddingVertical(2).Text(NormalizeValue(left.Value)).FontSize(9);

                    if (string.IsNullOrWhiteSpace(right.Label) && string.IsNullOrWhiteSpace(right.Value))
                    {
                        table.Cell().Text("");
                        table.Cell().Text("");
                    }
                    else
                    {
                        table.Cell().PaddingVertical(2).Text(right.Label).FontSize(9).SemiBold();
                        table.Cell().PaddingVertical(2).Text(NormalizeValue(right.Value)).FontSize(9);
                    }
                }
            });
        });
    }

    private static void ComposeKeyValueTable(IContainer container, IReadOnlyList<(string Label, string? Value)> items)
    {
        if (items.Count == 0)
            return;

        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.ConstantColumn(115);
                columns.RelativeColumn();
                columns.ConstantColumn(115);
                columns.RelativeColumn();
            });

            for (int i = 0; i < items.Count; i += 2)
            {
                var left = items[i];
                var right = i + 1 < items.Count ? items[i + 1] : (Label: "", Value: "");

                table.Cell().PaddingVertical(2).Text(left.Label).FontSize(9).SemiBold();
                table.Cell().PaddingVertical(2).Text(NormalizeValue(left.Value)).FontSize(9);

                if (string.IsNullOrWhiteSpace(right.Label) && string.IsNullOrWhiteSpace(right.Value))
                {
                    table.Cell().Text("");
                    table.Cell().Text("");
                }
                else
                {
                    table.Cell().PaddingVertical(2).Text(right.Label).FontSize(9).SemiBold();
                    table.Cell().PaddingVertical(2).Text(NormalizeValue(right.Value)).FontSize(9);
                }
            }
        });
    }

    private static void ComposeTopHeader(IContainer container, byte[]? logoBytes, HaltungsprotokollPdfOptions options)
    {
        container.Row(row =>
        {
            row.ConstantItem(120).Height(36).AlignMiddle().Element(c =>
            {
                if (logoBytes is not null)
                    c.Image(logoBytes).FitHeight();
            });

            row.RelativeItem().AlignRight().Column(col =>
            {
                var lines = options.SenderBlock?.Split('\n', StringSplitOptions.RemoveEmptyEntries) ?? Array.Empty<string>();
                foreach (var line in lines)
                {
                    col.Item().AlignRight().Text(line.Trim()).FontSize(8);
                }
            });
        });
    }

    private static void ComposeTitleBar(IContainer container, string title, string? subtitle, string brand)
    {
        container.Border(1)
            .BorderColor(Colors.Grey.Darken2)
            .Background("#EAF5F9")
            .PaddingVertical(4)
            .PaddingHorizontal(6)
            .Column(col =>
            {
                col.Item().AlignCenter().Text(title).FontSize(11).SemiBold().FontColor(brand);
                if (!string.IsNullOrWhiteSpace(subtitle))
                    col.Item().AlignCenter().Text(subtitle).FontSize(8).FontColor(Colors.Grey.Darken2);
            });
    }

    private static void ComposeHeaderTable(IContainer container, IReadOnlyList<(string Label, string? Value)> items)
    {
        if (items.Count == 0)
            return;

        static IContainer Cell(IContainer c) => c.Border(0.5f).BorderColor(Colors.Grey.Darken2).Padding(3);

        container.Border(1)
            .BorderColor(Colors.Grey.Darken2)
            .Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    for (var i = 0; i < 3; i++)
                    {
                        columns.ConstantColumn(70);
                        columns.RelativeColumn();
                    }
                });

                for (var i = 0; i < items.Count; i += 3)
                {
                    for (var col = 0; col < 3; col++)
                    {
                        var index = i + col;
                        var item = index < items.Count ? items[index] : (Label: "", Value: "");

                        table.Cell().Element(Cell).Text(item.Label).FontSize(8).SemiBold();
                        table.Cell().Element(Cell).Text(NormalizeValue(item.Value)).FontSize(8);
                    }
                }
            });
    }

    private static void ComposePhotoHeaderTable(IContainer container, IReadOnlyList<(string Label, string? Value)> items)
    {
        if (items.Count == 0)
            return;

        static IContainer Cell(IContainer c) => c.Border(0.5f).BorderColor(Colors.Grey.Darken2).Padding(3);

        container.Border(1)
            .BorderColor(Colors.Grey.Darken2)
            .Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    foreach (var _ in items)
                    {
                        columns.ConstantColumn(45);
                        columns.RelativeColumn();
                    }
                });

                foreach (var item in items)
                {
                    table.Cell().Element(Cell).Text(item.Label).FontSize(8).SemiBold();
                    table.Cell().Element(Cell).Text(NormalizeValue(item.Value)).FontSize(8);
                }
            });
    }

    private static void ComposePhotoCell(IContainer container, PhotoItem item, int index, HaltungsprotokollPdfOptions options)
    {
        container.Padding(4).Column(col =>
        {
            var bytes = SafeReadAllBytes(item.Path);
            if (bytes is null || bytes.Length == 0)
            {
                col.Item().Height(options.PhotoHeight)
                    .Border(1)
                    .AlignMiddle()
                    .AlignCenter()
                    .Text("Bild fehlt")
                    .FontSize(8)
                    .FontColor(Colors.Grey.Darken2);
            }
            else
            {
                col.Item().Height(options.PhotoHeight)
                    .Border(1)
                    .Padding(2)
                    .Image(bytes)
                    .FitArea();
            }

            var line1 = BuildPhotoCaptionLine1(item.Entry, index);
            if (!string.IsNullOrWhiteSpace(line1))
                col.Item().PaddingTop(2).Text(line1).FontSize(8);

            var line2 = BuildPhotoCaptionLine2(item.Entry);
            if (!string.IsNullOrWhiteSpace(line2))
                col.Item().Text(line2).FontSize(8);
        });
    }

    private static void ComposeObservationTable(IContainer container, IReadOnlyList<ProtocolEntry> entries)
    {
        static IContainer HeaderCell(IContainer c)
            => c.Background("#E6F3F8").PaddingVertical(3).PaddingHorizontal(4);

        static IContainer BodyCell(IContainer c)
            => c.BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).PaddingVertical(2).PaddingHorizontal(4);

        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.ConstantColumn(30);
                columns.ConstantColumn(60);
                columns.ConstantColumn(70);
                columns.ConstantColumn(95);
                columns.RelativeColumn(3);
                columns.RelativeColumn(2);
            });

            table.Header(header =>
            {
                header.Cell().Element(HeaderCell).Text("Nr.").FontSize(9).SemiBold();
                header.Cell().Element(HeaderCell).Text("Code").FontSize(9).SemiBold();
                header.Cell().Element(HeaderCell).Text("Meter (m)").FontSize(9).SemiBold();
                header.Cell().Element(HeaderCell).Text("Zeit").FontSize(9).SemiBold();
                header.Cell().Element(HeaderCell).Text("Beschreibung").FontSize(9).SemiBold();
                header.Cell().Element(HeaderCell).Text("Parameter").FontSize(9).SemiBold();
            });

            var index = 1;
            foreach (var entry in entries)
            {
                table.Cell().Element(BodyCell).Text(index.ToString(CultureInfo.InvariantCulture)).FontSize(9);
                table.Cell().Element(BodyCell).Text(string.IsNullOrWhiteSpace(entry.Code) ? "-" : entry.Code.Trim()).FontSize(9);
                table.Cell().Element(BodyCell).Text(BuildObservationMeterText(entry)).FontSize(9);
                table.Cell().Element(BodyCell).Text(BuildObservationTimeText(entry)).FontSize(9);
                table.Cell().Element(BodyCell).Text(entry.Beschreibung ?? "").FontSize(9);
                table.Cell().Element(BodyCell).Text(BuildParameterShortText(entry)).FontSize(9);
                index++;
            }
        });
    }

    private static void ComposeSectionObservationTable(IContainer container, IReadOnlyList<ProtocolEntry> entries)
    {
        static IContainer HeaderCell(IContainer c)
            => c.Background(Colors.Grey.Lighten3).PaddingVertical(3).PaddingHorizontal(4);

        static IContainer BodyCell(IContainer c)
            => c.BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).PaddingVertical(2).PaddingHorizontal(4);

        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.ConstantColumn(55); // m+
                columns.ConstantColumn(70); // OP Kuerzel
                columns.RelativeColumn(5);  // Zustand
                columns.ConstantColumn(70); // MPEG
                columns.ConstantColumn(45); // Foto
                columns.ConstantColumn(45); // Stufe
            });

            table.Header(header =>
            {
                header.Cell().Element(HeaderCell).Text("m+").FontSize(9).SemiBold();
                header.Cell().Element(HeaderCell).Text("OP Kuerzel").FontSize(9).SemiBold();
                header.Cell().Element(HeaderCell).Text("Zustand").FontSize(9).SemiBold();
                header.Cell().Element(HeaderCell).Text("MPEG").FontSize(9).SemiBold();
                header.Cell().Element(HeaderCell).Text("Foto").FontSize(9).SemiBold();
                header.Cell().Element(HeaderCell).Text("Stufe").FontSize(9).SemiBold();
            });

            foreach (var entry in entries)
            {
                table.Cell().Element(BodyCell).Text(BuildObservationMeterStartText(entry)).FontSize(9);
                table.Cell().Element(BodyCell).Text(string.IsNullOrWhiteSpace(entry.Code) ? "-" : entry.Code.Trim()).FontSize(9);
                table.Cell().Element(BodyCell).Text(entry.Beschreibung ?? "").FontSize(9);
                table.Cell().Element(BodyCell).Text(BuildObservationMpegText(entry)).FontSize(9);
                table.Cell().Element(BodyCell).Text(BuildObservationPhotoText(entry)).FontSize(9);
                table.Cell().Element(BodyCell).Text(BuildObservationStufeText(entry)).FontSize(9);
            }
        });
    }

    private static void ComposeObservationListTable(
        IContainer container,
        IReadOnlyList<ProtocolEntry> entries,
        IReadOnlyDictionary<ProtocolEntry, string>? photoNumbers)
    {
        static IContainer HeaderCell(IContainer c)
            => c.Background("#EAF5F9").PaddingVertical(3).PaddingHorizontal(4);

        static IContainer BodyCell(IContainer c)
            => c.BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).PaddingVertical(2).PaddingHorizontal(4);

        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.ConstantColumn(38); // m+
                columns.ConstantColumn(38); // m-
                columns.ConstantColumn(55); // OP
                columns.RelativeColumn(6);  // Zustand
                columns.ConstantColumn(45); // Foto
                columns.ConstantColumn(55); // MPEG
                columns.ConstantColumn(45); // Zeit
                columns.RelativeColumn(2);  // Bemerkung
            });

            table.Header(header =>
            {
                header.Cell().Element(HeaderCell).Text("m+").FontSize(8).SemiBold();
                header.Cell().Element(HeaderCell).Text("m-").FontSize(8).SemiBold();
                header.Cell().Element(HeaderCell).Text("OP Kürzel").FontSize(8).SemiBold();
                header.Cell().Element(HeaderCell).Text("Zustand").FontSize(8).SemiBold();
                header.Cell().Element(HeaderCell).Text("Foto").FontSize(8).SemiBold();
                header.Cell().Element(HeaderCell).Text("MPEG").FontSize(8).SemiBold();
                header.Cell().Element(HeaderCell).Text("Zeit").FontSize(8).SemiBold();
                header.Cell().Element(HeaderCell).Text("Bemerkung").FontSize(8).SemiBold();
            });

            foreach (var entry in entries)
            {
                table.Cell().Element(BodyCell).Text(FmtMeterValue(entry.MeterStart)).FontSize(8);
                table.Cell().Element(BodyCell).Text(FmtMeterValue(entry.MeterEnd)).FontSize(8);
                table.Cell().Element(BodyCell).Text(string.IsNullOrWhiteSpace(entry.Code) ? "-" : entry.Code.Trim()).FontSize(8);
                table.Cell().Element(BodyCell).Text(BuildObservationZustandTextLong(entry)).FontSize(8);
                table.Cell().Element(BodyCell).Text(ResolvePhotoNumberText(entry, photoNumbers)).FontSize(8);
                table.Cell().Element(BodyCell).Text(entry.Mpeg?.Trim() ?? "-").FontSize(8);
                table.Cell().Element(BodyCell).Text(entry.Zeit.HasValue ? FormatTime(entry.Zeit.Value) : "-").FontSize(8);
                table.Cell().Element(BodyCell).Text(BuildObservationNotesText(entry)).FontSize(8);
            }
        });
    }

    private static void ComposePhotosSection(
        ColumnDescriptor col,
        IReadOnlyList<PhotoItem> photoItems,
        Project project,
        HaltungRecord record,
        string inspectionDate,
        string holdingLabel,
        HaltungsprotokollPdfOptions options)
    {
        if (photoItems.Count == 0)
            return;

        var brand = "#006E9C";
        var title = string.IsNullOrWhiteSpace(holdingLabel)
            ? $"Haltungsbilder - {inspectionDate}"
            : $"Haltungsbilder - {inspectionDate} - {holdingLabel}";
        var headerItems = BuildPhotoHeaderTable(project, record, inspectionDate, holdingLabel);

        var perPage = Math.Max(1, options.PhotosPerPage);
        var perRow = Math.Max(1, options.PhotosPerRow);
        var photoIndex = 1;
        var captionHeight = 32f;

        for (var offset = 0; offset < photoItems.Count; offset += perPage)
        {
            col.Item().PageBreak();
            col.Item().Element(c => ComposeTitleBar(c, title, null, brand));
            col.Item().Element(c => ComposePhotoHeaderTable(c, headerItems));

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

    private sealed record PhotoItem(ProtocolEntry Entry, string Path);

    private static string BuildRangeText(ProtocolEntry entry, string rangeLabel)
    {
        if (entry.MeterStart is null && entry.MeterEnd is null)
            return $"{rangeLabel} -";

        var m1 = FmtMeterValue(entry.MeterStart);
        var m2 = FmtMeterValue(entry.MeterEnd);
        return $"{rangeLabel} {m1}-{m2} m";
    }

    private static string BuildDetailLine(ProtocolEntry entry)
    {
        var parts = new List<string>();
        if (entry.Zeit.HasValue)
            parts.Add("Zeit " + FormatTime(entry.Zeit.Value));
        if (!string.IsNullOrWhiteSpace(entry.Mpeg))
            parts.Add("MPEG " + entry.Mpeg.Trim());
        return string.Join(" | ", parts);
    }

    private static string BuildObservationMeterText(ProtocolEntry entry)
    {
        var start = entry.MeterStart;
        var end = entry.MeterEnd;

        if (entry.IsStreckenschaden && start.HasValue && end.HasValue)
            return $"{FmtMeterValue(start)}–{FmtMeterValue(end)}";

        if (start.HasValue)
            return FmtMeterValue(start);

        if (end.HasValue)
            return FmtMeterValue(end);

        return "-";
    }

    private static string BuildObservationTimeText(ProtocolEntry entry)
    {
        var parts = new List<string>();
        if (entry.Zeit.HasValue)
            parts.Add(FormatTime(entry.Zeit.Value));
        if (!string.IsNullOrWhiteSpace(entry.Mpeg))
            parts.Add("MPEG " + entry.Mpeg.Trim());
        return string.Join(" | ", parts);
    }

    private static string BuildObservationMeterStartText(ProtocolEntry entry)
    {
        var value = entry.MeterStart ?? entry.MeterEnd;
        return value.HasValue ? FmtMeterValue(value) : "-";
    }

    private static string BuildObservationMpegText(ProtocolEntry entry)
    {
        if (!string.IsNullOrWhiteSpace(entry.Mpeg))
            return entry.Mpeg.Trim();
        if (entry.Zeit.HasValue)
            return FormatTime(entry.Zeit.Value);
        return "-";
    }

    private static string BuildObservationPhotoText(ProtocolEntry entry)
    {
        if (entry.FotoPaths is null || entry.FotoPaths.Count == 0)
            return "-";
        return entry.FotoPaths.Count.ToString(CultureInfo.InvariantCulture);
    }

    private static string BuildObservationStufeText(ProtocolEntry entry)
    {
        if (!string.IsNullOrWhiteSpace(entry.CodeMeta?.Severity))
            return entry.CodeMeta.Severity!.Trim();
        if (entry.CodeMeta?.Count is not null)
            return entry.CodeMeta.Count.Value.ToString(CultureInfo.InvariantCulture);
        return "-";
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

    private static string BuildObservationNotesText(ProtocolEntry entry)
    {
        var parameters = entry.CodeMeta?.Parameters;
        if (parameters is not null)
        {
            var remark = GetParam(parameters, "vsa.anmerkung");
            if (!string.IsNullOrWhiteSpace(remark))
                return Shorten(remark.Trim(), 60);
        }

        if (!string.IsNullOrWhiteSpace(entry.CodeMeta?.Notes))
            return Shorten(entry.CodeMeta.Notes.Trim(), 60);

        return "-";
    }

    private static string BuildParameterShortText(ProtocolEntry entry)
    {
        var parameters = entry.CodeMeta?.Parameters;
        if (parameters is null || parameters.Count == 0)
            return string.Empty;

        var list = new List<string>();
        foreach (var kv in parameters.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(kv.Value))
                continue;
            if (kv.Key.StartsWith("vsa.", StringComparison.OrdinalIgnoreCase))
                continue;
            if (string.Equals(kv.Key, "Quantifizierung1", StringComparison.OrdinalIgnoreCase))
                continue;
            if (string.Equals(kv.Key, "Quantifizierung2", StringComparison.OrdinalIgnoreCase))
                continue;

            list.Add($"{kv.Key}={kv.Value}");
        }

        if (list.Count == 0)
        {
            var q1 = GetParam(parameters, "Quantifizierung1") ?? GetParam(parameters, "vsa.q1");
            var q2 = GetParam(parameters, "Quantifizierung2") ?? GetParam(parameters, "vsa.q2");
            if (!string.IsNullOrWhiteSpace(q1))
                list.Add($"Q1={q1}");
            if (!string.IsNullOrWhiteSpace(q2))
                list.Add($"Q2={q2}");
        }

        return string.Join(", ", list);
    }

    private static string Shorten(string text, int max)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;
        if (text.Length <= max)
            return text;
        return text.Substring(0, Math.Max(0, max - 1)).TrimEnd() + "…";
    }

    private static string EscapeSvgText(string text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;
        return text.Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;")
            .Replace("'", "&apos;");
    }

    private static string BuildDnMaterial(HaltungRecord record)
    {
        var dn = record.GetFieldValue("DN_mm");
        var material = record.GetFieldValue("Rohrmaterial");

        if (string.IsNullOrWhiteSpace(dn) && string.IsNullOrWhiteSpace(material))
            return string.Empty;
        if (string.IsNullOrWhiteSpace(dn))
            return material;
        if (string.IsNullOrWhiteSpace(material))
            return dn;

        return $"{dn} mm / {material}";
    }

    private static string BuildVsaParameterLine(ProtocolEntry entry)
    {
        var parameters = entry.CodeMeta?.Parameters;
        if (parameters is null || parameters.Count == 0)
            return string.Empty;

        var parts = new List<string>();

        var q1 = GetParam(parameters, "vsa.q1") ?? GetParam(parameters, "Quantifizierung1");
        var q2 = GetParam(parameters, "vsa.q2") ?? GetParam(parameters, "Quantifizierung2");
        if (!string.IsNullOrWhiteSpace(q1)) parts.Add($"Q1={q1}");
        if (!string.IsNullOrWhiteSpace(q2)) parts.Add($"Q2={q2}");

        var distanz = GetParam(parameters, "vsa.distanz");
        if (!string.IsNullOrWhiteSpace(distanz)) parts.Add($"Distanz={distanz}");

        var uhrVon = GetParam(parameters, "vsa.uhr.von");
        var uhrBis = GetParam(parameters, "vsa.uhr.bis");
        if (!string.IsNullOrWhiteSpace(uhrVon) || !string.IsNullOrWhiteSpace(uhrBis))
            parts.Add($"Uhr {uhrVon ?? "-"}-{uhrBis ?? "-"}");

        var strecke = GetParam(parameters, "vsa.strecke");
        if (!string.IsNullOrWhiteSpace(strecke)) parts.Add($"Strecke={strecke}");

        var verbindung = GetParam(parameters, "vsa.verbindung");
        if (IsTruthy(verbindung)) parts.Add("Verbindung=Ja");

        var ansicht = GetParam(parameters, "vsa.ansicht");
        if (!string.IsNullOrWhiteSpace(ansicht)) parts.Add($"Ansicht={ansicht}");

        var ez = GetParam(parameters, "vsa.ez");
        if (!string.IsNullOrWhiteSpace(ez)) parts.Add($"EZ={ez}");

        var schacht = GetParam(parameters, "vsa.schachtbereich");
        if (!string.IsNullOrWhiteSpace(schacht)) parts.Add($"Schachtbereich={schacht}");

        var anmerkung = GetParam(parameters, "vsa.anmerkung");
        if (!string.IsNullOrWhiteSpace(anmerkung)) parts.Add($"Diverses={anmerkung}");

        var known = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Quantifizierung1",
            "Quantifizierung2",
            "vsa.q1",
            "vsa.q2",
            "vsa.distanz",
            "vsa.uhr.von",
            "vsa.uhr.bis",
            "vsa.strecke",
            "vsa.verbindung",
            "vsa.ansicht",
            "vsa.ez",
            "vsa.schachtbereich",
            "vsa.anmerkung"
        };

        foreach (var kv in parameters
                     .Where(kv => !known.Contains(kv.Key) && !string.IsNullOrWhiteSpace(kv.Value))
                     .OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase))
        {
            parts.Add($"{kv.Key}={kv.Value}");
        }

        if (!string.IsNullOrWhiteSpace(entry.CodeMeta?.Severity))
            parts.Add($"Severity={entry.CodeMeta.Severity}");
        if (entry.CodeMeta?.Count is not null)
            parts.Add($"Count={entry.CodeMeta.Count}");
        if (!string.IsNullOrWhiteSpace(entry.CodeMeta?.Notes))
            parts.Add($"Notiz={entry.CodeMeta.Notes}");

        return parts.Count == 0 ? string.Empty : "Parameter: " + string.Join(" | ", parts);
    }

    private static List<PhotoItem> BuildPhotoItems(
        IReadOnlyList<ProtocolEntry> entries,
        string projectRootAbs,
        int maxPhotosPerEntry)
    {
        var items = new List<PhotoItem>();
        foreach (var entry in entries)
        {
            if (entry.FotoPaths is null || entry.FotoPaths.Count == 0)
                continue;

            var resolved = ResolvePhotoPaths(entry.FotoPaths, projectRootAbs, maxPhotosPerEntry);
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
        if (photoNumbers is not null && photoNumbers.TryGetValue(entry, out var numbers))
            return numbers;

        return BuildObservationPhotoText(entry);
    }

    private static string BuildHaltungsgrafikSvg(
        double length,
        IReadOnlyList<ProtocolEntry> entries,
        IReadOnlyDictionary<ProtocolEntry, string>? photoNumbers,
        string? startNode,
        string? endNode,
        bool? flowDown)
    {
        var width = HaltungsgrafikWidth;
        var height = HaltungsgrafikHeight;
        var marginTop = HaltungsgrafikMarginTop;
        var headerHeight = HaltungsgrafikHeaderHeight;
        var marginBottom = HaltungsgrafikMarginBottom;
        var lineX = HaltungsgrafikLineX;
        var tableX = HaltungsgrafikTableX;
        var rightMargin = HaltungsgrafikRightMargin;

        var top = (double)marginTop + headerHeight;
        var bottom = height - marginBottom;

        var tableWidth = Math.Max(1d, width - tableX - rightMargin);
        var colMeterWidth = 50d;
        var colCodeWidth = 50d;
        var colMpegWidth = 55d;
        var colFotoWidth = 35d;
        var colStufeWidth = 35d;
        var colZustandWidth = Math.Max(120d, tableWidth - (colMeterWidth + colCodeWidth + colMpegWidth + colFotoWidth + colStufeWidth));

        var colMeterX = tableX;
        var colCodeX = colMeterX + colMeterWidth;
        var colZustandX = colCodeX + colCodeWidth;
        var colMpegX = colZustandX + colZustandWidth;
        var colFotoX = colMpegX + colMpegWidth;
        var colStufeX = colFotoX + colFotoWidth;

        var headerY = marginTop + 12;
        var headerLineY = top - 6;

        var sb = new StringBuilder();
        sb.Append($"<svg xmlns='http://www.w3.org/2000/svg' width='{width}' height='{height}' viewBox='0 0 {width} {height}'>");
        sb.Append("<rect width='100%' height='100%' fill='white'/>");

        var ratio = ComputeScaleRatio(length);
        if (ratio.HasValue)
            sb.Append($"<text x='{Svg(5)}' y='{Svg(headerY)}' font-size='9' font-weight='bold' fill='#333333'>1:{ratio.Value}</text>");

        sb.Append($"<text x='{Svg(colMeterX)}' y='{Svg(headerY)}' font-size='9' font-weight='bold' fill='#333333'>m+</text>");
        sb.Append($"<text x='{Svg(colCodeX)}' y='{Svg(headerY)}' font-size='9' font-weight='bold' fill='#333333'>OP Kürzel</text>");
        sb.Append($"<text x='{Svg(colZustandX)}' y='{Svg(headerY)}' font-size='9' font-weight='bold' fill='#333333'>Zustand</text>");
        sb.Append($"<text x='{Svg(colMpegX)}' y='{Svg(headerY)}' font-size='9' font-weight='bold' fill='#333333'>MPEG</text>");
        sb.Append($"<text x='{Svg(colFotoX)}' y='{Svg(headerY)}' font-size='9' font-weight='bold' fill='#333333'>Foto</text>");
        sb.Append($"<text x='{Svg(colStufeX)}' y='{Svg(headerY)}' font-size='9' font-weight='bold' fill='#333333'>Stufe</text>");
        sb.Append($"<line x1='{Svg(tableX)}' y1='{Svg(headerLineY)}' x2='{Svg(width - rightMargin)}' y2='{Svg(headerLineY)}' stroke='#BDBDBD' stroke-width='0.7'/>");

        var tickStep = ChooseTickStep(length);
        var ticks = BuildTicks(length, tickStep);
        foreach (var meter in ticks)
        {
            var y = MapToLine(meter, length, top, bottom);
            sb.Append($"<line x1='{Svg(lineX - 4)}' y1='{Svg(y)}' x2='{Svg(lineX + 4)}' y2='{Svg(y)}' stroke='#6B6B6B' stroke-width='1'/>");
            sb.Append($"<text x='{Svg(lineX - 10)}' y='{Svg(y + 3)}' font-size='8' text-anchor='end' fill='#333333'>{meter:0.00}</text>");
        }

        sb.Append($"<line x1='{Svg(lineX)}' y1='{Svg(top)}' x2='{Svg(lineX)}' y2='{Svg(bottom)}' stroke='#2F2F2F' stroke-width='2'/>");
        sb.Append($"<circle cx='{Svg(lineX)}' cy='{Svg(top)}' r='4' fill='#2F2F2F'/>");
        sb.Append($"<circle cx='{Svg(lineX)}' cy='{Svg(bottom)}' r='4' fill='#2F2F2F'/>");

        if (!string.IsNullOrWhiteSpace(startNode))
        {
            var startLabelY = Math.Max(8, top - 10);
            sb.Append($"<text x='{Svg(lineX - 10)}' y='{Svg(startLabelY)}' font-size='8' text-anchor='end' fill='#333333'>{EscapeSvgText(startNode)}</text>");
        }
        if (!string.IsNullOrWhiteSpace(endNode))
        {
            var endLabelY = Math.Min(height - 2, bottom + 12);
            sb.Append($"<text x='{Svg(lineX - 10)}' y='{Svg(endLabelY)}' font-size='8' text-anchor='end' fill='#333333'>{EscapeSvgText(endNode)}</text>");
        }

        if (flowDown.HasValue)
        {
            var arrowY = flowDown.Value
                ? top + (bottom - top) * 0.25
                : top + (bottom - top) * 0.75;
            if (flowDown.Value)
            {
                sb.Append($"<polygon points='{Svg(lineX - 4)},{Svg(arrowY - 2)} {Svg(lineX + 4)},{Svg(arrowY - 2)} {Svg(lineX)},{Svg(arrowY + 6)}' fill='#006E9C'/>");
            }
            else
            {
                sb.Append($"<polygon points='{Svg(lineX - 4)},{Svg(arrowY + 2)} {Svg(lineX + 4)},{Svg(arrowY + 2)} {Svg(lineX)},{Svg(arrowY - 6)}' fill='#006E9C'/>");
            }
        }

        foreach (var entry in entries)
        {
            if (!entry.IsStreckenschaden || entry.MeterStart is null || entry.MeterEnd is null)
                continue;

            var y1 = MapToLine(entry.MeterStart.Value, length, top, bottom);
            var y2 = MapToLine(entry.MeterEnd.Value, length, top, bottom);
            if (y2 < y1)
                (y1, y2) = (y2, y1);

            sb.Append($"<line x1='{Svg(lineX)}' y1='{Svg(y1)}' x2='{Svg(lineX)}' y2='{Svg(y2)}' stroke='#D64541' stroke-width='6' stroke-linecap='round'/>");
        }

        foreach (var entry in entries)
        {
            if (entry.IsStreckenschaden)
                continue;

            var pos = entry.MeterStart ?? entry.MeterEnd;
            if (pos is null)
                continue;

            var y = MapToLine(pos.Value, length, top, bottom);
            sb.Append($"<circle cx='{Svg(lineX)}' cy='{Svg(y)}' r='4' fill='#1F6FEB'/>");
        }

        var labels = BuildHaltungsgrafikLabels(entries, length, top, bottom, photoNumbers);
        LayoutHaltungsgrafikLabels(labels, top, bottom);
        foreach (var label in labels)
        {
            var targetY = label.TargetY;
            var labelY = label.LabelY;
            var leaderX = tableX - 6;

            sb.Append($"<line x1='{Svg(lineX)}' y1='{Svg(targetY)}' x2='{Svg(leaderX)}' y2='{Svg(targetY)}' stroke='{label.LineColor}' stroke-width='1.5'/>");
            if (Math.Abs(labelY - targetY) > 0.1)
                sb.Append($"<line x1='{Svg(leaderX)}' y1='{Svg(targetY)}' x2='{Svg(leaderX)}' y2='{Svg(labelY)}' stroke='{label.LineColor}' stroke-width='1'/>");

            var textColor = "#1A1A1A";
            var fontSize = Svg(label.FontSize);
            sb.Append($"<text x='{Svg(colMeterX)}' y='{Svg(labelY + 3)}' font-size='{fontSize}' text-anchor='start' fill='{textColor}'>" +
                      $"{EscapeSvgText(label.MeterText)}</text>");
            sb.Append($"<text x='{Svg(colCodeX)}' y='{Svg(labelY + 3)}' font-size='{fontSize}' text-anchor='start' fill='{textColor}'>" +
                      $"{EscapeSvgText(label.CodeText)}</text>");
            sb.Append($"<text x='{Svg(colZustandX)}' y='{Svg(labelY + 3)}' font-size='{fontSize}' text-anchor='start' fill='{textColor}'>" +
                      $"{EscapeSvgText(label.ZustandText)}</text>");
            sb.Append($"<text x='{Svg(colMpegX)}' y='{Svg(labelY + 3)}' font-size='{fontSize}' text-anchor='start' fill='{textColor}'>" +
                      $"{EscapeSvgText(label.MpegText)}</text>");
            sb.Append($"<text x='{Svg(colFotoX)}' y='{Svg(labelY + 3)}' font-size='{fontSize}' text-anchor='start' fill='{textColor}'>" +
                      $"{EscapeSvgText(label.FotoText)}</text>");
            sb.Append($"<text x='{Svg(colStufeX)}' y='{Svg(labelY + 3)}' font-size='{fontSize}' text-anchor='start' fill='{textColor}'>" +
                      $"{EscapeSvgText(label.StufeText)}</text>");
        }

        sb.Append("</svg>");
        return sb.ToString();
    }

    private static string BuildHaltungsgrafikZustandText(ProtocolEntry entry)
    {
        var desc = NormalizeZustandDescription(entry.Beschreibung, entry.Code);
        if (string.IsNullOrWhiteSpace(desc))
            desc = BuildParameterShortText(entry);
        if (string.IsNullOrWhiteSpace(desc))
            desc = entry.CodeMeta?.Notes?.Trim();

        if (string.IsNullOrWhiteSpace(desc))
            return "-";

        return Shorten(desc, 55);
    }

    private static string BuildObservationZustandTextLong(ProtocolEntry entry)
    {
        var desc = NormalizeZustandDescription(entry.Beschreibung, entry.Code);
        if (string.IsNullOrWhiteSpace(desc))
            desc = BuildParameterShortText(entry);
        if (string.IsNullOrWhiteSpace(desc))
            desc = entry.CodeMeta?.Notes?.Trim();

        return string.IsNullOrWhiteSpace(desc) ? "-" : desc;
    }

    private static string NormalizeZustandDescription(string? raw, string? code)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return string.Empty;

        var text = raw.Trim();
        var codeToken = code?.Trim();

        // If pattern is "CODE @0.00m (desc)" -> take the inside.
        var open = text.IndexOf('(');
        var close = text.LastIndexOf(')');
        if (open >= 0 && close > open)
        {
            var prefix = text.Substring(0, open);
            if ((!string.IsNullOrWhiteSpace(codeToken) && prefix.Contains(codeToken, StringComparison.OrdinalIgnoreCase))
                || Regex.IsMatch(prefix, @"@\s*\d"))
            {
                text = text.Substring(open + 1, close - open - 1);
            }
        }

        if (!string.IsNullOrWhiteSpace(codeToken))
            text = Regex.Replace(text, @"^\s*" + Regex.Escape(codeToken) + @"\b\s*", "", RegexOptions.IgnoreCase);

        text = Regex.Replace(text, @"^\s*@?\s*\d+(?:[.,]\d+)?\s*m\b\s*", "", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"^\s*[A-Z0-9]{1,6}(?:\s+[A-Z0-9]{1,6})?\s*", "", RegexOptions.IgnoreCase);
        text = text.Trim(' ', '-', '–', ':', ',');

        return text;
    }

    private sealed class HaltungsgrafikLabel
    {
        public double TargetY { get; init; }
        public double LabelY { get; set; }
        public string MeterText { get; init; } = "-";
        public string CodeText { get; init; } = "-";
        public string ZustandText { get; init; } = "-";
        public string MpegText { get; init; } = "-";
        public string FotoText { get; init; } = "-";
        public string StufeText { get; init; } = "-";
        public string LineColor { get; init; } = "#1F6FEB";
        public double FontSize { get; set; } = 9;
    }

    private static List<HaltungsgrafikLabel> BuildHaltungsgrafikLabels(
        IReadOnlyList<ProtocolEntry> entries,
        double length,
        double top,
        double bottom,
        IReadOnlyDictionary<ProtocolEntry, string>? photoNumbers)
    {
        var list = new List<HaltungsgrafikLabel>();

        foreach (var entry in entries)
        {
            var isRange = entry.IsStreckenschaden && entry.MeterStart is not null && entry.MeterEnd is not null;
            var pos = isRange
                ? (entry.MeterStart!.Value + entry.MeterEnd!.Value) / 2d
                : entry.MeterStart ?? entry.MeterEnd;

            if (pos is null)
                continue;

            var y = MapToLine(pos.Value, length, top, bottom);
            var meterText = BuildObservationMeterStartText(entry);
            var codeText = string.IsNullOrWhiteSpace(entry.Code) ? "-" : entry.Code.Trim();
            var zustandText = BuildHaltungsgrafikZustandText(entry);
            var mpegText = BuildObservationMpegText(entry);
            var fotoText = ResolvePhotoNumberText(entry, photoNumbers);
            var stufeText = BuildObservationStufeText(entry);

            list.Add(new HaltungsgrafikLabel
            {
                TargetY = y,
                LabelY = y,
                MeterText = string.IsNullOrWhiteSpace(meterText) ? "-" : meterText,
                CodeText = string.IsNullOrWhiteSpace(codeText) ? "-" : codeText,
                ZustandText = string.IsNullOrWhiteSpace(zustandText) ? "-" : zustandText,
                MpegText = string.IsNullOrWhiteSpace(mpegText) ? "-" : mpegText,
                FotoText = string.IsNullOrWhiteSpace(fotoText) ? "-" : fotoText,
                StufeText = string.IsNullOrWhiteSpace(stufeText) ? "-" : stufeText,
                LineColor = isRange ? "#D64541" : "#1F6FEB"
            });
        }

        return list;
    }

    private static void LayoutHaltungsgrafikLabels(
        List<HaltungsgrafikLabel> labels,
        double top,
        double bottom)
    {
        if (labels.Count == 0)
            return;

        labels.Sort((a, b) => a.TargetY.CompareTo(b.TargetY));
        var available = Math.Max(1d, bottom - top);
        var minGap = Math.Clamp(available / Math.Max(1, labels.Count), 8d, 14d);
        var minY = top + 2;
        var maxY = bottom - 2;

        labels[0].LabelY = Math.Clamp(labels[0].TargetY, minY, maxY);
        for (var i = 1; i < labels.Count; i++)
        {
            labels[i].LabelY = Math.Clamp(Math.Max(labels[i].TargetY, labels[i - 1].LabelY + minGap), minY, maxY);
        }

        var overflow = labels[^1].LabelY - maxY;
        if (overflow > 0)
        {
            for (var i = 0; i < labels.Count; i++)
                labels[i].LabelY -= overflow;
        }

        for (var i = labels.Count - 2; i >= 0; i--)
        {
            if (labels[i].LabelY > labels[i + 1].LabelY - minGap)
                labels[i].LabelY = labels[i + 1].LabelY - minGap;
        }

        var underflow = minY - labels[0].LabelY;
        if (underflow > 0)
        {
            for (var i = 0; i < labels.Count; i++)
                labels[i].LabelY += underflow;
        }

        for (var i = 0; i < labels.Count; i++)
            labels[i].LabelY = Math.Clamp(labels[i].LabelY, minY, maxY);

        var fontSize = minGap < 9 ? 7 : minGap < 11 ? 8 : 9;
        foreach (var label in labels)
            label.FontSize = fontSize;
    }

    private sealed record HaltungsgrafikScale(string? LengthText, string? ScaleText);

    private static HaltungsgrafikScale BuildHaltungsgrafikScale(double? length)
    {
        if (!length.HasValue || length.Value <= 0)
            return new HaltungsgrafikScale(null, null);

        var ratio = ComputeScaleRatio(length.Value);
        var lengthText = $"Haltungslänge: {length.Value:0.00} m";
        var scaleText = ratio.HasValue ? $"Massstab: 1:{ratio.Value}" : "";
        return new HaltungsgrafikScale(lengthText, scaleText);
    }

    private static int? ComputeScaleRatio(double length)
    {
        if (length <= 0)
            return null;

        var plotHeight = HaltungsgrafikHeight - HaltungsgrafikMarginTop - HaltungsgrafikMarginBottom - HaltungsgrafikHeaderHeight;
        var plotCm = plotHeight * 2.54 / 72.0;
        if (plotCm <= 0.01)
            return null;

        var mPerCm = length / plotCm;
        if (mPerCm <= 0)
            return null;

        return (int)Math.Round(mPerCm * 100.0, MidpointRounding.AwayFromZero);
    }

    private static List<double> BuildTicks(double length, double step)
    {
        var list = new List<double>();
        if (length <= 0 || step <= 0)
            return list;

        var m = 0d;
        while (m <= length + 1e-6)
        {
            list.Add(m);
            m += step;
        }

        if (list.Count == 0 || Math.Abs(list[^1] - length) > 1e-6)
            list.Add(length);

        return list.Distinct().OrderBy(x => x).ToList();
    }

    private static double ChooseTickStep(double length)
    {
        var candidates = new[] { 0.2, 0.5, 1d, 2d, 5d, 10d, 20d, 50d };
        if (length <= 0)
            return 1;

        foreach (var step in candidates)
        {
            var count = length / step;
            if (count >= 4 && count <= 8)
                return step;
        }

        return candidates.Last();
    }

    private static double? ResolveHoldingLength(HaltungRecord record, IReadOnlyList<ProtocolEntry> entries)
    {
        var raw = record.GetFieldValue("Haltungslaenge_m");
        var parsed = TryParseDouble(raw);
        if (parsed.HasValue && parsed.Value > 0)
            return parsed.Value;

        var max = entries
            .Select(e => e.MeterEnd ?? e.MeterStart)
            .Where(v => v.HasValue)
            .Select(v => v!.Value)
            .DefaultIfEmpty(0d)
            .Max();

        return max > 0 ? max : null;
    }

    private static List<ProtocolEntry> ResolveEntriesForExport(HaltungRecord record, ProtocolDocument doc)
    {
        var current = doc.Current?.Entries ?? new List<ProtocolEntry>();
        var active = current.Where(e => !e.IsDeleted).ToList();
        var deletedKeys = new HashSet<string>(current.Where(e => e.IsDeleted).Select(BuildEntryKey));
        var existingKeys = new HashSet<string>(active.Select(BuildEntryKey));

        var result = new List<ProtocolEntry>(active);

        var fromFindings = BuildImportedEntriesFromFindings(record.VsaFindings);
        foreach (var entry in fromFindings)
        {
            var key = BuildEntryKey(entry);
            if (deletedKeys.Contains(key) || existingKeys.Contains(key))
                continue;

            result.Add(entry);
            existingKeys.Add(key);
        }

        if (fromFindings.Count == 0)
        {
            var fromPrimary = ParsePrimaryDamagesToEntries(record.GetFieldValue("Primaere_Schaeden"));
            foreach (var entry in fromPrimary)
            {
                var key = BuildEntryKey(entry);
                if (deletedKeys.Contains(key) || existingKeys.Contains(key))
                    continue;

                result.Add(entry);
                existingKeys.Add(key);
            }
        }

        return result;
    }

    private static List<ProtocolEntry> BuildImportedEntriesFromFindings(IReadOnlyList<VsaFinding> findings)
    {
        var list = new List<ProtocolEntry>();
        if (findings is null || findings.Count == 0)
            return list;

        foreach (var f in findings)
        {
            if (string.IsNullOrWhiteSpace(f.KanalSchadencode))
                continue;

            var mStart = f.MeterStart ?? f.SchadenlageAnfang;
            var mEnd = f.MeterEnd ?? f.SchadenlageEnde;
            if (mStart is null && !string.IsNullOrWhiteSpace(f.Raw))
                mStart = TryParseMeterFromRaw(f.Raw);
            if (mEnd is null && !string.IsNullOrWhiteSpace(f.Raw))
                mEnd = TryParseSecondMeterFromRaw(f.Raw);

            var time = ParseMpegTime(f.MPEG)
                       ?? (f.Timestamp is null ? null : f.Timestamp.Value.TimeOfDay);
            if (time is null && !string.IsNullOrWhiteSpace(f.Raw))
            {
                var rawTime = TryParseTimeFromRaw(f.Raw);
                time = ParseMpegTime(rawTime);
            }

            var entry = new ProtocolEntry
            {
                Code = f.KanalSchadencode?.Trim() ?? string.Empty,
                Beschreibung = f.Raw?.Trim() ?? string.Empty,
                MeterStart = mStart,
                MeterEnd = mEnd,
                IsStreckenschaden = mStart.HasValue && mEnd.HasValue && mEnd >= mStart,
                Mpeg = f.MPEG,
                Zeit = time,
                Source = ProtocolEntrySource.Imported
            };

            if (!string.IsNullOrWhiteSpace(f.Quantifizierung1) || !string.IsNullOrWhiteSpace(f.Quantifizierung2))
            {
                entry.CodeMeta = new ProtocolEntryCodeMeta
                {
                    Code = entry.Code,
                    Parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["Quantifizierung1"] = f.Quantifizierung1 ?? string.Empty,
                        ["Quantifizierung2"] = f.Quantifizierung2 ?? string.Empty
                    },
                    UpdatedAt = DateTimeOffset.UtcNow
                };
            }

            if (!string.IsNullOrWhiteSpace(f.FotoPath))
                entry.FotoPaths.Add(f.FotoPath);

            list.Add(entry);
        }

        return list;
    }

    private static List<ProtocolEntry> ParsePrimaryDamagesToEntries(string? rawText)
    {
        var list = new List<ProtocolEntry>();
        if (string.IsNullOrWhiteSpace(rawText))
            return list;

        var lines = rawText.Replace("\r\n", "\n").Split('\n');
        foreach (var line in lines)
        {
            var trimmed = (line ?? "").Trim();
            if (string.IsNullOrWhiteSpace(trimmed))
                continue;
            if (trimmed.StartsWith("...", StringComparison.Ordinal))
                continue;

            if (!TryParsePrimaryDamageLine(trimmed, out var code, out var meter, out var desc))
                continue;

            var entry = new ProtocolEntry
            {
                Code = code,
                Beschreibung = desc ?? string.Empty,
                MeterStart = meter,
                IsStreckenschaden = false,
                Source = ProtocolEntrySource.Imported
            };

            list.Add(entry);
        }

        return list;
    }

    private static bool TryParsePrimaryDamageLine(string line, out string code, out double? meter, out string? desc)
    {
        code = string.Empty;
        meter = null;
        desc = null;

        var match = Regex.Match(line, @"^\s*(?<code>[A-Z0-9]{1,6}(?:\s+[A-Z0-9]{1,6})?)\s*@\s*(?<m>\d+(?:[.,]\d+)?)\s*m?\s*(?:\((?<desc>.+)\))?\s*$");
        if (!match.Success)
            return false;

        code = match.Groups["code"].Value.Trim();
        var mText = match.Groups["m"].Value.Replace(',', '.');
        if (double.TryParse(mText, NumberStyles.Float, CultureInfo.InvariantCulture, out var val))
            meter = val;
        desc = match.Groups["desc"].Success ? match.Groups["desc"].Value.Trim() : string.Empty;
        return !string.IsNullOrWhiteSpace(code);
    }

    private static string BuildEntryKey(ProtocolEntry entry)
    {
        var code = (entry.Code ?? "").Trim().ToUpperInvariant();
        var start = entry.MeterStart ?? entry.MeterEnd ?? -1;
        var end = entry.MeterEnd ?? entry.MeterStart ?? -1;
        var desc = NormalizeKeyText(entry.Beschreibung ?? entry.CodeMeta?.Notes ?? "");
        return string.Format(CultureInfo.InvariantCulture, "{0}|{1:0.00}|{2:0.00}|{3}", code, start, end, desc);
    }

    private static string NormalizeKeyText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;
        var normalized = Regex.Replace(text.Trim(), @"\s+", " ");
        return normalized;
    }

    private static (string? Start, string? End) SplitHoldingNodes(string? holdingLabel)
    {
        if (string.IsNullOrWhiteSpace(holdingLabel))
            return (null, null);

        var parts = holdingLabel
            .Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (parts.Length == 1)
            return (parts[0], null);
        if (parts.Length >= 2)
            return (parts[0], parts[1]);

        return (null, null);
    }

    private static bool? ParseFlowDirection(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        if (text.Contains("gegen", StringComparison.OrdinalIgnoreCase))
            return false;
        if (text.Contains("in", StringComparison.OrdinalIgnoreCase))
            return true;

        return null;
    }

    private static string ResolveInspectionDate(Project project, HaltungRecord record, ProtocolDocument doc)
    {
        var meta = GetMeta(project, "InspektionsDatum");
        if (!string.IsNullOrWhiteSpace(meta))
            return meta.Trim();

        var recordDate = record.GetFieldValue("Datum_Jahr");
        if (!string.IsNullOrWhiteSpace(recordDate))
            return recordDate.Trim();

        return doc.Current.CreatedAt.ToLocalTime().ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);
    }

    private static byte[]? ResolveLogoBytes(HaltungsprotokollPdfOptions options, string projectRootAbs)
    {
        foreach (var path in BuildLogoCandidates(options.LogoPathAbs, projectRootAbs))
        {
            if (string.IsNullOrWhiteSpace(path))
                continue;
            if (!File.Exists(path))
                continue;

            try
            {
                return File.ReadAllBytes(path);
            }
            catch
            {
                // try next candidate
            }
        }

        return null;
    }

    private static IEnumerable<string> BuildLogoCandidates(string? explicitLogo, string projectRootAbs)
    {
        if (!string.IsNullOrWhiteSpace(explicitLogo))
            yield return explicitLogo;

        if (string.IsNullOrWhiteSpace(projectRootAbs))
            yield break;

        yield return Path.Combine(projectRootAbs, "Assets", "Brand", "abwasser-uri-logo.png");
        yield return Path.Combine(projectRootAbs, "Brand", "abwasser-uri-logo.png");
        yield return Path.Combine(projectRootAbs, "Dokumente", "abwasser-uri-logo.png");
        yield return Path.Combine(projectRootAbs, "abwasser-uri-logo.png");
        yield return Path.Combine(projectRootAbs, "logo.png");
        yield return Path.Combine(projectRootAbs, "logo.jpg");
        yield return Path.Combine(projectRootAbs, "logo.jpeg");
    }

    private static List<string> ResolvePhotoPaths(
        IReadOnlyList<string> photoPaths,
        string projectRootAbs,
        int maxPhotos)
    {
        var list = new List<string>();
        foreach (var raw in photoPaths)
        {
            if (string.IsNullOrWhiteSpace(raw))
                continue;

            var resolved = ResolvePhotoPath(projectRootAbs, raw);
            if (string.IsNullOrWhiteSpace(resolved) || !File.Exists(resolved))
                continue;

            list.Add(resolved);
            if (list.Count >= maxPhotos)
                break;
        }

        return list;
    }

    private static string ResolvePhotoPath(string projectRootAbs, string raw)
    {
        var normalized = raw.Replace('/', Path.DirectorySeparatorChar);
        if (Path.IsPathRooted(normalized))
            return normalized;
        if (string.IsNullOrWhiteSpace(projectRootAbs))
            return normalized;
        return Path.Combine(projectRootAbs, normalized);
    }

    private static byte[]? SafeReadAllBytes(string path)
    {
        try
        {
            return File.ReadAllBytes(path);
        }
        catch
        {
            return null;
        }
    }

    private static string? GetMeta(Project project, string key)
        => project.Metadata.TryGetValue(key, out var v) ? v : null;

    private static string NormalizeValue(string? value)
        => string.IsNullOrWhiteSpace(value) ? "-" : value.Trim();

    private static List<(string Label, string? Value)> FilterNonEmpty(List<(string Label, string? Value)> items)
        => items.Where(i => !string.IsNullOrWhiteSpace(i.Value)).ToList();

    private static string FmtMeterValue(double? value)
        => value is null ? "-" : value.Value.ToString("0.00", CultureInfo.InvariantCulture);

    private static string FormatTime(TimeSpan value)
        => value.TotalHours >= 1 ? value.ToString(@"hh\:mm\:ss") : value.ToString(@"mm\:ss");

    private static TimeSpan? ParseMpegTime(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var text = raw.Trim();
        var formats = new[] { @"hh\:mm\:ss", @"mm\:ss", @"h\:mm\:ss", @"m\:ss", @"hh\:mm\:ss\.fff", @"mm\:ss\.fff" };
        if (TimeSpan.TryParseExact(text, formats, CultureInfo.InvariantCulture, out var parsed))
            return parsed;

        if (TimeSpan.TryParse(text, CultureInfo.InvariantCulture, out parsed))
            return parsed;

        return null;
    }

    private static double? TryParseDouble(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var normalized = raw.Trim().Replace(',', '.');
        if (double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
            return parsed;

        return null;
    }

    private static readonly Regex RawMeterRegex =
        new(@"@?\s*(\d+(?:[.,]\d+)?)\s*m(?!m)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex RawTimeRegex =
        new(@"\b(\d{1,2}:\d{2}(?::\d{2})?)\b", RegexOptions.Compiled);

    private static double? TryParseMeterFromRaw(string raw)
    {
        var match = RawMeterRegex.Match(raw);
        if (!match.Success)
            return null;

        var text = match.Groups[1].Value.Replace(',', '.');
        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : null;
    }

    private static double? TryParseSecondMeterFromRaw(string raw)
    {
        var matches = RawMeterRegex.Matches(raw);
        if (matches.Count < 2)
            return null;

        var text = matches[1].Groups[1].Value.Replace(',', '.');
        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : null;
    }

    private static string? TryParseTimeFromRaw(string raw)
    {
        var match = RawTimeRegex.Match(raw);
        return match.Success ? match.Groups[1].Value : null;
    }

    private static string? GetParam(IReadOnlyDictionary<string, string> parameters, string key)
        => parameters.TryGetValue(key, out var value) ? value : null;

    private static bool IsTruthy(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;
        return string.Equals(value, "ja", StringComparison.OrdinalIgnoreCase)
               || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
               || string.Equals(value, "1", StringComparison.OrdinalIgnoreCase);
    }

    private static double MapToLine(double value, double length, double left, double right)
    {
        if (length <= 0)
            return left;
        var t = Math.Clamp(value / length, 0d, 1d);
        return left + (right - left) * t;
    }

    private static string Svg(double value)
        => value.ToString("0.###", CultureInfo.InvariantCulture);

    private static string BuildAiSummary(List<ProtocolEntry> entries, ProtocolPdfExportOptions options)
    {
        var aiEntries = entries.Select(e => GetMember(e, "Ai")).Where(ai => ai != null).ToList();
        if (aiEntries.Count == 0)
            return "Keine KI-Daten vorhanden.";

        var accepted = aiEntries.Count(ai => GetBool(ai, "Accepted"));
        var rejected = aiEntries.Count(ai => GetBool(ai, "Rejected"));
        var undecided = aiEntries.Count - accepted - rejected;

        var topCodes = aiEntries
            .Select(ai => SafeString(GetMember(ai, "FinalCode")) ?? SafeString(GetMember(ai, "SuggestedCode")))
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .GroupBy(c => c!)
            .OrderByDescending(g => g.Count())
            .Take(Math.Max(1, options.MaxAiSummaryCodes))
            .Select(g => $"{g.Key} ({g.Count()})")
            .ToList();

        var allFlags = aiEntries
            .SelectMany(ai => AsStringEnumerable(GetMember(ai, "Flags")))
            .Where(f => !string.IsNullOrWhiteSpace(f))
            .GroupBy(f => f)
            .OrderByDescending(g => g.Count())
            .Take(5)
            .Select(g => $"{g.Key} ({g.Count()})")
            .ToList();

        var parts = new List<string>
        {
            $"Akzeptiert: {accepted}   Abgelehnt: {rejected}   Offen: {undecided}"
        };
        if (topCodes.Count > 0)
            parts.Add("Top KI-Codes: " + string.Join(", ", topCodes));
        if (allFlags.Count > 0)
            parts.Add("Häufigste KI-Flags: " + string.Join(", ", allFlags));

        return string.Join("    ", parts);
    }

    private static void ComposeAiHintBlock(ColumnDescriptor block, ProtocolEntry e, ProtocolPdfExportOptions options)
    {
        var ai = GetMember(e, "Ai");
        if (ai == null)
            return;

        var accepted = GetBool(ai, "Accepted");
        var rejected = GetBool(ai, "Rejected");

        if (options.ShowAiHintsOnlyIfDecided && !(accepted || rejected))
            return;

        var status = accepted ? "übernommen" : rejected ? "abgelehnt" : "offen";
        var code = SafeString(GetMember(ai, "FinalCode")) ?? SafeString(GetMember(ai, "SuggestedCode")) ?? "—";
        var conf = SafeDouble(GetMember(ai, "Confidence"))?.ToString("0.00") ?? "—";
        var reason = SafeString(GetMember(ai, "Reason")) ?? SafeString(GetMember(ai, "ReasonShort")) ?? "";
        var flags = AsStringEnumerable(GetMember(ai, "Flags")).ToList();
        var flagsText = flags.Count > 0 ? $" [{string.Join(", ", flags)}]" : "";

        block.Item().Text($"KI-Vorschlag: {code} ({conf}) – {status}{flagsText}").FontSize(9).Italic();
        if (!string.IsNullOrWhiteSpace(reason))
            block.Item().Text($"Grund: {reason}").FontSize(9).Italic();
    }

    private static object? GetMember(object? obj, string name)
    {
        if (obj == null) return null;
        var prop = obj.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
        return prop?.GetValue(obj);
    }

    private static bool GetBool(object? obj, string name)
    {
        var v = GetMember(obj, name);
        return v is bool b && b;
    }

    private static double? SafeDouble(object? v)
        => v is double d ? d : v is float f ? f : v is decimal m ? (double)m : null;

    private static string? SafeString(object? v) => v as string;

    private static IEnumerable<string> AsStringEnumerable(object? v)
    {
        if (v is IEnumerable<string> es) return es;
        if (v is IEnumerable<object> eo) return eo.Select(x => x?.ToString() ?? "");
        return Array.Empty<string>();
    }

    private static string JoinFlags(object? flags)
    {
        var list = AsStringEnumerable(flags).Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
        return list.Count == 0 ? "" : string.Join(", ", list);
    }

    private static string EscapeCsv(string s)
    {
        if (s.Contains('"') || s.Contains(';') || s.Contains(',') || s.Contains('\n') || s.Contains('\r'))
            return "\"" + s.Replace("\"", "\"\"") + "\"";
        return s;
    }

    private static string FmtMeter(double? m) => m is null ? "—" : m.Value.ToString("0.00");

    private static string BuildParameterText(ProtocolEntry e)
    {
        var parts = new List<string>();
        if (e.CodeMeta?.Parameters != null && e.CodeMeta.Parameters.Count > 0)
        {
            var p = e.CodeMeta.Parameters
                .Where(kv => !string.IsNullOrWhiteSpace(kv.Value))
                .Select(kv => $"{kv.Key}={kv.Value}");
            parts.Add("Parameter: " + string.Join(", ", p));
        }
        if (!string.IsNullOrWhiteSpace(e.CodeMeta?.Severity))
            parts.Add($"Severity: {e.CodeMeta.Severity}");
        if (e.CodeMeta?.Count is not null)
            parts.Add($"Count: {e.CodeMeta.Count}");
        if (!string.IsNullOrWhiteSpace(e.CodeMeta?.Notes))
            parts.Add($"Notes: {e.CodeMeta.Notes}");

        return string.Join(" | ", parts.Where(p => !string.IsNullOrWhiteSpace(p)));
    }
}

public sealed record ProtocolPdfExportOptions
{
    public bool ShowAiHints { get; init; } = false;
    public bool ShowAiHintsOnlyIfDecided { get; init; } = true;

    public bool ShowAiSummary { get; init; } = false;
    public int MaxAiSummaryCodes { get; init; } = 5;

    public char CsvDelimiter { get; init; } = ';';
    public bool CsvIncludeBom { get; init; } = true;

    public bool CsvIncludeAiColumns { get; init; } = true;
}

public sealed record HaltungsprotokollPdfOptions
{
    public string Title { get; init; } = "Haltungsinspektion";
    public string Subtitle { get; init; } = "SN EN 13508-2";
    public string SenderBlock { get; init; } =
        "Abwasser Uri\n" +
        "Zentrale Dienste\n" +
        "Giessenstrasse 46\n" +
        "6460 Altdorf\n" +
        "info@abwasser-uri.ch\n" +
        "T 041 875 00 90";

    public bool IncludePhotos { get; init; } = true;
    public bool IncludeHaltungsgrafik { get; init; } = true;
    public int PhotosPerRow { get; init; } = 2;
    public int PhotosPerPage { get; init; } = 4;
    public int MaxPhotosPerEntry { get; init; } = int.MaxValue;
    public float PhotoWidth { get; init; } = 240f;
    public float PhotoHeight { get; init; } = 170f;
    public float PhotoSpacing { get; init; } = 12f;
    public string? LogoPathAbs { get; init; }
    public string FooterLine { get; init; } = "";

    /// <summary>Optional KI-optimisation result to append as a bordered block (§9).</summary>
    public AiOptimizationResult? AiOptimization { get; init; }
}

/// <summary>
/// Flattened snapshot of a KI Sanierungsoptimierung result for PDF embedding.
/// </summary>
public sealed record AiOptimizationResult
{
    public string RecommendedMeasure { get; init; } = "";
    public string CostBandText { get; init; } = "";   // e.g. "Min 12'000 | Erwartet 15'000 | Max 18'000 CHF"
    public double Confidence { get; init; }
    public string Reasoning { get; init; } = "";
    public string RiskText { get; init; } = "";
    public bool IsFallback { get; init; }
}

