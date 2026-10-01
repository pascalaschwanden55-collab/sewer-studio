using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.Import;
using AuswertungPro.Next.Infrastructure.Import.Common;

namespace AuswertungPro.Next.Infrastructure.Import.SchachtPro;

/// <summary>Gleiche Schachtordner, Datumsregel und geschuetzte Kopiertransaktion wie die Protokollverteilung.</summary>
internal static class SchachtProQrAblage
{
    internal static string Prepare(string source, ProtocolDto protocol, IImportFileStagingSession staging,
        CancellationToken cancellationToken)
    {
        var name = ProjectPathResolver.SanitizePathSegment(protocol.SchachtNr!.Trim());
        var directory = ProjectStructure.SchachtVerteiltDir(staging.ProjectRoot, name);
        new ProjectWritePathGuard(staging.ProjectRoot).EnsureSafeDirectoryTarget(directory);
        if (File.Exists(directory))
            throw new IOException("Der Schachtordner ist durch eine vorhandene Datei blockiert.");
        var stamp = ImportDateStampResolver.Resolve(protocol.Datum);
        var target = staging.StageCopyAs(source, directory,
            $"{stamp}_{name}_QR{Path.GetExtension(source).ToLowerInvariant()}",
            cancellationToken: cancellationToken);
        return ProjectPathResolver.MakeRelative(target, staging.ProjectRoot);
    }
}
