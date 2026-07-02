using System;
using System.Diagnostics;

namespace AuswertungPro.Next.UI.Services;

/// <summary>
/// Standard-Umsetzung von <see cref="IToastService"/>. Leitet Meldungen an eine angehaengte
/// Senke (den ToastHost im MainWindow) weiter. Ohne Senke wird nur geloggt statt zu crashen –
/// so funktionieren Aufrufer auch in Fenstern/Tests ohne sichtbaren Host.
/// </summary>
public sealed class ToastService : IToastService
{
    private Action<string, ToastSeverity>? _sink;

    /// <summary>Verbindet den Service einmalig mit dem sichtbaren Host (vom MainWindow gesetzt).</summary>
    public void AttachSink(Action<string, ToastSeverity> sink) => _sink = sink;

    public void Success(string message) => Post(message, ToastSeverity.Success);
    public void Info(string message) => Post(message, ToastSeverity.Info);
    public void Warning(string message) => Post(message, ToastSeverity.Warning);
    public void Error(string message) => Post(message, ToastSeverity.Error);

    private void Post(string message, ToastSeverity severity)
    {
        var sink = _sink;
        if (sink is null)
        {
            Debug.WriteLine($"[Toast/{severity}] {message}");
            return;
        }

        sink(message, severity);
    }
}
