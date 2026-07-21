using System.Text.Json;
using System.Text.Json.Serialization;
using AuswertungPro.Next.Application.Common;

namespace AuswertungPro.Next.Infrastructure.Ai.Teacher;

internal static class VsaYoloClassMapDocumentWriter
{
    public static void Write(string mapPath, VsaYoloClassMapDocument document)
    {
        VsaYoloClassMapDocumentValidator.Validate(document);

        var directory = Path.GetDirectoryName(mapPath)
                        ?? throw new InvalidOperationException("Ordner der Klassenkarte fehlt.");
        var classesPath = Path.Combine(directory, "classes.txt");
        var orderedClasses = document.Classes
            .OrderBy(item => item.Value)
            .ThenBy(item => item.Key, StringComparer.Ordinal)
            .ToArray();
        var classesText = BuildClassesText(orderedClasses.Select(item => item.Key));
        var mapText = Serialize(document, orderedClasses);

        // Zwei Dateien lassen sich mit normalen Dateisystemmitteln nicht gemeinsam
        // atomar ersetzen. Deshalb sichern wir die abgeleitete classes.txt und
        // stellen sie wieder her, falls die verbindliche JSON-Karte nicht geschrieben
        // werden kann.
        var classesFileExisted = File.Exists(classesPath);
        var previousClassesText = classesFileExisted
            ? File.ReadAllText(classesPath)
            : null;

        AtomicTextFileWriter.WriteAllText(classesPath, classesText);
        try
        {
            AtomicTextFileWriter.WriteAllText(mapPath, mapText);
        }
        catch (Exception mapWriteError)
        {
            try
            {
                RestoreClassesFile(
                    classesPath,
                    classesFileExisted,
                    previousClassesText);
            }
            catch (Exception restoreError)
            {
                throw new IOException(
                    "YOLO-Klassenkarte konnte nicht geschrieben und classes.txt nicht zurueckgesetzt werden.",
                    new AggregateException(mapWriteError, restoreError));
            }

            throw;
        }
    }

    public static string BuildClassesText(IEnumerable<string> lines)
    {
        var materialized = lines.ToArray();
        return materialized.Length == 0
            ? string.Empty
            : string.Join(Environment.NewLine, materialized) + Environment.NewLine;
    }

    private static string Serialize(
        VsaYoloClassMapDocument document,
        IReadOnlyList<KeyValuePair<string, int>> orderedClasses)
    {
        var classes = orderedClasses.ToDictionary(
            item => item.Key,
            item => item.Value,
            StringComparer.OrdinalIgnoreCase);

        if (document.Format == VsaYoloClassMapFormat.Legacy)
            return JsonSerializer.Serialize(classes, JsonDefaults.Indented);

        var model = new VersionedWriteModel(
            document.Version!.Value,
            document.VsaManifestHash!,
            classes);
        return JsonSerializer.Serialize(model, JsonDefaults.Indented);
    }

    private static void RestoreClassesFile(
        string classesPath,
        bool classesFileExisted,
        string? previousClassesText)
    {
        if (classesFileExisted)
        {
            AtomicTextFileWriter.WriteAllText(classesPath, previousClassesText!);
            return;
        }

        if (File.Exists(classesPath))
            File.Delete(classesPath);
    }

    private sealed record VersionedWriteModel(
        [property: JsonPropertyName("version")] int Version,
        [property: JsonPropertyName("vsa_manifest_hash")] string VsaManifestHash,
        [property: JsonPropertyName("classes")] IReadOnlyDictionary<string, int> Classes);
}
