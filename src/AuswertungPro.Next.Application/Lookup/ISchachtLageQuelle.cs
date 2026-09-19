using AuswertungPro.Next.Application.Xtf;

namespace AuswertungPro.Next.Application.Lookup;

/// <summary>
/// Die Lage eines Schachts und die Richtung seiner Haltungen, wie sie die Schachtgrafik fuer
/// den Grundriss braucht: der Schachtpunkt (LV95) und je Haltungsname der Azimut, unter dem
/// die Leitung den Schacht verlaesst (0° = Nord, im Uhrzeigersinn). Haltungen ohne
/// eindeutige Geometrie fehlen im Woerterbuch — sie bleiben in der Grafik «ohne Richtung».
/// </summary>
public sealed record SchachtLage(XtfPunkt Schachtpunkt, IReadOnlyDictionary<string, double> AzimutJeHaltung);

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
    /// nicht oder mehrfach vorkommt. Sonst der Schachtpunkt und die Azimute aller
    /// <paramref name="haltungsnamen"/>, die eindeutig und am Schacht angeschlossen sind.
    /// </summary>
    SchachtLage? Lies(string schachtnummer, IReadOnlyCollection<string> haltungsnamen);
}
