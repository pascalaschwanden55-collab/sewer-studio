using System.Text.Json;
using System.Text.RegularExpressions;
using AuswertungPro.Next.Application.UseCases.BendSuggestions;

namespace AuswertungPro.Next.Infrastructure.Ai.BendSuggestions;

/// <summary>
/// Liest <c>workpoint.json</c> neben dem Kandidaten.
///
/// Der Arbeitspunkt liegt bewusst nicht im <c>candidate_manifest.json</c>: Der
/// fail-closed Sidecar-Vertrag <c>/detect/yolo/bcc-test/candidates</c> liefert
/// feste Felder, ein neues Feld kaeme in C# gar nicht an. Diesen Pfad dafuer zu
/// erweitern waere der teurere Weg.
/// </summary>
public sealed partial class BendSuggestionCalibrationFileStore : IBendSuggestionCalibrationStore
{
    private const string FileName = "workpoint.json";

    private readonly string _candidatesRoot;

    public BendSuggestionCalibrationFileStore()
        : this(@"C:\KI_BRAIN\training\models\candidates")
    {
    }

    public BendSuggestionCalibrationFileStore(string candidatesRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(candidatesRoot);
        _candidatesRoot = Path.GetFullPath(candidatesRoot);
    }

    public BendSuggestionCalibration? TryRead(string candidateId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(candidateId);
        if (!CandidateIdPattern().IsMatch(candidateId))
        {
            throw new ArgumentException(
                $"Die Kandidaten-ID ist unsicher: {candidateId}", nameof(candidateId));
        }

        var path = Path.Combine(_candidatesRoot, candidateId, FileName);
        if (!File.Exists(path))
            return null;

        JsonElement document;
        try
        {
            document = JsonSerializer.Deserialize<JsonElement>(File.ReadAllText(path));
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            throw new InvalidDataException($"Der Arbeitspunkt ist nicht lesbar: {path}", ex);
        }

        return new BendSuggestionCalibration
        {
            CandidateId = ReadString(document, "candidate_id", path),
            WeightSha256 = ReadString(document, "weight_sha256", path),
            MinConfidence = ReadDouble(document, "min_confidence", path),
            StrongConfidence = ReadDouble(document, "strong_confidence", path),
            Source = ReadString(document, "source", path)
        };
    }

    private static string ReadString(JsonElement document, string name, string path)
    {
        if (document.ValueKind != JsonValueKind.Object
            || !document.TryGetProperty(name, out var value)
            || value.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(value.GetString()))
        {
            throw new InvalidDataException(
                $"Im Arbeitspunkt fehlt das Feld {name} oder es ist leer: {path}");
        }

        return value.GetString()!;
    }

    private static double ReadDouble(JsonElement document, string name, string path)
    {
        if (document.ValueKind != JsonValueKind.Object
            || !document.TryGetProperty(name, out var value)
            || value.ValueKind != JsonValueKind.Number
            || !value.TryGetDouble(out var number))
        {
            throw new InvalidDataException(
                $"Im Arbeitspunkt fehlt die Zahl {name}: {path}");
        }

        return number;
    }

    /// <summary>Dasselbe Muster, das auch der Sidecar-Waechter verlangt.</summary>
    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9_-]{0,127}$")]
    private static partial Regex CandidateIdPattern();
}
