using AuswertungPro.Next.Application.Xtf;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.UseCases.Xtf;

/// <summary>Was "Bestehende Katasterdaten aktualisieren" braucht: Projekt, Projektdatei, Zielordner.</summary>
public sealed record XtfAktualisierenRequest(Project Projekt, string ProjektPfad, string ZielOrdner);

/// <summary>
/// Was die Oberflaeche dem Ablauf leiht. <see cref="WaehleQuelldateien"/> liefert leer, wenn der
/// Nutzer abbricht. <see cref="BestaetigeVorschau"/> gibt true zurueck, wenn geschrieben werden
/// darf. <see cref="ZeigeFehler"/> zeigt eine Vorschau mit <c>IstFehler = true</c>.
/// </summary>
public sealed record XtfExportActions(
    Func<IReadOnlyList<string>> WaehleQuelldateien,
    Func<XtfExportVorschau, bool> BestaetigeVorschau,
    Action<XtfExportVorschau> ZeigeFehler);

/// <summary>Ergebnis fuer die Statuszeile: ob geschrieben wurde, ein kurzer Satz, der Ausgabeordner.</summary>
public sealed record XtfExportErgebnis(bool Geschrieben, string Meldung, string? Ordner);

/// <summary>
/// Der Ablauf hinter "Bestehende Katasterdaten aktualisieren": pruefen, bei fehlender
/// Projektkopie die Original-XTF erfragen, die Alt/Neu-Vorschau bestaetigen lassen, erst
/// dann schreiben. Vor der Bestaetigung wird nie geschrieben; eine gescheiterte Pruefung
/// zeigt den Fehler und fragt gar nicht erst nach Bestaetigung.
/// </summary>
public static class XtfAktualisierenUseCase
{
    public const string Titel = "Bestehende Katasterdaten aktualisieren";

    public static XtfExportErgebnis Execute(
        IXtfRevisionExportService dienst,
        XtfAktualisierenRequest request,
        XtfExportActions actions)
    {
        ArgumentNullException.ThrowIfNull(dienst);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actions);

        IReadOnlyList<string>? quellen = null;
        var pruefung = dienst.Erzeuge(new XtfRevisionExportRequest(
            request.Projekt, request.ProjektPfad, request.ZielOrdner, NurPruefen: true));

        if (pruefung.QuelleFehlt)
        {
            var gewaehlt = actions.WaehleQuelldateien();
            if (gewaehlt.Count == 0)
                return new XtfExportErgebnis(false, "Abgebrochen — keine Original-XTF gewählt.", null);

            quellen = gewaehlt;
            pruefung = dienst.Erzeuge(new XtfRevisionExportRequest(
                request.Projekt, request.ProjektPfad, request.ZielOrdner, NurPruefen: true, Quelldateien: quellen));
        }

        if (!pruefung.Ok)
        {
            actions.ZeigeFehler(XtfExportVorschau.Fehler(
                Titel,
                string.IsNullOrWhiteSpace(pruefung.Fehler) ? "Die Prüfung ist fehlgeschlagen." : pruefung.Fehler,
                pruefung.Bericht));
            return new XtfExportErgebnis(false, "Prüfung nicht bestanden — nichts geschrieben.", null);
        }

        var vorschau = XtfExportVorschau.AusRevision(pruefung.Plaene ?? [], pruefung.Bericht);
        if (!actions.BestaetigeVorschau(vorschau))
            return new XtfExportErgebnis(false, "Abgebrochen — nichts geschrieben.", null);

        var ergebnis = dienst.Erzeuge(new XtfRevisionExportRequest(
            request.Projekt, request.ProjektPfad, request.ZielOrdner, Quelldateien: quellen));

        if (!ergebnis.Ok)
        {
            actions.ZeigeFehler(XtfExportVorschau.Fehler(
                Titel,
                string.IsNullOrWhiteSpace(ergebnis.Fehler) ? "Das Schreiben ist fehlgeschlagen." : ergebnis.Fehler,
                ergebnis.Bericht));
            return new XtfExportErgebnis(false, "Nicht vollständig geschrieben.", null);
        }

        var ordner = ergebnis.Dateien.Count > 0 ? Path.GetDirectoryName(ergebnis.Dateien[0]) : null;
        var meldung = ergebnis.Dateien.Count switch
        {
            0 => "Keine Änderung gegenüber dem Kataster — nichts geschrieben.",
            1 => "Katasterdaten aktualisiert: 1 Datei geschrieben.",
            var n => $"Katasterdaten aktualisiert: {n} Dateien geschrieben."
        };
        return new XtfExportErgebnis(ergebnis.Dateien.Count > 0, meldung, ordner);
    }
}

/// <summary>
/// Der Ablauf hinter "Neue eigenstaendige XTF erstellen": Bericht als Vorschau zeigen, nach
/// Bestaetigung schreiben. Es gibt keine Alt-Werte, deshalb keine Tabelle.
/// </summary>
public static class XtfNeuErstellenUseCase
{
    public const string Titel = "Neue eigenständige XTF erstellen";

    public static XtfExportErgebnis Execute(
        IXtfNeuExportService dienst,
        XtfNeuExportRequest request,
        XtfExportActions actions)
    {
        ArgumentNullException.ThrowIfNull(dienst);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actions);

        var pruefung = dienst.Erzeuge(request with { NurPruefen = true });
        if (!pruefung.Ok)
        {
            actions.ZeigeFehler(XtfExportVorschau.Fehler(
                Titel,
                string.IsNullOrWhiteSpace(pruefung.Fehler) ? "Die Prüfung ist fehlgeschlagen." : pruefung.Fehler,
                pruefung.Bericht));
            return new XtfExportErgebnis(false, "Prüfung nicht bestanden — nichts geschrieben.", null);
        }

        if (!actions.BestaetigeVorschau(XtfExportVorschau.AusBericht(Titel, pruefung.Bericht)))
            return new XtfExportErgebnis(false, "Abgebrochen — nichts geschrieben.", null);

        var ergebnis = dienst.Erzeuge(request with { NurPruefen = false });
        if (!ergebnis.Ok || string.IsNullOrWhiteSpace(ergebnis.Datei))
        {
            actions.ZeigeFehler(XtfExportVorschau.Fehler(
                Titel,
                string.IsNullOrWhiteSpace(ergebnis.Fehler) ? "Das Schreiben ist fehlgeschlagen." : ergebnis.Fehler,
                ergebnis.Bericht));
            return new XtfExportErgebnis(false, "Neue XTF nicht erstellt.", null);
        }

        return new XtfExportErgebnis(
            true,
            $"Neue XTF erstellt: {Path.GetFileName(ergebnis.Datei)}",
            Path.GetDirectoryName(ergebnis.Datei));
    }
}
