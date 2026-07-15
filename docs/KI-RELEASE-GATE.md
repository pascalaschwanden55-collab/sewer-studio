# KI-Release-Gate (Golden-Lauf der Erkennungspipeline)

Die eigentliche Wertschöpfung von SewerStudio ist die KI-Erkennung
(YOLO → DINO → SAM → Quantifizierung). Die **Orchestrierung** ist mit Stubs im
normalen Testlauf abgesichert (`MultiModelAnalysisServiceE2ETests`), aber die
**echte Erkennungsqualität gegen ein reales Video** läuft nur maschinengebunden.
Ohne einen erzwungenen Golden-Lauf fällt eine schleichende Verschlechterung erst
im Feld auf, nicht im Test.

## Der Golden-Lauf

Der Test [`SidecarRealVideoIntegrationTests.EchtesVideo_ErfuelltGoldenVertrag`](../tests/AuswertungPro.Next.Pipeline.Tests/SidecarE2eSmokeContractTests.cs)
fährt die volle Pipeline gegen ein Referenzvideo und prüft den **Golden-Vertrag**
(`GoldenContractValidator`) mit acht Pflicht-Checks:

`health · video_frame_decode · classify · yolo · dino · sam · quantification · production_pipeline`

Er ist per `[MachineIntegrationFact]` **standardmäßig übersprungen** — er braucht
GPU + laufenden Sidecar (localhost:8100) und ein echtes Video, die es in CI nicht
gibt.

## Vor jeder Release ausführen (erzwungen)

Auf der Workstation (GPU + Sidecar an):

```powershell
$env:SEWERSTUDIO_E2E_VIDEO = 'D:\Videoprojekte\golden\referenz.mpg'
./scripts/ki-release-gate.ps1
```

Das Skript [`scripts/ki-release-gate.ps1`](../scripts/ki-release-gate.ps1)
- prüft, dass ein Referenzvideo gesetzt und vorhanden ist,
- schaltet den maschinengebundenen Test frei (`SEWERSTUDIO_RUN_MACHINE_INTEGRATION=1`),
- fährt den Golden-Lauf (`--filter Category=Integration`),
- endet mit Exit-Code 0 nur, wenn der Golden-Vertrag erfüllt ist.

**Regel: Ist dieses Gate rot, geht keine Release raus.** Erst wenn es grün ist,
darf der Release-Tag gesetzt/gepusht werden — ergänzend zum schnellen
[Entwicklungs-Gate](ENTWICKLUNGS-GATE.md) (Unit-/Struktur-Tests vor jedem Push).

## Referenzvideo festhalten

Damit der Lauf reproduzierbar bleibt, sollte **dasselbe** Referenzvideo pro
Release-Serie verwendet werden (fester Pfad, unveränderte Datei). Ändert sich das
erwartete Ergebnis bewusst (neues Modell), wird der Golden-Vertrag
(`PipelineGoldenContract`) angepasst — bewusst und sichtbar, nicht stillschweigend.

Optional eine bestimmte Videostelle prüfen:

```powershell
./scripts/ki-release-gate.ps1 -VideoAt 12.5   # Sekunde/Meter 12.5
```
