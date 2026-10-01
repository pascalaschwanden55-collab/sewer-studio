using System;
using System.Collections.Generic;

namespace AuswertungPro.Next.UI.Controls;

/// <summary>
/// Nova-Etappe 2: WPF-freie Knotenbewegung des Leitungsnetz-Hintergrunds.
/// Inventar 2: 60 Knoten, deterministisch gesaet (LCG 7 / 16807 mod 2^31-1), +-0,125 px je Bild.
/// Reine Rechnung ohne UI-Abhaengigkeit, damit der Code-Behind der Zeichnung nur noch abbildet.
/// </summary>
public sealed class NetzHintergrundModell
{
    public sealed record Knotenpunkt(double X, double Y, double R, double Vx, double Vy);
    public sealed record Verbindung(int A, int B, double Alpha);

    private readonly List<Knotenpunkt> _knoten = new();
    private readonly double _breite, _hoehe;
    private long _saat;

    public NetzHintergrundModell(int breite, int hoehe, int knoten = 60, int startwert = 7)
    {
        _breite = breite; _hoehe = hoehe; _saat = startwert;
        for (var i = 0; i < knoten; i++)
        {
            var x = Zufall() * breite; var y = Zufall() * hoehe;
            var r = 1.2 + Zufall() * 1.6;
            var vx = (Zufall() - 0.5) * 0.25; var vy = (Zufall() - 0.5) * 0.25;
            _knoten.Add(new Knotenpunkt(x, y, r, vx, vy));
        }
    }

    public IReadOnlyList<Knotenpunkt> Knoten => _knoten;

    private double Zufall()
    {
        _saat = (_saat * 16807L) % 2147483647L;
        return _saat / 2147483647.0;
    }

    /// <summary>Bewegt jeden Knoten um seine feste Geschwindigkeit und spiegelt sie am Rand.</summary>
    public void Schritt()
    {
        for (var i = 0; i < _knoten.Count; i++)
        {
            var k = _knoten[i];
            var (x, y, vx, vy) = (k.X + k.Vx, k.Y + k.Vy, k.Vx, k.Vy);
            if (x < 0 || x > _breite) { vx = -vx; x = Math.Clamp(x, 0, _breite); }
            if (y < 0 || y > _hoehe) { vy = -vy; y = Math.Clamp(y, 0, _hoehe); }
            _knoten[i] = k with { X = x, Y = y, Vx = vx, Vy = vy };
        }
    }

    /// <summary>Alle Knotenpaare unter <paramref name="maxAbstand"/>, mit linear abnehmendem Alpha.</summary>
    public IReadOnlyList<Verbindung> Verbindungen(double maxAbstand = 170)
    {
        var liste = new List<Verbindung>();
        for (var i = 0; i < _knoten.Count; i++)
            for (var j = i + 1; j < _knoten.Count; j++)
            {
                var d = Math.Sqrt(Math.Pow(_knoten[i].X - _knoten[j].X, 2) + Math.Pow(_knoten[i].Y - _knoten[j].Y, 2));
                if (d < maxAbstand) liste.Add(new Verbindung(i, j, 1 - d / maxAbstand));
            }
        return liste;
    }
}
