using System;
using System.Collections.Generic;
using System.Linq;

namespace AuswertungPro.Next.Application.UseCases.CodingSuggestions;

/// <summary>
/// Threadsichere In-Memory-Implementierung von <see cref="ICodingSuggestionRegistry"/>.
/// Lebt als Singleton fuer die Dauer des Programmlaufs; ein Neustart setzt sie zurueck.
/// </summary>
public sealed class CodingSuggestionRegistry : ICodingSuggestionRegistry
{
    private readonly object _gate = new();
    private readonly Dictionary<string, CodingSuggestionRun> _runs = new(StringComparer.OrdinalIgnoreCase);

    public event Action? Geaendert;

    public void Merke(string haltung, CodingSuggestionSet set)
    {
        ArgumentNullException.ThrowIfNull(set);
        var key = (haltung ?? string.Empty).Trim();
        if (key.Length == 0)
            return;

        lock (_gate)
            _runs[key] = new CodingSuggestionRun(key, DateTimeOffset.Now, set);

        Geaendert?.Invoke();
    }

    public IReadOnlyList<CodingSuggestionRun> Heute()
    {
        lock (_gate)
            return _runs.Values.OrderByDescending(r => r.Zeitpunkt).ToList();
    }
}
