using System.Globalization;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.DataPage;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Folgepaket zu PR #80: Die Funktionsauswahl liest die Bauwerksart ueber alle Schreibweisen
/// (SchachtFeldnamen.Wert). Ein alter Importwert neben einer bewusst geleerten Handkorrektur
/// darf die Auswahl nicht bestimmen.
/// </summary>
public sealed class SchachtNormoptionenHandwertTests
{
    [Fact]
    public void Bewusst_leere_Bauwerksart_schlaegt_alten_Importwert_in_anderer_Schreibweise()
    {
        var record = new SchachtRecord();
        record.SetFieldValue("BAUWERKSART", "Spezialbauwerk", FieldSource.Pdf, userEdited: false);
        record.SetFieldValue(FieldKeys.ShaftStructureType, "", FieldSource.Manual, userEdited: true);

        var optionen = (IReadOnlyList<string>)new SchachtNormoptionen()
            .Convert([record, null!], typeof(object), null!, CultureInfo.InvariantCulture);

        Assert.Equal(SchachtNormoptionen.Funktion("", ""), optionen);
        Assert.NotEqual(SchachtNormoptionen.Funktion("Spezialbauwerk", ""), optionen);
    }
}
