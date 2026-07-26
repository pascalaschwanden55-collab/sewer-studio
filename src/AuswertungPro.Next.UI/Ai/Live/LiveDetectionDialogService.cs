using System;

namespace AuswertungPro.Next.UI.Ai.Live;

public sealed class LiveDetectionDialogService
{
    private readonly Action<string, string> _warn;
    private readonly Action<string, string> _info;

    public LiveDetectionDialogService(
        Action<string, string> warn,
        Action<string, string> info)
    {
        _warn = warn;
        _info = info;
    }

    public void ShowRuntimeSettingsLoadFailed()
        => _warn("KI-Konfiguration konnte nicht geladen werden.", "Live-KI");

    public void ShowDisabled()
        => _info("KI ist deaktiviert. Bitte in den Einstellungen aktivieren.", "Live-KI");

    public void ShowStartFailed(string message)
        => _warn($"Live-KI konnte nicht gestartet werden: {message}", "Live-KI");

    public void ShowCodeCatalogUnavailable()
        => _info(
            "Schadenscode-Katalog nicht verf\u00fcgbar.\n" +
            "Bitte die App neu starten oder KI-Einstellungen pr\u00fcfen.",
            "Markieren");
}
