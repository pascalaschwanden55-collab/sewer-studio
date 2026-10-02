using AuswertungPro.Next.Application.Schatten;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.ViewModels.Pages;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// R5 (Deepscan 02.10.2026): Ein Rechenfehler der Schattenauswertung darf nicht wie ein Ergebnis aussehen.
/// </summary>
public sealed class SchattenauswertungFehlerZeileTests
{
    [Fact]
    public void Fehlerergebnis_ist_kein_Vergleich_gilt_als_veraltet_und_zeigt_den_Fehlertext()
    {
        var record = new HaltungRecord();
        record.SetFieldValue("Haltungsname", "H1", FieldSource.Xtf, false);
        record.SetFieldValue("Zustandsklasse", "2", FieldSource.Manual, true);
        var ergebnis = new SchattenHaltungErgebnis
        {
            Haltung = "H1",
            // Hash stimmt absichtlich: ein Fehler muss trotzdem neu gerechnet werden.
            CodierungsHash = SchattenCodierungsHash.Compute(record),
            Status = SchattenStatus.Fehler,
            Fehler = "Zustandsbewertung: abgestuerzt"
        };

        var row = SchattenauswertungRowVm.Erstelle("H1", record, ergebnis);

        Assert.Equal("Kein", row.AbweichungKey);
        Assert.True(row.IstVeraltet);
        Assert.Equal("Fehler", row.StatusText);
        Assert.Contains("abgestuerzt", row.KiFehler);
    }
}
