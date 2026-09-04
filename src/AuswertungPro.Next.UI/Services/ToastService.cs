using System;
using System.Diagnostics;
using AuswertungPro.Next.Application.Common;

namespace AuswertungPro.Next.UI.Services;

/// <summary>
/// Standard-Umsetzung von <see cref="IToastService"/>. Leitet Meldungen an eine angehaengte
/// Senke (den ToastHost im MainWindow) weiter. Ohne Senke wird nur geloggt statt zu crashen –
/// so funktionieren Aufrufer auch in Fenstern/Tests ohne sichtbaren Host.
/// </summary>
public sealed class ToastService : IToastService
{
    private Action<string, ToastSeverity, string?, Action?>? _sink;

    /// <summary>Verbindet den Service einmalig mit dem sichtbaren Host (vom MainWindow gesetzt).</summary>
    public void AttachSink(Action<string, ToastSeverity, string?, Action?> sink) => _sink = sink;

    public void Success(string message) => Post(message, ToastSeverity.Success, null, null);
    public void Success(string message, string aktionText, Action aktion)
        => Post(message, ToastSeverity.Success, aktionText, aktion);
    public void Info(string message) => Post(message, ToastSeverity.Info, null, null);
    public void Warning(string message) => Post(message, ToastSeverity.Warning, null, null);
    public void Error(string message) => Post(message, ToastSeverity.Error, null, null);

    private void Post(string message, ToastSeverity severity, string? aktionText, Action? aktion)
    {
        if (severity is ToastSeverity.Warning or ToastSeverity.Error)
            BestEffort.ReportWarning($"[Toast/{severity}] {message}");
        else
            Trace.WriteLine($"[Toast/{severity}] {message}");

        var sink = _sink;
        if (sink is null)
            return;

        sink(message, severity, aktionText, aktion);
    }
}
