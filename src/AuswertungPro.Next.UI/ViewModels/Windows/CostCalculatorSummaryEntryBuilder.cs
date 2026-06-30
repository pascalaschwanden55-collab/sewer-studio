using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Output.Offers;

namespace AuswertungPro.Next.UI.ViewModels.Windows;

public static class CostCalculatorSummaryEntryBuilder
{
    public static Dictionary<string, string> BuildOwnerLookup(
        IReadOnlyList<HaltungRecord>? projectRecords,
        HaltungRecord? haltungRecord)
    {
        var owners = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (projectRecords is not null)
        {
            foreach (var record in projectRecords)
                AddOwner(owners, record);
        }

        if (haltungRecord is not null)
            AddOwner(owners, haltungRecord);

        return owners;
    }

    public static List<CostSummaryEntry> Build(
        HoldingCost currentHoldingCost,
        IReadOnlyDictionary<string, string> ownerByHolding)
    {
        var currentHolding = (currentHoldingCost.Holding ?? "").Trim();
        if (string.IsNullOrWhiteSpace(currentHolding) || !HasSelectedLines(currentHoldingCost))
            return new List<CostSummaryEntry>();

        return new List<CostSummaryEntry>
        {
            new()
            {
                Holding = currentHolding,
                Owner = ResolveOwnerForHolding(currentHoldingCost.Holding, ownerByHolding),
                Cost = currentHoldingCost
            }
        };
    }

    private static void AddOwner(IDictionary<string, string> owners, HaltungRecord record)
    {
        var holding = (record.GetFieldValue("Haltungsname") ?? "").Trim();
        if (string.IsNullOrWhiteSpace(holding))
            return;

        var owner = (record.GetFieldValue("Eigentuemer") ?? "").Trim();
        if (string.IsNullOrWhiteSpace(owner))
            return;

        owners[holding] = owner;
    }

    private static string ResolveOwnerForHolding(
        string? holding,
        IReadOnlyDictionary<string, string> ownerByHolding)
    {
        var key = (holding ?? "").Trim();
        if (key.Length == 0)
            return "Unbekannt";

        return ownerByHolding.TryGetValue(key, out var owner) && !string.IsNullOrWhiteSpace(owner)
            ? owner.Trim()
            : "Unbekannt";
    }

    private static bool HasSelectedLines(HoldingCost cost)
        => cost.Measures.Any(m => m.Lines.Any(l => l.Selected));
}
