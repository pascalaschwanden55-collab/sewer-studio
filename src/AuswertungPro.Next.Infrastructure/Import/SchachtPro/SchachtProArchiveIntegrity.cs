using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;

namespace AuswertungPro.Next.Infrastructure.Import.SchachtPro;

/// <summary>Prueft den SHA-256-Vertrag der SchachtPro-Archive ab Format 2 vor jeder Uebernahme.</summary>
internal static class SchachtProArchiveIntegrity
{
    private const string FileName = "integrity.json";
    private const int MaxIntegrityBytes = 5 * 1024 * 1024;
    private const int MaxEntries = 10_000;

    internal static void Validate(IReadOnlyDictionary<string, ZipArchiveEntry> entries, bool required, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (!entries.TryGetValue(FileName, out var integrity))
        {
            if (required)
                throw new SchachtProArchiveException("INTEGRITY_MISSING", "Prüfsummendatei integrity.json fehlt im SchachtPro-Archiv.");
            return; // Format 1 hat noch keinen verpflichtenden Integritaetsnachweis.
        }

        Dictionary<string, string> expected;
        try
        {
            if (integrity.Length > MaxIntegrityBytes)
                throw Invalid("Die Prüfsummendatei ist grösser als 5 MB.");
            using var input = integrity.Open();
            using var content = new MemoryStream();
            CopyBounded(input, content, MaxIntegrityBytes, ct);
            using var document = JsonDocument.Parse(content.ToArray());
            var root = document.RootElement;
            if (Required(root, "algorithm", JsonValueKind.String).GetString() != "SHA-256")
                throw Invalid("Der Prüfsummenalgorithmus muss SHA-256 sein.");
            expected = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var property in Required(root, "entries", JsonValueKind.Object).EnumerateObject())
            {
                var path = SchachtProArchiveReader.NormalizeArchivePath(property.Name);
                if (path.Length == 0 || path.EndsWith('/') || path == FileName
                    || !SchachtProArchiveReader.IsAllowedArchivePath(property.Name))
                    throw Invalid($"Ungültiger Pfad im Prüfsummennachweis: {property.Name}");
                if (property.Value.ValueKind != JsonValueKind.String)
                    throw Invalid($"Ungültige Prüfsumme für {path}.");
                var hash = property.Value.GetString()!;
                if (hash.Length != 64 || !hash.All(Uri.IsHexDigit))
                    throw Invalid($"Ungültige SHA-256-Prüfsumme für {path}.");
                if (!expected.TryAdd(path, hash) || expected.Count > MaxEntries)
                    throw Invalid("Mehrfache Pfade oder zu viele Einträge im Prüfsummennachweis.");
            }
        }
        catch (Exception ex) when (ex is JsonException or InvalidDataException)
        {
            throw Invalid("Die Prüfsummendatei ist beschädigt oder unlesbar.");
        }

        var files = entries.Where(pair => pair.Key != FileName && !pair.Key.EndsWith('/')).ToArray();
        if (!expected.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(files.Select(pair => pair.Key)))
            throw Mismatch("Dateiliste und Prüfsummennachweis stimmen nicht überein.");
        foreach (var (path, entry) in files)
        {
            ct.ThrowIfCancellationRequested();
            using var stream = entry.Open();
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            var buffer = new byte[64 * 1024];
            long length = 0;
            int read;
            while ((read = stream.Read(buffer)) != 0)
            {
                ct.ThrowIfCancellationRequested();
                length += read;
                if (length > entry.Length)
                    throw Mismatch($"Die Längenangabe für {path} stimmt nicht.");
                hash.AppendData(buffer, 0, read);
            }
            if (length != entry.Length || !Convert.ToHexString(hash.GetHashAndReset())
                    .Equals(expected[path], StringComparison.OrdinalIgnoreCase))
                throw Mismatch($"Die Datei {path} wurde beschädigt oder verändert.");
        }
    }

    private static JsonElement Required(JsonElement root, string name, JsonValueKind kind)
    {
        if (root.ValueKind != JsonValueKind.Object)
            throw Invalid("Die Prüfsummendatei enthält kein JSON-Objekt.");
        var properties = root.EnumerateObject().Where(property => property.Name == name).ToArray();
        if (properties.Length != 1 || properties[0].Value.ValueKind != kind)
            throw Invalid($"Die Angabe {name} fehlt, ist mehrfach vorhanden oder ungültig.");
        return properties[0].Value;
    }

    private static void CopyBounded(Stream input, Stream output, int limit, CancellationToken ct)
    {
        var buffer = new byte[64 * 1024];
        int read;
        while ((read = input.Read(buffer)) != 0)
        {
            ct.ThrowIfCancellationRequested();
            if (output.Length + read > limit)
                throw Invalid("Die Prüfsummendatei ist grösser als 5 MB.");
            output.Write(buffer, 0, read);
        }
    }

    private static SchachtProArchiveException Invalid(string message) => new("INTEGRITY_INVALID", message);
    private static SchachtProArchiveException Mismatch(string message) => new("INTEGRITY_MISMATCH", message);
}
