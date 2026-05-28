using System.Text.Json;
using AuswertungPro.Next.Domain.Models.Devis;
using AuswertungPro.Next.Infrastructure.Devis;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class DevisMappingServiceVsaKekTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    [Theory]
    [InlineData("Laengsriss", "BAB")]
    [InlineData("Wurzeleinwuchs", "BBA")]
    [InlineData("Deformation", "BAA")]
    [InlineData("Korrosion", "BAF")]
    [InlineData("Rohrversatz", "BAJ")]
    [InlineData("Defekten Anschluss", "BAH")]
    public void DevisMappings_follow_vsa_kek_base_code_truth(string descriptionNeedle, string expectedCode)
    {
        var config = LoadRepoDevisConfig();

        var mapping = config.Mappings.Single(m =>
            m.MassnahmenBeschreibung.Contains(descriptionNeedle, StringComparison.OrdinalIgnoreCase));

        Assert.Equal(expectedCode, mapping.SchadensCode);
    }

    [Fact]
    public void DevisMappingService_uses_base_code_fallback_for_detailed_vsa_codes()
    {
        using var temp = new TempConfig("""
        {
          "version": "test",
          "mappings": [
            {
              "id": "VERSATZ_BAJ",
              "schadensCode": "BAJ",
              "minZustandsklasse": 1,
              "maxZustandsklasse": 4,
              "massnahme": "Teilersatz",
              "massnahmenBeschreibung": "Teilersatz bei Rohrversatz",
              "baumeisterPositionen": [],
              "rohrleitungsbauPositionen": [],
              "prioritaet": 10
            }
          ]
        }
        """);
        var service = new DevisMappingService(temp.Path);

        var result = service.GetEmpfehlung("BAJB", char1: null, char2: null, zustandsKlasse: 3, dn: 250);

        Assert.NotNull(result.Mapping);
        Assert.Equal("VERSATZ_BAJ", result.Mapping!.Id);
    }

    private static DevisMappingConfig LoadRepoDevisConfig()
    {
        var root = TestPaths.FindSolutionRoot();
        var path = System.IO.Path.Combine(
            root,
            "src",
            "AuswertungPro.Next.UI",
            "Config",
            "devis_mappings.json");

        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<DevisMappingConfig>(json, JsonOptions)
               ?? throw new InvalidOperationException("devis_mappings.json konnte nicht gelesen werden.");
    }

    private sealed class TempConfig : IDisposable
    {
        public TempConfig(string json)
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"{Guid.NewGuid():N}.json");
            File.WriteAllText(Path, json);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (File.Exists(Path))
                File.Delete(Path);
        }
    }
}
