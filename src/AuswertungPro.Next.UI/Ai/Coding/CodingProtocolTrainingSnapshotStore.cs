using System;
using System.IO;
using AuswertungPro.Next.Application.Common;
using InfraTeacher = AuswertungPro.Next.Infrastructure.Ai.Teacher;

namespace AuswertungPro.Next.UI.Ai.Coding;

public sealed class CodingProtocolTrainingSnapshotStore
{
    private readonly Func<string> _getImagesDirectory;
    private readonly Func<string, bool> _fileExists;
    private readonly Action<string, string, bool> _copyFile;
    private readonly Action<string> _deleteFile;

    public CodingProtocolTrainingSnapshotStore()
        : this(
            InfraTeacher.TeacherAnnotationStore.GetImagesDir,
            File.Exists,
            File.Copy,
            File.Delete)
    {
    }

    public CodingProtocolTrainingSnapshotStore(
        Func<string> getImagesDirectory,
        Func<string, bool> fileExists,
        Action<string, string, bool> copyFile,
        Action<string> deleteFile)
    {
        _getImagesDirectory = getImagesDirectory ?? throw new ArgumentNullException(nameof(getImagesDirectory));
        _fileExists = fileExists ?? throw new ArgumentNullException(nameof(fileExists));
        _copyFile = copyFile ?? throw new ArgumentNullException(nameof(copyFile));
        _deleteFile = deleteFile ?? throw new ArgumentNullException(nameof(deleteFile));
    }

    public string? CopySnapshotToTrainingImages(string? snapshotPath, string annotationId)
    {
        if (string.IsNullOrWhiteSpace(snapshotPath) || !_fileExists(snapshotPath))
            return null;

        var destination = Path.Combine(_getImagesDirectory(), $"mark_{annotationId}.png");
        _copyFile(snapshotPath, destination, true);
        return destination;
    }

    public void DeleteSnapshot(string? snapshotPath)
    {
        if (string.IsNullOrWhiteSpace(snapshotPath))
            return;

        BestEffort.Try(
            () =>
            {
                if (_fileExists(snapshotPath))
                    _deleteFile(snapshotPath);
            },
            "Foto/Snapshot: Temp loeschen");
    }
}
