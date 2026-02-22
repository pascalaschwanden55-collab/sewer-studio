using System;
using System.Collections.Generic;
using System.IO;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Vsa;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class VsaEvaluationServiceTests
{
    private static VsaEvaluationService CreateService()
    {
        var root = TestPaths.FindSolutionRoot();
        var channelsTable = Path.Combine(root, "src", "AuswertungPro.Next.UI", "Data", "classification_channels.json");
        var manholesTable = Path.Combine(root, "src", "AuswertungPro.Next.UI", "Data", "classification_manholes.json");
        return new VsaEvaluationService(channelsTable, manholesTable);
    }

    [Fact]
    public void Evaluate_UsesSchadencodeRules_WhenRuleExists()
    {
        var project = new Project();
        var rec = new HaltungRecord();
        rec.SetFieldValue("Haltungsname", "H1", FieldSource.Xtf, userEdited: false);
        rec.SetFieldValue("Haltungslaenge_m", "10", FieldSource.Xtf, userEdited: false);

        rec.VsaFindings = new List<VsaFinding>
        {
            new() { KanalSchadencode = "BAF", LL = 3.0 }
        };

        project.Data.Add(rec);

        var svc = CreateService();
        var res = svc.Evaluate(project);
        Assert.True(res.Ok, res.ErrorMessage);

        // With at least one classified finding, notes must be numeric (not n/a)
        Assert.NotEqual("n/a", rec.GetFieldValue("VSA_Zustandsnote_D"));
        Assert.NotEqual("n/a", rec.GetFieldValue("VSA_Zustandsnote_S"));
        Assert.NotEqual("n/a", rec.GetFieldValue("VSA_Zustandsnote_B"));

        // Zustandsklasse should be set (0..4 or n/a)
        Assert.False(string.IsNullOrWhiteSpace(rec.GetFieldValue("Zustandsklasse")));
    }

    [Fact]
    public void Evaluate_DoesNotFallbackToEinzelschadenklasse_WhenSchadencodeUnknown()
    {
        var project = new Project();
        var rec = new HaltungRecord();
        rec.SetFieldValue("Haltungsname", "H2", FieldSource.Xtf, userEdited: false);
        rec.SetFieldValue("Haltungslaenge_m", "10", FieldSource.Xtf, userEdited: false);

        // Unknown Schadencode, but importer might have filled EZ* from Einzelschadenklasse.
        rec.VsaFindings = new List<VsaFinding>
        {
            new()
            {
                KanalSchadencode = "ZZZ999",
                EZD = 1,
                EZS = 1,
                EZB = 1,
                LL = 3.0
            }
        };

        project.Data.Add(rec);

        var svc = CreateService();
        var res = svc.Evaluate(project);
        Assert.True(res.Ok, res.ErrorMessage);

        // With unknown code and no rule contribution, requirement results must be n/a (not silently using EZ fallback)
        Assert.Equal("n/a", rec.GetFieldValue("VSA_Zustandsnote_D"));
        Assert.Equal("n/a", rec.GetFieldValue("VSA_Zustandsnote_S"));
        Assert.Equal("n/a", rec.GetFieldValue("VSA_Zustandsnote_B"));
        Assert.Equal("n/a", rec.GetFieldValue("Zustandsklasse"));
    }

    [Fact]
    public void Evaluate_ParsesCodesFromPrimaereSchaeden_WhenNoFindings()
    {
        var project = new Project();
        var rec = new HaltungRecord();
        rec.SetFieldValue("Haltungsname", "H3", FieldSource.Xtf, userEdited: false);
        rec.SetFieldValue("Haltungslaenge_m", "10", FieldSource.Xtf, userEdited: false);

        // No structured findings, but textual primary damage codes exist.
        rec.SetFieldValue("Primaere_Schaeden", "BAF @12.3m (foo)", FieldSource.Xtf, userEdited: false);

        project.Data.Add(rec);

        var svc = CreateService();
        var res = svc.Evaluate(project);
        Assert.True(res.Ok, res.ErrorMessage);

        Assert.NotEqual("n/a", rec.GetFieldValue("VSA_Zustandsnote_D"));
        Assert.NotEqual("n/a", rec.GetFieldValue("VSA_Zustandsnote_S"));
        Assert.NotEqual("n/a", rec.GetFieldValue("VSA_Zustandsnote_B"));
        Assert.False(string.IsNullOrWhiteSpace(rec.GetFieldValue("Zustandsklasse")));
    }
}
