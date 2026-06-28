using System;
using System.Collections.Generic;
using System.Text;
using AuswertungPro.Next.Application.Protocol;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;

namespace AuswertungPro.Next.Infrastructure.Ai;

/// <summary>
/// Reine Prompt-Bau- und Mapping-Hilfsmethoden fuer den EnhancedVisionAnalysisService.
/// Alle Methoden sind zustandslos (pure static) — keine IO, kein Threading, kein Ollama.
/// </summary>
internal static class EnhancedVisionPromptBuilder
{
    /// <summary>
    /// Baut die Menge der im aktiven Katalog bekannten Codes (inkl. Hauptcodes).
    /// Gibt null zurueck, wenn kein/leerer Katalog vorliegt — dann wird nicht validiert.
    /// </summary>
    internal static IReadOnlySet<string>? BuildKnownCodeSet(ICodeCatalogProvider? catalog)
    {
        if (catalog is null) return null;

        var all = catalog.GetAll();
        if (all is null || all.Count == 0) return null;

        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var def in all)
        {
            if (def is not null && !string.IsNullOrWhiteSpace(def.Code))
                set.Add(def.Code.Trim());
        }

        // Leerer Katalog → keine Validierung (sonst wuerden alle Hints faelschlich verworfen).
        return set.Count > 0 ? set : null;
    }

    /// <summary>
    /// Validiert einen LLM-Code-Hint gegen den Katalog: unbekannte/erfundene Codes
    /// werden zu null verworfen, BEVOR sie in Dedup/Tracking/Anzeige landen.
    /// Ohne Katalog (knownCodes == null) wird der Hint unveraendert durchgereicht.
    /// </summary>
    internal static string? ValidateCodeHint(string? hint, IReadOnlySet<string>? knownCodes)
    {
        var code = hint?.Trim();
        if (string.IsNullOrWhiteSpace(code)) return null;
        if (knownCodes is null) return code;                  // ohne Katalog keine Validierung moeglich
        return knownCodes.Contains(code) ? code : null;       // erfundener/unbekannter Code → verwerfen
    }

    /// <summary>
    /// Normalisiert die vom LLM gelieferte BBox [x1,y1,x2,y2] (0-1): Ecken in
    /// Min/Max-Ordnung bringen, auf [0,1] clampen, degenerierte (Null-Flaeche)
    /// Boxen verwerfen. Liefert null-Werte wenn keine gueltige Box vorliegt.
    /// </summary>
    internal static (double? X1, double? Y1, double? X2, double? Y2) NormalizeBbox(
        IReadOnlyList<double>? bbox)
    {
        if (bbox is not { Count: >= 4 })
            return (null, null, null, null);

        // Ecken in Min/Max-Ordnung (Qwen vertauscht sie haeufig) + Clamp auf [0,1]
        var x1 = Math.Clamp(Math.Min(bbox[0], bbox[2]), 0, 1);
        var y1 = Math.Clamp(Math.Min(bbox[1], bbox[3]), 0, 1);
        var x2 = Math.Clamp(Math.Max(bbox[0], bbox[2]), 0, 1);
        var y2 = Math.Clamp(Math.Max(bbox[1], bbox[3]), 0, 1);

        // Degenerierte (Null-Flaeche) Box verwerfen
        if (x2 <= x1 || y2 <= y1)
            return (null, null, null, null);

        return (x1, y1, x2, y2);
    }

    /// <summary>
    /// Baut den vollstaendigen Analyse-Prompt. Optionaler Import-Kontext und
    /// unsichere Beobachtungshinweise werden als separate Abschnitte eingefuegt.
    /// </summary>
    internal static string BuildPrompt(
        ICodeCatalogProvider? codeCatalog,
        IReadOnlyList<(string Code, string Description, double Meter)>? importContext = null,
        IReadOnlyList<string>? observationHints = null)
    {
        var contextSection = BuildImportContextSection(importContext);
        var observationHintsSection = BuildObservationHintsSection(observationHints);

        return $"""
Du analysierst einen Frame aus einem Kanalinspektion-Video (TV-Inspektion Abwasserkanal).

AUFGABEN:
1. Lies den METERSTAND aus dem OSD (On-Screen Display) – typisch unten rechts im Bild als Dezimalzahl (z.B. "2.64", "18.40").
   IGNORIERE grosse Zahlen im oberen Header (Knotennummern wie 74468, 80872). Meterstand ist IMMER kleiner als 500.
2. Erkenne das ROHRMATERIAL und den DURCHMESSER falls sichtbar.
3. Erkenne ALLE sichtbaren Schäden/Anomalien mit Schweregrad 1-5 (1=kaum, 5=sehr schwer).
4. Gib für jeden Schaden die Uhrzeitlage an (z.B. "12:00" = Scheitel, "6:00" = Sohle).
5. Beurteile die Bildqualität.
6. Schätze, wenn erkennbar, Schadensmaße: Höhe (mm), Breite (mm), Einragungsgrad (%), Querschnittsverringerung (%), Durchmesserverringerung (mm).
7. Gib fuer jeden Schaden den passenden VSA-Code als vsa_code_hint an.
8. Wenn der exakte Untertyp eines ERKANNTEN Schadens unklar ist, verwende den passenden HAUPTCODE statt "???".
   Beispiele: Anschluss -> BCA, Bogen -> BCC, Ablagerung -> BBC.
   Strukturmerkmale (BCD/BCE/BCA/BCC) bei sichtbarem Merkmal vergeben, aber nicht aus blosser Unsicherheit raten.
{contextSection}
{observationHintsSection}
{BuildDamageClassesPrompt(codeCatalog)}

SCHWEREGRAD-SKALA (entspricht VSA Zustandsklasse):
1 = Optische Auffälligkeit, kein Handlungsbedarf
2 = Leichter Schaden, Beobachtung empfohlen
3 = Mittlerer Schaden, Sanierung mittelfristig
4 = Schwerer Schaden, Sanierung kurzfristig
5 = Kritischer Schaden, Sofortmassnahme
9. Gib fuer jeden Schaden eine bbox an: [x1, y1, x2, y2] normalisiert (0.0=links/oben, 1.0=rechts/unten).
   bbox = Region des Schadens IM BILD, nicht die Rohruhr-Position.

Antworte AUSSCHLIESSLICH mit gültigem JSON gemäß Schema.
Falls kein Schaden erkennbar: findings=[], is_empty_frame=true.
""";
    }

    /// <summary>
    /// Baut den VSA-KEK-Katalogauszug fuer den Prompt.
    /// Titel werden bei vorhandenem Katalog aus dem aktiven Manifest geladen,
    /// sonst werden Fallback-Bezeichnungen verwendet.
    /// </summary>
    internal static string BuildDamageClassesPrompt(ICodeCatalogProvider? codeCatalog)
    {
        var sb = new StringBuilder();
        sb.AppendLine("VSA-KEK-KATALOGAUSZUG (Code-Wahrheit aus aktivem Katalog):");
        sb.AppendLine();

        sb.AppendLine("GRUNDSTRUKTUR DER HALTUNG (bei klarem Merkmal vergeben - aber nicht aus Unsicherheit raten):");
        AppendCodeLine(sb, codeCatalog, "BCD", "Rohranfang", "wenn der Einstiegsschacht / die Schachtwand deutlich sichtbar ist und die Kamera ins Rohr einfaehrt");
        AppendCodeLine(sb, codeCatalog, "BCE", "Rohrende", "wenn der Zielschacht / die Schachtwand am Ende deutlich sichtbar ist");
        AppendCodeLine(sb, codeCatalog, "BCA", "Seitlicher Anschluss", "klar sichtbare seitliche Rohroeffnung in der Kanalwand");
        AppendCodeLine(sb, codeCatalog, "BCAEB", "Anschluss eingespitzt, verschlossen", null);
        AppendCodeLine(sb, codeCatalog, "BAHC", "Anschluss unvollstaendig eingebunden", "Stutzen ragt in den Kanal hinein");
        AppendCodeLine(sb, codeCatalog, "BCC", "Bogen", "sichtbare Richtungsaenderung des Kanals");
        sb.AppendLine();
        sb.AppendLine("ABGRENZUNG: Ein KLAR sichtbarer Einstiegs-/Zielschacht IST BCD bzw. BCE - den ruhig vergeben.");
        sb.AppendLine("ABER ein bloss dunkles Rohrinneres / der Fluchtpunkt in der Tiefe OHNE sichtbaren Schacht");
        sb.AppendLine("ist KEIN BCD. Nur im echten Zweifel ohne erkennbares Merkmal keinen Strukturcode raten.");
        sb.AppendLine();

        sb.AppendLine("STRUKTURELLE SCHAEDEN:");
        AppendCodeLine(sb, codeCatalog, "BAA", "Verformung", "vertikal oder horizontal");
        AppendCodeLine(sb, codeCatalog, "BAB", "Riss", "laengs/quer/diagonal/ringfoermig/verzweigt");
        AppendCodeLine(sb, codeCatalog, "BAC", "Bruch", "partiell oder total");
        AppendCodeLine(sb, codeCatalog, "BAH", "Schadhafter Anschluss", null);
        AppendCodeLine(sb, codeCatalog, "BAI", "Einragendes Dichtungsmaterial", null);
        AppendCodeLine(sb, codeCatalog, "BAJ", "Verschobene Rohrverbindung", "Rohrverbindung versetzt oder Knick");
        sb.AppendLine();

        sb.AppendLine("OBERFLAECHEN / EINWUCHS / ABLAGERUNGEN:");
        AppendCodeLine(sb, codeCatalog, "BAF", "Oberflaechenschaden", "rauhe Rohrwandung, chemischer Angriff, Korrosion");
        AppendCodeLine(sb, codeCatalog, "BBA", "Wurzeln", "Wurzeleinwuchs/Bewuchs");
        AppendCodeLine(sb, codeCatalog, "BBB", "Anhaftende Stoffe", "Inkrustation/Fett/anhaftende Stoffe");
        AppendCodeLine(sb, codeCatalog, "BBC", "Ablagerungen", "Sand/Kies/verfestigte Ablagerung");
        AppendCodeLine(sb, codeCatalog, "BBD", "Eindringendes Bodenmaterial", null);
        sb.AppendLine();

        sb.AppendLine("SONSTIGES:");
        AppendCodeLine(sb, codeCatalog, "BDDC", "Wasserspiegel/Wasserstand", "BDDC nur bei sichtbar angestautem/stehendem Wasser oder Rueckstau; normal fliessendes/truebes Abwasser ist kein Befund");
        AppendCodeLine(sb, codeCatalog, "BABBA", "Riss laengs", "mit Uhrlage und Breite in mm");
        AppendCodeLine(sb, codeCatalog, "BABAA", "Riss quer", null);

        return sb.ToString();
    }

    /// <summary>
    /// Fuegt eine Zeile mit Code, Titel und optionalem Hinweis an den StringBuilder an.
    /// Der Titel wird aus dem Katalog ermittelt; bei fehlendem Eintrag wird der Fallback-Titel verwendet.
    /// </summary>
    internal static void AppendCodeLine(
        StringBuilder sb,
        ICodeCatalogProvider? codeCatalog,
        string code,
        string fallbackTitle,
        string? hint)
    {
        var title = LookupCatalogTitle(codeCatalog, code) ?? fallbackTitle;
        sb.Append($"- {code} = {title}");
        if (!string.IsNullOrWhiteSpace(hint))
            sb.Append($" ({hint})");
        sb.AppendLine();
    }

    /// <summary>
    /// Sucht den Titel eines Codes im Katalog. Zuerst exakten Treffer, dann
    /// Haupt-Code-Fallback (erste 3 Zeichen). Gibt null zurueck wenn nicht gefunden.
    /// </summary>
    internal static string? LookupCatalogTitle(ICodeCatalogProvider? codeCatalog, string code)
    {
        if (codeCatalog is null)
            return null;

        if (codeCatalog.TryGet(code, out var exact) && !string.IsNullOrWhiteSpace(exact.Title))
            return exact.Title.Trim();

        if (code.Length > 3 && codeCatalog.TryGet(code[..3], out var main) && !string.IsNullOrWhiteSpace(main.Title))
            return main.Title.Trim();

        return null;
    }

    /// <summary>
    /// Baut den Import-Kontext-Abschnitt: Bekannte Befunde aus dem Inspektionsprotokoll
    /// als Erwartungshorizont fuer die KI-Analyse.
    /// </summary>
    internal static string BuildImportContextSection(
        IReadOnlyList<(string Code, string Description, double Meter)>? importContext)
    {
        if (importContext is null || importContext.Count == 0)
            return "";

        var sb = new StringBuilder();
        sb.AppendLine();
        sb.AppendLine("BEKANNTE BEFUNDE AUS DEM INSPEKTIONSPROTOKOLL (Erwartungshorizont):");
        sb.AppendLine("Diese Schaeden wurden in dieser Haltung bereits dokumentiert.");
        sb.AppendLine("Verwende bevorzugt diese VSA-Codes wenn die visuellen Anzeichen passen:");

        // Deduplizierung: gleicher Code nur einmal zeigen
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (code, desc, meter) in importContext)
        {
            if (string.IsNullOrWhiteSpace(code) || !seen.Add(code))
                continue;
            var meterInfo = meter > 0 ? $" @ {meter:F1}m" : "";
            sb.AppendLine($"  - {code}: {desc}{meterInfo}");
        }

        sb.AppendLine();
        sb.AppendLine("WICHTIG: Wenn du einen Schaden erkennst der zu einem dieser Codes passt,");
        sb.AppendLine("verwende EXAKT diesen Code als vsa_code_hint (nicht erfinden, nicht ??? verwenden).");
        sb.AppendLine("is_empty_frame=true nur dann setzen, wenn keiner dieser bekannten Befunde sichtbar ist.");
        sb.AppendLine("Bekannte Befunde koennen auch Rohranfang, Rohrende, Wasserstand, Anschluss oder Bogen sein - nicht nur klassische Schaeden.");
        return sb.ToString();
    }

    /// <summary>
    /// Baut den Abschnitt mit unsicheren Bild-Hinweisen (z.B. aus YOLO-cls).
    /// Diese Hinweise sind keine VSA-Code-Vorgabe.
    /// </summary>
    internal static string BuildObservationHintsSection(IReadOnlyList<string>? observationHints)
    {
        if (observationHints is null || observationHints.Count == 0)
            return "";

        var sb = new StringBuilder();
        sb.AppendLine();
        sb.AppendLine("ZUSAETZLICHE BILD-HINWEISE (unsicher, nicht als VSA-Code uebernehmen):");
        foreach (var hint in observationHints)
        {
            if (!string.IsNullOrWhiteSpace(hint))
                sb.AppendLine($"  - {hint.Trim()}");
        }

        sb.AppendLine();
        sb.AppendLine("Diese Hinweise sind nur ein Suchhinweis. Verwende sie nicht als VSA-Code.");
        sb.AppendLine("is_empty_frame=true nur dann setzen, wenn trotz Hinweis keine sichtbare Auffaelligkeit vorhanden ist.");
        return sb.ToString();
    }

    /// <summary>
    /// Baut den DINO/SAM-Kontext-Prompt fuer die Multi-Modell-Analyse.
    /// Nimmt Erkennungen und Segmentierungen als vorberechneten Kontext fuer Qwen auf.
    /// </summary>
    internal static string BuildContextPrompt(
        MultiModelFrameResult ctx,
        int pipeDiameterMm,
        (string Code, string Description, double Meter, double Confidence)? previousFinding = null)
    {
        var sb = new StringBuilder();

        // Vorheriger Befund fuer temporale Kohaerenz
        if (previousFinding is var (prevCode, prevDesc, prevMeter, prevConf))
        {
            sb.AppendLine("VORHERIGER BEFUND (Kontext aus dem vorherigen Analyseabschnitt):");
            sb.AppendLine($"  Bei {prevMeter:F2}m wurde '{prevCode}' ({prevDesc}) vermutet (Konfidenz: {prevConf:F0}%).");
            sb.AppendLine("  Pruefe ob das aktuelle Bild dasselbe Objekt zeigt oder einen neuen/anderen Befund.");
            sb.AppendLine();
        }

        sb.AppendLine("KONTEXT AUS VORHERIGER ANALYSE (Computer Vision Modelle):");
        sb.AppendLine($"- Bild: {ctx.ImageWidth}x{ctx.ImageHeight} px");
        sb.AppendLine($"- Rohrdurchmesser: DN{pipeDiameterMm}");
        sb.AppendLine();

        if (ctx.DinoDetections.Count > 0)
        {
            sb.AppendLine("ERKANNTE OBJEKTE (Grounding DINO):");
            foreach (var det in ctx.DinoDetections)
            {
                sb.AppendLine($"  - {det.Label} (Confidence={det.Confidence:F2}) @ [{det.X1:F0},{det.Y1:F0},{det.X2:F0},{det.Y2:F0}]");
            }
            sb.AppendLine();
        }

        if (ctx.SamMasks.Count > 0)
        {
            var quantified = MaskQuantificationService.QuantifyAll(
                new SamResponse(ctx.SamMasks, ctx.ImageWidth, ctx.ImageHeight, 0),
                pipeDiameterMm);

            sb.AppendLine("SEGMENTIERUNGSERGEBNISSE (SAM – pixelgenaue Masken):");
            foreach (var q in quantified)
            {
                sb.AppendLine($"  - {q.Label}: Höhe={q.HeightMm}mm, Breite={q.WidthMm}mm, " +
                    $"Ausdehnung={q.ExtentPercent}%, Querschnitt={q.CrossSectionReductionPercent}%, " +
                    $"Uhrlage={q.ClockPosition ?? "?"}");
            }
            sb.AppendLine();
        }

        sb.AppendLine("Bitte nutze diese Voranalyse als Kontext. Die Quantifizierungswerte aus SAM sind pixelgenau berechnet – übernimm sie bevorzugt.");
        sb.AppendLine("Deine Aufgabe: VSA-Code-Zuweisung und Validierung der Klassifizierung.");
        return sb.ToString();
    }
}
