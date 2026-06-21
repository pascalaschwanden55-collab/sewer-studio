# Video-Scrub Label-Werkzeug

Pro Protokoll-Befund (Haltung, Video-Zeit, VSA-Code) ins Video springen, zur Stelle scrubben,
wo der Schaden **wirklich** sichtbar ist, genau diesen Frame greifen und den Code bestaetigen.
Ergebnis = sauberer Gold-Satz statt Reparatur am kaputten, am Protokoll-Meter geschnittenen Frame.

## Start

```
tools\VideoLabelTool\start.bat
```
oder
```
python tools/VideoLabelTool/server.py
```
Dann im Browser **http://localhost:8200/** oeffnen.

Standard ist jetzt **Mix von allen verfuegbaren Klassen** aus dem neueren Datensatz
`C:\KI_BRAIN\yolo_vsa_cls_dataset_v3_bal`. Im Browser kannst du oben bei
**Klasse** zwischen `Alle / Mix` und einzelnen Codes wie `BCA`, `BCC`, `BAB` wechseln.
Nahe Duplikate werden standardmaessig bereinigt: gleicher Code in gleicher Haltung
innerhalb von 20 Sekunden erscheint nur einmal in der Session.

Gezielt nur Anschluss/Bogen labeln:
```
python tools/VideoLabelTool/server.py --dataset C:\KI_BRAIN\yolo_vsa_cls_dataset_v3_bal --classes BCA,BCC --limit 300
```

Gezielt wieder nur Risse:
```
python tools/VideoLabelTool/server.py --classes BAB --limit 300
```

Wenn du die Duplikat-Sperre bewusst ausschalten willst:
```
python tools/VideoLabelTool/server.py --dedupe-window 0
```

## Bedienung
- Der Server schneidet pro Befund mit ffmpeg ein **Fenster (Zeit ± 10 s)** als browser-faehiges mp4.
- Rechts steht der **Original-Frame** (was das Protokoll am Meter gegriffen hat) als Kontext.
- Scrubben mit den Knoepfen oder Tasten:
  - `←`/`→` Einzel-Frame · `A`/`D` ±0.2 s · `Leertaste` Play/Pause
  - `Enter` Frame greifen & annotieren · `E` kein Befund (LEER) · `U` unsicher · `S` ueberspringen
  - `,`/`.` vorheriger/naechster Befund
- **Code** rechts bestaetigen/korrigieren (Freitext + Vorschlagsliste). Bei „kein Befund" wird LEER gespeichert.

## Annotieren (Box + SAM-Maske)

`Enter` (oder „Frame greifen & annotieren") oeffnet die Annotations-Ansicht:
1. Du **ziehst eine Box** um den Schaden = „hier ist der Schaden".
2. **SAM-Maske erzeugen** → der Sidecar (`/segment/sam`) liefert die pixelgenaue Maske (gruen).
3. Du **bestaetigst/korrigierst** (Box neu, Maske loeschen, Code anpassen).
4. **Als Gold speichern** → Frame + Box + Maske + Code.

> SAM **entscheidet nichts** — es maskiert nur, nachdem DU die Box gesetzt hast. Box/Maske sind optional;
> du kannst auch nur Frame+Code speichern. **Sidecar muss laufen** (SAM laedt on-demand), sonst nur Box ohne Maske.

## Ausgabe
- Gold-Frames: `C:\KI_BRAIN\gold_labels\<CODE>\<haltung>_<zeit>s_<CODE>_gold.png`
- Gold-Annotation (bei Box/Maske): gleiche Basis `..._gold.json` mit
  `frame, haltung, protocol_time, chosen_time, code, box_norm (YOLO), box_px, mask_rle, image_w/h, mask_area_pixels, source_video, annotated_by`.
- Protokoll (jede Entscheidung): `C:\KI_BRAIN\gold_labels\gold_ledger.jsonl` (inkl. `has_box`, `has_mask`).
- Der gespeicherte Frame ist **genau das angezeigte Bild** (WYSIWYG), keine KI-Aufwertung.

## Sidecar (fuer SAM)
- URL `http://127.0.0.1:8100` (env `SEWER_SIDECAR_URL`), Auth `X-Sidecar-Token`
  (env `SEWER_SIDECAR_AUTH_TOKEN` oder Datei `%LOCALAPPDATA%\SewerStudio\.sidecar_token`).

## Optionen
- `--priority <keys.json>` : Liste von Befund-Keys (`haltung|zeit|code`) zuerst zeigen
  (z.B. die vom Clean-Retrain als falsch entfernten Befunde — genau die brauchen einen Rescue-Frame).
- `--limit N` : nur die ersten N Befunde laden.
- `--port P` : anderer Port (Default 8200).

## Leitplanke
Nur **du** bestaetigst Codes — die Maschine erfindet keine Labels. Unsicher/Skip landen NICHT im Gold-Satz.
