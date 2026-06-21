using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingClassifierDisplayPolicyTests
{
    [Fact]
    public void Classifier_code_checks_accept_only_known_boundary_and_structural_codes()
    {
        Assert.True(CodingClassifierDisplayPolicy.IsBoundaryClassifierCode("BCD"));
        Assert.True(CodingClassifierDisplayPolicy.IsBoundaryClassifierCode("BCE"));
        Assert.False(CodingClassifierDisplayPolicy.IsBoundaryClassifierCode("BCA"));

        Assert.True(CodingClassifierDisplayPolicy.IsStructuralClassifierCode("BCA"));
        Assert.True(CodingClassifierDisplayPolicy.IsStructuralClassifierCode("BCC"));
        Assert.False(CodingClassifierDisplayPolicy.IsStructuralClassifierCode("BCE"));
    }

    [Fact]
    public void Resolve_labels_prefer_catalog_text_and_use_vsa_fallbacks()
    {
        Assert.Equal("Katalog Rohranfang", CodingClassifierDisplayPolicy.ResolveBoundaryLabel("BCD", "Katalog Rohranfang"));
        Assert.Equal("Rohranfang", CodingClassifierDisplayPolicy.ResolveBoundaryLabel("BCD", null));
        Assert.Equal("Rohrende", CodingClassifierDisplayPolicy.ResolveBoundaryLabel("BCE", null));

        Assert.Equal("Katalog Anschluss", CodingClassifierDisplayPolicy.ResolveStructuralLabel("BCA", "Katalog Anschluss"));
        Assert.Equal("Anschluss", CodingClassifierDisplayPolicy.ResolveStructuralLabel("BCA", null));
        Assert.Equal("Bogen", CodingClassifierDisplayPolicy.ResolveStructuralLabel("BCC", null));
    }

    [Fact]
    public void BuildDetectedStatusText_distinguishes_added_and_existing_events()
    {
        Assert.Equal("Rohrende erkannt", CodingClassifierDisplayPolicy.BuildDetectedStatusText("Rohrende", added: true));
        Assert.Equal("Rohrende bereits vorhanden", CodingClassifierDisplayPolicy.BuildDetectedStatusText("Rohrende", added: false));
    }

    [Fact]
    public void BuildClassifierDetail_handles_missing_and_present_confidence()
    {
        Assert.Equal("Klassifikator", CodingClassifierDisplayPolicy.BuildClassifierDetail(null));

        var detail = CodingClassifierDisplayPolicy.BuildClassifierDetail(0.91);

        Assert.StartsWith("Klassifikator ", detail);
        Assert.Contains("91", detail);
    }

    [Fact]
    public void Build_boundary_findings_create_display_findings_for_list()
    {
        var accepted = CodingClassifierDisplayPolicy.BuildBoundaryFinding("BCE", "Rohrende");
        var possible = CodingClassifierDisplayPolicy.BuildPossibleBoundaryFinding("BCE", "Rohrende");

        Assert.Equal("Rohrende", accepted.Label);
        Assert.Equal(4, accepted.Severity);
        Assert.Equal("BCE", accepted.VsaCodeHint);

        Assert.Equal("Mögliches Rohrende", possible.Label);
        Assert.Equal(3, possible.Severity);
        Assert.Equal("BCE", possible.VsaCodeHint);
    }
}
