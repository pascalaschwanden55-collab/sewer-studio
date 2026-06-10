using System.Globalization;
using System.Text.RegularExpressions;

namespace AuswertungPro.Next.Application.Ai.Training;

/// <summary>
/// Reine Entscheidungslogik fuer den Bau eines eval-freien VSA-Klassifikator-Datensatzes
/// (v1, am eingefrorenen Eval-Set ausgerichtet): Code-&gt;Klasse-Mapping, Frame-Dateiname
/// zerlegen, Haltungs-stratifizierter Split. KEINE Datei-I/O hier — die liegt im Tool
/// ClassifierDatasetBuilder. Getestet in ClassifierDatasetPlanTests.
/// </summary>
public static class ClassifierDatasetPlan
{
    /// <summary>
    /// Zielklassen v2 (Hauptcode-Ebene). BBA bewusst klein/schwach, aber im Eval vorhanden.
    /// Paket 5: BCA/BCC/BBC/BAA ergaenzt — die Pilot-Fehlmuster (Anschluss als BAC/BAI,
    /// Bogen als BAJ) brauchen diese Klassen im Modell.
    /// </summary>
    public static readonly IReadOnlySet<string> TargetClasses = new HashSet<string>(StringComparer.Ordinal)
    {
        "BCD", "BCE", "BDA", "BDD", "BAJ", "BAF", "BAB", "BAI", "BBB", "BBA", "LEER",
        "BCA", "BCC", "BBC", "BAA"
    };

    /// <summary>
    /// Bildet einen Code-Token (aus dem Dateinamen) auf eine Zielklasse ab:
    /// kein_schaden -&gt; LEER; sonst Hauptcode (erste 3 Zeichen), falls in der Whitelist.
    /// Alles andere (axial, schacht, AE…, Codes ausserhalb v1) -&gt; null = ausschliessen.
    /// </summary>
    public static string? MapCodeToClass(string? code)
    {
        if (string.IsNullOrWhiteSpace(code)) return null;
        var c = code.Trim();
        if (c.Equals("kein_schaden", StringComparison.OrdinalIgnoreCase)) return "LEER";
        if (c.Length < 3) return null;
        var main = c[..3].ToUpperInvariant();
        return TargetClasses.Contains(main) ? main : null;
    }

    // <haltung>_<zeit>s_<code>[_t+/-N].png  — Haltung kann '.' und '-' enthalten, aber kein '_'.
    private static readonly Regex FramePattern =
        new(@"^(?<haltung>.+?)_(?<zeit>[0-9.]+)s_(?<code>.+?)(_t[+-]\d+)?\.png$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>Zerlegt einen Frame-Dateinamen in Haltung/Zeit/Code und mappt die Klasse.</summary>
    public static bool TryParseFrame(string fileName, out FrameInfo info)
    {
        info = default!;
        if (string.IsNullOrWhiteSpace(fileName)) return false;
        var m = FramePattern.Match(fileName);
        if (!m.Success) return false;
        var zeit = double.TryParse(m.Groups["zeit"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var t) ? t : 0;
        var code = m.Groups["code"].Value;
        info = new FrameInfo(m.Groups["haltung"].Value, zeit, code, MapCodeToClass(code));
        return true;
    }

    /// <summary>
    /// Waehlt deterministisch (seed) die Haltungen fuer den val-Split. Stabiler Hash pro
    /// Haltung -&gt; reproduzierbar, kein ungeseedetes Random. Eine Haltung landet damit immer
    /// komplett in genau einem Split (kein Pipe-Leakage zwischen train/val).
    /// </summary>
    public static IReadOnlySet<string> SelectValHaltungen(IEnumerable<string> haltungen, double valFraction, int seed)
    {
        var ordered = haltungen
            .Distinct(StringComparer.Ordinal)
            .OrderBy(h => StableKey(h, seed))
            .ThenBy(h => h, StringComparer.Ordinal)
            .ToList();
        var valCount = (int)Math.Round(ordered.Count * valFraction, MidpointRounding.AwayFromZero);
        return new HashSet<string>(ordered.Take(valCount), StringComparer.Ordinal);
    }

    /// <summary>Haltungs-stratifizierter Split einer Frame-Liste (nutzt <see cref="SelectValHaltungen"/>).</summary>
    public static DatasetSplit SplitByHaltung(IEnumerable<FrameInfo> frames, double valFraction, int seed)
    {
        var list = frames.ToList();
        var valSet = SelectValHaltungen(list.Select(f => f.Haltung), valFraction, seed);
        var train = list.Where(f => !valSet.Contains(f.Haltung)).ToList();
        var val = list.Where(f => valSet.Contains(f.Haltung)).ToList();
        return new DatasetSplit(train, val);
    }

    private static int StableKey(string haltung, int seed)
    {
        unchecked
        {
            var acc = 17 + seed;
            foreach (var ch in haltung) acc = acc * 31 + ch;
            return acc & 0x7fffffff;
        }
    }
}

/// <summary>Ein geparster Frame: Haltung, Zeit (s), roher Code-Token, gemappte Zielklasse (oder null).</summary>
public sealed record FrameInfo(string Haltung, double TimeSeconds, string Code, string? TrainingClass);

/// <summary>Ergebnis des Haltungs-Splits.</summary>
public sealed record DatasetSplit(IReadOnlyList<FrameInfo> Train, IReadOnlyList<FrameInfo> Val);
