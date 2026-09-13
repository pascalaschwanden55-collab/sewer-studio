namespace AuswertungPro.Next.Application.Xtf.Lieferung;

public sealed record XtfLieferungsGruppe(string Klasse, long Anzahl);
public sealed record XtfLieferungsInfo(string Datei, string Quelle, string Sha256, long Anzahl, long Geaendert,
    IReadOnlyList<XtfLieferungsGruppe> Gruppen);
public sealed record XtfLieferungsZeile(long Id, string Klasse, string Kennung, string Bezeichnung, bool Geaendert, string Problem);
public sealed record XtfLieferungsSeite(long Gesamt, IReadOnlyList<XtfLieferungsZeile> Zeilen);
public sealed record XtfLieferungsFeld(string Schluessel, string Label, string Wert, string Typ,
    bool Pflicht, bool Bearbeitbar, IReadOnlyList<string> Optionen, string Hinweis = "");
public sealed record XtfLieferungsObjekt(long Id, int Version, string Klasse, string Kennung, IReadOnlyList<XtfLieferungsFeld> Felder);
public sealed record XtfLieferungsPruefung(long Objekte, long FehlerhafteObjekte, long ExterneOrganisationen, string Bericht);

/// <summary>Eigene Arbeitsdatei einer vollständigen Lieferung, unabhängig von Projektzeilen und Kundendatei.</summary>
public interface IXtfLieferungsAblage
{
    XtfLieferungsInfo Importiere(string quelle, string arbeitsdatei, IProgress<string>? fortschritt = null, CancellationToken token = default);
    XtfLieferungsInfo Oeffne(string arbeitsdatei);
    XtfLieferungsSeite Suche(string arbeitsdatei, string? klasse, string suche, int seite = 0, bool nurProbleme = false);
    XtfLieferungsObjekt Lies(string arbeitsdatei, long id);
    void Speichere(string arbeitsdatei, long id, int version, IReadOnlyDictionary<string, string> aenderungen);
    XtfLieferungsPruefung Pruefe(string arbeitsdatei, IProgress<string>? fortschritt = null, CancellationToken token = default);
    XtfLieferungsPruefung Exportiere(string arbeitsdatei, string ziel, IProgress<string>? fortschritt = null, CancellationToken token = default);
    void SichereBericht(string arbeitsdatei, string ziel);
}
