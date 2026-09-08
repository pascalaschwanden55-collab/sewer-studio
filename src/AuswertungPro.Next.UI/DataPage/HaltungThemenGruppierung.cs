using System;
using System.Collections.Generic;
using System.Linq;
using AuswertungPro.Next.UI.Views.Windows;

namespace AuswertungPro.Next.UI.DataPage;

/// <summary>
/// Ein Thema der Haltungs-Eingabefelder mit genau EINER Gruppe, damit der unveraenderte
/// <c>RecordDetailsView</c> es rendern kann.
/// </summary>
public sealed record ThemaAnzeige(string Title, IReadOnlyList<RecordDetailGroup> EinzelGruppe)
{
    /// <summary>Anzahl der Felder in diesem Thema, fuer den Zaehler im Expander-Kopf.</summary>
    public int Anzahl => EinzelGruppe.Sum(g => g.Items.Count);

    /// <summary>
    /// Die vier festen Prototyp-Themen starten aufgeklappt; "Weitere Angaben" (alle uebrigen
    /// Projektfelder) startet zugeklappt, damit es nicht vom eigentlichen Formular ablenkt.
    /// Das ist die Vorgabe, nicht der aktuelle Zustand.
    /// </summary>
    public bool IstStandardAufgeklappt => Title != HaltungThemenGruppierung.WeitereAngabenTitel;

    private bool? _istAufgeklappt;

    /// <summary>
    /// Der aktuelle Auf-/Zuklappzustand des Themas; Vorgabe ist
    /// <see cref="IstStandardAufgeklappt"/>.
    ///
    /// Er gehoert bewusst hierher und nicht an den Expander: Die Aufklapp-Liste virtualisiert,
    /// und beim Wechsel der Themenanordnung an der 1100-px-Schwelle werden die Expander neu
    /// gebaut. Laege der Zustand nur im Bedienelement, faende der Benutzer nach jedem Scrollen
    /// wieder die Vorgabe vor.
    /// </summary>
    public bool IstAufgeklappt
    {
        get => _istAufgeklappt ?? IstStandardAufgeklappt;
        set => _istAufgeklappt = value;
    }
}

/// <summary>
/// Nova, Aufklapp-Liste (Task 1): Die eine Regel, wie aus den Gruppen des
/// <see cref="DataPageRecordDetailsBuilder"/> die anzeigbaren Themen werden.
///
/// Sie wird von der Eingabefelder-Schublade (<c>HaltungFelderDrawer</c>) UND von der
/// Aufklapp-Liste gelesen. Zwei Kopien haetten frueher oder spaeter zwei Reihenfolgen und zwei
/// Zaehler ergeben. Bis auf <see cref="RecordDetailGroup"/> ist hier nichts WPF-abhaengig.
/// </summary>
public static class HaltungThemenGruppierung
{
    /// <summary>Thementitel, das standardmaessig zugeklappt startet (Nova-Etappe 2b).</summary>
    public const string WeitereAngabenTitel = "Weitere Angaben";

    /// <summary>Alle Themen in der Reihenfolge des Detail-Builders, ohne Filterung.</summary>
    public static IReadOnlyList<ThemaAnzeige> Bilde(IReadOnlyList<RecordDetailGroup>? gruppen)
        => Filtere(gruppen, null);

    /// <summary>
    /// Reine Filterregel: nur Felder, deren Beschriftung den Suchtext enthaelt. Ein danach
    /// leeres Thema erscheint nicht.
    /// </summary>
    public static IReadOnlyList<ThemaAnzeige> Filtere(IReadOnlyList<RecordDetailGroup>? gruppen, string? suche)
    {
        var q = (suche ?? string.Empty).Trim();
        return (gruppen ?? Array.Empty<RecordDetailGroup>())
            .Select(g => q.Length == 0
                ? g
                : g with { Items = g.Items.Where(i => i.Label.Contains(q, StringComparison.OrdinalIgnoreCase)).ToList() })
            .Where(g => g.Items.Count > 0)
            .Select(g => new ThemaAnzeige(g.Title, new[] { g }))
            .ToList();
    }
}
