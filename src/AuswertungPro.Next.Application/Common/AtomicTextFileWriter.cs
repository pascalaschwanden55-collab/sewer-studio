using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AuswertungPro.Next.Application.Common;

public static class AtomicTextFileWriter
{
    public static void Write(string path, Action<TextWriter> write, Encoding? encoding = null)
    {
        ArgumentNullException.ThrowIfNull(write);

        var atomicWrite = PrepareWrite(path);
        try
        {
            using (var writer = encoding is null
                       ? new StreamWriter(atomicWrite.TempPath)
                       : new StreamWriter(atomicWrite.TempPath, append: false, encoding))
            {
                write(writer);
            }

            CompleteWrite(atomicWrite);
        }
        catch
        {
            DeleteTemp(atomicWrite.TempPath);
            throw;
        }
    }

    /// <param name="durable">
    /// Erzwingt vor dem Umbenennen ein echtes Schreiben auf den Datentraeger.
    ///
    /// Ohne das ist nur die HAELFTE atomar: Das Umbenennen fuehrt NTFS im Journal,
    /// den Inhalt nicht. Faellt der Strom zwischen Schreiben und Puffer-Leerung
    /// aus, ueberlebt die Umbenennung — und zurueck bleibt eine Datei mit
    /// richtigem Namen und leerem oder halbem Inhalt (Codeaudit 2026-08-17).
    ///
    /// Ein Programmabsturz ist davon NICHT betroffen; der Puffer gehoert dem
    /// Betriebssystem und ueberlebt ihn. Es geht allein um Stromausfall und
    /// harten Reset.
    ///
    /// Bewusst abschaltbar: Das Leeren des Puffers kostet Zeit. Es gehoert an
    /// die Stellen, an denen die Wiederherstellung spaeter auf den Inhalt baut —
    /// Transaktionsmarker und Projektdatei —, nicht an Berichte und Exporte.
    /// </param>
    public static void WriteAllText(string path, string content, bool durable = false)
    {
        var write = PrepareWrite(path);
        try
        {
            WriteTemp(write.TempPath, content, encoding: null, durable);
            CompleteWrite(write);
        }
        catch
        {
            DeleteTemp(write.TempPath);
            throw;
        }
    }

    /// <summary>
    /// Schreibt die Zwischendatei und leert bei <paramref name="durable"/> den
    /// Schreibpuffer bis auf den Datentraeger.
    /// </summary>
    private static void WriteTemp(string tempPath, string content, Encoding? encoding, bool durable)
    {
        if (!durable)
        {
            if (encoding is null)
                File.WriteAllText(tempPath, content);
            else
                File.WriteAllText(tempPath, content, encoding);
            return;
        }

        var bytes = (encoding ?? new UTF8Encoding(false)).GetBytes(content);
        using var stream = new FileStream(
            tempPath, FileMode.Create, FileAccess.Write, FileShare.None,
            bufferSize: 4096, FileOptions.WriteThrough);
        stream.Write(bytes, 0, bytes.Length);
        // Flush(true) reicht das Leeren bis zum Geraet durch — WriteThrough allein
        // umgeht nur den Zwischenspeicher des Betriebssystems.
        stream.Flush(flushToDisk: true);
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

    public static async Task WriteAllBytesAsync(
        string path,
        ReadOnlyMemory<byte> content,
        CancellationToken ct = default)
    {
        var write = PrepareWrite(path);
        try
        {
            await File.WriteAllBytesAsync(write.TempPath, content.ToArray(), ct).ConfigureAwait(false);
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
        catch (FileNotFoundException)
        {
            // Ziel ist zwischen Existenzpruefung und Replace verschwunden (AV-Scanner, Cloud-Sync,
            // externes Loeschen). Dann genuegt ein einfacher Move ohne Backup-Kopie — der
            // Fallback unten wuerde an File.Copy(targetPath, ...) erneut mit FileNotFound scheitern.
            File.Move(sourcePath, targetPath, overwrite: true);
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
