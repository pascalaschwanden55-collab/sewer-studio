using AuswertungPro.Next.Infrastructure.Ai.Training;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class TrainingCenterImportServiceParseTests
{
    [Fact]
    public void ExtractEntriesFromChunkText_discards_unknown_codes_from_free_text()
    {
        const string text = """
            12.300 BAB Riss laengs bei 3 Uhr
            13.400 ABC Das ist kein VSA-Code
            14.500 BBAA Wurzeln an der Rohrwand
            15.600 BA Nur Gruppe, kein Befundcode
            """;

        var entries = TrainingCenterImportService.ExtractEntriesFromChunkText(text);

        Assert.Collection(entries,
            first =>
            {
                Assert.Equal("BAB", first.Code);
                Assert.Equal(12.300, first.MeterStart, 3);
            },
            second =>
            {
                Assert.Equal("BBAA", second.Code);
                Assert.Equal(14.500, second.MeterStart, 3);
            });
    }
}
