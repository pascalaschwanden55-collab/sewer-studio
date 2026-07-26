using System.Security.Cryptography;
using AuswertungPro.Next.Application.Ai.Training.ClassMaps;

namespace AuswertungPro.Next.Infrastructure.Ai.Training.ClassMaps;

/// <summary>
/// Liest die versionierte Detect-Klassenkarte und ihre gepruefte Migration ohne Schreibzugriff.
/// </summary>
public sealed class TrainingYoloClassMapFileStore : ITrainingYoloClassMapStore
{
    private readonly string _classMapPath;
    private readonly string _migrationPath;
    private readonly string _vsaManifestPath;

    public TrainingYoloClassMapFileStore(
        string classMapPath,
        string migrationPath,
        string vsaManifestPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(classMapPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(migrationPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(vsaManifestPath);

        _classMapPath = Path.GetFullPath(classMapPath);
        _migrationPath = Path.GetFullPath(migrationPath);
        _vsaManifestPath = Path.GetFullPath(vsaManifestPath);
    }

    public TrainingYoloClassMapSnapshot ReadSnapshot()
    {
        try
        {
            var classMap = TrainingYoloClassMapJsonReader.ReadClassMap(_classMapPath);
            ValidateClassMap(classMap);

            var actualManifestHash = ComputeSha256(_vsaManifestPath);
            if (!string.Equals(
                    classMap.VsaManifestHash,
                    actualManifestHash,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new TrainingYoloClassMapException(
                    "Die Detect-Klassenkarte gehoert nicht zum aktuellen VSA-Katalog. " +
                    $"Karte: {classMap.VsaManifestHash}; Katalog: {actualManifestHash}.");
            }

            var migration = TrainingYoloClassMapJsonReader.ReadMigration(_migrationPath);
            if (migration.Version != classMap.Version
                || migration.TargetClassMapVersion != classMap.Version)
            {
                throw new TrainingYoloClassMapException(
                    $"Die Migration passt nicht zu class_map v{classMap.Version}.");
            }
            if (!string.Equals(
                    migration.TargetClassMap,
                    Path.GetFileName(_classMapPath),
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new TrainingYoloClassMapException(
                    $"Die Migration verweist auf '{migration.TargetClassMap}' statt auf '{Path.GetFileName(_classMapPath)}'.");
            }
            if (!string.Equals(
                    migration.VsaManifestHash,
                    actualManifestHash,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new TrainingYoloClassMapException(
                    "Die Migrationstabelle gehoert nicht zum aktuellen VSA-Katalog.");
            }

            return new TrainingYoloClassMapSnapshot(
                classMap.Version,
                classMap.VsaManifestHash,
                classMap.Classes,
                migration.Mappings,
                migration.SourceHashes,
                migration.ResolutionOrder);
        }
        catch (TrainingYoloClassMapException)
        {
            throw;
        }
        catch (Exception ex) when (ex is IOException
                                   or UnauthorizedAccessException
                                   or System.Text.Json.JsonException
                                   or ArgumentException)
        {
            throw new TrainingYoloClassMapException(
                $"Die YOLO-Detect-Klassenkonfiguration konnte nicht sicher gelesen werden: {ex.Message}",
                ex);
        }
    }

    private static void ValidateClassMap(TrainingYoloClassMapFileDocument classMap)
    {
        var expectedClasses = SupportedClasses(classMap.Version);

        if (classMap.Classes.Count != expectedClasses.Count)
        {
            throw new TrainingYoloClassMapException(
                $"class_map v{classMap.Version} muss genau {expectedClasses.Count} Klassen enthalten.");
        }

        foreach (var expected in expectedClasses)
        {
            if (!classMap.Classes.TryGetValue(expected.Key, out var actualId)
                || actualId != expected.Value)
            {
                throw new TrainingYoloClassMapException(
                    $"Klasse '{expected.Key}' muss die feste ID {expected.Value} haben.");
            }
        }
    }

    private static IReadOnlyDictionary<string, int> SupportedClasses(int version)
    {
        if (version == YoloDetectClassMapV2.Version)
            return YoloDetectClassMapV2.Classes;
        if (version == YoloDetectClassMapV3.Version)
            return YoloDetectClassMapV3.Classes;
        throw new TrainingYoloClassMapException(
            $"Nicht unterstuetzte Detect-Klassenkartenversion {version}; erwartet wird " +
            $"v{YoloDetectClassMapV2.Version} (eingefroren) oder v{YoloDetectClassMapV3.Version}.");
    }

    private static string ComputeSha256(string path)
    {
        if (!File.Exists(path))
            throw new TrainingYoloClassMapException($"VSA-Katalog fehlt: {path}");

        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }
}
