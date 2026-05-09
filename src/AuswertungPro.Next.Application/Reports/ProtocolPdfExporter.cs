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

public sealed partial class ProtocolPdfExporter
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
                page.DefaultTextStyle(x => x.FontSize(10).FontFamily("Arial"));

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

        // Dynamische SVG-Hoehe: waechst mit Anzahl Codes (Tabelle rechts) und Haltungslaenge (Distanz-Achse links).
        // Damit bleiben Beobachtungstabelle und Pipe-Skala lesbar, auch bei langen Haltungen mit vielen Schaeden.
        // Bei grafikHeight > 1000 greift die Slicing-Logik (Seite 1 / Seite 2).
        var grafikHeight = ComputeDynamicGrafikHeight(length, entries.Count);
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
                page.DefaultTextStyle(x => x.FontSize(10).FontFamily("Arial"));

                page.Header().Element(c => ComposeTopHeader(c, logoBytes, options));

                page.Content().Column(col =>
                {
                    // === SEITE 1: Titel + Header + Haltungsgrafik ===
                    // Strategie: Solange die Skalierung lesbar bleibt (>= 60%), packen wir die ganze Grafik
                    // auf eine Seite mit FitArea (proportional verkleinert -> Massstab passt sich an).
                    // Erst wenn die Skalierung zu klein wuerde (< 60% -> Beschriftungen unleserlich),
                    // splitten wir SVG-seitig auf mehrere Seiten mit fester Skala.
                    const float grafikDisplayHeightPage1 = 440f;
                    const float grafikDisplayHeightPageN = 700f;
                    const float grafikContainerWidth = 535f; // A4 - Margins - Padding/Border
                    const int svgViewBoxWidth = 770;
                    const float minReadableScale = 0.60f; // Grenzwert: ab hier wird Schrift unleserlich

                    col.Item().PaddingTop(2).Element(c => ComposeTitleBar(c, title, options.Subtitle, brand));
                    col.Item().PaddingTop(4).Element(c => ComposeHeaderTable(c, headerItems, brand));

                    if (options.IncludeHaltungsgrafik)
                    {
                        col.Item().PaddingTop(4).Element(c => ComposeSectionHeading(c, "Haltungsgrafik", brand));
                        if (!string.IsNullOrWhiteSpace(svg))
                        {
                            var scale = BuildHaltungsgrafikScale(length, grafikHeight);

                            // Was waere die natuerliche Display-Hoehe (ohne Skalierung) im Container?
                            var naturalDisplayHeight = grafikHeight * grafikContainerWidth / svgViewBoxWidth;
                            // Wie stark muss FitArea() skalieren um auf eine Seite zu passen?
                            var requiredScale = grafikDisplayHeightPage1 / naturalDisplayHeight;

                            if (requiredScale >= minReadableScale)
                            {
                                // Skalierung bleibt lesbar -> alles auf Seite 1, FitArea regelt das proportional.
                                col.Item().PaddingTop(2).Element(c =>
                                    ComposeGrafikFrame(c, svg!, scale, grafikDisplayHeightPage1));
                            }
                            else
                            {
                                // Skalierung zu stark -> Multi-Page-Slicing mit FESTER Skala (Schrift bleibt lesbar).
                                var pxPerSvgUnit = grafikContainerWidth / svgViewBoxWidth;
                                var maxSvgPerPage1 = grafikDisplayHeightPage1 / pxPerSvgUnit;
                                var maxSvgPerPageN = grafikDisplayHeightPageN / pxPerSvgUnit;

                                double cursorSvg = 0;
                                bool firstSlice = true;
                                while (cursorSvg < grafikHeight - 0.5)
                                {
                                    var maxThisPage = firstSlice ? maxSvgPerPage1 : maxSvgPerPageN;
                                    var sliceSvgHeight = Math.Min(grafikHeight - cursorSvg, maxThisPage);
                                    var sliceDisplayHeight = (float)(sliceSvgHeight * pxPerSvgUnit);

                                    var sliceSvg = ExtractSvgRange(svg!, svgViewBoxWidth, grafikHeight,
                                        (int)cursorSvg, (int)Math.Round(sliceSvgHeight));

                                    if (!firstSlice)
                                    {
                                        col.Item().PageBreak();
                                        col.Item().PaddingTop(4).Element(c =>
                                            ComposeSectionHeading(c, "Haltungsgrafik (Fortsetzung)", brand));
                                    }
                                    col.Item().PaddingTop(2).Element(c =>
                                        ComposeGrafikFrame(c, sliceSvg, firstSlice ? scale : null, sliceDisplayHeight));

                                    cursorSvg += sliceSvgHeight;
                                    firstSlice = false;
                                }
                            }
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


    internal static void ComposeSectionHeading(IContainer container, string title, string brand)
    {
        // Mutig-professionell: solider Brand-Block links als typografischer Anker,
        // grosser Titel UPPERCASE darueber. Brand-Linie unten als doppelte Linienfuehrung.
        container
            .PaddingBottom(2)
            .Column(col =>
            {
                col.Item().Row(row =>
                {
                    row.ConstantItem(14).Height(14).Background(brand);
                    row.ConstantItem(10);
                    row.RelativeItem().AlignMiddle()
                        .Text(title.ToUpperInvariant())
                        .FontSize(11).Bold().FontColor("#0F172A");
                });
                col.Item().PaddingTop(3).LineHorizontal(0.8f).LineColor(brand);
            });
    }

    internal static void ComposeHeaderTable(IContainer container, IReadOnlyList<(string Label, string? Value)> items, string brand = "#7A8A94")
    {
        if (items.Count == 0)
            return;

        // 3-Spalten-Daten-Grid: kompakt, leserlich, mit feinen Vertikal-Trennlinien.
        // Labels MICRO+UPPERCASE in Brand, Werte gross+SemiBold dunkel -> hohe Lesbarkeit auf wenig Raum.
        const int columns = 3;
        var perColumn = (int)Math.Ceiling(items.Count / (double)columns);

        container.Border(0.5f).BorderColor("#E5E7EB").Background("#FFFFFF").Padding(6).Row(row =>
        {
            for (var c = 0; c < columns; c++)
            {
                var slice = items.Skip(c * perColumn).Take(perColumn).ToList();
                if (slice.Count == 0)
                {
                    row.RelativeItem();
                    continue;
                }

                if (c > 0)
                    row.ConstantItem(8).BorderLeft(0.5f).BorderColor("#E5E7EB");

                row.RelativeItem().PaddingHorizontal(c == 0 ? 0 : 6).Column(colCell =>
                {
                    for (var i = 0; i < slice.Count; i++)
                    {
                        var item = slice[i];
                        colCell.Item().PaddingBottom(i == slice.Count - 1 ? 0 : 2.5f).Column(rowBlock =>
                        {
                            rowBlock.Item().Text(item.Label.ToUpperInvariant())
                                .FontSize(6.5f).SemiBold().FontColor(brand);
                            rowBlock.Item().Text(NormalizeValue(item.Value))
                                .FontSize(9.5f).SemiBold().FontColor("#0F172A");
                        });
                    }
                });
            }
        });
    }

    private static void ComposeHeaderCard(IContainer container, IReadOnlyList<(string Label, string? Value)> items, string brand)
    {
        if (items.Count == 0)
            return;

        // Magazin-Card: schmale Brand-Linie links + weisser Hintergrund mit feiner Border + Trennlinien zwischen Zeilen.
        // Keine flaechige Brand-Toenung -> dezent edel statt bunt.
        container
            .BorderLeft(2.5f).BorderColor(brand)
            .Border(0.5f).BorderColor("#E5E7EB")
            .Background("#FFFFFF")
            .Padding(8)
            .Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(86);
                    columns.RelativeColumn();
                });

                for (int i = 0; i < items.Count; i++)
                {
                    var item = items[i];
                    var isLast = i == items.Count - 1;
                    var labelCell = table.Cell().PaddingVertical(1.6f).PaddingRight(6);
                    var valueCell = table.Cell().PaddingVertical(1.6f);
                    if (!isLast)
                    {
                        labelCell = labelCell.BorderBottom(0.3f).BorderColor("#F1F2F4");
                        valueCell = valueCell.BorderBottom(0.3f).BorderColor("#F1F2F4");
                    }
                    labelCell.Text(item.Label.ToUpperInvariant()).FontSize(7.5f).FontColor("#94A3B8");
                    valueCell.Text(NormalizeValue(item.Value)).FontSize(9.5f).SemiBold().FontColor("#0F172A");
                }
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
    public float PhotoWidth { get; init; } = 250f;
    public float PhotoHeight { get; init; } = 285f;
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

