using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;

internal static class AuditRepros
{
    public static void Run(string assemblyFolder, string auditFolder)
    {
        var binaries = Path.GetFullPath(assemblyFolder);
        var workspace = Path.GetFullPath(auditFolder);
        var allowed = Path.GetFullPath("C:/Sewer-Studio_KI_4.5/.tmp/programmaudit-2026-09-06") + Path.DirectorySeparatorChar;
        if (!workspace.StartsWith(allowed, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Kein Audit-Ziel");
        Directory.CreateDirectory(workspace);
        AssemblyLoadContext.Default.Resolving += (_, name) => {
            var path = Path.Combine(binaries, name.Name + ".dll");
            return File.Exists(path) ? AssemblyLoadContext.Default.LoadFromAssemblyPath(path) : null;
        };
        var infra = Assembly.Load("AuswertungPro.Next.Infrastructure");
        var app = Assembly.Load("AuswertungPro.Next.Application");
        var domain = Assembly.Load("AuswertungPro.Next.Domain");
        dynamic repo = Activator.CreateInstance(infra.GetType("AuswertungPro.Next.Infrastructure.Projects.JsonProjectRepository")!)!;
        var results = new List<object>();
        foreach (var content in new[] { "null", "{}", "{broken" })
        {
            var path = Path.Combine(workspace, Guid.NewGuid().ToString("N") + ".json");
            File.WriteAllText(path, content);
            dynamic loaded = repo.Load(path);
            results.Add(new { test = "Projektdatei laden", input = content, ok = (bool)loaded.Ok, holdings = loaded.Value == null ? -1 : (int)loaded.Value.Data.Count });
        }
        dynamic rename = Activator.CreateInstance(app.GetType("AuswertungPro.Next.Application.Common.ShaftRenameFileService")!)!;
        var projectFolder = Path.Combine(workspace, "Projekt");
        Directory.CreateDirectory(projectFolder);
        var project = Path.Combine(projectFolder, "projekt.json");
        File.WriteAllText(project, "{}");
        var recoveryFolder = Path.Combine(workspace, "Recovery");
        Directory.CreateDirectory(recoveryFolder);
        var recoveryProject = Path.Combine(recoveryFolder, "projekt.json");
        File.WriteAllText(recoveryProject, "{broken");
        File.WriteAllText(recoveryProject + ".bak", "null");
        dynamic recovery = Activator.CreateInstance(infra.GetType("AuswertungPro.Next.Infrastructure.Projects.ProjectRecoveryService")!)!;
        object recoveryResult = recovery.TryRecover(recoveryProject, repo);
        results.Add(new { test = "Wiederherstellung mit null-Sicherung", result = recoveryResult, originalStillAtOriginalPath = File.Exists(recoveryProject) });
        var source = Path.Combine(workspace, "Simulierte-Kundenquelle", "123");
        Directory.CreateDirectory(source);
        var sourcePdf = Path.Combine(source, "123.pdf");
        File.WriteAllText(sourcePdf, "Unveraenderliches Testoriginal");
        dynamic shaft = Activator.CreateInstance(domain.GetType("AuswertungPro.Next.Domain.Models.SchachtRecord")!)!;
        shaft.SetFieldValueTechnical("PDF_Path", sourcePdf);
        dynamic renamed = rename.Rename(shaft, "123", "456", project);
        results.Add(new { test = "Schachtumbenennung mit projektfremder Quelle", success = (bool)renamed.Success, sourceFolderStillExists = Directory.Exists(source), originalStillAtOriginalPath = File.Exists(sourcePdf), movedOutsideProject = File.Exists(Path.Combine(workspace, "Simulierte-Kundenquelle", "456", "456.pdf")) });
        var nestedFolder = Path.Combine(projectFolder, "Schaechte_Verteilt", "789", "Sanierung");
        Directory.CreateDirectory(nestedFolder);
        var nestedPdf = Path.Combine(nestedFolder, "789.pdf");
        File.WriteAllText(nestedPdf, "Unterordner-Test");
        dynamic nestedShaft = Activator.CreateInstance(domain.GetType("AuswertungPro.Next.Domain.Models.SchachtRecord")!)!;
        nestedShaft.SetFieldValueTechnical("PDF_Path", nestedPdf);
        dynamic nestedResult = rename.Rename(nestedShaft, "789", "987", project);
        string stored = nestedShaft.GetFieldValue("PDF_Path");
        results.Add(new { test = "Schacht-PDF im Unterordner", success = (bool)nestedResult.Success, savedReferenceExists = File.Exists(stored), actualPdfRemainsUnderOldName = File.Exists(Path.Combine(projectFolder, "Schaechte_Verteilt", "987", "Sanierung", "789.pdf")), storedPath = stored });
        var json = JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(Path.Combine(workspace, "repros.json"), json);
        Console.WriteLine(json);
    }
}
