using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AuswertungPro.Next.Application.Common;

public static class AtomicTextFileWriter
{
    public static void WriteAllText(string path, string content)
    {
        var write = PrepareWrite(path);
        try
        {
            File.WriteAllText(write.TempPath, content);
            CompleteWrite(write);
        }
        catch
        {
            DeleteTemp(write.TempPath);
            throw;
        }
    }

    public static void WriteAllText(string path, string content, Encoding encoding)
    {
        var write = PrepareWrite(path);
        try
        {
            File.WriteAllText(write.TempPath, content, encoding);
            CompleteWrite(write);
        }
        catch
        {
            DeleteTemp(write.TempPath);
            throw;
        }
    }

    public static async Task WriteAllTextAsync(string path, string content, CancellationToken ct = default)
    {
        var write = PrepareWrite(path);
        try
        {
            await File.WriteAllTextAsync(write.TempPath, content, ct).ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();
            CompleteWrite(write);
        }
        catch
        {
            DeleteTemp(write.TempPath);
            throw;
        }
    }

    private static AtomicWrite PrepareWrite(string path)
    {
        var fullPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(fullPath);
        if (string.IsNullOrWhiteSpace(directory))
            throw new InvalidOperationException("Zielordner fehlt.");

        Directory.CreateDirectory(directory);
        var tempPath = Path.Combine(directory, $".{Path.GetFileName(fullPath)}.{Guid.NewGuid():N}.tmp");
        return new AtomicWrite(fullPath, tempPath);
    }

    private static void CompleteWrite(AtomicWrite write)
    {
        try
        {
            if (File.Exists(write.TargetPath))
            {
                var backupPath = write.TargetPath + ".bak";
                ReplaceExisting(write.TempPath, write.TargetPath, backupPath);
            }
            else
            {
                File.Move(write.TempPath, write.TargetPath);
            }
        }
        finally
        {
            DeleteTemp(write.TempPath);
        }
    }

    private static void ReplaceExisting(string sourcePath, string targetPath, string backupPath)
    {
        try
        {
            File.Replace(sourcePath, targetPath, backupPath, ignoreMetadataErrors: true);
        }
        catch (Exception ex) when (ex is PlatformNotSupportedException || ex is IOException || ex is UnauthorizedAccessException)
        {
            File.Copy(targetPath, backupPath, overwrite: true);
            File.Move(sourcePath, targetPath, overwrite: true);
        }
    }

    private static void DeleteTemp(string tempPath)
        => BestEffort.Try(
            () =>
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            },
            "AtomicTextFileWriter Temp-Datei loeschen");

    private sealed record AtomicWrite(string TargetPath, string TempPath);
}
