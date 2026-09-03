using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.Xtf;

public sealed record XtfRevisionExportRequest(
    Project Projekt,
    string ProjektPfad,
    string ZielOrdner,
    bool NurPruefen = false,
    IReadOnlyList<string>? Quelldateien = null);

public sealed record XtfRevisionExportResult(
    bool Ok,
    string Bericht,
    string? Fehler,
    IReadOnlyList<string> Dateien,
    bool QuelleFehlt = false,
    IReadOnlyList<XtfRevisionPlan>? Plaene = null);

/// <summary>
/// Erzeugt aus dem aktuellen Projektstand revidierte XTF-Dateien — eine je Originaldatei.
///
/// Die Kundenoriginale werden nur gelesen. Die Revisionen entstehen als neue Dateien in
/// einem eigenen Ausgabeordner mit Zeitstempel; nichts Bestehendes wird ueberschrieben.
/// Mit <see cref="XtfRevisionExportRequest.NurPruefen"/> entsteht ausschliesslich der
/// Bericht, ohne dass eine Datei geschrieben wird. Optional gewaehlte
/// <see cref="XtfRevisionExportRequest.Quelldateien"/> werden nur gelesen und ersetzen
/// fuer diesen Lauf die Suche im Projektordner.
/// </summary>
public interface IXtfRevisionExportService
{
    XtfRevisionExportResult Erzeuge(XtfRevisionExportRequest request);

    /// <summary>
    /// Die Importkopien der Original-XTF im Projekt, neueste zuerst — dieselbe Suche, die
    /// <see cref="Erzeuge"/> ohne ausdrueckliche Quelldateien verwendet. Leer ohne Projektpfad
    /// oder ohne Ablage; wirft nie, damit die Seite vor dem Start etwas anzeigen kann.
    /// </summary>
    IReadOnlyList<UseCases.Xtf.XtfProjektkopie> FindeProjektkopien(string? projektPfad);
}
