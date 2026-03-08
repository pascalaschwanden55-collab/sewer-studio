using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.Application.Reports;

/// <summary>
/// Erzeugt ein kombiniertes PDF-Dossier fuer eine Haltung inkl. Schaechte und Hydraulik.
/// </summary>
public static class HaltungsDossierPdfBuilder
{
    public static byte[] Build(
        Project project,
        HaltungRecord record,
        SchachtRecord? schachtVon,
        SchachtRecord? schachtBis,
        HydraulikCalcResult? hydraulik,
        string projectRootAbs,
        DossierPrintOptions options)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        var holdingLabel = record.GetFieldValue("Haltungsname") ?? "–";
        var nutzungsart = record.GetFieldValue("Nutzungsart")?.Trim() ?? "";
        var brand = ProtocolPdfExporter.ResolveNutzungsartBrand(nutzungsart);
        var logoBytes = ResolveLogoBytes(options.LogoPathAbs, projectRootAbs);

        var (startNode, endNode) = ProtocolPdfExporter.SplitHoldingNodes(holdingLabel);

        var haltungsprotokollOpts = new HaltungsprotokollPdfOptions
        {
            LogoPathAbs = options.LogoPathAbs,
            FooterLine = options.FooterLine,
            IncludePhotos = false, // fotos separately
            IncludeHaltungsgrafik = true,
        };

        var pdfBytes = Document.Create(container =>
        {
            // ── Deckblatt ──
            if (options.IncludeDeckblatt)
            {
                container.Page(page =>
                {
                    page.Margin(25);
                    page.Size(PageSizes.A4);
                    page.DefaultTextStyle(x => x.FontSize(10));

                    page.Header().Element(c =>
                        ProtocolPdfExporter.ComposeTopHeader(c, logoBytes, haltungsprotokollOpts));

                    page.Content().Column(col =>
                    {
                        col.Item().PaddingTop(4).Element(c =>
                            ProtocolPdfExporter.ComposeTitleBar(c, "Haltungsdossier", holdingLabel, brand));

                        col.Item().PaddingTop(12).Element(c =>
                            ComposeDeckblatt(c, project, record, startNode, endNode, schachtVon, schachtBis, hydraulik, brand));
                    });

                    page.Footer().Element(c => ComposeFooter(c, options.FooterLine));
                });
            }

            // ── Haltungsprotokoll ──
            if (options.IncludeHaltungsprotokoll)
            {
                var doc = record.Protocol ?? new ProtocolDocument();
                var exporter = new ProtocolPdfExporter();
                var protocolBytes = exporter.BuildHaltungsprotokollPdf(project, record, doc, projectRootAbs, haltungsprotokollOpts);

                // Embed as pre-built pages
                container.Page(page =>
                {
                    page.Margin(25);
                    page.Size(PageSizes.A4);
                    page.DefaultTextStyle(x => x.FontSize(10));

                    page.Header().Element(c =>
                        ProtocolPdfExporter.ComposeTopHeader(c, logoBytes, haltungsprotokollOpts));

                    page.Content().Column(col =>
                    {
                        col.Item().PaddingTop(4).Element(c =>
                            ProtocolPdfExporter.ComposeTitleBar(c, $"Haltungsprotokoll – {holdingLabel}", "SN EN 13508-2", brand));

                        col.Item().PaddingTop(6).Element(c =>
                            ComposeHaltungsprotokollContent(c, project, record, doc, projectRootAbs, brand));
                    });

                    page.Footer().Element(c => ComposeFooter(c, options.FooterLine));
                });
            }

            // ── Fotos ──
            if (options.IncludeFotos)
            {
                var doc = record.Protocol ?? new ProtocolDocument();
                var photoEntries = ResolvePhotoEntries(record, doc, projectRootAbs);
                if (photoEntries.Count > 0)
                {
                    container.Page(page =>
                    {
                        page.Margin(25);
                        page.Size(PageSizes.A4);
                        page.DefaultTextStyle(x => x.FontSize(10));

                        page.Header().Element(c =>
                            ProtocolPdfExporter.ComposeTopHeader(c, logoBytes, haltungsprotokollOpts));

                        page.Content().Column(col =>
                        {
                            col.Item().PaddingTop(4).Element(c =>
                                ProtocolPdfExporter.ComposeTitleBar(c, $"Fotos – {holdingLabel}", null, brand));

                            col.Item().PaddingTop(6).Element(c =>
                                ComposePhotos(c, photoEntries));
                        });

                        page.Footer().Element(c => ComposeFooter(c, options.FooterLine));
                    });
                }
            }

            // ── Schacht Von ──
            if (options.IncludeSchachtVon && schachtVon != null)
            {
                container.Page(page =>
                {
                    page.Margin(25);
                    page.Size(PageSizes.A4);
                    page.DefaultTextStyle(x => x.FontSize(10));

                    page.Header().Element(c =>
                        ProtocolPdfExporter.ComposeTopHeader(c, logoBytes, haltungsprotokollOpts));

                    page.Content().Column(col =>
                    {
                        col.Item().PaddingTop(4).Element(c =>
                            ProtocolPdfExporter.ComposeTitleBar(c, $"Schacht Von – {startNode ?? "?"}", null, brand));

                        col.Item().PaddingTop(6).Element(c =>
                            ComposeSchachtSection(c, schachtVon, brand));
                    });

                    page.Footer().Element(c => ComposeFooter(c, options.FooterLine));
                });
            }

            // ── Schacht Bis ──
            if (options.IncludeSchachtBis && schachtBis != null)
            {
                container.Page(page =>
                {
                    page.Margin(25);
                    page.Size(PageSizes.A4);
                    page.DefaultTextStyle(x => x.FontSize(10));

                    page.Header().Element(c =>
                        ProtocolPdfExporter.ComposeTopHeader(c, logoBytes, haltungsprotokollOpts));

                    page.Content().Column(col =>
                    {
                        col.Item().PaddingTop(4).Element(c =>
                            ProtocolPdfExporter.ComposeTitleBar(c, $"Schacht Bis – {endNode ?? "?"}", null, brand));

                        col.Item().PaddingTop(6).Element(c =>
                            ComposeSchachtSection(c, schachtBis, brand));
                    });

                    page.Footer().Element(c => ComposeFooter(c, options.FooterLine));
                });
            }

            // ── Hydraulik ──
            if (options.IncludeHydraulik && hydraulik != null)
            {
                container.Page(page =>
                {
                    page.Margin(25);
                    page.Size(PageSizes.A4);
                    page.DefaultTextStyle(x => x.FontSize(10));

                    page.Header().Element(c =>
                        ProtocolPdfExporter.ComposeTopHeader(c, logoBytes, haltungsprotokollOpts));

                    page.Content().Column(col =>
                    {
                        col.Item().PaddingTop(4).Element(c =>
                            ProtocolPdfExporter.ComposeTitleBar(c, $"Hydraulik – {holdingLabel}", "DWA-A 110 / Kreisquerschnitt", brand));

                        col.Item().PaddingTop(6).Element(c =>
                            ComposeHydraulikSection(c, record, hydraulik, brand));
                    });

                    page.Footer().Element(c => ComposeFooter(c, options.FooterLine));
                });
            }

            // ── Kostenschaetzung ──
            if (options.IncludeKostenschaetzung)
            {
                var hasDetailedCosts = options.HoldingCost?.Measures is { Count: > 0 };
                var kostenField = record.GetFieldValue("Kosten");
                var massnahme = record.GetFieldValue("Empfohlene_Sanierungsmassnahmen");
                var sanieren = record.GetFieldValue("Sanieren_JaNein");

                if (hasDetailedCosts || !string.IsNullOrWhiteSpace(kostenField) || !string.IsNullOrWhiteSpace(massnahme))
                {
                    container.Page(page =>
                    {
                        page.Margin(25);
                        page.Size(PageSizes.A4);
                        page.DefaultTextStyle(x => x.FontSize(10));

                        page.Header().Element(c =>
                            ProtocolPdfExporter.ComposeTopHeader(c, logoBytes, haltungsprotokollOpts));

                        page.Content().Column(col =>
                        {
                            col.Item().PaddingTop(4).Element(c =>
                                ProtocolPdfExporter.ComposeTitleBar(c, $"Kostenschätzung – {holdingLabel}", null, brand));

                            col.Item().PaddingTop(6).Element(c =>
                                ComposeKostenschaetzung(c, record, options.HoldingCost, brand));
                        });

                        page.Footer().Element(c => ComposeFooter(c, options.FooterLine));
                    });
                }
            }

        }).GeneratePdf();

        return pdfBytes;
    }

    // ── Deckblatt ──────────────────────────────────────────────

    private static void ComposeDeckblatt(
        IContainer container,
        Project project,
        HaltungRecord record,
        string? startNode,
        string? endNode,
        SchachtRecord? schachtVon,
        SchachtRecord? schachtBis,
        HydraulikCalcResult? hydraulik,
        string brand)
    {
        var items = new List<(string Label, string? Value)>
        {
            ("Haltung", record.GetFieldValue("Haltungsname")),
            ("Schacht Von", startNode ?? "–"),
            ("Schacht Bis", endNode ?? "–"),
            ("Strasse", record.GetFieldValue("Strasse")),
            ("DN [mm]", record.GetFieldValue("DN_mm")),
            ("Material", record.GetFieldValue("Rohrmaterial")),
            ("Nutzungsart", record.GetFieldValue("Nutzungsart")),
            ("Haltungslänge [m]", record.GetFieldValue("Haltungslaenge_m")),
            ("Zustandsklasse", record.GetFieldValue("Zustandsklasse")),
            ("VSA-Note D", record.GetFieldValue("VSA_Zustandsnote_D")),
            ("VSA-Note S", record.GetFieldValue("VSA_Zustandsnote_S")),
            ("VSA-Note B", record.GetFieldValue("VSA_Zustandsnote_B")),
            ("Inspektionsrichtung", record.GetFieldValue("Inspektionsrichtung")),
            ("Datum", record.GetFieldValue("Datum_Jahr")),
            ("Projekt", project.Name),
            ("Gemeinde", GetMeta(project, "Gemeinde")),
            ("Auftraggeber", GetMeta(project, "Auftraggeber")),
            ("Bearbeiter", GetMeta(project, "Bearbeiter")),
        };

        container.Column(col =>
        {
            col.Item().PaddingTop(4).Element(c =>
                ProtocolPdfExporter.ComposeSectionHeading(c, "Haltungsdaten", brand));
            col.Item().PaddingTop(2).Element(c =>
                ProtocolPdfExporter.ComposeHeaderTable(c, FilterNonEmpty(items), brand));

            // Status-Uebersicht
            col.Item().PaddingTop(12).Element(c =>
                ProtocolPdfExporter.ComposeSectionHeading(c, "Dossier-Inhalt", brand));

            col.Item().PaddingTop(4).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(20);
                    columns.RelativeColumn();
                });

                AddStatusRow(table, true, "Haltungsprotokoll");
                AddStatusRow(table, schachtVon != null, $"Schacht Von ({startNode ?? "–"})");
                AddStatusRow(table, schachtBis != null, $"Schacht Bis ({endNode ?? "–"})");
                AddStatusRow(table, hydraulik != null, "Hydraulik-Berechnung");
            });
        });
    }

    private static void AddStatusRow(TableDescriptor table, bool available, string label)
    {
        var symbol = available ? "●" : "○";
        var color = available ? "#16A34A" : "#9CA3AF";
        table.Cell().PaddingVertical(1).Text(symbol).FontSize(11).FontColor(color);
        table.Cell().PaddingVertical(1).AlignMiddle().Text(label).FontSize(10);
    }

    // ── Haltungsprotokoll-Inhalt inline ───────────────────────

    private static void ComposeHaltungsprotokollContent(
        IContainer container,
        Project project,
        HaltungRecord record,
        ProtocolDocument doc,
        string projectRootAbs,
        string brand)
    {
        var entries = (doc.Current?.Entries ?? new List<ProtocolEntry>())
            .Where(e => !e.IsDeleted)
            .OrderBy(e => e.MeterStart ?? e.MeterEnd ?? double.MaxValue)
            .ToList();

        var headerItems = new List<(string Label, string? Value)>
        {
            ("Haltung", record.GetFieldValue("Haltungsname")),
            ("DN [mm]", record.GetFieldValue("DN_mm")),
            ("Material", record.GetFieldValue("Rohrmaterial")),
            ("Datum", record.GetFieldValue("Datum_Jahr")),
            ("Nutzungsart", record.GetFieldValue("Nutzungsart")),
            ("Zustandsklasse", record.GetFieldValue("Zustandsklasse")),
        };

        container.Column(col =>
        {
            col.Item().PaddingTop(2).Element(c =>
                ProtocolPdfExporter.ComposeHeaderTable(c, FilterNonEmpty(headerItems), brand));

            if (entries.Count == 0)
            {
                col.Item().PaddingTop(6).Text("Keine Beobachtungen vorhanden.").FontSize(10).FontColor("#6B7280");
                return;
            }

            col.Item().PaddingTop(6).Element(c =>
                ProtocolPdfExporter.ComposeSectionHeading(c, $"Beobachtungen ({entries.Count})", brand));

            col.Item().PaddingTop(2).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(55); // Meter
                    columns.ConstantColumn(70); // Code
                    columns.RelativeColumn();   // Beschreibung
                });

                // Header
                table.Cell().Background("#F3F4F6").Padding(3).Text("Meter").FontSize(8).Bold();
                table.Cell().Background("#F3F4F6").Padding(3).Text("Code").FontSize(8).Bold();
                table.Cell().Background("#F3F4F6").Padding(3).Text("Beschreibung").FontSize(8).Bold();

                foreach (var entry in entries)
                {
                    var meterText = FormatMeterRange(entry.MeterStart, entry.MeterEnd);
                    table.Cell().BorderBottom(0.3f).BorderColor("#E5E7EB").Padding(2).Text(meterText).FontSize(8);
                    table.Cell().BorderBottom(0.3f).BorderColor("#E5E7EB").Padding(2).Text(entry.Code ?? "–").FontSize(8).Bold();
                    table.Cell().BorderBottom(0.3f).BorderColor("#E5E7EB").Padding(2).Text(entry.Beschreibung ?? "").FontSize(8);
                }
            });
        });
    }

    // ── Fotos ────────────────────────────────────────────────

    private static List<(string Label, string AbsPath)> ResolvePhotoEntries(
        HaltungRecord record,
        ProtocolDocument doc,
        string projectRootAbs)
    {
        var result = new List<(string, string)>();
        var entries = (doc.Current?.Entries ?? new List<ProtocolEntry>())
            .Where(e => !e.IsDeleted)
            .ToList();

        foreach (var entry in entries)
        {
            if (entry.FotoPaths is null || entry.FotoPaths.Count == 0)
                continue;

            foreach (var raw in entry.FotoPaths)
            {
                var resolved = ResolveMediaPath(raw, projectRootAbs);
                if (resolved != null && File.Exists(resolved))
                {
                    var label = $"{entry.Code ?? "–"} @ {FormatMeterRange(entry.MeterStart, entry.MeterEnd)}";
                    result.Add((label, resolved));
                }
            }
        }

        return result;
    }

    private static void ComposePhotos(IContainer container, List<(string Label, string AbsPath)> photos)
    {
        container.Column(col =>
        {
            foreach (var (label, path) in photos)
            {
                col.Item().PaddingTop(6).Column(photoCol =>
                {
                    photoCol.Item().Text(label).FontSize(9).Bold().FontColor("#374151");

                    try
                    {
                        var bytes = File.ReadAllBytes(path);
                        photoCol.Item().PaddingTop(2)
                            .Border(0.5f).BorderColor("#D1D5DB")
                            .MaxHeight(280)
                            .Image(bytes).FitArea();
                    }
                    catch
                    {
                        photoCol.Item().PaddingTop(2).Text($"Foto nicht lesbar: {Path.GetFileName(path)}")
                            .FontSize(8).FontColor("#DC2626");
                    }
                });
            }
        });
    }

    // ── Schacht-Sektion ──────────────────────────────────────

    private static void ComposeSchachtSection(IContainer container, SchachtRecord schacht, string brand)
    {
        var items = new List<(string Label, string? Value)>();

        // Alle Felder als Key-Value ausgeben
        foreach (var kv in schacht.Fields.OrderBy(f => f.Key, StringComparer.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(kv.Value))
                continue;
            items.Add((kv.Key, kv.Value));
        }

        container.Column(col =>
        {
            if (items.Count == 0)
            {
                col.Item().Text("Keine Schachtdaten vorhanden.").FontSize(10).FontColor("#6B7280");
                return;
            }

            col.Item().PaddingTop(2).Element(c =>
                ProtocolPdfExporter.ComposeSectionHeading(c, "Schachtdaten", brand));
            col.Item().PaddingTop(2).Element(c =>
                ProtocolPdfExporter.ComposeKeyValueTable(c, items));

            // Schacht-Beobachtungen
            if (schacht.Protocol?.Current?.Entries is { Count: > 0 } entries)
            {
                var active = entries.Where(e => !e.IsDeleted).ToList();
                if (active.Count > 0)
                {
                    col.Item().PaddingTop(6).Element(c =>
                        ProtocolPdfExporter.ComposeSectionHeading(c, $"Schacht-Beobachtungen ({active.Count})", brand));

                    col.Item().PaddingTop(2).Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.ConstantColumn(70); // Code
                            columns.RelativeColumn();   // Beschreibung
                        });

                        table.Cell().Background("#F3F4F6").Padding(3).Text("Code").FontSize(8).Bold();
                        table.Cell().Background("#F3F4F6").Padding(3).Text("Beschreibung").FontSize(8).Bold();

                        foreach (var entry in active)
                        {
                            table.Cell().BorderBottom(0.3f).BorderColor("#E5E7EB").Padding(2).Text(entry.Code ?? "–").FontSize(8).Bold();
                            table.Cell().BorderBottom(0.3f).BorderColor("#E5E7EB").Padding(2).Text(entry.Beschreibung ?? "").FontSize(8);
                        }
                    });
                }
            }
        });
    }

    // ── Hydraulik-Sektion ────────────────────────────────────

    private static void ComposeHydraulikSection(
        IContainer container, HaltungRecord record, HydraulikCalcResult calc, string brand)
    {
        var headerItems = new List<(string Label, string? Value)>
        {
            ("DN [mm]", Fmt(calc.DN_mm, 0)),
            ("Material", calc.Material),
            ("Gefaelle [‰]", Fmt(calc.Gefaelle_Promille, 1)),
            ("Wasserstand [mm]", Fmt(calc.Wasserstand_mm, 0)),
            ("Temperatur [°C]", Fmt(calc.Temperatur_C, 0)),
        };

        var teilfuellung = new List<(string Label, string? Value)>
        {
            ("v_T [m/s]", Fmt(calc.V_T, 3)),
            ("Q_T [l/s]", Fmt(calc.Q_T * 1000, 2)),
            ("A_T [cm²]", Fmt(calc.A_T * 1e4, 2)),
        };

        var bewertung = new List<(string Label, bool Ok)>
        {
            ("Geschwindigkeit v_T >= 0.5 m/s", calc.VelocityOk),
            ("Schubspannung Tau >= 2.5 N/m²", calc.ShearOk),
            ("Froude Fr <= 1", calc.FroudeOk),
            ("Ablagerungsfrei v_T >= v_c", calc.AblagerungOk),
        };

        container.Column(col =>
        {
            col.Item().PaddingTop(2).Element(c =>
                ProtocolPdfExporter.ComposeHeaderTable(c, headerItems, brand));

            col.Item().PaddingTop(6).Element(c =>
                ProtocolPdfExporter.ComposeSectionHeading(c, "Teilfuellung", brand));
            col.Item().PaddingTop(2).Element(c =>
                ProtocolPdfExporter.ComposeKeyValueTable(c, teilfuellung));

            col.Item().PaddingTop(6).Element(c =>
                ProtocolPdfExporter.ComposeSectionHeading(c, "Bewertung", brand));
            col.Item().PaddingTop(4).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(20);
                    columns.RelativeColumn();
                });

                foreach (var (label, ok) in bewertung)
                {
                    var symbol = "●";
                    var color = ok ? "#16A34A" : "#DC2626";
                    table.Cell().PaddingVertical(1).Text(symbol).FontSize(12).FontColor(color);
                    table.Cell().PaddingVertical(1).AlignMiddle().Text(label).FontSize(9);
                }
            });
        });
    }

    // ── Footer ──────────────────────────────────────────────

    private static void ComposeFooter(IContainer container, string footerLine)
    {
        container.Column(footer =>
        {
            footer.Item().LineHorizontal(0.5f).LineColor("#D1D5DB");
            footer.Item().PaddingTop(3).Row(row =>
            {
                row.RelativeItem().Text(
                    string.IsNullOrWhiteSpace(footerLine)
                        ? $"Erstellt: {DateTime.Now:dd.MM.yyyy}"
                        : footerLine)
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
    }

    // ── Helpers ─────────────────────────────────────────────

    private static string? ResolveMediaPath(string? raw, string projectRootAbs)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var path = raw.Replace('/', Path.DirectorySeparatorChar);
        if (Path.IsPathRooted(path) && File.Exists(path))
            return path;

        if (!string.IsNullOrWhiteSpace(projectRootAbs))
        {
            var combined = Path.Combine(projectRootAbs, path);
            if (File.Exists(combined))
                return combined;
        }

        return null;
    }

    private static byte[]? ResolveLogoBytes(string? logoPath, string projectRootAbs)
    {
        var candidates = new List<string?> { logoPath };
        if (!string.IsNullOrWhiteSpace(projectRootAbs))
        {
            candidates.Add(Path.Combine(projectRootAbs, "Assets", "Brand", "abwasser-uri-logo.png"));
            candidates.Add(Path.Combine(projectRootAbs, "logo.png"));
        }

        var appLogo = Path.Combine(AppContext.BaseDirectory, "Assets", "Brand", "abwasser-uri-logo.png");
        candidates.Add(appLogo);

        foreach (var p in candidates)
        {
            if (!string.IsNullOrWhiteSpace(p) && File.Exists(p))
            {
                try { return File.ReadAllBytes(p); }
                catch { /* next */ }
            }
        }

        return null;
    }

    private static string FormatMeterRange(double? start, double? end)
    {
        if (start is null && end is null) return "–";
        if (start is not null && end is not null && end > start)
            return $"{start.Value:0.00}–{end.Value:0.00}";
        return (start ?? end)!.Value.ToString("0.00", CultureInfo.InvariantCulture);
    }

    private static string Fmt(double v, int dec) =>
        v.ToString($"F{dec}", CultureInfo.InvariantCulture);

    private static string? GetMeta(Project project, string key) =>
        project.Metadata.TryGetValue(key, out var v) ? v : null;

    private static List<(string Label, string? Value)> FilterNonEmpty(List<(string Label, string? Value)> items) =>
        items.Where(i => !string.IsNullOrWhiteSpace(i.Value)).ToList();

    // ── Kostenschaetzung ────────────────────────────────────

    private static void ComposeKostenschaetzung(
        IContainer container, HaltungRecord record, HoldingCost? holdingCost, string brand)
    {
        container.Column(col =>
        {
            // Grunddaten aus Record
            var summaryItems = new List<(string Label, string? Value)>
            {
                ("Sanieren", record.GetFieldValue("Sanieren_JaNein")),
                ("Empfohlene Massnahme", record.GetFieldValue("Empfohlene_Sanierungsmassnahmen")),
                ("Kosten", record.GetFieldValue("Kosten")),
                ("Zustandsklasse", record.GetFieldValue("Zustandsklasse")),
            };

            col.Item().PaddingTop(2).Element(c =>
                ProtocolPdfExporter.ComposeSectionHeading(c, "Zusammenfassung", brand));
            col.Item().PaddingTop(2).Element(c =>
                ProtocolPdfExporter.ComposeKeyValueTable(c, FilterNonEmpty(summaryItems)));

            // Detaillierte Kostenaufstellung
            if (holdingCost?.Measures is { Count: > 0 } measures)
            {
                foreach (var measure in measures)
                {
                    col.Item().PaddingTop(8).Element(c =>
                        ProtocolPdfExporter.ComposeSectionHeading(c, measure.MeasureName ?? measure.MeasureId, brand));

                    if (measure.Lines is { Count: > 0 })
                    {
                        col.Item().PaddingTop(2).Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(3); // Text
                                columns.ConstantColumn(45); // Einheit
                                columns.ConstantColumn(50); // Menge
                                columns.ConstantColumn(65); // EP
                                columns.ConstantColumn(70); // Total
                            });

                            // Header
                            table.Cell().Background("#F3F4F6").Padding(3).Text("Position").FontSize(8).Bold();
                            table.Cell().Background("#F3F4F6").Padding(3).Text("Einh.").FontSize(8).Bold();
                            table.Cell().Background("#F3F4F6").Padding(3).AlignRight().Text("Menge").FontSize(8).Bold();
                            table.Cell().Background("#F3F4F6").Padding(3).AlignRight().Text("EP").FontSize(8).Bold();
                            table.Cell().Background("#F3F4F6").Padding(3).AlignRight().Text("Total").FontSize(8).Bold();

                            foreach (var line in measure.Lines.Where(l => l.Selected))
                            {
                                var lineTotal = line.Qty * line.UnitPrice;
                                table.Cell().BorderBottom(0.3f).BorderColor("#E5E7EB").Padding(2).Text(line.Text).FontSize(8);
                                table.Cell().BorderBottom(0.3f).BorderColor("#E5E7EB").Padding(2).Text(line.Unit).FontSize(8);
                                table.Cell().BorderBottom(0.3f).BorderColor("#E5E7EB").Padding(2).AlignRight().Text(FmtDec(line.Qty)).FontSize(8);
                                table.Cell().BorderBottom(0.3f).BorderColor("#E5E7EB").Padding(2).AlignRight().Text(FmtDec(line.UnitPrice)).FontSize(8);
                                table.Cell().BorderBottom(0.3f).BorderColor("#E5E7EB").Padding(2).AlignRight().Text(FmtDec(lineTotal)).FontSize(8).Bold();
                            }
                        });

                        col.Item().PaddingTop(2).AlignRight()
                            .Text($"Zwischentotal: {FmtDec(measure.Total)} CHF").FontSize(9).Bold();
                    }
                }

                // Gesamttotal
                col.Item().PaddingTop(10).Border(0.5f).BorderColor("#D1D5DB").Background("#F3F4F6").Padding(8).Column(totalCol =>
                {
                    if (holdingCost.MwstRate > 0)
                    {
                        totalCol.Item().Row(row =>
                        {
                            row.RelativeItem().Text("Netto").FontSize(10);
                            row.AutoItem().Text($"{FmtDec(holdingCost.Total)} CHF").FontSize(10).Bold();
                        });
                        totalCol.Item().Row(row =>
                        {
                            row.RelativeItem().Text($"MwSt ({holdingCost.MwstRate:0.0}%)").FontSize(10);
                            row.AutoItem().Text($"{FmtDec(holdingCost.MwstAmount)} CHF").FontSize(10);
                        });
                    }
                    totalCol.Item().Row(row =>
                    {
                        row.RelativeItem().Text("Gesamttotal").FontSize(11).Bold();
                        row.AutoItem().Text($"{FmtDec(holdingCost.TotalInclMwst > 0 ? holdingCost.TotalInclMwst : holdingCost.Total)} CHF").FontSize(11).Bold();
                    });
                });
            }
        });
    }

    private static string FmtDec(decimal v) =>
        v.ToString("N2", CultureInfo.InvariantCulture);

}
