using System;

namespace AuswertungPro.Next.UI.ViewModels.Windows;

internal sealed record VsaCodeExplorerFavoriteChipPresentation(
    string Content,
    string ToolTip);

internal static class VsaCodeExplorerFavoriteChipPresenter
{
    public static VsaCodeExplorerFavoriteChipPresentation? BuildSelectable(
        string? code,
        int anzahl,
        string? klartext,
        string? gruppenLabel)
    {
        var normalisierterCode = (code ?? string.Empty).Trim().ToUpperInvariant();
        return NormalizeLabel(klartext, normalisierterCode) is null
            ? null
            : Build(normalisierterCode, anzahl, klartext, gruppenLabel);
    }

    public static VsaCodeExplorerFavoriteChipPresentation Build(
        string? code,
        int anzahl,
        string? klartext,
        string? gruppenLabel)
    {
        var normalisierterCode = (code ?? string.Empty).Trim().ToUpperInvariant();
        var sichtbarerKlartext = NormalizeLabel(klartext, normalisierterCode);
        var tooltipTitel = sichtbarerKlartext
                           ?? NormalizeLabel(gruppenLabel, normalisierterCode)
                           ?? normalisierterCode;

        var content = sichtbarerKlartext is null
            ? $"{normalisierterCode} · {anzahl}×"
            : $"{sichtbarerKlartext} ({normalisierterCode}) · {anzahl}×";

        return new VsaCodeExplorerFavoriteChipPresentation(
            content,
            $"{tooltipTitel} — {anzahl}× verwendet. Klick springt zum Hauptcode.");
    }

    private static string? NormalizeLabel(string? label, string code)
    {
        var trimmed = label?.Trim();
        return string.IsNullOrWhiteSpace(trimmed)
               || string.Equals(trimmed, code, StringComparison.OrdinalIgnoreCase)
            ? null
            : trimmed;
    }
}
