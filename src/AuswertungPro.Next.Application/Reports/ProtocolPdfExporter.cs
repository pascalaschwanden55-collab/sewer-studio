using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

using AuswertungPro.Next.Application.Protocol;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using static AuswertungPro.Next.Application.Reports.ProtocolPdfAssetResolver;
using static AuswertungPro.Next.Application.Reports.ProtocolPdfObservationText;
using static AuswertungPro.Next.Application.Reports.ProtocolPdfValueFormatting;

namespace AuswertungPro.Next.Application.Reports;

public sealed class ProtocolPdfExporter
{

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
                                        // Pfad-Containment: ein Foto-Pfad darf nicht aus dem Projektordner
                                        // ausbrechen (absolute Pfade / "..") -> sonst verwerfen. (Audit)
                                        var abs = Path.GetFullPath(Path.Combine(projectRootAbs,
                                            rel.Replace('/', Path.DirectorySeparatorChar)));
                                        var rootGuard = Path.GetFullPath(projectRootAbs);
                                        if (!rootGuard.EndsWith(Path.DirectorySeparatorChar))
                                            rootGuard += Path.DirectorySeparatorChar;
                                        if (!abs.StartsWith(rootGuard, StringComparison.OrdinalIgnoreCase) || !File.Exists(abs))
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

        var entries = ProtocolPdfEntryResolver.ResolveEntriesForExport(record, doc)
            .OrderBy(e => e.MeterStart ?? e.MeterEnd ?? double.MaxValue)
            .ToList();

        var length = ProtocolPdfEntryResolver.ResolveHoldingLength(record, entries);
        var inspectionDate = ResolveInspectionDate(project, record, doc);
        var nutzungsart = record.GetFieldValue("Nutzungsart")?.Trim() ?? "";
        var brand = ResolveNutzungsartBrand(nutzungsart);
        var holdingLabel = record.GetFieldValue("Haltungsname");
        if (string.IsNullOrWhiteSpace(holdingLabel))
            holdingLabel = doc.HaltungId;

        var title = string.IsNullOrWhiteSpace(holdingLabel)
            ? $"{options.Title} - {inspectionDate}"
            : $"{options.Title} - {inspectionDate} - {holdingLabel}";

        // Verteil-Foto-Ordner der Haltung (Fotos\Haltungen\<H>) als bevorzugter Such-Ort — dort liegen
        // die Fotos nach der Medienverteilung, auch wenn der gespeicherte FotoPath auf einen alten
        // Import-Ort zeigt. (Ordnernamen entsprechen ProjectStructure.Fotos/FotosHaltungen.)
        var fotoSan = AuswertungPro.Next.Application.Common.ProjectPathResolver.SanitizePathSegment(holdingLabel);
        var haltungFotoDir = string.IsNullOrWhiteSpace(fotoSan)
            ? null
            : Path.Combine(projectRootAbs, "Fotos", "Haltungen", fotoSan);

        var photoItems = options.IncludePhotos
            ? BuildPhotoItems(entries, projectRootAbs, options.MaxPhotosPerEntry, haltungFotoDir)
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

                    // === Detaillierte Beobachtungstabelle unter der Grafik ===
                    // Fliessend/paginierend, mit Foto-/MPEG-/Zeit-Spalten, Klartext-Zustand (Katalog)
                    // und Trennzeile Haupt-/Gegeninspektion.
                    if (options.IncludeObservationTable && entries.Count > 0)
                    {
                        col.Item().PaddingTop(8).Element(c => ComposeSectionHeading(c, "Befunde", brand));
                        col.Item().PaddingTop(2).Element(c =>
                            ComposeObservationListTable(c, entries, photoNumberMap, options.CodeCatalog, ResolveNutzungsartBrandLight(brand)));
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
        ICodeCatalogProvider? catalog = null,
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

            // Segmentierung: Hauptinspektion / Gegeninspektion (Trennzeile am ersten Abbruchcode).
            foreach (var segment in InspectionSegmenter.Segments(entries))
            {
                if (!string.IsNullOrWhiteSpace(segment.Title))
                {
                    table.Cell().ColumnSpan(8)
                        .Background("#EEF2F4").BorderTop(0.8f).BorderColor(Colors.Grey.Medium)
                        .PaddingVertical(3).PaddingHorizontal(4)
                        .Text(segment.Title).FontSize(9).Bold().FontColor("#374151");
                }

                foreach (var entry in segment.Entries)
                {
                    table.Cell().Element(BodyCell).Text(FmtMeterValue(entry.MeterStart)).FontSize(9);
                    table.Cell().Element(BodyCell).Text(FmtMeterValue(entry.MeterEnd)).FontSize(9);
                    table.Cell().Element(BodyCell).Text(string.IsNullOrWhiteSpace(entry.Code) ? "-" : entry.Code.Trim()).FontSize(9);
                    table.Cell().Element(BodyCell).Text(ObservationZustandBuilder.Build(entry, catalog)).FontSize(9);
                    table.Cell().Element(BodyCell).Text(ResolvePhotoNumberText(entry, photoNumbers)).FontSize(9);
                    table.Cell().Element(BodyCell).Text(entry.Mpeg?.Trim() ?? "-").FontSize(9);
                    table.Cell().Element(BodyCell).Text(entry.Zeit.HasValue ? FormatTime(entry.Zeit.Value) : "-").FontSize(9);
                    table.Cell().Element(BodyCell).Text(BuildObservationNotesText(entry)).FontSize(9);
                }
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

    // Dünne Delegation zu ProtocolZustandText (verhaltensneutral extrahiert).
    private static string Shorten(string text, int max)
        => ProtocolZustandText.Shorten(text, max);

    // Dünne Delegation zu ProtocolTextHelpers (verhaltensneutral extrahiert).
    private static string EscapeSvgText(string text)
        => ProtocolTextHelpers.EscapeSvgText(text);

    private static List<PhotoItem> BuildPhotoItems(
        IReadOnlyList<ProtocolEntry> entries,
        string projectRootAbs,
        int maxPhotosPerEntry,
        string? preferredFolder = null)
    {
        var items = new List<PhotoItem>();
        var resolveCache = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in entries)
        {
            if (entry.FotoPaths is null || entry.FotoPaths.Count == 0)
                continue;

            var resolved = ResolvePhotoPaths(entry.FotoPaths, projectRootAbs, maxPhotosPerEntry, resolveCache, preferredFolder);
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

    // Dünne Delegation zu HaltungsgrafikSvgBuilder (verhaltensneutral extrahiert).
    private static string BuildHaltungsgrafikSvg(
        double length,
        IReadOnlyList<ProtocolEntry> entries,
        IReadOnlyDictionary<ProtocolEntry, string>? photoNumbers,
        string? startNode,
        string? endNode,
        bool? flowDown,
        string brand = "#006E9C",
        int? overrideHeight = null)
        => HaltungsgrafikSvgBuilder.BuildHaltungsgrafikSvg(length, entries, photoNumbers, startNode, endNode, flowDown, brand, overrideHeight);

    // Dünne Delegationen zu ProtocolZustandText (verhaltensneutral extrahiert).
    private static string BuildHaltungsgrafikZustandText(ProtocolEntry entry)
        => ProtocolZustandText.BuildHaltungsgrafikZustandText(entry);

    private static string BuildObservationZustandTextLong(ProtocolEntry entry)
        => ProtocolZustandText.BuildObservationZustandTextLong(entry);

    private static string NormalizeZustandDescription(string? raw, string? code)
        => ProtocolZustandText.NormalizeZustandDescription(raw, code);

    // Dünne Delegationen zu HaltungsgrafikLabelLayout (verhaltensneutral extrahiert).
    private static List<HaltungsgrafikLabel> BuildHaltungsgrafikLabels(
        IReadOnlyList<ProtocolEntry> entries,
        double length,
        double top,
        double bottom,
        IReadOnlyDictionary<ProtocolEntry, string>? photoNumbers,
        string brand = "#006E9C")
        => HaltungsgrafikLabelLayout.BuildHaltungsgrafikLabels(entries, length, top, bottom, photoNumbers, brand);

    private static void LayoutHaltungsgrafikLabels(List<HaltungsgrafikLabel> labels, double top, double bottom)
        => HaltungsgrafikLabelLayout.LayoutHaltungsgrafikLabels(labels, top, bottom);

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

    // Skala/Tick-Mathematik liegt verhaltensneutral in HaltungsgrafikScaleCalculator (unit-getestet);
    // Geometrie-Konstanten liegen in HaltungsgrafikSvgBuilder (verhaltensneutral extrahiert).
    private static int? ComputeScaleRatio(double length, int? svgHeight = null)
    {
        var effectiveHeight = svgHeight ?? HaltungsgrafikSvgBuilder.Height;
        var plotHeight = effectiveHeight - HaltungsgrafikSvgBuilder.MarginTop - HaltungsgrafikSvgBuilder.MarginBottom - HaltungsgrafikSvgBuilder.HeaderHeight - HaltungsgrafikSvgBuilder.NodeZone;
        return HaltungsgrafikScaleCalculator.ComputeScaleRatio(length, plotHeight);
    }

    private static List<double> BuildTicks(double length, double step)
        => HaltungsgrafikScaleCalculator.BuildTicks(length, step);

    private static double ChooseTickStep(double length)
        => HaltungsgrafikScaleCalculator.ChooseTickStep(length);

    // Dünne Delegationen zu HoldingNodeParser (verhaltensneutral extrahiert).
    public static (string? Start, string? End) SplitHoldingNodes(string? holdingLabel)
        => HoldingNodeParser.SplitHoldingNodes(holdingLabel);

    private static bool? ParseFlowDirection(string? text)
        => HoldingNodeParser.ParseFlowDirection(text);

    // Dünne Delegationen zu ProtocolTextHelpers (verhaltensneutral extrahiert).
    /// <summary>Prueft ob ein Protokolleintrag einen Inspektions-Abbruch darstellt (BDC-Codes).</summary>
    private static bool IsAbortCode(ProtocolEntry entry)
        => ProtocolTextHelpers.IsAbortCode(entry);

    /// <summary>Prueft ob ein Protokolleintrag ein Seitenanschluss (lateral connection) ist.</summary>
    private static bool IsLateralConnection(ProtocolEntry entry)
        => ProtocolTextHelpers.IsLateralConnection(entry);

    /// <summary>Extrahiert die Uhrzeitposition (1-12) eines Protokolleintrags.</summary>
    private static int? ExtractClockHour(ProtocolEntry entry)
        => ProtocolTextHelpers.ExtractClockHour(entry);

    /// <summary>Klassifiziert einen Schaden nach Symbol-Kategorie anhand des VSA-Codes.</summary>
    private static string ClassifyDamageSymbol(ProtocolEntry entry)
        => DamageSymbolClassifier.ResolveDamageSymbolCategory(entry.Code);

    // Dünne Delegationen zu DamageSymbolClassifier (verhaltensneutral extrahiert).
    internal static string ResolveDamageSymbolCategory(string? rawCode)
        => DamageSymbolClassifier.ResolveDamageSymbolCategory(rawCode);

    private static string GetDamageSymbolColor(string category, string fallback = "#006E9C")
        => DamageSymbolClassifier.GetDamageSymbolColor(category, fallback);

    /// <summary>Dünne Delegation zu DamageSymbolRenderer (verhaltensneutral extrahiert).</summary>
    private static void RenderDamageSymbol(StringBuilder sb, double cx, double cy, string category, string color, double s = 5)
        => DamageSymbolRenderer.RenderDamageSymbol(sb, cx, cy, category, color, s);

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
        => ProtocolTextHelpers.ExtractSingleDate(dateText);

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

    /// <summary>Detaillierte Beobachtungstabelle unter der Haltungsgrafik anzeigen (Foto/MPEG/Zeit/Klartext-Zustand).</summary>
    public bool IncludeObservationTable { get; init; } = true;

    /// <summary>
    /// Optionaler Code-Katalog zur Klartext-Formatierung der Quantifizierung
    /// (z.B. "Winkel = 45°" statt "Q1=45"). Null = heutiges Verhalten (Rohtext).
    /// </summary>
    public AuswertungPro.Next.Application.Protocol.ICodeCatalogProvider? CodeCatalog { get; init; }
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

