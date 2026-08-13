using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.Xtf;

public sealed record XtfRevisionExportRequest(
    Project Projekt,
    string ProjektPfad,
    string ZielOrdner,
    bool NurPruefen = false);

public sealed record XtfRevisionExportResult(
    bool Ok,
    string Bericht,
    string? Fehler,
    IReadOnlyList<string> Dateien);

/// <summary>
/// Erzeugt aus dem aktuellen Projektstand revidierte XTF-Dateien — eine je Originaldatei.
///
/// Die Kundenoriginale werden nur gelesen. Die Revisionen entstehen als neue Dateien in
/// einem eigenen Ausgabeordner mit Zeitstempel; nichts Bestehendes wird ueberschrieben.
/// Mit <see cref="XtfRevisionExportRequest.NurPruefen"/> entsteht ausschliesslich der
/// Bericht, ohne dass eine Datei geschrieben wird.
/// </summary>
public interface IXtfRevisionExportService
{
    XtfRevisionExportResult Erzeuge(XtfRevisionExportRequest request);
}
