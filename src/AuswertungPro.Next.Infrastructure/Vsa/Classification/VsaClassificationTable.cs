using System.Text.Json;

namespace AuswertungPro.Next.Infrastructure.Vsa.Classification;

public sealed class VsaClassificationTable
{
    public double DefaultMinLength_m { get; set; } = 3.0;
    public List<VsaRule> Rules { get; set; } = new();

    public sealed class VsaRule
    {
        public string Code { get; set; } = "";
        public int? EZD { get; set; }
        public int? EZS { get; set; }
        public int? EZB { get; set; }
    }

    public static VsaClassificationTable LoadFromFile(string path)
    {
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<VsaClassificationTable>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? new VsaClassificationTable();
    }

    public VsaRule? Find(string code)
    {
        var norm = VsaEvaluationService.NormalizeCode(code);
        var exact = Rules.FirstOrDefault(r => string.Equals(VsaEvaluationService.NormalizeCode(r.Code), norm, StringComparison.OrdinalIgnoreCase));
        if (exact is not null)
            return exact;

        if (norm.Length > 3)
        {
            var shortCode = norm.Substring(0, 3);
            return Rules.FirstOrDefault(r => string.Equals(VsaEvaluationService.NormalizeCode(r.Code), shortCode, StringComparison.OrdinalIgnoreCase));
        }

        return null;
    }
}
