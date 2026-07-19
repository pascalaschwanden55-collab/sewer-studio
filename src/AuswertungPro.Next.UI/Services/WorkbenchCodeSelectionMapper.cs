using System.Globalization;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.UI.Services;

/// <summary>Auf die Pruefplatz-Felder reduziertes Ergebnis einer VSA-Code-Auswahl im Codierfenster.</summary>
public readonly record struct WorkbenchCodeSelection(string? Code, double? ClockPosition, int? Severity);

/// <summary>
/// Bildet einen <see cref="ProtocolEntry"/> aus dem VsaCodeExplorer auf die Pruefplatz-Felder ab.
/// Reine, WPF-freie Logik. Uhrlage kommt aus <c>CodeMeta.Parameters["vsa.uhr.von"]</c>,
/// die Stufe aus <c>CodeMeta.Severity</c> (nur wenn 1..5). Fehlende Werte bleiben null.
/// </summary>
public static class WorkbenchCodeSelectionMapper
{
    public static WorkbenchCodeSelection FromProtocolEntry(ProtocolEntry entry)
    {
        var code = string.IsNullOrWhiteSpace(entry.Code) ? null : entry.Code.Trim();

        double? clock = null;
        var parameters = entry.CodeMeta?.Parameters;
        if (parameters is not null
            && parameters.TryGetValue("vsa.uhr.von", out var uhr)
            && !string.IsNullOrWhiteSpace(uhr))
        {
            // "3" oder "3:00" -> 3.0 (nur der Stundenteil ist die Uhrlage).
            var head = uhr.Split(':')[0].Trim();
            if (double.TryParse(head, NumberStyles.Any, CultureInfo.InvariantCulture, out var h))
                clock = h;
        }

        int? severity = null;
        if (int.TryParse(entry.CodeMeta?.Severity, NumberStyles.Any, CultureInfo.InvariantCulture, out var s)
            && s is >= 1 and <= 5)
        {
            severity = s;
        }

        return new WorkbenchCodeSelection(code, clock, severity);
    }
}
