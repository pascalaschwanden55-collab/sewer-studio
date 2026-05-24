using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.Ai;

/// <summary>
/// Persistiert Human-in-the-loop Entscheidungen aus dem Codiermodus.
/// Wichtig: Aufrufer duerfen nur echte KI-Vorschlaege uebergeben, keine rein
/// manuellen Codierungen, sonst werden die KI-Metriken kuenstlich verfaelscht.
/// </summary>
public interface ICodingFeedbackRecorder
{
    Task RecordDecisionAsync(CodingEvent ev, string caseId, CancellationToken ct = default);
}
