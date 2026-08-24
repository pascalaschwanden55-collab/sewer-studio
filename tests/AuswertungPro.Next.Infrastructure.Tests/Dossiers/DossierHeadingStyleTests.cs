using System;
using System.IO;

using AuswertungPro.Next.Application.Dossiers;

using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.Dossiers;

/// <summary>
/// Woran ein Kapitel in der Word-Vorlage erkannt wird.
///
/// Die Regel entscheidet zweierlei: welches Kapitel beim Weglassen entfernt
/// wird und wie die Vorschau eine Ueberschrift darstellt. Liefe sie
/// auseinander, entfernte Word ein anderes Kapitel als die Vorschau zeigt.
/// </summary>
public sealed class DossierHeadingStyleTests
{
    [Theory]
    [InlineData("berschrift1")]   // Word speichert die Umlaut-Stile so
    [InlineData("Überschrift2")]
    [InlineData("Heading1")]
    [InlineData("heading3")]
    [InlineData("ÜBERSCHRIFT1")]
    public void Bekannte_Ueberschriftenstile_werden_erkannt(string stil)
        => Assert.True(DossierHeadingStyle.IsHeading(stil));

    [Theory]
    [InlineData("Standard")]
    [InlineData("Textkoerper")]
    [InlineData("")]
    [InlineData(null)]
    public void Alles_andere_ist_keine_Ueberschrift(string? stil)
        => Assert.False(DossierHeadingStyle.IsHeading(stil));

    [Fact]
    public void Die_Regel_steht_nur_noch_an_einer_Stelle()
    {
        // Zwei Kopien liefen in diesem Programm schon einmal auseinander.
        var wurzel = RepoWurzel();

        foreach (var datei in new[]
                 {
                     Path.Combine("src", "AuswertungPro.Next.Infrastructure", "Dossiers",
                         "DocxChapterRemover.cs"),
                     Path.Combine("src", "AuswertungPro.Next.Infrastructure", "Dossiers",
                         "Preview", "DocxFormatResolver.cs")
                 })
        {
            var quelle = File.ReadAllText(Path.Combine(wurzel, datei));

            Assert.DoesNotContain("StartsWith(\"berschrift\"", quelle, StringComparison.Ordinal);
            Assert.Contains("DossierHeadingStyle.IsHeading", quelle, StringComparison.Ordinal);
        }
    }

    private static string RepoWurzel()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "AuswertungPro.sln")))
            dir = dir.Parent;

        Assert.NotNull(dir);
        return dir!.FullName;
    }
}
