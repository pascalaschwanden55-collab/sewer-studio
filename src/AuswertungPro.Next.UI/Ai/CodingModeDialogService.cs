using System;

namespace AuswertungPro.Next.UI.Ai;

public sealed class CodingModeDialogService
{
    private readonly Action<string, string> _info;
    private readonly Action<string, string> _warn;

    public CodingModeDialogService(
        Action<string, string> info,
        Action<string, string> warn)
    {
        _info = info;
        _warn = warn;
    }

    public void ShowMissingHaltung()
        => _info(
            "Codier-Modus ben\u00f6tigt eine Haltung.\n" +
            "Bitte das Video \u00fcber die Datenseite mit einer Haltung \u00f6ffnen.",
            "Codier-Modus");

    public void ShowSessionStartFailed(string message)
        => _warn(message, "Codier-Modus");

    public void ShowImportFrameCaptureFailed()
        => _warn(
            "Frame konnte nicht aufgenommen werden.\nBitte pruefen Sie ob das Video laeuft.",
            "Import bestaetigen");
}
