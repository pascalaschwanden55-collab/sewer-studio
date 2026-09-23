using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.WebGis;

/// <summary>
/// Bringt einen WebGIS-Klartext auf den Wert, den SewerStudio im Feld fuehrt (Holen, 23.09.2026).
/// Das WebGIS zeigt «Sammelkanal», «In Betrieb», «Kreisprofil (K)»; SewerStudio fuehrt
/// «PAA.Sammelkanal», «in_Betrieb», «Kreisprofil». Ein Wert kommt nur hinein, wenn er genau einem
/// SewerStudio-Wert entspricht — sonst null plus Hinweis, nie geraten. «unbekannt» fuellt nichts.
/// Reine Regel.
/// </summary>
public static class WebGisImportWert
{
    private static readonly System.Text.RegularExpressions.Regex KuerzelAmEnde =
        new(@"\s*\([A-Za-z]{1,4}\)\s*$", System.Text.RegularExpressions.RegexOptions.Compiled);

    private static readonly HashSet<string> Ganzzahlfelder = new(StringComparer.Ordinal)
    {
        FieldKeys.NominalDiameterMm, "Lichte_Breite_mm", "Lichte_Hoehe_mm", "Dimension 1 mm", "Dimension 2 mm",
    };

    private static readonly HashSet<string> Dezimalfelder = new(StringComparer.Ordinal)
    {
        "Tiefe", "Sohlenhoehe", "Gelaendehoehe", "Rotation",
    };

    /// <summary>
    /// Materialdetails in WebGIS-Schreibweise, die SewerStudio unter anderem Namen fuehrt
    /// (Zone 1.15, 23.09.2026). Nur belegte Paare — kein Raten.
    /// </summary>
    private static readonly Dictionary<(WebGisObjektart, string), string> WebGisBegriffe = new()
    {
        [(WebGisObjektart.Schacht, "beton, fertigteil")] = "Fertigbetonelement",
    };

    /// <param name="typAa">PAA/SAA der Haltung (Feld Typ AA): entscheidet ein Blatt wie
    /// «Liegenschaftsentwaesserung», das es unter beiden gibt.</param>
    public static string? Zuordne(WebGisObjektart art, string feld, string? webgisText, out string? hinweis, string? typAa = null)
    {
        hinweis = null;
        var text = (webgisText ?? string.Empty).Trim();
        if (text.Length == 0 || WebGisHandwertKarte.Falte(text) == "unbekannt") return null;

        if (Ganzzahlfelder.Contains(feld) || Dezimalfelder.Contains(feld))
        {
            if (double.TryParse(text.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var zahl))
                return Ganzzahlfelder.Contains(feld)
                    ? Math.Round(zahl).ToString("0", CultureInfo.InvariantCulture)
                    : zahl.ToString("0.###", CultureInfo.InvariantCulture);
            hinweis = $"{feld}: «{text}» ist keine Zahl — nicht übernommen.";
            return null;
        }

        var optionen = Optionen(art, feld);
        if (optionen.Count == 0) return text; // Freitextfeld (z.B. Ebene)

        // «Beton, unbekannt (BU)»: Kuerzel weg, «Gruppe, Detail» wie die Normschreibweise «Beton_unbekannt».
        var ohneKuerzel = KuerzelAmEnde.Replace(text, "").Trim();
        var alsNorm = ohneKuerzel.Replace(", ", "_");
        if (WebGisBegriffe.TryGetValue((art, ohneKuerzel.ToLowerInvariant()), out var begriff)) return begriff;
        foreach (var kandidat in new[] { text, Normalisiere(art, feld, text), ohneKuerzel,
                     Normalisiere(art, feld, ohneKuerzel), Normalisiere(art, feld, alsNorm) })
        {
            if (string.IsNullOrWhiteSpace(kandidat)) continue;
            var gefaltet = WebGisHandwertKarte.Falte(kandidat);
            var direkt = optionen.FirstOrDefault(o => WebGisHandwertKarte.Falte(o) == gefaltet);
            if (direkt is not null) return direkt;
        }

        // Zweistufige Werte («PAA.Sammelkanal»): das WebGIS zeigt nur das Blatt. Nur ein eindeutiges Blatt zaehlt.
        var gefaltetText = WebGisHandwertKarte.Falte(text);
        var blaetter = optionen.Where(o => o.Contains('.') && WebGisHandwertKarte.Falte(o[(o.LastIndexOf('.') + 1)..]) == gefaltetText).ToList();
        if (blaetter.Count == 1) return blaetter[0];
        if (blaetter.Count > 1 && !string.IsNullOrWhiteSpace(typAa))
        {
            var passend = blaetter.Where(o => o.StartsWith(typAa.Trim() + ".", StringComparison.OrdinalIgnoreCase)).ToList();
            if (passend.Count == 1) return passend[0];
        }

        hinweis = blaetter.Count > 1
            ? $"{feld}: «{text}» passt zu mehreren SewerStudio-Werten ({string.Join(", ", blaetter)}) — nicht übernommen."
            : $"{feld}: «{text}» passt zu keinem SewerStudio-Wert — nicht übernommen.";
        return null;
    }

    private static IReadOnlyList<string> Optionen(WebGisObjektart art, string feld)
    {
        IReadOnlyList<string> liste = art == WebGisObjektart.Schacht
            ? feld switch
            {
                "Funktion" => SchachtFunktionVokabular.Auswahl,
                "Material" => SchachtMaterialVokabular.Auswahl,
                "Schachtform" => SchachtformVokabular.Auswahl,
                _ => FieldCatalog.GetComboItems(feld),
            }
            : FieldCatalog.GetComboItems(feld);
        return liste.Where(o => !string.IsNullOrWhiteSpace(o)).ToList();
    }

    private static string Normalisiere(WebGisObjektart art, string feld, string text)
    {
        if (art == WebGisObjektart.Schacht)
            return feld switch
            {
                "Funktion" => SchachtFunktionVokabular.Normalisieren(text),
                "Material" => SchachtMaterialVokabular.Normalisieren(text),
                "Schachtform" => SchachtformVokabular.Normalisieren(text),
                "Nutzungsart" => NutzungsartVokabular.Normalisieren(text),
                _ => text,
            };
        return feld switch
        {
            FieldKeys.PipeMaterial => MaterialVokabular.Normalisieren(text),
            FieldKeys.UsageType => NutzungsartVokabular.Normalisieren(text),
            FieldKeys.ProfileType => ProfiltypVokabular.Normalisieren(text),
            _ => text,
        };
    }
}
