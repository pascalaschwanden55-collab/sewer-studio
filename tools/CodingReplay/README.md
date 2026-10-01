# Bildvergleich des Codiermodus

Dieses Werkzeug zeigt, welche Ereignisvorschläge aus bereits geprüften Bildern
entstehen. Es nutzt den bestehenden Mehrmodell-Einzelbildweg des Players:
Meter lesen, Klassifikation/DINO/SAM, räumliche Filter, Codezuordnung und
Ereignisregeln. Die normale App wird dabei weder gestartet noch beendet.

## Vorbereiten

```powershell
dotnet build tools\CodingReplay\CodingReplay.csproj -c Release --no-restore
dotnet tools\CodingReplay\bin\Release\net10.0-windows10.0.19041\CodingReplay.dll prepare `
  --eval-root C:\KI_BRAIN\eval_set `
  --review C:\KI_BRAIN\eval_review\v1_event_metadata_review.json `
  --project 'D:\Projekte\Zone 1.15\Projektdateien\projekt.json' `
  --output 'C:\Users\Besitzer\Documents\SewerStudio-KI-Strategie-20260920\Messungen'
```

Der Befehl gibt einen neuen Paketordner aus. `preparation.json` nennt eingeschlossene
Bilder, fehlende Stammdaten und Einzelfehler. Kandidatenliste und Bilder müssen zum
eingefrorenen Manifest passen; die vorhandene Review wird durch den bestehenden
`EvalReviewedDamageDataset` geprüft. Das Paket bindet vier Quellen mit SHA-256.
Aus dem Projekt gehen nur eindeutig zugeordnete Nennweite und Haltungslänge in die
Analyse. Sollcodes, Protokolle und Referenz-Meterwerte werden nicht übergeben.

## Lokal messen

Ollama und der normale lokale Sidecar müssen laufen. Das Werkzeug installiert,
startet oder trainiert keine Modelle und ändert keine Einstellungen. Eine erste
Bildanfrage kann nach dem Laden noch langsam sein; Kalt- und Warmlauf getrennt
aufbewahren. Der OSD-Leser behält sein produktives Limit von acht Sekunden.

```powershell
dotnet tools\CodingReplay\bin\Release\net10.0-windows10.0.19041\CodingReplay.dll run `
  --package '<der ausgegebene Paketordner>' `
  --output 'C:\Users\Besitzer\Documents\SewerStudio-KI-Strategie-20260920\Messungen' `
  --catalog 'C:\Sewer-Studio_KI_5.0\src\AuswertungPro.Next.UI\Data\vsa_kek_2020_catalog_manifest.json' `
  --limit 6 --max-minutes 20
```

`--limit` verwendet die ersten N Fälle in der festgeschriebenen Reihenfolge,
keine Zufallsstichprobe. Standard: sechs Bilder, maximal 20 Minuten insgesamt,
vier Minuten pro Bild. `--settings` erlaubt eine andere lokale Einstellungsdatei;
sonst wird `%LOCALAPPDATA%\SewerStudio\settings.json` schreibgeschützt gelesen.
Der Mehrmodellweg muss dort bzw. über die bestehenden Umgebungswerte aktiv sein.
Parallelstarts dieses Werkzeugs sind gesperrt. Andere GPU-Nutzer bleiben unberührt.

Jeder Lauf schreibt einen neuen Ordner mit:

- `bericht.html`: Bilder, Referenz in Klartext, Programmvorschläge und aufklappbare Schritte.
- `summary.json`: technische Abdeckung und Vergleich der Codevorschläge je Bild.
- `<Fall-ID>.json`: Einzelbeleg mit Meterantwort, Modellantworten und Codezuordnung.
- `osd-inputs/<Fall-ID>.png`: genau das an den Meterleser übergebene Suchbild,
  auch im HTML aufklappbar. Keine neu beschnittenen Kundenoriginale.
- `runtime.json`: effektive Einstellungen ohne Zugangstoken, Prompt, Katalog- und
  Assembly-Prüfsummen sowie Startzustand des Sidecars.
- `package.json`: Kopie der Paketbeschreibung mit Herkunft und Referenzen.

Exitcode 0 bedeutet technisch durchgelaufen, **keine fachliche Freigabe**. Exitcode 2
meldet Fehler oder unvollständige Messung. Bereits abgelegte Ergebnisse bleiben
erhalten. Es gibt noch keinen automatischen Wiederanlauf oder Zeitplaner. Für einen
erneuten Lauf denselben Paketordner verwenden; Ausgaben werden nie überschrieben.

## Grenzen

Jedes Bild startet mit leerer Ereignisliste, ohne importierte Referenzbefunde,
Kalibrierung oder vorherige Meterwerte. Ohne gelesenen Meterstand wird der Fall als
nicht auswertbar erfasst. Im vollständigen Player existieren zusätzlich Timeline-
und Verlaufspfade; ihr Verhalten ist damit nicht gemessen.

Nicht enthalten sind Vorabdurchlauf, Qwen-only-Modus, Video-Bildauswahl, Live-Takt,
bediente WPF-Oberfläche, Speicherung und zeitliche Ereigniszusammenführung.
Streckenschadenregeln werden innerhalb eines Bildes angewendet, beweisen aber
keine zeitliche Erkennung. `EventsAdded` ist ein interner Workflowausgang und kann
null tatsächlich erzeugte Vorschläge enthalten; entscheidend ist `Events`.

Die Bilder sind historisch bekannt. Ein gleicher Hauptcode ist kein genauer
Untercode; Zusatzcodes sind zunächst Prüfhinweise. Die Zahlen sind keine
Ereignis-Trefferquote oder Modellfreigabe. YOLO-Qualifikation wird nicht umgangen.
Ein Modellfehler oder `ReviewRequired` zählt niemals als bestätigter Negativfall.
Die Gewichte des Sidecars sind noch nicht vollständig gebunden; das beantwortende
Klassifikationsmodell liefert bereits SHA-256 und Vorverarbeitung in seiner Spur.

Originale und `KI_BRAIN` werden nur gelesen. Das neue Application-UseCase liegt in
`Application/UseCases/CodingReplay`; der eigenständige Windows-Host nutzt bestehende
öffentliche Player-Workflows. Keine neue ServiceProvider-Registrierung und keine
neuen NuGet-Pakete. Der injizierte Trainingsspeicher sperrt jeden Zugriff.

Tests: `CodingReplayUseCaseTests`, `CodingReplayComparisonTests`,
`CodingReplayAnalyzerTests`. Sie verwenden künstliche Fälle und keinen Kundenbestand.

## Sequenzieller Videonachlauf

Der Videoweg hält pro Haltung genau eine Codier-Sitzung. Meterverlauf,
Bildbereitschaft, Streckenschaden-Tracker, Endprüfung und `session.Events` bleiben
über alle Frames erhalten. Der bisherige Einzelbildweg bleibt unverändert.

Das Paket für den ersten geprüften Pilot wird rein mit CPU und `ffmpeg` vorbereitet:

```powershell
dotnet tools\CodingReplay\bin\Release\net10.0-windows10.0.19041\CodingReplay.dll prepare-video `
  --video 'D:\Projekte\Zone 1.15\Haltungen_Verteilt\80685-80706\20250926_80685-80706.mpg' `
  --eval-root C:\KI_BRAIN\eval_set `
  --review C:\KI_BRAIN\eval_review\v1_event_metadata_review.json `
  --project 'D:\Projekte\Zone 1.15\Projektdateien\projekt.json' `
  --catalog C:\Sewer-Studio_KI_5.0\src\AuswertungPro.Next.UI\Data\vsa_kek_2020_catalog_manifest.json `
  --holding 80685-80706 `
  --ffmpeg '<Pfad zu ffmpeg.exe>' `
  --output 'C:\Users\Besitzer\Documents\SewerStudio-KI-Strategie-20260920\Messungen' `
  --step-seconds 5
```

`ffprobe` liest Dauer, Auflösung und Bildrate. `ffmpeg` erzeugt die vorab feste
Folge 0, 5, 10, … bis zum letzten vollständigen Schritt vor Videoende. Beim
Pilotvideo sind das 59 PNG-Bilder bis 290 Sekunden. Das Werkzeug bindet Video,
Review, Kandidatenliste, Projekt, Katalog, `ffmpeg` und `ffprobe` vor dem Auslesen
mit SHA-256. Es prüft danach erneut, dass die Quellen gleich geblieben sind.

Der KI-Lauf erfolgt später bewusst mit:

```powershell
dotnet tools\CodingReplay\bin\Release\net10.0-windows10.0.19041\CodingReplay.dll run-video `
  --package '<der ausgegebene Videopaketordner>' `
  --output 'C:\Users\Besitzer\Documents\SewerStudio-KI-Strategie-20260920\Messungen' `
  --catalog C:\Sewer-Studio_KI_5.0\src\AuswertungPro.Next.UI\Data\vsa_kek_2020_catalog_manifest.json `
  --max-minutes 300 --frame-timeout-minutes 4
```

Der Lauf schreibt für jedes Bild die Meterherkunft, die unveränderte
Detektorqualifikation, Modell- und Boxnachweise sowie neue, geänderte und
geschlossene Ereignisse mit stabiler `EventId`. Fehlendes OSD nutzt die bestehende
Regel: gleiches Bild, höchstens 1,5 Sekunden alter Wert, danach Zeitabschätzung.
Der Bericht bewertet nur BAIZ bei 10,817 m / 132,03 s und BAHC bei 12,602 m /
160,86 s als bestehende menschliche Ereignisanker. Weitere Ereigniszeilen heißen
ausdrücklich „unbewertet“. Es wird keine vollständige Precision oder Recall
berechnet und kein Kandidat freigegeben.

Der feste Nachlauf misst keine ausgelassenen Ticks eines laufenden Players. Das
bleibt ein eigener zweiter Test mit echter Wiedergabe- und Busy-Steuerung.

### Entwicklungs-Kandidaten vergleichen

Ein separat erzeugter Detektionsbeleg kann denselben Ablauf speisen, ohne das
Kandidatengewicht im Sidecar zu aktivieren:

```powershell
dotnet tools\CodingReplay\bin\Release\net10.0-windows10.0.19041\CodingReplay.dll run-video-candidate `
  --package '<Videopaketordner>' `
  --candidate-detections '<candidate-detections.json>' `
  --candidate-sha256 '<SHA-256 der tatsaechlich verwendeten Gewichte>' `
  --output '<neuer Ausgabe-Stammordner>' `
  --catalog '<VSA-Katalogmanifest>'
```

Der Leser akzeptiert nur Schema 1 mit Zweck
`development_candidate_video_detection`. `candidate_id` muss `model.id`
entsprechen, `model.role` muss `ref43_anchor`, `kontrolle` oder `spiegelung`
sein. Erwartete und tatsaechliche Gewichts-SHA müssen beide der ausdruecklichen
CLI-SHA entsprechen. Protokollwerte sind fest `confidence=0.25` und
`image_size=1280`. Alle Frames, Bild-SHA, Bildmasse und ihre Reihenfolge muessen
genau zum Videopaket passen.

Dieser Weg bleibt immer `development_candidate_unqualified`; alle daraus
stammenden Befunde verlangen eine manuelle Pruefung. Ein `technical_error` ist
ein technischer Fehler und nie ein Negativbefund. Der Lauf nutzt weiterhin
Klassifikator, DINO, SAM und OSD des lokalen Codierwegs. Er darf deshalb erst
gestartet werden, wenn die GPU dafuer frei ist. `runtime.json`, jede Framespur
und die Ereignisbelege halten Kandidatenkennung, Rolle und Gewichts-SHA fest.
