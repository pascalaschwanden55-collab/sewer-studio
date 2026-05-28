using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.DataPage;
using Xunit;

namespace AuswertungPro.Next.UI.Tests;

public sealed class DataPageSearchMatcherTests
{
    [Fact]
    public void Matches_returns_true_for_empty_search()
    {
        var record = new HaltungRecord();

        Assert.True(DataPageSearchMatcher.Matches(record, ""));
        Assert.True(DataPageSearchMatcher.Matches(record, "   "));
        Assert.True(DataPageSearchMatcher.Matches(record, null));
    }

    [Fact]
    public void Matches_searches_holding_name_and_street_case_insensitive()
    {
        var record = new HaltungRecord();
        record.SetFieldValue("Haltungsname", "12.034-12.035", FieldSource.Manual, userEdited: true);
        record.SetFieldValue("Strasse", "Bahnhofstrasse", FieldSource.Manual, userEdited: true);

        Assert.True(DataPageSearchMatcher.Matches(record, "12.035"));
        Assert.True(DataPageSearchMatcher.Matches(record, "bahnhof"));
        Assert.False(DataPageSearchMatcher.Matches(record, "hauptstrasse"));
    }

    [Fact]
    public void BuildResultInfo_hides_text_without_search_term()
    {
        Assert.Equal("", DataPageSearchMatcher.BuildResultInfo("", 4, 10));
        Assert.Equal("4 von 10 Haltungen", DataPageSearchMatcher.BuildResultInfo("12", 4, 10));
    }
}
