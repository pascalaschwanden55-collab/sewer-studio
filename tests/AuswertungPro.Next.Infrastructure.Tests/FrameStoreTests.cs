using System.IO;
using AuswertungPro.Next.Infrastructure.Ai.Training;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class FrameStoreTests
{
    [Theory]
    // Verschachtelte CaseId mit '/' (und '.') wird zu einem flachen, sicheren Datei-Stamm.
    [InlineData("st_06.24341-35625/20250602_x_000", "st_06_24341-35625_20250602_x_000")]
    [InlineData("a\\b", "a_b")]
    [InlineData("a:b*c?", "a_b_c_")]
    [InlineData("normal-id_001", "normal-id_001")] // bereits sicher -> unveraendert
    public void SanitizeFileStem_macht_pfadsichere_Dateinamen(string raw, string expected)
    {
        Assert.Equal(expected, FrameStore.SanitizeFileStem(raw));
    }

    [Theory]
    [InlineData("st_06.24341-35625/20250602_x_000")]
    [InlineData("mit Leerzeichen / und \\ und : Zeichen")]
    public void SanitizeFileStem_enthaelt_keine_pfad_oder_ungueltigen_Zeichen(string raw)
    {
        var stem = FrameStore.SanitizeFileStem(raw);

        Assert.DoesNotContain('/', stem);
        Assert.DoesNotContain('\\', stem);
        foreach (var c in Path.GetInvalidFileNameChars())
            Assert.DoesNotContain(c, stem);
    }

    [Fact]
    public void SanitizeFileStem_leerer_Input_ergibt_Fallback()
    {
        Assert.Equal("frame", FrameStore.SanitizeFileStem(""));
    }
}
