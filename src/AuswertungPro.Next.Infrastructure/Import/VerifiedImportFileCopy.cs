using System.Security.Cryptography;

namespace AuswertungPro.Next.Infrastructure.Import;

internal static class VerifiedImportFileCopy
{
    public static string CopyToStage(
        string sourcePath,
        string stagePath,
        CancellationToken cancellationToken)
    {
        var sourceInfo = new FileInfo(sourcePath);
        var initialLength = sourceInfo.Length;
        var initialWriteTime = sourceInfo.LastWriteTimeUtc;
        try
        {
            var sourceHash = CopyAndHash(
                sourcePath,
                stagePath,
                cancellationToken);
            sourceInfo.Refresh();
            if (sourceInfo.Length != initialLength
                || sourceInfo.LastWriteTimeUtc != initialWriteTime)
            {
                throw new IOException(
                    $"Quelldatei wurde waehrend des Kopierens veraendert: {sourcePath}");
            }

            var stageHash = ComputeSha256(stagePath);
            if (!sourceHash.Equals(stageHash, StringComparison.OrdinalIgnoreCase))
                throw new IOException($"Pruefsumme der vorbereiteten Kopie stimmt nicht: {sourcePath}");
            return sourceHash;
        }
        catch
        {
            TryDeleteStage(stagePath);
            throw;
        }
    }

    public static bool ContentsEqual(string firstPath, string secondPath)
    {
        var firstInfo = new FileInfo(firstPath);
        var secondInfo = new FileInfo(secondPath);
        return firstInfo.Length == secondInfo.Length
               && ComputeSha256(firstPath).Equals(
                   ComputeSha256(secondPath),
                   StringComparison.OrdinalIgnoreCase);
    }

    public static string ComputeSha256(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexStringLower(SHA256.HashData(stream));
    }

    private static string CopyAndHash(
        string sourcePath,
        string stagePath,
        CancellationToken cancellationToken)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        using var source = new FileStream(
            sourcePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 128 * 1024,
            FileOptions.SequentialScan);
        using var target = new FileStream(
            stagePath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 128 * 1024,
            FileOptions.SequentialScan);
        var buffer = new byte[128 * 1024];
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var read = source.Read(buffer, 0, buffer.Length);
            if (read == 0)
                break;
            hash.AppendData(buffer, 0, read);
            target.Write(buffer, 0, read);
        }

        target.Flush(flushToDisk: true);
        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }

    private static void TryDeleteStage(string stagePath)
    {
        try
        {
            if (File.Exists(stagePath))
                File.Delete(stagePath);
        }
        catch
        {
            // Der eigentliche Kopierfehler bleibt die Hauptursache.
        }
    }
}
