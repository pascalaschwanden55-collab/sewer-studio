using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Vsa;

namespace AuswertungPro.Next.Application.Vsa;

/// <summary>
/// Berechnet Zustandsnoten (D/S/B) und Dringlichkeitszahlen.
/// Klassifizierung (Einzelzustände) ist datengetrieben über Mapping-Tabellen.
/// </summary>
public interface IVsaEvaluationService
{
    Result<IReadOnlyList<VsaConditionResult>> Evaluate(Project project);
    Result<string> Explain(Project project, HaltungRecord record);
}
