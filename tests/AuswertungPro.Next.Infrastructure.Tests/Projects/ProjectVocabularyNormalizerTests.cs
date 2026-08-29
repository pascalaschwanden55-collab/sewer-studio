using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Projects;

namespace AuswertungPro.Next.Infrastructure.Tests.Projects;

/// <summary>
/// Bestandsprojekte tragen Werte, die in der neuen Auswahlliste nicht mehr stehen.
/// Ohne Anhebung zeigt das Feld leer an, obwohl ein Wert gespeichert ist.
/// Gemessen an Zone 1.15: 19 Haltungen und 10 Schaechte mit "Beton Normalbeton".
/// </summary>
public sealed class ProjectVocabularyNormalizerTests
{
    [Fact]
    public void Rohrmaterial_aus_einem_Bestandsprojekt_wird_angehoben()
    {
        var project = new Project();
        var haltung = new HaltungRecord();
        haltung.Fields["Rohrmaterial"] = "Beton Normalbeton";
        project.Data.Add(haltung);

        var geaendert = ProjectVocabularyNormalizer.Normalize(project);

        Assert.Equal("Normalbeton", haltung.GetFieldValue("Rohrmaterial"));
        Assert.Equal(1, geaendert);
    }

    [Fact]
    public void Schachtfunktion_und_Schachtmaterial_werden_angehoben()
    {
        var project = new Project();
        var schacht = new SchachtRecord();
        schacht.Fields["Funktion"] = "Einstiegschacht";
        schacht.Fields["Material"] = "Beton Normalbeton";
        project.SchaechteData.Add(schacht);

        ProjectVocabularyNormalizer.Normalize(project);

        Assert.Equal("Kontrollschacht", schacht.GetFieldValue("Funktion"));
        Assert.Equal("Beton", schacht.GetFieldValue("Material"));
    }

    [Fact]
    public void Ein_unbekannter_Wert_bleibt_unveraendert_stehen()
    {
        var project = new Project();
        var haltung = new HaltungRecord();
        haltung.Fields["Rohrmaterial"] = "Blaustein";
        project.Data.Add(haltung);

        var geaendert = ProjectVocabularyNormalizer.Normalize(project);

        Assert.Equal("Blaustein", haltung.GetFieldValue("Rohrmaterial"));
        Assert.Equal(0, geaendert);
    }

    [Fact]
    public void Die_Herkunft_bleibt_unangetastet()
    {
        // Die wichtigste Grenze: waere der Wert danach als Handaenderung markiert,
        // wuerde die XTF-Revision Felder schreiben, die der Mensch nie bearbeitet hat.
        var project = new Project();
        var haltung = new HaltungRecord();
        haltung.SetFieldValue("Rohrmaterial", "Beton Normalbeton", FieldSource.Xtf405, userEdited: false);
        project.Data.Add(haltung);
        var vorher = haltung.FieldMeta["Rohrmaterial"].LastUpdatedUtc;

        ProjectVocabularyNormalizer.Normalize(project);

        Assert.Equal("Normalbeton", haltung.GetFieldValue("Rohrmaterial"));
        Assert.Equal(FieldSource.Xtf405, haltung.FieldMeta["Rohrmaterial"].Source);
        Assert.False(haltung.FieldMeta["Rohrmaterial"].UserEdited);
        Assert.Equal(vorher, haltung.FieldMeta["Rohrmaterial"].LastUpdatedUtc);
    }

    [Fact]
    public void Auch_eine_Handaenderung_wird_in_der_Schreibweise_angehoben()
    {
        // Nur die Schreibweise aendert sich, nie die Aussage - deshalb ist das
        // unbedenklich, und die Handmarkierung bleibt erhalten.
        var project = new Project();
        var haltung = new HaltungRecord();
        haltung.SetFieldValue("Rohrmaterial", "PVC", FieldSource.Manual, userEdited: true);
        project.Data.Add(haltung);

        ProjectVocabularyNormalizer.Normalize(project);

        Assert.Equal("Polyvinylchlorid", haltung.GetFieldValue("Rohrmaterial"));
        Assert.True(haltung.FieldMeta["Rohrmaterial"].UserEdited);
    }

    [Fact]
    public void Ein_zweiter_Lauf_aendert_nichts_mehr()
    {
        var project = new Project();
        var haltung = new HaltungRecord();
        haltung.Fields["Rohrmaterial"] = "Beton Normalbeton";
        project.Data.Add(haltung);

        ProjectVocabularyNormalizer.Normalize(project);
        Assert.Equal(0, ProjectVocabularyNormalizer.Normalize(project));
    }
}
