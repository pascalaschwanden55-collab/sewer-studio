using System.Security.Cryptography;
using System.Text.Json;

namespace DetectReleaseHoldoutPdfExtractor;

internal static class SafeFiles
{
    public static async Task<byte[]> ReadAllBytesLimitedAsync(
        string path,
        long maximumBytes,
        CancellationToken cancellationToken)
    {
        var info = new FileInfo(path);
        if (info.Length <= 0 || info.Length > maximumBytes)
            throw new InvalidDataException("Eine Eingabedatei ist leer oder zu groß.");
        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            128 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        using var memory = new MemoryStream((int)info.Length);
        await stream.CopyToAsync(memory, cancellationToken).ConfigureAwait(false);
        if (memory.Length != info.Length)
            throw new IOException("Eine Eingabedatei wurde während des Lesens verändert.");
        return memory.ToArray();
    }

    public static FileIdentity ReadFileIdentity(string path)
    {
        var info = new FileInfo(path);
        info.Refresh();
        if (!info.Exists || info.Length <= 0)
            throw new FileNotFoundException("Die Quelldatei fehlt oder ist leer.");
        return new FileIdentity(info.Length, info.LastWriteTimeUtc);
    }

    public static void WriteNewFileVerified(string target, byte[] bytes, string expectedSha)
    {
        if (File.Exists(target) || Directory.Exists(target))
            throw new IOException("Ein extrahiertes Zielbild ist bereits vorhanden.");
        using (var stream = new FileStream(
                   target,
                   FileMode.CreateNew,
                   FileAccess.Write,
                   FileShare.None,
                   128 * 1024,
                   FileOptions.WriteThrough))
        {
            stream.Write(bytes);
            stream.Flush(flushToDisk: true);
        }

        var actualSha = Hashing.ComputeFileSha256(target);
        if (!string.Equals(actualSha, expectedSha, StringComparison.Ordinal))
            throw new IOException("Das kopierte Prüfbild ist nicht bytegleich.");
    }

    public static async Task WriteJsonAtomicallyAsync<T>(
        string root,
        string target,
        T value,
        JsonSerializerOptions options,
        CancellationToken cancellationToken)
    {
        PathSafety.RequireInside(root, target, "Prüfbeleg");
        if (File.Exists(target) || Directory.Exists(target))
            throw new IOException("Der Prüfbeleg ist bereits vorhanden.");
        var bytes = JsonSerializer.SerializeToUtf8Bytes(value, options);
        var temp = Path.Combine(root, $".{Path.GetFileName(target)}.{Guid.NewGuid():N}.tmp");
        PathSafety.RequireInside(root, temp, "temporärer Prüfbeleg");
        try
        {
            await using (var stream = new FileStream(
                             temp,
                             FileMode.CreateNew,
                             FileAccess.Write,
                             FileShare.None,
                             128 * 1024,
                             FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await stream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
                stream.Flush(flushToDisk: true);
            }

            File.Move(temp, target);
        }
        finally
        {
            if (File.Exists(temp))
                File.Delete(temp);
        }
    }

    public static void TryDeleteOwnedWorkDirectory(string outputRoot, string workRoot)
    {
        try
        {
            PathSafety.RequireInside(outputRoot, workRoot, "Arbeitsordner");
            if (!Directory.Exists(workRoot))
                return;
            if (!Path.GetFileName(workRoot).StartsWith(".extract_work_", StringComparison.Ordinal))
                return;
            Directory.Delete(workRoot, recursive: true);
        }
        catch
        {
            // Ein abgebrochener Staging-Ordner bleibt sichtbar. Fertige Ausgabe
            // und Quellen werden bei einem Aufräumfehler niemals angetastet.
        }
    }

    public static string SafeLeafName(string? path, string fallback)
    {
        if (string.IsNullOrWhiteSpace(path))
            return fallback;
        try
        {
            var name = Path.GetFileName(path);
            return string.IsNullOrWhiteSpace(name) ? fallback : name;
        }
        catch
        {
            return fallback;
        }
    }

    public static string HidePath(string message, string? path)
    {
        if (string.IsNullOrWhiteSpace(message))
            return "Unbekannter Fehler.";
        if (string.IsNullOrWhiteSpace(path))
            return TrimMessage(message);
        return TrimMessage(message.Replace(path, SafeLeafName(path, "Quelldatei"), StringComparison.OrdinalIgnoreCase));
    }

    public static string TrimMessage(string message)
    {
        var normalized = message.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return normalized.Length <= 800 ? normalized : normalized[..800];
    }
}

internal static class Hashing
{
    public static string Sha256(byte[] bytes)
        => Convert.ToHexStringLower(SHA256.HashData(bytes));

    public static async Task<string> ComputeFileSha256Async(
        string path,
        CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            128 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        return Convert.ToHexStringLower(
            await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false));
    }

    public static string ComputeFileSha256(string path)
    {
        using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            128 * 1024,
            FileOptions.SequentialScan);
        return Convert.ToHexStringLower(SHA256.HashData(stream));
    }
}
