using System;

namespace AuswertungPro.Next.UI.Ai.Coding;

public sealed class CodingProtocolDialogService
{
    private readonly Func<string, string, bool> _confirm;
    private readonly Action<string, string> _error;

    public CodingProtocolDialogService(
        Func<string, string, bool> confirm,
        Action<string, string> error)
    {
        _confirm = confirm;
        _error = error;
    }

    public bool ConfirmPdfExport(int eventCount)
        => _confirm(
            $"Codier-Session abgeschlossen ({eventCount} Ereignisse).\n\n" +
            "M\u00f6chten Sie jetzt ein PDF-Protokoll mit Grafik und Fotos erstellen?",
            "PDF-Protokoll erstellen");

    public bool ConfirmProtocolPreview(int observationCount)
        => _confirm(
            $"{observationCount} Beobachtungen protokolliert.\n\n" +
            "Protokoll jetzt anzeigen und bearbeiten?\n" +
            "(\u00c4nderungen werden in Prim\u00e4re Sch\u00e4den \u00fcbernommen)",
            "Codier-Session abgeschlossen");

    public void ShowPdfExportFailed(string message)
        => _error($"PDF konnte nicht erstellt werden:\n{message}", "Fehler");
}
