using System;

namespace AuswertungPro.Next.Application.Common;

/// <summary>Eine ETA-Schaetzung: Restzeit und aktuelle Rate; null = noch keine serioese Aussage.</summary>
public sealed record EtaErgebnis(TimeSpan? Restzeit, double? RateProSekunde);

/// <summary>
/// Restzeit-Rechner fuer lange Laeufe (Video-Pipeline, Batch-Import).
/// Die verstrichene Zeit wird IMMER hereingereicht (kein DateTime.Now intern),
/// damit die Logik deterministisch testbar bleibt. Pro Lauf eine frische Instanz.
/// </summary>
public interface IEtaCalculator
{
    /// <summary>Fortschritt melden; liefert die aktuelle Schaetzung (Warmup: erst nach
    /// mehreren Meldungen und ein paar Sekunden gibt es ein Ergebnis).</summary>
    EtaErgebnis MeldeFortschritt(long erledigt, long gesamt, TimeSpan verstrichen);
}
