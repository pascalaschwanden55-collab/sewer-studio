using System.Collections.Generic;

namespace AuswertungPro.Next.Application.Lookup;

/// <summary>
/// Ein Bauteil aus dem QGIS-Bestand, mit seinen Rohwerten je Spalte.
///
/// <see cref="Werte"/> traegt die Spaltennamen der GeoPackage-Tabelle
/// (<c>ha_material</c>, <c>ns_funktion</c>, …) — die Uebersetzung in Projektfelder
/// macht <see cref="QgisFeldKarte"/>, nicht der Leser.
/// </summary>
public sealed record QgisBauteil(string Name, IReadOnlyDictionary<string, string> Werte);

/// <summary>
/// Der QGIS-Bestand einer Bauteilart, nachschlagbar ueber den Namen.
///
/// <see cref="Mehrdeutig"/> nennt die Namen, die im Bestand mehrfach vorkommen.
/// Sie liefern bewusst KEIN Bauteil: Im Abwassernetz des Kantons tragen 2574
/// Haltungsnamen mehr als ein Objekt (9910 Leitungen, 9,0 %) und 334
/// Schachtnamen (823 Schaechte). Einen davon zu nehmen waere geraten.
/// </summary>
public sealed record QgisBestand(
    IReadOnlyDictionary<string, QgisBauteil> JeName,
    IReadOnlySet<string> Mehrdeutig,
    int GeleseneObjekte)
{
    /// <summary>Das eindeutig zugeordnete Bauteil, oder <c>null</c>.</summary>
    public QgisBauteil? Finde(string? name)
    {
        var text = (name ?? "").Trim();
        return text.Length > 0 && JeName.TryGetValue(text, out var bauteil) ? bauteil : null;
    }

    public bool IstMehrdeutig(string? name)
        => Mehrdeutig.Contains((name ?? "").Trim());
}

/// <summary>
/// Liest den QGIS-Bestand einer Bauteilart. Ausschliesslich lesend; die
/// GeoPackage-Datei des Benutzers bleibt unveraendert.
/// </summary>
public interface IQgisBestandLeser
{
    /// <summary>
    /// Liest den Bestand. Wirft bei einer fehlenden oder unlesbaren Datei —
    /// eine leere Antwort waere von "nichts gefunden" nicht zu unterscheiden.
    /// </summary>
    QgisBestand Lies(BauteilArt art);

    /// <summary>Der Pfad, aus dem gelesen wird — fuer den Bericht.</summary>
    string Quellpfad(BauteilArt art);
}
