using System.Security.Cryptography;
using System.Text.Json;

namespace CodingReplay;

internal static class ReplayFiles
{
    internal static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
    internal static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    internal static string HashFile(string path)
    {
        using var file = new FileStream(SafePath(path), FileMode.Open, FileAccess.Read, FileShare.Read);
        return Convert.ToHexString(SHA256.HashData(file)).ToLowerInvariant();
    }
    internal static string SafePath(string path)
    {
        var full = Path.GetFullPath(path);
        var root = Path.GetPathRoot(full)!;
        if (full.StartsWith(@"\\", StringComparison.Ordinal) || new DriveInfo(root).DriveType == DriveType.Network)
            throw new IOException("Nur lokale Pfade sind erlaubt.");
        for (string? current = full; current is not null; current = Path.GetDirectoryName(current))
        {
            try
            {
                if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("Verknuepfung im Messpfad: " + current);
            }
            catch (FileNotFoundException) { }
            catch (DirectoryNotFoundException) { }
        }
        return full;
    }
    internal static byte[] Read(string path, long maxBytes = 128 * 1024 * 1024)
    {
        using var file = new FileStream(SafePath(path), FileMode.Open, FileAccess.Read, FileShare.Read);
        if (file.Length > maxBytes) throw new IOException("Datei ueberschreitet die Messgrenze.");
        using var buffer = new MemoryStream();
        file.CopyTo(buffer);
        return buffer.ToArray();
    }
    internal static void WriteNew(string path, byte[] bytes)
    {
        using var file = new FileStream(SafePath(path), FileMode.CreateNew, FileAccess.Write, FileShare.None);
        file.Write(bytes);
        file.Flush(true);
    }
    internal static void WriteJson(string path, object value) => WriteNew(path, JsonSerializer.SerializeToUtf8Bytes(value, Json));
    internal static string NewFolder(string parent, string prefix, params string[] protectedRoots)
    {
        var target = SafePath(parent);
        foreach (var protectedRoot in protectedRoots)
        {
            var p = Path.TrimEndingDirectorySeparator(SafePath(protectedRoot));
            if (target.Equals(p, StringComparison.OrdinalIgnoreCase)
                || target.StartsWith(p + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new IOException("Ausgabe liegt im geschuetzten Quellenordner.");
        }
        var fresh = SafePath(Path.Combine(target, prefix + "-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N")[..8]));
        Directory.CreateDirectory(fresh);
        return fresh;
    }
}
