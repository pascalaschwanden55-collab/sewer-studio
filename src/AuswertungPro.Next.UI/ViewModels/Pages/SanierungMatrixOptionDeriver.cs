using System;
using System.Collections.Generic;

namespace AuswertungPro.Next.UI.ViewModels.Pages;

/// <summary>Die fuenf Zusatz-Haekchen einer Matrix-Zeile (Verkehrsdienst, Wasserhaltung,
/// Fraesen, Dichtheitspruefung, Dokumentation).</summary>
public readonly record struct SanierungMatrixOptionFlags(bool Vd, bool Wasser, bool Fraesen, bool Dichtheit, bool Doku);

/// <summary>
/// Leitet die Zusatz-Haekchen der Matrix-Zeile aus den im Detail AUSGEWAEHLTEN Positionen ab:
/// ein Haekchen ist gesetzt, wenn irgendeine ausgewaehlte Detail-Position den zugehoerigen
/// Katalog-ItemKey traegt. So bleibt die obere Tabelle mit der Detailliste synchron. Reine,
/// testbare Funktion (keine God-Class im ViewModel).
/// </summary>
public static class SanierungMatrixOptionDeriver
{
    public static SanierungMatrixOptionFlags Derive(
        IEnumerable<(string? ItemKey, bool Selected)> lines,
        string keyVd, string keyWasser, string keyFraesen, string keyDichtheit, string keyDoku)
    {
        var selected = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (itemKey, isSelected) in lines)
            if (isSelected && !string.IsNullOrWhiteSpace(itemKey))
                selected.Add(itemKey.Trim());

        return new SanierungMatrixOptionFlags(
            selected.Contains(keyVd),
            selected.Contains(keyWasser),
            selected.Contains(keyFraesen),
            selected.Contains(keyDichtheit),
            selected.Contains(keyDoku));
    }
}
