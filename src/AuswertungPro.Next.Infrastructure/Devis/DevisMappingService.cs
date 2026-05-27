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
        var candidateCodes = GetCandidateCodes(schadensCode);

        var passend = config.Mappings
            .Select(m => new
            {
                Mapping = m,
                CodeRank = candidateCodes.FindIndex(code => string.Equals(m.SchadensCode, code, StringComparison.OrdinalIgnoreCase))
            })
            .Where(x => x.CodeRank >= 0)
            .Where(x => x.Mapping.Charakterisierung1 is null || string.Equals(x.Mapping.Charakterisierung1, char1, StringComparison.OrdinalIgnoreCase))
            .Where(x => x.Mapping.Charakterisierung2 is null || string.Equals(x.Mapping.Charakterisierung2, char2, StringComparison.OrdinalIgnoreCase))
            .Where(x => zustandsKlasse >= x.Mapping.MinZustandsklasse && zustandsKlasse <= x.Mapping.MaxZustandsklasse)
            .Where(x => (x.Mapping.MinDN is null || dn >= x.Mapping.MinDN) && (x.Mapping.MaxDN is null || dn <= x.Mapping.MaxDN))
            .OrderBy(x => x.CodeRank)
            .ThenByDescending(x => x.Mapping.Prioritaet)
            .Select(x => x.Mapping)
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

    private static List<string> GetCandidateCodes(string schadensCode)
    {
        var code = schadensCode.Trim().ToUpperInvariant();
        if (code.Length == 0)
            return [];

        var candidates = new List<string> { code };

        if (code.Length > 3)
        {
            var baseCode = code[..3];
            if (!candidates.Contains(baseCode, StringComparer.OrdinalIgnoreCase))
                candidates.Add(baseCode);
        }

        return candidates;
    }
}
