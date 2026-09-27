using System.Text.Json;
using AuswertungPro.Next.Application.Backup;
using AuswertungPro.Next.Infrastructure.Backup;
using AuswertungPro.Next.Infrastructure.Projects;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Application.Common;

var fixture = Path.GetFullPath(args[1]);
if (!fixture.StartsWith(@"C:\Sewer-Studio_KI_5.0\.tmp\audit-gesamt-2026-09-23\dateien\", StringComparison.OrdinalIgnoreCase))
    throw new InvalidOperationException("Nur eigene Testdaten erlaubt.");
string P(string rel) => Path.Combine(fixture, rel);
if (args[0] == "saveas")
{
    Directory.CreateDirectory(P("alt/Videos"));
    Directory.CreateDirectory(P("neu"));
    File.WriteAllText(P("alt/Videos/beleg.mp4"), "KORREKTER-KUNDENBELEG");
    var holding = new HaltungRecord();
    holding.Fields[FieldKeys.HoldingName] = "H1";
    holding.Fields[FieldKeys.Link] = "Videos/beleg.mp4";
    var project = new Project { Name = "Synthetic SaveAs" };
    project.Data.Add(holding);
    var repo = new JsonProjectRepository();
    var savedOld = repo.Save(project, P("alt/projekt.json"));
    var savedNew = repo.Save(project, P("neu/projekt.json"));
    var loaded = repo.Load(P("neu/projekt.json"));
    var link = loaded.Value!.Data[0].GetFieldValue(FieldKeys.Link);
    var evidenceSave = new { OldSaveOk = savedOld.Ok, NewSaveOk = savedNew.Ok, NewLoadOk = loaded.Ok,
        RelativeLink = link, OriginalStillExists = File.Exists(P("alt/"+link)), NewResolvedPath = P("neu/"+link),
        NewResolvedExists = File.Exists(P("neu/"+link)) };
    var json = JsonSerializer.Serialize(evidenceSave, new JsonSerializerOptions { WriteIndented = true });
    Console.WriteLine(json);
    File.WriteAllText(P("saveas-result.json"), json);
    return;
}
if (args[0] == "failedsave")
{
    Directory.CreateDirectory(P("alt/Originalfotos"));
    Directory.CreateDirectory(P("neu/Fotos/Haltungen/H1"));
    Directory.CreateDirectory(P("neu/gesperrt.json"));
    File.WriteAllText(P("alt/Originalfotos/beleg.jpg"), "KORREKTER-KUNDENBELEG");
    File.WriteAllText(P("neu/Fotos/Haltungen/H1/beleg.jpg"), "ANDERER-BELEG");
    var holding = new HaltungRecord();
    holding.Fields[FieldKeys.HoldingName] = "H1";
    var finding = new VsaFinding { FotoPath = "Originalfotos/beleg.jpg" };
    holding.VsaFindings.Add(finding);
    var project = new Project { Name = "Synthetic failed SaveAs" };
    project.Data.Add(holding);
    var repo = new JsonProjectRepository();
    var savedOld = repo.Save(project, P("alt/projekt.json"));
    var before = finding.FotoPath;
    var savedNew = repo.Save(project, P("neu/gesperrt.json"));
    var after = finding.FotoPath;
    var evidenceFail = new { OldSaveOk = savedOld.Ok, NewSaveOk = savedNew.Ok, savedNew.ErrorMessage,
        BeforePhoto = before, AfterPhoto = after, ChangedDespiteFailure = before != after,
        BeforeOriginalStillExists = File.Exists(P("alt/"+before)), AfterInOriginalProjectExists = File.Exists(P("alt/"+after)) };
    var json = JsonSerializer.Serialize(evidenceFail, new JsonSerializerOptions { WriteIndented = true });
    Console.WriteLine(json);
    File.WriteAllText(P("failedsave-result.json"), json);
    return;
}
if (args[0] == "first")
{
    foreach (var dir in new[] { "quelle/Medien", "brain", "local", "roaming", "legacy", "desktop", "ziel" }) Directory.CreateDirectory(P(dir));
    File.WriteAllText(P("quelle/Medien/alt.txt"), "EINZIGE-ALTE-SICHERUNG");
}
var sources = new FullBackupSources(P("quelle"), P("brain"), P("local"), P("roaming"), P("legacy"), P("desktop"), "audit-synthetisch", new Dictionary<string,string>());
var service = new FullBackupService(() => sources, availableBytes: _ => long.MaxValue);
var result = await service.RunAsync(P("ziel"));
var root = P("ziel/" + BackupPlanBuilder.TargetFolderName);
var copied = Path.Combine(root, "Programm/Medien/alt.txt");
var matching = Directory.Exists(root) ? Directory.EnumerateFiles(root, "alt.txt", SearchOption.AllDirectories).ToArray() : [];
var evidence = new { Phase = args[0], result.Success, result.Error, result.FilesDeleted, result.SkippedFileTotal, result.SkippedFiles,
    MirrorExists = File.Exists(copied), AllBackupMatches = matching, OriginalStillExists = File.Exists(P("quelle/Medien/alt.txt")),
    MaxVersions = BackupVersionRetention.MaxStaende, ManifestExists = File.Exists(Path.Combine(root, "manifest.json")) };
Console.WriteLine(JsonSerializer.Serialize(evidence, new JsonSerializerOptions { WriteIndented = true }));
File.WriteAllText(P(args[0] + "-result.json"), JsonSerializer.Serialize(evidence, new JsonSerializerOptions { WriteIndented = true }));
