using AuswertungPro.Next.Application.Common;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.Common;

public class ProjectPathResolverTests
{
    [Theory]
    [InlineData("..", "UNKNOWN")]
    [InlineData(".", "UNKNOWN")]
    [InlineData("...", "UNKNOWN")]
    [InlineData("  ", "UNKNOWN")]
    [InlineData(null, "UNKNOWN")]
    public void SanitizePathSegment_FaengtPunktSegmenteAb(string? input, string erwartet)
        => Assert.Equal(erwartet, ProjectPathResolver.SanitizePathSegment(input));

    [Theory]
    [InlineData("../..")]
    [InlineData("..\\..")]
    [InlineData("..\\..\\Windows")]
    public void SanitizePathSegment_ErzeugtNieEinTraversalSegment(string input)
    {
        // Sicherheits-Eigenschaft: keine Pfadtrenner und nicht exakt "."/".." ->
        // ueber Path.Combine kann damit nicht aus dem Zielordner ausgebrochen werden.
        var result = ProjectPathResolver.SanitizePathSegment(input);
        Assert.DoesNotContain('/', result);
        Assert.DoesNotContain('\\', result);
        Assert.NotEqual(".", result);
        Assert.NotEqual("..", result);
    }

    [Theory]
    [InlineData("06.24341-35625", "06.24341-35625")]  // normaler Haltungsname bleibt
    [InlineData("Gotthardstrasse", "Gotthardstrasse")]
    [InlineData("100-200", "100-200")]
    public void SanitizePathSegment_LaesstNormaleNamenDurch(string input, string erwartet)
        => Assert.Equal(erwartet, ProjectPathResolver.SanitizePathSegment(input));

    [Fact]
    public void SanitizePathSegment_EntferntFuehrendeUndAbschliessendePunkte()
        => Assert.Equal("Haltung", ProjectPathResolver.SanitizePathSegment(".Haltung."));
}
