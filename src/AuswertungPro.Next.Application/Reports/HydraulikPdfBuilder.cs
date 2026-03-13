using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.Reports;

/// <summary>
/// Erzeugt ein Hydraulik-Analysebericht-PDF fuer eine einzelne Haltung.
/// Nutzt die Compose-Helfer aus <see cref="ProtocolPdfExporter"/>.
/// </summary>
public static class HydraulikPdfBuilder
{
    public static byte[] Build(
        HaltungRecord record,
        HydraulikCalcResult calc,
        HydraulikPrintOptions? options = null)
    {
        options ??= new HydraulikPrintOptions();
        QuestPDF.Settings.License = LicenseType.Community;

        var holdingLabel = record.GetFieldValue("Haltungsname") ?? "–";
        var nutzungsart = record.GetFieldValue("Nutzungsart")?.Trim() ?? "";
        var brand = ProtocolPdfExporter.ResolveNutzungsartBrand(nutzungsart);

        var title = $"Hydraulik-Analyse – {holdingLabel}";
        var subtitle = "DWA-A 110 / Kreisquerschnitt";

        var headerItems = BuildHeaderItems(record, calc);
        var logoBytes = ResolveLogoBytes(options.LogoPathAbs);

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(25);
                page.Size(PageSizes.A4);
                page.DefaultTextStyle(x => x.FontSize(10));

                page.Header().Element(c =>
                {
                    var dummyOpts = new HaltungsprotokollPdfOptions
                    {
                        LogoPathAbs = options.LogoPathAbs
                    };
                    ProtocolPdfExporter.ComposeTopHeader(c, logoBytes, dummyOpts);
                });

                page.Content().Column(col =>
                {
                    col.Item().PaddingTop(4).Element(c =>
                        ProtocolPdfExporter.ComposeTitleBar(c, title, subtitle, brand));

                    col.Item().PaddingTop(4).Element(c =>
                        ProtocolPdfExporter.ComposeHeaderTable(c, headerItems, brand));

                    if (options.IncludeTeilfuellung)
                    {
                        col.Item().PaddingTop(6).Element(c =>
                            ProtocolPdfExporter.ComposeSectionHeading(c, "Teilfuellung", brand));
                        col.Item().PaddingTop(2).Element(c =>
                            ProtocolPdfExporter.ComposeKeyValueTable(c, BuildTeilfuellungItems(calc)));
                    }

                    if (options.IncludeVollfuellung)
                    {
                        col.Item().PaddingTop(6).Element(c =>
                            ProtocolPdfExporter.ComposeSectionHeading(c, "Vollfuellung", brand));
                        col.Item().PaddingTop(2).Element(c =>
                            ProtocolPdfExporter.ComposeKeyValueTable(c, BuildVollfuellungItems(calc)));
                    }

                    if (options.IncludeKennzahlen)
                    {
                        col.Item().PaddingTop(6).Element(c =>
                            ProtocolPdfExporter.ComposeSectionHeading(c, "Kennzahlen", brand));
                        col.Item().PaddingTop(2).Element(c =>
                            ProtocolPdfExporter.ComposeKeyValueTable(c, BuildKennzahlenItems(calc)));
                    }

                    if (options.IncludeAblagerung)
                    {
                        col.Item().PaddingTop(6).Element(c =>
                            ProtocolPdfExporter.ComposeSectionHeading(c, "Ablagerungsgefahr", brand));
                        col.Item().PaddingTop(2).Element(c =>
                            ProtocolPdfExporter.ComposeKeyValueTable(c, BuildAblagerungItems(calc)));
                        col.Item().PaddingTop(2).Text(calc.AblagerungOk
                            ? $"Ablagerungsfrei — v_T ({Fmt(calc.V_T, 3)} m/s) >= v_c ({Fmt(calc.Vc, 3)} m/s)"
                            : $"Ablagerungsgefahr — v_T ({Fmt(calc.V_T, 3)} m/s) < v_c ({Fmt(calc.Vc, 3)} m/s)")
                            .FontSize(9).FontColor(calc.AblagerungOk ? "#16A34A" : "#DC2626");
                    }

                    if (options.IncludeAuslastung)
                    {
                        col.Item().PaddingTop(6).Element(c =>
                            ProtocolPdfExporter.ComposeSectionHeading(c, "Auslastung", brand));
                        col.Item().PaddingTop(4).Element(c => ComposeAuslastungBar(c, calc.Auslastung, brand));
                    }

                    if (options.IncludeBewertung)
                    {
                        col.Item().PaddingTop(6).Element(c =>
                            ProtocolPdfExporter.ComposeSectionHeading(c, "Bewertung", brand));
                        col.Item().PaddingTop(4).Element(c => ComposeBewertungTable(c, calc));
                    }
                });

                page.Footer().Column(footer =>
                {
                    footer.Item().LineHorizontal(0.5f).LineColor("#D1D5DB");
                    footer.Item().PaddingTop(3).Row(row =>
                    {
                        row.RelativeItem().Text(
                            string.IsNullOrWhiteSpace(options.FooterLine)
                                ? $"Erstellt: {DateTime.Now:dd.MM.yyyy}"
                                : options.FooterLine)
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

    // ── Header items ────────────────────────────────────────

    private static IReadOnlyList<(string Label, string? Value)> BuildHeaderItems(
        HaltungRecord record, HydraulikCalcResult calc)
    {
        return new List<(string, string?)>
        {
            ("Haltung", record.GetFieldValue("Haltungsname")),
            ("DN [mm]", Fmt(calc.DN_mm, 0)),
            ("Material", calc.Material),
            ("Gefaelle [‰]", Fmt(calc.Gefaelle_Promille, 1)),
            ("Wasserstand [mm]", Fmt(calc.Wasserstand_mm, 0)),
            ("Abwassertyp", calc.AbwasserTyp == "MR" ? "Misch-/Regenwasser" : "Schmutzwasser"),
            ("Temperatur [°C]", Fmt(calc.Temperatur_C, 0)),
            ("kb [mm]", Fmt(calc.Kb * 1000, 2)),
        };
    }

    private static IReadOnlyList<(string Label, string? Value)> BuildTeilfuellungItems(HydraulikCalcResult c)
    {
        return new List<(string, string?)>
        {
            ("v_T [m/s]", Fmt(c.V_T, 3)),
            ("Q_T [l/s]", Fmt(c.Q_T * 1000, 2)),
            ("A_T [cm²]", Fmt(c.A_T * 1e4, 2)),
            ("Lu_T [mm]", Fmt(c.Lu_T * 1000, 1)),
            ("Rhy_T [mm]", Fmt(c.Rhy_T * 1000, 2)),
            ("Bsp [mm]", Fmt(c.Bsp * 1000, 1)),
        };
    }

    private static IReadOnlyList<(string Label, string? Value)> BuildVollfuellungItems(HydraulikCalcResult c)
    {
        return new List<(string, string?)>
        {
            ("v_V [m/s]", Fmt(c.V_V, 3)),
            ("Q_V [l/s]", Fmt(c.Q_V * 1000, 2)),
        };
    }

    private static IReadOnlyList<(string Label, string? Value)> BuildKennzahlenItems(HydraulikCalcResult c)
    {
        return new List<(string, string?)>
        {
            ("Reynolds Re", Fmt(c.Re, 0)),
            ("Froude Fr", Fmt(c.Fr, 3)),
            ("Lambda", FmtSci(c.Lambda)),
            ("Ny [10⁻⁶ m²/s]", Fmt(c.Ny * 1e6, 3)),
            ("Tau [N/m²]", Fmt(c.Tau, 2)),
        };
    }

    private static IReadOnlyList<(string Label, string? Value)> BuildAblagerungItems(HydraulikCalcResult c)
    {
        return new List<(string, string?)>
        {
            ("v_c [m/s]", Fmt(c.Vc, 3)),
            ("I_c [‰]", Fmt(c.Ic * 1000, 2)),
            ("Tau_c [N/m²]", Fmt(c.TauC, 2)),
        };
    }

    // ── Custom compose helpers ──────────────────────────────

    private static void ComposeAuslastungBar(IContainer container, double auslastung, string brand)
    {
        var pct = Math.Clamp(auslastung * 100, 0, 100);
        var color = pct switch
        {
            <= 70 => "#16A34A",
            <= 90 => "#F59E0B",
            _ => "#DC2626"
        };

        container.Column(col =>
        {
            col.Item().Text($"{pct:F0}% Auslastung (h/D)").FontSize(10).SemiBold();
            col.Item().PaddingTop(2).Height(16).Row(row =>
            {
                row.ConstantItem((float)(pct / 100.0 * 500)).Background(color).Border(0.5f).BorderColor("#D1D5DB");
                row.RelativeItem().Background("#F3F4F6").Border(0.5f).BorderColor("#D1D5DB");
            });
        });
    }

    private static void ComposeBewertungTable(IContainer container, HydraulikCalcResult c)
    {
        var checks = new List<(string Label, bool Ok)>
        {
            ("Geschwindigkeit v_T >= 0.5 m/s", c.VelocityOk),
            ("Schubspannung Tau >= 2.5 N/m²", c.ShearOk),
            ("Froude Fr <= 1", c.FroudeOk),
            ("Ablagerungsfrei v_T >= v_c", c.AblagerungOk),
        };

        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.ConstantColumn(20);
                columns.RelativeColumn();
            });

            foreach (var (label, ok) in checks)
            {
                var symbol = ok ? "●" : "●";
                var color = ok ? "#16A34A" : "#DC2626";
                table.Cell().PaddingVertical(1).Text(symbol).FontSize(12).FontColor(color);
                table.Cell().PaddingVertical(1).AlignMiddle().Text(label).FontSize(9);
            }
        });
    }

    // ── Formatting ──────────────────────────────────────────

    private static string Fmt(double v, int dec) =>
        v.ToString($"F{dec}", CultureInfo.InvariantCulture);

    private static string FmtSci(double v)
    {
        if (v == 0) return "—";
        if (v < 0.001) return v.ToString("E2", CultureInfo.InvariantCulture);
        return v.ToString("F4", CultureInfo.InvariantCulture);
    }

    private static byte[]? ResolveLogoBytes(string? logoPath)
    {
        if (string.IsNullOrWhiteSpace(logoPath) || !File.Exists(logoPath))
            return null;
        try { return File.ReadAllBytes(logoPath); }
        catch { return null; }
    }
}

/// <summary>
/// Flattened calculation result for PDF generation.
/// Created from HydraulikResult + HydraulikInput in the UI layer.
/// </summary>
public sealed record HydraulikCalcResult
{
    // Input echo
    public double DN_mm { get; init; }
    public double Wasserstand_mm { get; init; }
    public double Gefaelle_Promille { get; init; }
    public double Kb { get; init; }
    public string AbwasserTyp { get; init; } = "MR";
    public double Temperatur_C { get; init; }
    public string Material { get; init; } = "";

    // Teilfuellung
    public double V_T { get; init; }
    public double Q_T { get; init; }
    public double A_T { get; init; }
    public double Lu_T { get; init; }
    public double Rhy_T { get; init; }
    public double Bsp { get; init; }

    // Vollfuellung
    public double V_V { get; init; }
    public double Q_V { get; init; }

    // Kennzahlen
    public double Re { get; init; }
    public double Fr { get; init; }
    public double Lambda { get; init; }
    public double Tau { get; init; }
    public double Ny { get; init; }

    // Ablagerung
    public double Vc { get; init; }
    public double Ic { get; init; }
    public double TauC { get; init; }

    // Auslastung
    public double Auslastung { get; init; }

    // Bewertung
    public bool VelocityOk { get; init; }
    public bool ShearOk { get; init; }
    public bool FroudeOk { get; init; }
    public bool AblagerungOk { get; init; }
}
