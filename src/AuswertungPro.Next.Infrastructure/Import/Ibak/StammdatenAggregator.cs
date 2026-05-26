using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace AuswertungPro.Next.Infrastructure.Import.Ibak;

/// <summary>
/// Bringt Haltungs-Stammdaten aus mehreren Quellen zusammen und liefert eine
/// konsolidierte Map &lt;HaltungsKey&gt; -&gt; AggregatedStammdaten.
///
/// Prioritaet (bei Konflikt gewinnt die hoechere Quelle):
///   1. XTF (ISYBAU/SIA405 - strukturiertes offizielles Datenmodell)
///   2. PDF (KIAS-Stammdatenblock - gepflegt vom Inspekteur)
///   3. FDB (KIAS Arizona.fdb - Lt/Sc-Records mit Laenge/DN)
///   4. (AED/AEC aus Daten.txt = Wechsel-Marker, nur als letzter Fallback im IbakImporter)
///
/// Pro Feld wird vermerkt aus welcher Quelle der Wert stammt - der Importer kann
/// das nutzen um die Herkunft zu loggen.
/// </summary>
public static class StammdatenAggregator
{
    public enum SourceKind { Xtf, Pdf, Fdb, None }

    public sealed record AggregatedStammdaten(
        string Haltungsname,
        (string? Value, SourceKind Source) Material,
        (double? Value, SourceKind Source) Laenge_m,
        (int? Value, SourceKind Source) DN_mm,
        (int? Value, SourceKind Source) Profilbreite_mm,
        (string? Value, SourceKind Source) Geometrie,
        (string? Value, SourceKind Source) Nutzungsart,
        (string? Value, SourceKind Source) Strasse,
        (string? Value, SourceKind Source) Ort);

    public sealed record AggregateStats(
        int TotalHoldings,
        int FromXtf,
        int FromPdf,
        int FromFdb,
        int XtfFiles,
        int PdfFiles,
        bool FdbAvailable);

    /// <summary>
    /// Baut eine konsolidierte Stammdaten-Map. Liefert (Map, Stats).
    /// Schreibt informelle Status-Zeilen in messages.
    /// </summary>
    public static (IReadOnlyDictionary<string, AggregatedStammdaten> Stammdaten, AggregateStats Stats)
        Build(string exportRoot, List<string>? messages = null)
    {
        var combined = new Dictionary<string, AggregatedStammdaten>(StringComparer.OrdinalIgnoreCase);

        var xtfMap = XtfStammdatenExtractor.BuildIndex(exportRoot, messages);
        var pdfMap = IbakPdfStammdatenExtractor.BuildIndex(exportRoot);
        var fdbMap = KiasFdbTopologyReader.LoadStammdaten(exportRoot, messages);

        var fromXtf = 0;
        var fromPdf = 0;
        var fromFdb = 0;

        // Reihenfolge umgekehrt zur Prioritaet: erst FDB (niedrigste), dann PDF, dann XTF.
        foreach (var (key, fdb) in fdbMap)
        {
            combined[NormalizeKey(key)] = new AggregatedStammdaten(
                Haltungsname: fdb.ObjName,
                Material:        (null, SourceKind.None),
                Laenge_m:        (fdb.Laenge_m, fdb.Laenge_m.HasValue ? SourceKind.Fdb : SourceKind.None),
                DN_mm:           (fdb.ProfileHeight_mm, fdb.ProfileHeight_mm.HasValue ? SourceKind.Fdb : SourceKind.None),
                Profilbreite_mm: (fdb.ProfileWidth_mm, fdb.ProfileWidth_mm.HasValue ? SourceKind.Fdb : SourceKind.None),
                Geometrie:       (null, SourceKind.None),
                Nutzungsart:     (null, SourceKind.None),
                Strasse:         (fdb.Strasse, fdb.Strasse is not null ? SourceKind.Fdb : SourceKind.None),
                Ort:             (fdb.Ort,     fdb.Ort     is not null ? SourceKind.Fdb : SourceKind.None));
            if (fdb.Laenge_m.HasValue || fdb.ProfileHeight_mm.HasValue) fromFdb++;
        }

        foreach (var (key, pdf) in pdfMap)
        {
            var k = NormalizeKey(key);
            combined.TryGetValue(k, out var cur);
            cur ??= EmptyFor(pdf.Haltungsname ?? key);

            var changed = false;
            cur = Override(cur, "Material", pdf.Material, SourceKind.Pdf, ref changed);
            cur = Override(cur, "Laenge_m", pdf.Laenge_m, SourceKind.Pdf, ref changed);
            cur = Override(cur, "DN_mm", pdf.DN_mm, SourceKind.Pdf, ref changed);
            cur = Override(cur, "Profilbreite_mm", pdf.Profilbreite_mm, SourceKind.Pdf, ref changed);
            cur = Override(cur, "Geometrie", pdf.Geometrie, SourceKind.Pdf, ref changed);
            cur = Override(cur, "Nutzungsart", pdf.Nutzungsart, SourceKind.Pdf, ref changed);
            combined[k] = cur;
            if (changed) fromPdf++;
        }

        foreach (var (key, xtf) in xtfMap)
        {
            var k = NormalizeKey(key);
            combined.TryGetValue(k, out var cur);
            cur ??= EmptyFor(xtf.Haltungsname);

            var changed = false;
            cur = Override(cur, "Material", xtf.Material, SourceKind.Xtf, ref changed);
            cur = Override(cur, "Laenge_m", xtf.Laenge_m, SourceKind.Xtf, ref changed);
            cur = Override(cur, "DN_mm", xtf.DN_mm, SourceKind.Xtf, ref changed);
            cur = Override(cur, "Profilbreite_mm", xtf.Profilbreite_mm, SourceKind.Xtf, ref changed);
            cur = Override(cur, "Geometrie", xtf.Geometrie, SourceKind.Xtf, ref changed);
            cur = Override(cur, "Nutzungsart", xtf.Nutzungsart, SourceKind.Xtf, ref changed);
            combined[k] = cur;
            if (changed) fromXtf++;
        }

        var stats = new AggregateStats(
            TotalHoldings: combined.Count,
            FromXtf: fromXtf,
            FromPdf: fromPdf,
            FromFdb: fromFdb,
            XtfFiles: xtfMap.Count,
            PdfFiles: pdfMap.Count,
            FdbAvailable: fdbMap.Count > 0);

        messages?.Add($"Stammdaten-Aggregat: {combined.Count} Haltungen | XTF: {fromXtf}, PDF: {fromPdf}, FDB: {fromFdb}");
        return (combined, stats);
    }

    private static string NormalizeKey(string raw)
        => (raw ?? string.Empty).Replace(" ", "").Replace("/", "-").Replace("–", "-").Replace("—", "-");

    private static AggregatedStammdaten EmptyFor(string name) => new(
        Haltungsname: name,
        Material:        (null, SourceKind.None),
        Laenge_m:        (null, SourceKind.None),
        DN_mm:           (null, SourceKind.None),
        Profilbreite_mm: (null, SourceKind.None),
        Geometrie:       (null, SourceKind.None),
        Nutzungsart:     (null, SourceKind.None),
        Strasse:         (null, SourceKind.None),
        Ort:             (null, SourceKind.None));

    // Prioritaets-Override: hoehere Quelle gewinnt nur wenn aktueller Wert leer
    // ODER aus niedrigerer Quelle stammt.
    private static AggregatedStammdaten Override(
        AggregatedStammdaten cur, string field, string? value, SourceKind source, ref bool changed)
    {
        if (string.IsNullOrWhiteSpace(value)) return cur;
        return field switch
        {
            "Material"    => Replace(cur, cur.Material,    value, source, (c, v) => c with { Material = v },    ref changed),
            "Geometrie"   => Replace(cur, cur.Geometrie,   value, source, (c, v) => c with { Geometrie = v },   ref changed),
            "Nutzungsart" => Replace(cur, cur.Nutzungsart, value, source, (c, v) => c with { Nutzungsart = v }, ref changed),
            _ => cur
        };
    }

    private static AggregatedStammdaten Override(
        AggregatedStammdaten cur, string field, double? value, SourceKind source, ref bool changed)
    {
        if (!value.HasValue) return cur;
        return field switch
        {
            "Laenge_m" => Replace(cur, cur.Laenge_m, value, source, (c, v) => c with { Laenge_m = v }, ref changed),
            _ => cur
        };
    }

    private static AggregatedStammdaten Override(
        AggregatedStammdaten cur, string field, int? value, SourceKind source, ref bool changed)
    {
        if (!value.HasValue) return cur;
        return field switch
        {
            "DN_mm"           => Replace(cur, cur.DN_mm,           value, source, (c, v) => c with { DN_mm = v },           ref changed),
            "Profilbreite_mm" => Replace(cur, cur.Profilbreite_mm, value, source, (c, v) => c with { Profilbreite_mm = v }, ref changed),
            _ => cur
        };
    }

    private static AggregatedStammdaten Replace<T>(
        AggregatedStammdaten cur, (T? Value, SourceKind Source) currentField,
        T newVal, SourceKind newSource, Func<AggregatedStammdaten, (T?, SourceKind), AggregatedStammdaten> setter,
        ref bool changed)
    {
        // Nur ersetzen wenn aktuell leer ODER neue Quelle hat hoehere Prioritaet (kleinere Enum-Zahl).
        if (currentField.Source == SourceKind.None || (int)newSource < (int)currentField.Source)
        {
            changed = true;
            return setter(cur, (newVal, newSource));
        }
        return cur;
    }
}
