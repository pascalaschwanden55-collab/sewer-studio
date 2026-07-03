using System;
using System.Windows.Media;

namespace AuswertungPro.Next.UI.Ai;

public readonly record struct CodingImportConfirmationBadgeState(string Text, TimeSpan AutoHideDelay);

public readonly record struct CodingProtocolMatchOverlayState(string Text, TimeSpan Duration);

public static class CodingProtocolMatchDisplayPolicy
{
    public static Color BackgroundColor(CodingProtocolMatchBucket bucket)
        => bucket switch
        {
            CodingProtocolMatchBucket.TrainingGreen => Color.FromRgb(0x11, 0x38, 0x22),
            CodingProtocolMatchBucket.ReviewYellow => Color.FromRgb(0x47, 0x35, 0x10),
            CodingProtocolMatchBucket.WrongCode => Color.FromRgb(0x51, 0x25, 0x08),
            CodingProtocolMatchBucket.Missed => Color.FromRgb(0x4C, 0x1D, 0x1D),
            CodingProtocolMatchBucket.FalseAlarm => Color.FromRgb(0x2F, 0x1A, 0x45),
            _ => Color.FromRgb(0x1F, 0x29, 0x37)
        };

    public static Color BadgeColor(CodingProtocolMatchBucket bucket)
        => bucket switch
        {
            CodingProtocolMatchBucket.TrainingGreen => Color.FromRgb(0x16, 0xA3, 0x4A),
            CodingProtocolMatchBucket.ReviewYellow => Color.FromRgb(0xCA, 0x8A, 0x04),
            CodingProtocolMatchBucket.WrongCode => Color.FromRgb(0xEA, 0x58, 0x0C),
            CodingProtocolMatchBucket.Missed => Color.FromRgb(0xDC, 0x26, 0x26),
            CodingProtocolMatchBucket.FalseAlarm => Color.FromRgb(0x7C, 0x3A, 0xED),
            _ => Color.FromRgb(0x47, 0x55, 0x69)
        };

    public static string BadgeText(CodingProtocolMatchBucket bucket)
        => bucket switch
        {
            CodingProtocolMatchBucket.TrainingGreen => "TRAIN",
            CodingProtocolMatchBucket.ReviewYellow => "PRUEF",
            CodingProtocolMatchBucket.WrongCode => "CODE",
            CodingProtocolMatchBucket.Missed => "FEHLT",
            CodingProtocolMatchBucket.FalseAlarm => "EXTRA",
            _ => ""
        };

    public static string Tooltip(CodingProtocolMatchBucket bucket)
        => bucket switch
        {
            CodingProtocolMatchBucket.TrainingGreen => "Abgleich: sicherer Treffer, Trainingskandidat",
            CodingProtocolMatchBucket.ReviewYellow => "Abgleich: wahrscheinlicher Treffer, kurz pruefen",
            CodingProtocolMatchBucket.WrongCode => "Abgleich: gleiche Stelle, falscher Code",
            CodingProtocolMatchBucket.Missed => "Abgleich: im Import vorhanden, von KI verpasst",
            CodingProtocolMatchBucket.FalseAlarm => "Abgleich: KI-Fehlalarm ohne Import-Partner",
            _ => "Abgleich"
        };

    public static CodingImportConfirmationBadgeState BuildImportConfirmationBadge(
        string? code,
        double meter,
        CodingProtocolVerificationResult? verification = null)
    {
        var text = $"? {code} @ {meter:F1}m bestaetigt";
        if (!string.IsNullOrWhiteSpace(verification?.ConfirmationLevel))
            text += $" | Qwen: {verification.ConfirmationLevel}";

        return new(text, TimeSpan.FromSeconds(3));
    }

    public static CodingProtocolMatchOverlayState BuildAcceptedGreenMatchesOverlay(int accepted)
        => new($"{accepted} gruene Treffer als Training uebernommen", TimeSpan.FromSeconds(4));
}
