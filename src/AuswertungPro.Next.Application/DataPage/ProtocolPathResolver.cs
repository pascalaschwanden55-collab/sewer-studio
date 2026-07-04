using System;
using System.Collections.Generic;
using System.Linq;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.DataPage;

/// <summary>
/// Reine (IO-freie) Hilfslogik fuer die Zuordnung von Haltungsrecords zu Protokoll-PDF-Tokens.
/// Aus DataPageProtocolPathResolver extrahiert: kein File.Exists, kein Directory,
/// keine WPF-Abhaengigkeit — nur String- und Listen-Operationen.
/// Die eigentliche Kandidatenauswahl und Pfadlisten-Verarbeitung liegt in
/// <see cref="PdfCandidateSelector"/>.
/// </summary>
public static class ProtocolPathResolver
{
    /// <summary>
    /// Baut die Suchtoken einer Haltung aus dem Record: sanitisierter Name plus Rohname
    /// (dedupliziert). Delegiert an <see cref="PdfCandidateSelector.BuildHoldingTokens(string?)"/>
    /// mit dem Haltungsname-Feldwert des Records.
    /// Gibt eine leere Liste zurueck, wenn der Haltungsname fehlt.
    /// </summary>
    public static IReadOnlyList<string> BuildHoldingTokens(HaltungRecord record)
    {
        var holdingRaw = (record.GetFieldValue("Haltungsname") ?? string.Empty).Trim();
        var tokens = new List<string>();
        tokens.AddRange(PdfCandidateSelector.BuildHoldingTokens(holdingRaw));

        var oben = (record.GetFieldValue("Schacht_oben") ?? string.Empty).Trim();
        var unten = (record.GetFieldValue("Schacht_unten") ?? string.Empty).Trim();
        if (!string.IsNullOrWhiteSpace(oben) && !string.IsNullOrWhiteSpace(unten))
        {
            tokens.AddRange(PdfCandidateSelector.BuildHoldingTokens($"{oben}-{unten}"));
            tokens.AddRange(PdfCandidateSelector.BuildHoldingTokens($"{unten}-{oben}"));
        }

        return tokens
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
