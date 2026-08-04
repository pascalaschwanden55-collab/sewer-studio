# Detect-Release-Holdout: PDF-Extraktor

Dieses Werkzeug liest bereits codierte PDF-Protokolle mit dem vorhandenen
`TrainingPdfReviewImportService`. Es übernimmt nur eindeutig zugeordnete Fotos und
Operateurbefunde der 15 Detect-Klassen.

Es schreibt **kein Gold**, keine Wissensdatenbank und keine Teacher-Daten. Der
vorhandene PDF-Dienst legt lediglich seine normale, inhaltsadressierte
Workbench-Arbeitskopie unter `knowledge_root/training/pdf_review_imports` an.

## Auftrag

Der Aufruf erhält genau eine JSON-Datei:

```json
{
  "knowledge_root": "C:\\KI_BRAIN",
  "output_root": "C:\\KI_BRAIN\\eval_set\\staging\\detect_release_001",
  "ffmpeg_path": "ffmpeg",
  "ffprobe_path": "ffprobe",
  "pdfs": [
    {
      "path": "D:\\Haltungen\\24379-24377\\Protokoll.pdf",
      "pdf_sha256": "64_hex_zeichen",
      "haltung_key": "24379-24377",
      "video_path": "D:\\Haltungen\\24379-24377\\Video.mp4",
      "background_fraction": 0.5
    }
  ]
}
```

- `knowledge_root` muss bereits existieren.
- `output_root` muss bereits existieren und vollständig leer sein.
- `pdf_sha256` und `haltung_key` sind Pflicht und werden vor der Übernahme geprüft.
- Alternativ werden die Feldnamen `expected_pdf_sha256` und
  `expected_haltung_key` akzeptiert.
- `video_path` und `background_fraction` sind gemeinsam optional.
- Zulässige feste Videoanteile sind `0.25`, `0.5` und `0.75`.
- Ein Video erzeugt genau einen kandidatenunabhängigen Hintergrundframe ohne
  Operateurreferenz.

## Aufruf

```powershell
dotnet run --project tools\DetectReleaseHoldoutPdfExtractor\DetectReleaseHoldoutPdfExtractor.csproj `
  -c Release --no-restore -- C:\Pfad\auftrag.json
```

Ein kleiner interner Schutztest läuft so:

```powershell
dotnet run --project tools\DetectReleaseHoldoutPdfExtractor\DetectReleaseHoldoutPdfExtractor.csproj `
  -c Release --no-build --no-restore -- --self-test
```

## Ausgabe

Der leere Ausgabeordner erhält:

```text
images/<bild_sha256>.jpg oder .png
_pdf_extraction.json
```

Der Prüfbeleg bindet jedes Bild an SHA-256, Größe, Abmessungen, Haltung und
Herkunft. Ein PDF-Foto enthält eine oder mehrere `operator_references` mit
PDF-Hash, PDF-Name, Seite, Foto-ID, VSA-Code, Detect-Klasse und Befundtext.
Ein Video-Hintergrundframe trägt `source_kind=deterministic_video_frame` und eine
leere Referenzliste.

Unterstützte Hauptcodes:

```text
BCA BAB BAC BAA BAF BAH BAI BAJ BBA BBB BBC BBD BBF SONST BCC
```

`SONST` wird nur als ausdrücklich so codierter Wert akzeptiert. Andere Codes
werden nicht automatisch zu `SONST` umgedeutet.

## Rückgabecodes

- `0`: alle PDFs ohne gemeldete Probleme verarbeitet.
- `2`: Beleg geschrieben, aber mindestens ein PDF oder Einzelfall wurde ausgelassen.
- `1`: der Gesamtauftrag oder die Veröffentlichung ist fehlgeschlagen.
- `130`: Abbruch mit `Strg+C`.

Das Werkzeug veröffentlicht selbst **kein** Eval-Manifest. Ein nachgelagerter
Builder muss den Prüfbeleg und alle Bildbytes erneut prüfen, bevor er einen
eingefrorenen Holdout anlegt.
