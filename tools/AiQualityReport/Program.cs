using System.Text.Json;
using AuswertungPro.Next.Application.Ai.Evaluation;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Application.Schatten;
using AuswertungPro.Next.Infrastructure.Ai.KnowledgeBase;
using AuswertungPro.Next.Infrastructure.Projects;
using AuswertungPro.Next.Infrastructure.Schatten;

if (args.Contains("--help", StringComparer.OrdinalIgnoreCase))
{
    PrintHelp();
    return 0;
}

try
{
    var trainingPath = Option(args, "--training-samples")
        ?? KnowledgeBasePaths.GetTrainingSamplesPath();
    var output = Option(args, "--output")
        ?? Path.Combine(Environment.CurrentDirectory, "docs", "quality");
    var projects = Options(args, "--project");

    var samples = LoadSamples(trainingPath);
    var metadata = new List<FieldQualityCaseMetadata>();
    var shadow = new List<ShadowQualityInput>();
    LoadProjects(projects, metadata, shadow);

    var report = AiFieldQualityReportAnalyzer.Analyze(samples, metadata, shadow);
    var files = AiFieldQualityReportWriter.Write(output, report);

    Console.WriteLine("KI-Qualitaetsbericht erstellt");
    Console.WriteLine($"Trainingsdaten: {Path.GetFullPath(trainingPath)}");
    Console.WriteLine($"Deduplizierte KI-Befunde: {report.Detection.DeduplicatedAiFindings}");
    Console.WriteLine($"Moegliche Misses: {report.Detection.PossibleMisses}");
    Console.WriteLine($"Gruene Befunde geprueft: {report.GreenRelease.ReviewedGreenFindings}");
    Console.WriteLine($"Gruene Fehler: {report.GreenRelease.GreenErrors}");
    Console.WriteLine($"Obere 95%-Fehlergrenze: {report.GreenRelease.ErrorRateUpper95:P2}");
    Console.WriteLine($"Freigabekriterium: {(report.GreenRelease.ReleaseCriterionMet ? "ERFUELLT" : "NICHT ERFUELLT")}");
    Console.WriteLine($"Schatten-Vergleiche: {report.Shadow.Comparable}");
    Console.WriteLine($"Markdown: {files.MarkdownPath}");
    Console.WriteLine($"JSON:     {files.JsonPath}");
    Console.WriteLine($"Fehler:   {files.IssuesCsvPath}");
    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine("FEHLER: " + ex.Message);
    return 1;
}

static IReadOnlyList<TrainingSample> LoadSamples(string path)
{
    if (!File.Exists(path))
        return Array.Empty<TrainingSample>();

    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
    return JsonSerializer.Deserialize<List<TrainingSample>>(File.ReadAllText(path), options)
           ?? new List<TrainingSample>();
}

static void LoadProjects(
    IReadOnlyList<string> projectPaths,
    ICollection<FieldQualityCaseMetadata> metadata,
    ICollection<ShadowQualityInput> shadow)
{
    var projectRepository = new JsonProjectRepository();
    var shadowRepository = new SchattenAuswertungStoreRepository();

    foreach (var path in projectPaths)
    {
        var fullPath = Path.GetFullPath(path);
        var loaded = projectRepository.Load(fullPath);
        if (!loaded.Ok || loaded.Value is null)
            throw new InvalidDataException($"Projekt nicht lesbar ({fullPath}): {loaded.ErrorMessage}");

        var shadowStore = shadowRepository.Load(fullPath, out var shadowError);
        if (shadowError is not null)
            throw new InvalidDataException($"Schattenauswertung nicht lesbar ({fullPath}): {shadowError}");

        foreach (var record in loaded.Value.Data)
        {
            var caseId = record.GetFieldValue("Haltungsname");
            if (string.IsNullOrWhiteSpace(caseId))
                caseId = record.Id.ToString();

            metadata.Add(new FieldQualityCaseMetadata(
                caseId,
                ParseInt(record.GetFieldValue("DN_mm")),
                record.GetFieldValue("Rohrmaterial"),
                FirstNonBlank(
                    record.GetFieldValue("Bildqualitaet"),
                    record.GetFieldValue("Aufnahmetechnik"))));

            shadowStore.ByHaltung.TryGetValue(caseId, out var result);
            var shadowMeasure = result?.KiMassnahme
                ?? result?.RegelMassnahmen.FirstOrDefault();
            var shadowCost = result?.KostenErwartet ?? result?.RegelKosten;
            var stale = result is not null
                        && result.Status != SchattenStatus.OhneCodierung
                        && !string.Equals(
                            result.CodierungsHash,
                            SchattenCodierungsHash.Compute(record),
                            StringComparison.Ordinal);

            shadow.Add(new ShadowQualityInput(
                caseId,
                record.GetFieldValue("Zustandsklasse"),
                record.GetFieldValue("Empfohlene_Sanierungsmassnahmen"),
                record.GetFieldValue("Kosten"),
                result?.Zustandsklasse,
                shadowMeasure,
                shadowCost,
                stale,
                shadowStore.KiModell));
        }
    }
}

static int? ParseInt(string? value)
    => int.TryParse(value, out var parsed) && parsed > 0 ? parsed : null;

static string FirstNonBlank(params string?[] values)
    => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? "";

static string? Option(string[] values, string name)
{
    for (var i = 0; i < values.Length - 1; i++)
    {
        if (values[i].Equals(name, StringComparison.OrdinalIgnoreCase))
            return values[i + 1];
    }
    return null;
}

static IReadOnlyList<string> Options(string[] values, string name)
{
    var result = new List<string>();
    for (var i = 0; i < values.Length - 1; i++)
    {
        if (values[i].Equals(name, StringComparison.OrdinalIgnoreCase))
            result.Add(values[i + 1]);
    }
    return result;
}

static void PrintHelp()
{
    Console.WriteLine("""
        AiQualityReport

        Fuehrt Erkennungs- und Schattenqualitaet in einem Bericht zusammen.

        Verwendung:
          dotnet run --project tools/AiQualityReport -- \
            --training-samples C:\KI_BRAIN\training_samples.json \
            --project D:\Projekte\ProjektA\projekt.json \
            --project D:\Projekte\ProjektB\projekt.json \
            --output docs\quality

        --project kann mehrfach angegeben werden. Ohne Projektpfad wird nur die
        Erkennungsebene aus den menschlich geprueften Training-Samples ausgewertet.
        """);
}
