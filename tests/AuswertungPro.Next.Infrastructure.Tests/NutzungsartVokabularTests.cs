using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Projects;

namespace AuswertungPro.Next.Infrastructure.Tests;

/// <summary>
/// Die Nutzungsart mit den Begriffen der Norm — und der Weg zurueck in beide
/// Modellfassungen.
/// </summary>
public sealed class NutzungsartVokabularTests
{
    [Theory]
    [InlineData("Schmutzwasser", "Schmutzabwasser")]
    [InlineData("schmutzabwasser", "Schmutzabwasser")]
    [InlineData("Regenwasser", "Niederschlagsabwasser")]
    [InlineData("Regenabwasser", "Niederschlagsabwasser")]
    [InlineData("Meteorwasser", "Niederschlagsabwasser")]
    [InlineData("Mischwasser", "Mischabwasser")]
    [InlineData("entlastetes_Mischabwasser", "entlastetes Mischabwasser")]
    [InlineData("", "")]
    public void Alte_Schreibweisen_werden_auf_den_Normbegriff_gebracht(string alt, string erwartet)
        => Assert.Equal(erwartet, NutzungsartVokabular.Normalisieren(alt));

    // Ein unbekannter Wert koennte eine Angabe enthalten, die niemand sonst kennt.
    // Ihn zu loeschen waere schlimmer, als ihn stehen zu lassen.
    [Fact]
    public void Ein_unbekannter_Wert_bleibt_unveraendert()
        => Assert.Equal("Kuehlwasser", NutzungsartVokabular.Normalisieren("Kuehlwasser"));

    [Fact]
    public void Die_Auswahl_enthaelt_leer_und_die_neun_Werte_der_Norm()
    {
        Assert.Equal(10, NutzungsartVokabular.Auswahl.Count);
        Assert.Equal("", NutzungsartVokabular.Auswahl[0]);
        Assert.Contains("Schmutzabwasser", NutzungsartVokabular.Auswahl);
        Assert.Contains("Niederschlagsabwasser", NutzungsartVokabular.Auswahl);
        Assert.Contains("unbekannt", NutzungsartVokabular.Auswahl);
        Assert.DoesNotContain("Schmutzwasser", NutzungsartVokabular.Auswahl);
        Assert.DoesNotContain("Regenwasser", NutzungsartVokabular.Auswahl);
    }

    [Fact]
    public void Die_Auswahl_des_Feldkatalogs_ist_dieselbe()
        => Assert.Equal(NutzungsartVokabular.Auswahl, FieldCatalog.GetComboItems(FieldKeys.UsageType));

    // Die beiden Fassungen kennen den Wert der jeweils anderen nicht.
    [Theory]
    [InlineData(true, "Niederschlagsabwasser")]
    [InlineData(false, "Regenabwasser")]
    [InlineData(null, null)]
    public void Das_Regenwasser_richtet_sich_nach_der_Modellfassung(bool? ab2020, string? erwartet)
        => Assert.Equal(erwartet, NutzungsartVokabular.NachModell("Niederschlagsabwasser", ab2020));

    // Alle uebrigen Werte sind fassungsunabhaengig — auch ohne bekannte Fassung eindeutig.
    [Theory]
    [InlineData("Schmutzabwasser", "Schmutzabwasser")]
    [InlineData("Mischabwasser", "Mischabwasser")]
    [InlineData("entlastetes Mischabwasser", "entlastetes_Mischabwasser")]
    public void Fassungsunabhaengige_Werte_brauchen_keine_Modellangabe(string wert, string erwartet)
        => Assert.Equal(erwartet, NutzungsartVokabular.NachModell(wert, ab2020: null));

    [Fact]
    public void Ein_unbekannter_Wert_wird_nicht_in_die_Datei_geschrieben()
        => Assert.Null(NutzungsartVokabular.NachModell("Kuehlwasser", ab2020: true));

    [Fact]
    public void Der_Speicherlauf_stellt_alte_Schreibweisen_um()
    {
        var record = new HaltungRecord();
        record.SetFieldValue(FieldKeys.UsageType, "Schmutzwasser", FieldSource.Xtf, userEdited: false);
        var vorher = record.FieldMeta[FieldKeys.UsageType];
        var projekt = new Project { Data = { record } };

        Assert.Equal(1, ProjectVocabularyNormalizer.Normalize(projekt));

        Assert.Equal("Schmutzabwasser", record.GetFieldValue(FieldKeys.UsageType));
        // Entscheidend: Die Herkunft bleibt unangetastet. Sonst wuerde die XTF-Revision
        // ploetzlich Felder schreiben, die der Mensch nie bearbeitet hat.
        Assert.False(record.FieldMeta[FieldKeys.UsageType].UserEdited);
        Assert.Equal(vorher.Source, record.FieldMeta[FieldKeys.UsageType].Source);
        Assert.Equal(vorher.LastUpdatedUtc, record.FieldMeta[FieldKeys.UsageType].LastUpdatedUtc);
    }

    [Fact]
    public void Der_Speicherlauf_laesst_unbekannte_und_bereits_richtige_Werte_stehen()
    {
        var richtig = new HaltungRecord();
        richtig.SetFieldValue(FieldKeys.UsageType, "Mischabwasser", FieldSource.Manual, userEdited: true);
        var fremd = new HaltungRecord();
        fremd.SetFieldValue(FieldKeys.UsageType, "Kuehlwasser", FieldSource.Manual, userEdited: true);
        var projekt = new Project { Data = { richtig, fremd } };

        Assert.Equal(0, ProjectVocabularyNormalizer.Normalize(projekt));
        Assert.Equal("Mischabwasser", richtig.GetFieldValue(FieldKeys.UsageType));
        Assert.Equal("Kuehlwasser", fremd.GetFieldValue(FieldKeys.UsageType));
    }
}
