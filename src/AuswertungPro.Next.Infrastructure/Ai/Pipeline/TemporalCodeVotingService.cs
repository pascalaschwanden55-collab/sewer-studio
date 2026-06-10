using System;
using System.Collections.Generic;
using System.Linq;
using AuswertungPro.Next.Application.Ai;

namespace AuswertungPro.Next.Infrastructure.Ai.Pipeline;

/// <summary>
/// Mehrheits-Fenster ueber die letzten N Klassifikator-Entscheidungen:
/// Ein Code ist bestaetigt, sobald er in mindestens <see cref="MinAgreement"/>
/// der letzten <see cref="WindowSize"/> Entscheidungen auftritt UND die
/// Treffer innerhalb von <see cref="MeterRadius"/> Metern liegen.
/// Einzelbild-Ausreisser (1x BAB mitten in LEER-Frames) kommen so nie durch.
/// </summary>
public sealed class TemporalCodeVotingService : ITemporalCodeVotingService
{
    private readonly record struct Entry(string? Code, double Meter);

    private readonly Queue<Entry> _window = new();

    /// <summary>Fenstergroesse in Frame-Entscheidungen.</summary>
    public int WindowSize { get; }

    /// <summary>Mindestanzahl uebereinstimmender Entscheidungen im Fenster.</summary>
    public int MinAgreement { get; }

    /// <summary>Max. Meterabstand zwischen den uebereinstimmenden Entscheidungen.</summary>
    public double MeterRadius { get; }

    public TemporalCodeVotingService(int windowSize = 3, int minAgreement = 2, double meterRadius = 1.5)
    {
        if (windowSize < 1) throw new ArgumentOutOfRangeException(nameof(windowSize));
        if (minAgreement < 1 || minAgreement > windowSize) throw new ArgumentOutOfRangeException(nameof(minAgreement));
        WindowSize = windowSize;
        MinAgreement = minAgreement;
        MeterRadius = meterRadius;
    }

    public string? RegisterAndVote(string? code, double meter)
    {
        var normalized = string.IsNullOrWhiteSpace(code) ? null : code.Trim().ToUpperInvariant();
        _window.Enqueue(new Entry(normalized, meter));
        while (_window.Count > WindowSize)
            _window.Dequeue();

        if (normalized is null)
            return null;

        // Treffer desselben Codes im Fenster, beschraenkt auf das Meterfenster
        // um die aktuelle Position (Kamera faehrt — alte Treffer weit hinter
        // der aktuellen Stelle duerfen nicht mitstimmen).
        var votes = _window.Count(e =>
            e.Code == normalized && Math.Abs(e.Meter - meter) <= MeterRadius);

        return votes >= MinAgreement ? normalized : null;
    }

    public void Reset() => _window.Clear();
}
