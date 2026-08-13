using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Infrastructure.Tests;

/// <summary>
/// Schacht-Felder merken sich seit 2026-08-13 ihre Herkunft. Eine Handeingabe darf
/// von einem automatischen Schreiber nicht ueberholt werden, und ein spaeterer Export
/// muss erkennen koennen, was der Mensch gesetzt hat.
/// </summary>
public sealed class SchachtRecordFieldProtectionTests
{
    [Fact]
    public void Handeingabe_wird_als_solche_vermerkt()
    {
        var record = new SchachtRecord();

        record.SetFieldValue("Zustandsklasse", "2", FieldSource.Manual, userEdited: true);

        Assert.Equal("2", record.GetFieldValue("Zustandsklasse"));
        Assert.True(record.IsUserEdited("Zustandsklasse"));
        Assert.Equal(FieldSource.Manual, record.FieldMeta["Zustandsklasse"].Source);
    }

    [Fact]
    public void Automatischer_Schreiber_ueberholt_eine_Handeingabe_nicht()
    {
        var record = new SchachtRecord();
        record.SetFieldValue("Zustandsklasse", "2", FieldSource.Manual, userEdited: true);

        record.SetFieldValue("Zustandsklasse", "4", FieldSource.Legacy, userEdited: false);

        Assert.Equal("2", record.GetFieldValue("Zustandsklasse"));
        Assert.True(record.IsUserEdited("Zustandsklasse"));
    }

    [Fact]
    public void Automatischer_Schreiber_darf_ein_nicht_handgesetztes_Feld_fuellen()
    {
        var record = new SchachtRecord();
        record.SetFieldValue("Zustandsklasse", "1", FieldSource.Legacy, userEdited: false);

        record.SetFieldValue("Zustandsklasse", "3", FieldSource.Legacy, userEdited: false);

        Assert.Equal("3", record.GetFieldValue("Zustandsklasse"));
        Assert.False(record.IsUserEdited("Zustandsklasse"));
    }

    [Fact]
    public void Eine_Handeingabe_darf_von_Hand_wieder_geaendert_werden()
    {
        var record = new SchachtRecord();
        record.SetFieldValue("Zustandsklasse", "2", FieldSource.Manual, userEdited: true);

        record.SetFieldValue("Zustandsklasse", "4", FieldSource.Manual, userEdited: true);

        Assert.Equal("4", record.GetFieldValue("Zustandsklasse"));
    }

    // Umbenennen, Durchnummerieren und Massnahmen laufen weiter ueber den alten Aufruf.
    // Er muss schreiben duerfen, darf aber den Schutz nicht stillschweigend aufheben.
    [Fact]
    public void Der_alte_Aufruf_schreibt_weiter_und_senkt_die_Handmarkierung_nicht_ab()
    {
        var record = new SchachtRecord();
        record.SetFieldValue("Bemerkungen", "von Hand", FieldSource.Manual, userEdited: true);

        record.SetFieldValue("Bemerkungen", "technisch nachgezogen");

        Assert.Equal("technisch nachgezogen", record.GetFieldValue("Bemerkungen"));
        Assert.True(record.IsUserEdited("Bemerkungen"));
    }

    [Fact]
    public void Der_alte_Aufruf_erzeugt_ohne_Vorgeschichte_keine_Handmarkierung()
    {
        var record = new SchachtRecord();

        record.SetFieldValue("Strasse", "Hellgasse");

        Assert.Equal("Hellgasse", record.GetFieldValue("Strasse"));
        Assert.False(record.IsUserEdited("Strasse"));
    }

    [Fact]
    public void Ein_Altprojekt_ohne_Herkunftsangaben_gilt_als_nicht_handgesetzt()
    {
        var record = new SchachtRecord();
        record.Fields["Zustandsklasse"] = "3";   // wie aus einer alten Projektdatei geladen

        Assert.False(record.IsUserEdited("Zustandsklasse"));

        record.SetFieldValue("Zustandsklasse", "1", FieldSource.Legacy, userEdited: false);

        Assert.Equal("1", record.GetFieldValue("Zustandsklasse"));
    }

    [Fact]
    public void Ein_leerer_Handwert_bleibt_ebenfalls_geschuetzt()
    {
        var record = new SchachtRecord();
        record.SetFieldValue("Zustandsklasse", "", FieldSource.Manual, userEdited: true);

        record.SetFieldValue("Zustandsklasse", "4", FieldSource.Legacy, userEdited: false);

        Assert.Equal("", record.GetFieldValue("Zustandsklasse"));
    }
}
