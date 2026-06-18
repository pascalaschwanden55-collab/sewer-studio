# Spezifikation: Logik der KI-Analyse im Codiermodus

**Datum:** 2026-06-16
**Status:** Lebendige Spezifikation (Teil 1-7 + 11 umgesetzt/in Arbeit; 8-10 geplant)
**Grundlage:** User-Vorgaben 2026-06-16 (Overlay aufgeraeumt, Naehe-Gate verschaerft, BCE-Plausibilitaet, Streckenschaden, Uhrlage, Quantifizierung).
**Fachliche Referenz:** VSA-Richtlinie "Schadencodierung und Datentransfer" (SN EN 13508-2 konform).
Aus der Richtlinie werden NUR die **Anwendungsregeln** uebernommen, nicht die Code-Listen — die
Codes sind allein in `vsa_kek_2020_catalog_manifest.json` definiert (ADR-006, Single Source of Truth).

Diese Spezifikation beschreibt den vollstaendigen Entscheidungsablauf, wenn im Codiermodus ein
Frame analysiert wird ("Aktuellen Frame analysieren" bzw. Auto-KI-Analyse). Sie trennt sauber:
**[IST]** = im Code vorhanden · **[NEU 2026-06-16]** = heute geaendert · **[OFFEN]** = noch zu bauen.

---

## 0. Begriffe (kurz)

- **DN-Kreis / Referenz-DN:** Der gestrichelte Kreis im Bild = der kalibrierte Rohrdurchmesser
  (`Calibration.NormalizedDiameter`), zentriert auf den Fluchtpunkt (`Calibration.PipeCenter`).
  Er ist die geometrische Messlatte fuer "wie nah ist ein Ereignis".
- **Fluchtpunkt:** Die Rohrmitte am Tunnelende (dunkler Punkt bei Axialsicht).
- **Kamera-Position / Meter:** Der aktuelle Meterstand, primaer aus dem OSD gelesen, sonst aus
  linearer Schaetzung (Videoposition x Haltungslaenge). Quelle wird als "OSD" oder "geschaetzt" markiert.
- **codierbar / Voraus:** Ergebnis des Naehe-Gates (`MetrierungProximity`). Nur `codierbar`-Befunde
  werden protokolliert.

---

## 1. Reihenfolge der Analyse (Pipeline pro Frame) — [IST]

Eintritt: `RunCodingAnalysisAsync` (PlayerWindow.Coding.cs).

1. **Terminal-Stopp-Pruefung** (`IsCodingAfterTerminalBoundary`):
   Ist bereits ein gueltiges Rohrende (BCE) oder ein Abbruch (BDC) gesetzt UND die Kamera dort/dahinter,
   wird die Analyse abgebrochen ("Rohrende erreicht - KI-Analyse gestoppt"). Es wird nichts mehr
   protokolliert. → Regelt Punkt 5 (zu frueh gesetztes BCE darf das nicht ausloesen).
2. **Snapshot** des aktuellen VLC-Frames (`CaptureSnapshotAsync`). Dieser exakte Frame wird fuer
   Gold-Training und Foto am Befund festgehalten (`_detectionPendingFrameBytes`).
3. **OSD-Meter lesen** aus genau diesem Frame (`TryReadAnalyzedFrameOsdMeterAsync`) → Kamera-Position.
4. **Modell-Kette (Multi-Model-Pfad):**
   - **YOLO** — grobe Detektion (Boxen), liefert hoechste Box-Confidence des Frames.
   - **Grounding DINO** — benennt die Detektion (Label).
   - **SAM** — segmentiert die genaue Maske (grüne Kontur).
   - **Klassifikator** — prueft zusaetzlich auf Steuercodes BCD (Rohranfang) / BCE (Rohrende).
   - **Quantifizierung** (`MaskQuantificationService`) — Hoehe/Breite/%/Uhrlage aus der Maske.
   - **Qwen (optional)** — Vision-LLM-Textbefunde (eigener Pfad `AddAiFindingsAsEvents`).
5. **Naehe-Gate** pro Befund (Abschnitt 3).
6. **Rendering** (Abschnitt 4) + **Event-Erzeugung** nur fuer codierbare Befunde (Abschnitt 6).

> Architektur: C# steuert Geschaeftslogik/Dedup/QualityGate/Persistenz (Thin-AI). Der Sidecar liefert
> YOLO/DINO/SAM ueber HTTP. Kein Frame-Tracking in HEAD — das Naehe-Gate arbeitet rein relativ (DN-Kreis).

---

## 2. Steuercodes BCD / BCE / BDC — [IST + NEU]

### VSA-Bedeutung (PDF, unveraenderlich)
- **BCD = Rohranfang**, **BCE = Rohrende**, **BDC = Abbruch der Inspektion**. Mehr bedeuten diese
  Codes nicht. Sie sind **Einmal-Codes pro Haltung** (`CodingDedupPolicy.IsOneTimeCode`); Duplikate
  werden verworfen.
- Sie sind **Pflicht** und vom Naehe-Gate (Abschnitt 3) ausgenommen — ein Rohrende darf nicht
  "weggemerkt" werden.
- **Distanz-Bezugspunkt** (VSA 2.1.1): Der **Rohranfang ist der Nullpunkt** der Distanzmessung.
- **BCDXP / BCEXP (Distanzmessung Anfang/Ende):** Wenn der Bezugspunkt der Laengsmessung **nicht**
  dem Rohranfang bzw. das Inspektionsende **nicht** dem Rohrende entspricht (z.B. Messung beginnt im
  Schacht vor dem Rohr), ist zusaetzlich `BCDXP` bzw. `BCEXP` zu setzen. → [OFFEN] noch nicht
  automatisiert; aktuell Annahme "Nullpunkt = Rohranfang".
- **BDC Abbruch:** Wird der Abbruch durch ein **Hindernis oder einen Schaden** verursacht, muss
  dieser Grund **vorher separat codiert** werden (z.B. erst `BBCC` Harte Ablagerung, dann `BDC..`).
  → [OFFEN] noch nicht erzwungen.

### BCE-Plausibilitaet — [NEU 2026-06-16] — KEINE VSA-Regel, sondern unsere Plausibilitaetspruefung
> Klarstellung: Die VSA-PDF kennt keine "20 cm / 90 %"-Regel. BCE heisst dort nur "Rohrende".
> Das Folgende ist eine **technische Plausibilitaetspruefung der KI**, damit der Klassifikator das
> dunkle Tunnelende am Fluchtpunkt nicht faelschlich als Rohrende setzt (was sonst alle weitere
> Protokollierung stoppt).

Regel: **BCE nur akzeptieren, wenn ein tatsaechliches Rohrende plausibel erkannt wurde UND die
Positionsregel erfuellt ist.** Positionsregel: Kamera **innerhalb der letzten 0.20 m ODER ab 90 %
der bekannten Haltungslaenge** (`EndMeter`); es gilt die frueher erreichte Schwelle.
- Ist `EndMeter` unbekannt (kein Import/Stammdaten) → BCE wie bisher akzeptieren (sonst entstuende
  evtl. gar kein Rohrende).
- Kommt BCE zu frueh → **verworfen**, Status "Mögliches Rohrende voraus - noch nicht am Ende",
  normal weiteranalysieren (kein Stopp).
- Logik: `CodingDedupPolicy.IsBoundaryEndCodePlausible(code, currentMeter, endMeter)`.
- (Die frueher in der TXT genannte "~2 m"-Toleranz ist **ungueltig** — verbindlich ist 0.20 m / 90 %.)

---

## 3. Naehe-Gate (DN-Kreis-Regel) — [NEU 2026-06-16] Kernregel

**Fachregel des Inspekteurs:** Die scheinbare Groesse eines Ereignisses gibt die Distanz. Ein
Ereignis darf weit voraus **erkannt**, aber erst bei **Naehe** metriert/codiert werden. Erst dann
stimmt die Distanz.

Konkrete geometrische Regel (`MetrierungProximityEvaluator.Evaluate`):
- Bezug ist der Fluchtpunkt; Distanzen in Einheiten des Rohrradius (`outerR = 1.0` = am DN-Kreis).
- **codierbar**, wenn:
  1. der Befund **querschnittsfuellend** ist (Boxhoehe >= 70 % Bildhoehe) **und** die Rohrwand/den
     Bildrand beruehrt (grosse Muffe direkt vor der Kamera), ODER
  2. die **aeusserste Box-Ecke den DN-Kreis nach aussen ueberschreitet** (`outerR >= 1.0 - Toleranz`),
     d.h. der Befund reicht in den Ring zwischen DN-Kreis und Bildrand.
- **Voraus** (= nur intern merken, nicht codieren, nicht zeichnen), wenn der Befund **ganz innerhalb
  des DN-Kreises** liegt (klein, Richtung Tunnel/Fluchtpunkt).
- Grundhaltung: **konservativ** — im Zweifel "Voraus".

Diese Regel gilt in **beiden** Befund-Pfaden:
- Multi-Model/SAM: nur `IsCodierbar`-Befunde gehen in `AddMultiModelFindingsAsEvents`
  (Filter `BuildVisibleCodingFindings`). — [IST/NEU]
- Qwen/Enhanced: neues Gate `IsFindingTooFarAhead(finding)` vor `AddEvent`
  (per Bbox + Kalibrierung; ohne verwertbare Bbox kein Block; BCD/BCE ausgenommen). — [NEU 2026-06-16]

---

## 4. Overlay-Darstellung (aufgeraeumt) — [NEU 2026-06-16]

Ziel: nicht ueberladen. Pro Frame werden gezeichnet:
- **Eck-Marker** statt grosser YOLO-Vollboxen (vier dezente L-Ecken an der Bbox;
  `AddDetectionCornerMarkers`). Das klickbare Label-Badge (Klick = Code zuweisen) bleibt.
- **SAM-Konturen** (grün) **nur fuer codierbare** (nahe) Befunde.
- **DN-Kreis** (Referenz-DN) + Label "Ref: DN xxx" — bleibt immer sichtbar.
- **Voraus-Befunde:** werden **gar nicht gezeichnet** (weder Kontur noch frueheres oranges
  "voraus"-Kaestchen) — nur Status "Ereignis voraus erkannt - näher heranfahren".
- Hintergrundmasken (Wasserwand, Rohrwand, OSD) werden per WinCan-Policy ausgeblendet
  (`SamMaskRenderer.DecideVisualMode` → Hidden), Anzahl wird gemeldet.

---

## 5. Feststellung an einer Rohrverbindung (Kennung "A") — [OFFEN]

**VSA-Anwendungsregel (2.1.7):** Tritt eine Feststellung **an einer Rohrverbindung** auf (zwischen
zwei angrenzenden Rohren oder zwischen Rohr und Schacht), muss dies mit der Kennung **"A"** markiert
werden. Im VSA-DSS-Datenmodell ist das das Attribut `Verbindung = ja` der Klasse Kanalschaden
(entspricht SN EN 13508 "A").

**Soll-Verhalten:** Liegt ein Befund erkennbar auf einer Muffe/Rohrverbindung (z.B. versetzte
Verbindung BAJ, einragendes Dichtungsmaterial BAI, Riss an der Muffe), setzt die KI das
Verbindungs-Kennzeichen.

**Ist-Stand:** `ProtocolEntry` hat aktuell **kein** Verbindungs-Feld (nur `IsStreckenschaden`).
→ [OFFEN] Feld ergaenzen + automatische Erkennung (Befund-Naehe zu einer Muffe).

---

## 6. Event-Erzeugung, Dedup, QualityGate — [IST]

Fuer jeden codierbaren Befund:
1. **VSA-Code aufloesen** (gemeinsamer Resolver; Code muss im Katalog gueltig sein, sonst verworfen).
2. **Einmal-Code-Dedup** (BCD/BCE/BDC nur einmal pro Haltung; gegen Session- und VM-Events).
3. **Deckungs-Dedup** (`IsAlreadyCovered`): gleicher Haupt-Code am gleichen Meter (±Toleranz) bzw.
   innerhalb eines offenen Streckenschadens → kein Duplikat.
4. **QualityGate** (`QualityGateService.Evaluate`): EvidenceVector (YOLO/DINO/SAM/Qwen/Plausibilitaet)
   → Ampel Green/Yellow/Red. Green erfordert mindestens zwei Evidenzsignale.
5. **Foto anheften** (exakt der analysierte Frame) **vor** AddEvent.
6. **AddEvent** mit `Source = Ai`, Meter aus dem analysierten Frame (OSD bevorzugt, sonst geschaetzt;
   "geschaetzt" wird in CodeMeta markiert).

---

## 7. Bestaetigung durch den Menschen — [IST] (NICHT aendern)

- **Die KI akzeptiert nie selbst.** Jeder KI-Befund bleibt Vorschlag (`AiContext.Decision = Ignored`),
  bis der Mensch "Akzeptieren" klickt.
- Unsichere (gelb/rot) oder kritische (Severity >= 4) Befunde werden aktiv zur Bestaetigung vorgelegt.
- Erst das menschliche Akzeptieren macht daraus Gold-Trainingsdaten (eval-kontaminationsgeschuetzt,
  ESW-003). Ablehnen entfernt den Eintrag.

---

## 8. Streckenschaden (laengs > 1 m) — [OFFEN, geplant]

**VSA-Anwendungsregel (2.1.2):** Erstreckt sich eine Feststellung ueber **mehr als einen Meter**,
sind **Anfang (A)** und **Ende (B)** separat zu erfassen. Bei verschachtelten/ueberlappenden
Streckenschaeden zusaetzliche numerische Kennung (A1-B1, A2-B2). Aendert sich Quantifizierung
oder Lage am Umfang waehrend der Strecke, wird der Code mit korrigierten Werten und Zwischen-Code C
wiederholt (A3-C3-B3).

**Soll-Verhalten der KI:**
1. Erkennt die KI einen typischen Streckenschaden-Code (z.B. Wasserrueckstau/-spiegel, Wurzeln,
   Ablagerung, Korrosion — `VsaCodeResolver.IsStreckenschadenCode`) zum ersten Mal, setzt sie einen
   **offenen Anfang (A)** am aktuellen Meter.
2. Solange derselbe Schaden bei weiteren Frames fortbesteht (> 1 m Distanz seit Anfang), bleibt er
   **offen** (kein neues Punkt-Event; letzte Sichtung in `MeterAtCapture` nachfuehren).
3. Verschwindet der Schaden → **Ende (B)** am letzten Sichtungs-Meter setzen.
4. Aendert sich die Quantifizierung/Uhrlage deutlich waehrend der Strecke → Zwischeneintrag (C).
5. **Bei Rohrende (BCE) oder Abbruch (BDC) muessen ALLE offenen Streckenschaeden zwingend
   geschlossen werden** (MeterEnd = aktueller Meter / letzte Sichtung).

**Ist-Stand:** Manuelles Schliessen existiert (`CloseOpenStreckenschaeden`, Button + Exit/Rohrende-Hook,
`IsStreckenschaden`/`MeterEnd`). Die **automatische** Anfang/Ende-Erkennung durch die KI fehlt noch.

**Geplante Bauweise:** reine, testbare Application-Logik (z.B. `StreckenschadenTracker`), die je
Haupt-Code+Uhrlage einen offenen Zustand haelt (Anfang-Meter, letzte-Sichtung-Meter) und Open/Extend/Close
entscheidet. Aufruf duenn aus dem Codierpfad. Kein Frame-Tracking noetig (Dedup per Code+Uhrlage+Meterfenster).

---

## 9. Lage am Umfang (Uhrlage) — [OFFEN/teilweise]

**VSA-Anwendungsregel (2.1.6):** Zifferblattreferenz im Uhrzeigersinn, aus Kamerasicht in
Inspektionsrichtung. 12:00 = Scheitel (oben), 6:00 = Sohle (unten), 3:00 = rechts, 9:00 = links.

**Exakte Werte-Konvention (verbindlich):**
- **Punktbefund:** ein Wert (Mitte der Feststellung), zweiter Wert = **`00`**. Beispiel: Anschluss
  mittig rechts → `03 00`.
- **Bereich am Umfang:** Anfangs- und Endwert nacheinander im Uhrzeigersinn (z.B. `09 03`).
- **Gesamtumfang** (Feststellung laeuft rundum): **`12 12`**.
- **Unbekannt / keine Lage angebbar:** **`00 00`** (so wird auch transferiert).

**Soll-Verhalten:** Verlangt der Code eine Lage, leitet die KI die Uhrlage aus dem Winkel
Maskenschwerpunkt→Fluchtpunkt ab. Beispiel: Anschluss bei `03 00` = Anschluss rechts in Axialrichtung
ins Rohr. Bei versetzten Rohrverbindungen (BAJ) bezeichnet die Uhrlage die **Richtung des Versatzes**
in Inspektionsrichtung.

**Ist-Stand:** `MaskQuantificationService` liefert `ClockPosition`; `VsaCodeResolver.NormalizeClock`
normalisiert. Ablage in CodeMeta `vsa.uhr.von`. **Offen:** gepruefte Ableitung aus der Maskengeometrie,
korrekte Zweitwert-Belegung (`00` / Endwert / `12 12` / `00 00`), und Anfang/Ende bei Streckenschaeden.

---

## 10. Quantifizierung anhand des DN-Kreises — [OFFEN/teilweise]

**Kernregel:** Quantifizierung ist **codeabhaengig**. **Nicht jeder Schaden darf frei quantifiziert
werden** — fordert die Richtlinie fuer einen Code **keine** Quantifizierung, darf **keine** eingetragen
werden. Die KI waehlt die Groesse(n) anhand der Code-Gruppe, nicht generisch.

**Prinzip der Messung:** Der DN-Kreis ist die bekannte Referenzgroesse (kalibrierter Durchmesser in
mm). Daraus ergibt sich mm pro Pixel; absolute Masse (mm) und Prozente werden darauf bezogen berechnet.

**%-Konvention (Klarstellung):** **100 % Querschnittsverminderung = vollstaendig zugesetzt/blockiert.
0 % = keine Verminderung (frei).** (Der frueher in der TXT stehende Satz "Wurzeln 100 % ganzer
Querschnitt keine Wurzel" war falsch/missverstaendlich.)

**Quantifizierung je Code-Gruppe (aus VSA-PDF):**

| Code-Gruppe | Quantifizierung |
|-------------|-----------------|
| BCA Seitlicher Anschluss | Q1 = Hoehe in mm; Q2 = Breite in mm (falls abweichend). Bsp. "Anschluss 120 mm" |
| BAB Risse | Q1 = Rissbreite in mm (Haarriss Char.1 "A" = **keine** Quantifizierung) |
| BAC Leitungsbruch/Einsturz | Q1 = Bruchlaenge in mm |
| BAA Verformung | Q1 = % Reduzierung gegenueber Ursprungsform |
| BBA Wurzeln | Q1 = Querschnittsverminderung in % |
| BBB Anhaftende Stoffe | Q1 = Querschnittsverminderung in % |
| BBC Ablagerungen | Q1 = Ablagerungshoehe in % der Rohrhoehe |
| BBD Eindringen Bodenmaterial | Q1 = Querschnittsverminderung in % |
| BBE Andere Hindernisse | Q1 = Querschnittsverminderung in % |
| BCC Bogen | Q1 = Richtungsaenderung in Grad |
| BAJ Verschobene Rohrverbindung | Abstand/Versatz in mm bzw. Knick-Winkel in Grad |
| BDD Wasserspiegel | Wasserhoehe in % der lichten Hoehe |
| **BBF Infiltration, BBG Exfiltration, BDF gefaehrliche Atmosphaere** | **KEINE Quantifizierung** |

> Hinweis: Dies sind die haeufigsten Gruppen. Massgeblich ist immer die Quantifizierungsangabe des
> jeweiligen Codes in der VSA-Richtlinie; im Zweifel **keine** Quantifizierung statt einer falschen.

**Ist-Stand:** `MaskQuantificationService` rechnet Hoehe/Breite/% grob aus Maske + Kalibrierung.
**Offen:** codeabhaengige Auswahl der korrekten Quantifizierungsgroesse(n) je VSA-Gruppe, Unterdruecken
der Quantifizierung bei Codes ohne Quantifizierung, Validierung gegen den DN-Kreis (mm/Pixel).

---

## 11. Status der Umsetzung (2026-06-16)

| Teil | Thema | Status | Ort |
|------|-------|--------|-----|
| 1    | Pipeline-Reihenfolge | [IST] | RunCodingAnalysisAsync |
| 2    | Einmal-Codes BCD/BCE/BDC; BCE-Plausibilitaet (0.20 m / 90 %) | [IST] + [NEU] commit 4e010579 | CodingDedupPolicy |
| 2    | BCDXP/BCEXP (abweichender Bezugspunkt) | [OFFEN] | — |
| 2    | BDC: Abbruchgrund vorher separat codieren | [OFFEN] | — |
| 3    | Naehe-Gate (DN-Kreis) verschaerft | [NEU] commit f530a7d5 + 01154308 | MetrierungProximityEvaluator |
| 4    | Overlay aufgeraeumt (Eck-Marker, Voraus unsichtbar) | [NEU] commit ea422be7 + f530a7d5 | PlayerWindow.LiveDetection/.Coding |
| 5    | Verbindungs-Kennung "A" (Befund an Rohrverbindung) | [OFFEN] | Feld in ProtocolEntry fehlt noch (VSA-DSS: Kanalschaden.Verbindung) |
| 6-7  | Dedup, QualityGate, menschliche Bestaetigung | [IST] | QualityGateService, AiContext |
| 8    | Auto-Streckenschaden (A/B/C, Schliessen bei BCE/BDC) | [OFFEN] | geplant: StreckenschadenTracker |
| 9    | Uhrlage + exakte Werte-Konvention (00 / 12 12 / 00 00) | [OFFEN/teilweise] | MaskQuantificationService, VsaCodeResolver |
| 10   | Quantifizierung codeabhaengig per DN-Kreis; keine Quant. bei BBF/BBG/BDF | [OFFEN/teilweise] | MaskQuantificationService |

**Tests:** Pipeline-Tests gruen (MetrierungProximityEvaluator, CodingDedupPolicy inkl.
IsBoundaryEndCodePlausible); UI-Tests gruen (444). Vollbuild sauber.

**Naechste Schritte (Vorschlag, Reihenfolge):**
1. BCE-Plausibilitaet committen (Teil 5).
2. Teil 8 (Auto-Streckenschaden) — groesster fachlicher Hebel, reine Application-Logik + Tests.
3. Teil 10 (Quantifizierung je Gruppe) — verbessert Protokollqualitaet messbar.
4. Teil 9 (Uhrlage) — oft zusammen mit Teil 10 sinnvoll.
