using System;
using System.Linq;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.UseCases.NaechsteAufgabe;

public enum HaltungPruefstand { Offen, KiAnalysiert, Abgeschlossen }

/// <summary>
/// Arbeitsstand mit Vorrang fuer offene KI-Befunde. Abgeschlossen bezeichnet nur die
/// persoenliche Erledigt-Markierung, niemals eine fachliche Freigabe oder den Sanierungsstand.
/// </summary>
public static class HaltungPruefstatus
{
    public static HaltungPruefstand Bestimme(HaltungRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        var entries = record.Protocol?.Current?.Entries;
        if (entries is not null && entries.Any(e => !e.IsDeleted && e.Ai is { Accepted: false }))
            return HaltungPruefstand.KiAnalysiert;
        return record.BearbeitungErledigt ? HaltungPruefstand.Abgeschlossen : HaltungPruefstand.Offen;
    }

    public static string Text(HaltungPruefstand stand) => stand switch
    {
        HaltungPruefstand.Abgeschlossen => "Bearbeitung erledigt",
        HaltungPruefstand.KiAnalysiert => "KI-Befunde zu prüfen",
        _ => "Bearbeitung offen"
    };

    public static bool HatVideo(HaltungRecord record)
        => !string.IsNullOrWhiteSpace(record.GetFieldValue(FieldKeys.Link));
}
