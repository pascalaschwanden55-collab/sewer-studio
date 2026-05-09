using System;
using AuswertungPro.Next.Application.Ai.Vision;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Domain.Ai.Vision;
using AuswertungPro.Next.Infrastructure.Ai.Ollama;
using AuswertungPro.Next.Application.Ai.Ollama;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Ai.Pipeline;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Infrastructure.Ai;

namespace AuswertungPro.Next.Infrastructure.Ai;

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
public sealed partial class EnhancedVisionAnalysisService
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
        "is_empty_frame": { "type": "boolean" },
        "view_type": {
          "type": "string",
          "enum": ["axial", "nahaufnahme", "schwenk", "schacht"],
          "description": "Aufnahmetyp: axial=Axialsicht (normal), nahaufnahme=Kamera nah am Schaden, schwenk=Kamera schwenkt, schacht=Schachtaufnahme"
        }
      },
      "required": ["meter", "findings", "image_quality", "is_empty_frame", "view_type"]
    }
    """).RootElement.Clone();

    // Vollständige Schadensklassen nach DIN EN 13508-2 / VSA-DSS
    // Gruppiert für besseren Prompt
    // Voller Prompt fuer Batch/Video-Pipeline (DamageClassesPromptFull)
    // Fuer den Codier-Modus wird der kurze Prompt (DamageClassesPrompt) verwendet.
    private static readonly string DamageClassesPrompt = """
Kanalinspektion-Frame analysieren. Erkenne ALLE sichtbaren Befunde.

JEDER Befund kommt in das "findings" Array mit: label (VSA-Code), severity (1-5), position_clock.
view_type ist NUR: "axial", "nahaufnahme", "schwenk" oder "schacht". KEIN VSA-Code in view_type!

BEFUNDE (in findings[], severity=1):
BCD = Rohranfang (runde Oeffnung)
BCE = Rohrende
BCC = Bogen/Kurve
BCA = Anschluss (seitliche Oeffnung)

SCHAEDEN (in findings[], severity 2-5):
BAB = Riss, BAC = Bruch, BAF = Korrosion, BAJ = Rohrverbindung
BAI = Dichtung, BAA = Verformung, BBA = Wurzeln, BBB = Kalk
BBC = Ablagerung, BBF = Infiltration

WICHTIG:
- findings[] enthaelt ALLE erkannten VSA-Codes
- view_type beschreibt NUR den Kamerawinkel (axial/nahaufnahme/schwenk/schacht)
- label ist ein VSA-Code wie "BCC" oder "BABBA", KEIN Freitext
- image_quality ist "gut", "mittel" oder "schlecht" (deutsch!)
""";

    // Voller Prompt mit Aufnahmetechnik (fuer Batch/Video-Pipeline, ~1500 Woerter).
    // Phase 0.4: Aktiviert via Konstruktor-Parameter useFullDamagePrompt=true.
    // Siehe docs/CODIER-MODUS-PIPELINE.md Abschnitt 4.2.
    private static readonly string DamageClassesPromptFull = """
Bestimme ZUERST den view_type, BEVOR du Schaeden codierst:

AXIALSICHT (view_type="axial") — Normalbild fuer Codierung:
- Kamera blickt GERADEAUS in Rohrachse (Fluchtpunkt in Bildmitte)
- Rohrwand ringfoermig um Bildrand sichtbar
- OSD-Meterstand ist KORREKT und entspricht der Kameraposition
- → Schaeden CODIEREN, dies ist das massgebende Bild

NAHAUFNAHME (view_type="nahaufnahme") — Zusatzbild, NICHT codieren:
- Kamera ist NAH an der Rohrwand herangefahren oder geschwenkt
- Schaden fuellt GROSSFLÄCHIG das Bild (>50% der Bildflaeche)
- Rohrquerschnitt NICHT mehr ringfoermig sichtbar
- OSD-Meterstand stimmt NICHT mit der Schadensposition ueberein
- → findings=[], is_empty_frame=true (Nahaufnahme ist NUR ergaenzend)

SCHWENK (view_type="schwenk") — Kamera dreht, NICHT codieren:
- Bild ist VERZERRT oder schraeg, Rohr nicht in Achse
- Kamerabewegung sichtbar (Verwischung, schraeger Blickwinkel)
- Rohrwand nur auf einer Seite sichtbar, andere Seite fehlt
- → findings=[], is_empty_frame=true (waehrend Schwenk NICHT codieren)

SCHACHTAUFNAHME (view_type="schacht") — Kamera noch im Schacht, NICHT Rohr codieren:
- Schachtwand oder Schachtbauwerk sichtbar (Mauerwerk, Beton, Leiter, Gerinne)
- Typisch am ANFANG der Inspektion: Kamera faehrt durch Schacht ins Rohr
- Verbindung Rohr-Schacht sichtbar, Rohroeffnung als dunkler/heller Kreis
- WICHTIG: Was du hier siehst ist SCHACHT, nicht Rohr!
  → Schachtmauerwerk ist KEIN Oberflaechenschaden (BAF)!
  → Schachtwasser ist KEINE Infiltration (BBF)!
  → Schachtstufen/Leiter sind KEIN Hindernis (BBE)!
- → NUR BCD (Rohranfang) oder BCE (Rohrende) codieren, sonst findings=[]
- Die Kamera ist erst IM ROHR wenn:
  → Rohrwand RINGFOERMIG das Bild umgibt
  → Fahrwagen/Kamera KOMPLETT im Rohr ist
  → OSD-Meterstand bei ~0.00m oder hoeher steht

INSPEKTIONS-SEQUENZ (typischer Ablauf):
1. Kamera im Schacht → view_type="schacht", BCD bei 0.00m
2. Kamera faehrt ins Rohr → view_type="axial", Schaeden codieren
3. Bei Schaden: Axialsicht-Foto, dann evtl. Nahaufnahme → view_type="nahaufnahme"
4. Weiterfahrt in Axialsicht → view_type="axial"
5. Rohrende erreicht → view_type="schacht", BCE codieren

TIEFENFILTER — Was die KI NICHT codieren darf:
- Die Kamera sieht 5-10m voraus im Rohr
- Schaeden am FLUCHTPUNKT (Rohrmitte im Bild, weit weg) sind NICHT codierbar
- NUR Schaeden im NAHBEREICH analysieren (Rohrwand nahe der Kamera)
- Der Inspekteur codiert erst wenn er VOR ORT ist, nicht aus der Ferne

STRECKENSCHADEN (ueber >1 Meter):
- Gleicher Schaden ueber mehrere aufeinanderfolgende Frames = EIN Streckenschaden
- NICHT als separate Punktschaeden codieren
- Typische Streckenschaeden: Korrosion (BAFCE), Ablagerung (BBC), Infiltration (BBFA), Verformung (BAA)

ROHRVERBINDUNGEN:
- Schaeden die an einer Muffe/Rohrverbindung auftreten: Char2="A" setzen
- BAJ (Verschobene Rohrverbindung): Uhrlage = Versatzrichtung
  BAJA=breit (Abstand mm), BAJB=versetzt (Versatz mm), BAJC=Knick (Winkel °)
  Breite Rohrverbindungen <15mm NICHT aufzeichnen
- BAJC + BDD = Sank/Unterbogen als Streckenfeststellung

BAA VERFORMUNG — Quantifizierung:
- Prozentuale Reduzierung gegenueber Ursprungsabmessung
- Beispiel: DN300, lichte Hoehe 240mm → Deformation = 20%
- BAAA=vertikal deformiert, BAAB=horizontal deformiert
- Bei biegeweichen Rohren (PE/PVC): zuerst Risse SEPARAT beschreiben
- Druckstellen bei biegeweichen Rohren → BAFK

ABBRUCH-CODIERUNG (BDC):
- BDCAA = Hindernis, Inspektionsziel erreicht
- BDCAB = Hindernis, Auftraggeber verzichtet auf weitere Untersuchung
- BDCAZ = Abbruch Inspektion: Hindernis
- BDCAC = Hindernis, Gegenseite erreicht
- BDCAD = Hindernis, Gegenseite nicht erreicht
- BDB B = Inspektion erst nach Reinigung moeglich
- BDB C = Inspektion erfolgt zu einem spaeteren Zeitpunkt
- BDB F = Inspektion erfolgt von der Gegenseite

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
BCA  Anschluss (BCAAA=Formstueck, BCABA=Sattel gebohrt, BCACA=Sattel eingespitzt, BCADA=gebohrt, BCAEA=eingespitzt, BCAFA=Spezial, BCAGA=unbekannt) — Uhrlage + Durchmesser mm
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
  Fuehrt zu einem Abzweig. severity=1. Code: BCAAA (Formstueck), BCABA (Sattel geb.), BCADA (gebohrt), BCAEA (eingespitzt), BCAGA (unbekannt).
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
    // Phase 0.4: Wenn true wird DamageClassesPromptFull (mit Aufnahmetechnik
    // axial/nahaufnahme/schwenk/schacht-Erkennung) verwendet — fuer
    // Batch-/Video-Pipelines. Codier-Modus bleibt auf der kuerzeren Variante.
    private readonly bool _useFullDamagePrompt;
    // Retry-Throttle: max 2 gleichzeitige Retries (kein VRAM-Risiko mehr, nur CPU/GPU-Contention)
    private readonly SemaphoreSlim _retryThrottle = new(2, 2);
    // 32B Eskalations-Lock: serialisiert parallele 32B-Aufrufe (CLAUDE.md Phase 3.1).
    // 32B laeuft hybrid (~2 GB VRAM + ~20 GB RAM, num_gpu=10) — parallele Calls
    // wuerden Ollama in CPU-Fallback und VRAM/RAM-Stau druecken (~28 s statt ~9 s).
    // STATIC: gilt prozessweit, weil Ollama nur eine 32B-Instanz haelt — selbst wenn
    // mehrere EnhancedVisionAnalysisService-Instanzen leben, darf nur ein 32B-Call
    // gleichzeitig laufen. 8B-Calls bleiben unbeeinflusst.
    private static readonly SemaphoreSlim _escalation32BLock = new(1, 1);
    private int _retryCount;
    // Telemetrie: Retry-Gruende einzeln zaehlen fuer datenbasierte Schwellen-Justierung
    private int _retryAllCodesNull;
    private int _retryHighSeverity;
    private int _retryPoorQuality;
    private int _swap32bCount;
    // Backward-Compat: alte Telemetrie-Namen als Aliases
    private int _escalationCount => _retryCount;
    private FewShotExampleStore? _fewShotStore;
    private readonly object _fewShotLock = new();
    private IReadOnlyList<(FewShotExample Example, string Base64)>? _cachedFewShot;

    public EnhancedVisionAnalysisService(OllamaClient client, string model, string? referenceModel = null, int numCtx = 8192, bool useFullDamagePrompt = false)
    {
        _client = client;
        _model = model;
        _referenceModel = referenceModel;
        _numCtx = numCtx;
        _useFullDamagePrompt = useFullDamagePrompt;
    }

    /// <summary>Phase 0.4: gibt den aktuell aktiven Damage-Klassen-Prompt zurueck (kurz oder voll).</summary>
    private string ActiveDamageClassesPrompt => _useFullDamagePrompt ? DamageClassesPromptFull : DamageClassesPrompt;

    /// <summary>Zugriff auf den OllamaClient (fuer Modell-Vorladen).</summary>
    public OllamaClient Client => _client;
    /// <summary>Name des primaeren Vision-Modells.</summary>
    public string ModelName => _model;

    /// <summary>Anzahl erfolgreicher Retries mit erweitertem Prompt (Telemetrie).</summary>
    public int EscalationCount => _retryCount;
    /// <summary>Retries wegen fehlender VSA-Codes (alle Findings ohne Code).</summary>
    public int EscalationAllCodesNull => _retryAllCodesNull;
    /// <summary>Retries wegen hohem Schweregrad (Severity >= 4).</summary>
    public int EscalationHighSeverity => _retryHighSeverity;
    /// <summary>Retries wegen schlechter Bildqualitaet mit Findings.</summary>
    public int EscalationPoorQuality => _retryPoorQuality;
    /// <summary>Anzahl 32B Swap-Eskalationen (Telemetrie).</summary>
    public int Swap32bCount => _swap32bCount;

    /// <summary>
    /// Aktiviert Few-Shot Learning: Beispielbilder werden in den Prompt injiziert.
    /// Sollte einmal beim Start aufgerufen werden.
    /// </summary>
    public async Task EnableFewShotAsync(FewShotExampleStore store, CancellationToken ct = default)
    {
        _fewShotStore = store;
        await store.LoadAsync(ct);

        // Few-Shot-Budget: weniger Beispiele = schnellere Inferenz
        // 2 diverse Beispiele reichen — mehr fuellt den Kontext und verlangsamt Qwen
        int maxExamples = _numCtx switch
        {
            <= 2048  => 1,
            <= 8192  => 2,
            <= 16384 => 4,
            _        => 6
        };
        // Diverse Auswahl: Erst 1 pro Hauptgruppe (BA/BB/BC), dann auffuellen pro Code
        var allExamples = await store.GetBestExamplesAsync(maxExamples * 5, maxPerMainGroup: 10, ct: ct);
        var diverseExamples = new List<AuswertungPro.Next.Application.Ai.Training.FewShotExample>();
        var usedGroups = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var usedCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Runde 1: 1 Beispiel pro Hauptgruppe (BA, BB, BC)
        foreach (var ex in allExamples)
        {
            if (diverseExamples.Count >= maxExamples) break;
            var group = (ex.VsaCode ?? "").Length >= 2 ? ex.VsaCode![..2] : "";
            if (!string.IsNullOrEmpty(group) && usedGroups.Add(group))
            {
                var code3 = ex.VsaCode!.Length >= 3 ? ex.VsaCode[..3] : ex.VsaCode;
                usedCodes.Add(code3);
                diverseExamples.Add(ex);
            }
        }

        // Runde 2: Auffuellen mit 1 pro 3-Buchstaben Code (z.B. BCC neben BCD)
        foreach (var ex in allExamples)
        {
            if (diverseExamples.Count >= maxExamples) break;
            var code3 = (ex.VsaCode ?? "").Length >= 3 ? ex.VsaCode![..3] : ex.VsaCode ?? "";
            if (!string.IsNullOrEmpty(code3) && usedCodes.Add(code3))
                diverseExamples.Add(ex);
        }

        var examples = (IReadOnlyList<AuswertungPro.Next.Application.Ai.Training.FewShotExample>)diverseExamples;

        // Bilder vorladen und als Base64 cachen (einmal laden, bei jeder Analyse verwenden)
        var loaded = new List<(FewShotExample, string)>();
        foreach (var ex in examples)
        {
            var imgBytes = await store.LoadImageAsync(ex, ct);
            if (imgBytes != null)
                loaded.Add((ex, Convert.ToBase64String(imgBytes)));
        }

        lock (_fewShotLock) { _cachedFewShot = loaded; }

        // Instrumentation: Few-Shot-Inhalt sichtbar machen
        var fewShotCodes = string.Join(", ", loaded.Select(l => l.Item1.VsaCode));
        var fewShotLog = $"[FewShot] {loaded.Count}/{maxExamples} Beispiele geladen: {fewShotCodes}";
        System.Diagnostics.Debug.WriteLine(fewShotLog);
        FewShotDiagnostics = fewShotLog;
    }

    /// <summary>Diagnostik: Welche Few-Shot-Beispiele sind geladen (fuer UI/Logging).</summary>
    public string? FewShotDiagnostics { get; private set; }

    public async Task<EnhancedFrameAnalysis> AnalyzeAsync(
        string framePngBase64,
        CancellationToken ct = default)
    {
        LastPipelineWarning = null;
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
        catch (Exception ex) when (IsStructuredJsonParseFailure(ex))
        {
            // Phase 3.2: Unstrukturierte Ollama-Antwort → poorQuality-Fallback,
            // statt den ganzen Batch zu crashen.
            return PoorQualityFallback("Frame-Analyse", _model, ex);
        }
        catch (Exception ex)
        {
            return FailAnalysis("Frame-Analyse", _model, ex);
        }

        // Rohoutput-Logging: Qwen-Antwort VOR allen Filtern speichern
        var rawFindingsCount = dto.Findings?.Count ?? 0;
        var rawViewType = dto.ViewType ?? "null";
        LastRawOutput = $"[RawQwen] meter={dto.Meter}, view_type={rawViewType}, " +
            $"findings={rawFindingsCount}, quality={dto.ImageQuality}, empty={dto.IsEmptyFrame}";
        if (rawFindingsCount > 0)
        {
            var rawCodes = string.Join(", ", dto.Findings!.Select(f =>
                $"{f.Label}(s{f.Severity})"));
            LastRawOutput += $" | codes: {rawCodes}";
        }
        System.Diagnostics.Debug.WriteLine(LastRawOutput);

        var result = MapToAnalysis(dto);

        // Post-Filter-Logging: Was wurde gefiltert?
        var filteredCount = result.Findings.Count;
        if (filteredCount != rawFindingsCount)
        {
            var filterLog = $"[PostFilter] {rawFindingsCount} roh → {filteredCount} nach Filter (view_type={result.ViewType})";
            LastFilterLog = filterLog;
            System.Diagnostics.Debug.WriteLine(filterLog);
        }
        else
        {
            LastFilterLog = null;
        }

        return result;
    }

    /// <summary>Diagnostik: Letzte Qwen-Rohantwort (vor Filtern).</summary>
    public string? LastRawOutput { get; private set; }

    /// <summary>Letzte nicht-fatale Pipeline-Warnung, z.B. fehlgeschlagener Retry/Fallback.</summary>
    public string? LastPipelineWarning { get; private set; }

    /// <summary>Diagnostik: Was wurde durch Filter entfernt?</summary>
    public string? LastFilterLog { get; private set; }

    /// <summary>Diagnostik: Welche Findings wurden wegen ViewType unterdrueckt?</summary>
    public static string? LastSuppressedLog { get; private set; }

    /// <summary>
    /// Globales Event fuer alle Pipeline-Fehler. Wird von ALLEN Failure-Pfaden im
    /// EnhancedVisionAnalysisService gefeuert (Frame-Analyse, PDF-Foto, Kontext-Analyse,
    /// Modell-Eskalation, 32B-Swap, VerifyCode). Konsumenten (z.B. CodingModeWindow,
    /// PlayerWindow) koennen subscriben um die User-sichtbare Fehlermeldung zu zeigen.
    /// </summary>
    public static event EventHandler<PipelineFailureEvent>? PipelineFailure;

    public sealed record PipelineFailureEvent(
        string Stage,
        string Model,
        string ExceptionType,
        string Message,
        DateTimeOffset At);

    private EnhancedFrameAnalysis FailAnalysis(string stage, string model, Exception ex)
    {
        LastPipelineWarning = null;
        var message = $"{stage} ({model}) fehlgeschlagen: {ex.GetType().Name}: {ex.Message}";
        System.Diagnostics.Debug.WriteLine($"[EnhancedVision] {message}");
        try
        {
            PipelineFailure?.Invoke(this, new PipelineFailureEvent(
                Stage: stage,
                Model: model,
                ExceptionType: ex.GetType().Name,
                Message: ex.Message,
                At: DateTimeOffset.Now));
        }
        catch { /* Event-Handler-Fehler nicht propagieren */ }
        return EnhancedFrameAnalysis.Empty(message);
    }

    /// <summary>
    /// Phase 3.2: Erkennt unstrukturierte/korrupte Ollama-Antworten, die nicht
    /// ins erwartete JSON-Schema passen. Solche Faelle sollen nicht den ganzen
    /// Batchlauf killen — sie werden als poorQuality-Frame markiert und der Lauf
    /// laeuft weiter. CancellationToken-Faelle (OperationCanceledException)
    /// werden hier NICHT erkannt, die muessen weiter propagieren.
    /// </summary>
    private static bool IsStructuredJsonParseFailure(Exception ex)
    {
        if (ex is JsonException) return true;
        // OllamaClient.ChatStructuredWithOptionsAsync wirft InvalidOperationException
        // mit Nachricht "Structured JSON konnte nicht geparst werden: ..." bei
        // Deserialisierungs-Fehlern. Der "JSON konnte nicht geparst"-Match deckt
        // beide bekannten Varianten ab (Structured JSON / JSON konnte nicht geparst).
        if (ex is InvalidOperationException
            && !string.IsNullOrEmpty(ex.Message)
            && ex.Message.Contains("JSON konnte nicht geparst", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
        return false;
    }

    /// <summary>
    /// Phase 3.2: Konservativer Fallback fuer unstrukturierte Ollama-Antworten.
    /// Liefert ein leeres EnhancedFrameAnalysis mit ImageQuality="schlecht" und
    /// IsEmptyFrame=true (siehe EnhancedFrameAnalysis.Empty), damit das Frame
    /// als poorQuality klassifiziert wird statt den Batch zu crashen. Logging
    /// erfolgt als Warning ueber Debug.WriteLine + PipelineFailure-Event,
    /// damit Drift sichtbar bleibt.
    /// </summary>
    private EnhancedFrameAnalysis PoorQualityFallback(string stage, string model, Exception ex)
    {
        var marker = $"PoorQuality: {stage} ({model}) lieferte unstrukturierte Antwort " +
                     $"({ex.GetType().Name}): {ex.Message}";
        // Als Warning loggen — kein swallow, damit der Drift in den Logs sichtbar bleibt
        System.Diagnostics.Debug.WriteLine($"[EnhancedVision][WARN] {marker}");
        SetPipelineWarning(marker);
        try
        {
            PipelineFailure?.Invoke(this, new PipelineFailureEvent(
                Stage: stage + " (PoorQuality-Fallback)",
                Model: model,
                ExceptionType: ex.GetType().Name,
                Message: ex.Message,
                At: DateTimeOffset.Now));
        }
        catch { /* Event-Handler-Fehler nicht propagieren */ }
        // EnhancedFrameAnalysis.Empty setzt ImageQuality="schlecht", IsEmptyFrame=true,
        // Findings=leer — genau das gewuenschte poorQuality-Verhalten.
        return EnhancedFrameAnalysis.Empty(marker);
    }

    private void SetPipelineWarning(string message)
    {
        LastPipelineWarning = message;
        System.Diagnostics.Debug.WriteLine($"[EnhancedVision] {message}");
    }

    /// <summary>
    /// Analysiert ein Foto aus einem PDF-Bildbericht (KEIN Video-Frame).
    /// Angepasster Prompt ohne OSD-Erwartung, fokussiert auf Schadenserkennung.
    /// </summary>
    public async Task<EnhancedFrameAnalysis> AnalyzePdfPhotoAsync(
        string framePngBase64,
        CancellationToken ct = default)
    {
        LastPipelineWarning = null;
        var messages = new List<OllamaClient.ChatMessage>();

        // Few-Shot Beispiele auch fuer PDF-Fotos nutzen
        var fewShot = _cachedFewShot;
        if (fewShot is { Count: > 0 })
        {
            foreach (var (example, b64) in fewShot)
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
        catch (Exception ex) when (IsStructuredJsonParseFailure(ex))
        {
            // Phase 3.2: Unstrukturierte Ollama-Antwort → poorQuality-Fallback.
            return PoorQualityFallback("PDF-Foto-Analyse", _model, ex);
        }
        catch (Exception ex)
        {
            return FailAnalysis("PDF-Foto-Analyse", _model, ex);
        }

        return MapToAnalysis(dto);
    }

    /// <summary>
    /// Baut die Chat-Messages auf, mit Few-Shot Beispielen falls vorhanden.
    /// </summary>
    private IReadOnlyList<OllamaClient.ChatMessage> BuildMessages(string framePngBase64)
    {
        var messages = new List<OllamaClient.ChatMessage>();

        // Few-Shot DEAKTIVIERT: Tests zeigen dass Few-Shot-Bilder die Erkennung VERSCHLECHTERN.
        // Mit Few-Shot: findings=[] (Qwen denkt "schon gesehen"). Ohne: korrekte Erkennung.
        // Rohoutput-Tests: Test2 (nur Prompt) erkennt BCD, Test3 (Prompt+FewShot) erkennt NICHTS.
        // TODO: Few-Shot-Format ueberarbeiten wenn Qwen3-VL oder groesseres Modell verfuegbar.
        // var fewShot = _cachedFewShot;
        // Few-Shots werden geladen aber NICHT in die Messages injiziert.

        // Eigentliche Analyse-Anfrage
        messages.Add(new OllamaClient.ChatMessage(
            Role: "user",
            Content: BuildPrompt(),
            ImagesBase64: [framePngBase64]));

        return messages;
    }

    /// <summary>Baut eine synthetische "korrekte" Assistant-Antwort fuer ein Few-Shot Beispiel.</summary>


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
        LastPipelineWarning = null;
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
        catch (Exception ex) when (IsStructuredJsonParseFailure(ex))
        {
            // Phase 3.2: Unstrukturierte Ollama-Antwort → poorQuality-Fallback.
            return PoorQualityFallback("Kontextanalyse", _model, ex);
        }
        catch (Exception ex)
        {
            return FailAnalysis("Kontextanalyse", _model, ex);
        }

        return MapToAnalysis(dto);
    }

    public async Task<EnhancedFrameAnalysis> AnalyzeWithFastModelAsync(
        string framePngBase64,
        MultiModelFrameResult multiModelContext,
        int pipeDiameterMm = 300,
        CancellationToken ct = default)
    {
        if (string.Equals(_model, OllamaConfig.DefaultVisionModel, StringComparison.OrdinalIgnoreCase))
            return await AnalyzeWithContextAsync(framePngBase64, multiModelContext, pipeDiameterMm, ct).ConfigureAwait(false);

        return await AnalyzeWithModelAsync(
            OllamaConfig.DefaultVisionModel,
            framePngBase64,
            multiModelContext,
            pipeDiameterMm,
            ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Analysiert mit 8B. Wenn die Erkennung unsicher ist (Yellow/Red),
    /// wird ein Same-Model-Retry mit erweitertem Prompt durchgefuehrt.
    /// Kein Modellwechsel, kein VRAM-Swap — alles im gleichen 8B-Slot.
    /// </summary>
    public async Task<(EnhancedFrameAnalysis Result, bool Escalated)> AnalyzeWithEscalationAsync(
        string framePngBase64,
        MultiModelFrameResult? context,
        int pipeDiameterMm = 300,
        CancellationToken ct = default)
    {
        // 1. Analyse mit 8B (einziges Modell, kein Modellwechsel)
        var first = context != null
            ? await AnalyzeWithContextAsync(framePngBase64, context, pipeDiameterMm, ct).ConfigureAwait(false)
            : await AnalyzeAsync(framePngBase64, ct).ConfigureAwait(false);

        var reason = GetEscalationReason(first);
        if (reason == EscalationReason.None)
            return (first, false);

        // Telemetrie: Retry-Grund zaehlen
        switch (reason)
        {
            case EscalationReason.NoFindings: Interlocked.Increment(ref _retryAllCodesNull); break;
            case EscalationReason.AllCodesNull: Interlocked.Increment(ref _retryAllCodesNull); break;
            case EscalationReason.HighSeverity: Interlocked.Increment(ref _retryHighSeverity); break;
            case EscalationReason.PoorQuality: Interlocked.Increment(ref _retryPoorQuality); break;
        }

        // 2. Same-Model-Retry mit erweitertem Prompt (kein VRAM-Swap noetig!)
        //    Throttle: max 2 gleichzeitige Retries um GPU-Contention zu begrenzen
        if (!await _retryThrottle.WaitAsync(TimeSpan.FromSeconds(30), ct).ConfigureAwait(false))
        {
            System.Diagnostics.Debug.WriteLine(
                "[EnhancedVision] Retry uebersprungen — Throttle-Timeout (zu viele parallele Retries)");
            return (first, false);
        }
        try
        {
            // Erweiterter Retry: gleicher 8B-Slot, aber mit expliziterem Prompt
            var retryResult = await RetryWithEnhancedPromptAsync(
                framePngBase64, first, context, pipeDiameterMm, reason, ct).ConfigureAwait(false);

            Interlocked.Increment(ref _retryCount);
            System.Diagnostics.Debug.WriteLine(
                $"[EnhancedVision] Retry #{_retryCount} ({reason}): " +
                $"{retryResult.Findings.Count} Findings, Quality={retryResult.ImageQuality}");

            // 3. Optional: 32B Swap-Eskalation wenn Same-Model-Retry nicht gereicht hat
            if (!string.IsNullOrEmpty(_referenceModel)
                && !string.Equals(_referenceModel, _model, StringComparison.OrdinalIgnoreCase))
            {
                var retryReason = GetEscalationReason(retryResult);
                if (retryReason is EscalationReason.NoFindings or EscalationReason.AllCodesNull or EscalationReason.HighSeverity)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[EnhancedVision] Same-Model-Retry unzureichend ({retryReason}) — starte 32B Swap-Eskalation");
                    // Serialisierung: nur ein 32B-Call gleichzeitig (CLAUDE.md Phase 3.1).
                    // WaitAsync wirft OperationCanceledException bei ct → propagiert nach oben.
                    await _escalation32BLock.WaitAsync(ct).ConfigureAwait(false);
                    try
                    {
                        var swapResult = await AnalyzeWithModelAsync(_referenceModel, framePngBase64, ct)
                            .ConfigureAwait(false);
                        Interlocked.Increment(ref _swap32bCount);
                        System.Diagnostics.Debug.WriteLine(
                            $"[EnhancedVision] 32B Swap #{_swap32bCount}: {swapResult.Findings.Count} Findings");
                        return (swapResult, true);
                    }
                    catch (OperationCanceledException) { throw; }
                    catch (Exception ex32b)
                    {
                        SetPipelineWarning(
                            $"32B Swap fehlgeschlagen ({_referenceModel}): {ex32b.GetType().Name}: {ex32b.Message} - verwende Retry-Ergebnis");
                        try { PipelineFailure?.Invoke(this, new PipelineFailureEvent(
                            "32B-Eskalation", _referenceModel,
                            ex32b.GetType().Name, ex32b.Message, DateTimeOffset.Now)); } catch { }
                    }
                    finally
                    {
                        _escalation32BLock.Release();
                    }
                }
            }

            return (retryResult, true);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            // Retry fehlgeschlagen → Erst-Ergebnis als Fallback
            SetPipelineWarning(
                $"Retry fehlgeschlagen ({ex.GetType().Name}): {ex.Message} - verwende Erst-Ergebnis");
            return (first, false);
        }
        finally
        {
            _retryThrottle.Release();
        }
    }

    /// <summary>
    /// Same-Model-Retry: gleicher 8B-Slot, erweiterter Prompt je nach Fehlergrund.
    /// Nutzt 8192-Kontext um mehr Few-Shots und explizitere Anweisungen zu packen.
    /// </summary>
    private async Task<EnhancedFrameAnalysis> RetryWithEnhancedPromptAsync(
        string framePngBase64,
        EnhancedFrameAnalysis firstResult,
        MultiModelFrameResult? context,
        int pipeDiameterMm,
        EscalationReason reason,
        CancellationToken ct)
    {
        var messages = new List<OllamaClient.ChatMessage>();

        // Few-Shot Beispiele injizieren (8192 ctx erlaubt mehr Beispiele)
        var fewShot = _cachedFewShot;
        if (fewShot is { Count: > 0 })
        {
            foreach (var (example, b64) in fewShot)
            {
                var exPrompt = $"Analysiere dieses Kanalbild. " +
                    $"Hinweis: Dieses Bild zeigt {example.Description}" +
                    (example.ClockPosition != null ? $" bei {example.ClockPosition}" : "") +
                    $" (VSA-Code: {example.VsaCode}).";

                messages.Add(new OllamaClient.ChatMessage(
                    Role: "user", Content: exPrompt, ImagesBase64: [b64]));
                messages.Add(new OllamaClient.ChatMessage(
                    Role: "assistant", Content: BuildFewShotResponse(example)));
            }
        }

        // Erweiterter Prompt basierend auf dem Fehlergrund
        var enhancedHint = reason switch
        {
            EscalationReason.AllCodesNull =>
                "\nWICHTIG: Der erste Versuch konnte keinen VSA-Code zuordnen. " +
                "Pruefe NOCHMAL sorgfaeltig: Rohranfang (BCD), Rohrende (BCE), Anschluss (BCA), " +
                "Bogen (BCC), Riss (BAB), Bruch (BAC), Wurzeln (BBA), Ablagerungen (BBC). " +
                "JEDER Befund MUSS einen gültigen VSA-Code haben.",
            EscalationReason.HighSeverity =>
                "\nWICHTIG: Hoher Schweregrad erkannt. Pruefe NOCHMAL sorgfaeltig: " +
                "Ist es wirklich severity 4-5? Verwechslung mit Rohranfang (BCD, severity=1) ausschliessen. " +
                "Quantifiziere GENAU: Ausdehnung %, Querschnittsverringerung %, Uhrlage.",
            EscalationReason.PoorQuality =>
                "\nWICHTIG: Schlechte Bildqualitaet. Beschreibe NUR was SICHER erkennbar ist. " +
                "Im Zweifel: severity NICHT uebertreiben, lieber konservativ bewerten.",
            _ => ""
        };

        var prompt = BuildPrompt() + enhancedHint;

        // Context von YOLO/DINO mitgeben falls vorhanden
        if (context != null)
        {
            var contextPrompt = BuildContextPrompt(context, pipeDiameterMm);
            prompt = contextPrompt + "\n\n" + prompt;
        }

        messages.Add(new OllamaClient.ChatMessage(
            Role: "user", Content: prompt, ImagesBase64: [framePngBase64]));

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
        catch (Exception ex) when (IsStructuredJsonParseFailure(ex))
        {
            // Phase 3.2: Unstrukturierte Ollama-Antwort beim Retry → poorQuality-Fallback,
            // statt die Exception bis in den Batch hochpropagieren zu lassen.
            return PoorQualityFallback("Retry mit erweitertem Prompt", _model, ex);
        }

        return MapToAnalysis(dto);
    }

    /// <summary>Grund fuer die Eskalation (fuer Telemetrie).</summary>
    private enum EscalationReason { None, NoFindings, AllCodesNull, HighSeverity, PoorQuality }

    /// <summary>
    /// Prueft ob eine Eskalation zum Reference-Modell noetig ist.
    /// Gibt den konkreten Grund zurueck (fuer Telemetrie-Zaehler).
    /// </summary>
    private static EscalationReason GetEscalationReason(EnhancedFrameAnalysis fast)
    {
        if (fast.IsEmptyFrame) return EscalationReason.None;

        // Kein leerer Frame, aber keine Findings: fuer BBox-/Einzelframe-Analyse kritisch.
        if (!fast.HasFindings) return EscalationReason.NoFindings;

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
    /// Analysiert mit dem Reference-Modell (32B, komplett RAM mit num_gpu=0).
    /// Kein VRAM-Konflikt — 32B laeuft permanent im RAM neben 8B auf GPU.
    /// </summary>
    private async Task<EnhancedFrameAnalysis> AnalyzeWithModelAsync(
        string model,
        string framePngBase64,
        CancellationToken ct)
    {
        var messages = BuildMessages(framePngBase64);
        try
        {
            // num_gpu=10: hybrid GPU/RAM (~9s statt 28s, CPU-Last sinkt deutlich)
            var referenceOptions = new Dictionary<string, object> { ["num_gpu"] = 10 };
            var dto = await _client.ChatStructuredWithOptionsAsync<EnhancedVisionDto>(
                model: model,
                messages: messages,
                formatSchema: EnhancedVisionSchema,
                options: referenceOptions,
                ct: ct).ConfigureAwait(false);
            return MapToAnalysis(dto);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) when (IsStructuredJsonParseFailure(ex))
        {
            // Phase 3.2: Unstrukturierte Ollama-Antwort vom Reference-Modell → poorQuality-Fallback.
            return PoorQualityFallback("Modell-Eskalation", model, ex);
        }
        catch (Exception ex)
        {
            return FailAnalysis("Modell-Eskalation", model, ex);
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
        catch (Exception ex) when (IsStructuredJsonParseFailure(ex))
        {
            // Phase 3.2: Unstrukturierte Ollama-Antwort vom Reference-Modell → poorQuality-Fallback.
            return PoorQualityFallback("Modell-Eskalation mit Kontext", model, ex);
        }
        catch (Exception ex)
        {
            return FailAnalysis("Modell-Eskalation mit Kontext", model, ex);
        }
    }


    // ── DTOs (für JSON-Deserialisierung) ──────────────────────────────────────


    // ── V4.2 Phase 2.3: Protokoll-First Verifikation ─────────────────────────

    /// <summary>
    /// Minimales JSON-Schema fuer gerichtete Verifikation (Yes/No-Aufgabe).
    /// Drei Felder statt zwanzig — Qwen kann nicht mehr auf BCC kollabieren.
    /// </summary>
    private static readonly JsonElement VerificationSchema = JsonDocument.Parse("""
    {
      "type": "object",
      "additionalProperties": false,
      "properties": {
        "visible": { "type": "boolean" },
        "severity": { "type": ["integer", "null"], "minimum": 1, "maximum": 5 },
        "confidence": { "type": "number", "minimum": 0.0, "maximum": 1.0 },
        "notes": { "type": ["string", "null"] }
      },
      "required": ["visible", "confidence"]
    }
    """).RootElement.Clone();

    private sealed record VerificationDto(
        [property: JsonPropertyName("visible")] bool Visible,
        [property: JsonPropertyName("severity")] int? Severity,
        [property: JsonPropertyName("confidence")] double Confidence,
        [property: JsonPropertyName("notes")] string? Notes);

    /// <summary>
    /// V4.2 Phase 2: Gerichtete Verifikation eines einzelnen Protokoll-Eintrags.
    /// Fragt Qwen: "Ist VSA-Code {code} bei Meter {meter} in diesem Frame sichtbar?"
    /// Geschlossene Ja/Nein-Frage verhindert den BCC-Kollaps der Open-Set-Klassifikation.
    /// </summary>
    public async Task<AuswertungPro.Next.Application.Ai.Vision.DamageVerification> VerifyCodeAsync(
        string framePngBase64,
        string vsaCode,
        double meter,
        string? description = null,
        CancellationToken ct = default)
    {
        var prompt =
            $"Kanalinspektion-Frame: Pruefe gezielt, ob der VSA-Code {vsaCode} bei ca. Meter {meter:F1} im Bild sichtbar ist.\n" +
            (string.IsNullOrWhiteSpace(description) ? "" : $"Hinweis aus Protokoll: {description}\n") +
            "\nAntworte NUR in JSON:\n" +
            "{\n" +
            "  \"visible\": true/false,\n" +
            "  \"severity\": 1-5 (oder null wenn nicht sichtbar),\n" +
            "  \"confidence\": 0.0-1.0 (deine Sicherheit),\n" +
            "  \"notes\": kurze Begruendung auf Deutsch\n" +
            "}\n" +
            "\nBewerte NUR diese eine Frage. Gib keine anderen Codes zurueck.";

        try
        {
            var dto = await _client.ChatStructuredAsync<VerificationDto>(
                model: _model,
                messages:
                [
                    new OllamaClient.ChatMessage(
                        Role: "user",
                        Content: prompt,
                        ImagesBase64: [framePngBase64])
                ],
                formatSchema: VerificationSchema,
                ct: ct).ConfigureAwait(false);

            return new AuswertungPro.Next.Application.Ai.Vision.DamageVerification(
                Visible: dto.Visible,
                Severity: dto.Severity,
                Confidence: Math.Clamp(dto.Confidence, 0.0, 1.0),
                Notes: dto.Notes);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[VerifyCode] Fehler fuer {vsaCode}@{meter:F1}m: {ex.Message}");
            return new AuswertungPro.Next.Application.Ai.Vision.DamageVerification(false, null, 0.0, $"error: {ex.Message}");
        }
    }
}

// ── Analysis result types ─────────────────────────────────────────────────────
// Phase 5.3 vorbereitend: EnhancedFrameAnalysis + EnhancedFinding
// nach Domain/Ai/Vision/EnhancedFrameAnalysis.cs verschoben.
