using System.IO.Compression;
using System.Text.Json;

namespace AuswertungPro.Next.Infrastructure.Import.SchachtPro;

/// <summary>
/// Liest ein .spro-Archiv (ZIP) schreibgeschuetzt ein. Uebernimmt die Schutz-Limits
/// und Pfadregeln des Kotlin-Importers (ProjectImporter.kt):
/// max. 10'000 Eintraege, 200 MB pro Eintrag, 2 GB gesamt, 5 MB Manifest,
/// 20 MB Projekt-JSON; nur 'manifest.json', 'integrity.json' sowie 'projects/', 'photos/', 'logos/';
/// keine absoluten Pfade und keine '..'-/'.'-Segmente (Zip-Slip).
/// Es wird NICHTS auf die Platte extrahiert — Eintraege werden nur als Stream gelesen.
/// </summary>
internal sealed class SchachtProArchiveReader : IDisposable
{
    internal const int SupportedFormatVersion = 3;
    internal const int SupportedDbSchemaVersion = 23;

    private const int MaxEntryCount = 10_000;
    private const long MaxEntrySize = 200L * 1024 * 1024;
    private const long MaxTotalUncompressedSize = 2L * 1024 * 1024 * 1024;
    private const long MaxManifestSize = 5L * 1024 * 1024;
    private const long MaxProjectJsonSize = 20L * 1024 * 1024;

    private static readonly string[] AllowedPathPrefixes = { "projects/", "photos/", "logos/" };

    private readonly ZipArchive _archive;
    private readonly Dictionary<string, ZipArchiveEntry> _entries;

    private SchachtProArchiveReader(ZipArchive archive, Dictionary<string, ZipArchiveEntry> entries)
    {
        _archive = archive;
        _entries = entries;
    }

    /// <summary>
    /// Oeffnet das Archiv und validiert alle Eintragsnamen und Groessen.
    /// Wirft <see cref="SchachtProArchiveException"/> bei Verstoessen.
    /// </summary>
    public static SchachtProArchiveReader Open(string sproPath)
    {
        ZipArchive archive;
        try
        {
            archive = ZipFile.OpenRead(sproPath);
        }
        catch (InvalidDataException ex)
        {
            throw new SchachtProArchiveException(
                "INVALID_ARCHIVE",
                $"Die Datei ist kein gültiges ZIP-Archiv: {Path.GetFileName(sproPath)} ({ex.Message})");
        }

        try
        {
            if (archive.Entries.Count > MaxEntryCount)
            {
                throw new SchachtProArchiveException(
                    "INVALID_ARCHIVE",
                    $"Archiv enthält zu viele Einträge (>{MaxEntryCount}).");
            }

            var entries = new Dictionary<string, ZipArchiveEntry>(StringComparer.Ordinal);
            long total = 0;
            foreach (var entry in archive.Entries)
            {
                if (!IsAllowedArchivePath(entry.FullName))
                {
                    throw new SchachtProArchiveException(
                        "UNSAFE_ENTRY",
                        $"Unerlaubter Pfad im Archiv: {entry.FullName}");
                }
                var name = NormalizeArchivePath(entry.FullName);

                if (name.EndsWith('/') && entry.Length != 0)
                    throw new SchachtProArchiveException("INVALID_ARCHIVE",
                        $"Ein Ordner-Eintrag enthält unerwartete Dateidaten: {name}");

                if (entry.Length > MaxEntrySize)
                {
                    throw new SchachtProArchiveException(
                        "INVALID_ARCHIVE",
                        $"Eintrag '{name}' überschreitet {MaxEntrySize} Bytes.");
                }

                total += entry.Length;
                if (total > MaxTotalUncompressedSize)
                {
                    throw new SchachtProArchiveException(
                        "INVALID_ARCHIVE",
                        $"Archivinhalt überschreitet das Gesamtlimit von {MaxTotalUncompressedSize} Bytes (Zip-Bomb-Schutz).");
                }

                if (!entries.TryAdd(name, entry))
                    throw new SchachtProArchiveException("DUPLICATE_ENTRY",
                        $"Archiv enthält einen mehrfachen Dateipfad: {name}");
            }

            return new SchachtProArchiveReader(archive, entries);
        }
        catch
        {
            archive.Dispose();
            throw;
        }
    }

    /// <summary>Liest und validiert manifest.json inkl. Versions-Guard.</summary>
    public ArchiveManifestDto ReadManifest(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        if (!_entries.TryGetValue("manifest.json", out var entry))
        {
            throw new SchachtProArchiveException(
                "MANIFEST_MISSING",
                "Ungültiges Archiv: manifest.json fehlt.");
        }

        if (entry.Length > MaxManifestSize)
        {
            throw new SchachtProArchiveException(
                "INVALID_ARCHIVE",
                $"manifest.json ist grösser als {MaxManifestSize} Bytes.");
        }

        ArchiveManifestDto? manifest;
        try
        {
            manifest = JsonSerializer.Deserialize<ArchiveManifestDto>(
                ReadEntryText(entry), SchachtProArchiveJson.Options);
        }
        catch (JsonException ex)
        {
            throw new SchachtProArchiveException(
                "MANIFEST_INVALID",
                $"manifest.json ist beschädigt ({ex.Message})");
        }

        if (manifest is null || manifest.Projects is null || manifest.AppVersionName is null)
        {
            throw new SchachtProArchiveException(
                "MANIFEST_INVALID",
                "Manifest unvollständig: Pflichtfelder fehlen.");
        }

        if (manifest.FormatVersion > SupportedFormatVersion)
        {
            throw new SchachtProArchiveException(
                "UNSUPPORTED_VERSION",
                $"Archiv-Version {manifest.FormatVersion} ist neuer als unterstützt ({SupportedFormatVersion}). Bitte SewerStudio aktualisieren.");
        }

        if (manifest.DbSchemaVersion > SupportedDbSchemaVersion)
        {
            throw new SchachtProArchiveException(
                "UNSUPPORTED_VERSION",
                $"Archiv-DB-Schema {manifest.DbSchemaVersion} ist neuer als unterstützt ({SupportedDbSchemaVersion}). Bitte SewerStudio aktualisieren.");
        }

        if (manifest.FormatVersion < 1 || manifest.DbSchemaVersion < 1)
            throw new SchachtProArchiveException("MANIFEST_INVALID", "Ungültige Archiv- oder Datenbankversion.");

        if (manifest.ProjectCount != manifest.Projects.Count)
        {
            throw new SchachtProArchiveException(
                "MANIFEST_INVALID",
                "Manifest widersprüchlich: Projektanzahl stimmt nicht.");
        }

        var exportIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var project in manifest.Projects)
        {
            if (project.ExportId is null || project.Name is null || !IsValidExportId(project.ExportId))
            {
                throw new SchachtProArchiveException(
                    "MANIFEST_INVALID",
                    "Manifest enthält eine ungültige Projekt-ID.");
            }

            if (!exportIds.Add(project.ExportId))
            {
                throw new SchachtProArchiveException(
                    "MANIFEST_INVALID",
                    "Manifest enthält doppelte Projekt-IDs.");
            }
        }

        // Vollstaendig pruefen, bevor der Aufrufer irgendein Projekt oder Foto uebernimmt.
        SchachtProArchiveIntegrity.Validate(_entries, required: manifest.FormatVersion >= 2, ct);
        return manifest;
    }

    /// <summary>
    /// Liest ein Projekt-JSON als Text. Null wenn der Eintrag fehlt.
    /// </summary>
    public string? ReadProjectJson(string exportId)
    {
        var name = $"projects/{exportId}.json";
        if (!_entries.TryGetValue(name, out var entry))
            return null;

        if (entry.Length > MaxProjectJsonSize)
        {
            throw new SchachtProArchiveException(
                "INVALID_ARCHIVE",
                $"Projekt-JSON {exportId} ist grösser als {MaxProjectJsonSize} Bytes.");
        }

        return ReadEntryText(entry);
    }

    /// <summary>
    /// Oeffnet einen Lese-Stream fuer eine gepruefte Archiv-Referenz (z.B. Foto-Pfad
    /// aus einem Protokoll). Null wenn der Eintrag fehlt. Der Aufrufer disposed den Stream.
    /// </summary>
    public Stream? OpenValidatedEntry(string? rawPath, string requiredDir)
    {
        if (string.IsNullOrWhiteSpace(rawPath))
            return null;

        if (!IsAllowedArchivePath(rawPath))
        {
            throw new SchachtProArchiveException(
                "UNSAFE_ENTRY",
                $"Unerlaubte Dateireferenz im Archiv: {rawPath}");
        }
        var normalized = NormalizeArchivePath(rawPath);

        if (!normalized.StartsWith(requiredDir + "/", StringComparison.Ordinal))
        {
            throw new SchachtProArchiveException(
                "UNSAFE_ENTRY",
                $"Dateireferenz liegt nicht unter {requiredDir}/: {rawPath}");
        }

        return _entries.TryGetValue(normalized, out var entry) ? entry.Open() : null;
    }

    /// <summary>
    /// Pfadregeln wie ProjectImporter.isAllowedArchivePath: manifest.json und integrity.json im Root
    /// sowie Pfade unter projects/, photos/, logos/; absolute Pfade und '..'-/'.'-Segmente
    /// werden immer abgelehnt.
    /// </summary>
    internal static bool IsAllowedArchivePath(string rawName)
    {
        var unified = rawName.Replace('\\', '/');
        if (unified.StartsWith('/'))
            return false;
        if (unified.Length >= 2 && unified[1] == ':')
            return false;

        var normalized = unified.TrimStart('/');
        if (normalized.Length == 0)
            return true;

        foreach (var segment in normalized.Split('/'))
        {
            if (segment is ".." or ".")
                return false;
        }

        if (normalized is "manifest.json" or "integrity.json")
            return true;

        foreach (var prefix in AllowedPathPrefixes)
        {
            if (normalized.StartsWith(prefix, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    // Gleiche Trennzeichen-Normalisierung wie ProjectImporter.normalizedArchivePath.
    // Ordner bleiben als solche erkennbar; Pruefsummen beziehen sich nur auf Dateien.
    internal static string NormalizeArchivePath(string rawName)
    {
        var unified = rawName.Replace('\\', '/');
        var normalized = string.Join('/', unified.Split('/', StringSplitOptions.RemoveEmptyEntries));
        return unified.EndsWith('/') ? normalized + "/" : normalized;
    }

    internal static bool IsValidExportId(string value)
    {
        if (value.Length is < 1 or > 128)
            return false;

        foreach (var ch in value)
        {
            if (!char.IsAsciiLetterOrDigit(ch) && ch is not '_' and not '-')
                return false;
        }

        return true;
    }

    private static string ReadEntryText(ZipArchiveEntry entry)
    {
        using var stream = entry.Open();
        using var reader = new StreamReader(stream, System.Text.Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return reader.ReadToEnd();
    }

    public void Dispose() => _archive.Dispose();
}

/// <summary>Fachlicher Fehler beim Lesen eines .spro-Archivs (mit Fehlercode).</summary>
internal sealed class SchachtProArchiveException : Exception
{
    internal SchachtProArchiveException(string code, string message)
        : base(message)
    {
        Code = code;
    }

    internal string Code { get; }
}
