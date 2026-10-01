using System.Text.Json;
using AuswertungPro.Next.Application.Ai.Training;

namespace AuswertungPro.Next.Pipeline.Tests;

/// <summary>
/// Liest dieselbe Beispieldatei wie der Python-Test
/// (training/scripts/tests/test_haltungsidentitaet.py) und prueft die fuer die
/// Eval-Trennung massgebliche C#-Regel (Gegenrichtung, Normalisierung).
/// </summary>
public sealed class HaltungsidentitaetTests
{
    public static IEnumerable<object[]> Faelle()
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(
            TestRepoPaths.RepoFile("tests", "Fixtures", "Haltungsidentitaet", "beispiele.json")));
        foreach (var fall in doc.RootElement.GetProperty("faelle").EnumerateArray())
        {
            var eingabe = fall.GetProperty("eingabe");
            yield return new object[]
            {
                fall.GetProperty("id").GetString()!,
                eingabe.ValueKind == JsonValueKind.Null ? "\u0000null" : eingabe.GetString()!,
                fall.GetProperty("eval_schluessel").GetString()!,
                fall.GetProperty("gleiche_haltung").GetBoolean(),
                fall.GetProperty("csharp").GetProperty("normalisiert").GetString() ?? "\u0000null",
            };
        }
    }

    [Theory]
    [MemberData(nameof(Faelle))]
    public void Beispiele_gegen_CSharp_Regel(
        string id, string eingabeRoh, string evalSchluessel, bool gleicheHaltung, string normalisiertRoh)
    {
        string? eingabe = eingabeRoh == "\u0000null" ? null : eingabeRoh;
        string? erwartetNormalisiert = normalisiertRoh == "\u0000null" ? null : normalisiertRoh;

        Assert.True(erwartetNormalisiert == EvalContaminationGuard.NormalizeHaltungKey(eingabe),
            $"Normalisierung weicht ab: {id}");

        var evalSatz = new HashSet<string>(new[] { evalSchluessel }, StringComparer.OrdinalIgnoreCase);
        Assert.True(gleicheHaltung == EvalContaminationGuard.IsEvalHaltung(evalSatz, eingabe),
            $"IsEvalHaltung weicht ab: {id}");
    }
}
