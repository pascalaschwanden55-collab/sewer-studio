using System.Globalization;
using AuswertungPro.Next.Application.Protocol;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.UI.DataPage;

/// <summary>
/// Reine UI-Entscheidungslogik fuer Foto/Film-Links aus Protokollbeobachtungen.
/// Fenster- und PlayerWindow-Aufrufe bleiben im Code-behind.
/// </summary>
public static class DataPageProtocolMediaLinkController
{
    public static ProtocolEntry? ResolveEntry(object? tag, object? dataContext)
        => tag as ProtocolEntry ?? dataContext as ProtocolEntry;

    public static TimeSpan? ResolveTargetTime(ProtocolEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        return entry.Zeit ?? ProtocolTimeParser.ParseMpegTime(entry.Mpeg);
    }

    public static string BuildOverlayText(ProtocolEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(entry.Code))
            parts.Add(entry.Code.Trim());
        if (!string.IsNullOrWhiteSpace(entry.Beschreibung))
            parts.Add(entry.Beschreibung.Trim());
        if (entry.MeterStart.HasValue || entry.MeterEnd.HasValue)
        {
            var m1 = entry.MeterStart?.ToString("0.00", CultureInfo.InvariantCulture) ?? "-";
            var m2 = entry.MeterEnd?.ToString("0.00", CultureInfo.InvariantCulture) ?? "-";
            parts.Add(entry.IsStreckenschaden ? $"Strecke {m1} - {m2} m" : $"Meter {m1} - {m2}");
        }

        return string.Join(" | ", parts.Where(p => !string.IsNullOrWhiteSpace(p)));
    }
}
