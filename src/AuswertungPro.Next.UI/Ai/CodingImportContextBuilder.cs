using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Ai;

public static class CodingImportContextBuilder
{
    public static IReadOnlyList<(string Code, string Description, double Meter)>? Build(IEnumerable<CodingEvent>? importEvents)
    {
        if (importEvents is null)
            return null;

        var context = new List<(string Code, string Description, double Meter)>();
        foreach (var evt in importEvents)
        {
            var entry = evt.Entry;
            var code = entry?.Code;
            if (string.IsNullOrWhiteSpace(code))
                continue;

            context.Add((code, entry?.Beschreibung ?? code, evt.MeterAtCapture));
        }

        return context.Count > 0 ? context : null;
    }
}
