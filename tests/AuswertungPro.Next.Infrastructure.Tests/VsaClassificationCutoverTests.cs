using System.Collections.Generic;
using System.IO;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Vsa;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class VsaClassificationCutoverTests
{
    private static VsaEvaluationService CreateService()
    {
        var root = TestPaths.FindSolutionRoot();
        var channelsTable = Path.Combine(root, "src", "AuswertungPro.Next.UI", "Data", "classification_channels.json");
        var manholesTable = Path.Combine(root, "src", "AuswertungPro.Next.UI", "Data", "classification_manholes.json");
        return new VsaEvaluationService(channelsTable, manholesTable);
    }

    [Fact]
    public void ProductiveEvaluation_UsesV2Rules_NotLegacyClassificationChannels()
    {
        var project = new Project();
        var record = new HaltungRecord();
        record.SetFieldValue("Haltungsname", "cutover-baa", FieldSource.Xtf, userEdited: false);
        record.SetFieldValue("Haltungslaenge_m", "10", FieldSource.Xtf, userEdited: false);
        record.SetFieldValue("Rohrmaterial", "Beton", FieldSource.Xtf, userEdited: false);
        record.VsaFindings = new List<VsaFinding>
        {
            new() { KanalSchadencode = "BAAA", Quantifizierung1 = "0.5" }
        };
        project.Data.Add(record);

        var result = CreateService().Evaluate(project);

        Assert.True(result.Ok, result.ErrorMessage);
        Assert.Equal("vsa_zustandsklassifizierung_2023_channels.json", project.Metadata["VSA_Table"]);
        Assert.Equal("4.00", record.GetFieldValue("VSA_Zustandsnote_S"));
    }

    [Fact]
    public void ProductiveEvaluation_LeavesKnownObservationCodesUnassessed()
    {
        var project = new Project();
        var record = new HaltungRecord();
        record.SetFieldValue("Haltungsname", "cutover-bca", FieldSource.Xtf, userEdited: false);
        record.SetFieldValue("Haltungslaenge_m", "10", FieldSource.Xtf, userEdited: false);
        record.VsaFindings = new List<VsaFinding>
        {
            new() { KanalSchadencode = "BCA" }
        };
        project.Data.Add(record);

        var result = CreateService().Evaluate(project);

        Assert.True(result.Ok, result.ErrorMessage);
        Assert.Equal("n/a", record.GetFieldValue("VSA_Zustandsnote_D"));
        Assert.Equal("n/a", record.GetFieldValue("VSA_Zustandsnote_B"));
        Assert.Equal("n/a", record.GetFieldValue("Zustandsklasse"));
    }
}
