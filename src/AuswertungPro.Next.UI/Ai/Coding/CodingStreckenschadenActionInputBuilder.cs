using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Ai.Coding;

public static class CodingStreckenschadenActionInputBuilder
{
    /// <summary>
    /// Die offenen Streckenschaden-Anfaenge fuer den Live-Tracker. Was "offen" ist,
    /// entscheidet ausschliesslich <see cref="CodingStretchDamageDisplayPolicy"/> -
    /// dieselbe Regel wie im Kontextmenue, in der Listenanzeige und beim Verlassen
    /// des Codiermodus. Die frueher hier stehende Rohpruefung
    /// "IsStreckenschaden &amp;&amp; MeterEnd == null" hielt auch die beim Schliessen
    /// erzeugte Endmarke fuer einen offenen Anfang.
    /// </summary>
    public static IReadOnlyList<StreckenschadenActionMapper.OpenEntry> BuildOpenEntries(
        IEnumerable<CodingEvent> events)
    {
        return CodingStretchDamageDisplayPolicy.FindOpenStarts(events)
            .Select(e => new StreckenschadenActionMapper.OpenEntry(
                MainCode: e.Entry.Code,
                StartMeter: e.Entry.MeterStart ?? e.MeterAtCapture,
                Reference: e))
            .ToList();
    }
}
