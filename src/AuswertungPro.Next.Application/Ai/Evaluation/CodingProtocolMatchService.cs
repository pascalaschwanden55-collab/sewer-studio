using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.Application.Ai.Evaluation;

/// <summary>
/// Ergebnis des Codiermodus-Abgleichs, schon in Handlungs-Toepfe geroutet.
/// Es wird NICHTS automatisch uebernommen – hoher Score liefert einen Vorschlag,
/// kein Auto-Training (Schutz gegen Leakage / Selbstvergiftung).
/// </summary>
public sealed record CodingMatchRouting(
    BefundMatchResult Match,
    IReadOnlyList<BefundMatchPair> Trainingskandidaten,  // gruene Treffer: KI == Original am selben Meter (sicher)
    IReadOnlyList<BefundMatchPair> ReviewGelb,           // gelbe Treffer: wahrscheinlich, kurz pruefen
    IReadOnlyList<BefundMatchPair> FalscherCodeReview,   // richtige Stelle, falscher Code: korrigieren (Trainings-Gold)
    IReadOnlyList<BefundMatchFinding> Verpasst,          // Original-Befund, den die KI nicht fand
    IReadOnlyList<BefundMatchFinding> Fehlalarm)         // KI-Befund ohne Partner im Original
{
    /// <summary>Anzahl gewerteter Original-/KI-Befunde in allen vier Toepfen.</summary>
    public int Gesamt => Match.Treffer.Count + Match.FalscherCode.Count + Match.Verpasst.Count + Match.Fehlalarm.Count;
}

/// <summary>
/// Vergleicht im Codiermodus die KI-Befunde gegen das importierte Original-Protokoll
/// (Referenz-Codierung). Reine Logik – nutzt den geteilten <see cref="BefundMatcher"/>
/// und routet das Ergebnis in Trainings-/Review-/Fehler-Toepfe. Keine UI, kein I/O.
/// </summary>
public static class CodingProtocolMatchService
{
    /// <summary>Bildet einen Protokoll-Eintrag auf einen Match-Befund ab. Fehlende Meter → 0 bzw. Punktschaden.</summary>
    public static BefundMatchFinding ToFinding(ProtocolEntry e)
    {
        double meterStart = e.MeterStart ?? 0.0;
        double meterEnd = e.MeterEnd ?? meterStart;
        return new BefundMatchFinding(e.Code, meterStart, meterEnd, e.Beschreibung, e.EntryId.ToString());
    }

    /// <summary>
    /// Gleicht zwei explizite Listen ab: <paramref name="original"/> = importierte Referenz,
    /// <paramref name="ki"/> = KI-erkannte Befunde. Die UI entscheidet, was in welche Liste gehoert.
    /// </summary>
    public static CodingMatchRouting Match(
        IReadOnlyList<ProtocolEntry> original,
        IReadOnlyList<ProtocolEntry> ki,
        BefundMatchOptions? options = null)
    {
        var originalFindings = original.Select(ToFinding).ToList();
        var kiFindings = ki.Select(ToFinding).ToList();
        var result = BefundMatcher.Match(originalFindings, kiFindings, options);
        return Route(result);
    }

    /// <summary>
    /// Bequemlichkeit: zieht Original (Quelle = Imported) und KI (Quelle = Ai) direkt aus
    /// den Events einer Session. Manuelle Eintraege bleiben aussen vor.
    /// </summary>
    public static CodingMatchRouting MatchSession(CodingSession session, BefundMatchOptions? options = null)
    {
        var original = session.Events
            .Where(ev => !ev.Entry.IsDeleted && ev.Entry.Source == ProtocolEntrySource.Imported)
            .Select(ev => ev.Entry)
            .ToList();
        var ki = session.Events
            .Where(ev => !ev.Entry.IsDeleted && ev.Entry.Source == ProtocolEntrySource.Ai)
            .Select(ev => ev.Entry)
            .ToList();
        return Match(original, ki, options);
    }

    private static CodingMatchRouting Route(BefundMatchResult r)
        => new(
            Match: r,
            Trainingskandidaten: r.Treffer.Where(p => p.Tier == "gruen").ToList(),
            ReviewGelb: r.Treffer.Where(p => p.Tier == "gelb").ToList(),
            FalscherCodeReview: r.FalscherCode,
            Verpasst: r.Verpasst,
            Fehlalarm: r.Fehlalarm);
}
