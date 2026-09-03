using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.Xtf;

/// <summary>
/// Liefert die Linienzuege der Haltungen aus einer Bestandsquelle — heute die lokale
/// QGIS-Kopie. Ein Name, der dort mehrfach vorkommt, liefert nichts.
/// </summary>
public interface IXtfVerlaufQuelle
{
    /// <summary>Die Verlaeufe je Haltungsname. Leer, wenn keine Quelle eingerichtet ist.</summary>
    IReadOnlyDictionary<string, XtfNeuGeometrie> Lies();

    /// <summary>Die Quelle, aus der gelesen wird — fuer den Bericht.</summary>
    string? Quellpfad { get; }
}

public sealed record XtfNeuExportRequest(
    Project Projekt,
    string ZielOrdner,
    bool NurPruefen = false);

public sealed record XtfNeuExportResult(
    bool Ok,
    string Bericht,
    string? Fehler,
    string? Datei);

/// <summary>
/// Erzeugt aus dem ganzen Projektstand eine eigenstaendige, NEUE SIA405-XTF.
///
/// Der Gegenpart ist <see cref="IXtfRevisionExportService"/>: Der ergaenzt eine vorhandene
/// Kundendatei um Handaenderungen und braucht sie deshalb. Dieser Weg braucht keine
/// Quelldatei und schreibt alles selbst. Eine einzelne vorhandene Objekt-ID reicht nicht
/// fuer den ganzen SIA405-Objektverbund; der Neu-Export vergibt deshalb eigene TIDs.
///
/// Exportiert wird, was das Projekt enthaelt. Objekte, denen eine Pflichtangabe fehlt,
/// erscheinen im Bericht statt in der Datei. Mit
/// <see cref="XtfNeuExportRequest.NurPruefen"/> entsteht ausschliesslich der Bericht.
/// </summary>
public interface IXtfNeuExportService
{
    XtfNeuExportResult Erzeuge(XtfNeuExportRequest request);
}
