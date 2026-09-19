using AuswertungPro.Next.Application.Xtf;

namespace AuswertungPro.Next.Application.Lookup;

/// <summary>
/// Eine Leitung der Kopie, die am Schachtpunkt beginnt oder endet, aber nicht unter den
/// gefragten Haltungsnamen ist: Hausanschluesse (<c>u-80792</c>, 16'112 Stueck in der Kopie
/// des Kantons) und Leitungen, deren Name im Projekt anders lautet.
/// <see cref="EndetImSchacht"/>: Das Linienende liegt am Schacht, die Leitung fuehrt also
/// hinein (Einlauf); sonst beginnt sie dort (Auslauf).
/// </summary>
public sealed record SchachtLageLeitung(string Name, double AzimutGrad, bool EndetImSchacht, int? DnMm, string? Material);

/// <summary>
/// Die Lage eines Schachts und die Richtung seiner Haltungen, wie sie die Schachtgrafik fuer
/// den Grundriss braucht: der Schachtpunkt (LV95) und je Haltungsname der Azimut, unter dem
/// die Leitung den Schacht verlaesst (0° = Nord, im Uhrzeigersinn). Haltungen ohne
/// eindeutige Geometrie fehlen im Woerterbuch — sie bleiben in der Grafik «ohne Richtung».
/// <see cref="WeitereLeitungen"/> sind die Leitungen am Schachtpunkt ausserhalb der gefragten
/// Namen; die Liste ist leer, wenn die Kopie keinen Raumindex hat.
/// </summary>
public sealed record SchachtLage(
    XtfPunkt Schachtpunkt,
    IReadOnlyDictionary<string, double> AzimutJeHaltung,
    IReadOnlyList<SchachtLageLeitung>? WeitereLeitungen = null)
{
    public IReadOnlyList<SchachtLageLeitung> WeitereLeitungen { get; init; } = WeitereLeitungen ?? [];
}

/// <summary>
/// Liest Schachtpunkt und Leitungsrichtungen aus einer Bestandsquelle — heute die lokalen
/// QGIS-Kopien. Ausschliesslich lesend, nur fuer die Anzeige. Ein mehrdeutiger Name liefert
/// nichts: Im Abwassernetz des Kantons tragen 334 Schachtnamen und 2574 Haltungsnamen mehr
/// als ein Objekt; einen davon zu nehmen waere geraten, und eine falsche Richtung faellt in
/// einer Grafik nicht auf.
/// </summary>
public interface ISchachtLageQuelle
{
    /// <summary>
    /// <c>null</c>, wenn keine Quelle eingerichtet ist, die Datei fehlt oder die Schachtnummer
    /// nicht oder mehrfach vorkommt. Sonst der Schachtpunkt, die Azimute aller
    /// <paramref name="haltungsnamen"/>, die eindeutig und am Schacht angeschlossen sind, und
    /// die weiteren Leitungen am Schachtpunkt.
    /// </summary>
    SchachtLage? Lies(string schachtnummer, IReadOnlyCollection<string> haltungsnamen);
}
