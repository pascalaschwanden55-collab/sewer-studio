using AuswertungPro.Next.Infrastructure.Ai;

namespace AuswertungPro.Next.UI.Player;

public static class PlayerVsaCodeHintResolver
{
    public static string? ResolveKeyword(string? keyword)
    {
        var normalized = keyword?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        return normalized.ToUpperInvariant() switch
        {
            "ROHRANFANG" => "BCD",
            "ROHRENDE" => "BCE",
            "ANSCHLUSS" => "BCA",
            "BOGEN" => "BCC",
            "RISS" => "BAB",
            "BRUCH" => "BAC",
            "VERFORMUNG" => "BAA",
            "OBERFLAECHENSCHADEN" => "BAF",
            "VERSATZ" or "VERSCHIEBUNG" => "BAJ",
            "WURZELN" or "BEWUCHS" => "BBA",
            "ABLAGERUNG" => "BBC",
            "INKRUSTATION" => "BBB",
            "WASSERSTAND" => "BDDC",
            "ABBRUCH" => "BDC",
            _ => VsaCodeResolver.InferCodeFromLabel(normalized)
        };
    }
}
