using System.Collections.Generic;
using System.Linq;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.UseCases.NaechsteAufgabe;

/// <summary>Inventar 8.1: erste KI-analysierte Haltung, sonst erste offene mit Video, sonst keine.</summary>
public static class NaechsteAufgabeRegel
{
    public static HaltungRecord? Naechste(IEnumerable<HaltungRecord> haltungen)
    {
        var liste = haltungen.ToList();
        return liste.FirstOrDefault(h => HaltungPruefstatus.Bestimme(h) == HaltungPruefstand.KiAnalysiert)
            ?? liste.FirstOrDefault(h => HaltungPruefstatus.Bestimme(h) == HaltungPruefstand.Offen && HaltungPruefstatus.HatVideo(h));
    }

    public static string ChipText(HaltungRecord? naechste)
        => naechste is null
            ? "Keine offene Prüfung"
            : $"Nächste Aufgabe: {naechste.GetFieldValue(FieldKeys.HoldingName)} prüfen";
}
