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

        if (endEntry is null && haltungslaengeM > 0)
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
                Flags = new List<string> { "foto_required", "auto_boundary" }
            }
        };
    }
}

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
