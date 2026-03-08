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

    // ── Quantifizierungs-Tests ──────────────────────────────────────────

    [Fact]
    public void Evaluate_UsesQuantRules_WhenQ1Provided()
    {
        // BAA mit Q1=0.5mm (Rissbreite < 1mm) → EZS=3, EZB=3 (gemäss quantRules)
        // Ohne Q1 wäre der statische Default: EZS=2, EZB=2
        var project = new Project();
        var rec = new HaltungRecord();
        rec.SetFieldValue("Haltungsname", "H4_quant", FieldSource.Xtf, userEdited: false);
        rec.SetFieldValue("Haltungslaenge_m", "10", FieldSource.Xtf, userEdited: false);

        rec.VsaFindings = new List<VsaFinding>
        {
            new() { KanalSchadencode = "BAA", Quantifizierung1 = "0.5", LL = 3.0 }
        };

        project.Data.Add(rec);

        var svc = CreateService();
        var res = svc.Evaluate(project);
        Assert.True(res.Ok, res.ErrorMessage);

        // Quantifizierte BAA-Bewertung muss erfolgen (nicht n/a)
        Assert.NotEqual("n/a", rec.GetFieldValue("VSA_Zustandsnote_S"));
        Assert.NotEqual("n/a", rec.GetFieldValue("VSA_Zustandsnote_B"));

        // Die ZN sollte höher sein bei kleinem Riss (EZ=3 statt Default EZ=2)
        var znS = rec.GetFieldValue("VSA_Zustandsnote_S");
        Assert.True(double.TryParse(znS?.Replace(',', '.'),
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var znSVal));
        // EZ=3 + 0.4 = 3.4 (Startwert vor Abminderung) → ZN muss > 2.4 sein
        Assert.True(znSVal > 2.4, $"ZN_S mit Q1=0.5mm (EZ=3) sollte > 2.4 sein, ist aber {znSVal}");
    }

    [Fact]
    public void Evaluate_FallsBackToStatic_WhenQ1Null()
    {
        // BAA ohne Q1 → statische Defaults: EZS=2, EZB=2
        var project = new Project();
        var recWithQ1 = new HaltungRecord();
        recWithQ1.SetFieldValue("Haltungsname", "H5_noQ1", FieldSource.Xtf, userEdited: false);
        recWithQ1.SetFieldValue("Haltungslaenge_m", "10", FieldSource.Xtf, userEdited: false);

        recWithQ1.VsaFindings = new List<VsaFinding>
        {
            new() { KanalSchadencode = "BAA", Quantifizierung1 = null, LL = 3.0 }
        };

        project.Data.Add(recWithQ1);

        var svc = CreateService();
        var res = svc.Evaluate(project);
        Assert.True(res.Ok, res.ErrorMessage);

        // Muss trotzdem klassifiziert werden (nicht n/a)
        Assert.NotEqual("n/a", recWithQ1.GetFieldValue("VSA_Zustandsnote_S"));
        Assert.NotEqual("n/a", recWithQ1.GetFieldValue("VSA_Zustandsnote_B"));

        // EZ=2 (Default) + 0.4 = 2.4 → ZN muss <= 2.4 sein
        var znS = recWithQ1.GetFieldValue("VSA_Zustandsnote_S");
        Assert.True(double.TryParse(znS?.Replace(',', '.'),
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var znSVal));
        Assert.True(znSVal <= 2.5, $"ZN_S ohne Q1 (Default EZ=2) sollte <= 2.5 sein, ist aber {znSVal}");
    }

    [Fact]
    public void Evaluate_UsesQuantRules_LargeQ1GivesWorseEZ()
    {
        // BAA mit Q1=10mm (Rissbreite > 5mm) → EZS=1, EZB=1 (schlechter Zustand)
        var project = new Project();
        var rec = new HaltungRecord();
        rec.SetFieldValue("Haltungsname", "H6_largeQ1", FieldSource.Xtf, userEdited: false);
        rec.SetFieldValue("Haltungslaenge_m", "10", FieldSource.Xtf, userEdited: false);

        rec.VsaFindings = new List<VsaFinding>
        {
            new() { KanalSchadencode = "BAA", Quantifizierung1 = "10", LL = 3.0 }
        };

        project.Data.Add(rec);

        var svc = CreateService();
        var res = svc.Evaluate(project);
        Assert.True(res.Ok, res.ErrorMessage);

        // Grosser Riss → schlechtere ZN (EZ=1 + 0.4 = 1.4 Startwert)
        var znS = rec.GetFieldValue("VSA_Zustandsnote_S");
        Assert.True(double.TryParse(znS?.Replace(',', '.'),
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var znSVal));
        Assert.True(znSVal < 2.0, $"ZN_S mit Q1=10mm (EZ=1) sollte < 2.0 sein, ist aber {znSVal}");
    }

    [Fact]
    public void Evaluate_ClassifiesNewBCACodes()
    {
        // BCA (Anschluss) – neu hinzugefügter Code muss klassifiziert werden
        var project = new Project();
        var rec = new HaltungRecord();
        rec.SetFieldValue("Haltungsname", "H7_BCA", FieldSource.Xtf, userEdited: false);
        rec.SetFieldValue("Haltungslaenge_m", "10", FieldSource.Xtf, userEdited: false);

        rec.VsaFindings = new List<VsaFinding>
        {
            new() { KanalSchadencode = "BCA", LL = 3.0 }
        };

        project.Data.Add(rec);

        var svc = CreateService();
        var res = svc.Evaluate(project);
        Assert.True(res.Ok, res.ErrorMessage);

        // BCA hat EZD=2, EZB=2 → darf nicht n/a sein
        Assert.NotEqual("n/a", rec.GetFieldValue("VSA_Zustandsnote_D"));
        Assert.NotEqual("n/a", rec.GetFieldValue("VSA_Zustandsnote_B"));
    }
}
