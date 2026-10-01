# Offline-Archiv der Torch-Nightly-Wheels

SewerStudio verwendet für die RTX 5090 zwei exakt festgelegte CUDA-12.8-Wheels:

- `torch-2.12.0.dev20260408+cu128-cp312-cp312-win_amd64.whl`
- `torchvision-0.27.0.dev20260407+cu128-cp312-cp312-win_amd64.whl`

Nightly-Dateien können später vom öffentlichen Paketindex verschwinden. Deshalb
liegen die beiden Windows-/Python-3.12-Dateien außerhalb des Git-Repositories in:

`C:\SewerStudio-Offline-Wheels\cu128-py312-windows`

Die erwarteten SHA-256-Werte stehen versioniert in
[`sidecar/torch-nightly-windows-py312.sha256`](../sidecar/torch-nightly-windows-py312.sha256).

## Prüfen

```powershell
$wheelRoot = 'C:\SewerStudio-Offline-Wheels\cu128-py312-windows'
Get-ChildItem -LiteralPath $wheelRoot -File -Filter '*.whl' |
    Get-FileHash -Algorithm SHA256 |
    Select-Object Path, Hash
```

Die beiden ausgegebenen Werte müssen exakt mit der versionierten Datei
übereinstimmen. Zusätzlich wurden beide Wheels nach dem Herunterladen als
ZIP/Python-Paket geöffnet und ihre `*.dist-info/WHEEL`-Metadaten gelesen.

## Wiederherstellen

Bei einem Neuaufbau die beiden lokalen Wheels als Paketquelle an `uv` übergeben.
Die übrigen Abhängigkeiten bleiben weiterhin durch `requirements-lock.txt`
festgelegt. Die aktive Umgebung nicht von Hand überschreiben; zuerst eine neue
virtuelle Umgebung erstellen und dort `uv pip sync` mit `--find-links` auf den
oben genannten Archivordner ausführen.

Das Archiv liegt derzeit auf demselben Laufwerk wie das Repository. Gegen einen
Festplattendefekt schützt erst eine zusätzliche Kopie auf USB oder einem anderen
Datenträger.
