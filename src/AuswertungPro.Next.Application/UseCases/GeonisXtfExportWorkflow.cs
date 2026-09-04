using System.Globalization;
using AuswertungPro.Next.Application.Export.Geonis;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.UseCases;

/// <summary>Auftrag fuer einen GEONIS-Rueckschrieb.</summary>
/// <param name="Projekt">Geprueftes Projekt mit den beurteilten Haltungen und Schaechten.</param>
/// <param name="KatasterXtfPfad">SIA405-Katasterexport, aus dem Identitaet und Ist-Werte stammen.</param>
/// <param name="ZielOrdner">Ordner fuer Transferdatei und Protokoll.</param>
/// <param name="AenderungsDatum">Wert fuer Letzte_Aenderung.</param>
/// <param name="NurTrockenlauf">Nur Protokoll schreiben, keine Transferdatei erzeugen.</param>
public sealed record GeonisXtfExportRequest(
    Project Projekt,
    string KatasterXtfPfad,
    string ZielOrdner,
    DateOnly AenderungsDatum,
    bool NurTrockenlauf);

/// <summary>Ergebnis eines Rueckschrieb-Laufs.</summary>
public sealed record GeonisXtfExportResult(
    bool Erfolgreich,
    string Meldung,
    string? XtfPfad,
    string? ProtokollPfad,
    int ObjekteInDatei,
    int GeaenderteObjekte,
    int Hinweise);

/// <summary>
/// Fuehrt den Rueckschrieb in fester Reihenfolge aus: Kataster lesen, Plan bilden, Protokoll
/// schreiben, erst danach die Transferdatei.
///
/// Das Protokoll entsteht immer — auch beim Trockenlauf und auch dann, wenn es nichts zu
/// schreiben gibt. Ohne nachvollziehbare Liste darf in einer Datenbank ohne Rueckgaengig
/// nichts landen.
/// </summary>
public sealed class GeonisXtfExportWorkflow
{
    private readonly ISia405KatasterIndexReader _katasterLeser;
    private readonly ISia405ExportPlanBuilder _planBuilder;
    private readonly ISia405ObjektQuelltextLeser _quelltextLeser;
    private readonly ISia405XtfWriter _xtfWriter;
    private readonly ISia405ExportProtokollWriter _protokollWriter;

    public GeonisXtfExportWorkflow(
        ISia405KatasterIndexReader katasterLeser,
        ISia405ExportPlanBuilder planBuilder,
        ISia405ObjektQuelltextLeser quelltextLeser,
        ISia405XtfWriter xtfWriter,
        ISia405ExportProtokollWriter protokollWriter)
    {
        _katasterLeser = katasterLeser ?? throw new ArgumentNullException(nameof(katasterLeser));
        _planBuilder = planBuilder ?? throw new ArgumentNullException(nameof(planBuilder));
        _quelltextLeser = quelltextLeser ?? throw new ArgumentNullException(nameof(quelltextLeser));
        _xtfWriter = xtfWriter ?? throw new ArgumentNullException(nameof(xtfWriter));
        _protokollWriter = protokollWriter ?? throw new ArgumentNullException(nameof(protokollWriter));
    }

    public GeonisXtfExportResult Fuehre(GeonisXtfExportRequest anfrage)
    {
        ArgumentNullException.ThrowIfNull(anfrage);
        ArgumentNullException.ThrowIfNull(anfrage.Projekt);

        if (string.IsNullOrWhiteSpace(anfrage.KatasterXtfPfad))
            return new GeonisXtfExportResult(false, "Es ist kein Kataster-XTF angegeben.", null, null, 0, 0, 0);
        if (string.IsNullOrWhiteSpace(anfrage.ZielOrdner))
            return new GeonisXtfExportResult(false, "Es ist kein Zielordner angegeben.", null, null, 0, 0, 0);

        var kataster = _katasterLeser.Lies(anfrage.KatasterXtfPfad);
        var plan = _planBuilder.Erstelle(
            anfrage.Projekt,
            kataster,
            new Sia405ExportOptionen(anfrage.AenderungsDatum, anfrage.KatasterXtfPfad));

        var basisname = Basisname(anfrage.Projekt.Name, anfrage.AenderungsDatum);
        var protokollPfad = Path.Combine(anfrage.ZielOrdner, basisname + "_protokoll.txt");
        _protokollWriter.Schreibe(plan, protokollPfad);

        if (plan.Objekte.Count == 0)
        {
            return new GeonisXtfExportResult(
                true,
                "Es gibt nichts zurueckzuschreiben. Die Gruende stehen im Protokoll.",
                null,
                protokollPfad,
                0,
                0,
                plan.Hinweise.Count);
        }

        if (anfrage.NurTrockenlauf)
        {
            return new GeonisXtfExportResult(
                true,
                "Trockenlauf: nur das Protokoll wurde geschrieben, keine Transferdatei.",
                null,
                protokollPfad,
                plan.Objekte.Count,
                plan.GeaenderteObjekte,
                plan.Hinweise.Count);
        }

        var tids = plan.Objekte.Select(o => o.Tid).Distinct(StringComparer.Ordinal).ToList();
        var quelltexte = _quelltextLeser.Lies(anfrage.KatasterXtfPfad, tids);
        var xtfPfad = Path.Combine(anfrage.ZielOrdner, basisname + ".xtf");
        _xtfWriter.Schreibe(plan, quelltexte, xtfPfad);

        return new GeonisXtfExportResult(
            true,
            "Transferdatei und Protokoll wurden geschrieben.",
            xtfPfad,
            protokollPfad,
            plan.Objekte.Count,
            plan.GeaenderteObjekte,
            plan.Hinweise.Count);
    }

    private static string Basisname(string? projektname, DateOnly datum)
    {
        var name = (projektname ?? string.Empty).Trim();
        foreach (var zeichen in Path.GetInvalidFileNameChars())
            name = name.Replace(zeichen, '_');
        name = name.Replace(' ', '_');

        if (name.Length == 0)
            name = "Projekt";
        if (name.Length > 40)
            name = name[..40];

        return $"{name}_geonis_{datum.ToString("yyyyMMdd", CultureInfo.InvariantCulture)}";
    }
}
