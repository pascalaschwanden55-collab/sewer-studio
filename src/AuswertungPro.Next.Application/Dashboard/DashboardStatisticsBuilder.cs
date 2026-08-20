using System.Globalization;
using AuswertungPro.Next.Application.Costs;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.Domain.VsaCatalog;

namespace AuswertungPro.Next.Application.Dashboard;

public sealed record DashboardBucket(string Key, string Label, int Count, double Percent)
{
    public DashboardBucket(string label, int count, double percent)
        : this(label, label, count, percent)
    {
    }
}

public sealed record DashboardCostBucket(string Key, string Label, int Count, decimal Cost, double Percent)
{
    public DashboardCostBucket(string label, int count, decimal cost, double percent)
        : this(label, label, count, cost, percent)
    {
    }
}

public sealed record ZustandBucket(string Key, string Label, int Count, double Percent);

public sealed record ZustandVerteilung(IReadOnlyList<ZustandBucket> Buckets)
{
    public int Total => Buckets.Sum(b => b.Count);
}

public sealed record DashboardStatistics(
    int HoldingCount,
    int SchachtCount,
    double TotalLengthMeters,
    decimal TotalCost,
    ZustandVerteilung Haltungen,
    ZustandVerteilung Schaechte,
    IReadOnlyList<DashboardBucket> TopSchaeden,
    IReadOnlyList<DashboardCostBucket> HaltungDnCosts,
    int SanierenHaltungen,
    int HaltungenGesamt,
    int SchaechteMitMassnahmen,
    int DringendCount,
    int OhneZustandCount)
{
    // Additiv ergaenzt (2026-08-20). Bewusst als init-Eigenschaften und nicht als
    // weitere Positionsparameter — sonst muesste jeder bestehende Aufrufer angepasst werden.

    /// <summary>Nettokosten nur der Haltungen.</summary>
    public decimal HaltungSanierungsKosten { get; init; }

    /// <summary>Nettokosten nur der Schaechte.</summary>
    public decimal SchachtSanierungsKosten { get; init; }

    /// <summary>
    /// Schaechte mit Sanierungsentscheid "Ja". Schaechte haben kein eigenes
    /// Ja/Nein-Feld; "Ja" gilt bei eingetragener Massnahme ODER Kosten ueber 0.
    /// Ein Schacht mit Massnahme, aber noch ohne Preis, zaehlt damit mit.
    /// </summary>
    public int SchaechteSanierenJa { get; init; }

    /// <summary>Gesamtzahl der Schaechte — Nenner zu <see cref="SchaechteSanierenJa"/>.</summary>
    public int SchaechteGesamt => SchachtCount;

    /// <summary>Mengen der Sanierungsverfahren der HALTUNGEN (Liner, Kurzliner, Manschetten).</summary>
    public IReadOnlyList<RehabilitationQuantity> Sanierungsverfahren { get; init; } = [];

    public bool HasVerfahren => Sanierungsverfahren.Count > 0;

    /// <summary>Haltungskosten als Text — fest de-CH, damit CHF ueberall gleich aussieht.</summary>
    public string HaltungSanierungsKostenText => FormatChf(HaltungSanierungsKosten);

    /// <summary>Schachtkosten als Text.</summary>
    public string SchachtSanierungsKostenText => FormatChf(SchachtSanierungsKosten);

    private static string FormatChf(decimal value)
        => Math.Round(value, 0, MidpointRounding.AwayFromZero)
            .ToString("N0", CultureInfo.GetCultureInfo("de-CH"));

    public bool HasData => HoldingCount > 0 || SchachtCount > 0;
    public bool HasHoldings => HoldingCount > 0;

    // Kompatibilitaet fuer bestehende Preview-/Overview-Bindings bis zur UI-Migration.
    public int TotalHoldings => HoldingCount;
    public IReadOnlyList<DashboardBucket> DamageGroups => TopSchaeden;
    public IReadOnlyList<DashboardCostBucket> DnCostGroups => HaltungDnCosts;
    public IReadOnlyList<DashboardBucket> ConditionClasses =>
        Haltungen.Buckets.Select(b => new DashboardBucket(b.Key, b.Label, b.Count, b.Percent)).ToList();
}

public static class DashboardStatisticsBuilder
{
    private static readonly string[] ZustandOrder = ["0", "1", "2", "3", "4", "ohne"];

    private static readonly IReadOnlyDictionary<string, string> ZustandLabels =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["0"] = "Z0",
            ["1"] = "Z1",
            ["2"] = "Z2",
            ["3"] = "Z3",
            ["4"] = "Z4",
            ["ohne"] = "ZU"
        };

    private static readonly IReadOnlyDictionary<string, string> DamageShortLabels =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["BAA"] = "Verformung",
            ["BAB"] = "Riss",
            ["BAC"] = "Bruch",
            ["BAD"] = "Mauerwerk",
            ["BAE"] = "Moertel",
            ["BAF"] = "Oberflaeche",
            ["BAG"] = "Anschluss ragt ein",
            ["BAH"] = "Anschluss defekt",
            ["BAI"] = "Dichtungsmaterial",
            ["BAJ"] = "Rohrverbindung",
            ["BAK"] = "Innenauskleidung",
            ["BAL"] = "Reparatur defekt",
            ["BAM"] = "Schweissnaht",
            ["BAN"] = "Poroese Leitung",
            ["BAO"] = "Boden sichtbar",
            ["BAP"] = "Hohlraum",
            ["BBA"] = "Wurzeln",
            ["BBB"] = "Anhaftungen",
            ["BBC"] = "Ablagerung",
            ["BBD"] = "Boden dringt ein",
            ["BBE"] = "Hindernis",
            ["BBF"] = "Infiltration",
            ["BBG"] = "Exfiltration",
            ["BBH"] = "Ungeziefer"
        };

    public static DashboardStatistics Build(Project? project, ProjectCostStore? haltungCosts, ProjectCostStore? schachtCosts)
    {
        var holdings = project?.Data?.ToList() ?? new List<HaltungRecord>();
        var schaechte = project?.SchaechteData?.ToList() ?? new List<SchachtRecord>();
        var hCostMap = haltungCosts?.ByHolding ?? new Dictionary<string, HoldingCost>(StringComparer.OrdinalIgnoreCase);
        var sCostMap = schachtCosts?.ByHolding ?? new Dictionary<string, HoldingCost>(StringComparer.OrdinalIgnoreCase);

        var hVerteilung = BuildZustandVerteilung(
            holdings.Select(r => r.GetFieldValue(FieldKeys.ConditionClass)));
        var sVerteilung = BuildZustandVerteilung(
            schaechte.Select(r => r.GetFieldValue(FieldKeys.ConditionClass)));
        var haltungCost = hCostMap.Values.Sum(ResolveNetTotal);
        var schachtCost = sCostMap.Values.Sum(ResolveNetTotal);
        var totalCost = haltungCost + schachtCost;

        return new DashboardStatistics(
            holdings.Count,
            schaechte.Count,
            Math.Round(holdings.Sum(r =>
                ParseDouble(r.GetFieldValue(FieldKeys.HoldingLengthMeters)) ?? 0d), 2),
            totalCost,
            hVerteilung,
            sVerteilung,
            BuildDamageGroups(holdings),
            BuildDnCostGroups(holdings, hCostMap),
            holdings.Count(r => IsJa(r.GetFieldValue(FieldKeys.RenovationDecision))),
            holdings.Count,
            sCostMap.Values.Count(c => ResolveNetTotal(c) > 0m),
            CountKeys(hVerteilung, "0", "1") + CountKeys(sVerteilung, "0", "1"),
            CountKeys(hVerteilung, "ohne") + CountKeys(sVerteilung, "ohne"))
        {
            HaltungSanierungsKosten = haltungCost,
            SchachtSanierungsKosten = schachtCost,
            SchaechteSanierenJa = CountSchaechteSanieren(schaechte, sCostMap),
            // Bewusst nur die Haltungskosten: Schachtpositionen wuerden Rohrmeter
            // und Schachtstueck in derselben Zeile vermischen.
            Sanierungsverfahren = RehabilitationQuantityCalculator.Calculate(haltungCosts)
        };
    }

    /// <summary>
    /// Ein Schacht gilt als "Sanieren: Ja", wenn eine Massnahme eingetragen ist ODER
    /// Kosten hinterlegt sind. Ein eigenes Ja/Nein-Feld gibt es beim Schacht nicht.
    /// </summary>
    private static int CountSchaechteSanieren(
        IReadOnlyList<SchachtRecord> schaechte,
        IReadOnlyDictionary<string, HoldingCost> schachtCosts)
    {
        var count = 0;
        foreach (var schacht in schaechte)
        {
            if (!string.IsNullOrWhiteSpace(schacht.GetFieldValue("Massnahmen")))
            {
                count++;
                continue;
            }

            var nummer = (schacht.GetFieldValue("Schachtnummer") ?? string.Empty).Trim();
            if (nummer.Length > 0
                && schachtCosts.TryGetValue(nummer, out var cost)
                && ResolveNetTotal(cost) > 0m)
            {
                count++;
            }
        }

        return count;
    }

    public static DashboardStatistics Build(IEnumerable<HaltungRecord>? records)
    {
        var project = new Project();
        foreach (var record in records ?? Enumerable.Empty<HaltungRecord>())
            project.Data.Add(record);

        return Build(project, new ProjectCostStore(), new ProjectCostStore());
    }

    public static string NormalizeZustandsklasse(object? value)
    {
        var text = (value?.ToString() ?? string.Empty).Trim();
        if (text.Length == 0)
            return "ohne";

        var normalized = text.Replace(',', '.');
        if (double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
        {
            var rounded = (int)Math.Round(number, MidpointRounding.AwayFromZero);
            return rounded is >= 0 and <= 4 ? rounded.ToString(CultureInfo.InvariantCulture) : "ohne";
        }

        if (text.Length == 1 && text[0] is >= '0' and <= '4')
            return text;

        return "ohne";
    }

    private static ZustandVerteilung BuildZustandVerteilung(IEnumerable<string?> values)
    {
        var normalizedValues = values.Select(NormalizeZustandsklasse).ToList();
        var total = normalizedValues.Count;
        var counts = normalizedValues
            .GroupBy(v => v, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);

        return new ZustandVerteilung(
            ZustandOrder
                .Select(key => new ZustandBucket(
                    key,
                    ZustandLabels[key],
                    counts.GetValueOrDefault(key),
                    Percent(counts.GetValueOrDefault(key), total)))
                .ToList());
    }

    private static IReadOnlyList<DashboardBucket> BuildDamageGroups(IReadOnlyList<HaltungRecord> records)
    {
        var codes = records
            .SelectMany(EnumerateDamageCodes)
            .Select(NormalizeDamageGroup)
            .Where(IsDashboardDamageGroup)
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .ToList();

        var total = codes.Count;
        if (total == 0)
            return Array.Empty<DashboardBucket>();

        return codes
            .GroupBy(c => c, StringComparer.OrdinalIgnoreCase)
            .Select(g => new DashboardBucket(g.Key, FormatDamageLabel(g.Key), g.Count(), Percent(g.Count(), total)))
            .OrderByDescending(b => b.Count)
            .ThenBy(b => b.Key, StringComparer.OrdinalIgnoreCase)
            .Take(6)
            .ToList();
    }

    private static IReadOnlyList<DashboardCostBucket> BuildDnCostGroups(
        IReadOnlyList<HaltungRecord> records,
        IReadOnlyDictionary<string, HoldingCost> haltungCosts)
    {
        var rows = records
            .Select(record =>
            {
                var holding = (record.GetFieldValue(FieldKeys.HoldingName) ?? string.Empty).Trim();
                var cost = TryGetCostByHolding(haltungCosts, holding);
                return new
                {
                    DnKey = NormalizeDnKey(record.GetFieldValue(FieldKeys.NominalDiameterMm)),
                    Cost = cost is null ? 0m : ResolveNetTotal(cost)
                };
            })
            .ToList();

        var totalCost = rows.Sum(r => r.Cost);
        return rows
            .GroupBy(r => r.DnKey, StringComparer.OrdinalIgnoreCase)
            .Select(g => new DashboardCostBucket(
                g.Key,
                g.Key == "?" ? "DN ?" : $"DN {g.Key}",
                g.Count(),
                g.Sum(x => x.Cost),
                Percent(g.Sum(x => x.Cost), totalCost)))
            .OrderBy(b => DnSortKey(b.Key))
            .ThenBy(b => b.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static HoldingCost? TryGetCostByHolding(IReadOnlyDictionary<string, HoldingCost> haltungCosts, string holding)
    {
        if (string.IsNullOrWhiteSpace(holding))
            return null;

        if (haltungCosts.TryGetValue(holding, out var direct))
            return direct;

        return haltungCosts.FirstOrDefault(kvp =>
            string.Equals(kvp.Key, holding, StringComparison.OrdinalIgnoreCase)).Value;
    }

    private static IEnumerable<string> EnumerateDamageCodes(HaltungRecord record)
    {
        foreach (var entry in record.Protocol?.Current?.Entries ?? Enumerable.Empty<ProtocolEntry>())
        {
            if (!entry.IsDeleted && !string.IsNullOrWhiteSpace(entry.Code))
                yield return entry.Code;
        }

        if (record.ProtocolEntry is { IsDeleted: false } legacy && !string.IsNullOrWhiteSpace(legacy.Code))
            yield return legacy.Code;

        foreach (var finding in record.VsaFindings)
        {
            if (!string.IsNullOrWhiteSpace(finding.KanalSchadencode))
                yield return finding.KanalSchadencode;
        }
    }

    private static string NormalizeDamageGroup(string? code)
    {
        var text = new string((code ?? string.Empty).Trim().ToUpperInvariant().TakeWhile(char.IsLetterOrDigit).ToArray());
        if (text.Length == 0)
            return string.Empty;

        return text.Length <= 3 ? text : text[..3];
    }

    private static bool IsDashboardDamageGroup(string code)
    {
        if (code.Length != 3)
            return false;

        if (!code.StartsWith("BA", StringComparison.OrdinalIgnoreCase)
            && !code.StartsWith("BB", StringComparison.OrdinalIgnoreCase))
            return false;

        return VsaCodeTree.Groups.TryGetValue(code[..2], out var group) && group.Codes.ContainsKey(code);
    }

    private static string FormatDamageLabel(string code)
    {
        var label = DamageShortLabels.TryGetValue(code, out var shortLabel)
            ? shortLabel
            : VsaCodeTree.LookupLabel(code);

        return string.IsNullOrWhiteSpace(label)
            ? code
            : $"{code} ({label})";
    }

    private static string NormalizeDnKey(string? value)
    {
        var dn = ParseDouble(value);
        if (dn is null || dn <= 0)
            return "?";

        return Math.Round(dn.Value, 0).ToString("0", CultureInfo.InvariantCulture);
    }

    private static int DnSortKey(string key)
        => int.TryParse(key, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : int.MaxValue;

    private static int CountKeys(ZustandVerteilung verteilung, params string[] keys)
    {
        var set = keys.ToHashSet(StringComparer.Ordinal);
        return verteilung.Buckets.Where(b => set.Contains(b.Key)).Sum(b => b.Count);
    }

    private static bool IsJa(string? value)
        => string.Equals((value ?? string.Empty).Trim(), "Ja", StringComparison.OrdinalIgnoreCase);

    private static decimal ResolveNetTotal(HoldingCost? cost)
        => TablePauschaleCostHelper.ResolveNetTotal(cost);

    private static double Percent(int count, int total)
        => total <= 0 ? 0d : Math.Round(count * 100d / total, 1);

    private static double Percent(decimal value, decimal total)
        => total <= 0m ? 0d : Math.Round((double)(value * 100m / total), 1);

    private static double? ParseDouble(string? value)
    {
        var text = NormalizeNumber(value);
        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }

    private static string NormalizeNumber(string? value)
        => (value ?? string.Empty).Trim().Replace("'", string.Empty).Replace(" ", string.Empty).Replace(',', '.');
}
