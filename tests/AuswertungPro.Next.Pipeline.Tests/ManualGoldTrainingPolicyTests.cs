using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Application.Protocol;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

public sealed class ManualGoldTrainingPolicyTests
{
    [Fact]
    public void EvaluateForExport_erlaubt_persoenlich_bestaetigtes_Hand_Gold_ohne_Inspektionsdatum()
    {
        var sample = PersonalGold();
        var catalog = new InMemoryCodeCatalogProvider(
        [
            new CodeDefinition { Code = "BAB", IsSelectable = true }
        ]);

        var eligibility = TrainingSampleEligibility.Evaluate(sample, catalog);
        var approval = ManualGoldTrainingPolicy.EvaluateForExport(sample, "Besitzer");

        Assert.True(eligibility.IsEligible);
        Assert.True(approval.IsEligible);
    }

    [Fact]
    public void EvaluateForExport_sperrt_nicht_manuelle_Quelle()
    {
        var sample = PersonalGold();
        sample.SourceType = SourceTypeNames.BatchImport;

        var result = ManualGoldTrainingPolicy.EvaluateForExport(sample, "Besitzer");

        Assert.False(result.IsEligible);
        Assert.Equal(ManualGoldTrainingPolicy.ManualGoldRequiredReason, result.Reason);
    }

    [Fact]
    public void EvaluateForExport_sperrt_fremden_Bestaetiger()
    {
        var sample = PersonalGold();

        var result = ManualGoldTrainingPolicy.EvaluateForExport(sample, "Andere Person");

        Assert.False(result.IsEligible);
        Assert.Equal(ManualGoldTrainingPolicy.ConfirmedByOtherUserReason, result.Reason);
    }

    [Fact]
    public void EvaluateForExport_sperrt_Gold_ohne_Segmentierung()
    {
        var sample = PersonalGold();
        sample.SamMaskRle = null;

        var result = ManualGoldTrainingPolicy.EvaluateForExport(sample, "Besitzer");

        Assert.False(result.IsEligible);
        Assert.Equal(ManualGoldTrainingPolicy.GoldGeometryRequiredReason, result.Reason);
    }

    [Fact]
    public void EvaluateForExport_sperrt_formal_defekte_Segmentierung()
    {
        var sample = PersonalGold();
        sample.SamMaskRle = "0,10,5";

        var result = ManualGoldTrainingPolicy.EvaluateForExport(sample, "Besitzer");

        Assert.False(result.IsEligible);
        Assert.Equal(ManualGoldTrainingPolicy.GoldGeometryRequiredReason, result.Reason);
    }

    [Fact]
    public void EvaluateForExport_sperrt_Maske_ohne_echten_Vordergrundpixel_in_der_Box()
    {
        var sample = PersonalGold();
        sample.BboxXCenter = 0.5;
        sample.BboxYCenter = 0.5;
        sample.BboxWidth = 0.25;
        sample.BboxHeight = 0.25;
        sample.SamMaskRle = "1,1,14,1";
        sample.SamMaskImageWidth = 4;
        sample.SamMaskImageHeight = 4;

        var result = ManualGoldTrainingPolicy.EvaluateForExport(sample, "Besitzer");

        Assert.False(result.IsEligible);
        Assert.Equal(ManualGoldTrainingPolicy.GoldGeometryRequiredReason, result.Reason);
    }

    [Theory]
    [InlineData("1,1,15", 0.125)]
    [InlineData("0,15,1", 0.875)]
    public void EvaluateForExport_erlaubt_echten_Vordergrundpixel_am_Bildrand(
        string rle,
        double center)
    {
        var sample = PersonalGold();
        sample.BboxXCenter = center;
        sample.BboxYCenter = center;
        sample.BboxWidth = 0.25;
        sample.BboxHeight = 0.25;
        sample.SamMaskRle = rle;
        sample.SamMaskImageWidth = 4;
        sample.SamMaskImageHeight = 4;

        var result = ManualGoldTrainingPolicy.EvaluateForExport(sample, "Besitzer");

        Assert.True(result.IsEligible);
    }

    [Fact]
    public void EvaluateForExport_sperrt_Box_ausserhalb_des_Bildes()
    {
        var sample = PersonalGold();
        sample.BboxXCenter = 0.95;
        sample.BboxWidth = 0.2;

        var result = ManualGoldTrainingPolicy.EvaluateForExport(sample, "Besitzer");

        Assert.False(result.IsEligible);
        Assert.Equal(ManualGoldTrainingPolicy.GoldGeometryRequiredReason, result.Reason);
    }

    [Fact]
    public void EvaluateForExport_sperrt_Gold_ohne_Bild()
    {
        var sample = PersonalGold();
        sample.FramePath = string.Empty;

        var result = ManualGoldTrainingPolicy.EvaluateForExport(sample, "Besitzer");

        Assert.False(result.IsEligible);
        Assert.Equal(ManualGoldTrainingPolicy.GoldFrameRequiredReason, result.Reason);
    }

    [Fact]
    public void EvaluateForExport_erlaubt_Alt_Platzhalter_fuer_reines_BBox_Training()
    {
        var sample = PersonalGold();
        sample.Beschreibung = "Riss laengs — Lage und Ausmass ergaenzen";

        var result = ManualGoldTrainingPolicy.EvaluateForExport(sample, "Besitzer");

        Assert.True(result.IsEligible);
    }

    [Fact]
    public void Progress_zaehlt_nur_persoenliche_vollstaendige_Goldframes()
    {
        var full = PersonalGold();
        full.FramePath = @"C:\KI_BRAIN\gold_frames\gold_a.jpg";
        var incomplete = PersonalGold();
        incomplete.SampleId = "gold-2";
        incomplete.FramePath = @"C:\KI_BRAIN\gold_frames\gold_b.jpg";
        incomplete.SamMaskRle = null;
        var malformed = PersonalGold();
        malformed.SampleId = "gold-malformed";
        malformed.FramePath = @"C:\KI_BRAIN\gold_frames\gold_malformed.jpg";
        malformed.SamMaskRle = "0,10,5";
        var otherUser = PersonalGold();
        otherUser.SampleId = "gold-3";
        otherUser.FramePath = @"C:\KI_BRAIN\gold_frames\gold_c.jpg";
        otherUser.Code = "BAC";
        otherUser.ConfirmedByUser = "Andere Person";

        var progress = PersonalGoldProgressCalculator.Calculate(
            [full, incomplete, malformed, otherUser],
            "Besitzer",
            ["BAB", "BAC"],
            frameIsReadable: _ => true);

        var bab = Assert.Single(progress, item => item.MainCode == "BAB");
        Assert.Equal(3, bab.PersonalSamples);
        Assert.Equal(1, bab.FullGoldSamples);
        Assert.Equal(1, bab.UniqueGoldFrames);
        Assert.Equal(29, bab.NeededForMinimum);
        Assert.Equal("needs_more", bab.Status);

        var bac = Assert.Single(progress, item => item.MainCode == "BAC");
        Assert.Equal(0, bac.PersonalSamples);
        Assert.Equal(0, bac.FullGoldSamples);
        Assert.Equal("missing", bac.Status);
    }

    [Fact]
    public void Progress_zaehlt_Draft_mit_Geometrie_nicht_als_vollstaendig()
    {
        var draft = PersonalGold();
        draft.Status = TrainingSampleStatus.Draft;

        var progress = PersonalGoldProgressCalculator.Calculate(
            [draft],
            "Besitzer",
            ["BAB"],
            frameIsReadable: _ => true);

        var bab = Assert.Single(progress);
        Assert.Equal(1, bab.PersonalSamples);
        Assert.Equal(0, bab.FullGoldSamples);
        Assert.Equal(0, bab.UniqueGoldFrames);
        Assert.Equal("missing", bab.Status);
    }

    [Fact]
    public void Progress_zaehlt_fehlendes_oder_unlesbares_Bild_nicht_als_vollstaendig()
    {
        var missing = PersonalGold();
        var unreadable = PersonalGold();
        unreadable.SampleId = "gold-unreadable";
        unreadable.FramePath = @"C:\KI_BRAIN\gold_frames\unreadable.jpg";

        var progress = PersonalGoldProgressCalculator.Calculate(
            [missing, unreadable],
            "Besitzer",
            ["BAB"],
            frameIsReadable: path =>
            {
                if (path.EndsWith("unreadable.jpg", StringComparison.OrdinalIgnoreCase))
                    throw new IOException("Datei nicht lesbar");
                return false;
            });

        var bab = Assert.Single(progress);
        Assert.Equal(2, bab.PersonalSamples);
        Assert.Equal(0, bab.FullGoldSamples);
        Assert.Equal(0, bab.UniqueGoldFrames);
        Assert.Equal("missing", bab.Status);
    }

    [Fact]
    public void Hauptcode_Anzeige_verbindet_Code_mit_Katalog_Klartext()
    {
        var displayName = PersonalGoldMainCodeCatalog.FormatDisplayName(
            "bab",
            code => code == "BAB" ? "Riss" : null);

        Assert.Equal("BAB — Riss", displayName);
    }

    [Fact]
    public void Hauptcode_Anzeige_benennt_BBD_nicht_als_allgemeine_BB_Gruppe()
    {
        var displayName = PersonalGoldMainCodeCatalog.FormatDisplayName(
            "BBD",
            _ => "Betrieb der Rohrleitungen");

        Assert.Equal("BBD — Eindringender Boden", displayName);
    }

    [Fact]
    public void Hauptcode_Ordner_verbindet_endgueltigen_Code_mit_Klartext()
    {
        var folderName = PersonalGoldMainCodeCatalog.FormatFolderName(
            "bab.bb",
            code => code == "BAB" ? "Riss" : null);

        Assert.Equal("BAB - Riss", folderName);
    }

    [Fact]
    public void Hauptcode_Ordner_benennt_BBD_fachlich_korrekt()
    {
        var folderName = PersonalGoldMainCodeCatalog.FormatFolderName(
            "BBD",
            _ => "Betrieb der Rohrleitungen");

        Assert.Equal("BBD - Eindringender Boden", folderName);
    }

    private static TrainingSample PersonalGold()
        => new()
        {
            SampleId = "gold-1",
            FramePath = @"C:\KI_BRAIN\gold_frames\gold_a.jpg",
            Status = TrainingSampleStatus.Approved,
            Code = "BAB",
            SourceType = SourceTypeNames.ManualCoding,
            HumanConfirmed = true,
            Corrected = false,
            ConfirmedByUser = "Besitzer",
            ConfirmedAtUtc = new DateTime(2026, 7, 23, 8, 0, 0, DateTimeKind.Utc),
            MatchLevel = MatchLevelNames.ReviewApproved,
            BboxXCenter = 0.5,
            BboxYCenter = 0.5,
            BboxWidth = 0.2,
            BboxHeight = 0.2,
            SamMaskRle = "0,4050,1,3949",
            SamMaskImageWidth = 100,
            SamMaskImageHeight = 80
        };

    private sealed class InMemoryCodeCatalogProvider(IReadOnlyList<CodeDefinition> codes)
        : ICodeCatalogProvider
    {
        public IReadOnlyList<CodeDefinition> GetAll() => codes;

        public bool TryGet(string code, out CodeDefinition def)
        {
            def = codes.FirstOrDefault(
                      item => item.Code.Equals(code, StringComparison.OrdinalIgnoreCase))
                  ?? new CodeDefinition();
            return !string.IsNullOrWhiteSpace(def.Code);
        }

        public void Save(IReadOnlyList<CodeDefinition> values)
            => throw new InvalidOperationException("Testkatalog ist schreibgeschuetzt.");

        public IReadOnlyList<string> AllowedCodes()
            => codes.Where(item => item.IsSelectable && !item.IsObservedExtension)
                .Select(item => item.Code)
                .ToArray();

        public IReadOnlyList<string> Validate(IReadOnlyList<CodeDefinition>? values = null) => [];
    }
}
