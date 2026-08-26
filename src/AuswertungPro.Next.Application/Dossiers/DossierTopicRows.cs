using System;

using AuswertungPro.Next.Domain.Models.Dossiers;

namespace AuswertungPro.Next.Application.Dossiers;

/// <summary>
/// Stellt auch bei einem Gebiet ohne Standardthemen eine sichtbare, direkt
/// beschreibbare Tabellenzeile bereit. Eine unbenutzte Grundzeile wird vor dem
/// Speichern wieder entfernt.
/// </summary>
public static class DossierTopicRows
{
    public static void EnsureStarter(
        DossierAreaSettings? area,
        DossierDefinition dossier)
    {
        ArgumentNullException.ThrowIfNull(dossier);
        dossier.Topics ??= [];

        if (DossierTopicResolver.Resolve(area, dossier).Count == 0)
            dossier.Topics.Add(new DossierTopicRow());
    }

    /// <summary>
    /// Entfernt nur Zeilen ohne Titel und ohne Bemerkung. Ein Text ohne linke
    /// Beschriftung bleibt bewusst erlaubt: beide Tabellenzellen sind frei.
    /// </summary>
    public static int RemoveEmpty(DossierDefinition dossier)
    {
        ArgumentNullException.ThrowIfNull(dossier);
        dossier.Topics ??= [];

        return dossier.Topics.RemoveAll(row => row is null || !HasContent(row));
    }

    public static bool HasContent(DossierTopicRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        return !string.IsNullOrWhiteSpace(row.Title)
            || !string.IsNullOrWhiteSpace(row.Text);
    }
}
