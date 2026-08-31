using AuswertungPro.Next.UI.Services;
using System.Collections.ObjectModel;
using Xunit;

namespace AuswertungPro.Next.UI.Tests;

public sealed class DropdownOptionListTests
{
    [Fact]
    public void AddIfMissing_inserts_trimmed_value_at_top()
    {
        var options = new ObservableCollection<string> { "Nein" };

        var added = DropdownOptionList.AddIfMissing(options, "  Ja  ");

        Assert.True(added);
        Assert.Equal(new[] { "Ja", "Nein" }, options);
    }

    [Fact]
    public void AddIfMissing_ignores_empty_and_case_duplicates()
    {
        var options = new ObservableCollection<string> { "Ja" };

        Assert.False(DropdownOptionList.AddIfMissing(options, "  "));
        Assert.False(DropdownOptionList.AddIfMissing(options, "ja"));

        Assert.Equal(new[] { "Ja" }, options);
    }

    [Fact]
    public void Remove_deletes_case_insensitive_match()
    {
        var options = new ObservableCollection<string> { "Ja", "Nein" };

        var removed = DropdownOptionList.Remove(options, "nein");

        Assert.True(removed);
        Assert.Equal(new[] { "Ja" }, options);
    }

    [Fact]
    public void EnsureExact_replaces_missing_or_reordered_options()
    {
        var options = new ObservableCollection<string> { "Privat", "Kanton" };

        var changed = DropdownOptionList.EnsureExact(options, new[] { "Kanton", "Privat" });

        Assert.True(changed);
        Assert.Equal(new[] { "Kanton", "Privat" }, options);
    }

    [Fact]
    public void EnsureExact_returns_false_when_already_equal()
    {
        var options = new ObservableCollection<string> { "Kanton", "Privat" };

        var changed = DropdownOptionList.EnsureExact(options, new[] { "Kanton", "Privat" });

        Assert.False(changed);
        Assert.Equal(new[] { "Kanton", "Privat" }, options);
    }

    [Fact]
    public void Shared_eigentuemer_defaults_are_single_source()
    {
        var options = new ObservableCollection<string> { "Privat" };

        DropdownOptionList.EnsureExact(options, DropdownOptionsStore.FixedEigentuemerOptions);

        // Die amtlichen Begriffe des Kantons. "AWU" und "Kanton" stehen nicht
        // mehr zur Auswahl, bleiben in Altprojekten aber gefaerbt und gezaehlt.
        Assert.Equal(
            new[] { "Privat", "Abwasser Uri", "Gemeinde", "Kanton Uri", "Bund", "unbekannt" },
            options);
    }

    [Fact]
    public void ExtractText_handles_null_string_and_objects()
    {
        Assert.Equal("", DropdownOptionList.ExtractText(null));
        Assert.Equal("Text", DropdownOptionList.ExtractText("Text"));
        Assert.Equal("42", DropdownOptionList.ExtractText(42));
    }
}
