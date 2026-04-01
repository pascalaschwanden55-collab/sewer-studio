using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.UI.Ai.Pipeline;
using AuswertungPro.Next.UI.Ai.Training;

namespace AuswertungPro.Next.UI.Ai;

/// <summary>
/// Verbesserte Vision-Analyse mit vollständiger Schadensklassenliste nach
/// DIN EN 13508-2 und VSA-DSS (umfangreiche Schadensklassifikation).
///
/// Unterschiede zum bestehenden OllamaVisionFindingsService:
/// - Detailliertere Schadensklassen (Typ + Untertyp + Lage)
/// - Quantifizierung direkt im Vision-Schritt (Severity 1-5)
/// - OSD-Erkennung für Meterstand, Zeit, Haltungsinfo
/// - Materialkennzeichnung
/// - Strukturierte Ausgabe mit vsaCode-Vorschlag
/// </summary>
public sealed class EnhancedVisionAnalysisService
{
    private static readonly JsonElement EnhancedVisionSchema = JsonDocument.Parse("""
    {
      "type": "object",
      "additionalProperties": false,
      "properties": {
        "meter": { "type": ["number", "null"], "description": "OSD-Meterstand in Metern" },
        "time_in_video": { "type": ["number", "null"], "description": "Zeitstempel im Video in Sekunden" },
        "pipe_material": {
          "type": "string",
          "enum": ["beton", "steinzeug", "pvc", "pe", "gfk", "stahl", "mauerwerk", "faserzement", "unbekannt"]
        },
        "pipe_diameter_mm": { "type": ["integer", "null"] },
        "findings": {
          "type": "array",
          "items": {
            "type": "object",
            "additionalProperties": false,
            "properties": {
              "label": { "type": "string" },
              "vsa_code_hint": { "type": ["string", "null"], "description": "Wahrscheinlichster VSA/EN-Code" },
              "severity": { "type": "integer", "minimum": 1, "maximum": 5 },
              "position_clock": { "type": ["string", "null"], "description": "Uhrzeitlage z.B. 12:00, 6:00, 3:00" },
              "extent_percent": { "type": ["integer", "null"], "description": "Ausdehnung in % des Umfangs" },
              "height_mm": { "type": ["integer", "null"], "description": "Schadenshöhe in mm" },
              "width_mm": { "type": ["integer", "null"], "description": "Schadensbreite in mm" },
              "intrusion_percent": { "type": ["integer", "null"], "description": "Einragungsgrad in %" },
              "cross_section_reduction_percent": { "type": ["integer", "null"], "description": "Querschnittsverringerung in %" },
              "diameter_reduction_mm": { "type": ["integer", "null"], "description": "Durchmesserverringerung in mm" },
              "notes": { "type": ["string", "null"] }
            },
            "required": ["label", "severity"]
          }
        },
        "image_quality": {
          "type": "string",
          "enum": ["gut", "mittel", "schlecht"]
        },
        "is_empty_frame": { "type": "boolean" }
      },
      "required": ["meter", "findings", "image_quality", "is_empty_frame"]
    }
    """).RootElement.Clone();

    // Vollständige Schadensklassen nach DIN EN 13508-2 / VSA-DSS
    // Gruppiert für besseren Prompt
    private static readonly string DamageClassesPrompt = """
VSA/EN 13508-2 CODES fuer Kanalinspektion (Haltungen).
Melde ALLES was du siehst. Jeder Befund braucht vsa_code_hint, severity, position_clock.

=== BA: BAULICHE SCHAEDEN (severity 2-5) ===
BAA  Deformation (BAAA=vertikal, BAAB=horizontal) — Uhrlage + Querschnittsverringerung %
BAB  Riss (BABA/BABBA=laengs, BABB/BABBB=radial, BABC=klaffend) — Uhrlage von-bis
BAC  Bruch/Scherbe (BACA=verschoben, BACB=Loch, BACC=Einsturz) — Uhrlage von-bis
BAD  Mauerwerk defekt (BADA=verschoben, BADB=fehlen, BADC=Sohle, BADD=Einsturz)
BAE  Moertel fehlt — Uhrlage von-bis
BAF  Oberflaechenschaden (BAFA=rau, BAFB=Abplatzung, BAFC-BAFH=Zuschlag/Armierung, BAFI=Wand fehlt, BAFJ=korrodiert)
BAG  Anschluss einragend — Uhrlage
BAH  Anschluss schadhaft (BAHA=falsch, BAHB=zurueck, BAHC=unvollstaendig, BAHD=beschaedigt)
BAI  Dichtung (BAIA=Dichtring verschoben/einragend)
BAJ  Rohrverbindung (BAJA=breit, BAJB=versetzt, BAJC=Knick) — Uhrlage
BAK  Innenauskleidung (BAKA=abgeloest, BAKB=verfaerbt, BAKC=Endstelle, BAKE=Blasen, BAKI=Riss)
BAL  Reparatur mangelhaft (BALA=Wand fehlt, BALB=Loch, BALC=loest sich)
BAM  Schweissnaht mangelhaft (BAMA=laengs, BAMB=radial)
BAN  Leitung poroes
BAO  Boden sichtbar
BAP  Hohlraum sichtbar

=== BB: BETRIEBLICHE STOERUNGEN (severity 2-5) ===
BBA  Wurzeleinwuchs (BBAA=Pfahlwurzel, BBAB=fein, BBAC=komplex) — Uhrlage + Ausmass %
BBB  Anhaftungen (BBBA=Inkrustation/Kalk, BBBB=Fett, BBBC=Faeulnis) — Uhrlage + Ausmass %
BBC  Ablagerungen (BBCA=Sand, BBCB=Kies, BBCC=hart) — Hoehe in % des Querschnitts
BBD  Bodeneindringung (BBDA=Sand, BBDB=Humus, BBDC=Fein, BBDD=Grob) — Uhrlage
BBE  Hindernis (BBEA=Backsteine, BBEB=Leitungsstueck, BBEC=Gegenstand, BBED=durch Wand)
BBF  Infiltration/Eindringendes Wasser (BBFA=Schwitzen, BBFB=tropft, BBFC=fliesst, BBFD=spritzt) — Uhrlage
BBG  Sichtbarer Wasseraustritt
BBH  Tiere (BBHA=Ratte, BBHB=Kakerlake)

=== BC: BESTANDSAUFNAHME (severity=1, MUESSEN gemeldet werden!) ===
BCA  Anschluss (BCAAA=Formstueck, BCABA=Sattel gebohrt, BCACA=eingespitzt, BCADA=gebohrt) — Uhrlage + Durchmesser mm
BCB  Reparatur (BCBA=Rohr ausgetauscht, BCBB=Innenauskleidung, BCBZ=grabenlos)
BCC  Bogen/Kurve (BCCA=links, BCCB=rechts, BCCY=vertikal) — Richtung
BCD  Rohranfang — immer bei Meter 0.0
BCE  Rohrende — am Ende der Haltung

=== BD: WEITERE INFORMATIONEN (severity=1) ===
BDA  Allgemeinzustand, Fotobeispiel
BDD  Wasserspiegel (BDDA=klar, BDDB=trueb, BDDD=gefaerbt) — Hoehe %
BDE  Fehlanschluss — Uhrlage
BDF  Gefaehrdung (BDFA=Sauerstoffmangel, BDFB=Schwefelwasserstoff, BDFC=Methan)
BDG  Keine Sicht (BDGA=unter Wasser, BDGB=Verschlammung, BDGC=Dampf)

=== AE: AENDERUNGEN WAEHREND INSPEKTION (severity=1) ===
AECXC  Rohrprofilwechsel (Kreisprofil→Eiprofil etc.) — sichtbarer Querschnittswechsel im Bild
AEDXO  Rohrmaterialwechsel: Polyethylen (PE) — Farbwechsel der Rohrwand sichtbar (z.B. grau→schwarz)
AEDXP  Rohrmaterialwechsel: Polypropylen (PP) — aehnlich PE, oft heller
AEDXQ  Rohrmaterialwechsel: PVC — typisch hellgrau/weiss, glatte Oberflaeche
AEDXG  Rohrmaterialwechsel: Beton — raue, graue Oberflaeche
AEDXU  Rohrmaterialwechsel: Steinzeug — braun/rotbraun, glasiert
AEF    Neue Baulaenge — normaler Rohruebergang mit Laengenmarkierung, KEIN Schaden

=== HAEUFIGE VERWECHSLUNGEN (VERMEIDEN!) ===
BACB (Loch) vs BCD (Rohranfang) vs BCA (Anschluss) — WICHTIGSTE UNTERSCHEIDUNG:
- BCD (Rohranfang): RUNDE, SAUBERE Oeffnung. GLATTE Raender. Rohrwand dahinter SICHTBAR.
  Das Bild zeigt den kompletten Rohrquerschnitt von vorne. severity=1.
  MERKE: Wenn du am ANFANG einer Haltung bist (Meter ~0.0) und eine grosse runde Oeffnung siehst = BCD!
- BCA (Anschluss): SEITLICHE Oeffnung in der Rohrwand. KLEINER als Hauptkanal. Rund/oval.
  Fuehrt zu einem Abzweig. severity=1. Code: BCAAA (Formstueck) oder BCAEA (eingespitzt).
- BACB (Loch): UNRUNDE, UNREGELMAESSIGE Form. SCHARFE, GEZACKTE, GEBROCHENE Raender.
  Material FEHLT. Rohr ist BESCHAEDIGT. severity=3-4.
  ENTSCHEIDUNGSREGEL:
  → Raender GLATT + RUND → KEIN BACB sondern BCD oder BCA
  → Raender GEZACKT + GEBROCHEN → BACB
  → Im Zweifel: BCD oder BCA ist WAHRSCHEINLICHER als BACB (Schaeden sind selten, Rohranfaenge haeufig)

BAIA (Dichtring) vs BAJ (Rohrverbindung) vs AEF (Materialwechsel):
- BAJ: Sichtbare FUGE zwischen zwei Rohrsegmenten (Spalt, Versatz, Knick). Code: BAJA/BAJB/BAJC.
  Das ist der HAEUFIGSTE Fall bei Rohrverbindungen.
- BAIA: Dichtungsmaterial RAGT PHYSISCH in den Rohrquerschnitt HINEIN. Gummiring SICHTBAR verlagert.
  NUR wenn Material tatsaechlich EINRAGT (>20% des Querschnitts). Severity=2-3.
  → Normale Dichtung sichtbar aber NICHT einragend = BAJ, NICHT BAIA!
- AEF: Normaler Rohruebergang, Laengenmarkierung. KEIN Schaden, severity=1.
  ENTSCHEIDUNGSREGEL:
  → Normale Rohrverbindung ohne Auffaelligkeit = AEF
  → Fuge sichtbar, Versatz/Spalt = BAJ
  → Dichtungsmaterial ragt PHYSISCH ins Rohr = BAIA (selten!)

BBFC (Infiltration fliesst) vs BCD (Rohranfang mit Wasser):
- BCD: Am Rohranfang ist oft Wasser sichtbar (Restwasser im Schacht). Das ist KEIN Infiltration.
  → Wasser am Rohranfang/Schacht = normal, NICHT BBFC melden.
- BBFC: Wasser DRINGT AKTIV DURCH die Rohrwand oder Fuge. Nasser Fleck, Rinnsal an der Wand.
  → Nur wenn Wasser DURCH eine Schadstelle eindringt = BBFC.

=== REGELN (STRIKT EINHALTEN) ===
- SPRACHE: Antworte AUSSCHLIESSLICH auf Deutsch. Kein Englisch.
- label MUSS ein VSA-Code sein (z.B. "BABBA", "BCAAA", "BCD", "BBFA"). KEIN Freitext, KEIN Englisch, KEINE Beschreibung.
  FALSCH: "Seal defect", "hole", "Ablagerungen", "Korrosion (Corroded)"
  RICHTIG: "BAI", "BACB", "BBCC", "BAFJ"
- vsa_code_hint: gleicher Code wie label (Pflicht)
- Verwende den SPEZIFISCHSTEN Code den du bestimmen kannst (BABBA statt BAB, BCAAA statt BCA)
- severity: 1=Beobachtung, 2=leicht, 3=mittel, 4=schwer, 5=kritisch
- position_clock: Uhrlage als "HH" (z.B. "12"=Scheitel, "6"=Sohle, "3"=rechts, "9"=links)
- extent_percent: Ausdehnung in % des Rohrumfangs (bei Rissen, Wurzeln, Inkrustation)
- cross_section_reduction_percent: Querschnittsverringerung % (bei Deformation, Ablagerung)
- NUR offizielle VSA/EN 13508-2 Codes verwenden. Keine erfundenen Codes.
- BC-Codes (Rohranfang, Rohrende, Bogen, Anschluss) sind severity=1, MUESSEN gemeldet werden
- Wenn NICHTS sichtbar: findings=[] und is_empty_frame=true
""";


    private readonly OllamaClient _client;
    private readonly string _model;
    private readonly string? _referenceModel;
    private readonly int _numCtx;
    // 2 parallele Eskalationen erlauben (RTX 5090: genug VRAM fuer 32B + 8B)
    private readonly SemaphoreSlim _escalationLock = new(2, 2);
    private int _escalationCount;
    // Telemetrie: Eskalationsgruende einzeln zaehlen fuer datenbasierte Schwellen-Justierung
    private int _escalationAllCodesNull;
    private int _escalationHighSeverity;
    private int _escalationPoorQuality;
    private FewShotExampleStore? _fewShotStore;
    private IReadOnlyList<(FewShotExample Example, string Base64)>? _cachedFewShot;

    public EnhancedVisionAnalysisService(OllamaClient client, string model, string? referenceModel = null, int numCtx = 4096)
    {
        _client = client;
        _model = model;
        _referenceModel = referenceModel;
        _numCtx = numCtx;
    }

    /// <summary>Anzahl erfolgreicher Eskalationen (Telemetrie).</summary>
    public int EscalationCount => _escalationCount;
    /// <summary>Eskalationen wegen fehlender VSA-Codes (alle Findings ohne Code).</summary>
    public int EscalationAllCodesNull => _escalationAllCodesNull;
    /// <summary>Eskalationen wegen hohem Schweregrad (Severity >= 4).</summary>
    public int EscalationHighSeverity => _escalationHighSeverity;
    /// <summary>Eskalationen wegen schlechter Bildqualitaet mit Findings.</summary>
    public int EscalationPoorQuality => _escalationPoorQuality;

    /// <summary>
    /// Aktiviert Few-Shot Learning: Beispielbilder werden in den Prompt injiziert.
    /// Sollte einmal beim Start aufgerufen werden.
    /// </summary>
    public async Task EnableFewShotAsync(FewShotExampleStore store, CancellationToken ct = default)
    {
        _fewShotStore = store;
        await store.LoadAsync(ct);

        // Few-Shot Beispiele basierend auf Context-Limit
        int maxExamples = _numCtx <= 2048 ? 2 : 4;
        var examples = await store.GetBestExamplesAsync(maxExamples, ct: ct);

        // Bilder vorladen und als Base64 cachen (einmal laden, bei jeder Analyse verwenden)
        var loaded = new List<(FewShotExample, string)>();
        foreach (var ex in examples)
        {
            var imgBytes = await store.LoadImageAsync(ex, ct);
            if (imgBytes != null)
                loaded.Add((ex, Convert.ToBase64String(imgBytes)));
        }

        _cachedFewShot = loaded;

        System.Diagnostics.Debug.WriteLine(
            $"[EnhancedVision] Few-Shot aktiviert: {loaded.Count} Beispiele geladen " +
            $"({string.Join(", ", loaded.Select(l => l.Item1.VsaCode))})");
    }

    public async Task<EnhancedFrameAnalysis> AnalyzeAsync(
        string framePngBase64,
        CancellationToken ct = default)
    {
        var messages = BuildMessages(framePngBase64);

        EnhancedVisionDto dto;
        try
        {
            dto = await _client.ChatStructuredAsync<EnhancedVisionDto>(
                model: _model,
                messages: messages,
                formatSchema: EnhancedVisionSchema,
                ct: ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[EnhancedVision] KI-Fehler ({_model}): {ex.GetType().Name}: {ex.Message}");
            return EnhancedFrameAnalysis.Empty(ex.Message);
        }

        return MapToAnalysis(dto);
    }

    /// <summary>
    /// Analysiert ein Foto aus einem PDF-Bildbericht (KEIN Video-Frame).
    /// Angepasster Prompt ohne OSD-Erwartung, fokussiert auf Schadenserkennung.
    /// </summary>
    public async Task<EnhancedFrameAnalysis> AnalyzePdfPhotoAsync(
        string framePngBase64,
        CancellationToken ct = default)
    {
        var messages = new List<OllamaClient.ChatMessage>();

        // Few-Shot Beispiele auch fuer PDF-Fotos nutzen
        if (_cachedFewShot is { Count: > 0 })
        {
            foreach (var (example, b64) in _cachedFewShot)
            {
                var exPrompt = $"Analysiere dieses Kanalbild. " +
                    $"Hinweis: Dieses Bild zeigt {example.Description}" +
                    (example.ClockPosition != null ? $" bei {example.ClockPosition}" : "") +
                    $" (VSA-Code: {example.VsaCode}).";

                messages.Add(new OllamaClient.ChatMessage(
                    Role: "user",
                    Content: exPrompt,
                    ImagesBase64: [b64]));

                var exResponse = BuildFewShotResponse(example);
                messages.Add(new OllamaClient.ChatMessage(
                    Role: "assistant",
                    Content: exResponse));
            }
        }

        messages.Add(new OllamaClient.ChatMessage(
            Role: "user",
            Content: BuildPdfPhotoPrompt(),
            ImagesBase64: [framePngBase64]));

        EnhancedVisionDto dto;
        try
        {
            dto = await _client.ChatStructuredAsync<EnhancedVisionDto>(
                model: _model,
                messages: messages,
                formatSchema: EnhancedVisionSchema,
                ct: ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[EnhancedVision] PDF-Foto KI-Fehler ({_model}): {ex.GetType().Name}: {ex.Message}");
            return EnhancedFrameAnalysis.Empty(ex.Message);
        }

        return MapToAnalysis(dto);
    }

    /// <summary>
    /// Baut die Chat-Messages auf, mit Few-Shot Beispielen falls vorhanden.
    /// </summary>
    private IReadOnlyList<OllamaClient.ChatMessage> BuildMessages(string framePngBase64)
    {
        var messages = new List<OllamaClient.ChatMessage>();

        // Few-Shot Beispiele als User/Assistant-Paare injizieren
        if (_cachedFewShot is { Count: > 0 })
        {
            foreach (var (example, b64) in _cachedFewShot)
            {
                // User zeigt Beispielbild mit Kontext
                var exPrompt = $"Analysiere dieses Kanalbild. " +
                    $"Hinweis: Dieses Bild zeigt {example.Description}" +
                    (example.ClockPosition != null ? $" bei {example.ClockPosition}" : "") +
                    $" (VSA-Code: {example.VsaCode}).";

                messages.Add(new OllamaClient.ChatMessage(
                    Role: "user",
                    Content: exPrompt,
                    ImagesBase64: [b64]));

                // Assistant antwortet mit korrekter Klassifizierung
                var exResponse = BuildFewShotResponse(example);
                messages.Add(new OllamaClient.ChatMessage(
                    Role: "assistant",
                    Content: exResponse));
            }
        }

        // Eigentliche Analyse-Anfrage
        messages.Add(new OllamaClient.ChatMessage(
            Role: "user",
            Content: BuildPrompt(),
            ImagesBase64: [framePngBase64]));

        return messages;
    }

    /// <summary>Baut eine synthetische "korrekte" Assistant-Antwort fuer ein Few-Shot Beispiel.</summary>
    private static string BuildFewShotResponse(FewShotExample example)
    {
        var finding = new Dictionary<string, object?>
        {
            ["label"] = example.Description.Length > 60
                ? example.Description[..60]
                : example.Description,
            ["vsa_code_hint"] = example.VsaCode,
            ["severity"] = EstimateSeverity(example.VsaCode),
            ["position_clock"] = FormatClock(example.ClockPosition),
            ["extent_percent"] = (object?)null,
            ["height_mm"] = (object?)null,
            ["width_mm"] = (object?)null,
            ["intrusion_percent"] = (object?)null,
            ["cross_section_reduction_percent"] = (object?)null,
            ["diameter_reduction_mm"] = (object?)null,
            ["notes"] = (object?)null
        };

        var response = new Dictionary<string, object?>
        {
            ["meter"] = example.MeterPosition,
            ["time_in_video"] = (object?)null,
            ["pipe_material"] = GuessMaterial(example.Material),
            ["pipe_diameter_mm"] = (object?)null,
            ["findings"] = new[] { finding },
            ["image_quality"] = "gut",
            ["is_empty_frame"] = false
        };

        return JsonSerializer.Serialize(response);
    }

    /// <summary>Schaetzt Schweregrad anhand VSA-Code Praefix.</summary>
    private static int EstimateSeverity(string code)
    {
        if (code.Length < 2) return 2;
        return code[..2].ToUpperInvariant() switch
        {
            "BA" => code.Length >= 3 && code[2] == 'C' ? 4 : 3, // Bruch=4, sonst Riss=3
            "BB" => 2,  // Betrieblich
            "BC" => 2,  // Anschluss
            "BD" => 1,  // Allgemein
            _ => 2
        };
    }

    /// <summary>Konvertiert Material-String in Schema-Enum.</summary>
    private static string GuessMaterial(string? material)
    {
        if (string.IsNullOrEmpty(material)) return "unbekannt";
        var m = material.ToLowerInvariant();
        if (m.Contains("beton")) return "beton";
        if (m.Contains("steinzeug")) return "steinzeug";
        if (m.Contains("pvc") || m.Contains("kunststoff") || m.Contains("polypropylen") || m.Contains("pe"))
            return "pvc";
        if (m.Contains("gfk")) return "gfk";
        if (m.Contains("stahl")) return "stahl";
        if (m.Contains("faser")) return "faserzement";
        return "unbekannt";
    }

    /// <summary>Formatiert Uhrzeitlage fuer JSON-Antwort.</summary>
    private static string? FormatClock(string? clock)
    {
        if (string.IsNullOrEmpty(clock)) return null;
        // "12 Uhr" → "12:00", "6 Uhr bis 9 Uhr" → "6:00"
        var match = System.Text.RegularExpressions.Regex.Match(clock, @"(\d{1,2})");
        return match.Success ? $"{int.Parse(match.Groups[1].Value)}:00" : null;
    }

    /// <summary>
    /// Prompt fuer PDF-Bildbericht-Fotos: kein OSD erwartet, Fokus auf Schadenserkennung.
    /// </summary>
    private string BuildPdfPhotoPrompt()
    {
        return $"""
Du analysierst ein Foto aus einem Kanalinspektion-Protokoll (Bildbericht aus PDF).
Das Foto stammt aus einer TV-Inspektion eines Abwasserkanals.
WICHTIG: Es gibt KEIN OSD (On-Screen Display) in diesem Bild. Setze meter=null.

AUFGABEN:
1. Erkenne ALLE sichtbaren Schäden/Anomalien mit Schweregrad 1-5 (1=kaum, 5=sehr schwer).
2. Gib für jeden Schaden den wahrscheinlichsten VSA/EN 13508-2 Code als vsa_code_hint an.
3. Gib für jeden Schaden die Uhrzeitlage an (z.B. "12:00" = Scheitel/oben, "6:00" = Sohle/unten).
4. Erkenne das ROHRMATERIAL falls sichtbar.
5. Beurteile die Bildqualität.
6. Schätze, wenn erkennbar, Schadensmaße: Höhe (mm), Breite (mm), Ausdehnung (%), Querschnittsverringerung (%).

{DamageClassesPrompt}

SCHWEREGRAD-SKALA (entspricht VSA Zustandsklasse):
1 = Optische Auffälligkeit, kein Handlungsbedarf
2 = Leichter Schaden, Beobachtung empfohlen
3 = Mittlerer Schaden, Sanierung mittelfristig
4 = Schwerer Schaden, Sanierung kurzfristig
5 = Kritischer Schaden, Sofortmassnahme

WICHTIG: Das label-Feld ist IMMER ein VSA-Code, z.B.:
- Rohranfang (Blick vom Schacht ins Rohr, grosse runde Oeffnung) → label="BCD" (NICHT "BACB"!)
- Rohrende → label="BCE"
- Seitlicher Anschluss (runde Oeffnung in der Wand) → label="BCAAA" (NICHT "BACB"!)
- Rohrverbindung (Fuge zwischen Segmenten) → label="BAJC" (NICHT "BAIA"!)
- Riss laengs → label="BABBA"
- Bruch/Loch (fehlende Wandung, gezackte Kanten) → label="BACB"
- Wurzeleinwuchs → label="BBAC"
- Ablagerung hart → label="BBCC"
- Infiltration (Wasser dringt aktiv durch Wand) → label="BBFA" (NICHT bei Restwasser am Rohranfang!)
- Inkrustation → label="BBBA"
- Bogen nach links → label="BCCAY"

Antworte AUSSCHLIESSLICH auf Deutsch mit gueltigem JSON gemaess Schema.
Falls kein Schaden erkennbar: findings=[], is_empty_frame=true.
Wenn du einen Schaden siehst, gib IMMER mindestens einen Finding-Eintrag zurueck.
""";
    }

    private string BuildPrompt()
    {
        return $"""
Du analysierst einen Frame aus einem Kanalinspektion-Video (TV-Inspektion Abwasserkanal).

AUFGABEN:
1. Lies den METERSTAND aus dem OSD (On-Screen Display) – typisch oben oder unten im Bild (z.B. "18.40 m").
2. Erkenne das ROHRMATERIAL und den DURCHMESSER falls sichtbar.
3. Erkenne ALLE sichtbaren Schäden/Anomalien mit Schweregrad 1-5 (1=kaum, 5=sehr schwer).
4. Gib für jeden Schaden die Uhrzeitlage an (z.B. "12:00" = Scheitel, "6:00" = Sohle).
5. Beurteile die Bildqualität.
6. Schätze, wenn erkennbar, Schadensmaße: Höhe (mm), Breite (mm), Einragungsgrad (%), Querschnittsverringerung (%), Durchmesserverringerung (mm).

{DamageClassesPrompt}

SCHWEREGRAD-SKALA (entspricht VSA Zustandsklasse):
1 = Optische Auffälligkeit, kein Handlungsbedarf
2 = Leichter Schaden, Beobachtung empfohlen
3 = Mittlerer Schaden, Sanierung mittelfristig
4 = Schwerer Schaden, Sanierung kurzfristig
5 = Kritischer Schaden, Sofortmassnahme
WICHTIG: Das label-Feld ist IMMER ein VSA-Code (z.B. "BABBA", "BCAAA", "BBFA"). KEIN Freitext.
Antworte AUSSCHLIESSLICH auf Deutsch mit gueltigem JSON gemaess Schema.
Falls kein Schaden erkennbar: findings=[], is_empty_frame=true.
""";
    }

    private static EnhancedFrameAnalysis MapToAnalysis(EnhancedVisionDto dto)
    {
        var findings = (dto.Findings ?? Array.Empty<EnhancedFindingDto>())
            .Where(f => !string.IsNullOrWhiteSpace(f.Label))
            .Select(f =>
            {
                var label = f.Label.Trim();
                var codeHint = f.VsaCodeHint?.Trim();

                // Code-Extraktion aus Label (3 Fallbacks):
                // 1. Label ist reiner VSA-Code (3-6 Grossbuchstaben): "BABBA" → codeHint
                // 2. Label beginnt mit Code + Doppelpunkt: "BBBA: Inkrustation..." → "BBBA"
                // 3. Label ist Freitext → InferCodeFromLabel/ReverseLookup
                if (string.IsNullOrEmpty(codeHint))
                {
                    if (label.Length >= 3 && label.Length <= 6
                        && label.All(c => c >= 'A' && c <= 'Z'))
                    {
                        // Reiner VSA-Code
                        codeHint = label;
                    }
                    else if (label.Length >= 4 && label[..3].All(c => c >= 'A' && c <= 'Z')
                             && (label[3] == ':' || label[3] == ' ' || (label.Length > 3 && label[3] >= 'A' && label[3] <= 'Z')))
                    {
                        // Code am Anfang: "BBBA: Inkrustation..." oder "BBBA Inkrustation"
                        var codePart = label.Split(new[] { ':', ' ', '-' }, 2)[0].Trim();
                        if (codePart.Length >= 3 && codePart.Length <= 6 && codePart.All(c => c >= 'A' && c <= 'Z'))
                            codeHint = codePart;
                    }

                    // Freitext → VSA-Code (deutsch + englisch)
                    if (string.IsNullOrEmpty(codeHint))
                    {
                        codeHint = VsaCodeResolver.InferCodeFromLabel(label)
                            ?? Services.CodeCatalog.VsaCodeTree.ReverseLookup(label);
                    }
                }

                return new EnhancedFinding(
                Label: label,
                VsaCodeHint: codeHint,
                Severity: Math.Clamp(f.Severity, 1, 5),
                PositionClock: f.PositionClock?.Trim(),
                ExtentPercent: f.ExtentPercent,
                HeightMm: f.HeightMm,
                WidthMm: f.WidthMm,
                IntrusionPercent: f.IntrusionPercent,
                CrossSectionReductionPercent: f.CrossSectionReductionPercent,
                DiameterReductionMm: f.DiameterReductionMm,
                Notes: f.Notes?.Trim());
            })
            .ToList();

        return new EnhancedFrameAnalysis(
            Meter: dto.Meter,
            PipeMaterial: dto.PipeMaterial ?? "unbekannt",
            PipeDiameterMm: dto.PipeDiameterMm,
            Findings: findings,
            ImageQuality: dto.ImageQuality ?? "mittel",
            IsEmptyFrame: dto.IsEmptyFrame,
            Error: null);
    }

    /// <summary>
    /// Enhanced analysis that takes DINO/SAM context to improve VSA code assignment.
    /// The LLM receives bounding-box coordinates and quantification values as context.
    /// </summary>
    public async Task<EnhancedFrameAnalysis> AnalyzeWithContextAsync(
        string framePngBase64,
        MultiModelFrameResult multiModelContext,
        int pipeDiameterMm = 300,
        CancellationToken ct = default)
    {
        var contextPrompt = BuildContextPrompt(multiModelContext, pipeDiameterMm);
        var prompt = contextPrompt + "\n\n" + BuildPrompt();

        EnhancedVisionDto dto;
        try
        {
            dto = await _client.ChatStructuredAsync<EnhancedVisionDto>(
                model: _model,
                messages:
                [
                    new OllamaClient.ChatMessage(
                        Role: "user",
                        Content: prompt,
                        ImagesBase64: [framePngBase64])
                ],
                formatSchema: EnhancedVisionSchema,
                ct: ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            return EnhancedFrameAnalysis.Empty(ex.Message);
        }

        return MapToAnalysis(dto);
    }

    /// <summary>
    /// Analysiert mit dem schnellen 8B-Modell. Wenn die Erkennung unsicher ist
    /// und ein Reference-Modell (32B) konfiguriert ist, wird eskaliert.
    /// VRAM-Sicherheit: Primary entladen, Reference laden, danach zurueck.
    /// </summary>
    public async Task<(EnhancedFrameAnalysis Result, bool Escalated)> AnalyzeWithEscalationAsync(
        string framePngBase64,
        MultiModelFrameResult? context,
        int pipeDiameterMm = 300,
        CancellationToken ct = default)
    {
        // 1. Schnelle Analyse mit 8B
        var fast = context != null
            ? await AnalyzeWithContextAsync(framePngBase64, context, pipeDiameterMm, ct).ConfigureAwait(false)
            : await AnalyzeAsync(framePngBase64, ct).ConfigureAwait(false);

        var reason = GetEscalationReason(fast);
        if (_referenceModel == null || reason == EscalationReason.None)
            return (fast, false);

        // Telemetrie: Eskalationsgrund zaehlen (vor dem Lock, damit auch Timeouts erfasst werden)
        switch (reason)
        {
            case EscalationReason.AllCodesNull: Interlocked.Increment(ref _escalationAllCodesNull); break;
            case EscalationReason.HighSeverity: Interlocked.Increment(ref _escalationHighSeverity); break;
            case EscalationReason.PoorQuality: Interlocked.Increment(ref _escalationPoorQuality); break;
        }

        // 2. Eskalation mit 32B — SemaphoreSlim verhindert parallele Modellwechsel
        //    Timeout 120s: wenn Lock nicht verfuegbar → Graceful Degradation (8B-Ergebnis)
        if (!await _escalationLock.WaitAsync(TimeSpan.FromSeconds(120), ct).ConfigureAwait(false))
        {
            System.Diagnostics.Debug.WriteLine(
                "[EnhancedVision] Eskalation uebersprungen — Lock-Timeout (andere Eskalation laeuft)");
            return (fast, false);
        }
        try
        {
            // Primary entladen um VRAM frei zu machen
            await _client.UnloadModelAsync(_model, ct).ConfigureAwait(false);
            await Task.Delay(500, ct).ConfigureAwait(false); // GPU-Treiber VRAM-Freigabe

            try
            {
                // Reference-Modell (32B) fuer Re-Analyse nutzen
                var result = context != null
                    ? await AnalyzeWithModelAsync(_referenceModel, framePngBase64, context, pipeDiameterMm, ct)
                        .ConfigureAwait(false)
                    : await AnalyzeWithModelAsync(_referenceModel, framePngBase64, ct)
                        .ConfigureAwait(false);

                Interlocked.Increment(ref _escalationCount);
                System.Diagnostics.Debug.WriteLine(
                    $"[EnhancedVision] Eskalation #{_escalationCount} zu {_referenceModel}: " +
                    $"{result.Findings.Count} Findings, Quality={result.ImageQuality}");

                return (result, true);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                // OOM oder anderer Fehler beim 32B-Aufruf → 8B-Ergebnis als Fallback
                System.Diagnostics.Debug.WriteLine(
                    $"[EnhancedVision] Eskalation fehlgeschlagen ({ex.GetType().Name}): {ex.Message} — verwende 8B-Ergebnis");
                return (fast, false);
            }
            finally
            {
                // IMMER: Reference entladen, Primary wieder laden
                await _client.UnloadModelAsync(_referenceModel, CancellationToken.None)
                    .ConfigureAwait(false);
                await _client.WarmupModelAsync(_model, _numCtx, CancellationToken.None)
                    .ConfigureAwait(false);
            }
        }
        finally
        {
            _escalationLock.Release();
        }
    }

    /// <summary>Grund fuer die Eskalation (fuer Telemetrie).</summary>
    private enum EscalationReason { None, AllCodesNull, HighSeverity, PoorQuality }

    /// <summary>
    /// Prueft ob eine Eskalation zum Reference-Modell noetig ist.
    /// Gibt den konkreten Grund zurueck (fuer Telemetrie-Zaehler).
    /// </summary>
    private static EscalationReason GetEscalationReason(EnhancedFrameAnalysis fast)
    {
        if (fast.IsEmptyFrame || !fast.HasFindings) return EscalationReason.None;

        // Alle Findings ohne VSA-Code → 8B hat keine Zuordnung gefunden
        if (fast.Findings.All(f => string.IsNullOrEmpty(f.VsaCodeHint)))
            return EscalationReason.AllCodesNull;

        // Hoher Schweregrad → Genauigkeit kritisch
        if (fast.Findings.Any(f => f.Severity >= 4))
            return EscalationReason.HighSeverity;

        // Schlechte Bildqualitaet mit Findings → unsichere Erkennung
        // "schlecht" statt "mittel" — "mittel" ist Normalfall bei Kanalvideos und wuerde zu breit triggern
        if (fast.ImageQuality == "schlecht" && fast.HasFindings)
            return EscalationReason.PoorQuality;

        return EscalationReason.None;
    }

    /// <summary>
    /// Analysiert mit einem spezifischen Modell (fuer Eskalation).
    /// </summary>
    private async Task<EnhancedFrameAnalysis> AnalyzeWithModelAsync(
        string model,
        string framePngBase64,
        CancellationToken ct)
    {
        var messages = BuildMessages(framePngBase64);
        try
        {
            var dto = await _client.ChatStructuredAsync<EnhancedVisionDto>(
                model: model,
                messages: messages,
                formatSchema: EnhancedVisionSchema,
                ct: ct).ConfigureAwait(false);
            return MapToAnalysis(dto);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[EnhancedVision] Eskalation fehlgeschlagen ({model}): {ex.Message}");
            return EnhancedFrameAnalysis.Empty(ex.Message);
        }
    }

    /// <summary>
    /// Analysiert mit spezifischem Modell und Multi-Model-Kontext (fuer Eskalation).
    /// </summary>
    private async Task<EnhancedFrameAnalysis> AnalyzeWithModelAsync(
        string model,
        string framePngBase64,
        MultiModelFrameResult multiModelContext,
        int pipeDiameterMm,
        CancellationToken ct)
    {
        var contextPrompt = BuildContextPrompt(multiModelContext, pipeDiameterMm);
        var prompt = contextPrompt + "\n\n" + BuildPrompt();
        try
        {
            var dto = await _client.ChatStructuredAsync<EnhancedVisionDto>(
                model: model,
                messages:
                [
                    new OllamaClient.ChatMessage(
                        Role: "user",
                        Content: prompt,
                        ImagesBase64: [framePngBase64])
                ],
                formatSchema: EnhancedVisionSchema,
                ct: ct).ConfigureAwait(false);
            return MapToAnalysis(dto);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[EnhancedVision] Eskalation mit Kontext fehlgeschlagen ({model}): {ex.Message}");
            return EnhancedFrameAnalysis.Empty(ex.Message);
        }
    }

    private static string BuildContextPrompt(MultiModelFrameResult ctx, int pipeDiameterMm)
    {
        var sb = new StringBuilder();
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

    // ── DTOs (für JSON-Deserialisierung) ──────────────────────────────────────

    private sealed record EnhancedVisionDto(
        [property: JsonPropertyName("meter")] double? Meter,
        [property: JsonPropertyName("time_in_video")] double? TimeInVideo,
        [property: JsonPropertyName("pipe_material")] string? PipeMaterial,
        [property: JsonPropertyName("pipe_diameter_mm")] int? PipeDiameterMm,
        [property: JsonPropertyName("findings")] IReadOnlyList<EnhancedFindingDto>? Findings,
        [property: JsonPropertyName("image_quality")] string? ImageQuality,
        [property: JsonPropertyName("is_empty_frame")] bool IsEmptyFrame);

    private sealed record EnhancedFindingDto(
        [property: JsonPropertyName("label")] string Label,
        [property: JsonPropertyName("vsa_code_hint")] string? VsaCodeHint,
        [property: JsonPropertyName("severity")] int Severity,
        [property: JsonPropertyName("position_clock")] string? PositionClock,
        [property: JsonPropertyName("extent_percent")] int? ExtentPercent,
        [property: JsonPropertyName("height_mm")] int? HeightMm,
        [property: JsonPropertyName("width_mm")] int? WidthMm,
        [property: JsonPropertyName("intrusion_percent")] int? IntrusionPercent,
        [property: JsonPropertyName("cross_section_reduction_percent")] int? CrossSectionReductionPercent,
        [property: JsonPropertyName("diameter_reduction_mm")] int? DiameterReductionMm,
        [property: JsonPropertyName("notes")] string? Notes);
}

// ── Analysis result types ─────────────────────────────────────────────────────

public sealed record EnhancedFrameAnalysis(
    double? Meter,
    string PipeMaterial,
    int? PipeDiameterMm,
    IReadOnlyList<EnhancedFinding> Findings,
    string ImageQuality,
    bool IsEmptyFrame,
    string? Error)
{
    public bool HasFindings => Findings.Count > 0;

    public static EnhancedFrameAnalysis Empty(string? error = null) =>
        new(null, "unbekannt", null,
            Array.Empty<EnhancedFinding>(), "schlecht", true, error);
}

public sealed record EnhancedFinding(
    string Label,
    string? VsaCodeHint,
    int Severity,         // 1-5
    string? PositionClock,
    int? ExtentPercent,
    int? HeightMm,
    int? WidthMm,
    int? IntrusionPercent,
    int? CrossSectionReductionPercent,
    int? DiameterReductionMm,
    string? Notes,
    // BBox normiert (0.0–1.0) — aus DINO/SAM Pipeline
    double? BboxX1Norm = null,
    double? BboxY1Norm = null,
    double? BboxX2Norm = null,
    double? BboxY2Norm = null,
    double? CentroidXNorm = null,
    double? CentroidYNorm = null
);
