# Design: Pipeline-Kontrollsicherung im Codiermodus

**Datum:** 2026-06-03
**Status:** Entwurf zur Freigabe
**Branch-Kontext:** aktueller Arbeitsbaum
**Grundlage:** User-Freigabe fuer Ansatz 2 ("Dedizierter PipelineHealthMonitor-Service mit Interface")

## 1. Problem / Ausgangslage

Im Codiermodus entscheidet der Player heute beim KI-Start einmalig, ob die Multi-Model-Pipeline genutzt wird:

- `PlayerWindow.Coding.cs` initialisiert Qwen und prueft danach einmal `/health` des Sidecars.
- Wenn `VisionPipelineClient.HealthCheckAsync()` eine Antwort liefert, wird `_codingUseMultiModel = true` gesetzt.
- Wenn die Antwort `null` ist, bleibt die App im Qwen-only-Fallback.
- Kommt der Sidecar spaeter hoch, wird der Zustand nicht automatisch korrigiert.
- Faellt der Sidecar spaeter aus oder ist der Token falsch, sieht der Player das nicht als eigenen Zustand.

Die sichtbare Folge: Die App kann "KI bereit" zeigen, obwohl sie nur Qwen nutzt. Umgekehrt kann die Multi-Model-Pipeline nach spaetem Sidecar-Start verfuegbar sein, ohne dass der Player sie benutzt. Die heutige Health-Abfrage unterscheidet ausserdem `401 Token falsch` und `Sidecar nicht erreichbar` nicht, weil beides als `null` zurueckkommt.

Relevante Ist-Stellen:

- Player-KI-Initialisierung: `src/AuswertungPro.Next.UI/Views/Windows/PlayerWindow.Coding.cs`
- Player-Statuszeile: `src/AuswertungPro.Next.UI/Views/Windows/PlayerWindow.xaml`
- Sidecar-Client: `src/AuswertungPro.Next.Infrastructure/Ai/Pipeline/VisionPipelineClient.cs`
- Sidecar-Health-DTO: `src/AuswertungPro.Next.Infrastructure/Ai/Pipeline/VisionPipelineDtos.cs`
- Sidecar-Endpoint: `sidecar/sidecar/routes/health.py`

## 2. Ziel

Der Player soll den realen KI-Modus laufend und ehrlich anzeigen:

- Gruen: volle Multi-Model-Pipeline aktiv.
- Gelb: Schwachmodus, also Qwen-only, weil Sidecar/Token/Multi-Model nicht nutzbar ist.
- Rot: KI aus oder keine nutzbare KI.

Der Zustand soll sich automatisch erholen:

- Sidecar startet spaeter -> Player schaltet automatisch auf Multi-Model.
- Sidecar faellt aus -> Player faellt sichtbar auf Qwen-only zurueck.
- Token-Fehler -> sichtbar als Token-Problem, nicht als allgemeines "offline".

Das Feature soll klein bleiben, testbare Logik enthalten und das grosse `PlayerWindow.Coding.cs` nicht weiter mit Health-/Timer-Auswertung aufblasen.

## 3. Architektur-Entscheidung

Gewaehlter Ansatz: **Dedizierter PipelineHealthMonitor-Service mit Interface**.

Nicht gewaehlt:

- Direktes Polling im Player: zu viel Logik im UI-Code, schwer testbar.
- Sidecar-Health-Endpoint jetzt erweitern: aktuell nicht noetig, weil `/health` bereits genug Grunddaten liefert. Der C#-Client muss sie nur sauberer transportieren.

Der Monitor ist ein kleiner Infrastructure-Service. Die Bewertung des Status ist reine Logik und bleibt separat testbar.

## 4. Komponenten

### 4.1 `PipelineHealthStatus`

Neues Statusmodell fuer die UI und den Monitor.

Vorgeschlagener Ort:

- `src/AuswertungPro.Next.Application/Ai/PipelineHealthStatus.cs`

Kernfelder:

- `Level`: `Full`, `Degraded`, `Down`
- `SidecarReachable`
- `TokenValid`
- `MultiModelActive`
- `YoloLoaded`
- `DinoLoaded`
- `SamLoaded`
- `QwenAvailable`
- `Summary`
- `Detail`

Wichtig: `YoloLoaded/DinoLoaded/SamLoaded` sind Detailinformationen, keine harte Gruen-Bedingung. Wegen Lazy-Loading koennen Modelle noch nicht resident sein, obwohl die Pipeline bereit ist.

### 4.2 `PipelineHealthEvaluator`

Reine Auswertungslogik.

Vorgeschlagener Ort:

- `src/AuswertungPro.Next.Application/Ai/PipelineHealthEvaluator.cs`

Aufgabe:

- Wandelt Sidecar-Health-Ergebnis, Token-/HTTP-Status, KI-Enabled-Flag und Qwen-Verfuegbarkeit in `PipelineHealthStatus`.
- Keine Timer, kein HTTP, keine UI-Abhaengigkeiten.
- Voll per Unit-Test pruefbar.

Regeln:

- KI deaktiviert -> `Down`.
- Sidecar ok + Token ok + Multi-Model darf genutzt werden -> `Full`.
- Sidecar offline oder Token ungueltig, aber Qwen verfuegbar -> `Degraded`.
- Sidecar offline/Token ungueltig und Qwen nicht verfuegbar -> `Down`.
- Modelle nicht geladen wegen Lazy-Loading -> weiterhin `Full`, aber Detailtext "laedt bei Bedarf".

### 4.3 `PipelineHealthCheckResult`

Der aktuelle `VisionPipelineClient.HealthCheckAsync()` reicht nicht, weil er Fehlerarten verschluckt.

Vorgeschlagener Ort:

- `src/AuswertungPro.Next.Infrastructure/Ai/Pipeline/VisionPipelineClient.cs`
- DTO/Record entweder neben dem Client oder in `VisionPipelineDtos.cs`

Neue Rueckgabeform:

- `IsReachable`
- `IsAuthorized`
- `StatusCode`
- `Health`
- `Error`

Bestehende Methode kann fuer Rueckwaertskompatibilitaet bleiben. Neue Methode z.B.:

- `CheckHealthDetailedAsync(CancellationToken ct = default)`

Damit kann die UI unterscheiden:

- kein Prozess / Timeout / DNS -> Sidecar offline
- HTTP 401 -> Token falsch/fehlt
- HTTP 200 -> Health ok
- andere HTTP-Fehler -> Sidecar erreichbar, aber nicht gesund

### 4.4 `IPipelineHealthMonitor`

Vorgeschlagener Ort:

- Interface: `src/AuswertungPro.Next.Application/Ai/IPipelineHealthMonitor.cs`
- Implementation: `src/AuswertungPro.Next.Infrastructure/Ai/Pipeline/PipelineHealthMonitor.cs`

Aufgabe:

- Pollt alle 5 Sekunden.
- Meldet Aenderungen ueber `StatusChanged`.
- Kann gestartet/gestoppt werden.
- Pausiert, wenn KI im Codiermodus deaktiviert ist.
- Fuehrt keine UI-Aenderung direkt aus.

Minimaler Vertrag:

- `PipelineHealthStatus CurrentStatus { get; }`
- `event EventHandler<PipelineHealthStatus>? StatusChanged`
- `Task StartAsync(CancellationToken ct = default)`
- `Task StopAsync()`
- `Task RefreshOnceAsync(CancellationToken ct = default)`

### 4.5 Player-Integration

Vorgeschlagener Ort:

- `src/AuswertungPro.Next.UI/Views/Windows/PlayerWindow.Coding.cs`
- `src/AuswertungPro.Next.UI/Views/Windows/PlayerWindow.xaml`

Verhalten:

- `InitCodingAi()` erstellt Qwen wie bisher.
- Der Sidecar-Client wird mit Sidecar-URL und Token aus Runtime-Settings erstellt, nicht nur aus `SEWERSTUDIO_SIDECAR_URL`.
- Der Monitor wird gestartet, sobald KI aktiv ist.
- Bei `Full`:
  - `_codingUseMultiModel = true`
  - `_codingMultiModel` wird erstellt, falls noch nicht vorhanden
  - Status gruen: "KI bereit (Multi-Model)"
- Bei `Degraded`:
  - `_codingUseMultiModel = false`
  - Status gelb: "KI bereit (Qwen)"
  - Detail nennt Grund: Sidecar offline, Token ungueltig oder Health-Fehler
- Bei `Down`:
  - Analyse-Button deaktivieren, sofern kein Qwen verfuegbar ist
  - Status rot/grau je nach Ursache

Die vorhandene Statuszeile (`CodingAiDot`, `TxtCodingAiStatus`, `TxtCodingAiStage`) wird weiterverwendet. Optional kommt ein kleiner Detail-Popup/Tooltip dazu:

- Sidecar: ok/offline
- Token: ok/ungueltig/nicht benoetigt
- YOLO: geladen/laedt bei Bedarf
- DINO: geladen/laedt bei Bedarf
- SAM: geladen/laedt bei Bedarf
- Modus: Multi-Model/Qwen-only/KI aus

## 5. Ampel-Logik

### Gruen: `Full`

Bedingungen:

- KI ist in den Settings aktiv.
- Sidecar ist erreichbar.
- Token ist ok oder nicht erforderlich.
- Multi-Model-Modus darf genutzt werden.

Modell-Lazy-Loading ist kein Fehler. Direkt nach Sidecar-Start koennen `loaded_models` leer sein. Das ist gruen, solange die Pipeline-Endpunkte grundsaetzlich erreichbar sind. Detailtext: "Modelle laden bei Bedarf".

### Gelb: `Degraded`

Bedingungen:

- KI ist aktiv.
- Qwen ist verfuegbar.
- Multi-Model ist aktuell nicht nutzbar.

Gruende:

- Sidecar nicht erreichbar.
- Token fehlt/falsch (`401`).
- Sidecar antwortet, aber Health ist nicht `ok`.

Text:

- "KI bereit (Qwen)"
- Detail z.B. "Sidecar offline -> keine YOLO/DINO/SAM-Masken"
- Bei Token: "Sidecar Token ungueltig -> Qwen-only"

### Rot/Grau: `Down`

Bedingungen:

- KI in Settings deaktiviert, oder
- Qwen nicht verfuegbar und Sidecar nicht nutzbar.

Text:

- "Kuenstliche Intelligenz deaktiviert"
- oder "KI nicht verfuegbar"

## 6. Auto-Recovery

Polling-Intervall: **5 Sekunden**.

Begruendung:

- Passt zur bisherigen Live-Analyse-Taktung im Codiermodus.
- Schnell genug fuer Sidecar-Nachstart.
- Nicht aggressiv fuer HTTP/Sidecar.

Regeln:

- Monitor pollt nur, wenn KI im Codiermodus aktiv ist.
- Bei Statuswechsel wird UI auf dem Dispatcher aktualisiert.
- Bei Wechsel von `Degraded` zu `Full` wird `_codingUseMultiModel` automatisch auf `true` gesetzt.
- Bei Wechsel von `Full` zu `Degraded` wird `_codingUseMultiModel` automatisch auf `false` gesetzt.
- Eine laufende Einzelanalyse wird nicht mitten im Frame umgeschaltet; die neue Einstellung gilt ab dem naechsten Analyseaufruf.

## 7. Geltungsbereich

Erste Iteration:

- Nur Codier-Live-Modus im Player.

Bewusst nicht app-weit in der ersten Iteration:

- Trainingscenter.
- Vollanalyse-Fenster.
- globale Shell-Statusanzeige.

Der Service wird trotzdem generisch entworfen, damit er spaeter wiederverwendet werden kann.

## 8. Tests

Tests sind sinnvoll, weil der Status sicherheitsrelevant ist: Der User muss wissen, ob volle KI oder nur Qwen arbeitet.

Vorgeschlagener Testort:

- `tests/AuswertungPro.Next.Pipeline.Tests/PipelineHealthEvaluatorTests.cs`
- ggf. `tests/AuswertungPro.Next.Pipeline.Tests/VisionPipelineClientHealthTests.cs`

Zu pruefen:

- KI deaktiviert -> `Down`.
- Sidecar ok + Token ok -> `Full`.
- Sidecar offline + Qwen ok -> `Degraded`.
- HTTP 401 + Qwen ok -> `Degraded` mit Token-Detail.
- HTTP 401 + Qwen nicht ok -> `Down`.
- Sidecar ok, aber Modelle noch nicht geladen -> `Full` mit Lazy-Loading-Detail.
- Health-Status nicht `ok` -> `Degraded` oder `Down` je nach Qwen.

Nicht noetig in dieser Iteration:

- echter Sidecar-Prozess im Test.
- echte DINO/SAM-Inferenz.
- UI-Automation.

## 9. Nicht-Ziele

- Kein Sidecar-Selbsttest mit Probe-Inferenz.
- Keine Aenderung an DINO/SAM/YOLO-Modelllogik.
- Kein Sidecar-Start durch die WPF-App.
- Kein app-weiter Health-Bus.
- Keine neue globale Architektur fuer alle KI-Dienste.
- Kein Tuning von DINO-Labels oder Thresholds.

## 10. Risiken / Trade-offs

### Risiko: Gruen obwohl Modelle noch nicht geladen sind

Bewusst akzeptiert. Wegen Lazy-Loading ist "nicht geladen" kein Fehler. Die Detailanzeige muss ehrlich sein.

### Risiko: Qwen-Verfuegbarkeit wird ungenau bewertet

Wenn Qwen-Health nicht billig verfuegbar ist, darf die erste Iteration `QwenAvailable = config.Enabled` als konservative Annahme nutzen. Besser ist spaeter eine echte Ollama-Health-Abfrage.

### Risiko: PlayerWindow bleibt gross

Der Monitor reduziert neue Logik im Player. Die UI-Anbindung braucht trotzdem wenige Zeilen im Player, weil dort die bestehenden Controls und Felder liegen.

### Risiko: Token-Handling doppelt

Der Client liest Token bereits aus Settings/Env/Datei. Die neue detaillierte Health-Methode soll dieselbe Token-Quelle nutzen, nicht eigene Token-Logik einfuehren.

## 11. Umsetzungsreihenfolge fuer den spaeteren Plan

1. Health-Rueckgabe im `VisionPipelineClient` detaillieren, alte Methode kompatibel lassen.
2. Health-DTO erweitern, damit `loaded_models` und `yolo` aus `/health` nutzbar sind.
3. `PipelineHealthStatus` und `PipelineHealthEvaluator` schreiben.
4. Unit-Tests fuer den Evaluator schreiben.
5. `IPipelineHealthMonitor` und `PipelineHealthMonitor` schreiben.
6. Player-Initialisierung auf Monitor umstellen.
7. Player-Statusanzeige/Tooltip ergaenzen.
8. Build und gezielte Tests laufen lassen.

## 12. Akzeptanzkriterien

- Startet die App vor dem Sidecar, zeigt der Player gelb/Qwen-only.
- Startet der Sidecar spaeter, wird der Player ohne Neustart gruen/Multi-Model.
- Stoppt der Sidecar, faellt der Player sichtbar auf gelb/Qwen-only zurueck.
- Bei falschem Token steht nicht "offline", sondern ein Token-Hinweis.
- Direkt nach Sidecar-Start ohne geladene Modelle bleibt der Status gruen, aber Details zeigen "laedt bei Bedarf".
- Die naechste Einzelanalyse nutzt den jeweils aktuellen Modus.
- Tests fuer die Statusauswertung laufen stabil ohne Sidecar-Prozess.

## 13. Spec-Selbstpruefung

Abdeckung:

- Ansatz 2 ist festgelegt.
- Geltungsbereich ist Codier-Live-Modus im Player.
- Lazy-Loading-Verhalten ist definiert.
- Pruefintervall ist 5 Sekunden.
- Token-Fehler wird explizit von offline getrennt.
- Keine Sidecar-Code-Aenderung ist erforderlich.

Bewusste offene Punkte fuer die Freigabe:

1. Soll der Monitor schon in der ersten Iteration eine echte Ollama/Qwen-Health-Abfrage nutzen, oder reicht `config.Enabled` als Qwen-Annahme?
2. Soll die Detailanzeige nur Tooltip sein oder als kleines ausklappbares Panel umgesetzt werden?
3. Soll der Monitor nur laufen, wenn der Codiermodus sichtbar ist, oder solange das PlayerWindow offen ist?
