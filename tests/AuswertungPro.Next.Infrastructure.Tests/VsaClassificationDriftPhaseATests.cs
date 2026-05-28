using System.Text.Json;

namespace AuswertungPro.Next.Infrastructure.Tests;

/// <summary>
/// Phase-A-Drift-Nachweis fuer ADR-007.
/// Ohne Skip sind diese Tests erwartungsgemaess rot, bis die
/// Klassifizierungstabellen aus der VSA-Richtlinie 2023 neu modelliert sind.
/// </summary>
public sealed class VsaClassificationDriftPhaseATests
{
    [Theory(Skip = "ADR-007: dokumentiert bekannten Drift in classification_channels.json bis die VSA-Regelengine neu aufgebaut ist.")]
    [InlineData("BAA", "Riss")]
    [InlineData("BAB", "Bruch")]
    [InlineData("BBA", "Deformation")]
    [InlineData("BDD", "Deformation")]
    public void ClassificationChannels_Darf_PdfWahrheit_Nicht_Widersprechen(
        string code,
        string forbiddenMeaning)
    {
        var rule = LoadChannelRule(code);
        var description = rule.GetProperty("_desc").GetString() ?? string.Empty;

        Assert.DoesNotContain(forbiddenMeaning, description, StringComparison.OrdinalIgnoreCase);
    }

    private static JsonElement LoadChannelRule(string code)
    {
        var root = TestPaths.FindSolutionRoot();
        var path = Path.Combine(root, "src", "AuswertungPro.Next.UI", "Data", "classification_channels.json");
        using var document = JsonDocument.Parse(File.ReadAllText(path));

        foreach (var rule in document.RootElement.GetProperty("rules").EnumerateArray())
        {
            if (rule.TryGetProperty("code", out var value)
                && string.Equals(value.GetString(), code, StringComparison.OrdinalIgnoreCase))
            {
                return rule.Clone();
            }
        }

        throw new InvalidOperationException($"Klassifizierungsregel fuer '{code}' nicht gefunden.");
    }
}
