# NightlySoakRunner

Der Runner wiederholt den echten `SidecarE2eSmoke`-Vertragstest. Er prueft pro Runde
Video-Dekodierung, YOLO, DINO, SAM, Quantifizierung und den Golden-Vertrag. Danach
misst er RAM, Handles, Laufzeit sowie – wenn verfuegbar – den GPU-Speicher des
tatsaechlichen Python-Sidecars. Jede Runde wird sofort in eine CSV geschrieben.

Er startet nur nach einem ausdruecklichen Konsolenbefehl und beendet einen von ihm
gestarteten Sidecar beim Ende wieder. Ein bereits laufender Sidecar wird nicht beendet.

## Kurzer Funktionstest

```powershell
dotnet run --project tools\NightlySoakRunner -- --video "D:\Test\kurz.mp4" --max-rounds 2 --start-sidecar
```

## Acht Stunden mit mehreren Videos

```powershell
dotnet run --project tools\NightlySoakRunner -- --video "D:\Test\a.mp4" --video "D:\Test\b.mp4" --duration-hours 8 --start-sidecar --require-nvidia-smi
```

Standardgrenzen: 16 GB privater RAM, 4096 Handles, 24 GB VRAM, 2 GB RAM-Wachstum,
512 neue Handles und 15 Minuten fuer das 95%-Laufzeit-Perzentil. Alle Grenzwerte
lassen sich ueber `--help` anzeigen und pro Maschine enger einstellen.

Die CSV liegt standardmaessig unter `artifacts\nightly-soak`. Strg+C beendet den
Lauf sauber; die bis dahin geschriebenen Messwerte bleiben erhalten.
