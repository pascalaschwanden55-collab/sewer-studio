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

- BCD (Rohranfang), BCE (Rohrende), BDC (Abbruch) sind **Einmal-Codes pro Haltung**
  (`CodingDedupPolicy.IsOneTimeCode`). Duplikate werden verworfen.
- Sie sind **Pflicht** und vom normalen Naehe-Gate (Abschnitt 3) ausgenommen — ein Rohrende darf
  nicht "weggemerkt" werden.
- **Distanz-Bezugspunkt** (VSA 2.1.1): Der Rohranfang (BCD) = 0.00 m ist der Nullpunkt der
  Distanzmessung. Alle Meterstaende beziehen sich darauf.

### BCE-Plausibilitaet — [NEU 2026-06-16]
Der Klassifikator haelt das dunkle Tunnelende am Fluchtpunkt manchmal faelschlich fuer das Rohrende.
- BCE wird **nur akzeptiert, wenn die Kamera nahe am bekannten Haltungsende** (`EndMeter`) ist:
  **innerhalb der letzten 0.20 m ODER ab 90 % der Laenge** (es gilt die jeweils frueher erreichte Schwelle).
- Ist `EndMeter` unbekannt (kein Import/Stammdaten) → BCE wie bisher akzeptieren (sonst entstuende
  evtl. gar kein Rohrende).
- Kommt BCE zu frueh → **verworfen**, Status "Mögliches Rohrende voraus - noch nicht am Ende",
  und es wird **normal weiteranalysiert** (kein Stopp).
- Logik: `CodingDedupPolicy.IsBoundaryEndCodePlausible(code, currentMeter, endMeter)`.

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

## 5. (zusammengefuehrt mit Abschnitt 2 — BCE-Plausibilitaet)

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

**VSA-Anwendungsregel (2.1.6):** Lage am Umfang als Zifferblattreferenz im Uhrzeigersinn, aus
Kamerasicht in Inspektionsrichtung. 12:00 = Scheitel, 6:00 = Sohle, 3:00 = rechts, 9:00 = links.
Punktschaden: ein Wert (Mitte der Feststellung), zweiter Wert 00. Gesamtumfang: 12 12. Keine Angabe: 00 00.

**Soll-Verhalten:** Wenn der Code eine Lage verlangt, gibt die KI die Uhrlage an, abgeleitet aus der
Maskenposition relativ zum Fluchtpunkt. Beispiel: Anschluss bei 3 Uhr = Anschluss rechts in Axialrichtung.
Bei versetzten Rohrverbindungen (BAJ) bezeichnet die Uhrlage die **Richtung des Versatzes** in
Inspektionsrichtung.

**Ist-Stand:** `MaskQuantificationService` liefert `ClockPosition`; `VsaCodeResolver.NormalizeClock`
normalisiert. Ablage in CodeMeta `vsa.uhr.von`. **Offen:** Konsistente, gepruefte Ableitung der
Uhrlage aus der Maskengeometrie (Winkel Maskenschwerpunkt→Fluchtpunkt → Zifferblatt) und Anfang/Ende
bei Streckenschaeden.

---

## 10. Quantifizierung anhand des DN-Kreises — [OFFEN/teilweise]

**Prinzip:** Der DN-Kreis ist die bekannte Referenzgroesse (kalibrierter Durchmesser in mm). Daraus
wird die Groesse eines Ereignisses berechnet (mm pro Pixel aus DN-Kreis-Durchmesser).

**VSA-Anwendungsregeln (welche Groesse je Code-Gruppe):**
- **Anschluss (BCA):** Q1 = Hoehe der Anschlussleitung in mm, Q2 = Breite in mm (falls abweichend).
  Beispiel: "Anschluss 120 mm".
- **Riss (BAB):** Q1 = Breite des Risses in mm (Haarriss = keine Quantifizierung).
- **Wurzeln (BBA) / Anhaftende Stoffe (BBB) / Eindringen (BBD):** Q1 = Querschnittsverminderung in %.
  Beispiel: 100 % = ganzer Querschnitt zu, 0 % = frei. Entsprechend einschaetzen.
- **Ablagerung (BBC):** Q1 = Hoehe der Ablagerung in % der lichten Hoehe.
- **Verformung (BAA):** Q1 = prozentuale Reduzierung gegenueber der Ursprungsform.
- **Bogen (BCC):** Q1 = Richtungsaenderung in Altgrad.
- **Verschobene Rohrverbindung (BAJ):** Abstand/Versatz in mm bzw. Winkel in Grad.
- **Wasserspiegel (BDD):** % der lichten Hoehe.
- Wo die Richtlinie keine Quantifizierung verlangt, darf **keine** eingetragen werden.

**Ist-Stand:** `MaskQuantificationService` rechnet Hoehe/Breite/% grob aus der Maske + Kalibrierung.
**Offen:** Saubere, codeabhaengige Auswahl der korrekten Quantifizierungsgroesse(n) je VSA-Gruppe
und Validierung gegen den DN-Kreis (mm/Pixel), inkl. %-Querschnitt fuer Wurzeln/Ablagerung.

---

## 11. Status der Umsetzung (2026-06-16)

| Teil | Thema | Status | Ort |
|------|-------|--------|-----|
| 1-2  | Pipeline-Reihenfolge, Einmal-Codes | [IST] | RunCodingAnalysisAsync, CodingDedupPolicy |
| 3    | Naehe-Gate (DN-Kreis) verschaerft | [NEU] commit f530a7d5 + 01154308 | MetrierungProximityEvaluator, PlayerWindow.Coding |
| 4    | Overlay aufgeraeumt (Eck-Marker, Voraus unsichtbar) | [NEU] commit ea422be7 + f530a7d5 | PlayerWindow.LiveDetection/.Coding |
| 5/2  | BCE-Plausibilitaet (letzte 0.20 m / 90 %) | [NEU] noch nicht committet | CodingDedupPolicy.IsBoundaryEndCodePlausible |
| 6-7  | Dedup, QualityGate, menschliche Bestaetigung | [IST] | QualityGateService, AiContext |
| 8    | Auto-Streckenschaden (A/B/C, Schliessen bei BCE/BDC) | [OFFEN] | geplant: StreckenschadenTracker |
| 9    | Uhrlage aus Maskengeometrie | [OFFEN/teilweise] | MaskQuantificationService, VsaCodeResolver |
| 10   | Quantifizierung per DN-Kreis je Code-Gruppe | [OFFEN/teilweise] | MaskQuantificationService |

**Tests:** Pipeline-Tests gruen (MetrierungProximityEvaluator, CodingDedupPolicy inkl.
IsBoundaryEndCodePlausible); UI-Tests gruen (444). Vollbuild sauber.

**Naechste Schritte (Vorschlag, Reihenfolge):**
1. BCE-Plausibilitaet committen (Teil 5).
2. Teil 8 (Auto-Streckenschaden) — groesster fachlicher Hebel, reine Application-Logik + Tests.
3. Teil 10 (Quantifizierung je Gruppe) — verbessert Protokollqualitaet messbar.
4. Teil 9 (Uhrlage) — oft zusammen mit Teil 10 sinnvoll.
