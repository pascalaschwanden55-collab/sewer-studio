using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace AuswertungPro.Next.Infrastructure.Backup;

/// <summary>
/// Findet den externen Spiegel unabhängig vom wechselnden Laufwerksbuchstaben.
/// </summary>
internal sealed class KnowledgeMirrorTargetResolver
{
    public const string DefaultVolumeLabel = "Elements";
    public const string DefaultTargetFolder = "Brain";

    private readonly string _volumeLabel;
    private readonly string _targetFolder;
    private readonly Func<IReadOnlyList<DriveInfo>> _drives;

    public KnowledgeMirrorTargetResolver(
        string volumeLabel = DefaultVolumeLabel,
        string targetFolder = DefaultTargetFolder)
        : this(
            volumeLabel,
            targetFolder,
            () => DriveInfo.GetDrives().OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase).ToArray())
    {
    }

    internal KnowledgeMirrorTargetResolver(
        string volumeLabel,
        string targetFolder,
        Func<IReadOnlyList<DriveInfo>> drives)
    {
        if (string.IsNullOrWhiteSpace(volumeLabel))
            throw new ArgumentException("Datenträgername fehlt.", nameof(volumeLabel));
        if (string.IsNullOrWhiteSpace(targetFolder))
            throw new ArgumentException("Zielordner fehlt.", nameof(targetFolder));

        _volumeLabel = volumeLabel.Trim();
        _targetFolder = targetFolder.Trim();
        _drives = drives ?? throw new ArgumentNullException(nameof(drives));
    }

    public string? Resolve()
    {
        var matches = new List<DriveInfo>();
        foreach (var drive in _drives())
        {
            try
            {
                if (drive.IsReady
                    && string.Equals(drive.VolumeLabel, _volumeLabel, StringComparison.OrdinalIgnoreCase))
                {
                    matches.Add(drive);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Ein gerade abgezogenes Laufwerk ist kein Programmfehler.
            }
        }

        if (matches.Count == 0)
            return null;
        if (matches.Count > 1)
        {
            throw new IOException(
                $"Mehrere angeschlossene Datenträger heißen \"{_volumeLabel}\". " +
                "Der KI-Spiegel wurde aus Sicherheitsgründen nicht gestartet.");
        }

        return Path.GetFullPath(Path.Combine(matches[0].RootDirectory.FullName, _targetFolder));
    }
}
