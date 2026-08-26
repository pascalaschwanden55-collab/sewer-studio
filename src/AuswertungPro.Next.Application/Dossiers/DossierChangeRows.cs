using System;

using AuswertungPro.Next.Domain.Models.Dossiers;

namespace AuswertungPro.Next.Application.Dossiers;

/// <summary>
/// Gemeinsame Inhaltsregel fuer das Aenderungswesen. Die Word-Vorlage zeigt
/// immer eine leere Grundzeile; eine solche reine Eingabehilfe gehoert aber
/// nicht als fachlicher Eintrag in die gespeicherte Dossierdatei.
/// </summary>
public static class DossierChangeRows
{
    public static void EnsureStarter(DossierDefinition dossier)
    {
        ArgumentNullException.ThrowIfNull(dossier);
        dossier.Changes ??= [];
        dossier.Changes.RemoveAll(row => row is null);

        if (dossier.Changes.Count == 0)
            dossier.Changes.Add(new DossierChangeRow());
    }

    /// <summary>Entfernt nur vollstaendig leere Zeilen und liefert ihre Anzahl.</summary>
    public static int RemoveEmpty(DossierDefinition dossier)
    {
        ArgumentNullException.ThrowIfNull(dossier);
        dossier.Changes ??= [];

        return dossier.Changes.RemoveAll(row => row is null || !HasContent(row));
    }

    public static bool HasContent(DossierChangeRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        return !string.IsNullOrWhiteSpace(row.Version)
            || !string.IsNullOrWhiteSpace(row.Date)
            || !string.IsNullOrWhiteSpace(row.Visum)
            || !string.IsNullOrWhiteSpace(row.Change);
    }
}
