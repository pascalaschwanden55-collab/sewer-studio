using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Infrastructure.Backup;

namespace AuswertungPro.Next.Infrastructure.Ai.Training;

/// <summary>Sichere Kopier-, Hash- und Pfadregeln fuer die Gold-Gehirn-Trennung.</summary>
internal static class PersonalGoldBrainFileService
{
    public static string NormalizeRoot(string path, string name)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException($"{name} fehlt.");

        return Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
    }

    public static void EnsureDifferentRoots(params string[] roots)
    {
        var normalized = roots
            .Select(root => NormalizeRoot(root, "Pfad"))
            .ToArray();
        for (var left = 0; left < normalized.Length; left++)
        {
            for (var right = left + 1; right < normalized.Length; right++)
            {
                if (string.Equals(normalized[left], normalized[right], StringComparison.OrdinalIgnoreCase)
                    || IsInside(normalized[left], normalized[right])
                    || IsInside(normalized[right], normalized[left]))
                {
                    throw new InvalidDataException(
                        $"Archiv-, Quell- und Arbeitsordner duerfen sich nicht ueberlappen: " +
                        $"{normalized[left]} / {normalized[right]}");
                }
            }
        }
    }

    public static void EnsureSameVolume(string source, string target, string description)
    {
        var sourceRoot = Path.GetPathRoot(source);
        var targetRoot = Path.GetPathRoot(target);
        if (!string.Equals(sourceRoot, targetRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                $"{description} muss auf demselben Laufwerk liegen, damit das Umschalten atomar bleibt.");
        }
    }

    public static void EnsureNoReparsePoint(string path, string stopRoot)
    {
        var root = NormalizeRoot(stopRoot, "Schutzwurzel");
        var current = Path.GetFullPath(path);
        while (true)
        {
            if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException($"Verknuepfung im geschuetzten Pfad: {current}");
            if (string.Equals(current, root, StringComparison.OrdinalIgnoreCase))
                return;

            current = Path.GetDirectoryName(current)
                      ?? throw new InvalidDataException($"Pfad liegt ausserhalb der Schutzwurzel: {path}");
            if (!IsInside(root, current)
                && !string.Equals(current, root, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException($"Pfad liegt ausserhalb der Schutzwurzel: {path}");
            }
        }
    }

    public static void EnsureMutationPathIsSafe(string safetyRoot, string path)
        => BackupTargetPathGuard.EnsurePathIsSafe(safetyRoot, path);

    public static void EnsureMutationTreeIsSafe(string path)
        => BackupTargetPathGuard.EnsureTreeIsSafe(path);

    internal static void EnsureMutationPathIsSafe(
        string safetyRoot,
        string path,
        Func<string, FileAttributes?> readAttributes)
        => BackupTargetPathGuard.EnsurePathIsSafe(safetyRoot, path, readAttributes);

    public static void CreateDirectorySafe(string safetyRoot, string path)
    {
        EnsureMutationPathIsSafe(safetyRoot, path);
        Directory.CreateDirectory(path);
        EnsureMutationPathIsSafe(safetyRoot, path);
    }

    public static void MoveDirectorySafe(
        string sourceSafetyRoot,
        string source,
        string targetSafetyRoot,
        string target)
    {
        EnsureMutationPathIsSafe(sourceSafetyRoot, source);
        EnsureMutationPathIsSafe(targetSafetyRoot, target);
        Directory.Move(source, target);
    }

    public static void MoveFileSafe(
        string sourceSafetyRoot,
        string source,
        string targetSafetyRoot,
        string target)
    {
        EnsureMutationPathIsSafe(sourceSafetyRoot, source);
        EnsureMutationPathIsSafe(targetSafetyRoot, target);
        File.Move(source, target);
    }

    public static async Task CopyVerifiedAsync(
        string source,
        string target,
        CancellationToken cancellationToken)
        => await CopyVerifiedAsync(
                source,
                target,
                FindExistingRoot(target),
                cancellationToken)
            .ConfigureAwait(false);

    public static async Task CopyVerifiedAsync(
        string source,
        string target,
        string targetSafetyRoot,
        CancellationToken cancellationToken)
    {
        EnsureNoReparsePoint(source, FindExistingRoot(source));
        var targetDirectory = Path.GetDirectoryName(target)
                              ?? throw new InvalidDataException($"Zieldatei besitzt keinen Ordner: {target}");
        CreateDirectorySafe(targetSafetyRoot, targetDirectory);
        var temporary = target + ".tmp";
        try
        {
            EnsureMutationPathIsSafe(targetSafetyRoot, target);
            EnsureMutationPathIsSafe(targetSafetyRoot, temporary);
            if (File.Exists(temporary) || Directory.Exists(temporary))
                throw new IOException($"Temporaeres Kopierziel existiert bereits: {temporary}");
            await using (var input = new FileStream(
                             source,
                             FileMode.Open,
                             FileAccess.Read,
                             FileShare.Read,
                             1024 * 1024,
                             FileOptions.Asynchronous | FileOptions.SequentialScan))
            await using (var output = new FileStream(
                             temporary,
                             FileMode.CreateNew,
                             FileAccess.Write,
                             FileShare.None,
                             1024 * 1024,
                             FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                await input.CopyToAsync(output, cancellationToken).ConfigureAwait(false);
                await output.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            var sourceHash = await HashAsync(source, cancellationToken).ConfigureAwait(false);
            var targetHash = await HashAsync(temporary, cancellationToken).ConfigureAwait(false);
            if (!sourceHash.Equals(targetHash, StringComparison.OrdinalIgnoreCase))
                throw new IOException($"Pruefsumme stimmt nach dem Kopieren nicht: {source}");

            EnsureMutationPathIsSafe(targetSafetyRoot, temporary);
            EnsureMutationPathIsSafe(targetSafetyRoot, target);
            File.Move(temporary, target, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary))
            {
                EnsureMutationPathIsSafe(targetSafetyRoot, temporary);
                File.Delete(temporary);
            }
        }
    }

    public static async Task CopyTreeVerifiedAsync(
        string sourceRoot,
        string targetRoot,
        CancellationToken cancellationToken)
        => await CopyTreeVerifiedAsync(
                sourceRoot,
                targetRoot,
                FindExistingRoot(targetRoot),
                cancellationToken)
            .ConfigureAwait(false);

    public static async Task CopyTreeVerifiedAsync(
        string sourceRoot,
        string targetRoot,
        string targetSafetyRoot,
        CancellationToken cancellationToken)
    {
        if (!Directory.Exists(sourceRoot))
            return;

        EnsureNoReparsePoint(sourceRoot, sourceRoot);
        var stack = new Stack<(string Source, string Target)>();
        stack.Push((sourceRoot, targetRoot));
        while (stack.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var current = stack.Pop();
            CreateDirectorySafe(targetSafetyRoot, current.Target);
            foreach (var entry in Directory.EnumerateFileSystemEntries(current.Source))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if ((File.GetAttributes(entry) & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidDataException($"Verknuepfung im zu kopierenden Ordner: {entry}");

                var target = Path.Combine(current.Target, Path.GetFileName(entry));
                if (Directory.Exists(entry))
                    stack.Push((entry, target));
                else
                    await CopyVerifiedAsync(entry, target, targetSafetyRoot, cancellationToken)
                        .ConfigureAwait(false);
            }
        }
    }

    public static async Task<string> HashAsync(
        string path,
        CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            1024 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
        return Convert.ToHexStringLower(hash);
    }

    public static async Task WriteJsonAsync(
        string path,
        object value,
        CancellationToken cancellationToken)
        => await WriteJsonAsync(
                path,
                value,
                FindExistingRoot(path),
                cancellationToken)
            .ConfigureAwait(false);

    public static async Task WriteJsonAsync(
        string path,
        object value,
        string targetSafetyRoot,
        CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(value, JsonDefaults.Indented);
        await WriteTextAsync(path, json, targetSafetyRoot, cancellationToken)
            .ConfigureAwait(false);
    }

    public static async Task WriteTextAsync(
        string path,
        string content,
        string targetSafetyRoot,
        CancellationToken cancellationToken)
    {
        var targetDirectory = Path.GetDirectoryName(path)
                              ?? throw new InvalidDataException($"Zieldatei besitzt keinen Ordner: {path}");
        CreateDirectorySafe(targetSafetyRoot, targetDirectory);
        EnsureMutationPathIsSafe(targetSafetyRoot, path);
        var temporary = path + ".tmp";
        EnsureMutationPathIsSafe(targetSafetyRoot, temporary);
        if (File.Exists(temporary) || Directory.Exists(temporary))
            throw new IOException($"Temporaeres Schreibziel existiert bereits: {temporary}");
        try
        {
            var bytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(content);
            await using (var stream = new FileStream(
                             temporary,
                             FileMode.CreateNew,
                             FileAccess.Write,
                             FileShare.None,
                             64 * 1024,
                             FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await stream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
                stream.Flush(flushToDisk: true);
            }

            EnsureMutationPathIsSafe(targetSafetyRoot, temporary);
            EnsureMutationPathIsSafe(targetSafetyRoot, path);
            File.Move(temporary, path, overwrite: true);
            EnsureMutationPathIsSafe(targetSafetyRoot, path);
        }
        finally
        {
            if (File.Exists(temporary))
            {
                EnsureMutationPathIsSafe(targetSafetyRoot, temporary);
                File.Delete(temporary);
            }
        }
    }

    public static void DeleteFileSafe(string safetyRoot, string path)
    {
        EnsureMutationPathIsSafe(safetyRoot, path);
        if (File.Exists(path))
            File.Delete(path);
    }

    public static void DeleteEmptyDirectorySafe(string safetyRoot, string path)
    {
        EnsureMutationPathIsSafe(safetyRoot, path);
        if (Directory.Exists(path) && !Directory.EnumerateFileSystemEntries(path).Any())
            Directory.Delete(path);
    }

    public static void DeleteTreeSafe(string safetyRoot, string path)
    {
        EnsureMutationPathIsSafe(safetyRoot, path);
        BackupTargetPathGuard.EnsureTreeIsSafe(path);
        Directory.Delete(path, recursive: true);
    }

    public static string FindCommonSafetyRoot(string left, string right)
    {
        var current = NormalizeRoot(left, "Erster Pfad");
        var other = NormalizeRoot(right, "Zweiter Pfad");
        EnsureSameVolume(current, other, "Die Schutzwurzel");
        while (!PathsEqual(current, other) && !IsInside(current, other))
        {
            current = Path.GetDirectoryName(current)
                      ?? throw new InvalidDataException(
                          $"Keine gemeinsame Schutzwurzel gefunden: {left} / {right}");
        }

        return current;
    }

    public static bool IsInside(string root, string path)
    {
        var normalizedRoot = NormalizeRoot(root, "Wurzel");
        var normalizedPath = Path.GetFullPath(path);
        return normalizedPath.StartsWith(
            normalizedRoot + Path.DirectorySeparatorChar,
            StringComparison.OrdinalIgnoreCase);
    }

    public static bool PathsEqual(string left, string right)
        => string.Equals(
            NormalizeRoot(left, "Pfad"),
            NormalizeRoot(right, "Pfad"),
            StringComparison.OrdinalIgnoreCase);

    private static string FindExistingRoot(string path)
    {
        var current = Path.GetFullPath(path);
        while (!Directory.Exists(current))
        {
            current = Path.GetDirectoryName(current)
                      ?? throw new DirectoryNotFoundException($"Kein vorhandener Stamm fuer {path}");
        }
        return current;
    }
}
