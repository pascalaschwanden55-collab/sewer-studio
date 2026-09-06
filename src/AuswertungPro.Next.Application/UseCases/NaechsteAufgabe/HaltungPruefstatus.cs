using System;
using System.Linq;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.UseCases.NaechsteAufgabe;

public enum HaltungPruefstand { Offen, KiAnalysiert, Abgeschlossen }

/// <summary>
/// Nova-Etappe 2: fachlicher Pruefstatus einer Haltung (Prototyp: geprueft / analysiert / offen).
/// Abgeschlossen = Feld offen/abgeschlossen ist "abgeschlossen". KI analysiert = mindestens ein
/// offener KI-Befund im Protokoll. Sonst offen. Reine Rechnung, keine Datenaenderung.
/// </summary>
public static class HaltungPruefstatus
{
    public static HaltungPruefstand Bestimme(HaltungRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        if (string.Equals(record.GetFieldValue(FieldKeys.WorkflowStatus)?.Trim(), "abgeschlossen", StringComparison.OrdinalIgnoreCase))
            return HaltungPruefstand.Abgeschlossen;
        var entries = record.Protocol?.Current?.Entries;
        if (entries is not null && entries.Any(e => !e.IsDeleted && e.Ai is { Accepted: false }))
            return HaltungPruefstand.KiAnalysiert;
        return HaltungPruefstand.Offen;
    }

    public static string Text(HaltungPruefstand stand) => stand switch
    {
        HaltungPruefstand.Abgeschlossen => "fachlich geprüft",
        HaltungPruefstand.KiAnalysiert => "KI analysiert, Prüfung offen",
        _ => "nicht analysiert"
    };

    public static bool HatVideo(HaltungRecord record)
        => !string.IsNullOrWhiteSpace(record.GetFieldValue(FieldKeys.Link));
}
