using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.Tests;

public sealed class SchaechteSearchMatcherTests
{
    [Fact]
    public void Matches_returns_true_for_empty_search_text()
    {
        var record = new SchachtRecord();
        record.SetFieldValue("Schachtnummer", "S-100");

        Assert.True(SchaechteSearchMatcher.Matches(record, ""));
        Assert.True(SchaechteSearchMatcher.Matches(record, "   "));
        Assert.True(SchaechteSearchMatcher.Matches(record, null));
    }

    [Fact]
    public void Matches_checks_field_keys_and_values_case_insensitive()
    {
        var record = new SchachtRecord();
        record.SetFieldValue("Schachtnummer", "S-100");
        record.SetFieldValue("Strasse", "Bahnhofplatz");

        Assert.True(SchaechteSearchMatcher.Matches(record, "nummer"));
        Assert.True(SchaechteSearchMatcher.Matches(record, "bahnhof"));
        Assert.False(SchaechteSearchMatcher.Matches(record, "hauptstrasse"));
    }

    [Fact]
    public void BuildResultInfo_returns_empty_without_search_and_count_text_with_search()
    {
        Assert.Equal("", SchaechteSearchMatcher.BuildResultInfo("", 4, 10));
        Assert.Equal("4 von 10 Schaechten", SchaechteSearchMatcher.BuildResultInfo("S", 4, 10));
    }
}
