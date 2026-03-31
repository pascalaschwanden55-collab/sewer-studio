using AuswertungPro.Next.UI.Ai.KnowledgeBase;
using AuswertungPro.Next.UI.Ai.Training;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

/// <summary>
/// Tests fuer KnowledgeBaseManager: IsIndexWorthy und BuildEmbeddingText.
/// Prueft: Punkt-Codes, WinCan-Codes, kurze Beschreibungen, Embedding-Anreicherung.
/// </summary>
public sealed class KnowledgeBaseManagerTests
{
    private static TrainingSample MakeSample(
        string code = "BAB",
        string beschreibung = "Laengsriss im Scheitelbereich")
    {
        return new TrainingSample
        {
            SampleId = "test_kb_001",
            CaseId = "case_001",
            Code = code,
            Beschreibung = beschreibung,
        };
    }

    // ── IsIndexWorthy ───────────────────────────────────────────────

    [Fact]
    public void NormalesVsaSample_IstIndexWorthy()
    {
        Assert.True(KnowledgeBaseManager.IsIndexWorthy(MakeSample("BAB", "Laengsriss")));
    }

    [Fact]
    public void PunktCode_WirdNormalisiert()
    {
        // BCA.A.A → BCAAA → VsaCodeTree findet es
        Assert.True(KnowledgeBaseManager.IsIndexWorthy(MakeSample("BCA.A.A", "Seitlicher Anschluss")));
    }

    [Fact]
    public void WinCanCode_BEGINN_WirdAbgelehnt()
    {
        // BEGINN ist kein VSA-Code — darf NICHT in die KB
        Assert.False(KnowledgeBaseManager.IsIndexWorthy(MakeSample("BEGINN", "Beginn der Inspektion")));
    }

    [Fact]
    public void WinCanCode_BOGEN_WirdAbgelehnt()
    {
        Assert.False(KnowledgeBaseManager.IsIndexWorthy(MakeSample("BOGEN", "Richtungsaenderung")));
    }

    [Fact]
    public void WinCanCode_FOTO_WirdAbgelehnt()
    {
        Assert.False(KnowledgeBaseManager.IsIndexWorthy(MakeSample("FOTO", "Foto aufgenommen")));
    }

    [Fact]
    public void SchachtCode_DAB_WirdAbgelehnt()
    {
        // D-Codes (Schacht) gehoeren nicht in die Leitungs-KB
        Assert.False(KnowledgeBaseManager.IsIndexWorthy(MakeSample("DABBA", "Riss vertikal")));
    }

    [Fact]
    public void KurzeBeschreibung_Rohrende_IstAkzeptiert()
    {
        // "Rohrende" ist 8 Zeichen — war frueher < 10 und wurde rejected
        Assert.True(KnowledgeBaseManager.IsIndexWorthy(MakeSample("BCE", "Rohrende")));
    }

    [Fact]
    public void DreiZeichenBeschreibung_IstAkzeptiert()
    {
        // Minimum 3 Zeichen (z.B. "Riss")
        Assert.True(KnowledgeBaseManager.IsIndexWorthy(MakeSample("BAB", "Riss")));
    }

    [Fact]
    public void ZweiZeichenBeschreibung_IstNichtAkzeptiert()
    {
        Assert.False(KnowledgeBaseManager.IsIndexWorthy(MakeSample("BAB", "ab")));
    }

    [Fact]
    public void LeereBeschreibung_IstNichtAkzeptiert()
    {
        Assert.False(KnowledgeBaseManager.IsIndexWorthy(MakeSample("BAB", "")));
    }

    [Fact]
    public void LeererCode_IstNichtAkzeptiert()
    {
        Assert.False(KnowledgeBaseManager.IsIndexWorthy(MakeSample("", "Riss im Rohr")));
    }

    [Fact]
    public void UnbekannterCode_IstNichtAkzeptiert()
    {
        // MWST, RABATT etc. sind weder VSA noch WinCan-akzeptiert
        Assert.False(KnowledgeBaseManager.IsIndexWorthy(MakeSample("MWST", "Mehrwertsteuer")));
    }

    [Fact]
    public void UnbekannterCode_SKONTO_IstNichtAkzeptiert()
    {
        Assert.False(KnowledgeBaseManager.IsIndexWorthy(MakeSample("SKONTO", "Skonto 2%")));
    }

    // ── BuildEmbeddingText ──────────────────────────────────────────

    [Fact]
    public void LangeBeschreibung_WirdDirectVerwendet()
    {
        var text = KnowledgeBaseManager.BuildEmbeddingText(
            MakeSample("BAB", "Laengsriss im Scheitelbereich bei 12:00 Uhr"));
        // Beschreibung > 15 Zeichen und nicht Code-Echo → "Code: Beschreibung"
        Assert.StartsWith("BAB: ", text);
        Assert.Contains("Laengsriss", text);
    }

    [Fact]
    public void CodeEcho_WirdMitLabelAngereichert()
    {
        var text = KnowledgeBaseManager.BuildEmbeddingText(MakeSample("BAB", "BAB"));
        // Beschreibung = Code → VSA-Label anhängen
        Assert.Contains("BAB", text);
        // Sollte das VSA-Label enthalten (z.B. "Risse" oder aehnlich)
        Assert.True(text.Length > 5, $"Embedding-Text sollte angereichert sein, ist aber: '{text}'");
    }

    [Fact]
    public void PunktCode_WirdFuerLabelNormalisiert()
    {
        var text = KnowledgeBaseManager.BuildEmbeddingText(MakeSample("BCA.A.A", "BCA.A.A"));
        // Punkt-Code → normalisiert fuer VsaCodeTree Lookup
        Assert.Contains("BCA.A.A", text);
        Assert.True(text.Length > 10, $"Punkt-Code sollte angereichert sein: '{text}'");
    }

    [Fact]
    public void WinCanCode_OhneLabel_GibtCodePlusBeschreibung()
    {
        var text = KnowledgeBaseManager.BuildEmbeddingText(MakeSample("BEGINN", "Beginn"));
        Assert.Contains("BEGINN", text);
    }

    [Fact]
    public void KurzeBeschreibung_MitLabel_WirdKombiniert()
    {
        var text = KnowledgeBaseManager.BuildEmbeddingText(MakeSample("BCE", "Ende"));
        // "Ende" < 5 Zeichen, Code hat VSA-Label → "{code} — {label}"
        Assert.Contains("BCE", text);
        Assert.True(text.Length > 5);
    }
}
