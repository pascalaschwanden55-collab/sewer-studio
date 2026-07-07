using System;
using System.Collections.Generic;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.DataPage;

/// <summary>
/// Standard-Implementierung: geht ueber alle Haltungen und ruft <see cref="SanierungCostFieldMapper.SyncRecord"/>.
/// Holding-Schluessel = Feld <c>Haltungsname</c> (getrimmt), Lookup case-insensitiv.
/// </summary>
public sealed class DerivedCostFieldSynchronizer : IDerivedCostFieldSynchronizer
{
    public int Sync(Project project, ProjectCostStore store)
    {
        if (project?.Data is null)
            return 0;

        // Store case-insensitiv nach Haltungsname aufloesen (ByHolding-Key == Haltungsname).
        var byName = new Dictionary<string, HoldingCost>(StringComparer.OrdinalIgnoreCase);
        if (store?.ByHolding is not null)
        {
            foreach (var kv in store.ByHolding)
            {
                var key = (kv.Key ?? "").Trim();
                if (key.Length > 0)
                    byName[key] = kv.Value;
            }
        }

        var changed = 0;
        foreach (var rec in project.Data)
        {
            var key = (rec.GetFieldValue("Haltungsname") ?? "").Trim();
            byName.TryGetValue(key, out var cost);
            if (SanierungCostFieldMapper.SyncRecord(rec, cost))
                changed++;
        }
        return changed;
    }
}
