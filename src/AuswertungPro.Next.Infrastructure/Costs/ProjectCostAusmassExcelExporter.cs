using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using AuswertungPro.Next.Domain.Models;
using ClosedXML.Excel;

namespace AuswertungPro.Next.Infrastructure.Costs;

public sealed record ProjectCostAusmassExportOptions
{
    public bool IncludePrices { get; init; } = true;
    public string Title { get; init; } = "NPK 135 Ausmass";
    public string Currency { get; init; } = "CHF";
    public Project? Project { get; init; }
    public bool IncludeHoldingSheet { get; init; } = true;
}

public sealed record ProjectCostAusmassExportResult
{
    public bool Ok { get; init; }
    public string? Error { get; init; }
    public int PositionCount { get; init; }
    public decimal NetTotal { get; init; }
    public decimal VatRate { get; init; }
    public decimal VatAmount { get; init; }
    public decimal TotalInclVat { get; init; }

    public static ProjectCostAusmassExportResult Fail(string error)
        => new() { Ok = false, Error = error };
}

public sealed record ProjectCostAusmassLine
{
    public string SubmissionPos { get; init; } = "";
    public string Group { get; init; } = "";
    public string Label { get; init; } = "";
    public string Unit { get; init; } = "";
    public decimal Qty { get; init; }
    public decimal UnitPrice { get; init; }
    public decimal Amount { get; init; }
    public string AllocationScope { get; init; } = "";
    public IReadOnlyList<string> Holdings { get; init; } = Array.Empty<string>();
}

public sealed class ProjectCostAusmassExcelExporter
{
    public ProjectCostAusmassExportResult Export(
        ProjectCostStore store,
        string outputPath,
        ProjectCostAusmassExportOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(store);
        options ??= new ProjectCostAusmassExportOptions();

        if (string.IsNullOrWhiteSpace(outputPath))
            return ProjectCostAusmassExportResult.Fail("Ausgabepfad fehlt.");

        try
        {
            var lines = BuildLines(store);
            if (lines.Count == 0)
                return ProjectCostAusmassExportResult.Fail("Keine gespeicherten Kostenpositionen gefunden.");

            var vatRate = ResolveVatRate(store);
            var netTotal = Math.Round(lines.Sum(l => l.Amount), 2, MidpointRounding.AwayFromZero);
            var vatAmount = Math.Round(netTotal * vatRate, 2, MidpointRounding.AwayFromZero);
            var totalInclVat = Math.Round(netTotal + vatAmount, 2, MidpointRounding.AwayFromZero);

            var outputDir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrWhiteSpace(outputDir))
                Directory.CreateDirectory(outputDir);

            using var workbook = new XLWorkbook();
            if (options.IncludeHoldingSheet && options.Project is not null)
                WriteHoldingSheet(workbook, options.Project, store, options.IncludePrices);
            WriteAusmassSheet(workbook, lines, options, vatRate, netTotal, vatAmount, totalInclVat);
            workbook.SaveAs(outputPath);

            return new ProjectCostAusmassExportResult
            {
                Ok = true,
                PositionCount = lines.Count,
                NetTotal = netTotal,
                VatRate = vatRate,
                VatAmount = vatAmount,
                TotalInclVat = totalInclVat
            };
        }
        catch (Exception ex)
        {
            return ProjectCostAusmassExportResult.Fail(ex.Message);
        }
    }

    public IReadOnlyList<ProjectCostAusmassLine> BuildLines(ProjectCostStore store)
    {
        ArgumentNullException.ThrowIfNull(store);

        var rows = store.ByHolding
            .OrderBy(kvp => kvp.Key, StringComparer.OrdinalIgnoreCase)
            .SelectMany(kvp => (kvp.Value.Measures ?? new List<MeasureCost>())
                .SelectMany(measure => (measure.Lines ?? new List<CostLine>())
                    .Where(line => line.Selected)
                    .Select(line => new RawAusmassRow(
                        Holding: NormalizeHolding(kvp.Value.Holding, kvp.Key),
                        SubmissionPos: ResolveSubmissionPos(line),
                        Group: line.Group ?? "",
                        Label: BuildLabel(measure, line),
                        Unit: line.Unit ?? "",
                        Qty: Math.Max(0m, line.Qty),
                        UnitPrice: Math.Round(line.UnitPrice, 2, MidpointRounding.AwayFromZero),
                        AllocationScope: IsProjectSplitLine(line.ItemKey) ? "Projekt" : "Haltung"))))
            .ToList();

        return rows
            .GroupBy(row => new
            {
                row.SubmissionPos,
                row.Group,
                row.Label,
                row.Unit,
                row.UnitPrice,
                row.AllocationScope
            })
            .Select(group =>
            {
                var qty = group.Key.AllocationScope == "Projekt"
                    ? 1m
                    : Math.Round(group.Sum(x => x.Qty), 3, MidpointRounding.AwayFromZero);
                var amount = Math.Round(qty * group.Key.UnitPrice, 2, MidpointRounding.AwayFromZero);

                return new ProjectCostAusmassLine
                {
                    SubmissionPos = group.Key.SubmissionPos,
                    Group = group.Key.Group,
                    Label = group.Key.Label,
                    Unit = group.Key.Unit,
                    UnitPrice = group.Key.UnitPrice,
                    Qty = qty,
                    Amount = amount,
                    AllocationScope = group.Key.AllocationScope,
                    Holdings = group.Select(x => x.Holding)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                        .ToList()
                };
            })
            .OrderBy(line => ResolveSubmissionSortKey(line.SubmissionPos))
            .ThenBy(line => line.Group, StringComparer.OrdinalIgnoreCase)
            .ThenBy(line => line.Label, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static void WriteAusmassSheet(
        XLWorkbook workbook,
        IReadOnlyList<ProjectCostAusmassLine> lines,
        ProjectCostAusmassExportOptions options,
        decimal vatRate,
        decimal netTotal,
        decimal vatAmount,
        decimal totalInclVat)
    {
        var sheetName = options.IncludePrices ? "Ausmass mit Kosten" : "Ausschreibung ohne Preise";
        var ws = workbook.Worksheets.Add(sheetName);

        ws.Cell(1, 1).Value = options.IncludePrices
            ? options.Title
            : "NPK 135 Ausschreibung";
        ws.Cell(1, 1).Style.Font.Bold = true;
        ws.Cell(1, 1).Style.Font.FontSize = 16;
        ws.Range(1, 1, 1, 9).Merge();

        ws.Cell(2, 1).Value = options.IncludePrices
            ? "Gesamtausmass aus gespeicherten Haltungs-Kostenpositionen."
            : "Ausschreibungsfassung: Mengen ohne Einheitspreise und ohne Betraege.";
        ws.Range(2, 1, 2, 9).Merge();

        var row = 4;
        WriteHeader(ws, row, options.IncludePrices);
        row++;

        foreach (var block in lines.GroupBy(l => ResolveBlock(l.SubmissionPos)))
        {
            ws.Cell(row, 1).Value = block.Key;
            ws.Cell(row, 2).Value = ResolveBlockTitle(block.Key);
            ws.Range(row, 1, row, 9).Style.Font.Bold = true;
            ws.Range(row, 1, row, 9).Style.Fill.BackgroundColor = XLColor.FromHtml("#E7EEF8");
            row++;

            foreach (var line in block)
            {
                ws.Cell(row, 1).Value = line.SubmissionPos;
                ws.Cell(row, 2).Value = line.Group;
                ws.Cell(row, 3).Value = line.Label;
                ws.Cell(row, 4).Value = line.Unit;
                ws.Cell(row, 5).Value = (double)line.Qty;

                if (options.IncludePrices)
                {
                    ws.Cell(row, 6).Value = (double)line.UnitPrice;
                    ws.Cell(row, 7).Value = (double)line.Amount;
                }

                ws.Cell(row, 8).Value = line.AllocationScope;
                ws.Cell(row, 9).Value = string.Join(", ", line.Holdings);
                row++;
            }

            row++;
        }

        if (options.IncludePrices)
        {
            row++;
            ws.Cell(row, 6).Value = "Total exkl. MWST";
            ws.Cell(row, 7).Value = (double)netTotal;
            ws.Range(row, 6, row, 7).Style.Font.Bold = true;
            row++;
            ws.Cell(row, 6).Value = $"MWST {vatRate:P1}";
            ws.Cell(row, 7).Value = (double)vatAmount;
            row++;
            ws.Cell(row, 6).Value = "Total inkl. MWST";
            ws.Cell(row, 7).Value = (double)totalInclVat;
            ws.Range(row, 6, row, 7).Style.Font.Bold = true;
        }

        ws.Columns(1, 9).AdjustToContents();
        ws.Column(3).Width = Math.Min(Math.Max(ws.Column(3).Width, 32), 60);
        ws.Column(9).Width = Math.Min(Math.Max(ws.Column(9).Width, 18), 45);
        ws.Column(3).Style.Alignment.WrapText = true;
        ws.Column(9).Style.Alignment.WrapText = true;
        ws.Column(5).Style.NumberFormat.Format = "#,##0.###";
        ws.Column(6).Style.NumberFormat.Format = "#,##0.00";
        ws.Column(7).Style.NumberFormat.Format = "#,##0.00";
        ws.SheetView.FreezeRows(4);
    }

    private static void WriteHoldingSheet(
        XLWorkbook workbook,
        Project project,
        ProjectCostStore store,
        bool includePrices)
    {
        var ws = workbook.Worksheets.Add("Haltungen");
        ws.Cell(10, 1).Value = BuildProjectTitle(project);
        ws.Range(10, 1, 10, 17).Merge();
        ws.Cell(10, 1).Style.Font.Bold = true;
        ws.Cell(10, 1).Style.Font.FontSize = 12;

        ws.Cell(10, 18).Value = "Renovierung Inliner Stk.";
        ws.Cell(10, 19).Value = "m";
        ws.Cell(10, 20).Value = "Anschlüsse Stk.";
        ws.Cell(10, 21).Value = "Reparatur Manschette";
        ws.Cell(10, 22).Value = "Reparatur Kurzliner";
        ws.Cell(10, 23).Value = "Erneuerung Neubau m";
        ws.Cell(10, 24).Value = "Status";
        ws.Cell(10, 25).Value = "Ausgeführt am";

        var headers = new[]
        {
            "NR.",
            "Haltungsnahme (ID)",
            "Strasse",
            "Rohrmaterial",
            "DN mm",
            "Nutzungsart",
            "Haltungslänge m",
            "Fliessrichtung",
            "Primäre Schäden",
            "Zustandsklasse",
            "Prüfungsresultat",
            "Sanieren Ja/Nein",
            "Empfohlene Sanierungsmassnahmen",
            "Kosten",
            "Eigentümer",
            "Bemerkungen",
            "Link"
        };

        for (var i = 0; i < headers.Length; i++)
            ws.Cell(11, i + 1).Value = headers[i];

        for (var col = 18; col <= 23; col++)
            ws.Cell(11, col).FormulaA1 = $"SUM({ColumnName(col)}12:{ColumnName(col)}506)";
        ws.Cell(11, 24).Value = "offen/abgeschlossen";
        ws.Cell(11, 25).Value = "Datum/Jahr";

        var records = project.Data
            .OrderBy(r => TryInt(r.GetFieldValue("NR")) ?? int.MaxValue)
            .ThenBy(r => r.GetFieldValue("Haltungsname") ?? "", StringComparer.OrdinalIgnoreCase)
            .ToList();

        var row = 12;
        var runningNr = 1;
        foreach (var record in records)
        {
            var holding = (record.GetFieldValue("Haltungsname") ?? "").Trim();
            ws.Cell(row, 1).Value = ResolveText(record, "NR", defaultValue: runningNr.ToString(CultureInfo.InvariantCulture));
            ws.Cell(row, 2).Value = holding;
            ws.Cell(row, 3).Value = ResolveText(record, "Strasse");
            ws.Cell(row, 4).Value = ResolveText(record, "Rohrmaterial", "Material");
            ws.Cell(row, 5).Value = ResolveText(record, "DN_mm", "DN");
            ws.Cell(row, 6).Value = ResolveText(record, "Nutzungsart");
            ws.Cell(row, 7).Value = ResolveText(record, "Haltungslaenge_m");
            ws.Cell(row, 8).Value = ResolveText(record, "Inspektionsrichtung", "Fliessrichtung");
            ws.Cell(row, 9).Value = ResolveText(record, "Primaere_Schaeden");
            ws.Cell(row, 10).Value = ResolveText(record, "Zustandsklasse");
            ws.Cell(row, 11).Value = ResolveText(record, "Pruefungsresultat");
            var sanieren = ResolveText(record, "Sanieren_JaNein");
            ws.Cell(row, 12).Value = sanieren;
            ws.Cell(row, 13).Value = ResolveText(record, "Empfohlene_Sanierungsmassnahmen")
                .Replace("\r\n", "\n")
                .Replace("\n", " / ");
            ws.Cell(row, 14).Value = includePrices ? ResolveCostText(store, holding, record) : "";
            ws.Cell(row, 15).Value = ResolveText(record, "Eigentuemer");
            ws.Cell(row, 16).Value = ResolveText(record, "Bemerkungen");
            ws.Cell(row, 17).Value = ResolveText(record, "Link");
            ws.Cell(row, 18).Value = ResolveText(record, "Renovierung_Inliner_Stk");
            ws.Cell(row, 19).Value = ResolveText(record, "Renovierung_Inliner_m");
            ws.Cell(row, 20).Value = ResolveText(record, "Anschluesse_verpressen");
            ws.Cell(row, 21).Value = ResolveText(record, "Reparatur_Manschette");
            ws.Cell(row, 22).Value = ResolveText(record, "Reparatur_Kurzliner");
            ws.Cell(row, 23).Value = ResolveText(record, "Erneuerung_Neubau_m");
            ws.Cell(row, 24).Value = ResolveText(record, "Status", defaultValue: IsYes(sanieren) ? "offen" : "");
            ws.Cell(row, 25).Value = ResolveText(record, "Datum_Jahr");

            row++;
            runningNr++;
        }

        var header = ws.Range(11, 1, 11, 25);
        header.Style.Font.Bold = true;
        header.Style.Fill.BackgroundColor = XLColor.FromHtml("#D9E2F3");
        header.Style.Border.BottomBorder = XLBorderStyleValues.Thin;

        ws.Range(10, 18, 10, 25).Style.Font.Bold = true;
        ws.Range(10, 18, 11, 25).Style.Fill.BackgroundColor = XLColor.FromHtml("#E7EEF8");
        ws.Columns(1, 25).AdjustToContents();
        ws.Column(9).Width = Math.Min(Math.Max(ws.Column(9).Width, 28), 55);
        ws.Column(13).Width = Math.Min(Math.Max(ws.Column(13).Width, 28), 55);
        ws.Column(16).Width = Math.Min(Math.Max(ws.Column(16).Width, 20), 45);
        ws.Column(17).Width = Math.Min(Math.Max(ws.Column(17).Width, 24), 48);
        ws.Columns(9, 17).Style.Alignment.WrapText = true;
        ws.SheetView.FreezeRows(11);
        ws.SheetView.FreezeColumns(2);
    }

    private static string BuildProjectTitle(Project project)
    {
        var name = string.IsNullOrWhiteSpace(project.Name) ? "Auswertung" : project.Name.Trim();
        project.Metadata.TryGetValue("Gemeinde", out var municipality);
        project.Metadata.TryGetValue("Zone", out var zone);

        var suffix = string.Join(" ", new[] { municipality, zone }
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!.Trim()));

        return string.IsNullOrWhiteSpace(suffix) ? name : $"{name} {suffix}";
    }

    private static int? TryInt(string? value)
        => int.TryParse((value ?? "").Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;

    private static string ColumnName(int columnNumber)
    {
        var name = "";
        var col = columnNumber;
        while (col > 0)
        {
            var remainder = (col - 1) % 26;
            name = (char)('A' + remainder) + name;
            col = (col - 1) / 26;
        }

        return name;
    }

    private static string ResolveText(
        HaltungRecord record,
        string fieldName,
        string? alternateFieldName = null,
        string defaultValue = "")
    {
        var value = (record.GetFieldValue(fieldName) ?? "").Trim();
        if (!string.IsNullOrWhiteSpace(value))
            return value;

        if (!string.IsNullOrWhiteSpace(alternateFieldName))
        {
            value = (record.GetFieldValue(alternateFieldName) ?? "").Trim();
            if (!string.IsNullOrWhiteSpace(value))
                return value;
        }

        return defaultValue;
    }

    private static string ResolveCostText(ProjectCostStore store, string holding, HaltungRecord record)
    {
        if (!string.IsNullOrWhiteSpace(holding)
            && store.ByHolding.TryGetValue(holding.Trim(), out var cost))
            return cost.Total.ToString("0.00", CultureInfo.InvariantCulture);

        return ResolveText(record, "Kosten");
    }

    private static bool IsYes(string? value)
    {
        var text = (value ?? "").Trim();
        return text.Equals("Ja", StringComparison.OrdinalIgnoreCase)
            || text.Equals("Yes", StringComparison.OrdinalIgnoreCase)
            || text.Equals("True", StringComparison.OrdinalIgnoreCase)
            || text.Equals("1", StringComparison.OrdinalIgnoreCase);
    }

    private static void WriteHeader(IXLWorksheet ws, int row, bool includePrices)
    {
        ws.Cell(row, 1).Value = "NPK Pos.";
        ws.Cell(row, 2).Value = "Gruppe";
        ws.Cell(row, 3).Value = "Bezeichnung";
        ws.Cell(row, 4).Value = "Einheit";
        ws.Cell(row, 5).Value = "Menge";
        ws.Cell(row, 6).Value = includePrices ? "EP CHF" : "EP";
        ws.Cell(row, 7).Value = includePrices ? "Betrag CHF" : "Betrag";
        ws.Cell(row, 8).Value = "Aufteilung";
        ws.Cell(row, 9).Value = "Haltungen";

        var range = ws.Range(row, 1, row, 9);
        range.Style.Font.Bold = true;
        range.Style.Fill.BackgroundColor = XLColor.FromHtml("#D9E2F3");
        range.Style.Border.BottomBorder = XLBorderStyleValues.Thin;
    }

    private static decimal ResolveVatRate(ProjectCostStore store)
    {
        var rates = store.ByHolding.Values
            .Select(c => c.MwstRate)
            .Where(r => r > 0m)
            .Distinct()
            .ToList();

        return rates.Count == 1 ? rates[0] : 0.081m;
    }

    private static string NormalizeHolding(string? value, string fallback)
        => string.IsNullOrWhiteSpace(value) ? fallback.Trim() : value.Trim();

    private static string BuildLabel(MeasureCost measure, CostLine line)
    {
        var label = string.IsNullOrWhiteSpace(line.Text) ? line.ItemKey : line.Text.Trim();
        if (!measure.Dn.HasValue || !ShouldShowDn(line.ItemKey))
            return label;

        return label.Contains("DN ", StringComparison.OrdinalIgnoreCase)
            ? label
            : $"{label} DN {measure.Dn.Value}";
    }

    private static bool ShouldShowDn(string? itemKey)
    {
        if (string.IsNullOrWhiteSpace(itemKey))
            return false;

        var key = itemKey.Trim();
        return key.StartsWith("SCHLAUCHLINER", StringComparison.OrdinalIgnoreCase)
            || key.StartsWith("LINERENDMANSCHETTE", StringComparison.OrdinalIgnoreCase)
            || key.StartsWith("MANSCHETTE", StringComparison.OrdinalIgnoreCase)
            || key.StartsWith("KURZLINER", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsProjectSplitLine(string? itemKey)
    {
        if (string.IsNullOrWhiteSpace(itemKey))
            return false;

        var key = itemKey.Trim();
        return key.StartsWith("INSTALL_", StringComparison.OrdinalIgnoreCase)
            || key.StartsWith("HL_INSTALL_", StringComparison.OrdinalIgnoreCase)
            || key.Contains("WASSERHALTUNG", StringComparison.OrdinalIgnoreCase);
    }

    private static string ResolveSubmissionPos(CostLine line)
    {
        if (!string.IsNullOrWhiteSpace(line.SubmissionPos))
            return line.SubmissionPos.Trim();

        var itemKey = line.ItemKey ?? "";
        if (itemKey.StartsWith("INSTALL_", StringComparison.OrdinalIgnoreCase)
            || itemKey.StartsWith("HL_INSTALL_", StringComparison.OrdinalIgnoreCase))
            return "100.1";

        return itemKey.ToUpperInvariant() switch
        {
            "VORARBEIT_REINIGUNG" => "2.1.1",
            "VORARBEIT_TV_VORKONTROLLE" => "2.1.2",
            "VORARBEIT_FRAESEN" => "2.1.5",
            "VORARBEIT_EINMESSUNG" => "2.1.x",
            "VORARBEIT_ANSCHLUSS_EINMESSEN" => "2.1.x",
            "VORARBEIT_WASSERHALTUNG" => "300.1",
            "SCHLAUCHLINER_PRELINER" => "600.1",
            "SCHLAUCHLINER_GFK" => "600.5",
            "SCHLAUCHLINER_NADELFILZ" => "600.2",
            "SCHLAUCHLINER_NADELFILZ_OPENEND" => "600.2",
            "ANSCHLUSS_AUFFRAESEN" => "600.6",
            "ANSCHLUSS_EINBINDEN" => "600.6",
            "LINERENDMANSCHETTE_LEM" => "600.7",
            "KURZLINER_PARTLINER" => "500.1",
            "MANSCHETTE_EDELSTAHL" => "500.2",
            "QK_DICHTHEITSPRUEFUNG" => "800.1",
            "QK_TV_ABNAHME" => "2.1.4",
            "QK_DOKUMENTATION" => "800.2",
            _ => ""
        };
    }

    private static string ResolveBlock(string? submissionPos)
    {
        if (string.IsNullOrWhiteSpace(submissionPos))
            return "999";

        var pos = submissionPos.Trim();
        if (pos.StartsWith("2.", StringComparison.OrdinalIgnoreCase))
            return "200";

        var head = pos.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault();
        return string.IsNullOrWhiteSpace(head) ? "999" : head;
    }

    private static string ResolveBlockTitle(string block)
        => block switch
        {
            "100" => "Installation / Einrichtungen",
            "200" => "Vorarbeiten",
            "300" => "Wasserhaltung",
            "500" => "Reparaturen",
            "600" => "Renovierungen / Schlauchlining",
            "700" => "Schachtsanierung",
            "800" => "Qualitaetskontrolle",
            _ => "Weitere Positionen"
        };

    private static decimal ResolveSubmissionSortKey(string? submissionPos)
    {
        if (string.IsNullOrWhiteSpace(submissionPos))
            return 99999m;

        var parts = submissionPos
            .Trim()
            .Replace('x', '9')
            .Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (parts.Length == 0 || !int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var head))
            return 99999m;

        if (head == 2 && parts.Length >= 2)
        {
            var minor = int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var m) ? m : 0;
            var detail = parts.Length >= 3 && int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var d) ? d : 0;
            return 200m + minor + (detail / 100m);
        }

        var tail = 0m;
        for (var i = 1; i < parts.Length; i++)
        {
            if (!int.TryParse(parts[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
                continue;
            tail += value / (decimal)Math.Pow(100, i);
        }

        return head + tail;
    }

    private sealed record RawAusmassRow(
        string Holding,
        string SubmissionPos,
        string Group,
        string Label,
        string Unit,
        decimal Qty,
        decimal UnitPrice,
        string AllocationScope);
}
