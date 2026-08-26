using System;

using AuswertungPro.Next.Domain.Models.Dossiers;

namespace AuswertungPro.Next.Application.Dossiers;

/// <summary>
/// Gemeinsame Regel fuer die sichtbare Grundzeile der Eigentuemertabelle.
/// Die Eingabehilfe ist im Editor vorhanden, wird aber leer nicht gespeichert.
/// </summary>
public static class DossierOwnerRows
{
    public static void EnsureStarter(DossierDefinition dossier)
    {
        ArgumentNullException.ThrowIfNull(dossier);
        dossier.Owners ??= [];
        dossier.Owners.RemoveAll(row => row is null);

        if (dossier.Owners.Count == 0)
            dossier.Owners.Add(new DossierOwnerRow());
    }

    /// <summary>Entfernt nur vollstaendig leere Zeilen und liefert ihre Anzahl.</summary>
    public static int RemoveEmpty(DossierDefinition dossier)
    {
        ArgumentNullException.ThrowIfNull(dossier);
        dossier.Owners ??= [];

        return dossier.Owners.RemoveAll(row => row is null || !row.HasContent);
    }
}
