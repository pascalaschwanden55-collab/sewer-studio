using System.Globalization;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.UI.Ai;

public static class CodingOverlayQuantificationWriter
{
    public static void ApplyToEntry(ProtocolEntry entry, OverlayGeometry? overlay)
    {
        if (overlay == null)
            return;

        entry.CodeMeta ??= new ProtocolEntryCodeMeta();
        if (overlay.ClockFrom.HasValue)
            entry.CodeMeta.Parameters["vsa.uhr.von"] = Format(overlay.ClockFrom.Value);
        if (overlay.ClockTo.HasValue)
            entry.CodeMeta.Parameters["vsa.uhr.bis"] = Format(overlay.ClockTo.Value);
        if (overlay.Q1Mm.HasValue)
            entry.CodeMeta.Parameters["vsa.q1"] = Format(overlay.Q1Mm.Value);
        if (overlay.Q2Mm.HasValue)
            entry.CodeMeta.Parameters["vsa.q2"] = Format(overlay.Q2Mm.Value);
        if (overlay.ArcDegrees.HasValue && overlay.ToolType == OverlayToolType.PipeBend)
            entry.CodeMeta.Parameters["vsa.winkel"] = Format(overlay.ArcDegrees.Value);
        if (overlay.FillPercent.HasValue)
        {
            var key = overlay.ToolType == OverlayToolType.Level && overlay.Points.Count >= 3
                ? "vsa.querschnitt.prozent"
                : "vsa.fuellgrad.prozent";
            entry.CodeMeta.Parameters[key] = Format(overlay.FillPercent.Value);
        }
    }

    private static string Format(double value)
        => value.ToString("F1", CultureInfo.InvariantCulture);
}
