using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.Media;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.Infrastructure.Media;

namespace AuswertungPro.Next.UI.Views.Windows;

internal sealed record MediaSearchApplyResult(
    int VideoCount,
    int PdfCount,
    int FotoCount)
{
    internal bool Applied => VideoCount > 0 || PdfCount > 0 || FotoCount > 0;
}

internal static class MediaSearchApplyController
{
    internal static MediaSearchApplyResult Apply(
        IReadOnlyList<MediaMatchRow> rows,
        string? projectFilePath)
    {
        ArgumentNullException.ThrowIfNull(rows);

        var videoCount = 0;
        var pdfCount = 0;
        var fotoCount = 0;

        foreach (var row in rows.Where(row => row.Apply))
        {
            if (!string.IsNullOrWhiteSpace(row.VideoPath) && CanApply(row.Match.VideoStatus))
            {
                var projectRoot = ProjectFileLocator.ProjectRootFromFile(projectFilePath);
                var storedPath = ProjectPathResolver.MakeRelativeIfInsideProject(row.VideoPath, projectRoot);
                row.Match.Record.SetFieldValue(FieldKeys.Link, storedPath, FieldSource.Unknown, userEdited: false);
                videoCount++;
            }

            if (!string.IsNullOrWhiteSpace(row.PdfPath) && CanApply(row.Match.PdfStatus))
            {
                row.Match.Record.SetFieldValue(FieldKeys.PdfPath, row.PdfPath, FieldSource.Unknown, userEdited: false);
                pdfCount++;
            }

            fotoCount += ApplyPhotos(row.Match.Record, row.Match.FotoPaths);
        }

        return new MediaSearchApplyResult(videoCount, pdfCount, fotoCount);
    }

    private static bool CanApply(MediaMatchStatus status)
        => status is MediaMatchStatus.Found or MediaMatchStatus.Ambiguous;

    private static int ApplyPhotos(HaltungRecord record, IReadOnlyList<string> photoPaths)
    {
        if (photoPaths.Count == 0)
            return 0;

        var revision = GetOrCreateCurrentRevision(record);
        var activeEntries = revision.Entries
            .Where(entry => !entry.IsDeleted)
            .ToList();
        var appliedCount = 0;

        foreach (var photoPath in photoPaths)
        {
            var meter = PhotoFileMeterParser.TryParseFromPath(photoPath);
            if (meter is not null && activeEntries.Count > 0)
            {
                var best = PhotoProtocolEntryMatcher.FindNearestActiveEntry(activeEntries, meter.Value);
                if (best is not null)
                {
                    if (!ContainsPhoto(best, photoPath))
                    {
                        best.FotoPaths.Add(photoPath);
                        appliedCount++;
                    }

                    continue;
                }
            }

            if (activeEntries.Count > 0)
            {
                var first = activeEntries[0];
                if (!ContainsPhoto(first, photoPath))
                {
                    first.FotoPaths.Add(photoPath);
                    appliedCount++;
                }

                continue;
            }

            var placeholder = new ProtocolEntry
            {
                Code = "",
                Beschreibung = "Foto (automatisch zugeordnet)",
                MeterStart = meter,
                Source = ProtocolEntrySource.Imported,
                FotoPaths = [photoPath]
            };
            revision.Entries.Add(placeholder);
            activeEntries.Add(placeholder);
            appliedCount++;
        }

        return appliedCount;
    }

    private static ProtocolRevision GetOrCreateCurrentRevision(HaltungRecord record)
    {
        if (record.Protocol is null)
        {
            record.Protocol = new ProtocolDocument
            {
                HaltungId = record.GetFieldValue(FieldKeys.HoldingName),
                Original = CreateRevision("Medien-Import"),
                Current = CreateRevision("Arbeitskopie")
            };
        }

        record.Protocol.Current ??= CreateRevision("Arbeitskopie");
        return record.Protocol.Current;
    }

    private static ProtocolRevision CreateRevision(string comment)
        => new()
        {
            Comment = comment,
            Entries = []
        };

    private static bool ContainsPhoto(ProtocolEntry entry, string photoPath)
        => entry.FotoPaths.Contains(photoPath, StringComparer.OrdinalIgnoreCase);
}
