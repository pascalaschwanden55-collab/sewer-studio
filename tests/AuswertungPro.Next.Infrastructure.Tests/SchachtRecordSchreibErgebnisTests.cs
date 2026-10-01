using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Infrastructure.Tests;

/// <summary>
/// Der Setter muss sagen, was er getan hat. Ohne Ergebnis meldeten Aufrufer
/// Erfolg, obwohl der Schutz den Schreibvorgang abgelehnt hatte - Fotos wurden
/// kopiert, der Schacht zeigte nicht darauf.
/// </summary>
public sealed class SchachtRecordSchreibErgebnisTests
{
    [Fact]
    public void Ein_geschriebener_Wert_wird_als_geschrieben_gemeldet()
    {
        var record = new SchachtRecord();

        Assert.Equal(FeldSchreibErgebnis.Geschrieben, record.SetFieldValue("Strasse", "Hellgasse"));
    }

    [Fact]
    public void Ein_geschuetzter_Handwert_wird_als_geschuetzt_gemeldet()
    {
        var record = new SchachtRecord();
        record.SetFieldValue("Strasse", "Hellgasse", FieldSource.Manual, userEdited: true);

        Assert.Equal(FeldSchreibErgebnis.HandwertGeschuetzt, record.SetFieldValue("Strasse", "Dorfweg"));
        Assert.Equal(
            FeldSchreibErgebnis.HandwertGeschuetzt,
            record.SetFieldValue("Strasse", "Dorfweg", FieldSource.Pdf, userEdited: false));
        Assert.Equal("Hellgasse", record.GetFieldValue("Strasse"));
    }

    [Fact]
    public void Ein_unveraenderter_Wert_wird_als_unveraendert_gemeldet()
    {
        var record = new SchachtRecord();
        record.SetFieldValue("Strasse", "Hellgasse");

        Assert.Equal(FeldSchreibErgebnis.Unveraendert, record.SetFieldValue("Strasse", "Hellgasse"));
    }

    [Fact]
    public void Eine_technische_Aenderung_schreibt_ohne_die_Herkunft_zu_verfaelschen()
    {
        // Nach einem Umbenennen muss der Dateipfad nachgezogen werden. Das ist keine
        // Handeingabe - der Pfad darf danach nicht dauerhaft gegen Pflege gesperrt sein.
        var record = new SchachtRecord();
        record.SetFieldValue("PDF_Path", @"C:\alt\100.pdf", FieldSource.Pdf, userEdited: false);

        Assert.Equal(
            FeldSchreibErgebnis.Geschrieben,
            record.SetFieldValueTechnical("PDF_Path", @"C:\neu\101.pdf"));

        Assert.Equal(@"C:\neu\101.pdf", record.GetFieldValue("PDF_Path"));
        Assert.Equal(FieldSource.Pdf, record.FieldMeta["PDF_Path"].Source);
        Assert.False(record.IsUserEdited("PDF_Path"));
    }

    [Fact]
    public void Eine_technische_Aenderung_zieht_auch_einen_Handwert_nach_und_laesst_die_Marke_stehen()
    {
        // Der alte Pfad zeigt nach dem Umbenennen ins Leere - auch ein von Hand
        // gesetzter muss mit. Die Handmarke bleibt aber erhalten.
        var record = new SchachtRecord();
        record.SetFieldValue("PDF_Path", @"C:\alt\100.pdf", FieldSource.Manual, userEdited: true);

        Assert.Equal(
            FeldSchreibErgebnis.Geschrieben,
            record.SetFieldValueTechnical("PDF_Path", @"C:\neu\101.pdf"));

        Assert.Equal(@"C:\neu\101.pdf", record.GetFieldValue("PDF_Path"));
        Assert.True(record.IsUserEdited("PDF_Path"));
    }
}
