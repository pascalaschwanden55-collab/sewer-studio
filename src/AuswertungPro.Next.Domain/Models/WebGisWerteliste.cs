using System.Collections.ObjectModel;

namespace AuswertungPro.Next.Domain.Models;

/// <summary>
/// Eine Auswahlliste des WebGIS (GEONIS Uri), wie sie die Maske zeigt. Gespeichert wird in
/// SewerStudio genau eine dieser Beschriftungen (Entscheid Pascal 23.09.2026): Was hier steht,
/// geht so in die Trigonet-Datenbank.
///
/// <see cref="Normalisieren"/> erkennt Alt- und Importschreibweisen (gefaltet, ueber wenige
/// belegte Aliase oder ueber das bestehende Vokabular als Vorstufe). Was keiner Beschriftung
/// eindeutig entspricht, bleibt UNVERAENDERT stehen — nie geraten, nie geloescht.
/// </summary>
public sealed class WebGisWerteliste
{
    private readonly IReadOnlyDictionary<string, string> _aliase;
    private readonly Func<string?, string>? _vorstufe;

    public WebGisWerteliste(IEnumerable<string> beschriftungen,
        IReadOnlyDictionary<string, string>? aliase = null, Func<string?, string>? vorstufe = null)
    {
        ArgumentNullException.ThrowIfNull(beschriftungen);
        Werte = new ReadOnlyCollection<string>(beschriftungen
            .Where(b => !string.IsNullOrWhiteSpace(b)).Distinct(StringComparer.Ordinal).ToList());
        Auswahl = new ReadOnlyCollection<string>(new[] { "" }.Concat(Werte).ToList());
        var gefaltet = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (von, nach) in aliase ?? new Dictionary<string, string>())
        {
            if (!Werte.Contains(nach, StringComparer.Ordinal))
                throw new InvalidOperationException($"Alias «{von}» zeigt auf «{nach}», das nicht in der WebGIS-Liste steht.");
            gefaltet[WebGisBegriffe.Falte(von)] = nach;
        }
        _aliase = gefaltet;
        _vorstufe = vorstufe;
    }

    /// <summary>Die Beschriftungen des WebGIS, in der Reihenfolge der Maske, ohne Leereintrag.</summary>
    public IReadOnlyList<string> Werte { get; }

    /// <summary>Leer plus <see cref="Werte"/> — fuer die Auswahl im Programm.</summary>
    public IReadOnlyList<string> Auswahl { get; }

    /// <summary>True fuer leer oder eine Beschriftung der Liste (zeichengenau).</summary>
    public bool Kennt(string? wert)
    {
        var text = (wert ?? "").Trim();
        return text.Length == 0 || Werte.Contains(text, StringComparer.Ordinal);
    }

    /// <summary>Die WebGIS-Beschriftung zum Wert, "" fuer leer, sonst der Text unveraendert.</summary>
    public string Normalisieren(string? wert)
    {
        var text = (wert ?? "").Trim();
        if (text.Length == 0) return "";
        if (Finde(text) is { } direkt) return direkt;
        if (_vorstufe is not null)
        {
            var vor = (_vorstufe(text) ?? "").Trim();
            if (vor.Length > 0 && !string.Equals(vor, text, StringComparison.Ordinal) && Finde(vor) is { } ueber)
                return ueber;
        }
        return text;
    }

    private string? Finde(string text)
    {
        if (Werte.Contains(text, StringComparer.Ordinal)) return text;
        var gefaltet = WebGisBegriffe.Falte(text);
        var treffer = Werte.Where(w => WebGisBegriffe.Falte(w) == gefaltet).Take(2).ToList();
        if (treffer.Count == 1) return treffer[0];
        return _aliase.TryGetValue(gefaltet, out var alias) ? alias : null;
    }
}
