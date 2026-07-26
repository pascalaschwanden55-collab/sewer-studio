using System.Windows.Media;
using AuswertungPro.Next.Application.Ai.QualityGate;

namespace AuswertungPro.Next.UI.Ai.Coding;

public static class CodingConfirmationDisplayPolicy
{
    public static Color AmpelColor(QualityGateResult gateResult)
        => gateResult.IsGreen
            ? Color.FromRgb(0x22, 0xC5, 0x5E)
            : gateResult.IsYellow
                ? Color.FromRgb(0xF5, 0x9E, 0x0B)
                : Color.FromRgb(0xEF, 0x44, 0x44);

    public static string QualityGateStatusText(QualityGateResult gateResult)
        => gateResult.IsGreen
            ? "QualityGate: Grün (kritisch)"
            : gateResult.IsYellow
                ? "QualityGate: Gelb"
                : "QualityGate: Rot";

    public static string ConfirmationDetail(QualityGateResult gateResult)
        => gateResult.IsGreen
            ? "Kritischer Befund — bitte bestätigen oder korrigieren."
            : gateResult.IsYellow
                ? "KI ist unsicher — bitte prüfen."
                : "KI hat geringe Sicherheit — bitte Code korrigieren oder verwerfen.";
}
