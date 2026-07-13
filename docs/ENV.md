# SewerStudio Environment

Diese Datei ist die aktuelle Kurzreferenz fuer Runtime-Umgebungsvariablen. Historische Audit-Dateien koennen aeltere Namen enthalten.

## Sidecar Token

Der Vision-Sidecar erwartet bei Loopback-Aufrufen den Header:

```text
X-Sidecar-Token: <token>
```

Die C#-Seite loest das Token in dieser Reihenfolge auf:

1. explizit konfiguriertes Token aus AppSettings/AiSettings
2. `SEWERSTUDIO_SIDECAR_TOKEN`
3. `AUSWERTUNGPRO_SIDECAR_TOKEN`
4. `SEWER_SIDECAR_AUTH_TOKEN`
5. `SEWER_SIDECAR_TOKEN`
6. `%LOCALAPPDATA%\SewerStudio\.sidecar_token`

`SEWERSTUDIO_SIDECAR_TOKEN` ist der kanonische Name. `AUSWERTUNGPRO_SIDECAR_TOKEN` und die beiden `SEWER_SIDECAR_*` Namen bleiben nur aus Kompatibilitaetsgruenden gueltig.

## AI Runtime

| Variable | Zweck | Default |
| --- | --- | --- |
| `SEWERSTUDIO_AI_ENABLED` | KI global aktivieren (`1`/`true`) | `false` |
| `SEWERSTUDIO_PIPELINE_MODE` | `ollamaonly`, `multimodel` oder `auto` | `ollamaonly` |
| `SEWERSTUDIO_MULTIMODEL_ENABLED` | Multi-Model-Pipeline aktivieren | `false` |
| `SEWERSTUDIO_OLLAMA_URL` | Ollama-Basis-URL | `http://localhost:11434` |
| `SEWERSTUDIO_SIDECAR_URL` | Vision-Sidecar-Basis-URL | `http://localhost:8100` |
| `SEWERSTUDIO_AI_VISION_MODEL` | Vision-Modell oder `auto` | GPU-Auto, sonst `qwen3-vl:2b` |
| `SEWERSTUDIO_AI_TEXT_MODEL` | Textmodell | `qwen3-vl:2b` |
| `SEWERSTUDIO_AI_EMBED_MODEL` | Embedding-Modell | `nomic-embed-text` |
| `SEWERSTUDIO_AI_TIMEOUT_MIN` | Ollama-Timeout in Minuten | `5` |
| `SEWERSTUDIO_OLLAMA_KEEP_ALIVE` | Ollama keep_alive | `24h` |
| `SEWERSTUDIO_OLLAMA_NUM_CTX` | Ollama Kontextgroesse | `8192` oder GPU-Profil |
| `SEWERSTUDIO_SIDECAR_TIMEOUT_SEC` | Sidecar-Timeout in Sekunden | `300` |
| `SEWERSTUDIO_YOLO_CONFIDENCE` | YOLO Confidence-Schwelle | `0.25` |
| `SEWERSTUDIO_DINO_BOX_THRESHOLD` | DINO Box-Schwelle | `0.25` |
| `SEWERSTUDIO_DINO_TEXT_THRESHOLD` | DINO Text-Schwelle | `0.20` |
| `SEWERSTUDIO_PIPE_DIAMETER_MM` | optionaler Rohrdurchmesser-Override | leer |
| `SEWERSTUDIO_FFMPEG` | ffmpeg-Pfad | `ffmpeg` |

Fuer `SEWERSTUDIO_*` akzeptiert `AiSettingsFactory` weiterhin alte `AUSWERTUNGPRO_*` Aliase. Neue Konfiguration bitte mit `SEWERSTUDIO_*` schreiben.

Startet SewerStudio Ollama selbst, setzt es fuer diesen Prozess immer
`OLLAMA_HOST=127.0.0.1:<Port aus SEWERSTUDIO_OLLAMA_URL>`. Eine nichtlokale
`SEWERSTUDIO_OLLAMA_URL` wird deutlich gemeldet und niemals durch SewerStudio
als eigener Ollama-Prozess gestartet.

## Modellwahl

`SEWERSTUDIO_AI_VISION_MODEL=auto` oder ein leerer Wert nutzt die GPU-Erkennung:

- ab 24 GB VRAM: `qwen3-vl:8b-q8`
- ab 8 GB VRAM: `qwen3-vl:2b`
- ohne GPU-Erkennung: `qwen3-vl:2b`

Die Qwen2.5-Familie ist kein Default-Fallback mehr.
