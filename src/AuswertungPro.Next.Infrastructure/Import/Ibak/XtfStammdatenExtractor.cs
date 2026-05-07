using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace AuswertungPro.Next.Infrastructure.Import.Ibak;

/// <summary>
/// Extrahiert Haltungs-Stammdaten aus XTF-Dateien (ISYBAU XML / VSA-DSS Transferformat).
/// Nutzt die strukturierten Felder LaengeEffektiv, Lichte_Hoehe/Breite, Material - das ist
/// die zuverlaessigste Quelle (besser als PDF und FDB), da es ein offizielles Datenmodell ist.
///
/// Ergaenzt den IbakPdfStammdatenExtractor: liefert eine Map Haltungsname -> Stammdaten.
/// </summary>
public static class XtfStammdatenExtractor
{
    public sealed record StammdatenResult(
        string Haltungsname,
        string? Material,
        double? Laenge_m,
        int? DN_mm,
        int? Profilbreite_mm,
        string? Geometrie,
        string? Nutzungsart);

    /// <summary>
    /// Sucht alle *.xtf-Dateien im Export-Root und liefert eine Map
    /// HaltungsKey -> Stammdaten. Mehrere XTF-Dateien werden gemerged
    /// (erster Treffer gewinnt, fehlende Felder aus weiteren ergaenzt).
    /// </summary>
    public static Dictionary<string, StammdatenResult> BuildIndex(string exportRoot, List<string>? messages = null)
    {
        var result = new Dictionary<string, StammdatenResult>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(exportRoot) || !Directory.Exists(exportRoot))
            return result;

        var xtfFiles = SafeEnumerateXtf(exportRoot);
        if (xtfFiles.Count == 0)
            return result;

        foreach (var path in xtfFiles)
        {
            try
            {
                var per = ExtractFromFile(path);
                foreach (var (key, stamm) in per)
                {
                    if (result.TryGetValue(key, out var existing))
                        result[key] = Merge(existing, stamm);
                    else
                        result[key] = stamm;
                }
            }
            catch (Exception ex)
            {
                messages?.Add($"XTF-Stammdaten {Path.GetFileName(path)}: {ex.Message}");
            }
        }

        if (result.Count > 0)
            messages?.Add($"XTF-Stammdaten extrahiert: {result.Count} Haltungen aus {xtfFiles.Count} XTF-Dateien");

        return result;
    }

    /// <summary>Extrahiert Stammdaten aus einer einzelnen XTF-Datei.</summary>
    public static Dictionary<string, StammdatenResult> ExtractFromFile(string xtfPath)
    {
        var result = new Dictionary<string, StammdatenResult>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(xtfPath) || !File.Exists(xtfPath))
            return result;

        XDocument doc;
        try { doc = AuswertungPro.Next.Application.Common.SafeXmlLoader.Load(xtfPath); }
        catch { return result; }

        // Sammle Kanal-Daten (Nutzungsart) und Haltung-Daten (Geometrie/Material/Laenge).
        var kanalNutzung = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var kanal in doc.Descendants().Where(e => IsLocalName(e, "Kanal")))
        {
            var tid = (string?)kanal.Attribute("TID");
            if (string.IsNullOrWhiteSpace(tid)) continue;
            var nutz = kanal.Elements().FirstOrDefault(c => c.Name.LocalName == "Nutzungsart_Ist")?.Value;
            if (!string.IsNullOrWhiteSpace(nutz))
                kanalNutzung[tid!] = nutz!;
        }

        foreach (var haltung in doc.Descendants().Where(e => IsLocalName(e, "Haltung")))
        {
            string? bezeichnung = null;
            string? laenge = null;
            string? hoehe = null;
            string? breite = null;
            string? material = null;
            string? kanalRef = null;

            foreach (var child in haltung.Elements())
            {
                switch (child.Name.LocalName)
                {
                    case "Bezeichnung": bezeichnung = child.Value; break;
                    case "LaengeEffektiv": laenge = child.Value; break;
                    case "Lichte_Hoehe": hoehe = child.Value; break;
                    case "Lichte_Breite": breite = child.Value; break;
                    case "Material": material = child.Value; break;
                    case "AbwasserbauwerkRef": kanalRef = (string?)child.Attribute("REF"); break;
                }
            }

            if (string.IsNullOrWhiteSpace(bezeichnung))
                continue;

            var nutz = kanalRef is not null && kanalNutzung.TryGetValue(kanalRef, out var n) ? n : null;

            double? laengeNum = null;
            if (!string.IsNullOrWhiteSpace(laenge)
                && double.TryParse(laenge.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var ld))
                laengeNum = ld;

            int? dn = null;
            if (!string.IsNullOrWhiteSpace(hoehe)
                && double.TryParse(hoehe.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var hd))
                dn = (int)Math.Round(hd);

            int? brNum = null;
            if (!string.IsNullOrWhiteSpace(breite)
                && double.TryParse(breite.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var bd))
                brNum = (int)Math.Round(bd);

            var key = NormalizeKey(bezeichnung)!;
            var stamm = new StammdatenResult(
                Haltungsname: bezeichnung!.Trim(),
                Material: NormalizeSiaMaterial(material),
                Laenge_m: laengeNum,
                DN_mm: dn,
                Profilbreite_mm: brNum,
                Geometrie: null,
                Nutzungsart: nutz);

            if (result.TryGetValue(key, out var existing))
                result[key] = Merge(existing, stamm);
            else
                result[key] = stamm;
        }

        return result;
    }

    private static IReadOnlyList<string> SafeEnumerateXtf(string root)
    {
        try
        {
            return Directory.EnumerateFiles(root, "*.xtf", SearchOption.AllDirectories).ToList();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    private static bool IsLocalName(XElement e, string name)
        => e.Name.LocalName.Equals(name, StringComparison.OrdinalIgnoreCase)
        || e.Name.LocalName.EndsWith("." + name, StringComparison.OrdinalIgnoreCase);

    private static string? NormalizeKey(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var v = value.Trim().Replace(" ", "").Replace("/", "-").Replace("–", "-").Replace("—", "-");
        return string.IsNullOrWhiteSpace(v) ? null : v;
    }

    private static StammdatenResult Merge(StammdatenResult a, StammdatenResult b) => new(
        Haltungsname:    string.IsNullOrWhiteSpace(a.Haltungsname) ? b.Haltungsname : a.Haltungsname,
        Material:        a.Material        ?? b.Material,
        Laenge_m:        a.Laenge_m        ?? b.Laenge_m,
        DN_mm:           a.DN_mm           ?? b.DN_mm,
        Profilbreite_mm: a.Profilbreite_mm ?? b.Profilbreite_mm,
        Geometrie:       a.Geometrie       ?? b.Geometrie,
        Nutzungsart:     a.Nutzungsart     ?? b.Nutzungsart);

    /// <summary>
    /// Wandelt SIA405-Material-Codes ("R", "S", "PE", ...) in lesbare Namen um.
    /// Konsistent mit LegacyXtfImportService.NormalizeSiaMaterial.
    /// </summary>
    private static string? NormalizeSiaMaterial(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var v = raw.Trim();
        // SIA405-Codes (vereinfachte Ableitung)
        return v.ToUpperInvariant() switch
        {
            "R"   => "Beton",
            "RB"  => "Stahlbeton",
            "S"   => "Steingut",
            "PE"  => "Polyethylen",
            "PP"  => "Polypropylen",
            "PVC" => "Polyvinylchlorid",
            "GG"  => "Grauguss",
            "GFK" => "GFK",
            "AZ"  => "Asbestzement",
            "Z"   => "Zement",
            _     => v
        };
    }
}
