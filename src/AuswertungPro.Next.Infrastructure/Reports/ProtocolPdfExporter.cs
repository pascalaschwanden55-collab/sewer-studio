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
using static AuswertungPro.Next.Application.Reports.ProtocolPdfObservationText;
using static AuswertungPro.Next.Application.Reports.ProtocolPdfValueFormatting;

namespace AuswertungPro.Next.Application.Reports;

public sealed class ProtocolPdfExporter : IProtocolPdfExporter
{
    private readonly IProtocolPdfAssetResolver _assets;

    public ProtocolPdfExporter()
        : this(new ProtocolPdfAssetFileResolver())
    {
    }

    public ProtocolPdfExporter(IProtocolPdfAssetResolver assets)
        => _assets = assets ?? throw new ArgumentNullException(nameof(assets));

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

        var resolvedEntries = ProtocolPdfEntryResolver.ResolveEntriesForExport(record, doc);
        var length = ProtocolPdfEntryResolver.ResolveHoldingLength(record, resolvedEntries);
        var entries = CounterInspectionStationingNormalizer.NormalizeForExport(resolvedEntries, length)
            .OrderBy(e => e.MeterStart ?? e.MeterEnd ?? double.MaxValue)
            .ToList();
        var unknownGaps = InspectionGapDetector.DetectUnknownGaps(entries, length);
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
            ? ProtocolPdfPhotoSection.BuildItems(
                _assets,
                entries,
                projectRootAbs,
                options.MaxPhotosPerEntry,
                haltungFotoDir)
            : new List<ProtocolPdfPhotoSection.PhotoItem>();
        var photoNumberMap = ProtocolPdfPhotoSection.BuildNumberMap(photoItems);
        var (startNode, endNode) = SplitHoldingNodes(holdingLabel);
        var flowDown = ParseFlowDirection(record.GetFieldValue("Inspektionsrichtung"));

        var grafikHeight = HaltungsgrafikExportSizing.ChooseSvgHeight(entries.Count);
        var svg = options.IncludeHaltungsgrafik && length.HasValue && length.Value > 0
            ? BuildHaltungsgrafikSvg(length.Value, entries, photoNumberMap, startNode, endNode, flowDown, brand, overrideHeight: grafikHeight, unknownGaps: unknownGaps)
            : null;

        var headerItems = BuildHaltungsprotokollHeaderTable(project, record, inspectionDate, length, holdingLabel);
        var logoBytes = _assets.ResolveLogoBytes(options, projectRootAbs);

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
                            ComposeObservationListTable(c, entries, photoNumberMap, options.CodeCatalog, ResolveNutzungsartBrandLight(brand), unknownGaps));
                    }

                    if (options.IncludePhotos)
                        ProtocolPdfPhotoSection.Compose(
                            col,
                            photoItems,
                            project,
                            record,
                            inspectionDate,
                            holdingLabel,
                            options,
                            _assets,
                            brand,
                            title);

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

    /// <summary>
    /// Betreiber der Haltung. Die Haltung selbst weiss es am besten (Spalte
    /// "Eigentuemer"); das Projekt-Metadatum ist nur der Rueckfall. Vorher wurde
    /// NUR das Projekt gelesen - und dessen Standardwert ist "Privat", sodass auf
    /// AWU-Protokollen "Betreiber Privat" stand.
    /// </summary>
    internal static string? ResolveBetreiber(Project project, HaltungRecord record)
    {
        var ausHaltung = record.GetFieldValue("Eigentuemer");
        if (!string.IsNullOrWhiteSpace(ausHaltung))
            return ausHaltung.Trim();

        return GetMeta(project, "Eigentuemer");
    }

    internal static IReadOnlyList<(string Label, string? Value)> BuildHaltungsprotokollHeaderTable(
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
        // Ohne eigene Beschreibung sind GEP und Projektname derselbe Text -
        // zweimal dieselbe Zeile traegt nichts.
        var gep = string.Equals(projektname?.Trim(), project.Name?.Trim(), StringComparison.Ordinal)
            ? null
            : project.Name;
        var lengthText = length.HasValue ? length.Value.ToString("0.00", CultureInfo.InvariantCulture) : record.GetFieldValue("Haltungslaenge_m");

        var all = new List<(string, string?)>
        {
            ("GEP", gep),
            ("Projektname", projektname),
            ("Nr.", record.GetFieldValue("NR")),
            ("Ort", ort),
            ("Strasse", strasse),
            ("Datum", inspectionDate),
            ("Haltung", holdingLabel),
            ("Betreiber", ResolveBetreiber(project, record)),
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

                        // Zustandsklasse als Ampel-Chip - dieselben Farben wie in
                        // Tabelle und Excel-Export (ExcelReportStyle ist die Quelle).
                        var ampel = item.Label.StartsWith("Zustandsklasse", StringComparison.Ordinal)
                            ? ResolveZustandsklassenFarbe(item.Value)
                            : null;
                        if (ampel is not null)
                        {
                            table.Cell().PaddingVertical(0.8f).AlignLeft().Element(z => z
                                .Background(ampel.Value.Hintergrund)
                                .PaddingVertical(0.5f).PaddingHorizontal(6)
                                .Text(NormalizeValue(item.Value))
                                .FontSize(8.5f).SemiBold()
                                .FontColor(ampel.Value.Schrift));
                            continue;
                        }

                        table.Cell().PaddingVertical(0.8f).Text(NormalizeValue(item.Value)).FontSize(8.5f).SemiBold().FontColor("#1F2937");
                    }
                });
        });
    }

    /// <summary>
    /// Ampelfarbe der Zustandsklasse 0..4 - exakt die Werte aus
    /// <see cref="AuswertungPro.Next.Infrastructure.Export.Excel.ExcelReportStyle"/>,
    /// damit App-Tabelle, Excel-Bericht und PDF dieselbe Sprache sprechen.
    /// </summary>
    internal static (string Hintergrund, string Schrift)? ResolveZustandsklassenFarbe(string? wert)
    {
        var klasse = (wert ?? "").Trim();
        if (klasse.Length == 0)
            return null;

        foreach (var regel in AuswertungPro.Next.Infrastructure.Export.Excel.ExcelReportStyle.Zustandsklassen)
        {
            if (!string.Equals(regel.Wert, klasse, StringComparison.Ordinal))
                continue;

            var hex = "#" + regel.Farbe[^6..];
            // Rot und Orange brauchen weisse Schrift, die hellen Stufen dunkle.
            var weiss = klasse is "0" or "1";
            return (hex, weiss ? "#FFFFFF" : "#1F2937");
        }

        return null;
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
        string? headerBackground = null,
        IReadOnlyList<InspectionGap>? unknownGaps = null)
    {
        var headerBg = string.IsNullOrWhiteSpace(headerBackground) ? "#EAF5F9" : headerBackground;

        IContainer HeaderCell(IContainer c)
            => c.Background(headerBg).PaddingVertical(3).PaddingHorizontal(4);

        static IContainer BodyCell(IContainer c)
            => c.BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).PaddingVertical(2).PaddingHorizontal(4);

        static IContainer GapCell(IContainer c)
            => c.Background("#F3F4F6").BorderTop(0.8f).BorderBottom(0.8f).BorderColor(Colors.Grey.Medium)
                .PaddingVertical(3).PaddingHorizontal(4);

        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.ConstantColumn(38); // m+
                columns.ConstantColumn(38); // m-
                columns.ConstantColumn(55); // OP
                columns.RelativeColumn(6);  // Zustand
                columns.ConstantColumn(40); // Foto
                columns.ConstantColumn(78); // MPEG (Videodateinamen brechen sonst mitten im Wort)
                columns.ConstantColumn(34); // Zeit
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
            var gapsWritten = false;
            void WriteUnknownGaps()
            {
                if (gapsWritten || unknownGaps is null || unknownGaps.Count == 0)
                    return;

                foreach (var gap in unknownGaps)
                {
                    table.Cell().ColumnSpan(8).Element(GapCell)
                        .Text($"Unbekannter Bereich {FmtMeterValue(gap.StartMeter)}-{FmtMeterValue(gap.EndMeter)} m (nicht inspiziert)")
                        .FontSize(9).Bold().FontColor("#4B5563");
                }

                gapsWritten = true;
            }

            foreach (var segment in InspectionSegmenter.Segments(entries))
            {
                if (!string.IsNullOrWhiteSpace(segment.Title))
                {
                    WriteUnknownGaps();
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
                    table.Cell().Element(BodyCell)
                        .Text(ProtocolPdfPhotoSection.ResolveNumberText(entry, photoNumbers))
                        .FontSize(9);
                    table.Cell().Element(BodyCell).Text(entry.Mpeg?.Trim() ?? "-").FontSize(7.5f);
                    table.Cell().Element(BodyCell).Text(entry.Zeit.HasValue ? FormatTime(entry.Zeit.Value) : "-").FontSize(9);
                    table.Cell().Element(BodyCell).Text(BuildObservationNotesText(entry)).FontSize(9);
                }
            }

            WriteUnknownGaps();
        });
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

    // Dünne Delegation zu HaltungsgrafikSvgBuilder (verhaltensneutral extrahiert).
    private static string BuildHaltungsgrafikSvg(
        double length,
        IReadOnlyList<ProtocolEntry> entries,
        IReadOnlyDictionary<ProtocolEntry, string>? photoNumbers,
        string? startNode,
        string? endNode,
        bool? flowDown,
        string brand = "#006E9C",
        int? overrideHeight = null,
        IReadOnlyList<InspectionGap>? unknownGaps = null)
        => HaltungsgrafikSvgBuilder.BuildHaltungsgrafikSvg(length, entries, photoNumbers, startNode, endNode, flowDown, brand, overrideHeight, unknownGaps);

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

    // Dünne Delegationen zu HoldingNodeParser (verhaltensneutral extrahiert).
    public static (string? Start, string? End) SplitHoldingNodes(string? holdingLabel)
        => HoldingNodeParser.SplitHoldingNodes(holdingLabel);

    private static bool? ParseFlowDirection(string? text)
        => HoldingNodeParser.ParseFlowDirection(text);

    // Dünne Delegationen zu DamageSymbolClassifier (verhaltensneutral extrahiert).
    internal static string ResolveDamageSymbolCategory(string? rawCode)
        => DamageSymbolClassifier.ResolveDamageSymbolCategory(rawCode);

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

