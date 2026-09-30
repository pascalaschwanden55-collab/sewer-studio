using System.IO;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Wartbarkeitsaudit 30.09.2026, Z8: Neben jeder <c>...MitVerlauf</c>-Hülle lebt der alte Handler weiter.
/// Wer den alten Namen wieder bindet, verliert den Rückgängig-Verlauf still. Dieser Wächter leitet die
/// alten Namen aus den Hüllen selbst ab (jede Hülle ruft ihren alten Handler als <c>() =&gt; Name(...)</c>
/// auf) und verlangt, dass XAML und Seitencode nur die Hüllen verwenden.
/// </summary>
public sealed class DatenVerlaufHandlerBindungTests
{
    private static readonly string Seiten = TestRepoPaths.RepoFile("src", "AuswertungPro.Next.UI", "Views", "Pages");

    public static TheoryData<string, string, int> Seitenpaare => new()
    {
        { "DataPage", "DataPage.Verlauf.cs", 6 },
        { "SchaechtePage", "SchaechtePage.Verlauf.cs", 6 },
    };

    [Theory]
    [MemberData(nameof(Seitenpaare))]
    public void Alte_Handler_werden_in_der_Seite_nicht_direkt_gebunden(string seite, string huelle, int erwartet)
    {
        var alteNamen = LeseAlteHandler(huelle, erwartet);

        var xaml = XDocument.Load(Path.Combine(Seiten, seite + ".xaml"));
        var gebunden = xaml.Descendants()
            .SelectMany(e => e.Attributes())
            .Where(a => alteNamen.Contains(a.Value))
            .Select(a => $"{a.Name.LocalName}=\"{a.Value}\"")
            .ToList();
        Assert.True(
            gebunden.Count == 0,
            $"{seite}.xaml bindet alte Handler direkt (Verlauf geht still verloren): {string.Join(", ", gebunden)}. " +
            "Stattdessen die ...MitVerlauf-Hülle binden.");

        var direkt = new List<string>();
        foreach (var datei in SeitenCode(seite))
        {
            var code = string.Join('\n', File.ReadAllLines(datei)
                .Where(z => !z.TrimStart().StartsWith("//", StringComparison.Ordinal)));
            foreach (var name in alteNamen)
            {
                // Verwendung ausser der Definition (void Name) und ausser Fremdaufrufen (Typ.Name).
                if (Regex.IsMatch(code, $@"(?<![\.\w])(?<!void\s){Regex.Escape(name)}\b"))
                    direkt.Add($"{Path.GetFileName(datei)}: {name}");
            }
        }
        Assert.True(
            direkt.Count == 0,
            "Seitencode (Konstruktor, Verdrahtung, Aufrufe) verwendet alte Handler statt der Hüllen: " +
            string.Join(", ", direkt));
    }

    [Theory]
    [MemberData(nameof(Seitenpaare))]
    public void Jede_Huelle_wird_von_der_Seite_gebunden(string seite, string huelle, int erwartet)
    {
        var huellen = Regex.Matches(File.ReadAllText(Path.Combine(Seiten, huelle)), @"private void (\w+MitVerlauf)\(")
            .Select(m => m.Groups[1].Value)
            .ToList();
        Assert.Equal(erwartet, huellen.Count);

        var gesamt = File.ReadAllText(Path.Combine(Seiten, seite + ".xaml"))
            + string.Join('\n', SeitenCode(seite).Select(File.ReadAllText));
        foreach (var name in huellen)
            Assert.True(Regex.IsMatch(gesamt, $@"\b{Regex.Escape(name)}\b"), $"Hülle {name} wird nirgends gebunden.");
    }

    private static HashSet<string> LeseAlteHandler(string huelle, int erwartet)
    {
        var text = File.ReadAllText(Path.Combine(Seiten, huelle));
        var namen = Regex.Matches(text, @"\(\) => ([A-Za-z_]\w*)\(")
            .Select(m => m.Groups[1].Value)
            .ToHashSet(StringComparer.Ordinal);
        Assert.True(
            namen.Count == erwartet,
            $"{huelle}: {erwartet} alte Handler erwartet, gefunden: {string.Join(", ", namen)}. " +
            "Jede Hülle muss ihren alten Handler als () => Name(...) aufrufen.");
        return namen;
    }

    private static IEnumerable<string> SeitenCode(string seite)
        => Directory.EnumerateFiles(Seiten, seite + ".*.cs")
            .Concat(Directory.EnumerateFiles(Seiten, seite + ".xaml.cs"))
            .Where(f => !f.EndsWith(".Verlauf.cs", StringComparison.Ordinal));
}
