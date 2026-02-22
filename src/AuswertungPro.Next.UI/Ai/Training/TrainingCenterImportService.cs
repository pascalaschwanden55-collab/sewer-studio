using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace AuswertungPro.Next.UI.Ai.Training;

public sealed class TrainingCenterImportService
{
    private static readonly string[] VideoExts = [".mp4", ".mov", ".mkv", ".avi", ".mpg", ".mpeg", ".wmv", ".ts", ".m4v"];
    private static readonly string[] ProtocolExts = [".json", ".xml", ".pdf"];

    public Task<List<TrainingCase>> ScanAsync(string rootFolder)
    {
        if (string.IsNullOrWhiteSpace(rootFolder) || !Directory.Exists(rootFolder))
            return Task.FromResult(new List<TrainingCase>());

        var folders = EnumerateFolders(rootFolder);

        var cases = new List<TrainingCase>();

        foreach (var folder in folders)
        {
            try
            {
                var files = Directory.EnumerateFiles(folder, "*.*", SearchOption.TopDirectoryOnly).ToList();
                if (files.Count == 0)
                    continue;

                var videos = files.Where(f => VideoExts.Contains(Path.GetExtension(f).ToLowerInvariant())).ToList();
                var protos = files.Where(f => ProtocolExts.Contains(Path.GetExtension(f).ToLowerInvariant())).ToList();

                // Video ist Pflicht, Protokoll ist optional
                if (videos.Count == 0)
                    continue;

                var bestVideo = videos
                    .Select(p => new FileInfo(p))
                    .OrderByDescending(fi => fi.Length)
                    .First().FullName;

                var bestProto = protos.Count > 0 ? PickBestProtocol(protos) : "";

                var caseId = SafeRelativeId(rootFolder, folder);

                cases.Add(new TrainingCase
                {
                    CaseId = caseId,
                    FolderPath = folder,
                    VideoPath = bestVideo,
                    ProtocolPath = bestProto,
                    Status = TrainingCaseStatus.New,
                    CreatedUtc = DateTime.UtcNow
                });
            }
            catch
            {
                // ignore folder errors
            }
        }

        // Stable ordering for UI
        cases = cases.OrderBy(c => c.CaseId, StringComparer.OrdinalIgnoreCase).ToList();
        return Task.FromResult(cases);
    }

    private static IEnumerable<string> EnumerateFolders(string rootFolder)
    {
        yield return rootFolder;

        foreach (var dir in Directory.EnumerateDirectories(rootFolder, "*", SearchOption.AllDirectories))
            yield return dir;
    }

    private static string PickBestProtocol(List<string> protos)
    {
        // prefer json > xml > pdf
        string? pick = protos.FirstOrDefault(p => Path.GetExtension(p).Equals(".json", StringComparison.OrdinalIgnoreCase))
            ?? protos.FirstOrDefault(p => Path.GetExtension(p).Equals(".xml", StringComparison.OrdinalIgnoreCase))
            ?? protos.FirstOrDefault(p => Path.GetExtension(p).Equals(".pdf", StringComparison.OrdinalIgnoreCase))
            ?? protos.First();

        return pick!;
    }

    private static string SafeRelativeId(string root, string folder)
    {
        try
        {
            var rel = Path.GetRelativePath(root, folder);
            if (string.IsNullOrWhiteSpace(rel) || rel == ".")
                return new DirectoryInfo(folder).Name;

            // Normalize slashes
            rel = rel.Replace('\\', '/');
            return rel;
        }
        catch
        {
            return new DirectoryInfo(folder).Name;
        }
    }
}
