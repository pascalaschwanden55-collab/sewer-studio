using System.IO;
using System.Text.RegularExpressions;
using AuswertungPro.Next.UI.DataPage;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Schlusspruefung Schritt A (23.09.2026): Nutzungsart und Lagebestimmung wurden am Schacht zu
/// Auswahlfeldern, aber SchaechtePage.ResolveOptions kannte ihre Listen nicht — Kurzansicht,
/// Eingabefelder-Schublade und Schachtansicht zeigten eine leere Auswahl. Jede Liste, die ein
/// Schacht-Auswahlfeld verlangt, muss dort aufgeloest werden.
/// </summary>
public sealed class SchachtAuswahlListenWaechterTests
{
    private static readonly string[] Schachtspalten =
    [
        "Funktion", "Material", "Status", "Sanierungsbedarf", "Bauwerksart", "Versickerungsart",
        "Schachtform", "Belastungsklasse", "Ausgefuehrt durch", "Eigentümer", "Referenzpruefung",
        "Sanieren Ja/Nein", "Nutzungsart", "Lagebestimmung",
    ];

    [Fact]
    public void Jede_liste_eines_schacht_auswahlfelds_wird_aufgeloest()
    {
        var quelle = File.ReadAllText(Path.Combine(TestRepoPaths.FindRepositoryRoot(),
            "src", "AuswertungPro.Next.UI", "Views", "Pages", "SchaechtePage.xaml.cs"));
        var start = quelle.IndexOf("private IEnumerable<string> ResolveOptions(", StringComparison.Ordinal);
        Assert.True(start >= 0, "ResolveOptions nicht gefunden");
        var ende = quelle.IndexOf("_ => Array.Empty<string>()", start, StringComparison.Ordinal);
        var aufgeloest = Regex.Matches(quelle[start..ende], "\"(\\w+)\"\\s*=>")
            .Select(m => m.Groups[1].Value).ToHashSet(StringComparer.Ordinal);

        var fehlend = Schachtspalten
            .Select(SchaechteColumnPolicy.ResolveOptionField)
            .Where(f => f is not null)
            .Select(f => GridDropdownFieldPolicy.TryResolve(f!, out var spec) ? spec.ItemsSourcePath : null)
            .Where(p => p is not null && !aufgeloest.Contains(p))
            .Distinct()
            .ToList();

        Assert.True(fehlend.Count == 0, "Nicht aufgeloeste Schachtlisten: " + string.Join(", ", fehlend));
    }
}
