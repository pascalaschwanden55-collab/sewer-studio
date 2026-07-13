# SidecarE2eSmoke

Dieses Werkzeug prueft die echte KI-Verarbeitung ohne Sewer-Studio-Oberflaeche.
Es bleibt bewusst ausserhalb des schnellen Push-Schutzes, weil es GPU, Modelle,
FFmpeg und ein echtes Video braucht.

## Vollstaendiger Video-Vertragstest

```powershell
dotnet run --project tools/SidecarE2eSmoke -- `
  --video "D:\Testdaten\kurzer-kanalclip.mp4" `
  --at 2 `
  --full-pipeline `
  --start-sidecar
```

Der Lauf prueft drei echte Videobilder bei 2, 3 und 4 Sekunden. Er kontrolliert:

- lokaler Sidecar erreichbar, Token gueltig und Version passend;
- FFmpeg dekodiert die Videobilder;
- YOLO-Klassifikation und YOLO-Erkennung antworten;
- DINO antwortet ohne verdeckten Modellfehler;
- SAM verarbeitet eine DINO-/YOLO-Box oder eine sichere Ersatzbox;
- SAM-Masken werden in Millimeter, Prozent und Uhrlage umgerechnet;
- die produktive `SingleFrameMultiModelService`-Kette verarbeitet alle Bilder;
- alle Pflichtpruefungen entsprechen `golden/pipeline-contract.v1.json`.

Das Golden-JSON vergleicht bewusst den **Vertrag**, nicht die genaue Anzahl
erkannter Schaeden. Modellresultate koennen sich durch neue Gewichte leicht aendern.
Die fachliche Erkennungsqualitaet gehoert weiterhin in den Benchmark-Testbestand.

Der Ergebnisbericht wird automatisch unter `artifacts/sidecar-e2e/` gespeichert.

## Sidecar-Start

Mit `--start-sidecar` wird der lokale Sidecar nur gestartet, wenn er noch nicht
laeuft. Ein bereits laufender Sidecar wird nie beendet. Ein vom Werkzeug gestarteter
Sidecar wird nach dem Test beendet. Mit `--keep-sidecar` bleibt er anschliessend an.

## Maschinengebundener xUnit-Test

Der Integrationstest ist normalerweise uebersprungen. Fuer einen echten Lauf:

```powershell
$env:SEWERSTUDIO_RUN_MACHINE_INTEGRATION = "1"
$env:SEWERSTUDIO_E2E_VIDEO = "D:\Testdaten\kurzer-kanalclip.mp4"
$env:SEWERSTUDIO_E2E_VIDEO_AT = "2"

dotnet test tests/AuswertungPro.Next.Pipeline.Tests/AuswertungPro.Next.Pipeline.Tests.csproj `
  --filter "Category=Integration"
```

Ist der Sidecar noch nicht aktiv, startet der Test ihn automatisch. Ein ungeeignetes
oder zu kurzes Video fuehrt zu einer klaren Fehlermeldung statt zu einem falschen Gruen.

## Einzelbild-Schnelltest

```powershell
dotnet run --project tools/SidecarE2eSmoke -- `
  --image "C:\tmp\frame.png" `
  --run-dino `
  --run-sam `
  --sam-fallback-box `
  --report "C:\tmp\sidecar-e2e.json"
```
