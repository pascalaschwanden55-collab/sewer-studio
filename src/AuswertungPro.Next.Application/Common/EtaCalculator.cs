using System;

namespace AuswertungPro.Next.Application.Common;

/// <summary>
/// EMA-geglaetteter Restzeit-Rechner: neue Delta-Raten fliessen mit 30 % ein, damit
/// kurze Aussetzer die Schaetzung nicht springen lassen. Stillstand laesst die Rate
/// gegen 0 laufen — dann verschwindet die Restzeit statt Unsinn anzuzeigen.
/// </summary>
public sealed class EtaCalculator : IEtaCalculator
{
    private const int WarmupMeldungen = 5;
    private static readonly TimeSpan WarmupZeit = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan StillstandNach = TimeSpan.FromSeconds(10);
    private const double EmaAlpha = 0.3;
    private const double MinRate = 1e-9;

    private int _meldungen;
    private long _letztErledigt;
    private TimeSpan _letztVerstrichen;
    private TimeSpan _letzterFortschritt; // Zeitpunkt der letzten echten Bewegung
    private double _emaRate;

    public EtaErgebnis MeldeFortschritt(long erledigt, long gesamt, TimeSpan verstrichen)
    {
        var deltaZeit = (verstrichen - _letztVerstrichen).TotalSeconds;
        if (deltaZeit > 0)
        {
            var deltaErledigt = Math.Max(0, erledigt - _letztErledigt);
            if (deltaErledigt > 0)
                _letzterFortschritt = verstrichen;
            var momentanRate = deltaErledigt / deltaZeit;
            _emaRate = _meldungen == 0 ? momentanRate : EmaAlpha * momentanRate + (1 - EmaAlpha) * _emaRate;
            _letztErledigt = erledigt;
            _letztVerstrichen = verstrichen;
            _meldungen++;
        }

        if (gesamt <= 0 || _meldungen < WarmupMeldungen || verstrichen < WarmupZeit)
            return new EtaErgebnis(null, null);

        var rest = gesamt - erledigt;
        if (rest <= 0)
            return new EtaErgebnis(TimeSpan.Zero, _emaRate);

        // Stillstand: seit laengerem kein Fortschritt -> keine serioese Restzeit mehr anzeigen
        // (die EMA allein wuerde nur langsam fallen und absurde Werte liefern).
        if (verstrichen - _letzterFortschritt >= StillstandNach)
            return new EtaErgebnis(null, 0d);

        return _emaRate <= MinRate
            ? new EtaErgebnis(null, 0d)
            : new EtaErgebnis(TimeSpan.FromSeconds(rest / _emaRate), _emaRate);
    }
}
