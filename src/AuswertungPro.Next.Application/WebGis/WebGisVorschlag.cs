using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace AuswertungPro.Next.Application.WebGis;

/// <summary>
/// «Kanalfirma weicht vom WebGIS ab» (Entscheid Pascal 23.09.2026 abends): Ein Wert der Kanalfirma
/// (Ist-Zustand, nicht von Hand gesetzt) weicht vom WebGIS ab. Er wird NIE automatisch geschrieben —
/// nur wenn der Bearbeiter ihn in der Vorschau anhakt (<see cref="Gewaehlt"/>).
/// </summary>
public sealed class WebGisVorschlag
{
    /// <summary>SewerStudio-Feld, aus dem der Wert stammt.</summary>
    public required string Feld { get; init; }
    /// <summary>Name des WebGIS-Felds.</summary>
    public required string Anzeige { get; init; }
    /// <summary>Was jetzt im WebGIS steht (Klartext).</summary>
    public string? AltText { get; init; }
    /// <summary>Wert der Kanalfirma.</summary>
    public required string NeuText { get; init; }
    /// <summary>Was bei Wahl geschrieben wird (bei Materialdetail samt Hauptkategorie).</summary>
    public List<WebGisFeldAenderung> Aenderungen { get; init; } = new();
    /// <summary>Vom Bearbeiter angehakt. Standard: nein.</summary>
    public bool Gewaehlt { get; set; }
}

/// <summary>
/// Die Haken der Vorschau: merken, auf einen frisch gebauten Plan uebertragen und in den Plan uebernehmen.
/// «Jetzt schreiben» baut den Plan frisch; ohne diese Uebertragung gingen die Haken dort verloren.
/// Ein Haken gilt nur fuer genau diesen Wert an genau diesem Objekt — hat sich der Wert der Kanalfirma
/// seither geaendert, ist es ein anderer Vorschlag und er ist nicht angehakt.
/// </summary>
public static class WebGisVorschlagAuswahl
{
    public static IReadOnlySet<string> Gewaehlte(WebGisExportPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        var auswahl = new HashSet<string>(StringComparer.Ordinal);
        foreach (var p in plan.Positionen)
            foreach (var v in p.Vorschlaege)
                if (v.Gewaehlt) auswahl.Add(Schluessel(p, v));
        return auswahl;
    }

    public static void Waehle(WebGisExportPlan plan, IReadOnlySet<string> auswahl)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(auswahl);
        foreach (var p in plan.Positionen)
            foreach (var v in p.Vorschlaege)
                v.Gewaehlt = auswahl.Contains(Schluessel(p, v));
    }

    /// <summary>Die Haken der bestaetigten Vorschau auf den frisch gebauten Plan uebertragen (ohne Vorschau: keine).</summary>
    public static void UebertrageAuf(WebGisExportPlan? bestaetigt, WebGisExportPlan frisch)
        => Waehle(frisch, bestaetigt is null ? new HashSet<string>(StringComparer.Ordinal) : Gewaehlte(bestaetigt));

    /// <summary>
    /// Was aus einer Position geschrieben wird: ihre Aenderungen, dazu die angehakten Vorschlaege (je Feld
    /// hoechstens einmal). Gleich, ob die Haken schon uebernommen sind oder nicht.
    /// </summary>
    public static IReadOnlyList<WebGisFeldAenderung> Wirksam(WebGisExportPosition p)
    {
        ArgumentNullException.ThrowIfNull(p);
        var liste = new List<WebGisFeldAenderung>(p.Aenderungen);
        foreach (var v in p.Vorschlaege)
            if (v.Gewaehlt)
                foreach (var a in v.Aenderungen)
                    if (!liste.Exists(x => string.Equals(x.RefId, a.RefId, StringComparison.Ordinal))) liste.Add(a);
        return liste;
    }

    /// <summary>Angehakte Vorschlaege werden zu Aenderungen der Position. Wiederholbar: nie doppelt.</summary>
    public static void UebernimmGewaehlte(WebGisExportPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        foreach (var p in plan.Positionen)
            foreach (var a in Wirksam(p))
                if (!p.Aenderungen.Exists(x => string.Equals(x.RefId, a.RefId, StringComparison.Ordinal)))
                    p.Aenderungen.Add(a);
    }

    private static string Schluessel(WebGisExportPosition p, WebGisVorschlag v)
        => $"{p.Objektart}|{p.RecordId}|{p.GlobalId}|{v.Feld}|{v.NeuText}|{string.Join(",", v.Aenderungen.Select(a => a.RefId + "=" + a.Neu))}";
}

/// <summary>Eine Zeile «Kanalfirma weicht ab» in der Uebersicht; das Haekchen schreibt direkt in den Vorschlag.</summary>
public sealed class WebGisUebersichtVorschlag : INotifyPropertyChanged
{
    private readonly WebGisVorschlag _vorschlag;

    public WebGisUebersichtVorschlag(string objekt, WebGisObjektart objektart, Guid recordId, WebGisVorschlag vorschlag)
    {
        Objekt = objekt;
        Objektart = objektart;
        RecordId = recordId;
        _vorschlag = vorschlag ?? throw new ArgumentNullException(nameof(vorschlag));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Objekt { get; }
    public WebGisObjektart Objektart { get; }
    public Guid RecordId { get; }
    public string Feld => _vorschlag.Anzeige;
    public string Alt => string.IsNullOrWhiteSpace(_vorschlag.AltText) ? "(leer)" : _vorschlag.AltText!;
    public string Neu => _vorschlag.NeuText;

    public bool Gewaehlt
    {
        get => _vorschlag.Gewaehlt;
        set
        {
            if (_vorschlag.Gewaehlt == value) return;
            _vorschlag.Gewaehlt = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Gewaehlt)));
        }
    }
}
