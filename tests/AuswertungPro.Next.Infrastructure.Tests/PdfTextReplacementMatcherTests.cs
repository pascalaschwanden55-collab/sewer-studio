using System;
using System.IO;
using AuswertungPro.Next.Infrastructure.HoldingDistribution;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class PdfTextReplacementMatcherTests
{
    [Fact]
    public void FindMatches_ExactToken_FindsAllOccurrences()
    {
        using var fixture = PdfFixture.Create("(06-001) und [06-001]");
        using var document = PdfDocument.Open(fixture.Path);

        var matches = PdfTextReplacementMatcher.FindMatches(
            document.GetPage(1),
            new[] { new PdfTextReplacementTarget("06-001", "06-999") });

        Assert.Equal(2, matches.Count);
    }

    [Theory]
    [InlineData("X06-001")]
    [InlineData("06-0019")]
    [InlineData("06-001_A")]
    [InlineData("06-001.2")]
    public void FindMatches_PartOfAnotherIdentifier_DoesNotMatch(string text)
    {
        using var fixture = PdfFixture.Create(text);
        using var document = PdfDocument.Open(fixture.Path);

        var matches = PdfTextReplacementMatcher.FindMatches(
            document.GetPage(1),
            new[] { new PdfTextReplacementTarget("06-001", "06-999") });

        Assert.Empty(matches);
    }

    [Fact]
    public void FindMatches_DuplicateTargets_ReturnsOneNonOverlappingMatch()
    {
        using var fixture = PdfFixture.Create("(06-001)");
        using var document = PdfDocument.Open(fixture.Path);
        var target = new PdfTextReplacementTarget("06-001", "06-999");

        var matches = PdfTextReplacementMatcher.FindMatches(
            document.GetPage(1),
            new[] { target, target });

        Assert.Single(matches);
    }

    [Fact]
    public void FindMatches_GetrennteTextbloeckeWerdenNichtZuEinemTrefferVerbunden()
    {
        using var fixture = PdfFixture.CreateSeparated("06-", "001");
        using var document = PdfDocument.Open(fixture.Path);

        var matches = PdfTextReplacementMatcher.FindMatches(
            document.GetPage(1),
            new[] { new PdfTextReplacementTarget("06-001", "06-999") });

        Assert.Empty(matches);
    }

    [Fact]
    public void FindMatches_MehrereVerschiedeneZieleWerdenGefunden()
    {
        using var fixture = PdfFixture.Create("(06-001) und (07-002)");
        using var document = PdfDocument.Open(fixture.Path);

        var matches = PdfTextReplacementMatcher.FindMatches(
            document.GetPage(1),
            new[]
            {
                new PdfTextReplacementTarget("06-001", "06-999"),
                new PdfTextReplacementTarget("07-002", "07-999")
            });

        Assert.Equal(2, matches.Count);
    }

    private sealed class PdfFixture : IDisposable
    {
        private PdfFixture(string path) => Path = path;

        internal string Path { get; }

        internal static PdfFixture Create(string text)
        {
            var path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"pdf-match-{Guid.NewGuid():N}.pdf");
            using var builder = new PdfDocumentBuilder();
            var font = builder.AddStandard14Font(Standard14Font.Helvetica);
            var page = builder.AddPage(PageSize.A4);
            page.AddText(text, 12, new PdfPoint(40, 780), font);
            File.WriteAllBytes(path, builder.Build());
            return new PdfFixture(path);
        }

        internal static PdfFixture CreateSeparated(string first, string second)
        {
            var path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"pdf-match-{Guid.NewGuid():N}.pdf");
            using var builder = new PdfDocumentBuilder();
            var font = builder.AddStandard14Font(Standard14Font.Helvetica);
            var page = builder.AddPage(PageSize.A4);
            page.AddText(first, 12, new PdfPoint(40, 780), font);
            page.AddText(second, 12, new PdfPoint(300, 700), font);
            File.WriteAllBytes(path, builder.Build());
            return new PdfFixture(path);
        }

        public void Dispose()
        {
            try { File.Delete(Path); } catch { }
        }
    }
}
