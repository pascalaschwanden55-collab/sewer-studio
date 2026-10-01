# Auftrag: Trainingsdaten-Offensive für SewerStudio (Kanalinspektions-KI)

> Fassung 2, 2026-08-04. Überarbeitet nach externer Gegenprüfung.
> Änderungen gegenüber Fassung 1 sind unten unter „Revisionsnotiz" aufgeführt.

## Deine Rolle

Du bist Entwickler und ML-Engineer für ein bestehendes Windows-Programm zur
automatisierten Kanalinspektion. Du arbeitest an einem konkreten, bereits
analysierten Problem. Die Bestandsaufnahme ist gemacht — du sollst sie umsetzen.

## Das Programm

- WPF / .NET 10, MVVM, Windows 11. Solo-Entwickler, kein kommerzielles Ziel.
- Python-Sidecar (FastAPI, Port 8100) liefert YOLO, Grounding DINO, SAM 2.1.
- Hardware: Intel Core Ultra 9 285K, RTX 5090 32 GB, 64 GB RAM.
- Fachnorm: EN 13508-2 / VSA-KEK 2020 (Schweiz).
- Wissenswurzel (alle Trainingsdaten): `C:\KI_BRAIN`
- Repo: `c:\Sewer-Studio_KI_4.5`

## Ausgangslage: Das Modell funktioniert nicht

Der Mehrklassen-Detektor `detect_gold_9eb020e30322`, gemessen auf einem
frischen, menschlich geprüften Holdout mit 400 Bildern
(`conf=0,25`, `imgsz=1280`, `IoU=0,5`, 0 technische Fehler):

- Global: TP 36, FP 59, FN 314 → Precision 37,9 %, Recall **10,3 %**, F1 16,2 %
- Nur `BCC_bogen` funktioniert: Recall 73,0 %
- `BCA_anschluss`: Recall 20,5 %
- Elf weitere Klassen: **exakt null Treffer**
- Auf 9 von 74 sauberen Negativbildern gab es Fehlalarme

Das Modell ist `not_deployed` und darf es bleiben.

## Arbeitshypothese (nicht bewiesen — wird gemessen)

Der wahrscheinlichste Haupttreiber ist die Anzahl **verschiedener Haltungen**
(Inspektionsabschnitte) pro Klasse, nicht die Anzahl Boxen:

| Klasse | Boxen | verschiedene Haltungen | Recall im Holdout |
|---|---:|---:|---:|
| BCC_bogen | 200 | 99 | 73 % |
| BCA_anschluss | 199 | 77 | 20,5 % |
| BAB_riss | 99 | 41 | 0 % |
| BAF_oberflaeche | 73 | 43 | 1,1 % |
| BAA_verformung | 51 | 25 | 0 % |
| BBF_infiltration | 48 | 16 | 0 % |
| BAJ_verbindung | 45 | 35 | 0 % |
| BBC_ablagerung | 43 | 19 | 0 % |
| BAC_bruch | 43 | 23 | 0 % |
| BAI_dichtung | 35 | 9 | 0 % |
| BBA_wurzeln | 27 | 9 | 0 % |
| BBB_anhaftung | 23 | 13 | 0 % |
| BAH_schadanschluss | 8 | 4 | 0 % |

**Wie belastbar ist das?** Es gibt genau zwei Stützstellen oberhalb von
50 Haltungen (BCC und BCA), und beide sind geometrisch grossflächige,
unverwechselbare Objekte. Riss, Oberflächenschaden und Dichtungsmaterial sind
dagegen subtil. Dass BCA mit 77 Haltungen nur 20,5 % erreicht, passt ebenso gut
zur Erklärung „Klasse schwerer unterscheidbar" wie zu „zu wenig Haltungen".

**Konsequenz für die Arbeit:** Der Zielwert von rund 100 Haltungen je Klasse ist
eine begründete Arbeitshypothese, kein Gesetz. Er wird an den Meilensteinen A
und B (Schritt 7) gemessen. Wenn die Kurve zwischen 50 und 100 Haltungen flach
bleibt, ist die Hypothese falsch und der Plan muss angepasst werden — dann liegt
das Problem eher bei Labelstandard, Klassendefinition oder Modellarchitektur.

Zweiter, unstrittiger Befund: Es gibt nur **9 Negativbilder** (Bilder ohne jeden
Schaden). Das ist die wahrscheinlichste Ursache der Fehlalarme. Negative kosten
fast keine Labelzeit.

## Verfügbares Material (vollständig geprüft, schreibfrei gemessen)

### Bestand A: `D:\Haltungen`

- 1476 Haltungsordner, jeder mit mindestens einem PDF-Inspektionsprotokoll
- 1879 PDFs (3,8 GB), 1616 Videos (459 GB)
- 628 PDFs bereits importiert → **1143 PDFs noch neu**
- Nach Abzug von Eval-Schutz (243) und bereits genutzten (312):
  **921 frei nutzbare Haltungen**
- PDF-Formate: Fretz AG 53,8 %, KIT Bauinspekt 11,2 %, KINS 10,3 %,
  Abwasser Uri 7,4 %, Pallon 4,1 %, IBAK 0,6 %, unbekannt 12,6 %
  (davon 24 reine Scans ohne Textebene)
- 231 bereits importierte PDF-Ordner haben noch **kein** Gold — die Bilder
  liegen fertig extrahiert unter `C:\KI_BRAIN\training\pdf_review_imports`.
  Das ist die billigste sofort verfügbare Goldquelle.

### Bestand B: `D:\Videoprojekte`

- 50 komplette Projekte (WinCan-Exporte, IBAK/KIAS, KINS)
- **108 XTF-Dateien**, 75 inhaltlich verschieden, alle fehlerfrei lesbar
- **Achtung: es gibt ZWEI Modellvarianten.** `VSA_KEK_2020_LV95` (neu) und
  `VSA_KEK` (älter). Ein Parser, der nur auf das 2020er-Präfix prüft, übersieht
  rund 86 % der Befunde stillschweigend. Elemente modellunabhängig ansprechen:
  `<[A-Za-z_0-9.]*KEK\.Kanalschaden`, nicht `<VSA_KEK_2020_LV95.KEK.Kanalschaden`.
- **19'069 zuordenbare Schadensbefunde aus 1244 verschiedenen Haltungen**
  (modellunabhängig gemessen, 2026-08-04)
- Die früher genannten Anteile für Meterstand, Uhrlage und Video-Timecode
  (100 % / 93,5 % / 99,9 %) wurden nur auf der 2020er-Teilmenge gemessen und
  gelten für den Gesamtbestand als **ungeprüft**. Vor der Nutzung neu messen.
- 28 datenführende WinCan-Projektdatenbanken (`*.db3`, SQLite)
- 2368 Videos, 31'167 Bilder — davon ~13'140 echte Rohrinnenaufnahmen
- **99,6 % der Bilder sind exakt einem Code zuordenbar** (belegt, nicht geschätzt)
- 4789 Byte-Dubletten aus doppelten Exportordnern — vor dem Labeln entfernen
- Überschneidung mit Bestand A: 622 Haltungen. **Nicht doppelt zählen.**

### Wie die Verknüpfung Bild → Code funktioniert

**Weg 1 (XTF):** `VSA_KEK_2020_LV95.KEK.Datei` enthält `Bezeichnung` (exakter
Dateiname), `Klasse=Kanalschaden` und `Objekt` (TID des Kanalschadens). Über
diese TID kommt man an `KanalSchadencode`, `Distanz`, `SchadenlageAnfang/Ende`,
`Videozaehlerstand`, und über `UntersuchungRef` an `vonPunktBezeichnung` /
`bisPunktBezeichnung` = die Haltung.
Vorhandener Leser: `LegacyXtfImportService.cs:706-872`, Pfadauflösung in
`VsaMediaPathFileResolver.cs:18-81`.

**Weg 2 (WinCan `.db3`, SQLite):** `SECOBSMM.OMM_FileName` →
`SECOBSMM.OMM_Observation_FK` → `SECOBS` liefert `OBS_OpCode` (VSA-Code),
`OBS_Distance`, `OBS_ClockPos1/2`, `OBS_Observation` (deutscher Klartext),
und über `SECINSP` → `SECTION.OBJ_Key` die Haltung.

**Zwei Fallen bei Weg 2:**
- Haltung NUR über `SECTION.OBJ_Key` lesen. `OBJ_FromNode_REF`/`ToNode_REF`
  sind GUIDs.
- Uhrlage NUR über `CodeMeta.ClockPos1/2`. `VsaFinding.SchadenlageAnfang`
  enthält bei WinCan fälschlich den Meterstand
  (`WinCanFindingFactory.cs:54-55`).

**Nicht verwendbar als Goldquelle:** IBAK/KIAS (Fotozuordnung wird per
Warteschlange geraten, `IbakExportImportService.cs:396-427`), KINS (indiziert
nur Videos), die 297 `.mdb` (reine Nachschlagelisten ohne Fotoverknüpfung).

## Erreichbarkeit je Klasse (kombiniert, konservativ)

Stand 2026-08-04, nach der modellunabhängigen Neumessung des XTF-Bestands:

| Klasse | heute im Gold | XTF-Haltungen | PDF-Haltungen (Bestand A) |
|---|---:|---:|---:|
| BCC_bogen | 99 | 410 | 398 |
| BCA_anschluss | 77 | 377 | 323 |
| BAJ_verbindung | 35 | 356 | 303 |
| BAF_oberflaeche | 43 | 235 | 279 |
| BBC_ablagerung | 19 | 170 | 167 |
| BAA_verformung | 25 | 166 | 119 |
| BBB_anhaftung | 13 | 160 | 151 |
| BAB_riss | 41 | 152 | 156 |
| BBF_infiltration | 16 | 125 | 79 |
| BAC_bruch | 23 | 114 | 115 |
| BAH_schadanschluss | 4 | 74 | 122 |
| BAI_dichtung | 9 | 73 | 63 |
| BBA_wurzeln | 9 | 29 | 37 |
| BBD_boden | 0 | 2 | 0 |

Die beiden Bestände überschneiden sich bei 622 Haltungen — nicht addieren.

**Zwölf Klassen sind damit klar erreichbar**, auch die bisherigen Sorgenkinder
BAH, BAI, BBF und BBB.

Nicht erreichbar bleibt:
- **BBD_boden**: 2 Haltungen im XTF, 0 in 1647 PDF-Protokollen, 0 im heutigen
  Gold. Diese Klasse ist mit dem vorhandenen Material nicht belegbar.

Grenzfall:
- **BBA_wurzeln**: 29 XTF- und 37 PDF-Haltungen. Bei starker Überschneidung
  bleiben rund 40–50 — das Ziel 100 ist unsicher, aber die Klasse ist nicht
  aussichtslos wie zuvor angenommen.

→ Plane mit einem **12- bis 13-Klassen-Modell** ohne BBD.

**Wichtige Einschränkung:** Das sind Haltungen, in denen ein Operateur den Code
geschrieben hat. Wie viele davon ein brauchbares Foto mitliefern, ist die
eigentliche Frage. Erste Messung für BAH: 373 Befunde → 243 mit Fotoverweis →
234 physisch dekodierbar → nach Abzug von Gold, Schutz und Dubletten
**49 verschiedene Haltungen** mit 70 Kandidatenbildern. Für die anderen Klassen
steht diese Messung noch aus.

## HARTE REGELN — nicht verhandelbar

1. **Kundenoriginale auf `D:\` sind schreibgeschützt.** Niemals ändern,
   verschieben, löschen. Nur lesen, höchstens kopieren.
2. **Gold entsteht nur durch Handarbeit.** Ein Operateur-Code ist eine
   *Vorgabe*, kein Label. Ein Goldsample verlangt: persönlich gezogene
   Hand-Box, gültige SAM-Maske (mindestens 80 % der Maskenpixel innerhalb
   der Box), persönliches Akzeptieren. Kein automatischer Gold-Export.
3. **Eval-Schutz ist fail-closed.** Geschützte Haltungen und Bildhashes
   dürfen nie ins Training. Haltungen sind richtungsunabhängig: `A-B` und
   `B-A` sind dieselbe. Schutzquellen: `C:\KI_BRAIN\eval_set` (inkl.
   `subsets`), `eval_review`, `training\negatives`, die Gold-Audit-Berichte
   unter `training\reports`, und `test_sample_ids_excluded` im
   DETECT_ALL-Register.
4. **Kein grosses Refactoring am Bestand.** Neue Funktionen additiv
   danebenbauen: eigener Dienst + Interface + DI-Registrierung in
   `ServiceProviderRegistrationMap` + mindestens ein fokussierter Test.
   Neue Workflow-Klassen nach `src/AuswertungPro.Next.Application/UseCases/`,
   NICHT nach `UI/Ai/` (dort gilt ein Architektur-Freeze).
5. **Kommentare und UI-Texte auf Deutsch.**
6. **Keine NuGet-Pakete ohne Rückfrage.**
7. Prüfen mit `dotnet build AuswertungPro.sln` und `dotnet test AuswertungPro.sln`.
   Achtung: Ein laufendes `SewerStudio.exe` sperrt die Ausgabedateien —
   vor dem Build schliessen.

## BEKANNTER FEHLER — zuerst reparieren

**Rund 19,8 % der bestehenden PDF-Goldsamples tragen eine falsche
Haltungsnummer.** Gemessen: 192 von 969 prüfbaren Samples, 41 verschiedene
Fälle, 39 betroffene Haltungen. 61 Samples waren nicht prüfbar.

*Messmethode (nachvollziehbar):* Aus `Notes` die PDF-Operateurreferenz lesen,
aus dem Dateinamen `YYYYMMDD_<haltung>.pdf` die Haltung ziehen, beide Seiten
normalisieren (Bereichspräfix nur entfernen, wenn der Rest ≥ 4 Zeichen hat),
Endpunkte sortieren, vergleichen.

Drei Muster:

| Was passiert | Beispiel (CaseId ← Quell-PDF) | Betroffene Samples |
|---|---|---:|
| Platzhalter statt echtem Knoten | `999001-90327` ← `20231123_06.887943-90327.pdf` | 45 |
| Knoten abgeschnitten | `10009-10` ← `10009-10.8433` | 6 |
| komplett falsch gegriffen | `5-7` ← `955096-59460` | 20 |

Dazu 159 Samples mit unnormalisiertem Bereichspräfix und 6 Samples mit der
degenerierten CaseId `1-1` (Uhrlagen-Artefakt).

**Warum kritisch:** Die Haltungsnummer ist der wichtigste Eval-Schutz. Stimmt
sie nicht, greift die Sperre nicht. Vier Haltungen aus Holdout und Negativsatz
tragen deshalb heute schon Gold (u. a. `07.148371-10300`, `36798-10.675988`).

## DER PLAN

### Schritt 0 — Haltungszuordnung reparieren

**Kein einziges neues PDF wird vor Abschluss dieses Schritts importiert.**

**Wichtig — das vorhandene Werkzeug reicht nicht.**
`training/scripts/repair_gold_holding_ids.py` (498 Zeilen) deckt **keins** der
drei Fehlermuster ab. Der Filter in Zeile 218 bearbeitet ausschliesslich
Samples, deren CaseId mit `foto_` beginnt:

```python
if not old_case.casefold().startswith("foto_"):
    continue
```

Nachgeprüft im Bestand: 10 Samples haben eine `foto_`-CaseId, 1030 tragen eine
PDF-Operateurreferenz — die Überschneidung ist **null**. Alle drei Fehlermuster
sind haltungsförmige Falschwerte aus dem PDF-Weg und werden still übersprungen.
Die Wahrheit liegt bei ihnen auch anderswo: in `Notes → PDF-Operateurreferenz`,
nicht im Dateinamen eines Quellenordners.

**Was wiederverwendet wird (rund 70 % der Arbeit): das Transaktionsgerüst.**
Schreibfreier Prüflauf als Default, bytegleicher SHA-256-Beweis, atomare
JSON-Schreibungen, `BEGIN IMMEDIATE` mit Zeilenzahl-Prüfung in SQLite,
dreifache Sicherung plus `repair_plan.json`, bytegenaue Nachprüfung
(`build_plan_after_repair`), SewerStudio-Laufsperre, Signatur-Kollisionsprüfung,
gemeinsame Aktualisierung von Gold-JSON, Teacher-JSON und `Samples.CaseId`.

**Was neu gebaut wird (rund 30 %): ein Geschwister-Skript**
`training/scripts/repair_pdf_gold_holding_ids.py` mit einem PDF-spezifischen
Wahrheitsableiter:

1. `PDF-Operateurreferenz` aus `Notes` parsen → Kandidaten-Haltung aus dem
   PDF-Dateinamen ziehen, mit den neuen Schutzregeln (Bereichspräfix nur bei
   ≥ 4 Restzeichen, keine Uhrlagen, keine GUID-Fragmente).
2. Gegenprobe gegen den Haltungsordner unter `D:\Haltungen`.
   Übereinstimmung → Reparaturkandidat. Widerspruch → Quarantäneliste
   (das ist der Fall `999001-90327`).
3. Härtester Beleg: SHA-256-Vergleich des Goldbildes gegen das in genau diesem
   PDF eingebettete Bild. Reader vorhanden: `TrainingPdfEmbeddedImageReader`.
   **Achtung:** Rund 33 % der eingebetteten Fotos sind CMYK-JPEG und werden
   beim Import über `ITrainingPdfJpegColorNormalizer` nach RGB-PNG gewandelt.
   Für diese Bilder gibt es **keinen** bytegleichen Treffer gegen das rohe
   PDF-Bild. Der Vergleich muss deshalb gegen das *normalisierte* Ergebnis
   laufen — oder für diese Fälle als „kein Beleg möglich" ausgewiesen werden
   statt als Nichtübereinstimmung.
4. Signatur-Neuaufbau (`neue CaseId|Rest`) und Kollisionsprüfung unverändert
   aus dem Bestandswerkzeug übernehmen.

**Erster Lauf: schreibfrei, nur quantifizieren.** Danach steht die Zahl der
Fehlfälle belegt statt geschätzt, aufgeteilt nach reparierbar / quarantäne /
kein Beleg möglich. Erst danach `--execute`.

Codeseitig zu ändern (`TrainingPdfHaltungId.cs`):

- `AreEquivalent` (Zeile 141) ist heute **richtungsabhängig**:
  `CreateComparisonKey` (Zeile 171) fügt die Endpunkte in gegebener Reihenfolge
  zusammen und tilgt nur `.0`-Suffixe. `A-B` ≠ `B-A`. → Endpunkte sortieren.
- Bereichspräfix (`06.`, `07.`, `10.`) behandeln — aber **nur entfernen, wenn
  der Rest mindestens 4 Zeichen hat.** Sonst werden echte Knoten zerstört:
  `797.01-797.02` wurde zu `01-02` und mit `3.01-3.02` verschmolzen.
  Belegt: Das OSD einer Aufnahme zeigt die Haltung als „1534.01 1534.03" —
  das `.01` ist Teil des Knotennamens, kein Präfix.
- Uhrlagen (`12-1`, `7-5`) und GUID-Fragmente aus `HaltungIdRegex` ausschliessen.

**Konfliktregel — wichtig:** Bei Widerspruch zwischen Ordnername und
Protokolltext **nicht** pauschal eine Quelle bevorzugen, sondern den Fall
**quarantänisieren** und dem Menschen zur Entscheidung vorlegen. Beim Fall
`999001-90327` ist nicht entscheidbar, welche Seite recht hat. Eine falsche
Vorrangregel würde den Fehler nur in die andere Richtung verschieben.

Die 39 bestehenden Fehlfälle korrigieren oder quarantänisieren.

### Schritt 1 — Feste Sperrliste

Alle geschützten Haltungen aus allen Schutzquellen in eine Ausschlussliste
zusammenführen und beim Import automatisch anwenden. Heute kennt der Codepfad
nur `eval_set` und blockt damit rund 233 Ordner — die Gold-Test-Haltungen
(29) und Legacy-Negative (9) fehlen.

### Schritt 2 — Labelstandard klären, bevor 1300 Samples danach gesammelt werden

Eine Abweichungsanalyse über den **Trainingsbestand** (nicht den Holdout) zeigt:
62 % der `BAF_oberflaeche`-Trainingsboxen (49 davon im Register) folgen einem
anderen Grössenmuster als der Holdout-Standard, bei `BBC_ablagerung` 53 %.
Arbeitsliste: `artifacts/label-review-20260803/abweichungsliste.json`
(131 markierte Samples in 6 Klassen), Ansicht: `abweichungen.html`.

Das kann harmlos sein — eine kleine Box auf einem echt kleinen Schaden ist
richtig. Aber es ist **ungeprüft**. Wenn jetzt 1300 neue Samples nach demselben
Standard entstehen, skaliert man einen möglichen Fehler mit.

Zu tun:
1. Die markierten Samples durchsehen — nicht alle korrigieren, sondern
   verstehen, **warum** sie abweichen.
2. Daraus eine schriftliche **Boxregel je Klasse** ableiten (eine Seite):
   Wo genau wird bei BAF die Box gezogen? Bei BBC? Bei BAJ? Diese Regel ist
   die Voraussetzung dafür, dass 1300 neue Labels einheitlich werden.
3. Besonders prüfen, ob Flächen-/Texturklassen (BAF, BBC, BBF) überhaupt
   sinnvoll als Box-Objekte definierbar sind — oder ob sie besser als
   Bild-Ja/Nein-Klassifikation behandelt werden. Diese Entscheidung gehört
   **vor** die Massensammlung.

### Schritt 3 — Negativbilder sammeln

Von 9 auf 300–500 saubere Bilder aus vielen verschiedenen Haltungen.
Reine Klickarbeit, keine Boxen. Werkzeuge existieren:
`training/scripts/bcc_hard_negative_review.py`,
`derive_negative_set_for_gold_audit.py`,
`tools/EvalVisibilityReview/bcc_hard_negative_review_server.py`.
Wirkung: weniger Fehlalarme. Der Recall bleibt davon unberührt.

Kann parallel zu Schritt 0 bis 2 laufen.

### Schritt 4 — Kandidatenliste je Klasse (schreibfreies Skript)

Ein Python-Skript im Stil der vorhandenen `training/scripts/`:
liest die 75 XTF-Dateien und die 28 WinCan-`.db3` (Kopie ins Temp-Verzeichnis,
`D:\` bleibt unberührt) und schreibt eine Liste mit
Bildpfad, VSA-Code, Meter, Uhrlage, Haltung.

Gefiltert gegen bestehendes Gold **und** alle Schutzquellen.
Byte-Dubletten entfernen. Höchstens 1–2 Bilder je physischer Haltung.
Leitungsinspektionen (`L_`, DN160) getrennt kennzeichnen — sie sind fachlich
etwas anderes als DN300-Sammler.

### Schritt 5 — Pilot mit echtem Zeitmass

50–100 Bilder **einer Engpassklasse** (Empfehlung: `BAH_schadanschluss`,
heute nur 4 Haltungen, grösster relativer Sprung) über den bestehenden
Gold-Eingang `C:\KI_BRAIN\training\gold_inbox\<Hauptcode - Klartext>`
einschleusen und im Training Studio komplett durchlabeln.

Danach steht fest:
- wie viele Minuten ein Goldsample wirklich kostet
- wie hoch der Ausschuss ist (Schaden auf dem Operateurfoto nicht sichtbar —
  bei einer Stichprobe war das in 1 von 5 Fällen so)
- ob sich ein eigener XTF-Leseweg überhaupt lohnt

**Ohne diese Zahl ist jede Terminplanung geraten.**

### Schritt 6 — Nach Knappheit sammeln

Reihenfolge: BAH (4), BAI (9), BBA (9), BBB (13), BBF (16), BBC (19),
BAC (23), BAA (25). BCC und BCA haben 99 bzw. 77 Haltungen und brauchen keine
Menge mehr, sondern Vielfalt.

Gesammelt wird nach der in Schritt 2 festgelegten Boxregel.

### Schritt 7 — Etappenweise trainieren und die Hypothese messen

- **Meilenstein A:** alle Klassen ≥ 50 Haltungen → trainieren und messen
- **Meilenstein B:** alle Klassen ≥ 100 Haltungen → trainieren und messen

Beide Messpunkte prüfen die Arbeitshypothese aus dem Abschnitt oben. Bleibt die
Kurve zwischen A und B flach, liegt das Problem nicht bei der Haltungszahl —
dann Plan anpassen statt weitersammeln.

Trainingsseitig zusätzlich: mehr als 40 Epochen, stärkere Augmentierung, und
Copy-Paste-Augmentierung über die vorhandenen SAM-Masken für seltene Klassen.
(`flipud=0.0` und `fliplr=0.0` sind bereits gesetzt — die Uhrlage darf nicht
gespiegelt werden. Kein Handlungsbedarf.)

### Schritt 8 — Frischer Holdout

Der bestehende 400-Bilder-Holdout ist verbraucht: Seine Fehlfall-Review
(`detect_gold_failure_a46a82535c82`) und der Sammelplan
(`detect_gold_collection_874ec160e346`) sind bereits in die Modellentwicklung
eingeflossen. Für die Abnahme braucht es einen neuen, zuvor unberührten
Bestand aus anderen Haltungen.

## Optionale Entwicklung (erst nach Schritt 5 entscheiden)

Ein XTF-/WinCan-Trainingsleseweg analog zum bestehenden PDF-Weg.
Aufwand mittel für das Lesen, **gross** für die vollständige Herkunftskette:
Der Goldpfad ist heute bewusst PDF-verdrahtet und fail-closed
(`ManualGoldTrainingPolicy` lässt nur `PdfPhoto` und `ManualCoding` zu;
`AnnotationWorkbenchService.cs:399-446` setzt `SourceType` hart auf `PdfPhoto`;
`PdfGoldProvenancePolicy` verlangt wörtlich den PDF-Beleg).
Nötig wären: neuer SourceType, eigene Provenance-Policy, generalisierter
Vorschlagstyp. **Die PDF-Regex nicht aufweichen.**

Lohnt sich nur, wenn der Pilot zeigt, dass das Einsortieren ein grosser
Teil des Aufwands ist. Box, SAM und Bestätigung bleiben in jedem Fall Handarbeit.

## Entscheidung, die vor der Massensammlung fallen muss

Das OSD (Meterstand, Haltung, Befundtext) ist in viele Bilder **eingebrannt** —
im Klartext, z. B. „Breite Rohrverbindung von 12 Uhr bis 12 Uhr".

- Chance: automatische Gegenprüfung der Labels.
- Risiko: Das Modell lernt den Text zu lesen statt den Schaden zu sehen.

Diese Entscheidung gehört **vor** die Sammlung, nicht erst vor das Training —
sie ändert, *was* gelabelt wird (z. B. OSD-Bereich beschneiden, überdecken
oder bewusst behalten).

## Aufwandsrahmen (ehrlich)

12 Klassen × ~100 Haltungen = mindestens 1300 handbestätigte Samples.
Bei 1–3 Minuten pro Sample: **20 bis 65 Stunden** reine Klickarbeit.
Verteilbar über Wochen, aber nicht automatisierbar.

## Was du NICHT tun sollst

- Keine automatische Gold-Erzeugung aus Operateur-Codes
- Keine SAM-Video-Propagation als Goldfabrik (nur als Prüfwerkzeug für den
  Menschen; propagierte Nachbarframes sind stark abhängige Vorschläge)
- Kein zweiter YOLO-Datensatzschreiber neben dem bestehenden
  plan-gesteuerten Export
- Keine Modellaktivierung ohne frischen Holdout
- Die Bestandsaufnahme nicht neu erheben — die Zahlen sind schreibfrei
  gemessen und gegengeprüft. Die *Hypothese* darfst und sollst du prüfen.

## Deine erste Aufgabe

Beginne mit **Schritt 0**: Baue
`training/scripts/repair_pdf_gold_holding_ids.py` als Geschwister-Skript,
zunächst **ausschliesslich als schreibfreien Prüflauf** — kein `--execute`,
keine Schreibpfade, auch nicht auskommentiert.

Ergebnis des ersten Laufs soll ein Bericht sein, der die Fehlfälle nach vier
Gruppen aufteilt:

1. reparierbar mit Bildbeleg (SHA-Treffer gegen das PDF-Bild)
2. reparierbar ohne Bildbeleg (nur Ordner- und Dateinamenübereinstimmung)
3. Quarantäne (Widerspruch zwischen Ordner und Protokolltext)
4. kein Beleg möglich (CMYK-normalisiertes Bild, siehe Punkt 3 oben)

Zeige mir deinen Aufbau, bevor du Code schreibst. Frag nach, wenn dir Kontext
fehlt — rate nicht.

---

## Revisionsnotiz (Fassung 1 → 2)

Vier Behauptungen aus Fassung 1 wurden im Code nachgeprüft und korrigiert:

| Punkt in Fassung 1 | Befund | Folge |
|---|---|---|
| „CMYK-Normalisierer prüfen, ob injiziert" | Ist injiziert: `ServiceProvider.cs:408`, zusätzlich `TrainingStudioWindowDependencyFactory.cs:61` | Schritt gestrichen |
| „`flipud=0.0, fliplr=0.0` setzen" | Bereits gesetzt in `train_detect_gold.py` und `train_bcc_pilot.py` | Als erledigt vermerkt |
| „39 Fehlfälle korrigieren" ohne Werkzeugnennung | `repair_gold_holding_ids.py` existiert und macht genau das | Als Ausgangspunkt vorgeschrieben |
| „Ordnername als Vorrangquelle" | Bei echtem Quellenwiderspruch nicht entscheidbar | Ersetzt durch Quarantäne-Regel |

Drei inhaltliche Korrekturen:

1. Die 100-Haltungen-Schwelle war als „belegt" formuliert. Sie beruht auf zwei
   Stützstellen und ist jetzt ausdrücklich als Arbeitshypothese mit Messpunkten
   an den Meilensteinen A/B gekennzeichnet.
2. „Nicht die Labelqualität" war voreilig. Der Fehler-Review betraf nur die
   Holdout-Labels; der Trainingsbestand zeigt bei BAF und BBC ungeprüfte
   Abweichungen. Neuer Schritt 2 klärt den Labelstandard, bevor 1300 Samples
   danach entstehen.
3. Die OSD-Entscheidung war vor dem Training eingeordnet. Sie gehört vor die
   Sammlung, weil sie ändert, was gelabelt wird.

`AreEquivalent` ist wie beschrieben richtungsabhängig — bestätigt in
`TrainingPdfHaltungId.cs:141-184`.

## Nachtrag zu Fassung 2 (2026-08-04)

Fassung 2 wies an, Schritt 0 auf `repair_gold_holding_ids.py` aufzusetzen.
**Das war falsch.** Das Werkzeug filtert in Zeile 218 auf CaseIds mit
`foto_`-Präfix und kann deshalb keinen einzigen PDF-Fehlfall anfassen.
Nachgeprüft im Bestand: 10 Samples mit `foto_`-CaseId, 1030 mit
PDF-Operateurreferenz, Überschneidung null.

Schritt 0 ist entsprechend neu gefasst: Transaktionsgerüst wiederverwenden,
Wahrheitsableiter neu bauen. Neu ergänzt ist ausserdem die CMYK-Einschränkung
beim Bildbeleg — rund ein Drittel der eingebetteten Fotos wird beim Import
farbnormalisiert und kann deshalb nicht bytegleich gegen das rohe PDF-Bild
geprüft werden.
