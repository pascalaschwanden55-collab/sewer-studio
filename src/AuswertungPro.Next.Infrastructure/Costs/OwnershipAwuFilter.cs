using System;
using System.Collections.Generic;
using System.Text;

namespace AuswertungPro.Next.Infrastructure.Costs;

/// <summary>
/// Erkennt Eigentum "Abwasser Uri (AWU)" tolerant und bildet die Menge der AWU-Schaechte.
/// Genutzt fuer das NPK-135-Leistungsverzeichnis, das ZWINGEND nur AWU-Haltungen bzw.
/// -Schaechte enthaelt (Private werden separat/einzeln abgehandelt).
/// Rein, ohne Abhaengigkeiten, unit-testbar.
/// </summary>
public static class OwnershipAwuFilter
{
    /// <summary>
    /// True, wenn der Eigentuemer-Wert AWU meint. Tolerant: exakt "AWU" ODER Freitext, der
    /// "Abwasser Uri" enthaelt (Gross-/Kleinschreibung egal). Import-Schreibweisen
    /// (XTF/WinCan/KINS) koennen vom Whitelist-Wert "AWU" abweichen.
    /// </summary>
    public static bool IsAwu(string? owner)
    {
        if (string.IsNullOrWhiteSpace(owner))
            return false;

        var normalized = owner.Trim().ToLowerInvariant();
        return normalized == "awu" || normalized.Contains("abwasser uri", StringComparison.Ordinal);
    }

    /// <summary>
    /// Baut die Menge der AWU-Schacht-Nummern (normalisiert) aus (Schachtnummer, Eigentuemer)-Paaren.
    /// Nur Schaechte, deren Eigentuemer AWU ist (via <see cref="IsAwu"/>). Leere Nummern entfallen.
    /// </summary>
    public static HashSet<string> AwuSchachtKeys(IEnumerable<(string? Schachtnummer, string? Owner)> schaechte)
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);
        if (schaechte is null)
            return keys;

        foreach (var (nummer, owner) in schaechte)
        {
            if (!IsAwu(owner))
                continue;

            var key = NormalizeSchacht(nummer);
            if (key.Length > 0)
                keys.Add(key);
        }

        return keys;
    }

    /// <summary>
    /// Normalisiert eine Schachtnummer fuer den Abgleich: Trim, Innen-Whitespace entfernen,
    /// Grossbuchstaben — so matcht "KS 60191" (Kostendatei) auf "KS60191" (Schachtdatensatz).
    /// </summary>
    public static string NormalizeSchacht(string? schachtnummer)
    {
        if (string.IsNullOrWhiteSpace(schachtnummer))
            return "";

        var builder = new StringBuilder(schachtnummer.Length);
        foreach (var ch in schachtnummer)
        {
            if (char.IsWhiteSpace(ch))
                continue;
            builder.Append(char.ToUpperInvariant(ch));
        }

        return builder.ToString();
    }
}
