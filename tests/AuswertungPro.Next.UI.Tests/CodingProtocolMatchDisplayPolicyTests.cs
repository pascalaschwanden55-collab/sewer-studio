using System.Windows.Media;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingProtocolMatchDisplayPolicyTests
{
    [Theory]
    [InlineData(CodingProtocolMatchBucket.TrainingGreen, "TRAIN", "Abgleich: sicherer Treffer, Trainingskandidat")]
    [InlineData(CodingProtocolMatchBucket.ReviewYellow, "PRUEF", "Abgleich: wahrscheinlicher Treffer, kurz pruefen")]
    [InlineData(CodingProtocolMatchBucket.WrongCode, "CODE", "Abgleich: gleiche Stelle, falscher Code")]
    [InlineData(CodingProtocolMatchBucket.Missed, "FEHLT", "Abgleich: im Import vorhanden, von KI verpasst")]
    [InlineData(CodingProtocolMatchBucket.FalseAlarm, "EXTRA", "Abgleich: KI-Fehlalarm ohne Import-Partner")]
    public void Text_mappings_match_existing_badge_contract(
        CodingProtocolMatchBucket bucket,
        string badgeText,
        string tooltip)
    {
        Assert.Equal(badgeText, CodingProtocolMatchDisplayPolicy.BadgeText(bucket));
        Assert.Equal(tooltip, CodingProtocolMatchDisplayPolicy.Tooltip(bucket));
    }

    [Fact]
    public void Color_mappings_keep_existing_palette()
    {
        Assert.Equal(Color.FromRgb(0x11, 0x38, 0x22),
            CodingProtocolMatchDisplayPolicy.BackgroundColor(CodingProtocolMatchBucket.TrainingGreen));
        Assert.Equal(Color.FromRgb(0x7C, 0x3A, 0xED),
            CodingProtocolMatchDisplayPolicy.BadgeColor(CodingProtocolMatchBucket.FalseAlarm));
    }

    [Fact]
    public void BuildImportConfirmationBadge_formats_text_and_delay()
    {
        var result = CodingProtocolMatchDisplayPolicy.BuildImportConfirmationBadge("BCA", 12.34);

        Assert.Equal($"? BCA @ {12.34:F1}m bestaetigt", result.Text);
        Assert.Equal(TimeSpan.FromSeconds(3), result.AutoHideDelay);
    }
}
