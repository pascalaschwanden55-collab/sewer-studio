using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using AuswertungPro.Next.Infrastructure.Common;
using AuswertungPro.Next.Infrastructure.Import.Pdf;

namespace AuswertungPro.Next.Infrastructure.Import.Ibak;

/// <summary>
/// Extrahiert Haltungs-Stammdaten (DN, Material, Laenge, Geometrie, Nutzungsart)
/// aus den KIAS-Inspektions-PDFs (Report/H_*.pdf, L_*.pdf).
///
/// Hintergrund: Die KIAS-FDB (Arizona.fdb) speichert OBJ_LENGTH/PROFILE_HEIGHT/
/// PROFILE_WIDTH durchgehend NULL und IBAK-Daten.txt enthaelt nur Materialwechsel-
/// Marker (AED) - die einzige verlaessliche Quelle fuer Haltungs-Stammdaten ist
/// damit der Stammdatenblock auf Seite 1/2 jedes PDF-Berichts.
///
/// Erkannte Felder:
///   - Haltungslaenge_m  (z.B. "Haltungslänge   3.20 m")
///   - Material          (z.B. "Material   Polyvinylchlorid")
///   - DN_mm             (z.B. "Profilhöhe   120 mm" oder "DN 200")
///   - Geometrie         (z.B. "Geometrie   Kreisprofil")
///   - Nutzungsart       (z.B. "Nutzungsart   Mischabwasser")
///   - Profilbreite_mm
/// </summary>
public static class IbakPdfStammdatenExtractor
{
    public sealed record StammdatenResult(
        string? Haltungsname,
        string? Material,
        double? Laenge_m,
        int? DN_mm,
        int? Profilbreite_mm,
        string? Geometrie,
        string? Nutzungsart,
        string? Bemerkung);

    private static readonly Regex HaltungslaengeRx = new(
        @"Haltungsl[äa]nge\s+([\d.,]+)\s*m\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Material wird bis 2 oder mehr Whitespace-Zeichen oder Zeilenende gelesen.
    private static readonly Regex MaterialRx = new(
        @"Material\s+(?<v>\S(?:[^\n]{0,40}?\S)?)(?=\s{2,}|\n|$)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Profilhoehe: typisch "Profilhöhe   120 mm" - das ist DN bei Kreisprofil.
    private static readonly Regex ProfilHoeheRx = new(
        @"Profilh[öo]he\s+(\d{2,4})\s*mm",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex ProfilBreiteRx = new(
        @"Profilbreite\s+(\d{2,4})\s*mm",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Fallback: explizites "DN 200" / "DN200" / "DN: 200"
    private static readonly Regex DnRx = new(
        @"\bDN[\s:]*(\d{2,4})\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Inline-Profilbezeichnung wie in den Inspektionsprotokollen unter
    // D:/Haltungen, E:/Haltungen: "Kreisprofil 700mm", "Kreisprofil 240 mm",
    // "Eiprofil 600mm" - die Zahl direkt hinter dem Profilnamen ist die
    // lichte Hoehe (= DN bei Kreisprofil).
    private static readonly Regex InlineProfilDimensionRx = new(
        @"\b(?:Kreisprofil|Eiprofil|Maulprofil|Drachenprofil|Rechteckprofil|Trapezprofil|Sonderprofil)\s+(\d{2,4})\s*mm\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // "Lichte Hoehe 200 mm" / "Innendurchmesser 250mm" - alternative Bezeichner
    // die manche PDF-Vorlagen statt 'Profilhoehe' verwenden.
    private static readonly Regex LichteHoeheRx = new(
        @"(?:Lichte\s*H[öo]he|Innendurchmesser|Innen\s*[ØO])\s*[:=]?\s*(\d{2,4})\s*mm",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex GeometrieRx = new(
        @"Geometrie\s+(?<v>[A-Za-zÄÖÜäöüß][\w\-äöüÄÖÜß]*)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex NutzungsartRx = new(
        @"Nutzungsart\s+(?<v>[A-Za-zÄÖÜäöüß][\w\-äöüÄÖÜß]*)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex HaltungsnameRx = new(
        @"Haltung\s+((?:\d+\.\d+|\d+)\s*[-/]\s*(?:\d+\.\d+|\d+))",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    /// Extrahiert Stammdaten aus einer einzelnen Inspektions-PDF. Liest die ersten
    /// 2 Seiten (Stammdatenblock liegt typisch auf Seite 1-2). Liefert null falls
    /// keine Felder gefunden werden konnten.
    /// </summary>
    public static StammdatenResult? Extract(string pdfPath)
    {
        if (string.IsNullOrWhiteSpace(pdfPath) || !File.Exists(pdfPath))
            return null;

        string text;
        try
        {
            var extraction = PdfTextExtractor.ExtractPages(pdfPath);
            // Nur erste 2 Seiten - Stammdatenblock liegt vorne.
            text = string.Join("\n", extraction.Pages.Take(2));
        }
        catch
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(text))
            return null;

        return ExtractFromText(text);
    }

    /// <summary>Variante zum Testen mit bereits extrahiertem Text.</summary>
    public static StammdatenResult? ExtractFromText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        var haltung = MatchValue(HaltungsnameRx, text);

        double? laenge = null;
        var lm = HaltungslaengeRx.Match(text);
        if (lm.Success)
        {
            var raw = lm.Groups[1].Value.Replace(',', '.');
            if (double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) && d > 0)
                laenge = d;
        }

        var material = MatchValue(MaterialRx, text);

        // DN-Lookup mit Pattern-Kaskade (von spezifisch nach generisch).
        // Reihenfolge wichtig: erst die KIAS-eigenen "Profilhoehe ..." Felder,
        // dann die alternativen Bezeichner ("Lichte Hoehe", "Innendurchmesser"),
        // dann die Inline-Profilbezeichnung ("Kreisprofil 700mm" etc., wie sie
        // in den Inspektionsprotokollen unter D:/Haltungen / E:/Haltungen
        // vorkommt), zuletzt das nackte "DN 200" als Fallback.
        int? dnMm = null;
        foreach (var rx in new[] { ProfilHoeheRx, LichteHoeheRx, InlineProfilDimensionRx, DnRx })
        {
            var m = rx.Match(text);
            if (m.Success && int.TryParse(m.Groups[1].Value, out var v) && v > 0)
            {
                dnMm = v;
                break;
            }
        }

        int? profilbreite = null;
        var pb = ProfilBreiteRx.Match(text);
        if (pb.Success && int.TryParse(pb.Groups[1].Value, out var pbv))
            profilbreite = pbv;

        var geometrie = MatchValue(GeometrieRx, text);
        var nutzung = MatchValue(NutzungsartRx, text);

        // Wenn nichts erkannt wurde - kein Ergebnis.
        if (haltung is null && material is null && laenge is null && dnMm is null
            && profilbreite is null && geometrie is null && nutzung is null)
            return null;

        return new StammdatenResult(
            Haltungsname: haltung,
            Material: material,
            Laenge_m: laenge,
            DN_mm: dnMm,
            Profilbreite_mm: profilbreite,
            Geometrie: geometrie,
            Nutzungsart: nutzung,
            Bemerkung: null);
    }

    /// <summary>
    /// Index alle PDFs im Report-Ordner unter dem Export-Root. Liefert eine Map
    /// HaltungsKey -> Stammdaten. Mehrere PDFs zu einer Haltung werden gemerged
    /// (Wert aus erster PDF gewinnt, fehlende Felder werden aus weiteren ergaenzt).
    /// </summary>
    public static Dictionary<string, StammdatenResult> BuildIndex(string exportRoot)
    {
        var result = new Dictionary<string, StammdatenResult>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(exportRoot) || !Directory.Exists(exportRoot))
            return result;

        var pdfFiles = SafeEnumeratePdfs(exportRoot);
        foreach (var pdf in pdfFiles)
        {
            var data = Extract(pdf);
            if (data is null) continue;

            // Haltungsname aus PDF oder aus Dateiname ableiten (H_36262-36275.pdf)
            var key = NormalizeKey(data.Haltungsname) ?? NormalizeKey(HaltungFromFilename(pdf));
            if (string.IsNullOrWhiteSpace(key))
                continue;

            if (result.TryGetValue(key, out var existing))
                result[key] = Merge(existing, data);
            else
                result[key] = data;
        }
        return result;
    }

    private static IEnumerable<string> SafeEnumeratePdfs(string root)
    {
        try
        {
            // Bevorzuge Report-Unterordner, falle sonst auf alles im Root zurueck.
            var report = Path.Combine(root, "Report");
            // Audit 2026-05-17 (Nachzieh): SafeFileEnumeration ueberspringt gesperrte Unterordner.
            if (Directory.Exists(report))
                return SafeFileEnumeration.EnumerateFilesSafe(report, "*.pdf", recursive: true);
            return SafeFileEnumeration.EnumerateFilesSafe(root, "*.pdf", recursive: true);
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    private static string? HaltungFromFilename(string pdfPath)
        => KiasExportPattern.HoldingFromKiasFilename(pdfPath)
           ?? Path.GetFileNameWithoutExtension(pdfPath);

    private static string? NormalizeKey(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var v = value.Trim()
            .Replace(" ", "")
            .Replace("/", "-")
            .Replace("–", "-")
            .Replace("—", "-");
        return string.IsNullOrWhiteSpace(v) ? null : v;
    }

    private static StammdatenResult Merge(StammdatenResult a, StammdatenResult b) => new(
        Haltungsname:    a.Haltungsname    ?? b.Haltungsname,
        Material:        a.Material        ?? b.Material,
        Laenge_m:        a.Laenge_m        ?? b.Laenge_m,
        DN_mm:           a.DN_mm           ?? b.DN_mm,
        Profilbreite_mm: a.Profilbreite_mm ?? b.Profilbreite_mm,
        Geometrie:       a.Geometrie       ?? b.Geometrie,
        Nutzungsart:     a.Nutzungsart     ?? b.Nutzungsart,
        Bemerkung:       a.Bemerkung       ?? b.Bemerkung);

    private static string? MatchValue(Regex rx, string text)
    {
        var m = rx.Match(text);
        if (!m.Success) return null;
        var v = (m.Groups["v"].Success ? m.Groups["v"].Value : m.Groups[1].Value).Trim();
        // Filtere Header-Mitschnitte / leere Folgespalten (z.B. "Material   Datenlieferant").
        if (string.IsNullOrWhiteSpace(v)) return null;
        if (v.Length > 60) return null;
        return v;
    }
}
