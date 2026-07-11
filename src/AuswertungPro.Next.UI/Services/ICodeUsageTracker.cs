using System.Collections.Generic;

namespace AuswertungPro.Next.UI.Services;

/// <summary>Ein oft genutzter VSA-Code mit seiner Haeufigkeit.</summary>
public sealed record CodeUsageEintrag(string Code, int Anzahl);

/// <summary>
/// Zaehlt uebernommene VSA-Codes, damit der Code-Explorer Favoriten-Chips anbieten kann
/// (die haeufigsten Codes ueberspringen die 4-Stufen-Kaskade).
/// </summary>
public interface ICodeUsageTracker
{
    /// <summary>Einen uebernommenen Code zaehlen (leer/null wird ignoriert).</summary>
    void Erfasse(string? code);

    /// <summary>Die n haeufigsten Codes, absteigend nach Anzahl.</summary>
    IReadOnlyList<CodeUsageEintrag> TopCodes(int n);

    /// <summary>Die n zuletzt genutzten Codes (eindeutig, neueste zuerst).</summary>
    IReadOnlyList<string> Zuletzt(int n);
}
