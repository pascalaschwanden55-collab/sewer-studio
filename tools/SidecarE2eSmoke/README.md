# SidecarE2eSmoke

Opt-in Smoke-Test fuer den echten Python-Sidecar/GPU-Pfad.

Das Tool ist absichtlich kein normaler Unit-Test: Es braucht einen laufenden Sidecar, Token/GPU und ein echtes Bild oder Video. Der normale Build kompiliert das Tool, aber fuehrt keinen GPU-Lauf aus.

## Beispiele

```powershell
dotnet run --project tools/SidecarE2eSmoke -- --image C:\tmp\frame.png --report C:\tmp\sidecar-e2e.json
```

```powershell
dotnet run --project tools/SidecarE2eSmoke -- --video D:\Haltungen\35723-35734\20230831_35723-35734.mp4 --at 12.5 --run-dino --run-sam --sam-fallback-box --report C:\tmp\sidecar-e2e.json
```

## Geprueft

- `/health`
- `/classify/yolo`
- `/detect/yolo`
- optional `/detect/dino`
- optional `/segment/sam`

Der JSON-Report zeigt Health, GPU/VRAM, Modellantworten, Laufzeiten und Fehler.
