using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.UI.Views.Windows;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Fehlerpruefung 11.07., Kritisch 3: Ein schwerer Schaden ohne echten Modellwert darf
/// nicht mehr als gruene "100%"-Sicherheit erscheinen. Schadensgrad und Sicherheit
/// sind getrennte Anzeigen.
/// </summary>
public sealed class AiFindingDisplayItemConfidenceTests
{
    [Fact]
    public void Severity5_ohne_ModelConfidence_zeigt_nv_und_grau()
    {
        var item = new AiFindingDisplayItem(Finding(severity: 5));

        Assert.Null(item.ConfidencePercent);
        Assert.Equal("Sicherheit: n/v", item.ConfidenceText);
        // Grau (0x94,0xA3,0xB8) — niemals Gruen fuer einen erfundenen Wert.
        Assert.Equal(0x94, item.ConfidenceBrush.Color.R);
        Assert.Equal("5", item.SeverityText); // Schadensgrad bleibt separat sichtbar
    }

    [Fact]
    public void EchteModelConfidence_wird_getrennt_vom_Schadensgrad_angezeigt()
    {
        var item = new AiFindingDisplayItem(Finding(severity: 1, modelConfidence: 0.95));

        Assert.Equal(95, item.ConfidencePercent);
        Assert.Equal("95%", item.ConfidenceText);
        Assert.Equal("1", item.SeverityText);
    }

    [Fact]
    public void Details_werden_mit_lesbaren_Mittelpunkten_getrennt()
    {
        var item = new AiFindingDisplayItem(new LiveFrameFinding(
            Label: "Riss",
            Severity: 2,
            PositionClock: "6:00",
            ExtentPercent: 25,
            VsaCodeHint: "BAB"));

        Assert.Equal("Uhr 6:00 · Umfang 25%", item.DetailText);
    }

    private static LiveFrameFinding Finding(int severity, double? modelConfidence = null)
        => new(
            Label: "Riss",
            Severity: severity,
            PositionClock: "6:00",
            ExtentPercent: null,
            VsaCodeHint: "BAB",
            ModelConfidence: modelConfidence);
}
