using System.IO.Compression;
using System.Text.Json;

namespace AuswertungPro.Next.Infrastructure.Import.SchachtPro;

/// <summary>
/// Liest ein .spro-Archiv (ZIP) schreibgeschuetzt ein. Uebernimmt die Schutz-Limits
/// und Pfadregeln des Kotlin-Importers (ProjectImporter.kt):
/// max. 10'000 Eintraege, 200 MB pro Eintrag, 2 GB gesamt, 5 MB Manifest,
/// 20 MB Projekt-JSON; nur 'manifest.json' sowie 'projects/', 'photos/', 'logos/';
/// keine absoluten Pfade und keine '..'-/'.'-Segmente (Zip-Slip).
/// Es wird NICHTS auf die Platte extrahiert — Eintraege werden nur als Stream gelesen.
/// </summary>
internal sealed class SchachtProArchiveReader : IDisposable
{
    internal const int SupportedFormatVersion = 1;
    internal const int SupportedDbSchemaVersion = 21;

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
                $"Die Datei ist kein gueltiges ZIP-Archiv: {Path.GetFileName(sproPath)} ({ex.Message})");
        }

        try
        {
            if (archive.Entries.Count > MaxEntryCount)
            {
                throw new SchachtProArchiveException(
                    "INVALID_ARCHIVE",
                    $"Archiv enthaelt zu viele Eintraege (>{MaxEntryCount}).");
            }

            var entries = new Dictionary<string, ZipArchiveEntry>(StringComparer.Ordinal);
            long total = 0;
            foreach (var entry in archive.Entries)
            {
                var name = entry.FullName.Replace('\\', '/');
                if (!IsAllowedArchivePath(name))
                {
                    throw new SchachtProArchiveException(
                        "UNSAFE_ENTRY",
                        $"Unerlaubter Pfad im Archiv: {entry.FullName}");
                }

                if (entry.Length > MaxEntrySize)
                {
                    throw new SchachtProArchiveException(
                        "INVALID_ARCHIVE",
                        $"Eintrag '{name}' ueberschreitet {MaxEntrySize} Bytes.");
                }

                total += entry.Length;
                if (total > MaxTotalUncompressedSize)
                {
                    throw new SchachtProArchiveException(
                        "INVALID_ARCHIVE",
                        $"Archivinhalt ueberschreitet das Gesamtlimit von {MaxTotalUncompressedSize} Bytes (Zip-Bomb-Schutz).");
                }

                // Duplikate: der erste Eintrag gewinnt (deterministisch, kein Fehler).
                entries.TryAdd(name, entry);
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
    public ArchiveManifestDto ReadManifest()
    {
        if (!_entries.TryGetValue("manifest.json", out var entry))
        {
            throw new SchachtProArchiveException(
                "MANIFEST_MISSING",
                "Ungueltiges Archiv: manifest.json fehlt.");
        }

        if (entry.Length > MaxManifestSize)
        {
            throw new SchachtProArchiveException(
                "INVALID_ARCHIVE",
                $"manifest.json ist groesser als {MaxManifestSize} Bytes.");
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
                $"manifest.json ist beschaedigt ({ex.Message})");
        }

        if (manifest is null || manifest.Projects is null || manifest.AppVersionName is null)
        {
            throw new SchachtProArchiveException(
                "MANIFEST_INVALID",
                "Manifest unvollstaendig: Pflichtfelder fehlen.");
        }

        if (manifest.FormatVersion > SupportedFormatVersion)
        {
            throw new SchachtProArchiveException(
                "UNSUPPORTED_VERSION",
                $"Archiv-Version {manifest.FormatVersion} ist neuer als unterstuetzt ({SupportedFormatVersion}). Bitte SewerStudio aktualisieren.");
        }

        if (manifest.DbSchemaVersion > SupportedDbSchemaVersion)
        {
            throw new SchachtProArchiveException(
                "UNSUPPORTED_VERSION",
                $"Archiv-DB-Schema {manifest.DbSchemaVersion} ist neuer als unterstuetzt ({SupportedDbSchemaVersion}). Bitte SewerStudio aktualisieren.");
        }

        if (manifest.ProjectCount != manifest.Projects.Count)
        {
            throw new SchachtProArchiveException(
                "MANIFEST_INVALID",
                "Manifest widerspruechlich: Projektanzahl stimmt nicht.");
        }

        var exportIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var project in manifest.Projects)
        {
            if (project.ExportId is null || project.Name is null || !IsValidExportId(project.ExportId))
            {
                throw new SchachtProArchiveException(
                    "MANIFEST_INVALID",
                    "Manifest enthaelt eine ungueltige Projekt-ID.");
            }

            if (!exportIds.Add(project.ExportId))
            {
                throw new SchachtProArchiveException(
                    "MANIFEST_INVALID",
                    "Manifest enthaelt doppelte Projekt-IDs.");
            }
        }

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
                $"Projekt-JSON {exportId} ist groesser als {MaxProjectJsonSize} Bytes.");
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

        var normalized = rawPath.Replace('\\', '/').TrimStart('/');
        if (!IsAllowedArchivePath(normalized))
        {
            throw new SchachtProArchiveException(
                "UNSAFE_ENTRY",
                $"Unerlaubte Dateireferenz im Archiv: {rawPath}");
        }

        if (!normalized.StartsWith(requiredDir + "/", StringComparison.Ordinal))
        {
            throw new SchachtProArchiveException(
                "UNSAFE_ENTRY",
                $"Dateireferenz liegt nicht unter {requiredDir}/: {rawPath}");
        }

        return _entries.TryGetValue(normalized, out var entry) ? entry.Open() : null;
    }

    /// <summary>
    /// Pfadregeln wie ProjectImporter.isAllowedArchivePath: nur manifest.json im Root
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

        if (string.Equals(normalized, "manifest.json", StringComparison.Ordinal))
            return true;

        foreach (var prefix in AllowedPathPrefixes)
        {
            if (normalized.StartsWith(prefix, StringComparison.Ordinal))
                return true;
        }

        return false;
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
