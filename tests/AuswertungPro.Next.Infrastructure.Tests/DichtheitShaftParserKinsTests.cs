using AuswertungPro.Next.Infrastructure.HoldingDistribution;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests;

/// <summary>
/// KINS-Dichtheitsprotokolle (kleine Leitungen) nutzen die Labels
/// "von Schacht:" / "nach Schacht:". Ohne dieses Muster griff der Heuristik-
/// Fallback die Norm-Referenz aus der Kopfzeile ("Dichtheitspruefung nach
/// SIA190:2017") als Schachtnummer ab — Ergebnis waren Ordner wie
/// "07.279408-SIA190" statt "07.279408-279406".
/// </summary>
public sealed class DichtheitShaftParserKinsTests
{
    // Echter Textausschnitt aus D:\Videoprojekte\01-2026 (DP_klein, H24).
    private const string KinsDpKleinText =
        "Dichtheitsprüfung nach SIA190:2017 / VSA RL Dicht:2023 (Verfahren Luft)\n" +
        "von Schacht: 07.279408\n" +
        "nach Schacht: 279406\n" +
        "Info: Bis an die Hausgrenze\n" +
        "Prüfverfahren: Rohrleitungsprüfung Prüfdruck: 200.0 mbar\n";

    [Fact]
    public void TryExtractShafts_LiestVonNachSchachtLabels()
    {
        var (a, b) = DichtheitShaftParser.TryExtractShafts(KinsDpKleinText);

        Assert.Equal("07.279408", a);
        Assert.Equal("279406", b);
    }

    [Fact]
    public void TryExtractShafts_LiestSsPraefixMitAbstandAusFretzPruefprotokoll()
    {
        var (a, b) = DichtheitShaftParser.TryExtractShafts(
            "Dichtheitspruefung nach SIA190/VSA (Verfahren Luft)\n" +
            "von Schacht: SS 8993\n" +
            "nach Schacht: SS 10081\n" +
            "Pruefverfahren: Rohrleitungspruefung Pruefdruck: 200.0 mbar\n");

        Assert.Equal("8993", a);
        Assert.Equal("10081", b);
    }

    [Fact]
    public void TryExtractShafts_GreiftNieDieNormReferenzAlsSchacht()
    {
        // Nur Kopfzeile, keine Labels: lieber gar nichts als SIA190.
        var (a, b) = DichtheitShaftParser.TryExtractShafts(
            "Dichtheitsprüfung nach SIA190:2017 / VSA RL Dicht:2023 (Verfahren Luft)\n");

        Assert.Null(a);
        Assert.Null(b);
    }

    [Fact]
    public void TryExtractShafts_ObererUntererSchacht_BleibtWieBisher()
    {
        var (a, b) = DichtheitShaftParser.TryExtractShafts(
            "oberer Schacht: 58951\nunterer Schacht: 58950\n");

        Assert.Equal("58951", a);
        Assert.Equal("58950", b);
    }
}
