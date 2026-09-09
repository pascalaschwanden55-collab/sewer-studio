using System;
using System.Collections.Generic;
using System.Linq;

namespace AuswertungPro.Next.Application.Video;

/// <summary>Woraus der Meterstand stammt. Eine Grobortung soll nicht wie eine Messung aussehen.</summary>
public enum VideoMeterQuelle
{
    /// <summary>Weder Stuetzstellen noch eine brauchbare Laenge — es wird nicht geraten.</summary>
    Unbekannt,

    /// <summary>Nur Haltungslaenge geteilt durch Videodauer. Grobortung.</summary>
    MittlereGeschwindigkeit,

    /// <summary>Codierte Beobachtungen als Stuetzstellen. Deutlich genauer.</summary>
    Stuetzstellen
}

/// <summary>Eine codierte Beobachtung: zu dieser Videozeit stand die Kamera bei diesem Meter.</summary>
public sealed record VideoMeterStuetzstelle(TimeSpan Zeit, double Meter);

/// <summary>
/// Rechnet eine Videozeit in einen Meterstand der Haltung um.
///
/// Zwischen zwei Stuetzstellen wird linear interpoliert; ausserhalb gilt die mittlere
/// Vorschubgeschwindigkeit (Haltungslaenge geteilt durch Videodauer). Ohne Stuetzstellen
/// bleibt nur diese Geschwindigkeit — eine Grobortung, die bei einer Fahrt mit Stopps
/// mehrere Meter danebenliegen kann. Fehlt beides, wird KEIN Wert geliefert: ein
/// erfundener Meterstand waere schlimmer als gar keiner.
///
/// Reine Rechnung ohne Oberflaeche, Datei- oder Netzzugriff.
/// </summary>
public sealed class VideoMeterSpur
{
    private readonly IReadOnlyList<VideoMeterStuetzstelle> _stuetzstellen;
    private readonly double? _laengeM;
    private readonly double? _meterProSekunde;
    private readonly TimeSpan? _dauer;

    private VideoMeterSpur(
        IReadOnlyList<VideoMeterStuetzstelle> stuetzstellen,
        double? laengeM,
        double? meterProSekunde,
        TimeSpan? dauer,
        VideoMeterQuelle quelle)
    {
        _stuetzstellen = stuetzstellen;
        _laengeM = laengeM;
        _meterProSekunde = meterProSekunde;
        _dauer = dauer;
        Quelle = quelle;
    }

    /// <summary>Woher der Meterstand kommt — gehoert in die Antwort, nicht nur ins Log.</summary>
    public VideoMeterQuelle Quelle { get; }

    public static VideoMeterSpur Baue(
        IEnumerable<VideoMeterStuetzstelle>? stuetzstellen,
        double? haltungslaengeM,
        TimeSpan? videodauer)
    {
        var bereinigt = Bereinige(stuetzstellen);
        var laenge = IstBrauchbar(haltungslaengeM) ? haltungslaengeM : null;

        double? geschwindigkeit = null;
        if (laenge is { } l && videodauer is { TotalSeconds: > 0 } dauer)
            geschwindigkeit = l / dauer.TotalSeconds;

        var quelle = bereinigt.Count > 0
            ? VideoMeterQuelle.Stuetzstellen
            : geschwindigkeit is not null
                ? VideoMeterQuelle.MittlereGeschwindigkeit
                : VideoMeterQuelle.Unbekannt;

        return new VideoMeterSpur(bereinigt, laenge, geschwindigkeit, videodauer, quelle);
    }

    /// <summary>Meterstand zu dieser Videozeit, oder <c>null</c>, wenn er nicht bestimmbar ist.</summary>
    public double? MeterBei(TimeSpan zeit)
    {
        var roh = RohwertBei(zeit);
        if (roh is not { } meter || double.IsNaN(meter) || double.IsInfinity(meter))
            return null;

        // Der Marker gehoert auf die Haltung: ein Vor- oder Nachlauf im Video darf ihn
        // nicht daneben schieben. Er bleibt dann am Anfang beziehungsweise am Ende stehen.
        if (_laengeM is { } laenge)
            meter = Math.Clamp(meter, 0.0, laenge);

        return meter;
    }

    private double? RohwertBei(TimeSpan zeit)
    {
        if (_stuetzstellen.Count == 0)
            return _meterProSekunde is { } v ? zeit.TotalSeconds * v : null;

        var erste = _stuetzstellen[0];
        var letzte = _stuetzstellen[^1];

        if (zeit <= erste.Zeit)
            return _meterProSekunde is { } vorlauf
                ? erste.Meter - (erste.Zeit - zeit).TotalSeconds * vorlauf
                : erste.Meter;

        if (zeit >= letzte.Zeit)
            return _meterProSekunde is { } nachlauf
                ? letzte.Meter + (zeit - letzte.Zeit).TotalSeconds * nachlauf
                : letzte.Meter;

        for (var i = 1; i < _stuetzstellen.Count; i++)
        {
            var rechts = _stuetzstellen[i];
            if (zeit > rechts.Zeit)
                continue;

            var links = _stuetzstellen[i - 1];
            var spanne = (rechts.Zeit - links.Zeit).TotalSeconds;
            if (spanne <= 0)
                return rechts.Meter;

            var anteil = (zeit - links.Zeit).TotalSeconds / spanne;
            return links.Meter + anteil * (rechts.Meter - links.Meter);
        }

        return letzte.Meter;
    }

    /// <summary>
    /// Umkehrung von <see cref="MeterBei"/>: Videozeit zu diesem Meterstand, oder
    /// <c>null</c>, wenn sie nicht bestimmbar ist. Gebraucht fuer den Rueckweg aus
    /// QGIS — der Benutzer klickt eine Stelle der Haltung an, das Video springt hin.
    /// </summary>
    public TimeSpan? ZeitBei(double meter)
    {
        if (double.IsNaN(meter) || double.IsInfinity(meter))
            return null;

        var roh = RohzeitBei(meter);
        if (roh is not { } sekunden || double.IsNaN(sekunden) || double.IsInfinity(sekunden))
            return null;

        // Das Ziel gehoert ins Video: Vor- und Nachlauf duerfen nicht hinausfuehren.
        sekunden = Math.Max(0.0, sekunden);
        if (_dauer is { TotalSeconds: > 0 } dauer)
            sekunden = Math.Min(sekunden, dauer.TotalSeconds);

        return TimeSpan.FromSeconds(sekunden);
    }

    private double? RohzeitBei(double meter)
    {
        if (_stuetzstellen.Count == 0)
            return _meterProSekunde is { } v and > 0.0 ? meter / v : null;

        var erste = _stuetzstellen[0];
        var letzte = _stuetzstellen[^1];

        if (meter <= erste.Meter)
            return _meterProSekunde is { } vorlauf and > 0.0
                ? erste.Zeit.TotalSeconds - (erste.Meter - meter) / vorlauf
                : erste.Zeit.TotalSeconds;

        if (meter >= letzte.Meter)
            return _meterProSekunde is { } nachlauf and > 0.0
                ? letzte.Zeit.TotalSeconds + (meter - letzte.Meter) / nachlauf
                : letzte.Zeit.TotalSeconds;

        for (var i = 1; i < _stuetzstellen.Count; i++)
        {
            var rechts = _stuetzstellen[i];
            if (meter > rechts.Meter)
                continue;

            var links = _stuetzstellen[i - 1];
            var spanne = rechts.Meter - links.Meter;
            // Stand die Kamera still, tragen mehrere Zeiten denselben Meter. Dann gilt
            // die fruehere: dort erreicht die Kamera diese Stelle zum ersten Mal.
            if (spanne <= 0.0)
                return links.Zeit.TotalSeconds;

            var anteil = (meter - links.Meter) / spanne;
            return links.Zeit.TotalSeconds + anteil * (rechts.Zeit - links.Zeit).TotalSeconds;
        }

        return letzte.Zeit.TotalSeconds;
    }

    /// <summary>
    /// Nach Zeit sortieren und nur monoton steigende Meterwerte behalten. Ein Tippfehler
    /// im Protokoll (Meter springt zurueck) darf den Marker nicht ruecklaufen lassen.
    /// </summary>
    private static List<VideoMeterStuetzstelle> Bereinige(IEnumerable<VideoMeterStuetzstelle>? quelle)
    {
        var ergebnis = new List<VideoMeterStuetzstelle>();
        if (quelle is null)
            return ergebnis;

        var sortiert = quelle
            .Where(s => s is not null && IstBrauchbar(s.Meter) && s.Meter >= 0.0)
            .OrderBy(s => s.Zeit)
            .ToList();

        foreach (var stelle in sortiert)
        {
            if (ergebnis.Count == 0)
            {
                ergebnis.Add(stelle);
                continue;
            }

            var letzte = ergebnis[^1];
            if (stelle.Zeit == letzte.Zeit)
                continue;
            if (stelle.Meter < letzte.Meter)
                continue;

            ergebnis.Add(stelle);
        }

        return ergebnis;
    }

    private static bool IstBrauchbar(double? wert)
        => wert is { } w && !double.IsNaN(w) && !double.IsInfinity(w) && w > 0.0;

    private static bool IstBrauchbar(double wert)
        => !double.IsNaN(wert) && !double.IsInfinity(wert);
}
