using System;
using System.Collections.Generic;
using AuswertungPro.Next.Application.Ai;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

public sealed class QuantificationCodeMetaReaderTests
{
    private static Dictionary<string, string> P(params (string Key, string Val)[] kv)
    {
        var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (k, v) in kv) d[k] = v;
        return d;
    }

    [Fact]
    public void Wurzeln_BBA_liest_Querschnitt_Prozent_als_Q1()
    {
        var p = P(("vsa.querschnitt.prozent", "40"));
        Assert.Equal("40", QuantificationCodeMetaReader.ReadQ1(p, "BBAC"));
    }

    [Fact]
    public void Ablagerung_BBC_liest_Ausdehnung_Prozent_als_Q1()
    {
        var p = P(("vsa.ausdehnung.prozent", "25"));
        Assert.Equal("25", QuantificationCodeMetaReader.ReadQ1(p, "BBCC"));
    }

    [Fact]
    public void Anschluss_BCA_liest_Hoehe_als_Q1_und_Breite_als_Q2()
    {
        var p = P(("vsa.hoehe.mm", "120"), ("vsa.breite.mm", "80"));
        Assert.Equal("120", QuantificationCodeMetaReader.ReadQ1(p, "BCAEB"));
        Assert.Equal("80", QuantificationCodeMetaReader.ReadQ2(p, "BCAEB"));
    }

    [Fact]
    public void Riss_BAB_liest_Breite_als_Q1()
    {
        var p = P(("vsa.breite.mm", "5"));
        Assert.Equal("5", QuantificationCodeMetaReader.ReadQ1(p, "BABBA"));
    }

    [Fact]
    public void ExpliziterWert_Quantifizierung1_hat_Vorrang_vor_KI_Key()
    {
        // Import/manuell gesetzt -> gewinnt gegen den code-spezifischen KI-Wert.
        var p = P(("Quantifizierung1", "33"), ("vsa.querschnitt.prozent", "40"));
        Assert.Equal("33", QuantificationCodeMetaReader.ReadQ1(p, "BBAC"));
    }

    [Fact]
    public void Vsa_q1_hat_Vorrang_vor_KI_Key()
    {
        var p = P(("vsa.q1", "7"), ("vsa.hoehe.mm", "120"));
        Assert.Equal("7", QuantificationCodeMetaReader.ReadQ1(p, "BCAEB"));
    }

    [Fact]
    public void BAI_Dichtungsmaterial_liest_Querschnitt_Prozent_als_Q1()
    {
        // Konsistenz mit der VSA-Zustandsrichtlinie (Tabelle 15: BAI q1 = %) und der
        // Schadencodierung 2018 (BAI = Querschnittsminderung %). Frueher faelschlich mm.
        var p = P(("vsa.querschnitt.prozent", "30"));
        Assert.Equal("30", QuantificationCodeMetaReader.ReadQ1(p, "BAIAD"));
        // Ein mm-Wert darf fuer BAI NICHT als Q1 gelten (falsche Einheit).
        var pmm = P(("vsa.hoehe.mm", "12"));
        Assert.Null(QuantificationCodeMetaReader.ReadQ1(pmm, "BAIAD"));
    }

    [Fact]
    public void Code_ohne_passende_Einheit_liest_nichts()
    {
        // BBF (Infiltration): keine Quantifizierung -> auch wenn faelschlich Werte da waeren, kein Q1.
        var p = P(("vsa.hoehe.mm", "10"));
        Assert.Null(QuantificationCodeMetaReader.ReadQ1(p, "BBF"));
    }

    [Fact]
    public void Falsche_Einheit_im_Dictionary_wird_nicht_als_Q1_genommen()
    {
        // Wurzeln erwarten %, aber nur ein mm-Wert ist da -> kein Q1 (statt falscher mm-Wert).
        var p = P(("vsa.hoehe.mm", "50"));
        Assert.Null(QuantificationCodeMetaReader.ReadQ1(p, "BBAC"));
    }

    [Fact]
    public void Null_Parameter_liefert_null()
    {
        Assert.Null(QuantificationCodeMetaReader.ReadQ1(null, "BBAC"));
        Assert.Null(QuantificationCodeMetaReader.ReadQ2(null, "BCAEB"));
    }
}
