// Charakterisierungstests für GuidedVerificationService.ExtractJson → JsonObjectExtractor
// Dokumentiert, dass TryExtractFirstObject für alle realen LLM-Ausgabe-Formen
// verhaltensgleich oder besser ist als die alte ExtractJson-Implementierung.
using AuswertungPro.Next.Application.Ai;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class GuidedVerificationExtractJsonCharacterisierungTests
{
    // Normaler Qwen-Rückgabewert: einziges flaches JSON-Objekt ohne Umrahmung
    [Fact]
    public void Flaches_Einzelobjekt_ohne_Umrahmung_wird_korrekt_extrahiert()
    {
        var raw = """{"meter":2.8,"schaden_sichtbar":true,"bestaetigung":"bestaetigt","schweregrad":3,"erklaerung":"Sichtbar"}""";

        var result = JsonObjectExtractor.TryExtractFirstObject(raw);

        Assert.Equal(raw, result);
    }

    // LLM sendet manchmal Code-Fence trotz Anweisung "NUR JSON"
    [Fact]
    public void JSON_in_Code_Fence_wird_korrekt_extrahiert()
    {
        var raw = "```json\n{\"meter\":2.8,\"schaden_sichtbar\":true}\n```";

        var result = JsonObjectExtractor.TryExtractFirstObject(raw);

        // Brace-Counter findet { nach dem Fence-Prefix und stoppt korrekt bei }
        Assert.Equal("{\"meter\":2.8,\"schaden_sichtbar\":true}", result);
    }

    // LLM antwortet mit Vorrede vor dem JSON
    [Fact]
    public void JSON_mit_LLM_Vorrede_wird_korrekt_extrahiert()
    {
        var raw = "Hier ist meine Antwort:\n{\"meter\":5.1,\"schaden_sichtbar\":false,\"bestaetigung\":\"nicht_sichtbar\",\"schweregrad\":1,\"erklaerung\":\"Kein Schaden\"}";

        var result = JsonObjectExtractor.TryExtractFirstObject(raw);

        Assert.Equal("{\"meter\":5.1,\"schaden_sichtbar\":false,\"bestaetigung\":\"nicht_sichtbar\",\"schweregrad\":1,\"erklaerung\":\"Kein Schaden\"}", result);
    }

    // Geschweifte Klammern in String-Werten dürfen nicht als Objekt-Grenzen zählen
    [Fact]
    public void Geschweifte_Klammern_in_Strings_werden_ignoriert()
    {
        var raw = """{"erklaerung":"Formel {x} sichtbar","ok":true}""";

        var result = JsonObjectExtractor.TryExtractFirstObject(raw);

        Assert.Equal(raw, result);
    }

    // Verschachteltes JSON: Brace-Counter ist korrekt, alter Regex wäre falsch gewesen
    [Fact]
    public void Verschachteltes_JSON_wird_vollstaendig_extrahiert()
    {
        var raw = """{"meter":3.0,"details":{"x":1,"y":2},"ok":true}""";

        var result = JsonObjectExtractor.TryExtractFirstObject(raw);

        // Brace-Counter liefert das vollständige Objekt — alter non-greedy Regex hätte
        // fälschlicherweise bei {"meter":3.0,"details":{"x":1 gestoppt
        Assert.Equal(raw, result);
    }

    // Leerer / null-Input
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("kein JSON hier")]
    public void Gibt_null_bei_fehlendem_Objekt_zurueck(string? raw)
    {
        Assert.Null(JsonObjectExtractor.TryExtractFirstObject(raw));
    }
}
