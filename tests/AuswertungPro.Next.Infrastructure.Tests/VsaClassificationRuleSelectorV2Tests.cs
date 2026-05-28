using System.IO;
using AuswertungPro.Next.Infrastructure.Vsa.Classification;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class VsaClassificationRuleSelectorV2Tests
{
    private static VsaClassificationRuleSelector CreateSelector()
    {
        var root = TestPaths.FindSolutionRoot();
        var channelsPath = Path.Combine(root, "src", "AuswertungPro.Next.UI", "Data", "vsa_zustandsklassifizierung_2023_channels.json");
        var manholesPath = Path.Combine(root, "src", "AuswertungPro.Next.UI", "Data", "vsa_zustandsklassifizierung_2023_manholes.json");
        return VsaClassificationRuleSelector.Load(channelsPath, manholesPath);
    }

    [Fact]
    public void Classify_Baa_UsesPipeMaterialScope()
    {
        var selector = CreateSelector();

        var rigid = selector.Classify(new VsaClassificationRequest(
            Code: "BAA",
            Ch1: "A",
            Requirement: "S",
            Q1: "0.5",
            Material: "Beton"));
        Assert.Equal(4, rigid.S?.Ez);
        Assert.Equal("rigid", rigid.S?.PipeFlexibility);

        var flexible = selector.Classify(new VsaClassificationRequest(
            Code: "BAA",
            Ch1: "A",
            Requirement: "S",
            Q1: "14",
            Material: "PVC"));
        Assert.Equal(1, flexible.S?.Ez);
        Assert.Equal("flexible", flexible.S?.PipeFlexibility);
    }

    [Fact]
    public void Classify_Baa_WithoutMaterial_DoesNotGuessRigidOrFlexible()
    {
        var selector = CreateSelector();

        var outcome = selector.Classify(new VsaClassificationRequest(
            Code: "BAA",
            Ch1: "A",
            Requirement: "S",
            Q1: "0.5"));

        Assert.Null(outcome.S);
        Assert.Contains(outcome.Diagnostics, d => d.Reason == "scope-unresolved");
    }

    [Fact]
    public void Classify_Bab_UsesCrackRules_NotBaaDeformationRules()
    {
        var selector = CreateSelector();

        var severe = selector.Classify(new VsaClassificationRequest(
            Code: "BAB",
            Ch1: "B",
            Ch2: "A",
            Requirement: "S",
            Q1: "8"));
        Assert.Equal(0, severe.S?.Ez);

        var harmless = selector.Classify(new VsaClassificationRequest(
            Code: "BAB",
            Ch1: "B",
            Ch2: "B",
            Requirement: "S"));
        Assert.Equal(4, harmless.S?.Ez);
    }

    [Fact]
    public void Classify_Bac_Collapse_IsCriticalForAllRequirements()
    {
        var selector = CreateSelector();

        var outcome = selector.Classify(new VsaClassificationRequest(
            Code: "BAC",
            Ch1: "C"));

        Assert.Equal(0, outcome.D?.Ez);
        Assert.Equal(0, outcome.S?.Ez);
        Assert.Equal(0, outcome.B?.Ez);
    }

    [Fact]
    public void Classify_Baj_C_S_IsFixedEz4_FromPdfTable16()
    {
        var selector = CreateSelector();

        var outcome = selector.Classify(new VsaClassificationRequest(
            Code: "BAJ",
            Ch1: "C",
            Requirement: "S",
            Q1: "5"));

        Assert.Equal(4, outcome.S?.Ez);
        Assert.Equal("c-074-BAJ-S", outcome.S?.RuleId);
    }

    [Fact]
    public void Classify_Baj_B_S_RemainsNonAssessable()
    {
        var selector = CreateSelector();

        var outcome = selector.Classify(new VsaClassificationRequest(
            Code: "BAJ",
            Ch1: "B",
            Requirement: "S",
            Q1: "25"));

        Assert.Null(outcome.S);
        Assert.Contains(outcome.Diagnostics, d =>
            d.Requirement == "S" &&
            d.Reason == "not-assessable");
    }

    [Fact]
    public void Classify_UnknownRule_AddsDiagnosticReason()
    {
        var selector = CreateSelector();

        var outcome = selector.Classify(new VsaClassificationRequest(
            Code: "BDA",
            Requirement: "S"));

        Assert.Null(outcome.S);
        Assert.Contains(outcome.Diagnostics, d =>
            d.Requirement == "S" &&
            d.Reason == "rule-not-found");
    }
}
