using System.Collections.Generic;
using System.Linq;
using AuswertungPro.Next.Application.Lookup;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.DataPage;

/// <summary>
/// Uebersetzt Projektdatensaetze in die schlanke Sicht, mit der
/// <see cref="IStrassenUebernahme"/> rechnet. Die Regel selbst kennt weder
/// HaltungRecord noch SchachtRecord — dadurch bleibt sie ohne Oberflaeche
/// pruefbar.
/// </summary>
public static class StrassenUebernahmeMapper
{
    public const string StrassenFeld = "Strasse";
    public const string HaltungsnameFeld = "Haltungsname";
    public const string SchachtObenFeld = "Schacht_oben";
    public const string SchachtUntenFeld = "Schacht_unten";
    public const string SchachtnummerFeld = "Schachtnummer";

    public static IReadOnlyList<StrassenHaltung> Haltungen(IEnumerable<HaltungRecord>? records)
        => records is null
            ? []
            : records
                .Where(r => r is not null)
                .Select(r => new StrassenHaltung(
                    r.GetFieldValue(HaltungsnameFeld),
                    r.GetFieldValue(StrassenFeld),
                    r.GetFieldValue(SchachtObenFeld),
                    r.GetFieldValue(SchachtUntenFeld)))
                .ToList();

    public static IReadOnlyList<StrassenSchacht> Schaechte(IEnumerable<SchachtRecord>? records)
        => records is null
            ? []
            : records
                .Where(r => r is not null)
                .Select(r => new StrassenSchacht(
                    r.GetFieldValue(SchachtnummerFeld),
                    r.GetFieldValue(StrassenFeld)))
                .ToList();
}
