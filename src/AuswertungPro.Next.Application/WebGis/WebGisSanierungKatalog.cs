using System;
using System.Collections.Generic;

namespace AuswertungPro.Next.Application.WebGis;

/// <summary>
/// Die Combo-Kataloge der Maske "Sanierungsmassnahme", wie das WebGIS sie liefert
/// (je refId die Liste Schluessel/Klartext). Damit wird der SewerStudio-Klartext
/// in den WebGIS-Schluessel uebersetzt — nie ueber Nummern, nur ueber den Text.
///
/// Abhaengige Listen: Das Verfahren haengt von der Art ab (getControlValues mit
/// filter=Art-Schluessel: Reparatur -> Vermoertelung/Roboter/..., Renovierung ->
/// Schlauch-/Kurzrohr-/..., Erneuerung -> Berst-/Rohrzieh-/...). Solche Listen
/// werden unter refId + Filter abgelegt und beim Aufloesen zuerst befragt.
/// </summary>
public sealed class WebGisSanierungKatalog
{
    private readonly Dictionary<string, List<(string Key, string Text)>> _listen = new(StringComparer.Ordinal);

    public void Setze(string refId, IEnumerable<(string Key, string Text)> eintraege, string? filter = null)
        => _listen[Schluesselname(refId, filter)] = new List<(string, string)>(eintraege);

    public bool Hat(string refId, string? filter = null) => _listen.ContainsKey(Schluesselname(refId, filter));

    public IReadOnlyList<(string Key, string Text)> Eintraege(string refId, string? filter = null)
        => _listen.TryGetValue(Schluesselname(refId, filter), out var l) ? l : Array.Empty<(string, string)>();

    /// <summary>
    /// Schluessel zum Klartext (getrimmt, Gross/Klein egal). Mit <paramref name="filter"/>
    /// wird AUSSCHLIESSLICH die abhaengige Liste (z.B. Verfahren je Art) befragt — fehlt
    /// sie, gibt es keinen Schluessel. Dieselbe Nummer bedeutet je Art etwas anderes; die
    /// ungefilterte Liste waere ein Raten (Pruefung 22.09.2026). Null, wenn der Text im
    /// WebGIS-Katalog nicht vorkommt — dann darf NICHT geraten werden.
    /// </summary>
    public string? Schluessel(string refId, string? text, string? filter = null)
    {
        var t = Normalisiert(text);
        if (t.Length == 0) return null;
        if (filter is not null)
            return _listen.TryGetValue(Schluesselname(refId, filter), out var gefiltert) ? Suche(gefiltert, t) : null;
        return _listen.TryGetValue(refId, out var l) ? Suche(l, t) : null;
    }

    private static string? Suche(List<(string Key, string Text)> l, string t)
    {
        foreach (var (key, wert) in l)
            if (string.Equals(Normalisiert(wert), t, StringComparison.OrdinalIgnoreCase)) return key;
        return null;
    }

    private static string Schluesselname(string refId, string? filter)
        => filter is null ? refId : refId + "|" + filter;

    /// <summary>Klartext zum Schluessel (fuer Bericht).</summary>
    public string? Text(string refId, string? key)
    {
        if (key is null || !_listen.TryGetValue(refId, out var l)) return null;
        foreach (var (k, wert) in l) if (k == key) return wert;
        return null;
    }

    private static string Normalisiert(string? s)
    {
        var t = (s ?? string.Empty).Trim();
        // Mehrfach-Leerzeichen und Leerzeichen um Trenner egalisieren ("Punktuell, Abzweig/Stutzen").
        return System.Text.RegularExpressions.Regex.Replace(t, @"\s*([,/])\s*", "$1")
            .Replace("  ", " ");
    }
}
