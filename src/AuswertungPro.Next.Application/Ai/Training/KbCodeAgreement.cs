using System;
using System.Collections.Generic;
using System.Linq;

namespace AuswertungPro.Next.Application.Ai.Training;

/// <summary>Ergebnis des KB-Abgleichs (Weg 1): stimmt der KI-Code mit der KB-Mehrheit ueberein?</summary>
public enum KbCheckResult
{
    /// <summary>Keine (verwertbaren) KB-Treffer -> neutral.</summary>
    KbNoSignal,
    /// <summary>KI-Code stimmt mit dem Mehrheits-Code der KB-Nachbarn ueberein -> starker Kandidat.</summary>
    KbAgreement,
    /// <summary>KB-Mehrheit widerspricht dem KI-Code -> zwingend Review.</summary>
    KbDisagreement
}

/// <summary>
/// Vergleicht den KI-Code gegen die Mehrheit der KB-Top-Treffer (Ground-Truth-basiert).
/// Reine, deterministische Logik (kein I/O). Defensiv: bei Unklarheit lieber Disagreement/NoSignal.
/// Vergleich auf Basis des 3-stelligen Hauptcodes (z.B. BAI/BAF/BCC).
/// </summary>
public static class KbCodeAgreement
{
    public static KbCheckResult Classify(string? kiCode, IReadOnlyList<string>? kbTopCodes)
    {
        if (string.IsNullOrWhiteSpace(kiCode) || kbTopCodes is null || kbTopCodes.Count == 0)
            return KbCheckResult.KbNoSignal;

        var ki = Base(kiCode);
        if (ki.Length == 0)
            return KbCheckResult.KbNoSignal;

        var majority = kbTopCodes
            .Select(Base)
            .Where(c => c.Length > 0)
            .GroupBy(c => c)
            .OrderByDescending(g => g.Count())
            .ThenBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => g.Key)
            .FirstOrDefault();

        if (string.IsNullOrEmpty(majority))
            return KbCheckResult.KbNoSignal;

        return majority == ki ? KbCheckResult.KbAgreement : KbCheckResult.KbDisagreement;
    }

    /// <summary>3-stelliger Hauptcode in Grossbuchstaben (Punkt-Notation entfernt).</summary>
    private static string Base(string? code)
    {
        var c = (code ?? string.Empty).Trim().ToUpperInvariant().Split('.')[0];
        return c.Length >= 3 ? c[..3] : c;
    }
}
