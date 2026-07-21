# Plan — Lernen aus den Inspektions-PDFs (`D:\Haltungen`)

**Version:** 1.0 · **Datum:** 2026-07-16 · **Status:** Durchstich bewiesen, bereit für Pilot
**Bezug:** Trainingsplan v1.2, Agent-Konzept, Spec Freigabemanifest

---

## 1. Was in `D:\Haltungen` liegt

- **1.879 PDFs**, ~1.660 Videos (mpg/mp4/mp2/avi), 102 jpg + 76 png, je **ein Ordner pro Haltung** (Name = Start-End-Schacht, z. B. `06.24379-06.24377`).
- Pro Ordner: das **VSA-KEK-Inspektionsprotokoll** (PDF) + das **zugehörige Video** + teils `self_training_frames/`.
- Damit ist **Video ↔ Haltung ↔ Protokoll strukturell verknüpft** — genau die Zuordnung, die uns bisher fehlte.
- `.db` = nur `Thumbs.db` (irrelevant). Faktura-/Sanierungs-PDFs (`norm 175`) sind **keine** Protokolle → ausschließen.

**Template-Lage (wichtig für den Parser):** Mehrheit `combit List & Label` (Firma KIT Bauinspekt). Daneben Ausnahmen: `NCReport`, `PDF24`, `norm 175` (Faktura). → Ein Parser für combit zuerst, Rest erkennen und getrennt behandeln.

---

## 2. Der bewiesene Durchstich (heute, an einer Haltung)

Aus dem PDF `06.24379-06.24377` sauber geparst: **11 Befunde** mit Meterstand, VSA-Code, Beschreibung (inkl. Uhrlage), **Video-Zeitstempel** und Foto-Referenz. Zwei Befunde per `ffmpeg` aus dem Video gezogen:

| Protokoll | Meter | Zeitstempel | Frame zeigt | OSD-Meter im Bild |
|---|---|---|---|---|
| BACB „Fehlende Scherbe/Wandungsteil, 2–10 Uhr" | 0,00 m | 00:00:21 | Rohranfang | +0000.00 m ✓ |
| BBCC „Harte Ablagerungen, 1–6 Uhr" | 9,20 m | 00:04:51 | sichtbare Verkrustung | +0009.27 m ✓ |

**Kernbefund:** Der Zeitstempel liefert den exakten Frame, und der **OSD-Meterstand im Bild bestätigt die PDF-Meterspalte**. Damit entsteht aus jedem Protokolleintrag ein **von einem zertifizierten Inspektor gelabelter Trainingsframe** — Bild → VSA-Code + Uhrlage + Beschreibung.

---

## 3. Warum das der Pipeline enorm hilft

Diese PDFs sind die **menschlich geprüfte Wahrheit**, die die ganze Trainingsstrategie braucht:

1. **Echte Label-Frames** statt nur DINO/SAM-Auto-Labels — Experten-Ground-Truth zum Bootstrappen von YOLO-Detect und -cls.
2. **Gold-/Abnahme-Set:** ganze, menschlich gelabelte Haltungen — genau das fordert v1.2, war aber noch offen.
3. **Herkunfts-Recovery:** die 288 Quarantäne-Samples lassen sich über Haltung/Video wieder zuordnen.
4. **Qwen-Zielformat:** die Protokoll-Beschreibungen sind exakt die deutsche VSA-Formulierung, die Qwen erzeugen soll.
5. **Meter↔Zeit-Kalibrierung:** Zeitstempel + OSD-Meter helfen der Meterstands-Logik.

---

## 4. Extraktions-Pipeline (neues Schicht-1-Skript, rein lesend)

Pro Haltungs-Ordner, vollautomatisch:

1. **PDF klassifizieren** (combit / NCReport / PDF24 / Faktura). Faktura & Nicht-Protokolle überspringen.
2. **Protokoll parsen** → Header (Haltung, DN/Profil, Material, Länge, Richtung) + Befundliste (Meter, Code, Beschreibung, Zeitstempel, Foto).
3. **Uhrlage & Ausdehnung** aus der Beschreibung ziehen („von 2 Uhr bis 10 Uhr").
4. **Frame(s) extrahieren:** ffmpeg am Zeitstempel, kleines **Fenster** (z. B. ±1 s, 3–5 Frames) statt Einzelframe.
5. **OSD-Meter-Cross-Check:** Frame nur behalten, wenn OSD-Meterstand ≈ Protokoll-Meter (Toleranz z. B. ±0,3 m) → automatische Qualitätssicherung gegen Zeitversatz.
6. **Ausgabe** nach `C:\KI_BRAIN\training\pdf_ingest\<haltung>\`: Frames + strukturiertes JSON (Label je Frame) + Manifest.

**Deterministisch, kein LLM.** Fügt sich als Datenquelle in den Nachtlauf-Agenten ein.

---

## 5. Ehrliche Fallstricke & Schutzregeln

- **Template-Varianten:** NCReport/PDF24 brauchen eigene Parser-Zweige; unbekannte Layouts werden **gemeldet, nicht geraten**.
- **Zeitstempel ≠ exakte Bildmitte:** der Inspektor loggt evtl. 1–3 s versetzt → Fenster + OSD-Meter-Check (siehe 4.5) fangen das ab.
- **Code-Typen trennen:** Protokolle enthalten auch Nicht-Schäden (AEF/AEC/AED = Rohr-/Profil-/Materialwechsel, BCD/BCE Anfang/Ende, BDBA/BDA, BCCAY Bogen). Nur echte Schadenscodes werden Detektor-Klassen (via class_map v2).
- **Split nach Haltung:** Frames einer Haltung nie über Train/Dev-Val/Gold streuen. Gold-Haltungen sofort sperren.
- **OCR-Fallback:** ein PDF war „PDF24" — falls gescannt/ohne Textlayer, Tesseract-Zweig nötig.
- **Kundendaten bleiben draußen:** Extrakte nach `KI_BRAIN`, nie ins Repo. Auswertung rein lesend, keine Originaldateien anfassen.

---

## 6. Nächste Schritte

1. **Pilot (20–50 Haltungen)** quer über die Templates: Parser robust machen, OSD-Meter-Check kalibrieren, Trefferquote je Template messen.
2. Ausbeute prüfen: wie viele saubere Label-Frames je Schadensklasse? (Erwartung: mehrere Tausend echte Labels.)
3. **Skalieren auf alle 1.879**, Ergebnis in `pdf_ingest/` + Report.
4. Einspeisen gemäß gewählter Priorität (Abschnitt 3): Bootstrap-Frames / Gold-Set / 288-Recovery / Qwen-Format.
5. Als Datenquelle in den Nachtlauf-Agenten hängen.
