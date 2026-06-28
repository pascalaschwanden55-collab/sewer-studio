using AuswertungPro.Next.Application.Protocol;

namespace AuswertungPro.Next.Infrastructure.Tests;

/// <summary>
/// Charakterisierungstests fuer das gemeinsame Klon-Verhalten (CodeDefinitionCloning),
/// getestet ueber JsonCodeCatalogProvider und CompositeCodeCatalogProvider als oeffentliche Einstiegspunkte.
/// </summary>
public sealed class CodeDefinitionCloningTests
{
    // Hilfsmethode: erzeugt einen JsonCodeCatalogProvider mit einem einzigen Eintrag
    private static JsonCodeCatalogProvider ProviderMit(CodeDefinition def)
    {
        var path = Path.Combine(
            Path.GetTempPath(), "AuswertungProTests", Guid.NewGuid().ToString("N"), "codes.json");
        var p = new JsonCodeCatalogProvider(path);
        p.Save(new List<CodeDefinition> { def });
        return p;
    }

    [Fact]
    public void GetAll_LiefertTiefekopie_KeineReferenzIdentitaetMitInternemZustand()
    {
        // CloneCode muss neue Listen erzeugen
        var provider = ProviderMit(new CodeDefinition
        {
            Code = "BCA",
            Title = "Anschluss",
            CategoryPath = new List<string> { "Bestand" },
            Examples = new List<string> { "BCA-1" },
            Parameters = new List<CodeParameter> { new() { Name = "P1", Type = "string" } }
        });

        var first = provider.GetAll()[0];
        var second = provider.GetAll()[0];

        // Zwei Aufrufe -> zwei verschiedene Instanzen
        Assert.NotSame(first, second);
        Assert.NotSame(first.CategoryPath, second.CategoryPath);
        Assert.NotSame(first.Examples, second.Examples);
        Assert.NotSame(first.Parameters, second.Parameters);
    }

    [Fact]
    public void TryGet_Parameter_DataKey_WirdGetrimmt()
    {
        // CloneParameter muss DataKey trimmen (kein Whitespace im Ergebnis)
        var provider = ProviderMit(new CodeDefinition
        {
            Code = "BAB",
            Title = "Riss",
            Parameters = new List<CodeParameter>
            {
                new() { Name = "Char1", DataKey = "  dk1  ", Type = "text", Required = true }
            }
        });

        Assert.True(provider.TryGet("BAB", out var def));
        Assert.Equal("dk1", def.Parameters[0].DataKey);
    }

    [Fact]
    public void TryGet_Parameter_WhitespaceDataKey_WirdNull()
    {
        // CloneParameter: reiner Whitespace-DataKey -> null
        var provider = ProviderMit(new CodeDefinition
        {
            Code = "BBC",
            Title = "Ablagerung",
            Parameters = new List<CodeParameter>
            {
                new() { Name = "P", DataKey = "   ", Type = "" }
            }
        });

        Assert.True(provider.TryGet("BBC", out var def));
        Assert.Null(def.Parameters[0].DataKey);
        Assert.Equal("string", def.Parameters[0].Type);
    }

    [Fact]
    public void TryGet_Parameter_WhitespaceUnit_WirdNull()
    {
        // CloneParameter: reiner Whitespace-Unit -> null
        var provider = ProviderMit(new CodeDefinition
        {
            Code = "BBB",
            Title = "Inkrustation",
            Parameters = new List<CodeParameter>
            {
                new() { Name = "P", Unit = "  " }
            }
        });

        Assert.True(provider.TryGet("BBB", out var def));
        Assert.Null(def.Parameters[0].Unit);
    }

    [Fact]
    public void TryGet_NullGroup_WirdUnbekannt()
    {
        // CloneCode: Group null -> "Unbekannt"
        var provider = ProviderMit(new CodeDefinition
        {
            Code = "BCD",
            Title = "Rohranfang",
            Group = null!
        });

        Assert.True(provider.TryGet("BCD", out var def));
        Assert.Equal("Unbekannt", def.Group);
    }

    [Fact]
    public void CompositeProvider_Parameter_DataKeyWirdEbenfallsGetrimmt()
    {
        // Sichert das angeglichene Verhalten im CompositeCodeCatalogProvider ab:
        // frueheres CloneParameter hatte kein DataKey-Trim, jetzt delegiert es an CodeDefinitionCloning.
        var inner = ProviderMit(new CodeDefinition
        {
            Code = "BAF",
            Title = "Oberflaechenschaden",
            Parameters = new List<CodeParameter>
            {
                new() { Name = "Char1", DataKey = "  char1  ", Type = "  text  ", Unit = "  mm  " }
            }
        });

        var composite = new CompositeCodeCatalogProvider(new List<ICodeCatalogProvider> { inner });
        Assert.True(composite.TryGet("BAF", out var def));
        Assert.Equal("char1", def.Parameters[0].DataKey);
        Assert.Equal("text", def.Parameters[0].Type);
        Assert.Equal("mm", def.Parameters[0].Unit);
    }
}
