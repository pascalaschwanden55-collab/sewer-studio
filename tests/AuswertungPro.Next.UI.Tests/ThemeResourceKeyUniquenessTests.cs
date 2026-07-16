using System.IO;
using System.Text.RegularExpressions;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Verhindert doppelte Ressourcen-Schluessel in einer Theme-Datei.
///
/// Der Build meldet sie NICHT: Kompiliertes XAML nimmt stillschweigend den letzten Eintrag.
/// Erst wer das Woerterbuch zur Laufzeit laedt, bekommt "Item has already been added" um die
/// Ohren — und dann faellt auch auf, dass zwei Stellen dieselbe Farbe pflegen wollten.
/// Genau so ist am 16.07. ein doppelter GlowAccentColor entstanden.
/// </summary>
public sealed class ThemeResourceKeyUniquenessTests
{
    [Theory]
    [InlineData("ThemeLight.xaml")]
    [InlineData("Theme.xaml")]
    [InlineData("Controls.xaml")]
    public void Theme_file_defines_every_resource_key_only_once(string themeFile)
    {
        var xaml = File.ReadAllText(RepoFile("src", "AuswertungPro.Next.UI", "Theme", themeFile));

        var duplicates = Regex.Matches(xaml, "x:Key=\"(?<key>[^\"]+)\"")
            .Select(match => match.Groups["key"].Value)
            .GroupBy(key => key, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => $"{group.Key} ({group.Count()}x)")
            .ToArray();

        Assert.True(
            duplicates.Length == 0,
            $"{themeFile} definiert Schluessel mehrfach — der Build schluckt das stillschweigend:\n"
            + string.Join("\n", duplicates));
    }
}
