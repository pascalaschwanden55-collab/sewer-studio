using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace AuswertungPro.Next.Domain.Models
{
    public class VsaRule
    {
        public string Schadencode { get; set; } = string.Empty;
        public int? EZD { get; set; }
        public int? EZS { get; set; }
        public int? EZB { get; set; }
    }

    public class VsaRuleProvider
    {
        private readonly Dictionary<string, VsaRule> _rules;

        public VsaRuleProvider(string jsonPath)
        {
            _rules = new Dictionary<string, VsaRule>();
            try
            {
                var json = File.ReadAllText(jsonPath);
                var rules = JsonSerializer.Deserialize<List<VsaRule>>(json);
                if (rules != null)
                {
                    foreach (var rule in rules)
                    {
                        _rules[rule.Schadencode] = rule;
                    }
                }
            }
            catch (Exception)
            {
                // Korrupte oder fehlende JSON-Datei: leeres Regelwerk verwenden
            }
        }

        public VsaRule? GetRule(string schadencode)
        {
            _rules.TryGetValue(schadencode, out var rule);
            return rule;
        }
    }
}
