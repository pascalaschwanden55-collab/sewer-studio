using System.Windows.Media;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Ai;

public static class CodingAiOverlayDisplayPolicy
{
    public static Color StrokeColor(CodingUserDecision decision)
        => decision switch
        {
            CodingUserDecision.Accepted or CodingUserDecision.AcceptedWithEdit
                => Color.FromRgb(0x22, 0xC5, 0x5E),
            CodingUserDecision.Rejected
                => Color.FromRgb(0xEF, 0x44, 0x44),
            _ => Color.FromRgb(0xF5, 0x9E, 0x0B)
        };

    public static string LabelText(string? code, double? confidence)
    {
        var codeText = string.IsNullOrWhiteSpace(code) ? "?" : code;
        return confidence.HasValue
            ? $"{codeText} [{confidence.Value * 100:F1}%]"
            : codeText;
    }
}
