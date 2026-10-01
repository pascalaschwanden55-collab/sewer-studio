using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.Ai;

/// <summary>Rolle eines Codier-Ereignisses im Streckenschaden.</summary>
public enum CodingStretchDamageRole
{
    /// <summary>Punktschaden oder Steuercode - kein Streckenschaden.</summary>
    None,

    /// <summary>Anfang eines Streckenschadens, das Ende fehlt noch.</summary>
    OpenStart,

    /// <summary>Anfang eines Streckenschadens mit gesetztem Ende.</summary>
    ClosedStart,

    /// <summary>Die beim Schliessen erzeugte Endmarke eines Streckenschadens.</summary>
    EndMarker
}

/// <summary>
/// Macht in der Codierliste sichtbar, welche Zeile den Anfang eines Streckenschadens
/// traegt und ob sein Ende noch fehlt. Vorher zeigte jede Zeile nur einen einzelnen
/// Meterwert - Anfang, Ende und Punktschaden sahen gleich aus.
///
/// Die beim Schliessen erzeugte Endmarke (<c>CodingStreckenschadenEventFactory.CloseStart</c>)
/// traegt selbst <c>IsStreckenschaden = true</c> und <c>MeterEnd = null</c>. Ohne die
/// Paarung hier gilt sie sonst faelschlich als offener Streckenschaden.
/// Bei Zweifel wird zugunsten von <see cref="CodingStretchDamageRole.OpenStart"/>
/// entschieden: Ein uebersehener offener Schaden waere der teurere Fehler.
/// </summary>
public static class CodingStretchDamageDisplayPolicy
{
    /// <summary>Toleranz beim Zusammenfuehren von Endmeter und Endmarke.</summary>
    private const double MeterTolerance = 0.005;

    /// <summary>Beschreibungs-Zusatz, den das Schliessen an die Endmarke haengt.</summary>
    private const string EndMarkerSuffix = "(Ende)";

    public static CodingStretchDamageRole ResolveRole(
        CodingEvent? codingEvent,
        IEnumerable<CodingEvent>? allEvents)
    {
        if (codingEvent?.Entry is null || !codingEvent.Entry.IsStreckenschaden)
            return CodingStretchDamageRole.None;

        if (codingEvent.Entry.MeterEnd.HasValue)
            return CodingStretchDamageRole.ClosedStart;

        return IsEndMarker(codingEvent, allEvents)
            ? CodingStretchDamageRole.EndMarker
            : CodingStretchDamageRole.OpenStart;
    }

    /// <summary>
    /// Nur ein wirklich offener Anfang darf geschlossen werden. Eine Endmarke,
    /// ein bereits geschlossener Anfang und ein Punktschaden nicht.
    /// </summary>
    public static bool CanClose(CodingEvent? codingEvent, IEnumerable<CodingEvent>? allEvents)
        => ResolveRole(codingEvent, allEvents) == CodingStretchDamageRole.OpenStart;

    /// <summary>
    /// Meterangabe der Listenzeile: Punktschaden weiterhin als einzelner Wert,
    /// Streckenschaden als Von-Bis mit Laenge, offener Anfang klar als offen.
    /// </summary>
    public static string BuildMeterText(
        CodingEvent? codingEvent,
        IEnumerable<CodingEvent>? allEvents)
    {
        if (codingEvent is null)
            return string.Empty;

        var start = ResolveStartMeter(codingEvent);

        switch (ResolveRole(codingEvent, allEvents))
        {
            case CodingStretchDamageRole.OpenStart:
                return $"ab {Meter(start)} · Ende offen";

            case CodingStretchDamageRole.ClosedStart:
                var end = codingEvent.Entry.MeterEnd!.Value;
                var length = end - start;
                return length > MeterTolerance
                    ? $"{Meter(start)} – {Meter(end)} ({Meter(length)})"
                    : $"{Meter(start)} – {Meter(end)}";

            case CodingStretchDamageRole.EndMarker:
                return $"Ende {Meter(start)}";

            default:
                return Meter(start);
        }
    }

    /// <summary>Kurzer Hinweistext neben dem Code; leer, wenn nichts offen ist.</summary>
    public static string BuildBadgeText(
        CodingEvent? codingEvent,
        IEnumerable<CodingEvent>? allEvents)
        => ResolveRole(codingEvent, allEvents) == CodingStretchDamageRole.OpenStart
            ? "OFFEN"
            : string.Empty;

    /// <summary>
    /// Offene Streckenschaeden-Anfaenge in Listenreihenfolge. Endmarken sind
    /// ausdruecklich nicht dabei.
    /// </summary>
    public static IReadOnlyList<CodingEvent> FindOpenStarts(IEnumerable<CodingEvent>? events)
    {
        if (events is null)
            return [];

        var all = events.ToList();
        return all
            .Where(ev => ResolveRole(ev, all) == CodingStretchDamageRole.OpenStart)
            .ToList();
    }

    private static bool IsEndMarker(CodingEvent codingEvent, IEnumerable<CodingEvent>? allEvents)
    {
        // Zwei unabhaengige Belege muessen zusammenpassen, sonst bleibt es ein offener
        // Anfang: der angehaengte Beschreibungszusatz UND ein Anfang derselben Klasse,
        // dessen Endmeter genau hier liegt.
        var description = codingEvent.Entry.Beschreibung?.TrimEnd() ?? string.Empty;
        if (!description.EndsWith(EndMarkerSuffix, StringComparison.OrdinalIgnoreCase))
            return false;

        if (allEvents is null)
            return false;

        var meter = ResolveStartMeter(codingEvent);
        return allEvents.Any(other =>
            !ReferenceEquals(other, codingEvent)
            && other.Entry is { IsStreckenschaden: true }
            && other.Entry.MeterEnd.HasValue
            && SameCode(other.Entry.Code, codingEvent.Entry.Code)
            && Math.Abs(other.Entry.MeterEnd.Value - meter) <= MeterTolerance);
    }

    private static double ResolveStartMeter(CodingEvent codingEvent)
        => codingEvent.Entry?.MeterStart ?? codingEvent.MeterAtCapture;

    private static bool SameCode(string? left, string? right)
        => string.Equals(
            (left ?? string.Empty).Trim(),
            (right ?? string.Empty).Trim(),
            StringComparison.OrdinalIgnoreCase);

    private static string Meter(double value)
        => value.ToString("0.00", CultureInfo.InvariantCulture) + "m";
}
