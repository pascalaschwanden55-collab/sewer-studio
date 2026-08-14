using AuswertungPro.Next.Application.Import;

namespace AuswertungPro.Next.Infrastructure.Import;

/// <summary>
/// Verwaltet Vorbereitung, Veroeffentlichung und Ruecknahme der Dateien
/// eines einzelnen Importlaufs.
/// </summary>
internal sealed class ImportFileStagingSession : IImportFileStagingSession
{
    private const string StagingDirectoryName = ".import-staging";
    private readonly object _sync = new();
    private readonly ImportFileStagingPathGuard _paths;
    private readonly string _projectFileDirectory;
    private readonly string _stagingParent;
    private readonly string _stagingDirectory;
    private readonly Dictionary<string, StagedFile> _filesByTarget =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly List<StagedFile> _stagedFiles = [];
    private readonly HashSet<string> _createdDirectories =
        new(StringComparer.OrdinalIgnoreCase);
    private SessionState _state;

    public ImportFileStagingSession(string projectRoot, string projectFileDirectory)
    {
        _paths = new ImportFileStagingPathGuard(projectRoot);
        _projectFileDirectory = Path.GetFullPath(projectFileDirectory);
        _paths.EnsureWithinProject(_projectFileDirectory, nameof(projectFileDirectory));
        _stagingParent = Path.Combine(_projectFileDirectory, StagingDirectoryName);
        _stagingDirectory = Path.Combine(_stagingParent, Guid.NewGuid().ToString("N"));
    }

    public string ProjectRoot => _paths.ProjectRoot;

    public string StagingRoot => _stagingDirectory;

    public IReadOnlyList<PublishedFileInfo> PreparedFiles
    {
        get
        {
            lock (_sync)
            {
                return _stagedFiles
                    .Select(f => new PublishedFileInfo(
                        Path.GetRelativePath(ProjectRoot, f.TargetPath),
                        f.Sha256))
                    .ToList();
            }
        }
    }

    public IReadOnlyList<PublishedFileInfo> PublishedFiles
    {
        get
        {
            lock (_sync)
            {
                return _stagedFiles
                    .Where(f => f.PublishedBySession)
                    .Select(f => new PublishedFileInfo(
                        Path.GetRelativePath(ProjectRoot, f.TargetPath),
                        f.Sha256))
                    .ToList();
            }
        }
    }

    public string StageCopy(
        string sourcePath,
        string targetDirectory,
        Func<DateTime>? now = null,
        CancellationToken cancellationToken = default)
        => StageCopyAs(
            sourcePath,
            targetDirectory,
            Path.GetFileName(sourcePath),
            now,
            cancellationToken);

    public string StageCopyAs(
        string sourcePath,
        string targetDirectory,
        string targetFileName,
        Func<DateTime>? now = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetFileName);

        lock (_sync)
        {
            EnsureState(SessionState.Open, "Weitere Dateien koennen nicht mehr vorbereitet werden.");
            cancellationToken.ThrowIfCancellationRequested();

            var fullSource = Path.GetFullPath(sourcePath);
            if (!File.Exists(fullSource))
                throw new FileNotFoundException("Quelldatei wurde nicht gefunden.", fullSource);

            var fullTargetDirectory = Path.GetFullPath(targetDirectory);
            _paths.EnsureWithinProject(fullTargetDirectory, nameof(targetDirectory));
            _paths.EnsureNoNestedReparsePoint(fullTargetDirectory);

            var fileName = Path.GetFileName(targetFileName);
            if (string.IsNullOrWhiteSpace(fileName)
                || !fileName.Equals(targetFileName, StringComparison.Ordinal))
            {
                throw new ArgumentException("Quelldatei hat keinen gueltigen Dateinamen.", nameof(sourcePath));
            }

            var targetPath = ResolveTargetPath(
                fullSource,
                fullTargetDirectory,
                fileName,
                now ?? (() => DateTime.Now));
            if (File.Exists(targetPath))
                return targetPath;
            if (_filesByTarget.TryGetValue(targetPath, out var existingStage))
                return existingStage.TargetPath;

            EnsureStagingDirectory();
            var stagePath = Path.Combine(
                _stagingDirectory,
                $"{Guid.NewGuid():N}{Path.GetExtension(fileName)}.stage");
            var sha256 = VerifiedImportFileCopy.CopyToStage(
                fullSource,
                stagePath,
                cancellationToken);
            var stagedFile = new StagedFile(stagePath, targetPath, sha256);
            _filesByTarget.Add(targetPath, stagedFile);
            _stagedFiles.Add(stagedFile);
            return targetPath;
        }
    }

    public string ResolveReadPath(string targetPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetPath);

        lock (_sync)
        {
            var fullTarget = Path.GetFullPath(targetPath);
            _paths.EnsureWithinProject(fullTarget, nameof(targetPath));
            if (_filesByTarget.TryGetValue(fullTarget, out var staged)
                && File.Exists(staged.StagePath))
            {
                return staged.StagePath;
            }

            return fullTarget;
        }
    }

    public IReadOnlyList<ImportReadableFile> EnumerateReadableFiles(
        string targetDirectory,
        string searchPattern,
        SearchOption searchOption)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(searchPattern);
        if (searchOption is not SearchOption.TopDirectoryOnly and not SearchOption.AllDirectories)
            throw new ArgumentOutOfRangeException(nameof(searchOption));

        lock (_sync)
        {
            var fullDirectory = Path.GetFullPath(targetDirectory);
            _paths.EnsureWithinProject(fullDirectory, nameof(targetDirectory));
            _paths.EnsureNoNestedReparsePoint(fullDirectory);

            var result = new Dictionary<string, ImportReadableFile>(StringComparer.OrdinalIgnoreCase);
            if (Directory.Exists(fullDirectory))
            {
                foreach (var path in Directory.EnumerateFiles(fullDirectory, searchPattern, searchOption))
                {
                    var fullPath = Path.GetFullPath(path);
                    result[fullPath] = new ImportReadableFile(fullPath, fullPath);
                }
            }

            foreach (var file in _stagedFiles)
            {
                if (!IsVisibleBelow(file.TargetPath, fullDirectory, searchOption)
                    || !System.IO.Enumeration.FileSystemName.MatchesSimpleExpression(
                        searchPattern,
                        Path.GetFileName(file.TargetPath),
                        ignoreCase: true))
                {
                    continue;
                }

                var readPath = File.Exists(file.StagePath)
                    ? file.StagePath
                    : file.TargetPath;
                result[file.TargetPath] = new ImportReadableFile(file.TargetPath, readPath);
            }

            return result.Values
                .OrderBy(file => file.TargetPath, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }

    public string StageGeneratedFile(
        string preferredTargetPath,
        Action<string> writeStageFile,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(preferredTargetPath);
        ArgumentNullException.ThrowIfNull(writeStageFile);

        lock (_sync)
        {
            EnsureState(SessionState.Open, "Weitere Dateien koennen nicht mehr vorbereitet werden.");
            cancellationToken.ThrowIfCancellationRequested();

            var fullPreferredTarget = Path.GetFullPath(preferredTargetPath);
            _paths.EnsureWithinProject(fullPreferredTarget, nameof(preferredTargetPath));
            var targetDirectory = Path.GetDirectoryName(fullPreferredTarget)
                                  ?? throw new ArgumentException(
                                      "Zieldatei hat keinen gueltigen Ordner.",
                                      nameof(preferredTargetPath));
            _paths.EnsureNoNestedReparsePoint(targetDirectory);

            EnsureStagingDirectory();
            var stagePath = Path.Combine(
                _stagingDirectory,
                $"{Guid.NewGuid():N}{Path.GetExtension(fullPreferredTarget)}");
            try
            {
                writeStageFile(stagePath);
                cancellationToken.ThrowIfCancellationRequested();
                ImportFileStagingPathGuard.EnsureDirectChild(stagePath, _stagingDirectory);
                if (!File.Exists(stagePath))
                    throw new IOException("Der Dateierzeuger hat keine vorbereitete Datei geschrieben.");
                ImportFileStagingPathGuard.EnsureNotReparsePoint(stagePath);

                var sha256 = VerifiedImportFileCopy.ComputeSha256(stagePath);
                var targetPath = ResolveGeneratedTargetPath(fullPreferredTarget, stagePath, sha256);
                if (File.Exists(targetPath)
                    || _filesByTarget.TryGetValue(targetPath, out _))
                {
                    File.Delete(stagePath);
                    return targetPath;
                }

                var stagedFile = new StagedFile(stagePath, targetPath, sha256);
                _filesByTarget.Add(targetPath, stagedFile);
                _stagedFiles.Add(stagedFile);
                return targetPath;
            }
            catch
            {
                try
                {
                    if (File.Exists(stagePath))
                        File.Delete(stagePath);
                }
                catch
                {
                    // Der eigentliche Erzeugungsfehler bleibt die Hauptursache.
                }

                throw;
            }
        }
    }

    public void Publish()
    {
        lock (_sync)
        {
            EnsureState(SessionState.Open, "Dateien wurden bereits veroeffentlicht.");
            try
            {
                foreach (var file in _stagedFiles)
                    PublishOne(file);
                _state = SessionState.Published;
            }
            catch (Exception publishError)
            {
                var rollbackErrors = RollbackPublishedFiles();
                _state = SessionState.Faulted;
                if (rollbackErrors.Count > 0)
                {
                    throw new AggregateException(
                        "Dateiveroeffentlichung ist fehlgeschlagen; die Ruecknahme war unvollstaendig.",
                        new[] { publishError }.Concat(rollbackErrors));
                }

                throw;
            }
        }
    }

    public void Accept()
    {
        lock (_sync)
        {
            EnsureState(SessionState.Published, "Dateien muessen vor der Bestaetigung veroeffentlicht sein.");

            // Der Status wird vor dem Aufraeumen gesetzt. Ein reiner
            // Aufraeumfehler darf bestaetigte Projektdateien nie zuruecknehmen.
            _state = SessionState.Accepted;
            foreach (var file in _stagedFiles)
                file.PublishedBySession = false;
            CleanupStagingDirectory();
        }
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_state == SessionState.Disposed)
                return;

            var errors = new List<Exception>();
            if (_state is SessionState.Published or SessionState.Faulted)
                errors.AddRange(RollbackPublishedFiles());

            try
            {
                CleanupStagingDirectory();
            }
            catch (Exception ex)
            {
                errors.Add(ex);
            }

            _state = SessionState.Disposed;
            if (errors.Count > 0)
            {
                throw new AggregateException(
                    "Import-Datei-Staging konnte nicht vollstaendig aufgeraeumt werden.",
                    errors);
            }
        }
    }

    private string ResolveTargetPath(
        string sourcePath,
        string targetDirectory,
        string fileName,
        Func<DateTime> now)
    {
        var preferred = Path.Combine(targetDirectory, fileName);
        if (CanUseOrReuse(sourcePath, preferred, out var reusable))
            return reusable!;

        var name = Path.GetFileNameWithoutExtension(fileName);
        var extension = Path.GetExtension(fileName);
        var stem = $"{name}_{now():yyyyMMdd_HHmmss}";
        var suffix = 1;
        while (true)
        {
            var candidateName = suffix == 1
                ? stem + extension
                : $"{stem}_{suffix}{extension}";
            var candidate = Path.Combine(targetDirectory, candidateName);
            if (CanUseOrReuse(sourcePath, candidate, out reusable))
                return reusable!;
            suffix++;
        }
    }

    private string ResolveGeneratedTargetPath(
        string preferredTarget,
        string stagePath,
        string sha256)
    {
        if (CanUseOrReuseGenerated(preferredTarget, stagePath, sha256))
            return preferredTarget;

        var directory = Path.GetDirectoryName(preferredTarget)!;
        var stem = Path.GetFileNameWithoutExtension(preferredTarget);
        var extension = Path.GetExtension(preferredTarget);
        for (var suffix = 1; suffix < int.MaxValue; suffix++)
        {
            var candidate = Path.Combine(directory, $"{stem}_{suffix}{extension}");
            if (CanUseOrReuseGenerated(candidate, stagePath, sha256))
                return candidate;
        }

        throw new IOException($"Kein freier Zielname fuer erzeugte Importdatei: {preferredTarget}");
    }

    private bool CanUseOrReuseGenerated(
        string candidate,
        string stagePath,
        string sha256)
    {
        if (File.Exists(candidate))
        {
            ImportFileStagingPathGuard.EnsureNotReparsePoint(candidate);
            return VerifiedImportFileCopy.ComputeSha256(candidate)
                .Equals(sha256, StringComparison.OrdinalIgnoreCase);
        }

        if (_filesByTarget.TryGetValue(candidate, out var staged))
        {
            var readable = File.Exists(staged.StagePath)
                ? staged.StagePath
                : staged.TargetPath;
            return File.Exists(readable)
                   && VerifiedImportFileCopy.ContentsEqual(stagePath, readable);
        }

        return true;
    }

    private static bool IsVisibleBelow(
        string targetPath,
        string directory,
        SearchOption searchOption)
    {
        var relative = Path.GetRelativePath(directory, targetPath);
        if (relative.Equals("..", StringComparison.Ordinal)
            || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            || Path.IsPathRooted(relative))
        {
            return false;
        }

        return searchOption == SearchOption.AllDirectories
               || !relative.Contains(Path.DirectorySeparatorChar)
                  && !relative.Contains(Path.AltDirectorySeparatorChar);
    }

    private bool CanUseOrReuse(
        string sourcePath,
        string candidate,
        out string? usablePath)
    {
        usablePath = null;
        if (File.Exists(candidate))
        {
            ImportFileStagingPathGuard.EnsureNotReparsePoint(candidate);
            if (VerifiedImportFileCopy.ContentsEqual(sourcePath, candidate))
            {
                usablePath = candidate;
                return true;
            }

            return false;
        }

        if (_filesByTarget.TryGetValue(candidate, out var staged))
        {
            if (VerifiedImportFileCopy.ContentsEqual(sourcePath, staged.StagePath))
            {
                usablePath = staged.TargetPath;
                return true;
            }

            return false;
        }

        usablePath = candidate;
        return true;
    }

    private void PublishOne(StagedFile file)
    {
        var targetDirectory = Path.GetDirectoryName(file.TargetPath)
                              ?? throw new IOException($"Zielordner fehlt: {file.TargetPath}");
        _paths.EnsureWithinProject(targetDirectory, nameof(file.TargetPath));
        _paths.EnsureNoNestedReparsePoint(targetDirectory);
        RememberMissingDirectories(targetDirectory);
        Directory.CreateDirectory(targetDirectory);
        _paths.EnsureNoNestedReparsePoint(targetDirectory);

        if (File.Exists(file.TargetPath))
        {
            ImportFileStagingPathGuard.EnsureNotReparsePoint(file.TargetPath);
            if (VerifiedImportFileCopy.ContentsEqual(file.StagePath, file.TargetPath))
            {
                File.Delete(file.StagePath);
                return;
            }

            throw new IOException(
                $"Importziel wurde waehrend des Laufs durch eine andere Datei belegt: {file.TargetPath}");
        }

        File.Move(file.StagePath, file.TargetPath, overwrite: false);
        file.PublishedBySession = true;
    }

    private List<Exception> RollbackPublishedFiles()
    {
        var errors = new List<Exception>();
        foreach (var file in _stagedFiles.AsEnumerable().Reverse())
        {
            if (!file.PublishedBySession)
                continue;

            try
            {
                DeletePublishedFileIfUnchanged(file);
                file.PublishedBySession = false;
            }
            catch (Exception ex)
            {
                errors.Add(ex);
            }
        }

        RemoveCreatedEmptyDirectories(errors);
        return errors;
    }

    private static void DeletePublishedFileIfUnchanged(StagedFile file)
    {
        if (!File.Exists(file.TargetPath))
            return;

        ImportFileStagingPathGuard.EnsureNotReparsePoint(file.TargetPath);
        var currentHash = VerifiedImportFileCopy.ComputeSha256(file.TargetPath);
        if (!currentHash.Equals(file.Sha256, StringComparison.OrdinalIgnoreCase))
        {
            throw new IOException(
                $"Neue Importdatei wurde nach der Veroeffentlichung veraendert und wird nicht geloescht: {file.TargetPath}");
        }

        File.Delete(file.TargetPath);
    }

    private void RememberMissingDirectories(string targetDirectory)
    {
        var current = targetDirectory;
        while (!string.Equals(current, ProjectRoot, StringComparison.OrdinalIgnoreCase)
               && _paths.IsWithinProject(current))
        {
            if (Directory.Exists(current))
                break;
            _createdDirectories.Add(current);
            current = Path.GetDirectoryName(current) ?? ProjectRoot;
        }
    }

    private void RemoveCreatedEmptyDirectories(List<Exception> errors)
    {
        foreach (var directory in _createdDirectories.OrderByDescending(path => path.Length))
        {
            try
            {
                if (Directory.Exists(directory)
                    && !Directory.EnumerateFileSystemEntries(directory).Any())
                {
                    Directory.Delete(directory, recursive: false);
                }
            }
            catch (Exception ex)
            {
                errors.Add(ex);
            }
        }
    }

    private void EnsureStagingDirectory()
    {
        if (Directory.Exists(_stagingDirectory))
            return;

        _paths.EnsureWithinProject(_stagingDirectory, nameof(_stagingDirectory));
        _paths.EnsureNoNestedReparsePoint(_stagingDirectory);
        Directory.CreateDirectory(_stagingDirectory);
        _paths.EnsureNoNestedReparsePoint(_stagingDirectory);
    }

    private void CleanupStagingDirectory()
    {
        if (Directory.Exists(_stagingDirectory))
        {
            _paths.EnsureNoNestedReparsePoint(_stagingDirectory);
            ImportFileStagingPathGuard.EnsureDirectChild(_stagingDirectory, _stagingParent);
            ImportFileStagingPathGuard.EnsureNotReparsePoint(_stagingDirectory);
            foreach (var entry in Directory.EnumerateFileSystemEntries(_stagingDirectory))
            {
                ImportFileStagingPathGuard.EnsureDirectChild(entry, _stagingDirectory);
                ImportFileStagingPathGuard.EnsureNotReparsePoint(entry);
                if ((File.GetAttributes(entry) & FileAttributes.Directory) != 0)
                    throw new IOException($"Unerwarteter Unterordner im Import-Arbeitsordner: {entry}");
                File.Delete(entry);
            }

            Directory.Delete(_stagingDirectory, recursive: false);
        }

        if (Directory.Exists(_stagingParent)
            && !Directory.EnumerateFileSystemEntries(_stagingParent).Any())
        {
            ImportFileStagingPathGuard.EnsureDirectChild(_stagingParent, _projectFileDirectory);
            ImportFileStagingPathGuard.EnsureNotReparsePoint(_stagingParent);
            Directory.Delete(_stagingParent, recursive: false);
        }
    }

    private void EnsureState(SessionState expected, string message)
    {
        if (_state != expected)
            throw new InvalidOperationException(message);
    }

    private sealed class StagedFile(string stagePath, string targetPath, string sha256)
    {
        public string StagePath { get; } = stagePath;
        public string TargetPath { get; } = targetPath;
        public string Sha256 { get; } = sha256;
        public bool PublishedBySession { get; set; }
    }

    private enum SessionState
    {
        Open,
        Published,
        Accepted,
        Faulted,
        Disposed
    }
}
