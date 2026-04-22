# K4 — BatchPipeline-Deadlock: Diagnose-Playbook

**Datum:** 2026-04-22
**Audit-ID:** K4 (Kritisch)
**Status:** Symptomatisch abgestellt (`SemaphoreSlim(3,3)` statt `6,6`), Root-Cause unbekannt.
**Aufwand:** ~8h Diagnose — Fix-Aufwand hängt vom Befund ab.

---

## Warum dieser Plan?

Das Audit-Memo sagt: „BatchPipeline-Deadlock Root-Cause ungelöst, nur symptomatisch reduziert." Zeile 217 in [`BatchPipelineService.cs`](../../src/AuswertungPro.Next.UI/Ai/Pipeline/BatchPipelineService.cs#L217) dokumentiert das:

```csharp
// 6 parallel → Deadlock. 3 parallel = guter Kompromiss.
var semaphore = new SemaphoreSlim(3, 3);
```

V4.1-Architektur war auf 6 Slots ausgelegt → wir verschenken ~50% Durchsatz. Ohne echten Reproduktionskontext ist Blindfliegen zu teuer — dieser Plan sammelt erst Evidenz, bevor Code geändert wird.

## Hypothesen (alphabetisch, kein Ranking)

### H-A: Ollama-Slot-Contention mit Eskalation
Ollama hat typisch `OLLAMA_NUM_PARALLEL=N` Slots pro Modell. Wenn 6 Qwen-8B-Requests laufen UND parallel ein Eskalationspfad Qwen-32B lädt/entlädt, könnten alle 6 in der Warteschlange hängen.
**Signal:** Ollama-Logs zeigen „loading model" oder „model unload" während des Hangs.

### H-B: SemaphoreSlim im Escalation-Swap
`EnhancedVisionAnalysisService` hat `SemaphoreSlim(1,1)` für Modell-Wechsel. Wenn ein 32B-Load 20s dauert und 6 parallel 8B-Tasks gleichzeitig darauf warten, hängt alles am gleichen `WaitAsync()`.
**Signal:** Thread-Dump zeigt 6× `WaitAsync` auf dem gleichen `_modelSwapLock`.

### H-C: HttpClient-Connection-Pool erschöpft
Ollama-Endpunkt ist lokal (`localhost:11434`), standardmäßig 2 Connections pro Host. 6 parallele Anfragen → 4 warten auf freie Socket-Slots, Timeout-Kette triggert.
**Signal:** `ServicePointManager.DefaultConnectionLimit` default, oder Connection-Count in Performance-Monitor.

### H-D: VRAM-Peak bei Batch-Start
Wenn 6 Qwen-Requests gleichzeitig loslegen, KV-Cache × 6 könnte kurzzeitig die 20GB-Grenze sprengen, Ollama reagiert mit Throttle/Block.
**Signal:** `nvidia-smi -l 1` während Hang zeigt VRAM bei >95% oder OOM-Log in Ollama.

### H-E: Deadlock in `_qwen` wegen Async-Void-Handler
Irgendwo wird ein `.Result` oder `.Wait()` auf einem Task aufgerufen, der wiederum UI-Thread-Synchronisation braucht → klassischer async-Deadlock.
**Signal:** Thread-Dump zeigt Thread im `WaitHelper.GetWaiterForCancellation` auf UI-Dispatcher.

### H-F: `protocolContext.FindClosestTarget` nicht thread-safe
[`BatchPipelineService.cs:237`](../../src/AuswertungPro.Next.UI/Ai/Pipeline/BatchPipelineService.cs#L237) ruft `FindClosestTarget` aus 6 Tasks parallel. Wenn der Lookup intern einen mutierenden State hat, kollidieren Reads/Writes.
**Signal:** Deadlock nur bei ProtocolContext-Mode, nicht bei InverseGapsMode.

### H-G: Logger-Contention
Wenn 6 Frames gleichzeitig viel loggen (FileLogger mit `AppendAllText`), kann das Locking zum Serializer der gesamten Pipeline werden — sieht wie Deadlock aus, ist aber nur sehr langsam.
**Signal:** Tasks machen Fortschritt, aber ~100× langsamer als einzeln gemessen.

---

## Reproduktions-Setup

**Minimaler Repro-Harness:**
1. Ein Ordner mit 10 realen Kanalbildern (aus einem schon verarbeiteten Case)
2. Neuer xUnit-Test oder CLI-Mini-App die nur `BatchPipelineService.AnalyzeBatchAsync` aufruft
3. Semaphore lokal auf 6 setzen (temporärer Debug-Build)
4. Logger auf `LogLevel.Debug`, FileLogger deaktivieren (nur Console)
5. Ollama läuft wie in Produktion (NUM_PARALLEL=6, beide Modelle vorgeladen)

**Datensammlung während des Hangs:**

```bash
# 1. Prozess-ID finden
tasklist /FI "IMAGENAME eq SewerStudio.exe" /FO LIST | grep PID

# 2. Managed Thread Dump (das wichtigste Artefakt)
dotnet-dump collect -p <PID> -o C:\KI_BRAIN\k4-dump.dmp

# 3. Ollama-Log vom Zeitraum
type %LOCALAPPDATA%\Ollama\server.log | grep -E "\[ERROR|loading model|request queued"

# 4. VRAM-Snapshot
nvidia-smi > C:\KI_BRAIN\k4-nvidia.txt

# 5. Connection-Count
netstat -ano | findstr :11434
```

**Analyse-Kommandos für den Dump:**

```bash
dotnet-dump analyze C:\KI_BRAIN\k4-dump.dmp

# Im interaktiven Tool:
# > clrstack -all             # alle Threads + Stacks
# > syncblk                    # alle Lock-Objekte + Wartende
# > !dumpasync                 # hängende async-Tasks
# > !mlocks                    # Managed Locks
```

## Entscheidungsbaum nach Datensammlung

| Befund | Hypothese | Fix |
|---|---|---|
| 6 Threads in `WaitAsync` auf gleichem Lock | H-B | Eskalation muss Queue statt Lock nutzen — ReaderWriterLock oder Tempo-Buffering |
| `dumpasync` zeigt hängendes `ReadAsync` auf HTTP | H-C | `ServicePointManager.DefaultConnectionLimit = 16` setzen |
| Ollama-Log zeigt „loading model" während Hang | H-A | Escalation vom Batch-Pfad ausschließen (32B nur im Nachgang) |
| `nvidia-smi` zeigt 100% VRAM | H-D | Semaphore als Dynamic-Limit: messe VRAM, limitiere |
| Thread-Dump zeigt Blocking auf UI-Dispatcher | H-E | `.Result`/`.Wait()` finden und mit `await` ersetzen |
| Hang nur mit ProtocolContext | H-F | `FindClosestTarget` thread-safe machen (Immutable-Copy) |
| Keine Blocks, aber 100× langsamer | H-G | FileLogger im Batch-Mode deaktivieren |

## Was NICHT tun bis Daten vorliegen

- **Nicht einfach Semaphore auf 6 hochdrehen und testen** — wenn's wieder hängt, wissen wir nichts Neues.
- **Nicht den Qwen-Call refaktorieren** — der ist nicht das Problem, wenn's mit 3 läuft.
- **Nicht Async-Locks durch Sync-Locks ersetzen** — hilft der Diagnose, verschleiert Production-Verhalten.
- **Nicht den Connection-Pool blind raufsetzen** — erst messen, ob das wirklich die Bremse ist.

## Quick-Wins ohne Root-Cause (falls User jetzt akut Durchsatz braucht)

Diese sind bewusst symptomatisch, aber besser-dokumentiert als der aktuelle Stand:

1. **Semaphore adaptiv** — Start mit 3, bei N erfolgreichen Frames +1, cap 6, bei Timeout −1
2. **Connection-Pool raufsetzen** (billig): `ServicePointManager.DefaultConnectionLimit = 16`
3. **Pro-Frame-Timeout senken** auf 30s statt 45s → hängt nicht so lang

Alle drei sind ≤30min Änderungen und können vor der eigentlichen Diagnose passieren, falls der Deadlock grade schmerzt.

## Abschluss-Kriterium

K4 gilt als gelöst, wenn:
1. Der Dump nach einem reproduzierten Hang eindeutig eine der H-A…H-G bestätigt.
2. Ein minimaler Fix angewendet wurde, der den Wiederholungslauf mit Semaphore(6,6) ohne Hang durchbringt.
3. Die dokumentierte Ursache in `memory/project_batchpipeline_deadlock.md` erfasst ist.

## Trigger — wann diesen Plan aktivieren

Nächstes Mal wenn du im Produktiv-Betrieb siehst, dass ein Batch hängt (nicht terminiert, Fortschritt-Report stoppt) — **dann** ist der richtige Moment, diesen Plan zu aktivieren. Solange alles mit 3 Slots durchläuft, ist der Plan in Warteposition.
