using AuswertungPro.Next.Domain.Models;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests;

/// <summary>
/// Sichert die Migration alter Rohrmaterial-Werte.
///
/// Hintergrund: Der SIA405-Import erzeugte bis Juli 2026 Werte wie "Kunststoff Polyvinilchlorid",
/// die in der Auswahlliste des Feldes nie standen. Das Programm zeigte sie darum als leer an,
/// obwohl der Wert gespeichert war — betroffen war rund ein Drittel aller Haltungen.
/// Die Migration hebt sie beim Laden auf die heutigen Auswahlwerte.
/// </summary>
public sealed class ProjectPipeMaterialMigrationTests
{
    [Theory]
    [InlineData("Kunststoff Polyvinilchlorid", "Polyvinylchlorid")]
    [InlineData("Kunststoff PVC", "Polyvinylchlorid")]
    [InlineData("PVC Polyvinilchlorid", "Polyvinylchlorid")]
    [InlineData("Kunststoff PE", "Polyethylen")]
    [InlineData("Kunststoff PE-HD", "Hartpolyethylen")]
    [InlineData("Kunststoff Polypropylen", "Polypropylen")]
    [InlineData("Kunststoff Epoxydharz", "Epoxydharz")]
    [InlineData("Guss Grauguss", "Guss")]
    public void Legacy_values_become_selectable_again(string alt, string erwartet)
    {
        var project = ProjectWithMaterial(alt, userEdited: false);

        project.EnsureMetadataDefaults();

        var wert = project.Data[0].GetFieldValue(FieldKeys.PipeMaterial);
        Assert.Equal(erwartet, wert);
        // Der Zweck der Uebung: Das Feld zeigt den Wert wieder an.
        Assert.Contains(wert, FieldCatalog.GetComboItems(FieldKeys.PipeMaterial));
    }

    [Fact]
    public void A_value_the_user_typed_is_never_touched()
    {
        var project = ProjectWithMaterial("Kunststoff PE", userEdited: true);

        project.EnsureMetadataDefaults();

        Assert.Equal("Kunststoff PE", project.Data[0].GetFieldValue(FieldKeys.PipeMaterial));
    }

    [Theory]
    [InlineData("Beton")]
    [InlineData("Steinzeug")]
    [InlineData("Polyvinylchlorid")]
    [InlineData("")]
    public void Current_and_empty_values_stay_as_they_are(string wert)
    {
        var project = ProjectWithMaterial(wert, userEdited: false);

        project.EnsureMetadataDefaults();

        Assert.Equal(wert, project.Data[0].GetFieldValue(FieldKeys.PipeMaterial));
    }

    [Fact]
    public void Migration_runs_only_once_and_stays_stable()
    {
        var project = ProjectWithMaterial("Kunststoff Polyvinilchlorid", userEdited: false);

        project.EnsureMetadataDefaults();
        project.EnsureMetadataDefaults();

        Assert.Equal("Polyvinylchlorid", project.Data[0].GetFieldValue(FieldKeys.PipeMaterial));
    }

    private static Project ProjectWithMaterial(string material, bool userEdited)
    {
        var record = new HaltungRecord();
        record.SetFieldValue(FieldKeys.HoldingName, "77467-77463", FieldSource.Xtf405, userEdited: false);
        record.SetFieldValue(FieldKeys.PipeMaterial, material, FieldSource.Xtf405, userEdited);

        var project = new Project { Name = "Test" };
        project.Data.Add(record);
        return project;
    }
}
