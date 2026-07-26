using System.Collections.Generic;
using System.Text;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Ai.Coding;

public static class CodingOpenStretchDamagePromptBuilder
{
    public static string Build(IEnumerable<CodingEvent> openEvents, double currentMeter)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Folgende Streckensch\u00E4den sind noch offen (kein MeterEnde):");
        sb.AppendLine();

        foreach (var ev in openEvents)
        {
            sb.AppendLine($"  \u2022 {ev.Entry.Code} \u2013 {ev.Entry.Beschreibung}");
            sb.AppendLine($"    Start: {ev.MeterAtCapture:F2}m");
        }

        sb.AppendLine();
        sb.AppendLine($"Sollen alle offenen Streckensch\u00E4den bei {currentMeter:F2}m geschlossen werden?");
        return sb.ToString();
    }
}
