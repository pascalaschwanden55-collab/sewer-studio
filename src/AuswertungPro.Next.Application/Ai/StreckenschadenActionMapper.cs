using System;
using System.Collections.Generic;
using System.Linq;

namespace AuswertungPro.Next.Application.Ai;

/// <summary>
/// Uebersetzt eine <see cref="StreckenschadenTracker.SegmentAction"/> in eine konkrete, UI-freie
/// Anweisung an den Codierpfad: einen offenen Streckenschaden-Eintrag anlegen, einen bestehenden
/// schliessen, oder nichts tun. Reine, testbare Application-Logik (gleiche Bauweise wie
/// <see cref="CodingFeedbackDecisionMapper"/>) — der Aufrufer (PlayerWindow) fuehrt die Anweisung
/// nur aus (AddEvent / UpdateEvent), damit die Fachregel nicht in der UI-God-Class verteilt wird.
///
/// Die Identitaet eines offenen Eintrags wird ueber Hauptcode + Anfangs-Meter bestimmt — das ist
/// genau das, was der Tracker als StartMeter pro Segment fuehrt.
/// </summary>
public static class StreckenschadenActionMapper
{
    /// <summary>Was der Aufrufer mit der Anweisung tun soll.</summary>
    public enum InstructionKind
    {
        /// <summary>Nichts tun (z.B. Extend ohne Statuswechsel — der offene Eintrag besteht schon).</summary>
        None,
        /// <summary>Neuen offenen Streckenschaden-Eintrag anlegen (IsStreckenschaden=true, MeterEnd=null).</summary>
        CreateOpen,
        /// <summary>Einen bestehenden offenen Eintrag schliessen (MeterEnd setzen).</summary>
        CloseExisting
    }

    /// <summary>
    /// Minimal-Sicht auf einen bestehenden offenen Streckenschaden-Eintrag (entkoppelt von
    /// ProtocolEntry/CodingEvent). Der Aufrufer baut diese Liste aus seinen Events.
    /// </summary>
    public sealed record OpenEntry(string MainCode, double StartMeter, object Reference);

    /// <summary>Konkrete Anweisung an den Codierpfad.</summary>
    public sealed record Instruction(
        InstructionKind Kind,
        string MainCode,
        double StartMeter,
        double EndMeter,
        bool IsConfirmedStrecke,
        double? ClockHour,
        object? TargetReference); // bei CloseExisting: der zu schliessende Eintrag (OpenEntry.Reference)

    /// <summary>Meter-Toleranz, innerhalb der ein offener Eintrag als "derselbe Anfang" gilt.</summary>
    private const double StartMeterMatchTolerance = 0.05;

    /// <summary>
    /// Bildet eine Tracker-Aktion auf eine Anweisung ab. <paramref name="openEntries"/> sind die
    /// aktuell offenen Streckenschaden-Eintraege (IsStreckenschaden=true, MeterEnd=null) des Aufrufers.
    /// </summary>
    public static Instruction Map(
        StreckenschadenTracker.SegmentAction action,
        IReadOnlyList<OpenEntry> openEntries)
    {
        if (action is null) throw new ArgumentNullException(nameof(action));
        openEntries ??= Array.Empty<OpenEntry>();

        switch (action.Type)
        {
            case StreckenschadenTracker.SegmentActionType.Open:
                // Nur anlegen, wenn nicht schon ein offener Eintrag mit gleichem Code+Anfang existiert
                // (Idempotenz gegen Doppel-Open bei wiederholten Ticks).
                if (FindMatch(action, openEntries) != null)
                    return None(action);
                return new Instruction(
                    InstructionKind.CreateOpen, action.MainCode, action.StartMeter, action.StartMeter,
                    action.IsConfirmedStrecke, action.ClockHour, TargetReference: null);

            case StreckenschadenTracker.SegmentActionType.Close:
                var target = FindMatch(action, openEntries);
                if (target == null)
                    return None(action); // nichts offen zum Schliessen
                return new Instruction(
                    InstructionKind.CloseExisting, action.MainCode, action.StartMeter, action.EndMeter,
                    action.IsConfirmedStrecke, action.ClockHour, target.Reference);

            case StreckenschadenTracker.SegmentActionType.Extend:
            default:
                // Extend aendert nur den internen Tracker-Zustand; der offene Eintrag bleibt wie er ist.
                // (Eine spaetere Ausbaustufe koennte hier MeterEnd-Vorschau oder Quant-Update liefern.)
                return None(action);
        }
    }

    /// <summary>Bequeme Stapelverarbeitung mehrerer Aktionen eines Ticks.</summary>
    public static IReadOnlyList<Instruction> MapAll(
        IReadOnlyList<StreckenschadenTracker.SegmentAction> actions,
        IReadOnlyList<OpenEntry> openEntries)
        => (actions ?? Array.Empty<StreckenschadenTracker.SegmentAction>())
            .Select(a => Map(a, openEntries))
            .Where(i => i.Kind != InstructionKind.None)
            .ToList();

    private static Instruction None(StreckenschadenTracker.SegmentAction action)
        => new(InstructionKind.None, action.MainCode, action.StartMeter, action.EndMeter,
            action.IsConfirmedStrecke, action.ClockHour, TargetReference: null);

    private static OpenEntry? FindMatch(
        StreckenschadenTracker.SegmentAction action,
        IReadOnlyList<OpenEntry> openEntries)
        => openEntries.FirstOrDefault(e =>
            string.Equals(e.MainCode, action.MainCode, StringComparison.OrdinalIgnoreCase)
            && Math.Abs(e.StartMeter - action.StartMeter) <= StartMeterMatchTolerance);
}
