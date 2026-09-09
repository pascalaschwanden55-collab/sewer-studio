namespace AuswertungPro.Next.Application.Import;

/// <summary>Hochrechnung je Schritt, erst nach drei erledigten Einheiten. Kein Gesamt-ETA.</summary>
public sealed class ImportRestzeitSchaetzer
{
    private string? _phase;
    private TimeSpan _start;
    private int _letzterStand;
    private int _startStand;
    private int _gesamt;
    private TimeSpan? _restzeit;

    public TimeSpan? Aktualisiere(ImportProgress fortschritt, TimeSpan laufzeit)
    {
        if (_phase != fortschritt.Phase || fortschritt.Current < _letzterStand
            || (_gesamt > 0 && _gesamt != fortschritt.Total))
        {
            _phase = fortschritt.Phase;
            _start = laufzeit;
            _startStand = Math.Max(0, fortschritt.Current);
            _letzterStand = 0;
            _restzeit = null;
        }
        _gesamt = fortschritt.Total;
        var erledigt = fortschritt.Current - _startStand;
        if (!ImportFortschrittText.IstBestimmt(fortschritt) || erledigt < 3
            || fortschritt.Current >= fortschritt.Total || laufzeit <= _start)
        {
            _letzterStand = fortschritt.Current;
            return _restzeit = null;
        }
        if (fortschritt.Current == _letzterStand)
            return _restzeit;
        _letzterStand = fortschritt.Current;
        var ticks = (laufzeit - _start).Ticks / (double)erledigt
            * (fortschritt.Total - fortschritt.Current);
        return _restzeit = ticks < TimeSpan.MaxValue.Ticks ? TimeSpan.FromTicks((long)ticks) : null;
    }
}
