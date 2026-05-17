using System;

namespace AuswertungPro.Next.Application.Ai.Pipeline;

/// <summary>
/// Pflicht-Whitelist fuer VSA-KEK BC-Steuercodes (BCD/BCE/BCC/BCA).
///
/// Diese Codes sind nach VSA-KEK Pflichtmeldungen mit Severity 1
/// (Beobachtung, kein Schaden). Sie duerfen vom Soft-Filter im
/// EnhancedVisionAnalysisService NICHT wegen view_type=nahaufnahme/schwenk
/// unterdrueckt werden, sonst fehlen Rohranfang/Rohrende im Protokoll.
///
/// Aus EnhancedVisionAnalysisService.MapToAnalysis extrahiert (Audit
/// 2026-05-17), damit die Whitelist als Pure-Function gegen Regression
/// testbar wird.
/// </summary>
public static class BcCodeWhitelist
{
    /// <summary>
    /// True wenn der Code mit einem der vier BC-Praefixe beginnt
    /// (BCD, BCE, BCC, BCA — inkl. Charakterisierungen wie BCCAY, BCCBY).
    /// Null/leerer Code → false. Case-insensitiv.
    /// </summary>
    public static bool IsMandatory(string? vsaCode)
    {
        var code = vsaCode?.Trim();
        if (string.IsNullOrEmpty(code))
            return false;

        return code.StartsWith("BCD", StringComparison.OrdinalIgnoreCase)
            || code.StartsWith("BCE", StringComparison.OrdinalIgnoreCase)
            || code.StartsWith("BCC", StringComparison.OrdinalIgnoreCase)
            || code.StartsWith("BCA", StringComparison.OrdinalIgnoreCase);
    }
}
