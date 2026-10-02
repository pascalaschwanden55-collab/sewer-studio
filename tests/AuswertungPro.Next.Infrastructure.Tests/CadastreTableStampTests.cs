using AuswertungPro.Next.Infrastructure.Map;

namespace AuswertungPro.Next.Infrastructure.Tests;

/// <summary>Gemeinsame Frischeregel der Kataster-Tabellen von Haltungen und Schaechten (B6, Deepscan 02.10.2026).</summary>
public sealed class CadastreTableStampTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "sewer-stamp-" + Guid.NewGuid().ToString("N"));

    public CadastreTableStampTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); }
        catch (IOException) { /* Aufraeumen im Temp-Ordner des Tests; ein gesperrter Rest stoert das Ergebnis nicht */ }
    }

    [Fact]
    public void Tabelle_mit_passender_Herkunftszeile_ist_frisch_und_nach_Aenderung_der_Quelle_nicht_mehr()
    {
        var xtf = Path.Combine(_root, "daten.xtf");
        var tabelle = Path.Combine(_root, "tabelle.tsv");
        File.WriteAllText(xtf, "<xtf/>");
        File.WriteAllText(tabelle, CadastreTableStamp.Line(xtf, new FileInfo(xtf)) + "\nKopf\n");

        Assert.True(CadastreTableStamp.IsFresh(tabelle, xtf));

        File.WriteAllText(xtf, "<xtf>mehr Inhalt</xtf>");
        Assert.False(CadastreTableStamp.IsFresh(tabelle, xtf));
    }

    [Fact]
    public void Fehlende_Dateien_und_fehlende_Herkunftszeile_sind_nicht_frisch()
    {
        var xtf = Path.Combine(_root, "daten.xtf");
        var tabelle = Path.Combine(_root, "tabelle.tsv");
        File.WriteAllText(xtf, "<xtf/>");

        Assert.False(CadastreTableStamp.IsFresh(tabelle, xtf));

        File.WriteAllText(tabelle, "Kopf ohne Herkunft\n");
        Assert.False(CadastreTableStamp.IsFresh(tabelle, xtf));

        File.WriteAllText(tabelle, CadastreTableStamp.Line(xtf, new FileInfo(xtf)) + "\n");
        File.Delete(xtf);
        Assert.False(CadastreTableStamp.IsFresh(tabelle, xtf));
    }
}
