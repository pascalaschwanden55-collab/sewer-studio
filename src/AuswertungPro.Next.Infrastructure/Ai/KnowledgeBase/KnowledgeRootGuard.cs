using System;
using System.IO;

namespace AuswertungPro.Next.Infrastructure.Ai.KnowledgeBase;

/// <summary>Art der Startwarnung zur Wissensdatenbank (AP-06).</summary>
public enum KnowledgeRootWarnungArt
{
    Keine,
    RootGewechselt,
    LeereOderNeueDb,
    SampleEinbruch
}

/// <summary>Ergebnis der Startpruefung der Wissensdatenbank.</summary>
public sealed record KnowledgeRootGuardResult(KnowledgeRootWarnungArt Art, string? Meldung)
{
    public bool HatWarnung => Art != KnowledgeRootWarnungArt.Keine;
    public static readonly KnowledgeRootGuardResult Ok = new(KnowledgeRootWarnungArt.Keine, null);
}

/// <summary>
/// Warnt beim Start, wenn die App unbemerkt mit einer anderen oder leeren
/// Wissensdatenbank laeuft (Split-Brain durch verlorene Umgebungsvariable).
/// Reine Entscheidungslogik ohne Datei-/UI-Zugriff — voll testbar.
/// </summary>
public static class KnowledgeRootGuard
{
    // Sample-Einbruch nur melden, wenn der letzte Bestand substanziell war —
    // sonst Fehlalarme, solange die KB erst aufgebaut wird.
    private const int MinRelevanterBestand = 50;

    public static KnowledgeRootGuardResult Evaluate(
        string currentRoot,
        string? lastKnownRoot,
        bool dbExisted,
        int currentSampleCount,
        int? lastKnownSampleCount)
    {
        // Erststart: noch nichts gemerkt -> keine Warnung, nur Werte uebernehmen.
        if (string.IsNullOrWhiteSpace(lastKnownRoot))
            return KnowledgeRootGuardResult.Ok;

        // 1. Root-Wechsel = Split-Brain-Gefahr (hoechste Prioritaet).
        if (!PfadeGleich(currentRoot, lastKnownRoot))
            return new(KnowledgeRootWarnungArt.RootGewechselt,
                "Die Wissensdatenbank liegt jetzt in einem anderen Ordner als beim letzten Start.\n" +
                $"Zuletzt: {lastKnownRoot}\n" +
                $"Jetzt: {currentRoot}\n" +
                "Falls das nicht gewollt ist, pruefe die Umgebungsvariable SEWERSTUDIO_KNOWLEDGE_ROOT — " +
                "sonst landen neue Trainingsdaten in einer anderen Wissensdatenbank.");

        // 2. Gleicher Ordner, aber die DB war weg -> neu/leer angelegt.
        if (!dbExisted)
            return new(KnowledgeRootWarnungArt.LeereOderNeueDb,
                $"Die Wissensdatenbank wurde neu (leer) angelegt: {currentRoot}\n" +
                "Beim letzten Start lag hier bereits eine Datenbank. Wurde sie geloescht oder verschoben? " +
                "Bitte eine Datensicherung pruefen, bevor du weiterarbeitest.");

        // 3. Sample-Einbruch ueber 90 % bei vorher substanziellem Bestand.
        if (lastKnownSampleCount is int last
            && last >= MinRelevanterBestand
            && currentSampleCount < last / 10)
            return new(KnowledgeRootWarnungArt.SampleEinbruch,
                "Die Wissensdatenbank enthaelt viel weniger Beispiele als beim letzten Start " +
                $"(jetzt {currentSampleCount}, zuletzt {last}). Moeglicher Datenverlust — bitte eine Datensicherung pruefen.");

        return KnowledgeRootGuardResult.Ok;
    }

    private static bool PfadeGleich(string a, string b)
    {
        try
        {
            return string.Equals(Normalisiere(a), Normalisiere(b), StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            // Bei ungueltigen Pfaden auf reinen Text-Vergleich zurueckfallen.
            return string.Equals(a?.Trim(), b?.Trim(), StringComparison.OrdinalIgnoreCase);
        }
    }

    private static string Normalisiere(string pfad)
        => Path.TrimEndingDirectorySeparator(Path.GetFullPath(pfad.Trim()));
}
