using AuswertungPro.Next.Application.Ai;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

public sealed class JsonObjectExtractorTests
{
    [Fact]
    public void Liefert_TopLevel_Objekt_bei_verschachteltem_JSON()
    {
        var raw = "{\"meter\":18.4,\"details\":{\"x\":1},\"severity\":\"high\"}";

        var json = JsonObjectExtractor.TryExtractFirstObject(raw);

        Assert.Equal(raw, json);
    }

    [Fact]
    public void Findet_Objekt_nach_LLM_Vorrede()
    {
        var raw = "Hier ist das Ergebnis:\n{\"findings\":[\"Riss\"],\"severity\":\"mid\"}\nFertig.";

        var json = JsonObjectExtractor.TryExtractFirstObject(raw);

        Assert.Equal("{\"findings\":[\"Riss\"],\"severity\":\"mid\"}", json);
    }

    [Fact]
    public void Ignoriert_geschweifte_Klammern_in_Strings()
    {
        var raw = "{\"reason\":\"Klammer { im Text\",\"ok\":true}";

        var json = JsonObjectExtractor.TryExtractFirstObject(raw);

        Assert.Equal(raw, json);
    }

    [Fact]
    public void Respektiert_Escape_Sequenzen_in_Strings()
    {
        var raw = "{\"reason\":\"Anfuehrungszeichen: \\\" und }\",\"ok\":true}";

        var json = JsonObjectExtractor.TryExtractFirstObject(raw);

        Assert.Equal(raw, json);
    }

    [Fact]
    public void Liefert_null_wenn_kein_Objekt_im_Text()
    {
        Assert.Null(JsonObjectExtractor.TryExtractFirstObject("kein JSON hier"));
        Assert.Null(JsonObjectExtractor.TryExtractFirstObject(""));
        Assert.Null(JsonObjectExtractor.TryExtractFirstObject(null));
    }

    [Fact]
    public void Liefert_null_bei_unbalancierten_Klammern()
    {
        var raw = "{\"a\":1, \"b\":[1,2";

        Assert.Null(JsonObjectExtractor.TryExtractFirstObject(raw));
    }
}
