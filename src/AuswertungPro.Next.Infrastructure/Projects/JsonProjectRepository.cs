using System.Text.Json;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.Projects;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Infrastructure.Projects;

public sealed class JsonProjectRepository : IProjectRepository
{
    private static readonly JsonSerializerOptions Opt = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public Result<Project> Load(string path)
    {
        try
        {
            if (!File.Exists(path))
                return Result<Project>.Fail("APP-NOTFOUND", $"Datei nicht gefunden: {path}");

            var json = File.ReadAllText(path);
            var project = JsonSerializer.Deserialize<Project>(json, Opt) ?? new Project();
            project.EnsureMetadataDefaults();
            return Result<Project>.Success(project);
        }
        catch (Exception ex)
        {
            return Result<Project>.Fail("APP-LOAD", ex.Message);
        }
    }

    public Result Save(Project project, string path)
    {
        string? tempPath = null;
        try
        {
            if (string.IsNullOrWhiteSpace(path))
                return Result.Fail("APP-SAVE", "Speicherpfad ist leer.");

            project.ModifiedAtUtc = DateTime.UtcNow;
            var json = JsonSerializer.Serialize(project, Opt);

            var fullPath = Path.GetFullPath(path);
            var directory = Path.GetDirectoryName(fullPath);
            if (string.IsNullOrWhiteSpace(directory))
                return Result.Fail("APP-SAVE", $"Ungültiger Speicherpfad: {path}");

            Directory.CreateDirectory(directory);

            tempPath = Path.Combine(directory, $".{Path.GetFileName(fullPath)}.{Guid.NewGuid():N}.tmp");
            File.WriteAllText(tempPath, json);

            if (File.Exists(fullPath))
            {
                var backupPath = fullPath + ".bak";
                try
                {
                    File.Replace(tempPath, fullPath, backupPath, ignoreMetadataErrors: true);
                }
                catch (Exception ex) when (ex is PlatformNotSupportedException || ex is IOException || ex is UnauthorizedAccessException)
                {
                    // Fallback when atomic replace is not available in this environment.
                    File.Copy(fullPath, backupPath, overwrite: true);
                    File.Move(tempPath, fullPath, overwrite: true);
                }
            }
            else
            {
                File.Move(tempPath, fullPath, overwrite: false);
            }

            project.Dirty = false;
            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Fail("APP-SAVE", ex.Message);
        }
        finally
        {
            if (!string.IsNullOrWhiteSpace(tempPath) && File.Exists(tempPath))
            {
                try { File.Delete(tempPath); } catch { /* best effort cleanup */ }
            }
        }
    }
}
