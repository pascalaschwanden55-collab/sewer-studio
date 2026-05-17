using System.Globalization;
using AuswertungPro.Next.Infrastructure.Costs;

if (args.Length > 0 && args[0].Equals("calc", StringComparison.OrdinalIgnoreCase))
{
    RunCalculation(args.Skip(1).ToArray());
    return;
}

RunCatalogList(args.Length > 0 ? args[0] : null);

static void RunCatalogList(string? projectPath)
{
    var templateStore = new MeasureTemplateStore();
    var catalogStore = new CostCatalogStore();

    var templates = templateStore.LoadMerged(projectPath);
    var catalog = catalogStore.LoadMerged(projectPath);

    Console.WriteLine("Measure templates: " + templates.Measures.Count);
    foreach (var template in templates.Measures)
    {
        var disabled = template.Disabled ? " (deaktiviert)" : "";
        Console.WriteLine($"- {template.Id}: {template.Name}{disabled} ({template.Lines.Count} Positionen)");
    }

    Console.WriteLine();
    Console.WriteLine("Cost catalog items: " + catalog.Items.Count);
    foreach (var item in catalog.Items.Take(5))
    {
        Console.WriteLine($"- {item.Key}: {item.Name} ({item.Unit})");
    }

    Console.WriteLine();
    Console.WriteLine("Hinweis: optional Projektpfad als erstes Argument angeben.");
    Console.WriteLine("Kalkulation: calc --measure SCHLAUCHLINER_NADELFILZ --holding 100-200 --dn 200 --length 12.5 --connections 2");
}

static void RunCalculation(string[] args)
{
    var projectPath = GetOption(args, "--project");
    var holding = GetOption(args, "--holding") ?? "MANUELL";
    var measures = GetMeasures(args);
    var dn = ParseInt(GetOption(args, "--dn"));
    var length = ParseDecimal(GetOption(args, "--length"));
    var connections = ParseInt(GetOption(args, "--connections")) ?? 0;
    var vat = ParseDecimal(GetOption(args, "--vat"));
    var count = Math.Max(1, ParseInt(GetOption(args, "--count")) ?? 1);

    if (measures.Count == 0)
    {
        Console.Error.WriteLine("Bitte mindestens eine Massnahme angeben: --measure SCHLAUCHLINER_NADELFILZ");
        Environment.ExitCode = 2;
        return;
    }

    var templateStore = new MeasureTemplateStore();
    var catalogStore = new CostCatalogStore();
    var templates = templateStore.LoadMerged(projectPath);
    var catalog = catalogStore.LoadMerged(projectPath);

    var service = new HoldingCostCalculationService();
    if (count > 1)
    {
        var projectResult = service.CalculateProject(
            templates,
            catalog,
            new ProjectCostCalculationRequest
            {
                VatRate = vat,
                Holdings = Enumerable.Range(1, count)
                    .Select(i => new HoldingCostCalculationRequest
                    {
                        Holding = $"{holding}_{i:00}",
                        MeasureIds = measures,
                        Dn = dn,
                        LengthMeters = length,
                        Connections = connections
                    })
                    .ToList()
            });

        Console.WriteLine($"Projektkalkulation: {holding} ({count} Haltungen)");
        Console.WriteLine($"Massnahmen: {string.Join(", ", measures)}");
        Console.WriteLine($"Netto: {projectResult.TotalCost.Total:N2} {catalog.Currency}");
        Console.WriteLine($"MWST ({projectResult.TotalCost.MwstRate:P1}): {projectResult.TotalCost.MwstAmount:N2} {catalog.Currency}");
        Console.WriteLine($"Total inkl. MWST: {projectResult.TotalCost.TotalInclMwst:N2} {catalog.Currency}");

        if (projectResult.Warnings.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("Warnungen:");
            foreach (var warning in projectResult.Warnings)
                Console.WriteLine("- " + warning);
        }

        Console.WriteLine();
        Console.WriteLine("NPK-/Submissions-Zusammenfassung:");
        foreach (var line in projectResult.SummaryLines)
        {
            var scope = line.AllocationScope == "ProjectSplit" ? "geteilt" : "pro Haltung";
            var pos = string.IsNullOrWhiteSpace(line.SubmissionPos) ? "-" : line.SubmissionPos;
            Console.WriteLine($"- {pos} [{scope}] {line.Label} | {line.Qty:0.###} {line.Unit} x {line.UnitPrice:N2} = {line.Amount:N2}");
        }

        Console.WriteLine();
        Console.WriteLine("Kosten je Haltung:");
        foreach (var cost in projectResult.Store.ByHolding.Values.OrderBy(c => c.Holding, StringComparer.OrdinalIgnoreCase))
            Console.WriteLine($"- {cost.Holding}: {cost.Total:N2} {catalog.Currency}");

        return;
    }

    var result = service.Calculate(
        templates,
        catalog,
        new HoldingCostCalculationRequest
        {
            Holding = holding,
            MeasureIds = measures,
            Dn = dn,
            LengthMeters = length,
            Connections = connections,
            VatRate = vat
        });

    Console.WriteLine($"Kalkulation: {result.Cost.Holding}");
    Console.WriteLine($"Massnahmen: {string.Join(", ", result.Cost.Measures.Select(m => m.MeasureName))}");
    Console.WriteLine($"Netto: {result.Cost.Total:N2} {catalog.Currency}");
    Console.WriteLine($"MWST ({result.Cost.MwstRate:P1}): {result.Cost.MwstAmount:N2} {catalog.Currency}");
    Console.WriteLine($"Total inkl. MWST: {result.Cost.TotalInclMwst:N2} {catalog.Currency}");

    if (result.Warnings.Count > 0)
    {
        Console.WriteLine();
        Console.WriteLine("Warnungen:");
        foreach (var warning in result.Warnings)
            Console.WriteLine("- " + warning);
    }

    Console.WriteLine();
    Console.WriteLine("Positionen:");
    foreach (var measure in result.Cost.Measures)
    {
        Console.WriteLine($"[{measure.MeasureName}]");
        foreach (var line in measure.Lines.Where(l => l.Selected))
        {
            var amount = line.Qty * line.UnitPrice;
            Console.WriteLine($"- {line.Group}: {line.Text} | {line.Qty:0.###} {line.Unit} x {line.UnitPrice:N2} = {amount:N2}");
        }
    }
}

static string? GetOption(string[] args, string name)
{
    for (var i = 0; i < args.Length; i++)
    {
        if (!args[i].Equals(name, StringComparison.OrdinalIgnoreCase))
            continue;

        if (i + 1 >= args.Length)
            return null;

        return args[i + 1];
    }

    return null;
}

static List<string> GetMeasures(string[] args)
{
    var measures = new List<string>();
    for (var i = 0; i < args.Length; i++)
    {
        if (!args[i].Equals("--measure", StringComparison.OrdinalIgnoreCase)
            && !args[i].Equals("--measures", StringComparison.OrdinalIgnoreCase))
            continue;

        if (i + 1 >= args.Length)
            continue;

        measures.AddRange(args[i + 1]
            .Split([',', ';', '|'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }

    return measures.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
}

static int? ParseInt(string? value)
    => int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;

static decimal? ParseDecimal(string? value)
{
    if (string.IsNullOrWhiteSpace(value))
        return null;

    var text = value.Trim();
    if (decimal.TryParse(text, NumberStyles.Number, CultureInfo.CurrentCulture, out var current))
        return current;
    if (decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var invariant))
        return invariant;

    text = text.Replace(',', '.');
    return decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var normalized)
        ? normalized
        : null;
}
