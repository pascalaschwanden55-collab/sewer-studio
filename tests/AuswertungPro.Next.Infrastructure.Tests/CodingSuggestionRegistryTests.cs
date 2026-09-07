using AuswertungPro.Next.Application.UseCases.CodingSuggestions;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class CodingSuggestionRegistryTests
{
    [Fact]
    public void Merkt_je_Haltung_nur_den_letzten_Lauf_juengster_zuerst()
    {
        var reg = new CodingSuggestionRegistry();
        var n = 0; reg.Geaendert += () => n++;
        reg.Merke("1-2", CodingSuggestionSet.Leer("a"));
        reg.Merke("3-4", CodingSuggestionSet.Leer("b"));
        reg.Merke("1-2", CodingSuggestionSet.Leer("c"));
        var heute = reg.Heute();
        Assert.Equal(2, heute.Count);
        Assert.Equal("1-2", heute[0].Haltung);
        Assert.Equal("c", heute[0].Set.BogenTeil.Grund);
        Assert.Equal(3, n);
    }

    [Fact]
    public void Leere_Haltung_wird_nicht_gemerkt()
    {
        var reg = new CodingSuggestionRegistry();
        reg.Merke(" ", CodingSuggestionSet.Leer("a"));
        Assert.Empty(reg.Heute());
    }
}
