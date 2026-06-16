using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace AuswertungPro.Next.Infrastructure.Map;

/// <summary>
/// Universeller Abgleich: ordnet ein Schacht-Paar (in BELIEBIGER Reihenfolge) der
/// amtlich korrekten Haltung aus dem Abwasserkataster zu. Format-unabhaengig –
/// egal ob Inspektions- oder Dichtheitspruefungs-Protokoll, egal welche Firma.
/// </summary>
public interface IHaltungCadastreResolver
{
    /// <summary>Anzahl bekannter Haltungen im Index.</summary>
    int Count { get; }

    /// <summary>
    /// Loest ein konkretes Schacht-Paar auf. Liefert die amtliche Haltungs-Bezeichnung
    /// in KORREKTER Reihenfolge (korrigiert vertauschte Schaechte). False wenn unbekannt
    /// ODER mehrdeutig (mehrere Haltungen zwischen denselben Schaechten).
    /// </summary>
    bool TryResolvePair(string shaftA, string shaftB, out string canonicalHaltung);

    /// <summary>
    /// Universell: aus einer Menge Kandidaten-Schachtnummern (z.B. alle Zahlen nahe
    /// "Haltung"/"Schacht" im PDF) alle Paare finden, die im Kataster eine Haltung bilden.
    /// Genau ein Treffer = sichere Zuordnung; mehrere = mehrdeutig (Aufrufer entscheidet).
    /// </summary>
    IReadOnlyList<string> ResolveFromCandidates(IEnumerable<string> candidateShaftNumbers);

    /// <summary>
    /// Prueft NUR, ob ein Schacht-Paar (in beliebiger Reihenfolge) ueberhaupt eine
    /// amtliche Haltung im Kataster bildet. Anders als <see cref="TryResolvePair"/> auch
    /// dann true, wenn das Paar mehrdeutig ist (mehrere Haltungen). Dient als
    /// Plausibilitaets-Gate: Paare, die der Kataster nicht kennt, gelten als unzugeordnet.
    /// </summary>
    bool PairExists(string shaftA, string shaftB);
}

/// <summary>
/// In-Memory-Index ueber die eigenstaendige Kataster-Tabelle (TSV). Wird einmal aus der
/// XTF gebaut und FEST im SewerStudio-Ordner abgelegt, damit der Abgleich schnell ist und
/// die ~600 MB grosse XTF nicht bei jeder Verteilung neu geparst werden muss.
/// </summary>
public sealed class HaltungCadastreIndex : IHaltungCadastreResolver
{
    // pairKey ("864|865") -> distinkte amtliche Bezeichnungen ("865-864")
    private readonly Dictionary<string, HashSet<string>> _byPair;

    private HaltungCadastreIndex(Dictionary<string, HashSet<string>> byPair) => _byPair = byPair;

    public int Count => _byPair.Values.Sum(s => s.Count);

    /// <summary>
    /// Feste Ablage der Tabelle im SewerStudio-Ordner:
    /// %LOCALAPPDATA%\SewerStudio\map\abwasserkataster_haltungen.tsv
    /// (gleiche Konvention wie der Netz-Cache).
    /// </summary>
    public static string DefaultTablePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SewerStudio", "map", "abwasserkataster_haltungen.tsv");

    /// <summary>
    /// Stellt die feste Tabelle sicher (baut/aktualisiert sie aus der XTF, wenn sie fehlt
    /// oder veraltet ist) und laedt den Index. xtfPath darf null sein, wenn die Tabelle
    /// bereits existiert.
    /// </summary>
    public static HaltungCadastreIndex EnsureAndLoad(string? xtfPath, string? tablePath = null)
    {
        var table = string.IsNullOrWhiteSpace(tablePath) ? DefaultTablePath : tablePath!;

        if (!string.IsNullOrWhiteSpace(xtfPath) && File.Exists(xtfPath)
            && !HaltungCadastreExtractor.IsTableFresh(table, xtfPath!))
        {
            HaltungCadastreExtractor.BuildTable(xtfPath!, table);
        }

        return File.Exists(table) ? Load(table) : new HaltungCadastreIndex(new());
    }

    /// <summary>Laedt den Index aus einer bereits gebauten TSV-Tabelle.</summary>
    public static HaltungCadastreIndex Load(string tablePath)
    {
        var byPair = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (var h in HaltungCadastreExtractor.ReadTable(tablePath))
        {
            if (string.IsNullOrWhiteSpace(h.ShaftA) || string.IsNullOrWhiteSpace(h.ShaftB))
                continue;
            var key = PairKey(h.ShaftA, h.ShaftB);
            if (!byPair.TryGetValue(key, out var set))
                byPair[key] = set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            set.Add(h.Bezeichnung);
        }
        return new HaltungCadastreIndex(byPair);
    }

    public bool TryResolvePair(string shaftA, string shaftB, out string canonicalHaltung)
    {
        canonicalHaltung = "";
        if (string.IsNullOrWhiteSpace(shaftA) || string.IsNullOrWhiteSpace(shaftB))
            return false;
        if (_byPair.TryGetValue(PairKey(shaftA, shaftB), out var set) && set.Count == 1)
        {
            canonicalHaltung = set.First();
            return true;
        }
        return false;
    }

    public bool PairExists(string shaftA, string shaftB)
    {
        if (string.IsNullOrWhiteSpace(shaftA) || string.IsNullOrWhiteSpace(shaftB))
            return false;
        return _byPair.ContainsKey(PairKey(shaftA, shaftB));
    }

    public IReadOnlyList<string> ResolveFromCandidates(IEnumerable<string> candidateShaftNumbers)
    {
        var nums = candidateShaftNumbers
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Select(n => n.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var hits = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < nums.Count; i++)
            for (var j = i + 1; j < nums.Count; j++)
                if (_byPair.TryGetValue(PairKey(nums[i], nums[j]), out var set))
                    foreach (var b in set) hits.Add(b);

        return hits.ToList();
    }

    /// <summary>Reihenfolge-unabhaengiger Schluessel fuer ein Schacht-Paar.</summary>
    private static string PairKey(string a, string b)
    {
        var x = a.Trim().ToUpperInvariant();
        var y = b.Trim().ToUpperInvariant();
        return string.CompareOrdinal(x, y) <= 0 ? $"{x}|{y}" : $"{y}|{x}";
    }
}
