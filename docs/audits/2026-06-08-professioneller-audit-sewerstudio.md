# Professioneller Audit SewerStudio / AuswertungPro

Datum: 2026-06-08  
Typ: Architektur-, Robustheits- und KI-Pipeline-Audit  
Basis: aktueller Arbeitsstand im Repo plus Deep-Dive-Report `docs/audits/2026-06-08-deep-dive-audit.md` und Findings-JSON  
Arbeitsweise: keine Feature-Aenderung durch diesen Bericht; nur Audit-Dokument erstellt

## 1. Kurzurteil

SewerStudio ist fuer ein Solo-Projekt technisch ungewoehnlich diszipliniert. Die wichtigsten Prinzipien sind im Code erkennbar: Thin-AI, C#-basierte Fachlogik, Hash-Schutz gegen Eval-Kontamination, defensive Pfadbehandlung, loopback-only Server und breite Tests.

Der Deep-Dive hat keine kritischen Funde ergeben. Die zwei wichtigsten HIGH-Funde aus dem Report sind im aktuellen Code bereits behoben und mit Tests abgedeckt:

- R1: `VisionPipelineClient` mutiert keinen geteilten `HttpClient.BaseAddress` mehr.
- R2: `TeacherDelete` loescht ueber `TeacherAnnotationStore.DeleteAsync` unter Store-Lock.

Der aktuelle Stand baut sauber und die relevanten Testprojekte sind gruen. Die Restschuld liegt jetzt vor allem bei zweiter Verteidigungslinie und Datenintegritaet: QualityGate im Fallback, Eval-Guard beim UI-YOLO-Export, atomare JSON-Speicherung, Video-Zuordnung, VRAM-Budget und strengere Modell-/Eval-Gates.

## 2. Scope

Geprueft:

- .NET-Schichten `Domain`, `Application`, `Infrastructure`, `UI`
- KI-Pipeline: YOLO/DINO/SAM/Qwen, Sidecar-Anbindung, QualityGate, Dedup
- Trainings- und Lehrer-Daten: TeacherAnnotationStore, TrainingCenter, Dataset-/Eval-Schutz
- Import-/Medienpfade, Kosten-/Katalog-Speicherung, Tooling- und Teststruktur
- vorhandene Audit-Artefakte: Deep-Dive Markdown und Findings-JSON

Nicht tief geprueft:

- kein externer CVE-/Dependency-Scan
- kein echter End-to-End-Videolauf mit Sidecar/GPU
- kein Lasttest fuer VRAM, Event-Loop oder parallele UI-Nutzung
- keine vollstaendige XAML-Binding-Pruefung
- keine Zeile-fuer-Zeile-Pruefung aller sehr grossen God-Files
- keine semantische Pruefung aller Orphan-Tools

## 3. Methodik

Der Audit folgt diesem Schema:

1. Code und vorhandene Audit-Artefakte lesen.
2. Top-Funde direkt an Datei/Zeile gegenpruefen.
3. Severity nur vergeben, wenn Auswirkung und Pfad plausibel sind.
4. Widerlegte oder entschaerfte Funde separat dokumentieren.
5. Fuer jeden Top-Fund Abnahmekriterien definieren.
6. Build und relevante Tests ausfuehren.
7. Offene Restrisiken und naechste Reihenfolge klar trennen.

## 4. Severity-Modell

| Severity | Bedeutung |
|---|---|
| CRITICAL | Datenverlust, Security-Durchbruch oder Produktionsabbruch ohne realistischen Workaround |
| HIGH | Hauptpfad kaputt, stiller Verlust wertvoller Daten oder klare Gefahr fuer Trainings-/Eval-Integritaet |
| MEDIUM | reales Risiko mit Workaround oder begrenztem Auswirkungsbereich |
| LOW | Hygiene, Defense-in-Depth, toter/latenter Pfad oder geringe Eintrittswahrscheinlichkeit |
| INFO | Beobachtung, Staerke, Dokumentationspunkt oder technische Schuld ohne akuten Schaden |

## 5. Verifikationsstand

Ausgefuehrt am aktuellen Arbeitsstand:

| Check | Ergebnis |
|---|---|
| `dotnet build AuswertungPro.sln -v minimal` | bestanden, 0 Fehler, 0 Warnungen |
| `dotnet test tests/AuswertungPro.Next.Pipeline.Tests/AuswertungPro.Next.Pipeline.Tests.csproj -v minimal --no-restore` | bestanden, 425 Tests, 0 Skip |
| `dotnet test tests/AuswertungPro.Next.Infrastructure.Tests/AuswertungPro.Next.Infrastructure.Tests.csproj -v minimal --no-restore` | bestanden, 302 Tests, 0 Skip |
| `dotnet test tests/AuswertungPro.Next.UI.Tests/AuswertungPro.Next.UI.Tests.csproj -v minimal --no-restore` | bestanden, 302 Tests, 0 Skip |

Hinweis: Ein erster parallel gestarteter Infrastructure-Testlauf scheiterte nur durch Build-Dateilock (`CS2012`), nicht durch Testfehler. Der Einzel-Rerun war gruen.

Gesamt der ausgefuehrten Tests: 1029 bestanden, 0 uebersprungen.

## 6. Top-Funde mit Status

| ID | Severity | Status | Fund | Bewertung |
|---|---|---|---|---|
| R1 | HIGH | gefixt + getestet | Multi-Model-Hauptpfad konnte durch `HttpClient.BaseAddress`-Mutation abbrechen | aktueller Code behebt es; Pipeline-Tests gruen |
| R2 | HIGH | gefixt + getestet, Restpunkt R6 | `TeacherDelete` konnte Lehrer-Annotationen durch Lost-Update verlieren | Lock-Fix ist drin; atomare Speicherung bleibt offen |
| R3 | MEDIUM | offen | QualityGate laeuft im LLM-Fallback nicht immer sichtbar durch | Anzeige kann "Gelb" wirken, obwohl nie bewertet wurde |
| R4 | MEDIUM | offen | UI-YOLO-Export ohne Eval-Guard / teils Dummy-BBox | Risiko fuer Trainingskontamination und schlechte Box-Labels |
| R5 | MEDIUM | offen | Video-Zuordnung per Dateigroesse | stille Falschzuordnung moeglich |
| R6 | MEDIUM | offen | nicht-atomares JSON-Schreiben in mehreren Stores | Crash/Stromausfall kann Datei korrupt machen |
| R7 | MEDIUM | offen | Meter-Plausibilisierung nicht konsistent | schlechter Qwen-Frame kann Timeline/Meter verfaelschen |
| R8 | MEDIUM | offen | VRAM-Budget nicht im Code erzwungen | Prinzip ist dokumentiert, aber nicht hart abgesichert |
| R9 | MEDIUM | offen | einzelne Qwen-Hilfspfade nutzen Freitext statt strict JSON | Hauptpfad sauber, Hilfspfade nicht konsistent |
| R10 | LOW/MEDIUM | offen | Tool-/Test-Blindspots | Orphan-Tools, MCP-Tests, einzelne Policy-Tests fehlen |

## 7. Detailbewertung der wichtigsten Punkte

### R1: Multi-Model-Hauptpfad

Status: behoben und getestet  
Schwere im Alt-Report: HIGH

Urspruengliches Problem: `VisionPipelineClient` setzte auf einem geteilten `HttpClient` `BaseAddress`. Nach dem ersten Request kann .NET diese Property nicht mehr aendern. Dadurch konnte der Multi-Model-Pfad beim Wechsel Health-Check/Analyse/Ollama abbrechen.

Aktueller Code:

- `src/AuswertungPro.Next.Infrastructure/Ai/Pipeline/VisionPipelineClient.cs:32-35` setzt `BaseAddress` nicht mehr.
- Kommentar erklaert den Audit-Fall.
- Regressionstests in `tests/AuswertungPro.Next.Pipeline.Tests/VisionPipelineClientTests.cs`.

Abnahmekriterien:

- geteilter `HttpClient` bleibt ohne `BaseAddress`-Mutation
- zweiter `VisionPipelineClient` mit bereits genutztem Client wirft nicht
- Pipeline-Tests gruen
- Solution-Build gruen

Status der Kriterien: erfuellt.

### R2: Lehrer-Annotationen / Delete-Race

Status: Race behoben und getestet; atomare Persistenz noch offen  
Schwere im Alt-Report: HIGH

Urspruengliches Problem: `TeacherDelete_Click` lud Annotationen, filterte in-memory und schrieb `teacher_annotations.json` direkt aus der UI. Damit wurde der `_fileLock` des Stores umgangen. Paralleles `AppendAsync` konnte neue Gold-Annotationen still verlieren oder geloeschte wieder herstellen.

Aktueller Code:

- `TrainingCenterWindow.xaml.cs:584-586` ruft `TeacherAnnotationStore.DeleteAsync(...)`.
- `TeacherAnnotationStore.cs:97-117` fuehrt Load+Filter+Save unter demselben `_fileLock` aus wie `AppendAsync`.
- Tests in `tests/AuswertungPro.Next.Infrastructure.Tests/TeacherAnnotationStoreTests.cs`.

Abnahmekriterien:

- UI schreibt nicht mehr direkt in `teacher_annotations.json`
- Delete laeuft im Store unter Lock
- paralleles Append/Delete verliert keine Annotation
- Infrastructure-Tests gruen

Status der Kriterien: erfuellt.

Restpunkt:

- `TeacherAnnotationStore.SaveInternalAsync` nutzt weiterhin `File.WriteAllTextAsync(path, json)`. Das ist gelockt, aber nicht crash-atomar. Dieser Punkt bleibt als R6 offen.

### R3: QualityGate im Fallback

Status: offen  
Schwere: MEDIUM

Problem: Im Happy-Path wird das QualityGate sauber bewertet. In Fallback-Zweigen kann aber ein Ergebnis ohne echtes `QualityGateResult` entstehen. Die UI kann das wie Gelb behandeln, obwohl nicht bewertet wurde.

Empfehlung:

- Auch im Fallback immer ein `QualityGateResult` erzeugen.
- Wenn keine Evidenz vorhanden ist: explizit Rot oder "nicht bewertet" statt stilles Gelb.

Abnahmekriterien:

- kein erzeugter KI-Befund ohne QualityGate-Status
- Test fuer LLM-Fehler/Fallback
- UI unterscheidet "Gelb bewertet" von "nicht bewertet"

### R4: UI-YOLO-Export ohne Eval-Guard

Status: offen  
Schwere: MEDIUM

Problem: Die Trainings-/YOLO-Exportpfade im Training Center muessen denselben Eval-Kontaminationsschutz nutzen wie die sauberen Dataset-Builder. Sonst koennen eingefrorene Evalbilder wieder in Trainingsdaten geraten.

Empfehlung:

- gemeinsamer Export-Helper fuer lokale und Sidecar-Pfade
- vor jedem Kopieren: Hash + CaseId/Haltung gegen Eval-Guard pruefen
- kontaminierte Samples skippen und reporten
- Dummy-BBox nur entfernen: exportieren nur, wenn echte Box vorhanden ist

Abnahmekriterien:

- Export bricht oder skippt bei Eval-Treffer
- Report enthaelt `skipped_eval_hash`, `skipped_eval_case`, `skipped_missing_bbox`
- Test mit kuenstlichem Eval-Hash

### R5: Video-Zuordnung per Dateigroesse

Status: offen  
Schwere: MEDIUM

Problem: Gleiche Dateigroesse ist kein Identitaetsbeweis. Zwei verschiedene Videos koennen gleich gross sein. Eine automatische Verlinkung kann dann Protokoll und Video falsch verbinden.

Empfehlung:

- Dateigroesse nur als schwaches Signal nutzen.
- zusaetzlich Dateiname, Haltung oder Teilhash pruefen.
- bei Mehrdeutigkeit nicht automatisch verlinken, sondern `Ambiguous` erzeugen.

Abnahmekriterien:

- gleiche Groesse allein reicht nicht mehr
- Test mit zwei gleich grossen Dummy-Dateien
- Ambiguous-Result statt stiller Falschzuordnung

### R6: Nicht-atomares Schreiben

Status: offen  
Schwere: MEDIUM

Problem: Mehrere Stores schreiben JSON direkt auf die Zieldatei. Bei Crash/Stromausfall waehrend des Schreibens kann eine halb geschriebene Datei entstehen. Gerade `teacher_annotations.json` ist wertvoll.

Empfehlung:

- gemeinsamer `AtomicJsonFileStore` oder Helper
- Ablauf: temp schreiben, validieren, dann `File.Replace` oder Move mit `.bak`
- korrupte Originale beim Laden sichern statt still leer zurueckzugeben

Abnahmekriterien:

- TeacherAnnotationStore schreibt atomar
- Cost-/Catalog-Stores folgen spaeter demselben Helper
- Test fuer kaputte JSON-Datei und Backup-Verhalten

## 8. False Positives / entschaerfte Funde

| Fund | Urspruengliche Sorge | Ergebnis |
|---|---|---|
| SAM-Race | parallele SAM-Zugriffe koennten Race erzeugen | widerlegt; Single-Loop/kein ausnutzbarer Thread-Race im geprueften Pfad |
| PdfPig-Restore | Paket koennte nicht reproduzierbar/restorable sein | widerlegt; Paket ist oeffentlich verfuegbar |
| Few-Shot-Kontamination | Evalbilder koennten direkt in Few-Shot-Prompt gelangen | auf LOW reduziert; Codepfad ist im aktuellen HEAD dormant/tot, Guard sollte trotzdem vor Aktivierung rein |
| Hidden-Eval im Autopilot | Hidden koennte mittrainiert werden | auf LOW reduziert; upstream Builder schliesst das volle 120er Eval per Name+Hash aus, Autopilot sollte Hidden trotzdem redundant pruefen |

Diese Tabelle ist wichtig: Ein professioneller Audit muss zeigen, welche Funde nicht gehalten haben. Sonst ist die Severity nicht glaubwuerdig.

## 9. Prinzipien-Treue

| Prinzip | Status | Kommentar |
|---|---|---|
| Thin-AI | gut | Fachlogik liegt ueberwiegend in C#, LLM liefert Vorschlaege |
| Dedup ohne schweres Tracking | gut | framebasiert, kein Kalman/ByteTrack als versteckte Wahrheit |
| Bild-Forensik | gut | keine generative Frame-Erfindung im Befundpfad |
| Strict JSON fuer Qwen | ueberwiegend gut | Hauptpfad sauber, Hilfspfade noch Freitext |
| Eval-Kontamination | gut, mit Luecken | Builder stark; UI-YOLO-Export muss nachziehen |
| QualityGate immer | teilweise | Happy-Path gut, Fallback offen |
| VRAM-Budget | offen | Hardware traegt es, Code erzwingt es nicht |
| Trainingsdaten-Schutz | besser geworden | R2 Race behoben; atomare Speicherung noch offen |

## 10. Roadmap

### Sofort

1. R1/R2 als erledigt markieren, falls noch nicht committet: Diff pruefen, dann committen.
2. R6 fuer TeacherAnnotationStore nachziehen: atomar schreiben + Backup.
3. R3 fixen: QualityGate auch im Fallback erzwingen.

### Kurzfristig

1. R4: Eval-Guard in UI-YOLO-Export.
2. R5: Video-Zuordnung mit Teilhash/Namen haerten.
3. R7: Meterwerte clampen/plausibilisieren und schlechte Frames nicht in Timeline uebernehmen.

### Mittelfristig

1. VRAM-Budget entweder ehrlich dokumentieren oder im Sidecar erzwingen.
2. Freitext-Qwen-Hilfspfade auf strict JSON ziehen.
3. Orphan-Tools sortieren: in Solution, Scratch-Ordner oder entfernen.
4. MCP-Server-Tests und Policy-Tests ergaenzen.

## 11. Artefakte

| Artefakt | Zweck |
|---|---|
| `docs/audits/2026-06-08-deep-dive-audit.md` | Detailanalyse mit Subsystemen und Funden |
| `docs/audits/2026-06-08-deep-dive-audit-findings.json` | maschinenlesbare Findings |
| `docs/audits/2026-06-08-professioneller-audit-sewerstudio.md` | dieser formale Auditbericht |
| Testausgaben im Terminal | Build/Test-Verifikation des aktuellen Arbeitsstands |

## 12. Schlussbewertung

Der Code ist nicht perfekt, aber gesund. Die groessten akuten Risiken aus dem Deep-Dive sind im aktuellen Arbeitsstand bereits behoben und getestet. Jetzt sollte nicht wahllos weitergefixt werden. Die naechsten sinnvollen Schritte sind klar:

1. R1/R2 sauber committen, wenn noch nicht passiert.
2. Datenintegritaet fertig haerten: atomare Stores.
3. KI-Integritaet haerten: QualityGate-Fallback und Eval-Guard im UI-YOLO-Export.
4. Danach erst die groesseren Architekturthemen VRAM-Budget, Freitext-Qwen und Tool-Aufraeumen angehen.
