using System.Text.Json;
using AuswertungPro.Next.Application.Devis;
using AuswertungPro.Next.Domain.Models.Devis;

namespace AuswertungPro.Next.Infrastructure.Devis;

public sealed class DevisMappingService : IDevisMappingService
{
    private readonly string _configPath;
    private DevisMappingConfig? _config;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public DevisMappingService(string configPath)
    {
        _configPath = configPath;
    }

    public DevisMappingConfig LoadConfig()
    {
        if (_config is not null)
            return _config;

        if (!File.Exists(_configPath))
        {
            _config = new DevisMappingConfig();
            return _config;
        }

        var json = File.ReadAllText(_configPath);
        _config = JsonSerializer.Deserialize<DevisMappingConfig>(json, JsonOpts) ?? new DevisMappingConfig();
        return _config;
    }

    public MassnahmenEmpfehlung GetEmpfehlung(string schadensCode, string? char1, string? char2, int zustandsKlasse, int dn)
    {
        var config = LoadConfig();

        var passend = config.Mappings
            .Where(m => string.Equals(m.SchadensCode, schadensCode, StringComparison.OrdinalIgnoreCase))
            .Where(m => m.Charakterisierung1 is null || string.Equals(m.Charakterisierung1, char1, StringComparison.OrdinalIgnoreCase))
            .Where(m => m.Charakterisierung2 is null || string.Equals(m.Charakterisierung2, char2, StringComparison.OrdinalIgnoreCase))
            .Where(m => zustandsKlasse >= m.MinZustandsklasse && zustandsKlasse <= m.MaxZustandsklasse)
            .Where(m => (m.MinDN is null || dn >= m.MinDN) && (m.MaxDN is null || dn <= m.MaxDN))
            .OrderByDescending(m => m.Prioritaet)
            .FirstOrDefault();

        if (passend is null)
            return MassnahmenEmpfehlung.KeineEmpfehlung(schadensCode);

        return new MassnahmenEmpfehlung
        {
            Mapping = passend,
            Konfidenz = ConfidenceLevel.Medium,
            Begruendung = $"Schaden {schadensCode} Klasse {zustandsKlasse}, DN {dn}"
        };
    }
}
