using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Views.Pages.Haltungsansicht;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Fix-Runde 1: die testbare Textbildung der zweiten Schadenszeile der Uebersicht
/// (Stufe, Quelle fachlich/KI, Freigabestatus).
/// </summary>
public sealed class SchadenStufeQuelleTextTests
{
    [Fact]
    public void KI_Vorschlag_mit_Stufe_und_offenem_Status()
    {
        var e = new ProtocolEntry
        {
            Code = "BAB",
            CodeMeta = new ProtocolEntryCodeMeta { Severity = "3" },
            Ai = new ProtocolEntryAiMeta { Confidence = 0.91, Accepted = false }
        };
        Assert.Equal(
            "Stufe 3 · KI-Vorschlag, Konfidenz 0.91 (Modellsicherheit) · offen",
            SchadenStufeQuelleConverter.Text(e));
    }

    [Fact]
    public void KI_Vorschlag_bestaetigt()
    {
        var e = new ProtocolEntry
        {
            Code = "BAB",
            Ai = new ProtocolEntryAiMeta { Confidence = 0.72, Accepted = true }
        };
        Assert.Equal(
            "KI-Vorschlag, Konfidenz 0.72 (Modellsicherheit) · bestätigt",
            SchadenStufeQuelleConverter.Text(e));
    }

    [Fact]
    public void Fachlich_erfasst_ohne_Stufe_und_ohne_KI()
    {
        var e = new ProtocolEntry { Code = "BAB" };
        Assert.Equal("fachlich erfasst", SchadenStufeQuelleConverter.Text(e));
    }
}
