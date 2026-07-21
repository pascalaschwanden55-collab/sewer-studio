using System.Text.Json;

namespace AuswertungPro.Next.Infrastructure.Ai.Teacher;

internal static class VsaYoloClassMapDocumentReader
{
    private static readonly HashSet<string> VersionedPropertyNames =
        new(StringComparer.Ordinal)
        {
            "version",
            "vsa_manifest_hash",
            "classes"
        };

    public static VsaYoloClassMapDocument Read(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        using var json = JsonDocument.Parse(File.ReadAllText(path), new JsonDocumentOptions
        {
            AllowTrailingCommas = false,
            CommentHandling = JsonCommentHandling.Disallow
        });

        if (json.RootElement.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException("Die Klassenkarte muss ein JSON-Objekt sein.");

        var properties = json.RootElement.EnumerateObject().ToArray();
        EnsureUniquePropertyNames(properties, "Klassenkarte", StringComparer.Ordinal);

        var isVersioned = properties.Any(property => VersionedPropertyNames.Contains(property.Name));
        var document = isVersioned
            ? ReadVersioned(properties)
            : ReadLegacy(properties);

        VsaYoloClassMapDocumentValidator.Validate(document);
        return document;
    }

    private static VsaYoloClassMapDocument ReadVersioned(IReadOnlyList<JsonProperty> properties)
    {
        var unexpected = properties
            .Where(property => !VersionedPropertyNames.Contains(property.Name))
            .Select(property => property.Name)
            .ToArray();
        if (unexpected.Length > 0)
        {
            throw new InvalidDataException(
                "Unbekannte Felder in der versionierten Klassenkarte: " + string.Join(", ", unexpected));
        }

        var versionElement = GetRequiredProperty(properties, "version");
        if (versionElement.ValueKind != JsonValueKind.Number
            || !versionElement.TryGetInt32(out var version))
            throw new InvalidDataException("'version' muss eine ganze Zahl sein.");

        var hashElement = GetRequiredProperty(properties, "vsa_manifest_hash");
        if (hashElement.ValueKind != JsonValueKind.String)
            throw new InvalidDataException("'vsa_manifest_hash' muss eine Zeichenfolge sein.");

        var classesElement = GetRequiredProperty(properties, "classes");
        if (classesElement.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException("'classes' muss ein Objekt mit Zuordnungen 'Schluessel: ID' sein.");

        return new VsaYoloClassMapDocument(
            VsaYoloClassMapFormat.Versioned,
            ReadClasses(classesElement),
            version,
            hashElement.GetString());
    }

    private static VsaYoloClassMapDocument ReadLegacy(IReadOnlyList<JsonProperty> properties)
    {
        var classes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in properties)
        {
            if (property.Value.ValueKind != JsonValueKind.Number
                || !property.Value.TryGetInt32(out var id))
            {
                throw new InvalidDataException(
                    $"Altformat: Die ID fuer Klasse '{property.Name}' muss eine ganze Zahl sein.");
            }

            if (!classes.TryAdd(property.Name, id))
                throw new InvalidDataException($"Klassenschluessel '{property.Name}' ist mehrfach vorhanden.");
        }

        return new VsaYoloClassMapDocument(VsaYoloClassMapFormat.Legacy, classes);
    }

    private static Dictionary<string, int> ReadClasses(JsonElement classesElement)
    {
        var properties = classesElement.EnumerateObject().ToArray();
        EnsureUniquePropertyNames(properties, "'classes'", StringComparer.OrdinalIgnoreCase);

        var classes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in properties)
        {
            if (property.Value.ValueKind != JsonValueKind.Number
                || !property.Value.TryGetInt32(out var id))
            {
                throw new InvalidDataException(
                    $"Die ID fuer Klasse '{property.Name}' muss eine ganze Zahl sein.");
            }

            classes.Add(property.Name, id);
        }

        return classes;
    }

    private static JsonElement GetRequiredProperty(
        IReadOnlyList<JsonProperty> properties,
        string name)
    {
        foreach (var property in properties)
        {
            if (string.Equals(property.Name, name, StringComparison.Ordinal))
                return property.Value;
        }

        throw new InvalidDataException($"Pflichtfeld '{name}' fehlt.");
    }

    private static void EnsureUniquePropertyNames(
        IReadOnlyList<JsonProperty> properties,
        string location,
        StringComparer comparer)
    {
        var names = new HashSet<string>(comparer);
        foreach (var property in properties)
        {
            if (!names.Add(property.Name))
                throw new InvalidDataException($"Doppeltes Feld '{property.Name}' in {location}.");
        }
    }
}
