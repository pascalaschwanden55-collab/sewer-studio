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
    private const int HaltungsgrafikWidth = 770;
    private const int HaltungsgrafikHeight = 520;
    private const int HaltungsgrafikMarginTop = 8;
    private const int HaltungsgrafikHeaderHeight = 16;
    private const int HaltungsgrafikNodeZone = 32;   // Platz fuer Schachtknoten + Beschriftung
    private const int HaltungsgrafikMarginBottom = 44; // Platz fuer unteren Knoten + Beschriftung
    private const int HaltungsgrafikLineX = 42;
    private const int HaltungsgrafikTableX = 98;
    private const int HaltungsgrafikRightMargin = 6;

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
                                            .Border(0.5f).BorderColor("#444444")
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
        var nutzungsart = record.GetFieldValue("Nutzungsart")?.Trim() ?? "";
        var brand = ResolveNutzungsartBrand(nutzungsart);
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

        var grafikHeight = 700; // Tall SVG for page-filling graphic
        var svg = options.IncludeHaltungsgrafik && length.HasValue && length.Value > 0
            ? BuildHaltungsgrafikSvg(length.Value, entries, photoNumberMap, startNode, endNode, flowDown, brand, overrideHeight: grafikHeight)
            : null;

        var headerItems = BuildHaltungsprotokollHeaderTable(project, record, inspectionDate, length, holdingLabel);
        var logoBytes = ResolveLogoBytes(options, projectRootAbs);

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(25);
                page.Size(PageSizes.A4);
                page.DefaultTextStyle(x => x.FontSize(10));

                page.Header().Element(c => ComposeTopHeader(c, logoBytes, options));

                page.Content().Column(col =>
                {
                    // === SEITE 1: Titel + Header + Haltungsgrafik ===
                    // Alles auf einer Seite, keine Spacing/ExtendVertical.
                    // A4=842pt, Margins=50pt, Header~45pt, Footer~25pt → Content≈722pt
                    // Titel~30pt, HeaderTable~110pt, SectionHeading~18pt, Spacing~14pt, Scale~12pt, Border/Pad~14pt → Grafik≈524pt
                    const float grafikDisplayHeight = 490f;

                    col.Item().PaddingTop(4).Element(c => ComposeTitleBar(c, title, options.Subtitle, brand));
                    col.Item().PaddingTop(4).Element(c => ComposeHeaderTable(c, headerItems, brand));

                    if (options.IncludeHaltungsgrafik)
                    {
                        col.Item().PaddingTop(2).Element(c => ComposeSectionHeading(c, "Haltungsgrafik", brand));
                        if (!string.IsNullOrWhiteSpace(svg))
                        {
                            var scale = BuildHaltungsgrafikScale(length, grafikHeight);
                            col.Item().PaddingTop(2).Border(0.5f).BorderColor("#D1D5DB").Background("#FFFFFF").Padding(4).Column(g =>
                            {
                                if (!string.IsNullOrWhiteSpace(scale.LengthText) || !string.IsNullOrWhiteSpace(scale.ScaleText))
                                {
                                    g.Item().Row(row =>
                                    {
                                        row.RelativeItem().Text(scale.LengthText ?? "").FontSize(10).FontColor(Colors.Grey.Darken2);
                                        row.AutoItem().Text(scale.ScaleText ?? "").FontSize(10).FontColor(Colors.Grey.Darken2);
                                    });
                                }
                                g.Item().Height(grafikDisplayHeight).Svg(svg).FitArea();
                            });
                        }
                        else
                        {
                            col.Item().Border(0.5f).BorderColor("#D1D5DB").Background("#FAFBFC").Padding(8)
                                .Text("Keine Distanzdaten fuer eine Haltungsgrafik vorhanden.");
                        }
                    }

                    if (options.IncludePhotos)
                        ComposePhotosSection(col, photoItems, project, record, inspectionDate, holdingLabel, options, brand, title);

                    if (options.AiOptimization is { } ai)
                    {
                        col.Item().PaddingTop(10).Border(1).BorderColor("#CCCCCC").Padding(8).Column(aiCol =>
                        {
                            aiCol.Item().Text("KI-gestützte Empfehlung").Bold().FontSize(11);
                            aiCol.Item().PaddingTop(4).Row(row =>
                            {
                                row.AutoItem().Text("Empfohlene Massnahme: ").FontSize(10).Bold();
                                row.RelativeItem().Text(ai.RecommendedMeasure).FontSize(10);
                            });
                            if (!string.IsNullOrWhiteSpace(ai.CostBandText))
                            {
                                aiCol.Item().Row(row =>
                                {
                                    row.AutoItem().Text("Kostenbandbreite: ").FontSize(10).Bold();
                                    row.RelativeItem().Text(ai.CostBandText).FontSize(10);
                                });
                            }
                            aiCol.Item().Row(row =>
                            {
                                row.AutoItem().Text("Konfidenzwert: ").FontSize(10).Bold();
                                row.RelativeItem().Text(ai.Confidence.ToString("P0")).FontSize(10);
                            });
                            if (!string.IsNullOrWhiteSpace(ai.Reasoning))
                            {
                                aiCol.Item().PaddingTop(2).Text("Begründung:").FontSize(10).Bold();
                                aiCol.Item().Text(ai.Reasoning).FontSize(9);
                            }
                            if (!string.IsNullOrWhiteSpace(ai.RiskText))
                            {
                                aiCol.Item().PaddingTop(2).Text("Risiko-Hinweis:").FontSize(10).Bold();
                                aiCol.Item().Text(ai.RiskText).FontSize(9);
                            }
                            aiCol.Item().PaddingTop(4)
                                .Text("KI-gestützte Empfehlung (nicht bindend)")
                                .FontSize(8).Italic().FontColor(Colors.Grey.Medium);
                        });
                    }
                });

                page.Footer().Column(footer =>
                {
                    footer.Item().LineHorizontal(0.5f).LineColor("#D1D5DB");
                    footer.Item().PaddingTop(3).Row(row =>
                    {
                        if (!string.IsNullOrWhiteSpace(options.FooterLine))
                        {
                            row.RelativeItem()
                                .Text(options.FooterLine)
                                .FontSize(8)
                                .FontColor(Colors.Grey.Darken2);
                        }
                        else
                        {
                            row.RelativeItem()
                                .Text($"Erstellt: {DateTime.Now:dd.MM.yyyy}")
                                .FontSize(8)
                                .FontColor(Colors.Grey.Medium);
                        }

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

        var all = new List<(string, string?)>
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
            ("Haltungslänge [m]", lengthText),
            ("Nutzungsart", record.GetFieldValue("Nutzungsart")),
            ("Inspektionsrichtung", record.GetFieldValue("Inspektionsrichtung")),
            ("Zustandsklasse", record.GetFieldValue("Zustandsklasse")),
            ("VSA Zustandsnote", record.GetFieldValue("VSA_Zustandsnote_D")),
            ("Bearbeiter", GetMeta(project, "Bearbeiter")),
            ("Auftrag Nr.", GetMeta(project, "AuftragNr"))
        };
        // Nur Felder mit Wert anzeigen
        return FilterNonEmpty(all);
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

    internal static void ComposeKeyValueTable(IContainer container, IReadOnlyList<(string Label, string? Value)> items)
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

    internal static void ComposeTopHeader(IContainer container, byte[]? logoBytes, HaltungsprotokollPdfOptions options)
    {
        container.Column(outer =>
        {
            outer.Item().Row(row =>
            {
                row.ConstantItem(100).Height(32).AlignMiddle().Element(c =>
                {
                    if (logoBytes is not null)
                        c.Image(logoBytes).FitHeight();
                });

                row.RelativeItem().AlignRight().AlignBottom().Column(col =>
                {
                    var lines = options.SenderBlock?.Split('\n', StringSplitOptions.RemoveEmptyEntries) ?? Array.Empty<string>();
                    foreach (var line in lines)
                    {
                        col.Item().AlignRight().Text(line.Trim()).FontSize(8).FontColor("#4A5568");
                    }
                });
            });
            outer.Item().PaddingTop(2).LineHorizontal(0.5f).LineColor("#D1D5DB");
        });
    }

    internal static void ComposeTitleBar(IContainer container, string title, string? subtitle, string brand)
    {
        container.Border(0.5f).BorderColor("#D1D5DB").Row(row =>
        {
            row.ConstantItem(4).Background(brand);
            row.RelativeItem()
                .Background("#FFFFFF")
                .PaddingVertical(5)
                .PaddingHorizontal(10)
                .Column(col =>
                {
                    col.Item().AlignCenter().Text(title).FontSize(13).Bold().FontColor("#111827");
                    if (!string.IsNullOrWhiteSpace(subtitle))
                        col.Item().AlignCenter().Text(subtitle).FontSize(9).FontColor("#4B5563");
                });
        });
    }

    internal static void ComposeSectionHeading(IContainer container, string title, string brand)
    {
        var light = ResolveNutzungsartBrandLight(brand);
        container.Border(0.5f).BorderColor("#D1D5DB").Row(row =>
        {
            row.ConstantItem(3).Background(brand);
            row.RelativeItem()
                .Background(light)
                .PaddingVertical(4)
                .PaddingHorizontal(8)
                .Text(title)
                .FontSize(10)
                .Bold()
                .FontColor("#111827");
        });
    }

    internal static void ComposeHeaderTable(IContainer container, IReadOnlyList<(string Label, string? Value)> items, string brand = "#7A8A94")
    {
        if (items.Count == 0)
            return;

        // Split items into two card groups
        var half = (int)Math.Ceiling(items.Count / 2.0);
        var group1 = items.Take(half).ToList();
        var group2 = items.Skip(half).ToList();

        container.Row(row =>
        {
            row.RelativeItem().Element(c => ComposeHeaderCard(c, group1, brand));
            row.ConstantItem(6); // Spacer
            row.RelativeItem().Element(c => ComposeHeaderCard(c, group2, brand));
        });
    }

    private static void ComposeHeaderCard(IContainer container, IReadOnlyList<(string Label, string? Value)> items, string brand)
    {
        if (items.Count == 0)
            return;

        container.Border(0.5f).BorderColor("#D1D5DB").Row(cardRow =>
        {
            cardRow.ConstantItem(3).Background(brand);
            cardRow.RelativeItem()
                .Background("#FAFBFC")
                .Padding(4)
                .Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.ConstantColumn(80);
                        columns.RelativeColumn();
                    });

                    foreach (var item in items)
                    {
                        table.Cell().PaddingVertical(0.8f).Text(item.Label).FontSize(8).FontColor("#6B7280");
                        table.Cell().PaddingVertical(0.8f).Text(NormalizeValue(item.Value)).FontSize(8.5f).SemiBold().FontColor("#1F2937");
                    }
                });
        });
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
        var photoWidth = Math.Max(220f, Math.Min(options.PhotoWidth, 500f));

        container.AlignCenter().Width(photoWidth).Padding(4).Column(col =>
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

            var line1 = BuildPhotoCaptionLine1(item.Entry, index);
            if (!string.IsNullOrWhiteSpace(line1))
                col.Item().PaddingTop(2).AlignCenter().Text(line1).FontSize(9);

            var line2 = BuildPhotoCaptionLine2(item.Entry);
            if (!string.IsNullOrWhiteSpace(line2))
                col.Item().AlignCenter().Text(line2).FontSize(9);
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
                header.Cell().Element(HeaderCell).Text("Nr.").FontSize(10).SemiBold();
                header.Cell().Element(HeaderCell).Text("Code").FontSize(10).SemiBold();
                header.Cell().Element(HeaderCell).Text("Meter (m)").FontSize(10).SemiBold();
                header.Cell().Element(HeaderCell).Text("Zeit").FontSize(10).SemiBold();
                header.Cell().Element(HeaderCell).Text("Beschreibung").FontSize(10).SemiBold();
                header.Cell().Element(HeaderCell).Text("Parameter").FontSize(10).SemiBold();
            });

            var index = 1;
            foreach (var entry in entries)
            {
                table.Cell().Element(BodyCell).Text(index.ToString(CultureInfo.InvariantCulture)).FontSize(10);
                table.Cell().Element(BodyCell).Text(string.IsNullOrWhiteSpace(entry.Code) ? "-" : entry.Code.Trim()).FontSize(10);
                table.Cell().Element(BodyCell).Text(BuildObservationMeterText(entry)).FontSize(10);
                table.Cell().Element(BodyCell).Text(BuildObservationTimeText(entry)).FontSize(10);
                table.Cell().Element(BodyCell).Text(entry.Beschreibung ?? "").FontSize(10);
                table.Cell().Element(BodyCell).Text(BuildParameterShortText(entry)).FontSize(10);
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
                header.Cell().Element(HeaderCell).Text("m+").FontSize(10).SemiBold();
                header.Cell().Element(HeaderCell).Text("OP Kuerzel").FontSize(10).SemiBold();
                header.Cell().Element(HeaderCell).Text("Zustand").FontSize(10).SemiBold();
                header.Cell().Element(HeaderCell).Text("MPEG").FontSize(10).SemiBold();
                header.Cell().Element(HeaderCell).Text("Foto").FontSize(10).SemiBold();
                header.Cell().Element(HeaderCell).Text("Stufe").FontSize(10).SemiBold();
            });

            foreach (var entry in entries)
            {
                table.Cell().Element(BodyCell).Text(BuildObservationMeterStartText(entry)).FontSize(10);
                table.Cell().Element(BodyCell).Text(string.IsNullOrWhiteSpace(entry.Code) ? "-" : entry.Code.Trim()).FontSize(10);
                table.Cell().Element(BodyCell).Text(entry.Beschreibung ?? "").FontSize(10);
                table.Cell().Element(BodyCell).Text(BuildObservationMpegText(entry)).FontSize(10);
                table.Cell().Element(BodyCell).Text(BuildObservationPhotoText(entry)).FontSize(10);
                table.Cell().Element(BodyCell).Text(BuildObservationStufeText(entry)).FontSize(10);
            }
        });
    }

    private static void ComposeObservationListTable(
        IContainer container,
        IReadOnlyList<ProtocolEntry> entries,
        IReadOnlyDictionary<ProtocolEntry, string>? photoNumbers,
        string? headerBackground = null)
    {
        var headerBg = string.IsNullOrWhiteSpace(headerBackground) ? "#EAF5F9" : headerBackground;

        IContainer HeaderCell(IContainer c)
            => c.Background(headerBg).PaddingVertical(3).PaddingHorizontal(4);

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
                header.Cell().Element(HeaderCell).Text("m+").FontSize(9).SemiBold();
                header.Cell().Element(HeaderCell).Text("m-").FontSize(9).SemiBold();
                header.Cell().Element(HeaderCell).Text("OP Kürzel").FontSize(9).SemiBold();
                header.Cell().Element(HeaderCell).Text("Zustand").FontSize(9).SemiBold();
                header.Cell().Element(HeaderCell).Text("Foto").FontSize(9).SemiBold();
                header.Cell().Element(HeaderCell).Text("MPEG").FontSize(9).SemiBold();
                header.Cell().Element(HeaderCell).Text("Zeit").FontSize(9).SemiBold();
                header.Cell().Element(HeaderCell).Text("Bemerkung").FontSize(9).SemiBold();
            });

            foreach (var entry in entries)
            {
                table.Cell().Element(BodyCell).Text(FmtMeterValue(entry.MeterStart)).FontSize(9);
                table.Cell().Element(BodyCell).Text(FmtMeterValue(entry.MeterEnd)).FontSize(9);
                table.Cell().Element(BodyCell).Text(string.IsNullOrWhiteSpace(entry.Code) ? "-" : entry.Code.Trim()).FontSize(9);
                table.Cell().Element(BodyCell).Text(BuildObservationZustandTextLong(entry)).FontSize(9);
                table.Cell().Element(BodyCell).Text(ResolvePhotoNumberText(entry, photoNumbers)).FontSize(9);
                table.Cell().Element(BodyCell).Text(entry.Mpeg?.Trim() ?? "-").FontSize(9);
                table.Cell().Element(BodyCell).Text(entry.Zeit.HasValue ? FormatTime(entry.Zeit.Value) : "-").FontSize(9);
                table.Cell().Element(BodyCell).Text(BuildObservationNotesText(entry)).FontSize(9);
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

        var perPage = 2;
        var perRow = 1;
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

    /// <summary>Akzentfarbe abhaengig von Nutzungsart (dezent, nicht knallig).</summary>
    internal static string ResolveNutzungsartBrand(string nutzungsart)
    {
        var n = nutzungsart.ToUpperInvariant();
        if (n.Contains("SCHMUTZ"))
            return "#7A6242"; // braun (dezent)
        if (n.Contains("REGEN") || n.Contains("RAIN") || n.Contains("METEOR") || n.Contains("REIN"))
            return "#4A7FA5"; // blau (gedaempft)
        if (n.Contains("MISCH"))
            return "#8E4A6E"; // magenta (gedaempft)
        return "#7A8A94"; // neutral grau fuer unbekannte Nutzungsart
    }

    /// <summary>Helle Akzentfarbe fuer Hintergruende (aus brand abgeleitet).</summary>
    internal static string ResolveNutzungsartBrandLight(string brand) => brand switch
    {
        "#7A6242" => "#F5F0E8", // braun-hell (warm)
        "#4A7FA5" => "#EBF2F7", // blau-hell (kuehl)
        "#8E4A6E" => "#F5ECF1", // magenta-hell (sanft)
        _ => "#F2F4F5"          // neutral-hell (grau)
    };

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

    private static string BuildHaltungsgrafikSvg(
        double length,
        IReadOnlyList<ProtocolEntry> entries,
        IReadOnlyDictionary<ProtocolEntry, string>? photoNumbers,
        string? startNode,
        string? endNode,
        bool? flowDown,
        string brand = "#006E9C",
        int? overrideHeight = null)
    {
        var width = HaltungsgrafikWidth;
        var height = overrideHeight ?? HaltungsgrafikHeight;
        var marginTop = HaltungsgrafikMarginTop;
        var headerHeight = HaltungsgrafikHeaderHeight;
        var nodeZone = HaltungsgrafikNodeZone;
        var marginBottom = HaltungsgrafikMarginBottom;
        var lineX = HaltungsgrafikLineX;
        var tableX = HaltungsgrafikTableX;
        var rightMargin = HaltungsgrafikRightMargin;

        // top/bottom: Rohr-Anfang/-Ende mit Abstand fuer Schachtknoten und Header
        var top = (double)marginTop + headerHeight + nodeZone;
        var bottom = height - marginBottom;
        var pipeWidth = 14d;
        var pipeHalf = pipeWidth / 2.0;

        var tableWidth = Math.Max(1d, width - tableX - rightMargin);
        var colMeterWidth = 54d;
        var colCodeWidth = 56d;
        var colMpegWidth = 62d;
        var colFotoWidth = 38d;
        var colStufeWidth = 40d;
        var colZustandWidth = Math.Max(120d, tableWidth - (colMeterWidth + colCodeWidth + colMpegWidth + colFotoWidth + colStufeWidth));

        var colMeterX = tableX;
        var colCodeX = colMeterX + colMeterWidth;
        var colZustandX = colCodeX + colCodeWidth;
        var colMpegX = colZustandX + colZustandWidth;
        var colFotoX = colMpegX + colMpegWidth;
        var colStufeX = colFotoX + colFotoWidth;

        var headerY = marginTop + 11;
        var headerLineY = marginTop + headerHeight + 2;

        var sb = new StringBuilder();
        sb.Append($"<svg xmlns='http://www.w3.org/2000/svg' width='{width}' height='{height}' viewBox='0 0 {width} {height}'>");
        sb.Append("<rect width='100%' height='100%' fill='#FFFFFF'/>");

        // --- Defs: Gradients, Patterns, Filters, ClipPaths ---
        sb.Append("<defs>");

        // 3D-Rohr-Gradient (horizontal: hell -> dunkel -> hell)
        sb.Append($"<linearGradient id='pipeGrad' x1='0' y1='0' x2='1' y2='0'>");
        sb.Append($"<stop offset='0%' stop-color='{brand}' stop-opacity='0.3'/>");
        sb.Append($"<stop offset='35%' stop-color='{brand}' stop-opacity='0.85'/>");
        sb.Append($"<stop offset='50%' stop-color='{brand}' stop-opacity='1'/>");
        sb.Append($"<stop offset='65%' stop-color='{brand}' stop-opacity='0.85'/>");
        sb.Append($"<stop offset='100%' stop-color='{brand}' stop-opacity='0.3'/>");
        sb.Append("</linearGradient>");

        // Schaden-Schraffur-Pattern (diagonale rote Linien)
        sb.Append("<pattern id='dmgHatch' patternUnits='userSpaceOnUse' width='6' height='6' patternTransform='rotate(45)'>");
        sb.Append("<line x1='0' y1='0' x2='0' y2='6' stroke='#D64541' stroke-width='2'/>");
        sb.Append("</pattern>");

        // Drop-Shadow fuer Schachtknoten
        sb.Append("<filter id='nodeShadow' x='-30%' y='-30%' width='160%' height='160%'>");
        sb.Append("<feDropShadow dx='1' dy='1' stdDeviation='1.5' flood-color='#00000033'/>");
        sb.Append("</filter>");

        // Boden-Schraffur (horizontale Linien fuer Erdreich)
        sb.Append("<pattern id='groundHatch' patternUnits='userSpaceOnUse' width='4' height='4'>");
        sb.Append("<line x1='0' y1='2' x2='4' y2='2' stroke='#8B7355' stroke-width='0.7'/>");
        sb.Append("</pattern>");

        // Blauer Wasser-Gradient fuer Fliessrichtung
        sb.Append("<linearGradient id='flowGrad' x1='0' y1='0' x2='0' y2='1'>");
        sb.Append("<stop offset='0%' stop-color='#2196F3' stop-opacity='0.9'/>");
        sb.Append("<stop offset='100%' stop-color='#1565C0' stop-opacity='1'/>");
        sb.Append("</linearGradient>");

        // Glow-Filter fuer Fliessrichtungspfeil
        sb.Append("<filter id='flowGlow' x='-50%' y='-50%' width='200%' height='200%'>");
        sb.Append("<feGaussianBlur in='SourceAlpha' stdDeviation='2' result='blur'/>");
        sb.Append("<feFlood flood-color='#2196F3' flood-opacity='0.3'/>");
        sb.Append("<feComposite in2='blur' operator='in'/>");
        sb.Append("<feMerge><feMergeNode/><feMergeNode in='SourceGraphic'/></feMerge>");
        sb.Append("</filter>");

        // ClipPath-Definitionen fuer jede Spalte
        sb.Append($"<clipPath id='clipMeter'><rect x='{Svg(colMeterX)}' y='0' width='{Svg(colMeterWidth - 2)}' height='{height}'/></clipPath>");
        sb.Append($"<clipPath id='clipCode'><rect x='{Svg(colCodeX)}' y='0' width='{Svg(colCodeWidth - 2)}' height='{height}'/></clipPath>");
        sb.Append($"<clipPath id='clipZustand'><rect x='{Svg(colZustandX)}' y='0' width='{Svg(colZustandWidth - 2)}' height='{height}'/></clipPath>");
        sb.Append($"<clipPath id='clipMpeg'><rect x='{Svg(colMpegX)}' y='0' width='{Svg(colMpegWidth - 2)}' height='{height}'/></clipPath>");
        sb.Append($"<clipPath id='clipFoto'><rect x='{Svg(colFotoX)}' y='0' width='{Svg(colFotoWidth - 2)}' height='{height}'/></clipPath>");
        sb.Append($"<clipPath id='clipStufe'><rect x='{Svg(colStufeX)}' y='0' width='{Svg(colStufeWidth - 2)}' height='{height}'/></clipPath>");
        sb.Append("</defs>");

        // --- Card-Style Spaltenheader ---
        var hdrBgY = marginTop - 2;
        var hdrBgH = headerHeight + 4;
        sb.Append($"<rect x='{Svg(tableX - 4)}' y='{Svg(hdrBgY)}' width='{Svg(tableWidth + 8)}' height='{Svg(hdrBgH)}' rx='4' ry='4' fill='#FFFFFF' stroke='#D1D5DB' stroke-width='0.6'/>");

        sb.Append($"<text x='{Svg(colMeterX)}' y='{Svg(headerY)}' font-size='11' font-weight='bold' fill='#1F2937' font-family='sans-serif'>m+</text>");
        sb.Append($"<text x='{Svg(colCodeX)}' y='{Svg(headerY)}' font-size='11' font-weight='bold' fill='#1F2937' font-family='sans-serif'>OP Kürzel</text>");
        sb.Append($"<text x='{Svg(colZustandX)}' y='{Svg(headerY)}' font-size='11' font-weight='bold' fill='#1F2937' font-family='sans-serif'>Zustand</text>");
        sb.Append($"<text x='{Svg(colMpegX)}' y='{Svg(headerY)}' font-size='11' font-weight='bold' fill='#1F2937' font-family='sans-serif'>MPEG</text>");
        sb.Append($"<text x='{Svg(colFotoX)}' y='{Svg(headerY)}' font-size='11' font-weight='bold' fill='#1F2937' font-family='sans-serif'>Foto</text>");
        sb.Append($"<text x='{Svg(colStufeX)}' y='{Svg(headerY)}' font-size='11' font-weight='bold' fill='#1F2937' font-family='sans-serif'>Stufe</text>");

        // Vertikale Spaltentrennlinien
        foreach (var cx in new[] { colCodeX, colZustandX, colMpegX, colFotoX, colStufeX })
            sb.Append($"<line x1='{Svg(cx - 3)}' y1='{Svg(hdrBgY + 3)}' x2='{Svg(cx - 3)}' y2='{Svg(hdrBgY + hdrBgH - 3)}' stroke='#D1D5DB' stroke-width='0.5'/>");

        sb.Append($"<line x1='{Svg(tableX - 4)}' y1='{Svg(headerLineY)}' x2='{Svg(width - rightMargin + 4)}' y2='{Svg(headerLineY)}' stroke='#D1D5DB' stroke-width='0.8'/>");

        // --- Alternating tick background stripes ---
        var tickStep = ChooseTickStep(length);
        var ticks = BuildTicks(length, tickStep);
        for (var ti = 0; ti < ticks.Count - 1; ti++)
        {
            if (ti % 2 != 0) continue;
            var yStart = MapToLine(ticks[ti], length, top, bottom);
            var yEnd = MapToLine(ticks[ti + 1], length, top, bottom);
            sb.Append($"<rect x='{Svg(lineX - pipeHalf - 10)}' y='{Svg(yStart)}' width='{Svg(pipeWidth + 20)}' height='{Svg(yEnd - yStart)}' fill='#F5F5F5' rx='2'/>");
        }

        // --- Tick-Markierungen (Messband-Stil) ---
        foreach (var meter in ticks)
        {
            var y = MapToLine(meter, length, top, bottom);
            var isMainTick = Math.Abs(meter % (tickStep * 2)) < 0.001 || meter == 0 || Math.Abs(meter - length) < 0.001;
            var tickLen = isMainTick ? 8d : 5d;
            var strokeW = isMainTick ? "1.2" : "0.8";
            sb.Append($"<line x1='{Svg(lineX - pipeHalf - tickLen)}' y1='{Svg(y)}' x2='{Svg(lineX - pipeHalf)}' y2='{Svg(y)}' stroke='#4A5568' stroke-width='{strokeW}'/>");
            sb.Append($"<text x='{Svg(lineX - pipeHalf - tickLen - 3)}' y='{Svg(y + 3)}' font-size='{(isMainTick ? "11" : "10")}' text-anchor='end' fill='#1F2937' font-family='sans-serif'>{meter:0.00}</text>");
        }

        // --- 3D-Rohr (Gradient-Rechteck statt einfache Linie) ---
        sb.Append($"<rect x='{Svg(lineX - pipeHalf)}' y='{Svg(top)}' width='{Svg(pipeWidth)}' height='{Svg(bottom - top)}' fill='url(#pipeGrad)' rx='3'/>");
        // Rohrwand-Randlinien
        sb.Append($"<line x1='{Svg(lineX - pipeHalf)}' y1='{Svg(top)}' x2='{Svg(lineX - pipeHalf)}' y2='{Svg(bottom)}' stroke='{brand}' stroke-width='0.8' opacity='0.6'/>");
        sb.Append($"<line x1='{Svg(lineX + pipeHalf)}' y1='{Svg(top)}' x2='{Svg(lineX + pipeHalf)}' y2='{Svg(bottom)}' stroke='{brand}' stroke-width='0.8' opacity='0.6'/>");

        // --- Bodenlinien an Schachtpositionen (Erdreich-Darstellung) ---
        var hasAbort = entries.Any(e => IsAbortCode(e));
        var groundW = 30d;
        sb.Append($"<rect x='{Svg(lineX - groundW)}' y='{Svg(top - 2)}' width='{Svg(groundW * 2)}' height='4' fill='url(#groundHatch)'/>");

        if (!hasAbort)
            sb.Append($"<rect x='{Svg(lineX - groundW)}' y='{Svg(bottom - 2)}' width='{Svg(groundW * 2)}' height='4' fill='url(#groundHatch)'/>");

        // --- Schachtknoten: Kreis sitzt UEBER/UNTER dem Rohranfang/-ende ---
        // Rohranfang (top) und Rohrende (bottom) sind der Uebergang Schacht->Haltung.
        // Der Schachtdeckel-Kreis sitzt daher oberhalb bzw. unterhalb des Rohres.
        var nodeR = 11d;
        var topNodeCY = top - nodeR;      // Oberer Schacht: Kreismitte oberhalb Rohranfang
        var bottomNodeCY = bottom + nodeR; // Unterer Schacht: Kreismitte unterhalb Rohrende

        // --- Oberer Schachtknoten: Realistischer Schachtdeckel ---
        sb.Append($"<circle cx='{Svg(lineX)}' cy='{Svg(topNodeCY)}' r='{Svg(nodeR)}' fill='#F5F5F5' stroke='#4A5568' stroke-width='1.8' filter='url(#nodeShadow)'/>");
        sb.Append($"<circle cx='{Svg(lineX)}' cy='{Svg(topNodeCY)}' r='{Svg(nodeR * 0.65)}' fill='none' stroke='{brand}' stroke-width='1.2'/>");
        // Radiale Linien (Schachtdeckel-Muster)
        for (var angle = 0; angle < 360; angle += 45)
        {
            var rad = angle * Math.PI / 180.0;
            var x2 = lineX + Math.Cos(rad) * nodeR * 0.9;
            var y2 = topNodeCY + Math.Sin(rad) * nodeR * 0.9;
            sb.Append($"<line x1='{Svg(lineX)}' y1='{Svg(topNodeCY)}' x2='{Svg(x2)}' y2='{Svg(y2)}' stroke='{brand}' stroke-width='0.8' opacity='0.5'/>");
        }

        // --- Unterer Schachtknoten: Nur anzeigen wenn kein Abbruch ---
        if (!hasAbort)
        {
            sb.Append($"<circle cx='{Svg(lineX)}' cy='{Svg(bottomNodeCY)}' r='{Svg(nodeR)}' fill='#F5F5F5' stroke='#4A5568' stroke-width='1.8' filter='url(#nodeShadow)'/>");
            sb.Append($"<circle cx='{Svg(lineX)}' cy='{Svg(bottomNodeCY)}' r='{Svg(nodeR * 0.65)}' fill='none' stroke='{brand}' stroke-width='1.2'/>");
            sb.Append($"<circle cx='{Svg(lineX)}' cy='{Svg(bottomNodeCY)}' r='3' fill='{brand}'/>");
        }

        // --- Schacht-Beschriftungen ---
        if (!string.IsNullOrWhiteSpace(startNode))
        {
            var startLabelY = Math.Max(8, topNodeCY - nodeR - 4);
            sb.Append($"<text x='{Svg(lineX)}' y='{Svg(startLabelY)}' font-size='13' font-weight='600' text-anchor='middle' fill='#1F2937' font-family='sans-serif'>{EscapeSvgText(startNode)}</text>");
        }
        if (!hasAbort && !string.IsNullOrWhiteSpace(endNode))
        {
            var endLabelY = Math.Min(height - 2, bottomNodeCY + nodeR + 12);
            sb.Append($"<text x='{Svg(lineX)}' y='{Svg(endLabelY)}' font-size='13' font-weight='600' text-anchor='middle' fill='#1F2937' font-family='sans-serif'>{EscapeSvgText(endNode)}</text>");
        }

        // --- Fliessrichtung (Blauer Pfeil mit Wellen) ---
        if (flowDown.HasValue)
        {
            var flowColor = "#2196F3";
            var flowColorDark = "#1565C0";
            var arrowX = lineX; // Pfeil zentriert auf dem Rohr
            var arrowY = flowDown.Value
                ? top + (bottom - top) * 0.30
                : top + (bottom - top) * 0.70;
            var aW = 10d; // Pfeilbreite (Halbbreite)
            var aH = 14d; // Pfeilhoehe

            // Grosser blauer Pfeil mit Glow-Effekt
            if (flowDown.Value)
            {
                // Pfeil nach unten
                sb.Append($"<polygon points='{Svg(arrowX - aW)},{Svg(arrowY - aH / 2)} {Svg(arrowX + aW)},{Svg(arrowY - aH / 2)} {Svg(arrowX)},{Svg(arrowY + aH / 2)}' " +
                          $"fill='url(#flowGrad)' stroke='white' stroke-width='1.5' filter='url(#flowGlow)'/>");
            }
            else
            {
                // Pfeil nach oben
                sb.Append($"<polygon points='{Svg(arrowX - aW)},{Svg(arrowY + aH / 2)} {Svg(arrowX + aW)},{Svg(arrowY + aH / 2)} {Svg(arrowX)},{Svg(arrowY - aH / 2)}' " +
                          $"fill='url(#flowGrad)' stroke='white' stroke-width='1.5' filter='url(#flowGlow)'/>");
            }

            // Wellenlinien (3 Wellen) links neben dem Rohr
            var waveX = lineX - pipeHalf - 10; // Links neben dem Rohr
            var waveCenterY = (top + bottom) / 2.0;
            var waveLen = 40d; // Laenge der Wellenlinien
            var waveAmp = 2.5; // Amplitude der Wellen
            var waveSpacing = 6d; // Abstand zwischen Wellenlinien

            for (var wi = -1; wi <= 1; wi++)
            {
                var wy = waveCenterY + wi * waveSpacing;
                var waveStartY = wy - waveLen / 2;
                // SVG-Pfad fuer Sinuswelle (vertikal, da Rohr vertikal)
                var wavePath = new StringBuilder();
                wavePath.Append($"M {Svg(waveX)} {Svg(waveStartY)}");
                var segments = 8;
                var segLen = waveLen / segments;
                for (var si = 0; si < segments; si++)
                {
                    var cy1 = waveStartY + si * segLen + segLen * 0.33;
                    var cy2 = waveStartY + si * segLen + segLen * 0.66;
                    var ey = waveStartY + (si + 1) * segLen;
                    var dx = (si % 2 == 0) ? waveAmp : -waveAmp;
                    wavePath.Append($" C {Svg(waveX + dx)} {Svg(cy1)}, {Svg(waveX + dx)} {Svg(cy2)}, {Svg(waveX)} {Svg(ey)}");
                }
                sb.Append($"<path d='{wavePath}' fill='none' stroke='{flowColor}' stroke-width='1.2' opacity='0.6'/>");
            }

            // Kleiner Richtungspfeil am Ende der Wellen
            var waveArrowY = flowDown.Value ? waveCenterY + waveLen / 2 + 4 : waveCenterY - waveLen / 2 - 4;
            var waTip = flowDown.Value ? waveArrowY + 5 : waveArrowY - 5;
            sb.Append($"<polygon points='{Svg(waveX - 3)},{Svg(waveArrowY)} {Svg(waveX + 3)},{Svg(waveArrowY)} {Svg(waveX)},{Svg(waTip)}' " +
                      $"fill='{flowColor}' opacity='0.7'/>");

            // Rotierter Label-Text
            var midY = (top + bottom) / 2.0;
            var flowLabel = flowDown.Value ? "\u2193 Fliessrichtung" : "\u2191 Fliessrichtung";
            var rotation = flowDown.Value ? 90 : -90;
            sb.Append($"<text x='{Svg(waveX - 10)}' y='{Svg(midY)}' font-size='9' fill='{flowColorDark}' font-weight='600' text-anchor='middle' font-family='sans-serif' " +
                      $"transform='rotate({rotation} {Svg(waveX - 10)} {Svg(midY)})'>{EscapeSvgText(flowLabel)}</text>");
        }

        // --- Streckenschaeden (schraffierte Rohr-Abschnitte) ---
        foreach (var entry in entries)
        {
            if (!entry.IsStreckenschaden || entry.MeterStart is null || entry.MeterEnd is null)
                continue;

            var y1 = MapToLine(entry.MeterStart.Value, length, top, bottom);
            var y2 = MapToLine(entry.MeterEnd.Value, length, top, bottom);
            if (y2 < y1)
                (y1, y2) = (y2, y1);

            var segH = Math.Max(2, y2 - y1);
            // Schraffierter Bereich ueber dem Rohr
            sb.Append($"<rect x='{Svg(lineX - pipeHalf - 1)}' y='{Svg(y1)}' width='{Svg(pipeWidth + 2)}' height='{Svg(segH)}' fill='url(#dmgHatch)' opacity='0.7' rx='2'/>");
            // Rote Randlinien
            sb.Append($"<line x1='{Svg(lineX - pipeHalf - 1)}' y1='{Svg(y1)}' x2='{Svg(lineX + pipeHalf + 1)}' y2='{Svg(y1)}' stroke='#D64541' stroke-width='1.5'/>");
            sb.Append($"<line x1='{Svg(lineX - pipeHalf - 1)}' y1='{Svg(y1 + segH)}' x2='{Svg(lineX + pipeHalf + 1)}' y2='{Svg(y1 + segH)}' stroke='#D64541' stroke-width='1.5'/>");
        }

        // --- Punktschaeden (schadenstypische Symbole) ---
        foreach (var entry in entries)
        {
            if (entry.IsStreckenschaden)
                continue;
            if (IsAbortCode(entry) || IsLateralConnection(entry))
                continue;

            var pos = entry.MeterStart ?? entry.MeterEnd;
            if (pos is null)
                continue;

            var y = MapToLine(pos.Value, length, top, bottom);
            var category = ClassifyDamageSymbol(entry);
            var symColor = GetDamageSymbolColor(category, brand);
            RenderDamageSymbol(sb, lineX, y, category, symColor);
        }

        // --- Abbruch-Symbol (zwei rote schraege Parallelstriche) ---
        foreach (var entry in entries)
        {
            if (!IsAbortCode(entry))
                continue;

            var pos = entry.MeterStart ?? entry.MeterEnd;
            if (pos is null)
                continue;

            var ay = MapToLine(pos.Value, length, top, bottom);
            var abortLen = 12d; // Laenge der Striche
            var abortGap = 4d;  // Abstand zwischen den beiden Parallelstrichen
            var abortStroke = 2.5;
            // Erster Strich: schraeg von links-oben nach rechts-unten
            sb.Append($"<line x1='{Svg(lineX - abortLen / 2 - abortGap / 2)}' y1='{Svg(ay - abortLen / 2)}' " +
                      $"x2='{Svg(lineX + abortLen / 2 - abortGap / 2)}' y2='{Svg(ay + abortLen / 2)}' " +
                      $"stroke='#D64541' stroke-width='{Svg(abortStroke)}' stroke-linecap='round'/>");
            // Zweiter Strich: parallel verschoben
            sb.Append($"<line x1='{Svg(lineX - abortLen / 2 + abortGap / 2)}' y1='{Svg(ay - abortLen / 2)}' " +
                      $"x2='{Svg(lineX + abortLen / 2 + abortGap / 2)}' y2='{Svg(ay + abortLen / 2)}' " +
                      $"stroke='#D64541' stroke-width='{Svg(abortStroke)}' stroke-linecap='round'/>");
        }

        // --- Seitenanschluesse (Laterale Rohrstutzen nach Uhrzeitposition) ---
        foreach (var entry in entries)
        {
            if (!IsLateralConnection(entry))
                continue;

            var pos = entry.MeterStart ?? entry.MeterEnd;
            if (pos is null)
                continue;

            var connY = MapToLine(pos.Value, length, top, bottom);
            var clockHour = ExtractClockHour(entry);
            if (clockHour is null)
            {
                // Kein Uhrzeitwert: Standardmaessig nach rechts (3 Uhr)
                clockHour = 3;
            }

            // Winkel berechnen: 12 Uhr = 0 Grad (nach oben), Uhrzeigersinn
            // In der Grafik: 9 Uhr = links, 3 Uhr = rechts
            // Mapping: 3h=rechts(0°), 6h=unten(90°), 9h=links(180°), 12h=oben(270°)
            var angleDeg = (clockHour.Value - 3) * 30.0; // 30° pro Stunde, 3 Uhr = 0°
            var angleRad = angleDeg * Math.PI / 180.0;
            var stubLen = 22d; // Laenge des Rohrstutzens
            var stubEndX = lineX + Math.Cos(angleRad) * (pipeHalf + stubLen);
            var stubEndY = connY + Math.Sin(angleRad) * (pipeHalf + stubLen);
            var stubStartX = lineX + Math.Cos(angleRad) * pipeHalf;
            var stubStartY = connY + Math.Sin(angleRad) * pipeHalf;

            // Rohrstutzen-Linie
            sb.Append($"<line x1='{Svg(stubStartX)}' y1='{Svg(stubStartY)}' x2='{Svg(stubEndX)}' y2='{Svg(stubEndY)}' " +
                      $"stroke='#6B7280' stroke-width='3' stroke-linecap='round'/>");
            // Anschluss-Kreis am Ende
            sb.Append($"<circle cx='{Svg(stubEndX)}' cy='{Svg(stubEndY)}' r='3.5' fill='#6B7280' stroke='white' stroke-width='1'/>");
            // Uhrzeitlabel
            var labelOffsetX = Math.Cos(angleRad) * 8;
            var labelOffsetY = Math.Sin(angleRad) * 8;
            var anchor = clockHour.Value >= 7 && clockHour.Value <= 11 ? "end" : "start";
            if (clockHour.Value == 12 || clockHour.Value == 6) anchor = "middle";
            sb.Append($"<text x='{Svg(stubEndX + labelOffsetX)}' y='{Svg(stubEndY + labelOffsetY + 3)}' " +
                      $"font-size='8' fill='#4B5563' text-anchor='{anchor}' font-family='sans-serif'>" +
                      $"{clockHour.Value}h</text>");
        }

        // --- Beobachtungs-Labels ---
        var labels = BuildHaltungsgrafikLabels(entries, length, top, bottom, photoNumbers, brand);
        LayoutHaltungsgrafikLabels(labels, top, bottom);

        // --- Bezugslinien (Verbindung Beobachtung auf Leitung -> Label-Zeile) ---
        var refStartX = lineX + pipeHalf + 2;
        var refEndX = colMeterX - 4;
        foreach (var label in labels)
        {
            // Kleiner Punkt am Rohr (Abgangspunkt)
            sb.Append($"<circle cx='{Svg(refStartX)}' cy='{Svg(label.TargetY)}' r='1.5' fill='{label.LineColor}' opacity='0.5'/>");
            // Verbindungslinie vom Rohr zur Label-Zeile
            sb.Append($"<line x1='{Svg(refStartX)}' y1='{Svg(label.TargetY)}' x2='{Svg(refEndX)}' y2='{Svg(label.LabelY)}' " +
                      $"stroke='{label.LineColor}' stroke-width='0.6' opacity='0.35' stroke-dasharray='2,1.5'/>");
            // Kleiner Punkt am Label-Ende
            sb.Append($"<circle cx='{Svg(refEndX)}' cy='{Svg(label.LabelY)}' r='1' fill='{label.LineColor}' opacity='0.4'/>");
        }

        foreach (var label in labels)
        {
            var labelY = label.LabelY;

            var textColor = "#111827";
            var fontSize = Svg(label.FontSize);
            sb.Append($"<text clip-path='url(#clipMeter)' x='{Svg(colMeterX)}' y='{Svg(labelY + 3)}' font-size='{fontSize}' text-anchor='start' fill='{textColor}' font-family='sans-serif'>" +
                      $"{EscapeSvgText(label.MeterText)}</text>");
            sb.Append($"<text clip-path='url(#clipCode)' x='{Svg(colCodeX)}' y='{Svg(labelY + 3)}' font-size='{fontSize}' font-weight='600' text-anchor='start' fill='{brand}' font-family='sans-serif'>" +
                      $"{EscapeSvgText(label.CodeText)}</text>");
            sb.Append($"<text clip-path='url(#clipZustand)' x='{Svg(colZustandX)}' y='{Svg(labelY + 3)}' font-size='{fontSize}' text-anchor='start' fill='{textColor}' font-family='sans-serif'>" +
                      $"{EscapeSvgText(label.ZustandText)}</text>");
            sb.Append($"<text clip-path='url(#clipMpeg)' x='{Svg(colMpegX)}' y='{Svg(labelY + 3)}' font-size='{fontSize}' text-anchor='start' fill='#4B5563' font-family='sans-serif'>" +
                      $"{EscapeSvgText(label.MpegText)}</text>");
            sb.Append($"<text clip-path='url(#clipFoto)' x='{Svg(colFotoX)}' y='{Svg(labelY + 3)}' font-size='{fontSize}' text-anchor='start' fill='#4B5563' font-family='sans-serif'>" +
                      $"{EscapeSvgText(label.FotoText)}</text>");
            sb.Append($"<text clip-path='url(#clipStufe)' x='{Svg(colStufeX)}' y='{Svg(labelY + 3)}' font-size='{fontSize}' text-anchor='start' fill='{textColor}' font-family='sans-serif'>" +
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

        return Shorten(desc, 120);
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
        // Nur isolierte Kuerzel (z.B. "BCD", "BBCC") am Anfang entfernen, keine normalen Woerter
        text = Regex.Replace(text, @"^\s*[A-Z0-9]{1,6}(?:\s+[A-Z0-9]{1,6})?(?=\s|$)", "", RegexOptions.None);

        // Import-Artefakte: Trailing Hash/ID-Fragmente entfernen
        // Beispiele: "-80631_6e c06c5c-c9", "137124-fc", "80fd46-", "f5fa69-828"
        text = Regex.Replace(text, @"\s+-?\d+_[0-9a-fA-F]+(?:\s+[0-9a-fA-F-]+)*\s*$", "");
        text = Regex.Replace(text, @"\s+[0-9a-fA-F]{5,}-[0-9a-fA-F]*\s*$", "");

        // Klartext: Redundante Phrasen kuerzen
        text = Regex.Replace(text, @"\s*Richtungs[aä]nderung\b", "", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"^Anderer Grund f[uü]r Abbruch der Inspektion,?\s*", "", RegexOptions.IgnoreCase);

        text = text.Trim(' ', '-', '–', ':', ',', '/');

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
        IReadOnlyDictionary<ProtocolEntry, string>? photoNumbers,
        string brand = "#006E9C")
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
                LineColor = isRange ? "#D64541" : GetDamageSymbolColor(ClassifyDamageSymbol(entry), brand)
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
        var minGap = Math.Clamp(available / Math.Max(1, labels.Count), 9d, 15d);
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

        var fontSize = minGap < 10 ? 9 : minGap < 12 ? 10 : 11;
        foreach (var label in labels)
            label.FontSize = fontSize;
    }

    private sealed record HaltungsgrafikScale(string? LengthText, string? ScaleText);

    private static HaltungsgrafikScale BuildHaltungsgrafikScale(double? length, int? svgHeight = null)
    {
        if (!length.HasValue || length.Value <= 0)
            return new HaltungsgrafikScale(null, null);

        var ratio = ComputeScaleRatio(length.Value, svgHeight);
        var lengthText = $"Haltungslänge: {length.Value:0.00} m";
        var scaleText = ratio.HasValue ? $"Massstab: 1:{ratio.Value}" : "";
        return new HaltungsgrafikScale(lengthText, scaleText);
    }

    private static int? ComputeScaleRatio(double length, int? svgHeight = null)
    {
        if (length <= 0)
            return null;

        var effectiveHeight = svgHeight ?? HaltungsgrafikHeight;
        var plotHeight = effectiveHeight - HaltungsgrafikMarginTop - HaltungsgrafikMarginBottom - HaltungsgrafikHeaderHeight - HaltungsgrafikNodeZone;
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

        // Lookup fuer bestehende Entries (zum Foto-Merge)
        var keyToEntry = new Dictionary<string, ProtocolEntry>();
        foreach (var entry in active)
        {
            var key = BuildEntryKey(entry);
            keyToEntry.TryAdd(key, entry);
        }

        var result = new List<ProtocolEntry>(active);

        var fromFindings = BuildImportedEntriesFromFindings(record.VsaFindings);
        foreach (var entry in fromFindings)
        {
            var key = BuildEntryKey(entry);
            if (deletedKeys.Contains(key))
                continue;

            if (existingKeys.Contains(key))
            {
                // Entry existiert bereits → Fotos aus Finding in bestehenden Entry mergen
                if (keyToEntry.TryGetValue(key, out var existing))
                    MergePhotoPaths(existing, entry);
                continue;
            }

            result.Add(entry);
            existingKeys.Add(key);
            keyToEntry.TryAdd(key, entry);
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

    /// <summary>Fotos aus source in target mergen (ohne Duplikate).</summary>
    private static void MergePhotoPaths(ProtocolEntry target, ProtocolEntry source)
    {
        if (source.FotoPaths is null || source.FotoPaths.Count == 0)
            return;

        var existing = new HashSet<string>(
            target.FotoPaths.Select(p => p.Replace('\\', '/').ToUpperInvariant()));

        foreach (var path in source.FotoPaths)
        {
            var normalized = path.Replace('\\', '/').ToUpperInvariant();
            if (existing.Add(normalized))
                target.FotoPaths.Add(path);
        }
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

    public static (string? Start, string? End) SplitHoldingNodes(string? holdingLabel)
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

    /// <summary>Prüft ob ein Protokolleintrag einen Inspektions-Abbruch darstellt (BDC-Codes).</summary>
    private static bool IsAbortCode(ProtocolEntry entry)
    {
        var code = (entry.Code ?? "").Trim().ToUpperInvariant();
        // BDC* = Abbruch der Inspektion (Hindernis, hoher Wasserstand, Versagen der Ausruestung, etc.)
        return code.StartsWith("BDC", StringComparison.Ordinal);
    }

    /// <summary>Prüft ob ein Protokolleintrag ein Seitenanschluss (lateral connection) ist.</summary>
    private static bool IsLateralConnection(ProtocolEntry entry)
    {
        var code = (entry.Code ?? "").Trim().ToUpperInvariant();
        // BAG* = Anschluss einragend, BAH* = Anschluss falsch/beschaedigt etc.
        // BCA* = Bestandsaufnahme Anschluss (Formstueck, Sattelanschluss)
        if (code.StartsWith("BAG", StringComparison.Ordinal) ||
            code.StartsWith("BAH", StringComparison.Ordinal) ||
            code.StartsWith("BCAA", StringComparison.Ordinal) ||
            code.StartsWith("BCAB", StringComparison.Ordinal))
            return true;

        // Fallback: Beschreibung enthält "Anschluss" oder "Seiteneinlauf"
        var desc = entry.Beschreibung ?? entry.CodeMeta?.Notes ?? "";
        if (desc.Contains("Anschluss", StringComparison.OrdinalIgnoreCase) ||
            desc.Contains("Seiteneinlauf", StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }

    /// <summary>Extrahiert die Uhrzeitposition (1-12) eines Protokolleintrags.</summary>
    private static int? ExtractClockHour(ProtocolEntry entry)
    {
        var parameters = entry.CodeMeta?.Parameters;
        if (parameters is null || parameters.Count == 0)
            return null;

        // Prioritaet: vsa.uhr.von > ClockPos1
        var raw = GetParam(parameters, "vsa.uhr.von")
               ?? GetParam(parameters, "ClockPos1")
               ?? GetParam(parameters, "Quantifizierung1");

        if (string.IsNullOrWhiteSpace(raw))
            return null;

        // Versuche die Uhrzeit zu parsen (z.B. "3", "3 Uhr", "03:00", "9")
        var cleaned = Regex.Match(raw.Trim(), @"(\d{1,2})");
        if (cleaned.Success && int.TryParse(cleaned.Groups[1].Value, out var hour) && hour >= 1 && hour <= 12)
            return hour;

        return null;
    }

    /// <summary>Klassifiziert einen Schaden nach Symbol-Kategorie anhand des VSA-Codes.</summary>
    private static string ClassifyDamageSymbol(ProtocolEntry entry)
    {
        var code = (entry.Code ?? "").Trim().ToUpperInvariant();
        if (code.StartsWith("BAA", StringComparison.Ordinal)) return "crack";        // Rissbildung
        if (code.StartsWith("BAB", StringComparison.Ordinal)) return "break";        // Bruch / Einsturz
        if (code.StartsWith("BAC", StringComparison.Ordinal)) return "deformation";  // Deformation
        if (code.StartsWith("BAD", StringComparison.Ordinal)) return "leak";         // Undichtheit
        if (code.StartsWith("BAE", StringComparison.Ordinal)) return "offset";       // Versatz
        if (code.StartsWith("BAF", StringComparison.Ordinal)) return "surface";      // Oberflaechenschaden
        if (code.StartsWith("BAI", StringComparison.Ordinal)) return "obstacle";     // Hindernis
        if (code.StartsWith("BAJ", StringComparison.Ordinal)) return "roots";        // Wurzeleinwuchs
        if (code.StartsWith("BAK", StringComparison.Ordinal)) return "infiltration"; // Infiltration
        if (code.StartsWith("BAL", StringComparison.Ordinal)) return "exfiltration"; // Exfiltration
        if (code.StartsWith("BBA", StringComparison.Ordinal)) return "deposit";      // Ablagerung
        if (code.StartsWith("BBB", StringComparison.Ordinal)) return "obstacle";     // Verstopfung
        return "default";
    }

    /// <summary>Gibt die harmonisierte Farbe fuer eine Schadens-Kategorie zurueck.</summary>
    private static string GetDamageSymbolColor(string category, string fallback = "#006E9C")
    {
        return category switch
        {
            "crack" or "break"                          => "#D64541", // Rot - strukturell kritisch
            "deformation" or "offset" or "surface"      => "#E67E22", // Orange - Verformung / Oberflaeche
            "leak" or "infiltration" or "exfiltration"   => "#2196F3", // Blau - Wasser
            "roots"                                      => "#27AE60", // Gruen - biologisch
            "deposit"                                    => "#8B6914", // Braun - Ablagerung
            "obstacle"                                   => "#6B7280", // Grau - Hindernis
            _ => fallback
        };
    }

    /// <summary>Rendert ein schadenstypisches SVG-Symbol zentriert auf (cx, cy).</summary>
    private static void RenderDamageSymbol(StringBuilder sb, double cx, double cy, string category, string color, double s = 5)
    {
        // Weisser Hintergrund-Kreis fuer Kontrast auf dem Rohr-Gradient
        sb.Append($"<circle cx='{Svg(cx)}' cy='{Svg(cy)}' r='{Svg(s + 1.5)}' fill='white' opacity='0.85'/>");

        switch (category)
        {
            case "crack": // Blitz-Zickzack (Rissbildung)
                sb.Append($"<path d='M {Svg(cx)},{Svg(cy - s)} L {Svg(cx + s * 0.5)},{Svg(cy - s * 0.15)} " +
                          $"L {Svg(cx - s * 0.5)},{Svg(cy + s * 0.15)} L {Svg(cx)},{Svg(cy + s)}' " +
                          $"stroke='{color}' stroke-width='2' fill='none' stroke-linecap='round' stroke-linejoin='round'/>");
                break;

            case "break": // X-Kreuz (Bruch / Einsturz)
                sb.Append($"<line x1='{Svg(cx - s * 0.7)}' y1='{Svg(cy - s * 0.7)}' x2='{Svg(cx + s * 0.7)}' y2='{Svg(cy + s * 0.7)}' " +
                          $"stroke='{color}' stroke-width='2' stroke-linecap='round'/>");
                sb.Append($"<line x1='{Svg(cx + s * 0.7)}' y1='{Svg(cy - s * 0.7)}' x2='{Svg(cx - s * 0.7)}' y2='{Svg(cy + s * 0.7)}' " +
                          $"stroke='{color}' stroke-width='2' stroke-linecap='round'/>");
                break;

            case "deformation": // Gequetschte Ellipse (Deformation)
                sb.Append($"<ellipse cx='{Svg(cx)}' cy='{Svg(cy)}' rx='{Svg(s)}' ry='{Svg(s * 0.5)}' " +
                          $"fill='none' stroke='{color}' stroke-width='1.8'/>");
                break;

            case "leak": // Wassertropfen (Undichtheit)
                sb.Append($"<path d='M {Svg(cx)},{Svg(cy - s)} " +
                          $"Q {Svg(cx + s * 0.7)},{Svg(cy + s * 0.2)} {Svg(cx)},{Svg(cy + s)} " +
                          $"Q {Svg(cx - s * 0.7)},{Svg(cy + s * 0.2)} {Svg(cx)},{Svg(cy - s)} Z' " +
                          $"fill='{color}' opacity='0.85'/>");
                break;

            case "offset": // Versatz-Stufe
                sb.Append($"<path d='M {Svg(cx - s)},{Svg(cy - s * 0.5)} L {Svg(cx)},{Svg(cy - s * 0.5)} " +
                          $"L {Svg(cx)},{Svg(cy + s * 0.5)} L {Svg(cx + s)},{Svg(cy + s * 0.5)}' " +
                          $"stroke='{color}' stroke-width='2' fill='none' stroke-linecap='round' stroke-linejoin='round'/>");
                break;

            case "surface": // Wellige Linie (Oberflaechenschaden)
                sb.Append($"<path d='M {Svg(cx - s)},{Svg(cy)} " +
                          $"Q {Svg(cx - s * 0.5)},{Svg(cy - s * 0.6)} {Svg(cx)},{Svg(cy)} " +
                          $"Q {Svg(cx + s * 0.5)},{Svg(cy + s * 0.6)} {Svg(cx + s)},{Svg(cy)}' " +
                          $"stroke='{color}' stroke-width='2' fill='none' stroke-linecap='round'/>");
                break;

            case "obstacle": // Gefuelltes Quadrat (Hindernis / Verstopfung)
                sb.Append($"<rect x='{Svg(cx - s * 0.6)}' y='{Svg(cy - s * 0.6)}' " +
                          $"width='{Svg(s * 1.2)}' height='{Svg(s * 1.2)}' " +
                          $"fill='{color}' rx='1'/>");
                break;

            case "roots": // Y-Gabel (Wurzeleinwuchs)
                sb.Append($"<line x1='{Svg(cx)}' y1='{Svg(cy + s)}' x2='{Svg(cx)}' y2='{Svg(cy)}' " +
                          $"stroke='{color}' stroke-width='2' stroke-linecap='round'/>");
                sb.Append($"<line x1='{Svg(cx)}' y1='{Svg(cy)}' x2='{Svg(cx - s * 0.6)}' y2='{Svg(cy - s)}' " +
                          $"stroke='{color}' stroke-width='1.8' stroke-linecap='round'/>");
                sb.Append($"<line x1='{Svg(cx)}' y1='{Svg(cy)}' x2='{Svg(cx + s * 0.6)}' y2='{Svg(cy - s)}' " +
                          $"stroke='{color}' stroke-width='1.8' stroke-linecap='round'/>");
                break;

            case "infiltration": // Pfeil nach innen (Wassereintritt)
                sb.Append($"<line x1='{Svg(cx + s)}' y1='{Svg(cy)}' x2='{Svg(cx - s * 0.3)}' y2='{Svg(cy)}' " +
                          $"stroke='{color}' stroke-width='2' stroke-linecap='round'/>");
                sb.Append($"<path d='M {Svg(cx + s * 0.2)},{Svg(cy - s * 0.4)} L {Svg(cx - s * 0.3)},{Svg(cy)} L {Svg(cx + s * 0.2)},{Svg(cy + s * 0.4)}' " +
                          $"stroke='{color}' stroke-width='1.8' fill='none' stroke-linecap='round' stroke-linejoin='round'/>");
                break;

            case "exfiltration": // Pfeil nach aussen (Wasseraustritt)
                sb.Append($"<line x1='{Svg(cx - s)}' y1='{Svg(cy)}' x2='{Svg(cx + s * 0.3)}' y2='{Svg(cy)}' " +
                          $"stroke='{color}' stroke-width='2' stroke-linecap='round'/>");
                sb.Append($"<path d='M {Svg(cx - s * 0.2)},{Svg(cy - s * 0.4)} L {Svg(cx + s * 0.3)},{Svg(cy)} L {Svg(cx - s * 0.2)},{Svg(cy + s * 0.4)}' " +
                          $"stroke='{color}' stroke-width='1.8' fill='none' stroke-linecap='round' stroke-linejoin='round'/>");
                break;

            case "deposit": // Geschichtete Linien (Ablagerung)
                sb.Append($"<line x1='{Svg(cx - s * 0.8)}' y1='{Svg(cy)}' x2='{Svg(cx + s * 0.8)}' y2='{Svg(cy)}' " +
                          $"stroke='{color}' stroke-width='1.8' stroke-linecap='round'/>");
                sb.Append($"<line x1='{Svg(cx - s * 0.5)}' y1='{Svg(cy + s * 0.5)}' x2='{Svg(cx + s * 0.5)}' y2='{Svg(cy + s * 0.5)}' " +
                          $"stroke='{color}' stroke-width='1.5' stroke-linecap='round'/>");
                sb.Append($"<line x1='{Svg(cx - s * 0.3)}' y1='{Svg(cy + s)}' x2='{Svg(cx + s * 0.3)}' y2='{Svg(cy + s)}' " +
                          $"stroke='{color}' stroke-width='1.2' stroke-linecap='round'/>");
                break;

            default: // Diamant (Allgemein / unbekannt)
                sb.Append($"<polygon points='{Svg(cx)},{Svg(cy - s)} {Svg(cx + s)},{Svg(cy)} {Svg(cx)},{Svg(cy + s)} {Svg(cx - s)},{Svg(cy)}' " +
                          $"fill='{color}' stroke='white' stroke-width='1.2'/>");
                break;
        }
    }

    private static string ResolveInspectionDate(Project project, HaltungRecord record, ProtocolDocument doc)
    {
        // Prioritaet: Haltungs-spezifisches Aufnahmedatum vor Projekt-Metadaten
        var recordDate = record.GetFieldValue("Datum_Jahr");
        if (!string.IsNullOrWhiteSpace(recordDate))
            return ExtractSingleDate(recordDate.Trim());

        var meta = GetMeta(project, "InspektionsDatum");
        if (!string.IsNullOrWhiteSpace(meta))
            return ExtractSingleDate(meta.Trim());

        return doc.Current.CreatedAt.ToLocalTime().ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);
    }

    /// <summary>Aus einem Datumsbereich (z.B. "05.11.2025 - 11.11.2025") nur das erste Datum extrahieren.</summary>
    private static string ExtractSingleDate(string dateText)
    {
        if (string.IsNullOrWhiteSpace(dateText))
            return dateText;

        // "05.11.2025 - 11.11.2025" → "05.11.2025"
        var separators = new[] { " - ", " – ", " bis ", "–", "-" };
        foreach (var sep in separators)
        {
            var idx = dateText.IndexOf(sep, StringComparison.OrdinalIgnoreCase);
            if (idx > 4) // mindestens ein Datum davor (dd.MM oder aehnlich)
            {
                var candidate = dateText.Substring(0, idx).Trim();
                if (candidate.Length >= 8) // plausibles Datum
                    return candidate;
            }
        }

        return dateText;
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
        int maxPhotos,
        Dictionary<string, string?> resolveCache)
    {
        var list = new List<string>();
        foreach (var raw in photoPaths)
        {
            if (string.IsNullOrWhiteSpace(raw))
                continue;

            var resolved = ResolvePhotoPath(projectRootAbs, raw, resolveCache);
            if (string.IsNullOrWhiteSpace(resolved) || !File.Exists(resolved))
                continue;

            list.Add(resolved);
            if (list.Count >= maxPhotos)
                break;
        }

        return list;
    }

    private static string ResolvePhotoPath(
        string projectRootAbs,
        string raw,
        Dictionary<string, string?> resolveCache)
    {
        var normalized = raw.Replace('/', Path.DirectorySeparatorChar);
        var fileName = Path.GetFileName(normalized);

        if (Path.IsPathRooted(normalized))
        {
            if (File.Exists(normalized))
                return normalized;

            if (!string.IsNullOrWhiteSpace(fileName))
            {
                var rootedSearchRoot = Path.GetDirectoryName(normalized);
                while (!string.IsNullOrWhiteSpace(rootedSearchRoot))
                {
                    if (Directory.Exists(rootedSearchRoot))
                    {
                        var rootedMatch = FindFileByName(rootedSearchRoot, fileName, resolveCache);
                        if (!string.IsNullOrWhiteSpace(rootedMatch))
                            return rootedMatch;
                    }

                    rootedSearchRoot = Path.GetDirectoryName(rootedSearchRoot);
                }
            }
        }

        if (string.IsNullOrWhiteSpace(projectRootAbs))
            return normalized;

        var direct = Path.Combine(projectRootAbs, normalized);
        if (File.Exists(direct))
            return direct;

        if (!string.IsNullOrWhiteSpace(fileName))
        {
            var candidates = new[] { "Fotos", "Photos", "Bilder", "Images", "Fotos_TV", "TV_Fotos", "Foto", "Photo" };
            foreach (var sub in candidates)
            {
                var candidate = Path.Combine(projectRootAbs, sub, fileName);
                if (File.Exists(candidate))
                    return candidate;
            }

            var parent = Path.GetDirectoryName(projectRootAbs);
            if (!string.IsNullOrWhiteSpace(parent))
            {
                var parentCandidate = Path.Combine(parent, normalized);
                if (File.Exists(parentCandidate))
                    return parentCandidate;
            }

            var projectMatch = FindFileByName(projectRootAbs, fileName, resolveCache);
            if (!string.IsNullOrWhiteSpace(projectMatch))
                return projectMatch;

            if (!string.IsNullOrWhiteSpace(parent))
            {
                var parentMatch = FindFileByName(parent, fileName, resolveCache);
                if (!string.IsNullOrWhiteSpace(parentMatch))
                    return parentMatch;
            }
        }

        return direct;
    }

    private static string? FindFileByName(
        string? searchRoot,
        string fileName,
        Dictionary<string, string?> cache)
    {
        if (string.IsNullOrWhiteSpace(searchRoot) || string.IsNullOrWhiteSpace(fileName))
            return null;
        if (!Directory.Exists(searchRoot))
            return null;

        var cacheKey = $"{searchRoot}|{fileName}";
        if (cache.TryGetValue(cacheKey, out var cached))
            return cached;

        string? found = null;
        try
        {
            found = Directory.EnumerateFiles(searchRoot, fileName, SearchOption.AllDirectories).FirstOrDefault();
        }
        catch
        {
            found = null;
        }

        cache[cacheKey] = found;
        return found;
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
    public int PhotosPerRow { get; init; } = 1;
    public int PhotosPerPage { get; init; } = 2;
    public int MaxPhotosPerEntry { get; init; } = int.MaxValue;
    public float PhotoWidth { get; init; } = 500f;
    public float PhotoHeight { get; init; } = 255f;
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

