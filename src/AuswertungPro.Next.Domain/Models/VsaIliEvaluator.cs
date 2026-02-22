using System.Collections.Generic;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Domain.Models
{


    public class VsaIliEvaluator
    {
        private readonly VsaRuleProvider _ruleProvider;

        public VsaIliEvaluator(VsaRuleProvider ruleProvider)
        {
            _ruleProvider = ruleProvider;
        }

        public void EvaluateFindings(List<VsaFinding> findings)
        {
            foreach (var finding in findings)
            {
                var rule = _ruleProvider.GetRule(finding.KanalSchadencode);
                if (rule != null)
                {
                    finding.EZD = rule.EZD;
                    finding.EZS = rule.EZS;
                    finding.EZB = rule.EZB;
                }
                else
                {
                    // Diagnostics: unknown Schadencode
                    System.Diagnostics.Debug.WriteLine($"[VSA] Unbekannter Schadencode: {finding.KanalSchadencode}");
                }
            }
        }
    }
}
