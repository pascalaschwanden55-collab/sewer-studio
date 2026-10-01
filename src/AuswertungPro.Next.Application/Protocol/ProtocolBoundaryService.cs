using System;
using System.Collections.Generic;
using System.Linq;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.Application.Protocol;

/// <summary>
/// Stellt sicher, dass jedes Protokoll die Grundregeln einhält:
///   1. Erster Eintrag = BCD (Rohranfang) bei 0.00m
///   2. Letzter Eintrag = BCE (Rohrende) oder BDC* (Abbruch)
///   3. Inspektionslänge = Meter von BCD bis BCE/BDC*
///   4. Foto-Flag fuer BCD und BCE/BDC*
/// Gilt gleichermassen fuer Operator- und KI-Protokolle.
/// </summary>
public static class ProtocolBoundaryService
{
    public const string CodeRohranfang = "BCD";
    public const string CodeRohrende = "BCE";
    public const string AbortPrefix = "BDC";

    /// <summary>
    /// Rechenungenauigkeit beim Vergleich zweier Meterwerte. Meter werden auf zwei
    /// Stellen gerundet; ein halber Zentimeter trennt sicher gleich von groesser.
    /// </summary>
    private const double MeterEpsilon = 0.005;

    /// <summary>
    /// Kennzeichen einer selbst ergaenzten Rohrgrenze.
    ///
    /// ACHTUNG, geprueft und verworfen: Dieses Kennzeichen taugt NICHT dazu, solche
    /// Eintraege beim Abgleich mit der Ereignisliste vor dem Loeschen zu schuetzen.
    /// Beim erneuten Oeffnen des Codiermodus laedt
    /// <c>CodingSessionService.LoadExistingObservations</c> jeden Protokolleintrag als
    /// Ereignis - auch BCD und BCE. Eine dort vom Menschen geloeschte Zeile ist danach
    /// von einer nie eingetragenen nicht mehr zu unterscheiden. Ein solcher Schutz
    /// macht ein falsches Rohrende ueber den Codiermodus unloeschbar.
    /// </summary>
    public const string AutoBoundaryFlag = "auto_boundary";

    /// <summary>
    /// Ergaenzt fehlende Rohranfang/Rohrende-Eintraege und setzt Foto-Flags.
    /// Gibt Inspektionslänge zurueck (oder null wenn kein Endpunkt vorhanden).
    /// </summary>
    public static ProtocolBoundaryResult EnsureBoundaries(
        List<ProtocolEntry> entries,
        double haltungslaengeM,
        string? photoDir = null)
    {
        var result = new ProtocolBoundaryResult();
        var active = entries.Where(e => !e.IsDeleted).ToList();

        // --- 1. Rohranfang (BCD) bei 0.00m sicherstellen ---
        var rohranfang = active.FirstOrDefault(e =>
            string.Equals(e.Code, CodeRohranfang, StringComparison.OrdinalIgnoreCase));

        if (rohranfang is null)
        {
            rohranfang = CreateBoundaryEntry(CodeRohranfang, "Rohranfang", 0.0);
            entries.Insert(0, rohranfang);
            result.RohranfangInserted = true;
        }
        else
        {
            // Meter immer auf 0.00 korrigieren
            rohranfang.MeterStart = 0.0;
            rohranfang.MeterEnd = null;
        }

        // Foto-Flag setzen
        rohranfang.Ai ??= new ProtocolEntryAiMeta();
        if (!rohranfang.Ai.Flags.Contains("foto_required"))
            rohranfang.Ai.Flags.Add("foto_required");
        result.RohranfangEntry = rohranfang;

        // --- 2. Rohrende (BCE) oder Abbruch (BDC*) pruefen ---
        var endEntry = active.LastOrDefault(e =>
            string.Equals(e.Code, CodeRohrende, StringComparison.OrdinalIgnoreCase)
            || (e.Code?.StartsWith(AbortPrefix, StringComparison.OrdinalIgnoreCase) ?? false));

        // Dieselbe Grenze wie in PlanBoundaries: Ein Rohrende liegt hinter jeder
        // Beobachtung. Eine ersatzweise aus dem hoechsten Protokollmeter
        // abgeleitete "Haltungslaenge" wuerde es sonst auf den letzten Befund
        // setzen. Zwei Fassungen derselben Regel duerfen nicht auseinanderlaufen.
        if (endEntry is null && IstPlausiblesRohrende(haltungslaengeM, LetzteBeobachtung(active)))
        {
            // Standard: Rohrende am Ende der Haltung einfuegen
            endEntry = CreateBoundaryEntry(CodeRohrende, "Rohrende", haltungslaengeM);
            entries.Add(endEntry);
            result.EndInserted = true;
        }

        if (endEntry is not null)
        {
            // Foto-Flag setzen
            endEntry.Ai ??= new ProtocolEntryAiMeta();
            if (!endEntry.Ai.Flags.Contains("foto_required"))
                endEntry.Ai.Flags.Add("foto_required");

            result.EndEntry = endEntry;
            result.IsAbort = endEntry.Code?.StartsWith(AbortPrefix, StringComparison.OrdinalIgnoreCase) ?? false;

            // --- 3. Inspektionslänge berechnen ---
            var endMeter = endEntry.MeterStart ?? endEntry.MeterEnd ?? haltungslaengeM;
            result.Inspektionslaenge = Math.Round(endMeter, 2);
        }

        return result;
    }

    /// <summary>
    /// Meldet schreibfrei, welche Rohrgrenze fehlt. Der Codiermodus ergaenzt daraufhin
    /// den Rohranfang still (0.00 m stimmt immer), fragt beim Rohrende aber nach:
    /// Ein automatisch gesetztes BCE wuerde behaupten, die ganze Haltung sei befahren
    /// worden - bei einem Abbruch waere das falsch.
    /// Ein vom Menschen gesetzter Rohranfang wird hier NICHT auf 0.00 m verschoben.
    /// </summary>
    public static ProtocolBoundaryPlan PlanBoundaries(
        IReadOnlyList<ProtocolEntry> entries,
        double? haltungslaengeM)
    {
        ArgumentNullException.ThrowIfNull(entries);

        var active = entries.Where(e => !e.IsDeleted).ToList();

        var hasStart = active.Any(e =>
            string.Equals(e.Code, CodeRohranfang, StringComparison.OrdinalIgnoreCase));

        var hasEnd = active.Any(e =>
            string.Equals(e.Code, CodeRohrende, StringComparison.OrdinalIgnoreCase)
            || (e.Code?.StartsWith(AbortPrefix, StringComparison.OrdinalIgnoreCase) ?? false));

        // Ein Rohrende liegt hinter jeder Beobachtung. Fehlt in den Stammdaten
        // jede Laenge, traegt der Codiermodus beim Einstieg ersatzweise den
        // hoechsten Protokollmeter als "Haltungslaenge_m" nach - und der ist,
        // wenn zugleich das Rohrende fehlt, genau der letzte Befund. Ein daraus
        // gebauter Vorschlag saesse auf dem Schaden und wuerde dem Benutzer als
        // "Haltungslaenge" angeboten. Lieber kein Vorschlag als ein erfundener:
        // dann setzt der Mensch das Rohrende von Hand.
        var gerundet = haltungslaengeM is > 0
            ? Math.Round(haltungslaengeM.Value, 2)
            : (double?)null;

        var letzteBeobachtung = LetzteBeobachtung(active);
        var taugt = gerundet is { } wert && IstPlausiblesRohrende(wert, letzteBeobachtung);
        var proposal = taugt ? gerundet : null;

        return new ProtocolBoundaryPlan(
            PipeStartMissing: !hasStart,
            PipeEndMissing: !hasEnd,
            PipeEndProposalMeter: hasEnd ? null : proposal,
            // Eine bekannte, aber unbrauchbare Laenge geht mit: nur so kann der
            // Dialog den Unterschied zwischen "keine Laenge" und "diese Laenge
            // taugt nicht als Rohrende" benennen.
            RejectedLengthM: hasEnd || taugt ? null : gerundet,
            LastObservationM: letzteBeobachtung);
    }

    /// <summary>
    /// Der hoechste Meter, an dem im Protokoll etwas beobachtet wurde - Anfang
    /// oder Ende eines Eintrags. Ohne Eintraege 0.
    /// </summary>
    private static double LetzteBeobachtung(IReadOnlyList<ProtocolEntry> active)
        => active
            .SelectMany(e => new[] { e.MeterStart, e.MeterEnd })
            .Where(meter => meter.HasValue)
            .Select(meter => meter!.Value)
            .DefaultIfEmpty(0)
            .Max();

    /// <summary>
    /// Taugt dieser Meter als Rohrende? Nur, wenn er hinter jeder Beobachtung
    /// liegt. Ein Wert auf oder vor dem letzten Befund ist keine Haltungslaenge,
    /// sondern aus dem Protokoll zurueckgerechnet.
    /// </summary>
    private static bool IstPlausiblesRohrende(double haltungslaengeM, double letzteBeobachtung)
        => haltungslaengeM > 0 && haltungslaengeM > letzteBeobachtung + MeterEpsilon;

    /// <summary>Fuegt den Rohranfang (BCD) bei 0.00 m vorne ein und gibt ihn zurueck.</summary>
    public static ProtocolEntry InsertPipeStart(List<ProtocolEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        var entry = CreateBoundaryEntry(CodeRohranfang, "Rohranfang", 0.0);
        entries.Insert(0, entry);
        return entry;
    }

    /// <summary>Haengt das Rohrende (BCE) am gegebenen Meter an und gibt es zurueck.</summary>
    public static ProtocolEntry AppendPipeEnd(List<ProtocolEntry> entries, double meter)
    {
        ArgumentNullException.ThrowIfNull(entries);

        var entry = CreateBoundaryEntry(CodeRohrende, "Rohrende", Math.Round(meter, 2));
        entries.Add(entry);
        return entry;
    }

    /// <summary>
    /// Prüft ob ein Protokoll gültige Grenzen hat (ohne etwas zu ändern).
    /// </summary>
    public static ProtocolValidationResult Validate(IReadOnlyList<ProtocolEntry> entries)
    {
        var active = entries.Where(e => !e.IsDeleted).ToList();
        var warnings = new List<string>();

        // BCD prüfen
        var rohranfang = active.FirstOrDefault(e =>
            string.Equals(e.Code, CodeRohranfang, StringComparison.OrdinalIgnoreCase));

        if (rohranfang is null)
            warnings.Add("Kein Rohranfang (BCD) vorhanden.");
        else if (rohranfang.MeterStart is not null and not 0.0)
            warnings.Add($"Rohranfang (BCD) nicht bei 0.00m (ist bei {rohranfang.MeterStart:F2}m).");

        // Endpunkt prüfen
        var hasEnd = active.Any(e =>
            string.Equals(e.Code, CodeRohrende, StringComparison.OrdinalIgnoreCase)
            || (e.Code?.StartsWith(AbortPrefix, StringComparison.OrdinalIgnoreCase) ?? false));

        if (!hasEnd)
            warnings.Add("Kein Rohrende (BCE) oder Abbruch (BDC*) vorhanden.");

        // BCD muss erster Eintrag sein (nach Meter-Sortierung)
        if (rohranfang is not null)
        {
            var firstByMeter = active
                .Where(e => e.MeterStart.HasValue)
                .OrderBy(e => e.MeterStart!.Value)
                .FirstOrDefault();

            if (firstByMeter is not null && firstByMeter.EntryId != rohranfang.EntryId
                && (firstByMeter.MeterStart ?? 0) < (rohranfang.MeterStart ?? 0))
            {
                warnings.Add("Rohranfang (BCD) ist nicht der erste Eintrag nach Meterwert.");
            }
        }

        return new ProtocolValidationResult
        {
            IsValid = warnings.Count == 0,
            Warnings = warnings
        };
    }

    private static ProtocolEntry CreateBoundaryEntry(string code, string beschreibung, double meter)
    {
        return new ProtocolEntry
        {
            EntryId = Guid.NewGuid(),
            Code = code,
            Beschreibung = beschreibung,
            MeterStart = meter,
            Source = ProtocolEntrySource.Manual,
            Ai = new ProtocolEntryAiMeta
            {
                Flags = new List<string> { "foto_required", AutoBoundaryFlag }
            },
            Training = new ProtocolEntryTrainingMeta
            {
                SkipAutomaticPersistence = true,
                SkipReason = "Automatisch ergaenzte Rohrgrenze"
            }
        };
    }
}

/// <summary>Welche Rohrgrenze fehlt, und welcher Meter waere fuer das Rohrende plausibel.</summary>
public sealed record ProtocolBoundaryPlan(
    bool PipeStartMissing,
    bool PipeEndMissing,
    double? PipeEndProposalMeter,

    /// <summary>
    /// Eine bekannte Haltungslaenge, die als Rohrende NICHT taugt, weil sie nicht
    /// hinter der letzten Beobachtung liegt. <c>null</c> heisst: entweder gibt es
    /// gar keine Laenge, oder sie wurde als Vorschlag angenommen. Der Unterschied
    /// entscheidet, was der Benutzer zu lesen bekommt.
    /// </summary>
    double? RejectedLengthM = null,

    /// <summary>Hoechster Meter, an dem im Protokoll etwas beobachtet wurde.</summary>
    double LastObservationM = 0);

public sealed class ProtocolBoundaryResult
{
    public ProtocolEntry? RohranfangEntry { get; set; }
    public ProtocolEntry? EndEntry { get; set; }
    public bool RohranfangInserted { get; set; }
    public bool EndInserted { get; set; }
    public bool IsAbort { get; set; }

    /// <summary>Inspektionslänge in Meter (BCD bis BCE/BDC*).</summary>
    public double? Inspektionslaenge { get; set; }
}

public sealed class ProtocolValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Warnings { get; set; } = new();
}
