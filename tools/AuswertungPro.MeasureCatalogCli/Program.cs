// See https://aka.ms/new-console-template for more information
using System.Linq;
using AuswertungPro.Next.Infrastructure.Costs;

string? projectPath = args.Length > 0 ? args[0] : null;

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
