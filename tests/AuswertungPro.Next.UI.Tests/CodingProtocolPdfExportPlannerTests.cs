using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingProtocolPdfExportPlannerTests
{
    [Fact]
    public void Build_uses_haltung_name_date_project_root_and_existing_logo()
    {
        var record = new HaltungRecord();
        record.SetFieldValue("Haltungsname", "12.034-12.035", FieldSource.Pdf, userEdited: false);

        var plan = CodingProtocolPdfExportPlanner.Build(
            record,
            @"C:\Projects\Testprojekt\projekt.json",
            @"C:\App",
            new DateTime(2026, 6, 23),
            path => path == @"C:\App\Assets\Brand\abwasser-uri-logo.png");

        Assert.Equal("Protokoll_12.034-12.035_20260623.pdf", plan.DefaultFileName);
        Assert.Equal(@"C:\Projects\Testprojekt", plan.ProjectRoot);
        Assert.True(plan.Options.IncludePhotos);
        Assert.True(plan.Options.IncludeHaltungsgrafik);
        Assert.Equal(@"C:\App\Assets\Brand\abwasser-uri-logo.png", plan.Options.LogoPathAbs);
    }

    [Fact]
    public void Build_uses_project_root_when_project_json_is_in_Projektdateien()
    {
        var record = new HaltungRecord();
        record.SetFieldValue("Haltungsname", "12.034-12.035", FieldSource.Pdf, userEdited: false);

        var plan = CodingProtocolPdfExportPlanner.Build(
            record,
            @"C:\Projects\Testprojekt\Projektdateien\projekt.json",
            @"C:\App",
            new DateTime(2026, 6, 23),
            fileExists: _ => false);

        Assert.Equal(@"C:\Projects\Testprojekt", plan.ProjectRoot);
    }

    [Fact]
    public void Build_uses_existing_empty_haltung_behavior_without_project_or_logo()
    {
        var record = new HaltungRecord();

        var plan = CodingProtocolPdfExportPlanner.Build(
            record,
            lastProjectPath: "",
            baseDirectory: @"C:\App",
            now: new DateTime(2026, 1, 2),
            fileExists: _ => false);

        Assert.Equal("Protokoll__20260102.pdf", plan.DefaultFileName);
        Assert.Equal("", plan.ProjectRoot);
        Assert.Null(plan.Options.LogoPathAbs);
    }

    /// <summary>
    /// Optikanalyse 28.09.2026, Aufgabe 15: mit gereichtem <c>resolveLogoPath</c> (die
    /// gemeinsame Quelle) gilt dessen Wert statt der lokalen baseDirectory/fileExists-Berechnung
    /// darueber.
    /// </summary>
    [Fact]
    public void Build_verwendet_resolveLogoPath_statt_der_lokalen_Berechnung_wenn_gereicht()
    {
        var record = new HaltungRecord();

        var plan = CodingProtocolPdfExportPlanner.Build(
            record,
            lastProjectPath: "",
            baseDirectory: @"C:\App",
            now: new DateTime(2026, 1, 2),
            // Wuerde ohne resolveLogoPath fuer den Standardpfad true liefern - resolveLogoPath
            // muss trotzdem gewinnen.
            fileExists: _ => true,
            resolveLogoPath: () => @"D:\Firmenlogos\aktuelles-logo.png");

        Assert.Equal(@"D:\Firmenlogos\aktuelles-logo.png", plan.Options.LogoPathAbs);
    }
}
