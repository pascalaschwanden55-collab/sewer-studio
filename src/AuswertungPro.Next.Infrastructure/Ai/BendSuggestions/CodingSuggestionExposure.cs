using System.Collections.Concurrent;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Application.UseCases.BendSuggestions;

namespace AuswertungPro.Next.Infrastructure.Ai.BendSuggestions;

/// <summary>
/// Sitzungsgedaechtnis der angesehenen Vorschlagslisten. Lebt nur im
/// Programmlauf und ist threadsicher; nichts wird auf Platte geschrieben —
/// ein Neustart setzt bewusst zurueck (siehe Vertrag).
/// </summary>
public sealed class CodingSuggestionExposure : ICodingSuggestionExposure
{
    private readonly ConcurrentDictionary<string, byte> _exposed =
        new(StringComparer.OrdinalIgnoreCase);

    public void MarkExposed(string haltung)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(haltung);
        var key = EvalContaminationGuard.NormalizeHaltungKey(haltung);
        if (key is null)
            return;
        _exposed.TryAdd(key, 0);
    }

    public bool WasExposed(string haltung)
    {
        if (string.IsNullOrWhiteSpace(haltung))
            return false;
        var key = EvalContaminationGuard.NormalizeHaltungKey(haltung);
        return key is not null && _exposed.ContainsKey(key);
    }
}
