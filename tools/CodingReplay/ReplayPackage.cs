using System.Text.Json;
using System.Text.RegularExpressions;
using AuswertungPro.Next.Application.Ai.Evaluation;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.UseCases.CodingReplay;

namespace CodingReplay;

public sealed record ReplayCase(CodingReplayFrame Input, string Image, string Holding,
    string ExpectedCode, string? EventId, int? Severity, string ContextNote);
public sealed record ReplaySource(string Path, string Sha256);
public sealed record ReplayPackage(int SchemaVersion, string Role, DateTime CreatedUtc,
    IReadOnlyList<ReplaySource> Sources, IReadOnlyList<ReplayCase> Cases);

internal static class ReplayPackageBuilder
{
    internal static string Prepare(string evalRoot, string reviewFile, string projectFile, string outputRoot)
    {
        evalRoot = ReplayFiles.SafePath(evalRoot);
        reviewFile = ReplayFiles.SafePath(reviewFile);
        projectFile = ReplayFiles.SafePath(projectFile);
        var sources = new[] { Path.Combine(evalRoot, "_manifest.json"), Path.Combine(evalRoot, "_candidates.json"), reviewFile, projectFile };
        var bytes = sources.Select(p => ReplayFiles.Read(p)).ToArray();
        using var manifest = JsonDocument.Parse(bytes[0]);
        if (!manifest.RootElement.GetProperty("frozen").GetBoolean()) throw new InvalidDataException("Eval-Set ist nicht eingefroren.");
        var hashes = manifest.RootElement.GetProperty("hashes");
        RequireHash(bytes[1], hashes.GetProperty("_candidates.json").GetProperty("sha256").GetString()!);
        var reviewed = EvalReviewedDamageDataset.Load(evalRoot, reviewFile);
        using var candidates = JsonDocument.Parse(bytes[1]);
        var times = candidates.RootElement.EnumerateArray().ToDictionary(
            e => e.GetProperty("id").GetString()!, e => e.GetProperty("zeit_sek").GetDouble(), StringComparer.OrdinalIgnoreCase);
        using var project = JsonDocument.Parse(bytes[3]);
        var holdings = project.RootElement.GetProperty("Data").EnumerateArray()
            .Select(e => e.GetProperty("Fields")).Where(e => e.TryGetProperty("Haltungsname", out _))
            .GroupBy(e => e.GetProperty("Haltungsname").GetString()!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToArray(), StringComparer.OrdinalIgnoreCase);
        var output = ReplayFiles.NewFolder(outputRoot, "messpaket", evalRoot,
            Path.GetDirectoryName(reviewFile)!, Path.GetDirectoryName(projectFile)!, @"C:\KI_BRAIN", @"D:\Haltungen");
        Directory.CreateDirectory(Path.Combine(output, "images"));
        var cases = new List<ReplayCase>();
        var issues = new List<object>();
        foreach (var reviewedCase in reviewed.Cases)
        {
            var c = reviewedCase.BenchmarkCase;
            try
            {
                if (!Regex.IsMatch(c.Id, "^[A-Za-z0-9_-]{1,80}$")) throw new InvalidDataException("Ungueltige Fallkennung.");
                var image = ReplayFiles.Read(c.ImagePath, 32 * 1024 * 1024);
                RequireHash(image, hashes.GetProperty("images/" + c.FrameFileName).GetProperty("sha256").GetString()!);
                int? dn = null; double? length = null;
                var note = "Keine eindeutigen Stammdaten im ausgewaehlten Projekt.";
                if (holdings.TryGetValue(c.HoldingKey!, out var matches) && matches.Length == 1)
                {
                    var fields = matches[0];
                    var diameter = Number(fields, "DN_mm");
                    dn = diameter is > 0 and <= 10000 && diameter == Math.Truncate(diameter.Value) ? (int)diameter.Value : null;
                    length = Number(fields, "Haltungslaenge_m") ?? Number(fields, "Laenge_m");
                    note = "Stammdaten aus Projekt; keine Befunde/Importereignisse an den Analysator uebergeben.";
                }
                var relative = "images/" + c.Id + Path.GetExtension(c.ImagePath);
                ReplayFiles.WriteNew(Path.Combine(output, relative), image);
                cases.Add(new(new(c.Id, ReplayFiles.Hash(image), times[c.Id], dn, length),
                    relative, c.HoldingKey!, c.ExpectedFullCode, c.EventId, c.ExpectedSeverity, note));
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or KeyNotFoundException or ArgumentException)
            { issues.Add(new { c.Id, Error = ex.Message }); }
        }
        for (var i = 0; i < sources.Length; i++) RequireHash(ReplayFiles.Read(sources[i]), ReplayFiles.Hash(bytes[i]));
        var package = new ReplayPackage(1, "historical_diagnostic_not_release_or_training", DateTime.UtcNow,
            sources.Select((p, i) => new ReplaySource(p, ReplayFiles.Hash(bytes[i]))).ToArray(), cases);
        var json = JsonSerializer.SerializeToUtf8Bytes(package, ReplayFiles.Json);
        ReplayFiles.WriteNew(Path.Combine(output, "package.json"), json);
        ReplayFiles.WriteNew(Path.Combine(output, "package.sha256"), System.Text.Encoding.ASCII.GetBytes(ReplayFiles.Hash(json)));
        ReplayFiles.WriteJson(Path.Combine(output, "preparation.json"), new
        {
            Reviewed = reviewed.Cases.Count, Included = cases.Count, Issues = issues,
            ContextReady = cases.Count(c => c.Input.DiameterMm > 0 && c.Input.ReachLengthM > 0),
            TrainingAllowed = false, ReleaseQualified = false
        });
        return output;
    }
    internal static ReplayPackage Load(string folder)
    {
        var bytes = ReplayFiles.Read(Path.Combine(folder, "package.json"));
        RequireHash(bytes, System.Text.Encoding.ASCII.GetString(ReplayFiles.Read(Path.Combine(folder, "package.sha256"))).Trim());
        var package = JsonSerializer.Deserialize<ReplayPackage>(bytes) ?? throw new InvalidDataException("Messpaket fehlt.");
        if (package.SchemaVersion != 1 || package.Role != "historical_diagnostic_not_release_or_training" || package.Cases.Count == 0)
            throw new InvalidDataException("Messpaket ist ungueltig.");
        foreach (var c in package.Cases)
        {
            if (!Regex.IsMatch(c.Input.Id, "^[A-Za-z0-9_-]{1,80}$") || c.Image != "images/" + c.Input.Id + Path.GetExtension(c.Image)
                || Path.GetExtension(c.Image).ToLowerInvariant() is not (".png" or ".jpg" or ".jpeg"))
                throw new InvalidDataException("Bildpfad verlaesst den Paketvertrag.");
        }
        return package;
    }
    private static double? Number(JsonElement fields, string key) =>
        fields.TryGetProperty(key, out var e) && e.ValueKind == JsonValueKind.String
        && FachzahlParser.TryParseMeasurement(e.GetString(), out var value) ? (double)value : null;
    private static void RequireHash(byte[] bytes, string expected)
    {
        if (!ReplayFiles.Hash(bytes).Equals(expected, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Pruefsumme stimmt nicht.");
    }
}
