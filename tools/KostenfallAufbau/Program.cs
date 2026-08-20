using System.Text.Json;
using AuswertungPro.Next.Application.Kostenanalyse;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Kostenanalyse;

// Baut die Lernfaelle aus einem Projekt auf. Liest nur; schreibt allein die Falldatei.

if (args.Length >= 1 && args[0] == "--hilfe")
{
    SchreibeHilfe();
    return 0;
}

if (args.Length < 3)
{
    SchreibeHilfe();
    return 2;
}

var projektPfad = args[0];
var kostenPfad = args[1];
var wurzel = args[2];
var schreiben = args.Contains("--execute");

var optionen = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

Project? projekt;
ProjectCostStore? kosten;
try
{
    projekt = JsonSerializer.Deserialize<Project>(File.ReadAllText(projektPfad), optionen);
    kosten = JsonSerializer.Deserialize<ProjectCostStore>(File.ReadAllText(kostenPfad), optionen);
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Lesefehler: {ex.Message}");
    return 1;
}

if (projekt is null || kosten is null)
{
    Console.Error.WriteLine("Projekt oder Kostendatei nicht lesbar.");
    return 1;
}

var projektName = string.IsNullOrWhiteSpace(projekt.Name)
    ? Path.GetFileNameWithoutExtension(projektPfad)
    : projekt.Name;

var (faelle, uebersprungen) = KostenfallAufbauLauf.Baue(projekt, kosten, projektName, DateTime.UtcNow);

Console.WriteLine($"Projekt          : {projektName}");
Console.WriteLine($"Haltungen        : {projekt.Data.Count}");
Console.WriteLine($"Kostenzeilen     : {kosten.ByHolding.Count}");
Console.WriteLine($"Faelle aufgebaut : {faelle.Count}");
Console.WriteLine($"Uebersprungen    : {uebersprungen.Count}");

// Nach Grund gruppieren — so sieht man sofort, WORAN es liegt.
foreach (var gruppe in uebersprungen
             .GroupBy(z => z[(z.IndexOf(": ", StringComparison.Ordinal) + 2)..])
             .OrderByDescending(g => g.Count()))
{
    Console.WriteLine($"  {gruppe.Count(),4}x  {gruppe.Key}");
}

if (!schreiben)
{
    Console.WriteLine();
    Console.WriteLine("Prueflauf - nichts geschrieben. Mit --execute schreiben.");
    return 0;
}

new KostenfallFileStore(wurzel).Speichere(faelle);
Console.WriteLine();
Console.WriteLine($"Geschrieben nach {Path.Combine(wurzel, "kostenanalyse", "kostenfaelle_v1.json")}");
return 0;

static void SchreibeHilfe()
{
    Console.WriteLine("Aufruf: KostenfallAufbau <projekt.json> <costs.json> <KnowledgeRoot> [--execute]");
    Console.WriteLine("        KostenfallAufbau --messen <KnowledgeRoot>");
    Console.WriteLine();
    Console.WriteLine("Ohne --execute ist es ein reiner Prueflauf.");
}
