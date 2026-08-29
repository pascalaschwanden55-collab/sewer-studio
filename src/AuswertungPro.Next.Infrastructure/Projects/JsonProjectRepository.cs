using System.IO;
using System.Text;
using System.Text.Json;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.Projects;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Infrastructure.Projects;

public sealed class JsonProjectRepository : IProjectRepository
{
    public const int CurrentVersion = 2;

    // Oeffentlich, damit die Content-Signatur (JsonProjectContentSignature) exakt dieselbe
    // Serialisierung nutzt wie der echte Save/Load.
    public static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly IProjectPhotoReferenceNormalizer _photoReferenceNormalizer;

    public JsonProjectRepository()
        : this(new ProjectPhotoReferenceNormalizationService())
    {
    }

    public JsonProjectRepository(IProjectPhotoReferenceNormalizer photoReferenceNormalizer)
    {
        _photoReferenceNormalizer = photoReferenceNormalizer
            ?? throw new ArgumentNullException(nameof(photoReferenceNormalizer));
    }

    // AP-50 Save-Schutz: serialisiert ALLE Speichervorgaenge prozessweit. Sobald Speichern
    // in den Hintergrund wandert, koennen manuelles Speichern, AutoSave und Restore-Point-Kopien
    // sonst gleichzeitig dieselbe projekt.json (und deren .bak) schreiben -> File.Replace-Kollision.
    private static readonly object SaveLock = new();

    public Result<Project> Load(string path)
    {
        try
        {
            if (!File.Exists(path))
                return Result<Project>.Fail("APP-NOTFOUND", $"Datei nicht gefunden: {path}");

            var json = File.ReadAllText(path);
            var project = JsonSerializer.Deserialize<Project>(json, SerializerOptions) ?? new Project();
            if (project.Version > CurrentVersion)
            {
                return Result<Project>.Fail(
                    "APP-VERSION",
                    $"Das Projekt stammt aus einer neueren Programmversion (Projektformat {project.Version}, unterstuetzt bis {CurrentVersion}). " +
                    "Bitte oeffne es mit der neueren SewerStudio-Version. Die Datei wurde nicht veraendert.");
            }

            if (project.Version < CurrentVersion)
            {
                MigrateToCurrentVersion(project);
                project.Dirty = true;
            }

            project.EnsureMetadataDefaults();
            _photoReferenceNormalizer.Normalize(project, path);
            ProjectVideoReferenceNormalizer.Normalize(project, path);

            // Auch beim Laden, nicht nur beim Speichern: Die Auswahlmenues fuehren nur
            // die Begriffe der Norm. Ohne Anhebung zeigte ein Bestandsprojekt dort leer
            // an, bis es einmal gespeichert wurde. Nur die Schreibweise aendert sich,
            // die Herkunft bleibt unangetastet - deshalb wird das Projekt dadurch auch
            // nicht als geaendert markiert.
            ProjectVocabularyNormalizer.Normalize(project);
            return Result<Project>.Success(project);
        }
        catch (Exception ex)
        {
            return Result<Project>.Fail("APP-LOAD", ex.Message);
        }
    }

    private static void MigrateToCurrentVersion(Project project)
    {
        // Version 1 -> 2 braucht keine Feldumbenennung. EnsureMetadataDefaults fuellt
        // die hinzugekommenen Standardfelder nach dem Laden kontrolliert auf.
        project.Version = CurrentVersion;
    }

    public Project DeepCopy(Project source)
    {
        // Serialisieren + zurueck deserialisieren = unabhaengige Tiefenkopie.
        var json = JsonSerializer.Serialize(source, SerializerOptions);
        var copy = JsonSerializer.Deserialize<Project>(json, SerializerOptions) ?? new Project();
        copy.EnsureMetadataDefaults();
        return copy;
    }

    public Result Save(Project project, string path)
    {
        // Serialisierung + atomarer Dateitausch laufen unter dem gemeinsamen Lock, damit sich
        // parallele Speichervorgaenge nie ueberlappen (siehe SaveLock).
        lock (SaveLock)
        {
            return SaveInternal(project, path);
        }
    }

    private Result SaveInternal(Project project, string path)
    {
        string? tempPath = null;
        try
        {
            if (string.IsNullOrWhiteSpace(path))
                return Result.Fail("APP-SAVE", "Speicherpfad ist leer.");

            _photoReferenceNormalizer.Normalize(project, path);
            ProjectVideoReferenceNormalizer.Normalize(project, path);
            ProjectVocabularyNormalizer.Normalize(project);
            project.ModifiedAtUtc = DateTime.UtcNow;
            var json = JsonSerializer.Serialize(project, SerializerOptions);

            var fullPath = Path.GetFullPath(path);
            var directory = Path.GetDirectoryName(fullPath);
            if (string.IsNullOrWhiteSpace(directory))
                return Result.Fail("APP-SAVE", $"Ungültiger Speicherpfad: {path}");

            Directory.CreateDirectory(directory);

            tempPath = Path.Combine(directory, $".{Path.GetFileName(fullPath)}.{Guid.NewGuid():N}.tmp");
            // Erzwungenes Schreiben auf den Datentraeger VOR dem Umbenennen. Das
            // Umbenennen fuehrt NTFS im Journal, den Inhalt nicht — ein
            // Stromausfall dazwischen hinterliess sonst eine projekt.json mit
            // richtigem Namen und leerem Inhalt. Die .bak-Kopie aus File.Replace
            // bleibt als zweites Netz (Codeaudit 2026-08-17).
            WriteDurable(tempPath, json);

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

    /// <summary>
    /// Schreibt die Zwischendatei und leert den Schreibpuffer bis auf den
    /// Datentraeger. Erst danach darf umbenannt werden — sonst ist nur die
    /// Umbenennung dauerhaft, der Inhalt aber nicht.
    /// </summary>
    private static void WriteDurable(string tempPath, string json)
    {
        var bytes = new UTF8Encoding(false).GetBytes(json);
        using var stream = new FileStream(
            tempPath, FileMode.Create, FileAccess.Write, FileShare.None,
            bufferSize: 4096, FileOptions.WriteThrough);
        stream.Write(bytes, 0, bytes.Length);
        stream.Flush(flushToDisk: true);
    }
}
