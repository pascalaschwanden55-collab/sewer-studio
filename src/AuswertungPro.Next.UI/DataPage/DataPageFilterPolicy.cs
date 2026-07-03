using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.DataPage;

/// <summary>
/// Filterzustand der Chip-Leiste ueber dem Haltungen-Grid. Der Filter wirkt
/// NUR auf die Sicht (ICollectionView) — die NR-Laufnummer bleibt die
/// Listenposition; Zeilen-Verschieben wird bei aktivem Filter gesperrt.
/// </summary>
public sealed record DataPageFilter(string? Zustandsklasse, bool NurMitVideo, bool NurMitSchaeden)
{
    public static readonly DataPageFilter Aus = new(null, false, false);

    public bool IstAktiv => Zustandsklasse is not null || NurMitVideo || NurMitSchaeden;

    /// <summary>Prueft, ob eine Haltung den aktiven Filter passiert.</summary>
    public bool Passt(HaltungRecord? record)
    {
        if (record is null)
            return false;
        if (!IstAktiv)
            return true;

        if (Zustandsklasse is not null
            && !string.Equals(
                (record.GetFieldValue("Zustandsklasse") ?? "").Trim(),
                Zustandsklasse,
                System.StringComparison.Ordinal))
            return false;

        if (NurMitVideo && string.IsNullOrWhiteSpace(record.GetFieldValue("Link")))
            return false;

        if (NurMitSchaeden && string.IsNullOrWhiteSpace(record.GetFieldValue("Primaere_Schaeden")))
            return false;

        return true;
    }
}
