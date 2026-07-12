using AuswertungPro.Next.Application.Backup;
using AuswertungPro.Next.Infrastructure.Ai.KnowledgeBase;
using AuswertungPro.Next.Infrastructure.Backup;
using AuswertungPro.Next.Infrastructure.Projects;
using Microsoft.Data.Sqlite;

if (args.Length == 2 && string.Equals(args[0], "--verify-restore", StringComparison.OrdinalIgnoreCase))
    return VerifyRestore(Path.GetFullPath(args[1]));

if (args.Length < 2)
{
    Console.Error.WriteLine("Aufruf: FullBackupSmoke <Backup-Elternordner> <Projektwurzel> [Repo-Wurzel]");
    return 2;
}

var backupParent = Path.GetFullPath(args[0]);
var projectRoot = Path.GetFullPath(args[1]);
var repoRoot = Path.GetFullPath(args.Length >= 3 ? args[2] : Directory.GetCurrentDirectory());
var knowledgeRoot = Environment.GetEnvironmentVariable("SEWERSTUDIO_KNOWLEDGE_ROOT")?.Trim();
if (string.IsNullOrWhiteSpace(knowledgeRoot))
    knowledgeRoot = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SewerStudio",
        "Knowledge");

var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
var roamingAppData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
var sources = new FullBackupSources(
    RepoRoot: repoRoot,
    KnowledgeRoot: knowledgeRoot,
    LocalSewerStudioDir: Path.Combine(localAppData, "SewerStudio"),
    RoamingSewerStudioDir: Path.Combine(roamingAppData, "SewerStudio"),
    RoamingAuswertungProDir: Path.Combine(roamingAppData, "AuswertungPro"),
    DesktopDir: Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
    AppVersion: "4.5.0",
    EnvironmentVariables: Environment.GetEnvironmentVariables()
        .Cast<System.Collections.DictionaryEntry>()
        .Where(entry => entry.Key is string key
                        && (key.StartsWith("SEWERSTUDIO_", StringComparison.OrdinalIgnoreCase)
                            || key.StartsWith("SEWER_", StringComparison.OrdinalIgnoreCase)))
        .ToDictionary(entry => (string)entry.Key, entry => Convert.ToString(entry.Value) ?? string.Empty,
            StringComparer.OrdinalIgnoreCase),
    ProjectRoots: [projectRoot],
    IncludeProjectVideos: false);

var service = new FullBackupService(() => sources);
Console.WriteLine($"Backup-Ziel: {Path.Combine(backupParent, BackupPlanBuilder.TargetFolderName)}");
Console.WriteLine($"Projekte: {projectRoot} (Videos ausgeschlossen)");
Console.WriteLine("Analysiere Quellen ...");

var report = await service.AnalyzeAsync(new Progress<string>(name => Console.WriteLine($"  {name}")));
Console.WriteLine($"Zu sichern: {report.TotalFiles:N0} Dateien, {report.TotalBytes / 1024d / 1024d / 1024d:N2} GB");

var lastPrinted = DateTime.MinValue;
var progress = new Progress<FullBackupProgress>(state =>
{
    if (DateTime.UtcNow - lastPrinted < TimeSpan.FromSeconds(2)
        && state.FilesDone < state.FilesTotal)
        return;

    lastPrinted = DateTime.UtcNow;
    var percent = state.BytesTotal <= 0 ? 0 : Math.Clamp(state.BytesDone * 100d / state.BytesTotal, 0, 100);
    Console.WriteLine($"[{percent,6:N2}%] {state.Component}: {Path.GetFileName(state.CurrentFile)}");
});

var result = await service.RunAsync(backupParent, progress);
Console.WriteLine($"Ergebnis: {(result.Success ? "ERFOLG" : "FEHLER")}");
Console.WriteLine($"Ziel: {result.TargetRoot}");
Console.WriteLine($"Kopiert: {result.FilesCopied:N0}; geprueft: {result.FilesVerified:N0}; unveraendert: {result.FilesUnchanged:N0}");
Console.WriteLine($"Datenbank-Schnappschuesse: {result.DatabasesSnapshotted:N0}; uebersprungen: {result.SkippedFiles.Count:N0}");
Console.WriteLine($"Dauer: {result.Duration}");

if (!result.Success)
{
    Console.Error.WriteLine(result.Error);
    return 1;
}

if (result.SkippedFiles.Count > 0)
{
    foreach (var skipped in result.SkippedFiles.Take(20))
        Console.Error.WriteLine($"Uebersprungen: {skipped}");
    return 3;
}

return 0;

static int VerifyRestore(string restoreRoot)
{
    var manifest = Path.Combine(restoreRoot, "manifest.json");
    var programSolution = Path.Combine(restoreRoot, "Programm", "AuswertungPro.sln");
    var settings = Path.Combine(restoreRoot, "Einstellungen", "Local_SewerStudio", "settings.json");
    var knowledgeDb = Path.Combine(restoreRoot, "KI_BRAIN", "KnowledgeBase.db");
    var projectFiles = Directory.Exists(Path.Combine(restoreRoot, "Projekte"))
        ? Directory.EnumerateFiles(Path.Combine(restoreRoot, "Projekte"), "projekt.json", SearchOption.AllDirectories)
            .OrderByDescending(path => path.Contains($"{Path.DirectorySeparatorChar}61{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .ThenBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray()
        : Array.Empty<string>();
    var projectFile = projectFiles.FirstOrDefault();

    var required = new[] { manifest, programSolution, settings, knowledgeDb };
    var missing = required.Where(path => !File.Exists(path)).ToArray();
    if (missing.Length > 0 || string.IsNullOrWhiteSpace(projectFile))
    {
        foreach (var path in missing)
            Console.Error.WriteLine($"Fehlt: {path}");
        if (string.IsNullOrWhiteSpace(projectFile))
            Console.Error.WriteLine("Fehlt: mindestens eine wiederhergestellte projekt.json");
        return 4;
    }

    var health = KnowledgeBaseHealthChecker.Check(knowledgeDb);
    if (!health.IsHealthy)
    {
        Console.Error.WriteLine($"KnowledgeBase ungesund: {health.Error}");
        return 5;
    }

    long sampleCount;
    var builder = new SqliteConnectionStringBuilder
    {
        DataSource = knowledgeDb,
        Mode = SqliteOpenMode.ReadOnly,
        Pooling = false
    };
    using (var connection = new SqliteConnection(builder.ToString()))
    {
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM Samples;";
        sampleCount = Convert.ToInt64(command.ExecuteScalar());
    }

    var repository = new JsonProjectRepository();
    var projectResult = repository.Load(projectFile!);
    if (!projectResult.Ok || projectResult.Value is null || projectResult.Value.Data.Count == 0)
    {
        foreach (var candidate in projectFiles.Skip(1))
        {
            var candidateResult = repository.Load(candidate);
            if (!candidateResult.Ok || candidateResult.Value is null || candidateResult.Value.Data.Count == 0)
                continue;

            projectFile = candidate;
            projectResult = candidateResult;
            break;
        }

        if (!projectResult.Ok || projectResult.Value is null || projectResult.Value.Data.Count == 0)
        {
            Console.Error.WriteLine($"Kein lesbares Projekt mit Haltungen gefunden: {projectResult.ErrorMessage}");
            return 6;
        }
    }

    Console.WriteLine("Restore-Pruefung: ERFOLG");
    Console.WriteLine($"Projekt: {projectFile}");
    Console.WriteLine($"Haltungen: {projectResult.Value.Data.Count:N0}");
    Console.WriteLine($"KB-Beispiele: {sampleCount:N0}");
    Console.WriteLine($"Einstellungen: {settings}");
    Console.WriteLine($"Programm: {programSolution}");
    return 0;
}
