# SewerStudio: erweitertes Code-Audit und Verbesserungsplan

> Veröffentlichung am 24.09.2026: Dieser Bericht dokumentiert die Prüfung vom 23.09.2026. Seitdem erfolgte Produktänderungen sind nicht nachgeprüft. Die beigefügten Quellen und Ergebnisse sind historische Nachweise. Die ursprünglichen ausführbaren Prüfprojekte und vollständigen Build-/Testprotokolle unter `.tmp` bleiben lokal; Befehle mit diesen Pfaden benötigen diese lokale Prüfumgebung.

**23.09.2026 · Prüfauftrag: untersuchen und planen · keine Produktänderungen**

Ausgangsstand: `feature/webgis-uebertragung`, Commit `927c9f1d3`, einschliesslich vorhandener lokaler Änderungen.

## 1. Ergebnis und Entscheidungsvorschlag

**Die grössten Verbesserungen liegen bei verlässlichen Abläufen, korrekten Bildmarkierungen und weniger unnötiger Arbeit in der Oberfläche.**

Die vorhandene RTX 5090 wird grundsätzlich unterstützt. WPF, Videowiedergabe und KI verwenden unterschiedliche Grafikwege.
Eine neue Engine behebt die gefundenen Fehler in diesen Wegen nicht automatisch.

Meine empfohlene Reihenfolge:

1. Sicherung, Speichern und WebGIS-Schreiben zuverlässig absichern.
2. Falsche KI-Anzeigen, verschobene Markierungen und Rechenfehler korrigieren.
3. Kleine Verbesserungen für Lesbarkeit, Bedienung und Fehlerdiagnose umsetzen.
4. Bildverarbeitung und Grafikkartennutzung messen und gezielt verbessern.
5. Eine zusätzliche 3D-Ansicht anhand eines kleinen Vergleichsversuchs auswählen.

**Technische Empfehlung:** WPF als Hauptoberfläche behalten. Für einen eingebetteten technischen 3D-Betrachter zuerst HelixToolkit prüfen.
Für eine eigenständige 3D-Erkundung ist Godot mein bevorzugter Vergleichskandidat unter den drei genannten Spiele-Engines.
Unity bleibt eine Alternative bei späterem VR-/AR-Bedarf. Unreal wird interessant, wenn fotorealistische Präsentation ein eigener Produktzweck wird.
Diese Auswahl ist eine Einschätzung zum vorliegenden Programm, kein gemessener Engine-Vergleich.

### Was neu geprüft wurde

- **7 zusätzliche Fehlerbefunde:** sechs durch Gegenproben, einer durch den vollständigen Kontrollfluss des Werkzeugs belegt.
- Zusätzlich konkrete Leistungs- und Bedienungsschwächen, darunter die Fotogalerie mit 1'000 gleichzeitig erzeugten Einträgen.
- **12 kleine Arbeitspakete** sowie grössere Verbesserungen mit Abnahmekriterien.
- Vergleich von WPF, HelixToolkit, SkiaSharp, Godot, Unity, Unreal, DLSS und RTX Video.
- Grafikkarte und installierte CUDA-/PyTorch-Unterstützung gelesen. Keine Modelle oder Trainingsläufe gestartet.

Der [Basisbericht](BASISAUDIT.md) enthält die bisherigen 18 Befunde und ihre Nachweise.
Zusammen dokumentieren beide Prüfungen **25 Befunde**. Das bedeutet wegen parallel laufender Korrekturen nicht „25 aktuell unverändert offene Fehler“.

### Gleichzeitige Änderungen beachten

Zu Beginn waren alle im Basis-Audit erfassten Quelltexte unverändert. Während dieser Erweiterung wurden WebGIS-Schnittstelle,
Client, Modelle, Ablauf und Tests von anderer Arbeit verändert. Die neuen Änderungen wurden nicht von diesem Audit geschrieben.
Eine zusätzliche Klasse für den Standvergleich kam ebenfalls hinzu.

Bis zum Abschluss wurden auch Sicherung, Quellenpriorität, KINS-Import sowie Kosten- und Hydraulikberechnung parallel verändert.
Die betroffenen Dateinamen sind im Nachweis `pruefumfang.json` festgehalten. Auch diese Korrekturen sind hier noch nicht abgenommen.

Im Client ist inzwischen eine Prüfung gegen einen erwarteten Ausgangsstand sichtbar. Das betrifft A04.
Solange der gesamte neue Ablauf nicht abgeschlossen und erneut geprüft ist, wird daraus keine Freigabe abgeleitet.
**Nach Abschluss der parallelen Arbeit die betroffenen Basisbefunde erneut prüfen**, insbesondere A01, A04–A07, A12/A13 und A16/A17.
Die Basisbefunde sind eine Liste nachzuweisender Korrekturen; bereits parallel erledigte Arbeit soll nicht nochmals umgesetzt werden.
Die zusätzlichen Grafik- und DINO-Gegenproben betreffen getrennte Dateien.

## 2. Umfang, Belege und Grenzen

Die automatische Bestandsaufnahme erfasst **6'244 C#-, Python- und XAML-Dateien mit 824'261 physischen Zeilen**.
Enthalten sind Programm, Werkzeuge und Tests; Kommentare und Leerzeilen zählen mit.

| Bereich | Dateien | Zeilen | Vertiefung |
|---|---:|---:|---|
| Vier Hauptprojekte unter `src` | 3'314 | 402'093 | Daten, Fachregeln, Infrastruktur, WPF, Video und Anzeige |
| Produktiver Python-Sidecar | 32 | 7'042 | Gerätewahl, GPU-Zulassung, DINO/SAM/YOLO, Fehlerzustände |
| QGIS-Integration einschliesslich dortiger Tests | 7 | 1'968 | Bestehender Basis-Testlauf, Geometrie-Cache und Aufrufer |
| Werkzeuge | 223 | 43'141 | Breite Suche, vertiefte Stichproben bei Datei- und Prüfabläufen |
| .NET-Tests | 2'603 | 359'001 | Basis-Gesamtlauf und zusätzliche Grafiktests |
| Python-Tests | 60 | 10'618 | Basis-Gesamtlauf und zusätzliche fokussierte Tests |

Weitere fünf Python-Dateien mit 398 Zeilen liegen direkt im Sidecar, unter dessen Modellordner und Sicherheitsprüfung.
Das erklärt die Differenz zur Gesamtsumme.

**Dies ist ein projektweites Audit mit gezielter Vertiefung. Nicht jede Zeile wurde einzeln manuell gelesen.**
Suchtreffer wie leere Fehlerblöcke sind Hinweise für die Untersuchung, keine automatisch gezählten Fehler.
Grafikleistung, reale Inferenzgeschwindigkeit und alle Monitor-/Treiberkombinationen wurden nicht vermessen.

### Prüfungen in dieser Erweiterung

| Prüfung | Ergebnis |
|---|---|
| 46 bestehende UI-Tests zu Overlays, Bildmasken, Galerie und Bewegung | 46 bestanden |
| Fokussierter Python-Lauf für DINO, SAM und GPU-Verwaltung | 8 bestanden, 2 echte GPU-Tests ausgeschlossen |
| Eigene WPF-Gegenproben mit echten Programmklassen | Verschiebung, Ausnahme, verschwundene Kontur, Cache-Altstand und Kontrast belegt |
| DINO-Vertrag gegen kontrollierte Abhängigkeit | Falsche Geräteweitergabe belegt; Standardwert der tatsächlich installierten Bibliothek gelesen |
| Galerie mit künstlichen Einträgen | 1'000 von 1'000 Einträgen als Steuerelemente erzeugt |

Die Gegenproben geben beobachtetes Fehlverhalten aus. Ihr erfolgreicher Prozessabschluss bedeutet nicht „Schutz bestanden“.
Es wurden keine Kundenbilder verwendet. Die WPF-Proben liefen in separaten Fenstern ausserhalb des sichtbaren Bildschirmbereichs.
Die Quelltexte der Proben sind Prüfwerkzeuge, noch keine fest eingebauten Regressionstests.

Der vollständige Build und die breiten Tests stammen aus dem Basis-Audit desselben Tages:
Build 0 Fehler / 5 Warnungen; Infrastructure 7'253 bestanden / 1 sporadischer Fehler / 6 übersprungen;
Pipeline 2'839 bestanden / 3 übersprungen; UI 7'412 bestanden / 16 Fehler / 32 übersprungen;
ProjectModernizer 62 bestanden; Python ohne GPU 572 bestanden; QGIS 14 bestanden.
Der einzelne Infrastructure-Fehler bestand beim Wiederholen. Diese Werte sind **keine Abnahme der danach parallel geänderten Dateien**.

## 3. Neue Fehlerbefunde

### B01 — KI-Markierungen berücksichtigen das tatsächliche Videorechteck nicht überall

**Priorität: hoch. Nachweis: echte WPF-Zeichnung mit künstlicher Geometrie.**

Ein 4:3-Video in einer Fläche von 1'600 × 900 hat seitlich je 200 Bildpunkte Rand.
Der gemeinsame Umrechner berücksichtigt das korrekt. Rechtecke, Linien und Punkte verwenden jedoch die ganze Zeichenfläche.
Andere Werkzeuge erreichen den vorhandenen Umrechner.

Gegenprobe für ein Rechteck von 20 % bis 40 % Bildbreite:

| Wert | Erwartet | Tatsächlich |
|---|---:|---:|
| Linker Rand | 440 | 320 |
| Breite | 240 | 320 |
| Aufrufe des übergebenen Umrechners | erforderlich | 0 |

Beim passenden 16:9-Video stimmt dieselbe Probe. Der Fehler hängt somit am abweichenden Seitenverhältnis.
Eine Schadensmarkierung kann auf der falschen Stelle liegen. Die Gegenprobe behauptet keinen Fehler in den gespeicherten Originalkoordinaten.

**Fundstellen:** `src/AuswertungPro.Next.UI/Player/CodingAiOverlayRenderer.cs:82`, `:85`;
`CodingAiRectangleOverlayRenderer.cs:36`; `CodingAiPrimitiveOverlayRenderer.cs:49`.
Der echte Player liefert bereits `CodingNormToPixel` über `PlayerWindowControllerSetFactory.cs:174`.

**Korrektur:** Alle Geometrien über dieselbe Bildfläche abbilden. Auch Labelpositionen daran ausrichten.
**Abnahme:** 4:3, 16:9, Hochformat, unterschiedliche Pixel-Seitenverhältnisse, Fensterwechsel und 100–200 % Windows-Skalierung.
Ein gespeicherter Bildpunkt muss nach jeder Grössenänderung dieselbe Bildstelle treffen.

### B02 — Beschriftung einer KI-Box wirft bei kleiner Fläche einen Fehler

**Priorität: mittel. Nachweis: echter Renderer.**

Die Beschriftung ist manchmal breiter als die verfügbare Fläche.
Dann erhält `Math.Clamp` eine Obergrenze unterhalb der Untergrenze.
Eine 20 × 20 grosse Fläche ergibt reproduzierbar `ArgumentException`; 1'600 × 1'600 funktioniert.
Der Player beginnt seinen Overlay-Canvas sogar mit Breite und Höhe 1, bevor das Layout steht.
Ein Absturz des vollständigen Players wurde damit nicht provoziert; belegt ist die Ausnahme im echten Zeichenweg.

**Fundstellen:** `src/AuswertungPro.Next.UI/Player/CodingAiRectangleOverlayRenderer.cs:86`, `:87`;
`src/AuswertungPro.Next.UI/Views/Windows/PlayerWindow.xaml` beim `CodingOverlayCanvas`.

**Korrektur:** Bei fehlendem Platz Label ausblenden oder kürzen; gültige Grenzen vor dem Begrenzen bilden.
**Abnahme:** Grössen 0, 1, 20 und normale Fenster; lange Beschriftung; wiederholtes Minimieren und Vergrössern.

### B03 — DINO erhält die ausgewählte CPU/GPU nicht

**Priorität: mittel; bei CPU-Rückfall oder mehreren GPUs relevant. Nachweis: echter Wrapper und installierter Bibliotheksvertrag.**

Der Wrapper lädt das Modell auf das gewählte Gerät, lässt beim anschliessenden `predict` aber `device` weg.
Die installierte Grounding-DINO-Bibliothek verwendet dann `cuda` und verschiebt Modell und Bild dorthin.

| Angefordert und verwaltet | Tatsächlich an `predict` wirksam |
|---|---|
| `cpu` | `cuda` |
| `cuda:1` | `cuda` |
| `cuda:0` | `cuda` |

Auf dem Rechner mit einer einzigen RTX 5090 kann der Normalfall funktionieren.
Der CPU-Rückfall und die Wahl einer anderen GPU werden trotzdem nicht eingehalten.
Im CPU-Fall erreicht das Modell dadurch einen anderen Speicherweg als die GPU-Zulassung erwartet.

**Fundstellen:** `sidecar/sidecar/models/dino_wrapper.py:151`;
lokal installierte `groundingdino/util/inference.py:51` mit `device="cuda"`, danach `model.to(device)`.

**Korrektur:** Das bereits aufgelöste Gerät ausdrücklich an `predict` weitergeben.
**Abnahme:** CPU ohne CUDA, CUDA 0 und simulierte zweite GPU; Ladegerät und Rechengerät müssen übereinstimmen.
Der allgemeine GPU-Manager liest seine Kapazität heute ebenfalls von Gerät 0. Eine vollständige Mehr-GPU-Auswahl braucht deshalb zusätzliche Arbeit.

### B04 — Schmale SAM-Schadenskonturen können vollständig verschwinden

**Priorität: mittel. Nachweis: echte Maskenzeichnung.**

Für die Anzeige wird eine Maske standardmässig auf 480 Bildpunkte Breite reduziert.
Dabei werden einzelne Quellpixel ausgewählt. Eine schmale Struktur zwischen diesen Pixeln entfällt vollständig.

Gegenprobe: Maske 960 × 540, eine 340 Pixel lange und einen Pixel breite Linie.
Bei X=1 ist die verkleinerte Kontur leer. Bei X=2 ist sie sichtbar.
Mit voller Auflösung sind beide sichtbar. Die gespeicherte Maske und ihre fachliche Vermessung wurden nicht verändert.

**Fundstellen:** `src/AuswertungPro.Next.UI/Ai/Pipeline/SamMaskRenderer.cs:125`, `:135`, `:432`;
`src/AuswertungPro.Next.Infrastructure/Ai/Pipeline/SamMaskDecoder.cs:71`.

**Korrektur:** Kontur aus der Originalmaske gewinnen und erst die Geometrie skalieren.
Alternativ einen geprüften, strukturerhaltenden Anzeigeweg verwenden. Eine gröbere Anzeige darf keine neue Messgrundlage werden.
**Abnahme:** Dünne horizontale, vertikale und diagonale Risse, einzelne Punkte, Löcher und Bildrand; Verschieben um einen Pixel darf keinen Befund unsichtbar machen.

### B05 — Geometrie-Cache erkennt geänderten Inhalt bei gleicher Dateizeit nicht

**Priorität: mittel. Nachweis: tatsächliche Cache-Basisklasse mit künstlichem Extractor.**

Der Cache prüft Pfad, Formatversion und Änderungszeit. Inhalt und Dateigrösse werden nicht geprüft.
Wird eine Datei durch andere Inhalte mit derselben Zeit ersetzt, liefert er weiter die alte Geometrie.
Die Probe hält zusätzlich die Länge gleich. Ein blosser Grössenvergleich würde diesen Fall ebenfalls nicht lösen.

**Fundstellen:** `src/AuswertungPro.Next.Infrastructure/Map/XtfJsonGeometryCache.cs:41`, `:50`;
echte Aufrufer `src/AuswertungPro.Next.UI/QgisBridge/QgisBridgeSnapshotBuilder.cs:412`, `:424`.

**Korrektur:** Bei Quellenübernahme eindeutig invalidieren oder eine Inhaltskennung für die geladene Quelldatei prüfen.
Den grossen Kataster nicht bei jedem Zeichenschritt erneut hashen.
**Abnahme:** Gleicher Pfad, gleiche Länge und gleiche Zeit bei geändertem Inhalt; unveränderte Quelle bleibt schnell.

### B06 — Grüner Text im dunklen Player ist zu kontrastarm

**Priorität: mittel für Lesbarkeit. Nachweis: tatsächliche Theme-Ressourcen und konkrete Textverwendung.**

Die Legende „≥85“ verwendet `SuccessBrush` auf `CardGlassBrush`, bei Schriftgrösse 11.
Gemessener Kontrast der deckenden Farben: **3,16:1**.
Die bereits vorhandene Ressource `SuccessTextBrush` ergibt dort **8,23:1**.

**Fundstellen:** `src/AuswertungPro.Next.UI/Views/Windows/PlayerCodingSidePanel.xaml:36`;
`src/AuswertungPro.Next.UI/Theme/Theme.xaml:48`, `:90`.

**Korrektur:** Vorhandene Textfarben konsequent für Text verwenden. Status zusätzlich ausschreiben oder mit Symbol kennzeichnen.
**Abnahme:** Alle Statuslegenden im hellen und dunklen Modus prüfen, einschliesslich Auswahl und deaktivierter Zustände.
Als Gestaltungsziel für kleine normale Schrift gelten 4,5:1; dieser Einzeltest ist keine vollständige Barrierefreiheitszertifizierung.
[W3C: Mindestkontrast](https://www.w3.org/WAI/WCAG22/Understanding/contrast-minimum.html)

### B07 — Ein Verteilungs-Prüfwerkzeug endet auch bei gemeldeten Fehlern erfolgreich

**Priorität: mittel für verlässliche Prüfabläufe. Nachweis: vollständiger Kontrollfluss, nicht ausgeführt.**

`DichtheitDistributeTest` zählt fehlgeschlagene Verteilergebnisse und schreibt sie auf die Konsole.
Nach beiden Durchläufen gibt das Programm trotzdem immer Prozesscode 0 zurück.
Ein automatischer Prüflauf kann damit scheitern und zugleich Erfolg signalisieren.
Zusätzlich löscht das Werkzeug feste temporäre Ausgabeordner beim Start und verschluckt Löschfehler.
Zwei gleichzeitige Läufe besitzen somit keinen eigenen Ausgabeordner.

**Fundstellen:** `tools/DichtheitDistributeTest/Program.cs:44`, `:49`, `:63`.

**Korrektur:** Fehlerzahl an den Einstieg zurückgeben und bei Fehlern einen Fehlercode liefern.
Jeder Lauf erhält einen eindeutig eigenen Ausgabeordner. Keine Kundenpfade als Standard-Prüfdaten verwenden.
**Abnahme:** Ein absichtlich nicht lesbares synthetisches PDF führt zu einem Fehlercode. Zwei Läufe beeinflussen sich nicht.
Das Werkzeug wurde wegen seiner fest eingetragenen realen Datenpfade im Audit nicht gestartet.

## 4. Leistungs- und Bedienungsschwächen

Diese Punkte sind nicht als zusätzliche Funktionsfehler gezählt. Ein Teil ist gemessen, ein Teil braucht einen Leistungsversuch.

| Punkt | Tatsächlicher Befund | Sinnvolle Verbesserung | Noch zu messen |
|---|---|---|---|
| Fotogalerie | Echter `WrapPanel` erzeugt 1'000 Einträge bei 1'000 Fotos; sichtbar ist nur ein Ausschnitt. Laden erfolgt im Bild-Konverter. | Nur sichtbare Kacheln samt kleinem Puffer erzeugen; Vorschaubilder im Hintergrund laden und begrenzt zwischenspeichern. | Öffnungszeit, Scroll-Verzögerung, Speicher bei 50/500/2'000 Bildern. |
| Abgedockte Galerie | Der innere Bereich bleibt auch im 520 hohen Fenster maximal 284 hoch. | Höhenbegrenzung nur für die eingebettete Galerie; eigenes Fenster vollständig nutzen. | Bedienprüfung verschiedener Fenstergrössen. |
| Bewegung reduzieren | Einstellung wirkt laut vorhandenem Code erst beim nächsten Aufbau bereits offener Ansichten. | Änderungsereignis für aktive Animationen, sauber anmelden und abmelden. | Sofortiger Stillstand, kein verbleibender Zeittakt. |
| KI-Markierungen | Elemente werden beim erneuten Zeichnen entfernt und neu angelegt; Schatten werden neu erstellt. | Nach Behebung B01/B04 Geometrie und unveränderte Darstellungsdaten wiederverwenden. | Wiederholtes Zeichnen bei vielen Befunden; Aufwand auf dem UI-Thread. |
| SAM-Anzeige | Maske wird dekodiert; Füllung und Kontur reduzieren sie nochmals getrennt. | Aufbereitung pro Maske und Auflösung einmal; begrenzter Zwischenspeicher. | Zeitanteil an einem echten Befundbild. |
| YOLO-Ausgabe | Pro Box mehrere `.cpu()`-/`.item()`-Zugriffe; Klassifikator liest Werte ebenfalls einzeln zurück. | Ergebnisse gesammelt zur CPU übernehmen; Reihenfolge und Datentypen erhalten. | Gewinn abhängig von Anzahl der Boxen, möglicherweise klein. |
| DINO-Bildvorbereitung | Neues Transform-Objekt pro Aufruf; vollständige NumPy-Kopie vor allem zur Grössenabfrage. | Stabile Vorverarbeitung wiederverwenden, Bildgrösse direkt lesen; Auflösungsgrenze gesondert prüfen. | CPU-Zeit und Speicher; Genauigkeit bei geänderter Auflösung. |
| Videobilder für die KI | Persistenter ffmpeg-Prozess ist vorhanden; Ausgabe läuft über PNG und CPU-Speicher. Kein `-hwaccel` im untersuchten Stream. | Erst Extraktion, PNG-Kodierung, Übertragung und Modellzeit getrennt messen. | Hardwaredekodierung kann bei seltenen Einzelbildern auch wenig bringen. |
| Live-Snapshot | Separater Weg schreibt PNG temporär und wartet pauschal 80 ms. | Fertigmeldung/gebundenes Bild abwarten; unnötige Dateirundreise später prüfen. | Bildalter, Ausfälle, Abbruch und Aufnahmetreue. |
| Video-Rückfall | `PlayerLibVlcFactory` verwirft Fehler der benutzerdefinierten Initialisierung und startet Standard-VLC. | Effektiven Rückfall protokollieren und in Diagnose anzeigen. | Keine Behauptung, dass DXVA2 auf dieser Karte langsam oder defekt ist. |

Fundstellen: `PhotoGalleryPanel.xaml:14`, `:55`; `PhotoGalleryPanel.xaml.cs:134`;
`SettingsPageViewModel.cs:392`; `CodingAiOverlayRenderer.cs:47`;
`SamMaskRenderer.cs:414`; `yolo_wrapper.py:504`, `:807`; `dino_wrapper.py:121`;
`VideoFrameStream.cs:49`; `LiveDetectionFrameCaptureService.cs:13`; `PlayerLibVlcFactory.cs:11`.

### Was bereits sinnvoll gelöst ist

- Haltungs- und Schachttabellen haben Zeilen- und Spaltenvirtualisierung sowie Wiederverwendung eingeschaltet.
- Die Fotogalerie lädt bereits kleine Vorschaubilder mit `DecodePixelWidth=200` und gibt Dateizugriffe nach dem Laden frei.
- Ein persistenter ffmpeg-Prozess vermeidet im untersuchten Stapelweg einen Prozessstart pro Bild.
- Der animierte Netz-Hintergrund verwendet bereits einen Formen-Pool und stoppt bei inaktivem Fenster.
- SAM-Bildzustand und Modellaufrufe haben Sperren; GPU-Wächter und kontrollierte Fehlerantworten sind vorhanden.
- Die Windows-Grafik- und CUDA-Unterstützung muss nicht erst grundsätzlich eingebaut werden.

## 5. Quick Wins: kleine, abgegrenzte Arbeitspakete

Vor jedem Paket den aktuellen Stand prüfen: Einige Basisfehler werden bereits parallel korrigiert.
Die Zeitangaben sind grobe Entwicklungsaufwände für Umsetzung und fokussierte Prüfung durch eine mit dem Projekt vertraute Person.
Sie sind keine Termine. Breiter Freigabelauf, Konflikte mit paralleler Arbeit und reale Bedienabnahme kommen hinzu.

| Reihenfolge | Paket | Aufwand grob | Abnahme |
|---|---|---|---|
| Q01 | B06: vorhandene Textfarbe für grüne Legenden einsetzen | 1–2 Stunden | Hell/dunkel lesbar; gemessener Kontrast ausreichend. |
| Q02 | B02: Labels bei kleiner Fläche sicher platzieren | 2–4 Stunden | Keine Ausnahme bei 0–20 Pixeln und langen Labels. |
| Q03 | B03: DINO-Gerät vollständig durchreichen | 3–6 Stunden | CPU/GPU-Gerät im echten Aufrufvertrag gleich. |
| Q04 | A14: Feldnamen vor Begriffsumwandlung vereinheitlichen | 2–4 Stunden | `Status` und unterstütztes `STATUS` ergeben denselben Wert. |
| Q05 | A15: vorhandenen Schachtwert im Auswahlfeld sichtbar halten | 2–6 Stunden | Gültiger Wert und Altwert bleiben beim Öffnen sichtbar. |
| Q06 | A12: Kurzliner vor allgemeinem Liner abgleichen | 3–6 Stunden | Zusatztext ändert Stückpreis nicht in Meterpreis. Feste Kennungen folgen später. |
| Q07 | A18: offizielle Alias-Kennungen im Paketprüfer berücksichtigen | 3–5 Stunden | Bekannte und wirklich neue Lücken richtig unterscheiden. |
| Q08 | VLC-Rückfall mit Ursache protokollieren | 1–3 Stunden | Diagnose zeigt angefordert und tatsächlich zurückgefallen. |
| Q09 | Abgedockte Fotogalerie auf verfügbare Höhe ausdehnen | 1–3 Stunden | Fenstervergrösserung vergrössert den Bildbereich. |
| Q10 | Bewegungsreduktion für offene Controls sofort anwenden | 3–6 Stunden | Aktive Animation stoppt ohne Seitenwechsel; keine Ereignis-Lecks. |
| Q11 | B07: Fehlercode und eigener Prüf-Ausgabeordner | 2–4 Stunden | Fehlerlauf rot; getrennte parallele Ausgaben. |
| Q12 | Anzeigenamen der Projektprüfung verständlich machen | 1–3 Stunden | Zum Beispiel „Durchmesser“ mit technischem Namen nur in Details. |

**Nicht künstlich als Quick Win verkleinern:** Speichern unter, Sicherungsbereinigung, WebGIS-Nebenänderungen,
strukturerhaltende Maskenzeichnung und Galerievirtualisierung. Dort müssen zusammenhängende Abläufe geprüft werden.
Auch A13 braucht eine fachlich nachvollziehbare Materialzuordnung; eine weitere zufällige Textsonderregel reicht nicht.

## 6. Bessere Oberfläche: konkretes Zielbild

Für die optische Einschätzung wurden aktuelle XAML-Dateien und archivierte Screenshots vom **08.09.2026** verwendet.
Die Archivbilder zeigen Anordnung und Dichte, nicht den heute vollständig bedienten Programmstand.
Aktuelle Gegenproben betreffen einzelne echte Steuerelemente. Eine vollständige heutige Bildschirmabnahme steht aus.

### Hauptarbeitsplatz „Haltungen“ und „Schächte“

| Bereich | Ziel |
|---|---|
| Kopfzeile | Projekt, aktuelles Objekt und Speicherzustand klar zusammenhalten. |
| Hauptaktionen | Eine erkennbare Hauptaktion je Arbeitsschritt; seltene Aktionen im vorhandenen Menü. |
| Tabelle | Bestehende Ansichten behalten; Prüfung, KI-Status und Sanierung sprachlich deutlich unterscheiden. |
| Detailbereich | Ohne Auswahl platzsparender Hinweis; mit Auswahl veränderbare Breite und Höhe. |
| Herkunft | Bei wichtigen Werten erkennbar: Hand, Kanalfirma, Kataster oder WebGIS. Details auf Wunsch. |
| Fehler | Objekt und konkrete Handlung nennen: „Status unbekannt — Auswahl korrigieren“. |
| Suche | Trefferzahl und aktive Filter sichtbar; vorhandene Tastaturkürzel einheitlich anzeigen. |

Die im Archiv sichtbaren grossen leeren Detailflächen sollten bei fehlender Auswahl zusammenfallen.
Das ist ein Gestaltungsauftrag; vor Umsetzung prüfen, ob neuere Ansichten dies bereits lösen.
Bestehende Spaltenprofile, Aufklapplisten und persönliche Fenstergrössen weiterverwenden.

### Videoarbeitsplatz

Empfohlene Anordnung als Plan, noch nicht umgesetzt:

```text
Projekt / Haltung                 Gespeichert · Video verbunden
┌───────────────────────────────────┬──────────────────────────┐
│                                   │ Befunde                  │
│ Video in richtigem Seitenformat   │ Offen / geprüft / Fehler │
│ Bildgetreue Boxen und Konturen     │                          │
│                                   │ Ausgewählter Befund      │
│ Original / Anzeigeverbesserung*   │ Bild · Meter · Code      │
├───────────────────────────────────┴──────────────────────────┤
│ Wiedergabe · Einzelbild · Zeitachse · Meterstand              │
└──────────────────────────────────────────────────────────────┘
* Nur bei später freigegebener Anzeigeverbesserung.
```

- Video und Befundliste erhalten den meisten Platz. Diagnosen bleiben einklappbar.
- Vier verständliche KI-Zustände: „kein Befund erkannt“, „Vorschlag prüfen“, „Bild nicht auswertbar“, „Technischer Fehler“.
- Ein Klick auf den Befund zeigt das zugehörige Originalbild und seine gebundene Zeit-/Meterangabe.
- Rahmenfarbe wird durch Text oder Symbol ergänzt. Auswahl und Bearbeitungsstatus haben unterschiedliche Zeichen.
- Fachliche Markierungen bleiben stabil beim Wechsel zwischen Monitoren und beim Andocken.
- Für kleine Bildschirme einen kompakten Modus anbieten; für lange Auswertung einen ruhigen Modus ohne Dauerbewegung.

### Gestaltungsregeln

- Weniger verschachtelte Rahmen und Schatten; klare Abstände und vorhandene Theme-Ressourcen verwenden.
- Haupttext nach Bedienprobe vorzugsweise 13–14 logische Bildpunkte; kleine Angaben gezielt, nicht für kritische Zustände.
- Tastaturfokus, beschriftete Symbole und geeignete Namen für Bildschirmleser prüfen.
- Statusfarben der fachlichen Zustandsklassen erhalten. Fachfarbe und dekorativen Akzent nicht verwechseln.
- Keine störende Hintergrundanimation während Videoauswertung, längerer KI-Arbeit oder im reduzierten Bewegungsmodus.
- Zielmatrix: 1'366 × 768, 1'920 × 1'080, 4K; Windows-Skalierung 100/125/150/200 %; hell/dunkel.

WPF unterstützt hardwarebeschleunigtes Zeichnen bereits. Mehr GPU-Leistung beseitigt nicht automatisch teure Layout- und Bindungsarbeit.
[Microsoft: WPF und Hardware](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/advanced/optimizing-performance-taking-advantage-of-hardware)
Die Erzeugung nur sichtbarer Listenelemente ist ein vorhandenes WPF-Verfahren.
[Microsoft: Leistung von Steuerelementen](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/advanced/optimizing-performance-controls)

## 7. GPU-Plan für die vorhandene RTX 5090

### Gelesener Ist-Zustand

| Bestandteil | Befund |
|---|---|
| Grafikkarte | NVIDIA GeForce RTX 5090, 32'607 MiB gemeldeter Speicher |
| Treiber | 616.92 |
| PyTorch | `2.12.0.dev20260408+cu128` |
| CUDA im PyTorch-Paket | 12.8, `cuda_available=true` |
| Kompilierte Architekturen | Enthalten `sm_120` |
| Player-Standard | Hardwaredekodierung an; Ausgabe `direct3d11`; Decoderargument `dxva2` |
| Tatsächlicher Decoder je Video | Noch nicht gemessen; eine Einstellung allein beweist dessen Verwendung nicht. |
| GPU-Auslastung im Arbeitsablauf | Kein Lastprofil aufgenommen. Eine Leerlauf-Momentaufnahme wäre keine Leistungsbewertung. |

### GPU-1: Gerätewahl und Speicher zuerst korrekt machen

- B03 korrigieren und A11 zur gleichzeitigen Modellzulassung beheben.
- Freien Gerätespeicher, laufende Reservierungen und tatsächliches Rechengerät zusammen prüfen.
- Für eine künftige Mehr-GPU-Wahl darf der Speicher nicht immer von GPU 0 gelesen werden.
- Parallel laufendes Ollama, Anzeige, Videodekoder und Sidecar im Speicherplan berücksichtigen.
- Die bestehende Reserve nicht pauschal verkleinern. Den benötigten Spielraum unter echter Last messen.

### GPU-2: Arbeitszeiten sichtbar machen

Pro geprüftem Bild getrennt erfassen: Extraktion, Bildvorbereitung, Übertragung, Warten auf Modellplatz, Modellrechnung,
Maskenaufbereitung und Anzeige. Kalten Modellstart getrennt vom bereits geladenen Modell ausweisen.
Für PyTorch GPU-Zeiten korrekt synchronisieren beziehungsweise CUDA-Ereignisse verwenden.
Eine reine CPU-Uhr um einen asynchronen GPU-Aufruf kann irreführend sein.

In der Diagnose genügen verständliche Angaben: „Grafikkarte erkannt“, „Video über Hardware“, „KI auf GPU“,
„Warten auf Speicher“, „Rückfall auf CPU“. Entwicklerdetails in einen ausklappbaren Abschnitt.

### GPU-3: Vier Optimierungen einzeln vergleichen

| Versuch | Erwartbarer Nutzen | Freigabebedingung |
|---|---|---|
| YOLO-Ergebnisse gesammelt zurücklesen | Weniger kleine Synchronisationspunkte | Gleiche Boxen, Klassen, Reihenfolge und Werte. |
| SAM mit geprüftem BF16-Kontext | Weniger Rechen-/Speicheraufwand möglich | Original-Prüfbilder, keine relevante Verschlechterung dünner Schäden oder Grenzfälle. |
| Bildvorbereitung und Konturen wiederverwenden | Weniger CPU-Arbeit und UI-Pausen | Korrekte Bindung an Bild, Maske, Grösse und Modell; begrenzter Speicher. |
| Video-Hardwaredekodierung im Extraktionsweg | CPU entlasten, je Codec und Intervall unterschiedlich | Frame-Zeit, Farbe, Seitenformat und Befundbeleg unverändert nachvollziehbar. |

Der aktuelle SAM-Wrapper setzt keinen eigenen `autocast`-Kontext. Die Bibliothek schützt Teile bereits mit `no_grad`.
Deshalb nicht pauschal behaupten, es würden heute Gradienten berechnet.
Meta zeigt für SAM 2 Bildinferenz ausdrücklich `inference_mode` und BF16-Autocast.
Das macht BF16 zu einem sinnvollen Vergleichsversuch, beweist aber noch keinen Gewinn für diese Anwendung.
[Meta: SAM 2 Bildinferenz](https://github.com/facebookresearch/sam2#image-prediction)

`torch.compile`, TensorRT oder ein anderer Modellruntime-Wechsel folgen erst, wenn Profil und Qualitätstest einen Nutzen erwarten lassen.
Neue Formen, Startzeit, Windows-Unterstützung, Modellqualifikation und Rückweg gehören in diesen Versuch.
Die vorhandene Nightly-Installation bleibt bis zu einem separaten Vergleich erhalten; kein spontanes Paket- oder Treiberupdate.

### GPU-4: messbare Ziele

Das sind **Ziele für spätere Abnahme**, keine bereits gemessenen Verbesserungen:

- Keine zusätzlichen Bild-/Metervertauschungen und keine unbemerkten Auslassungen.
- Häufige UI-Rückmeldungen normalerweise unter 100 ms; keine regelmässigen Pausen über 200 ms durch Bildaufbereitung.
- Bei 60-Hz-Anzeige Zeichnen innerhalb des 16,7-ms-Budgets anstreben; seltene Ausreisser separat ausweisen.
- 1'000-Foto-Galerie erzeugt nur sichtbare Elemente samt begrenztem Puffer.
- Langer Prüflauf bleibt unter dem festgelegten Speicherbudget; keine unbegrenzt wachsenden Caches.
- Optimierung übernimmt man erst bei wiederholbarem Nutzen und bestandener fachlicher Gegenprüfung.

## 8. DLSS und RTX Video

### DLSS

DLSS ist kein allgemeiner Schalter für schärfere WPF-Tabellen.
Die reguläre Integration von DLSS Super Resolution verarbeitet Renderdaten wie Farbe, Tiefe und Bewegungsvektoren.
Der vorhandene WPF-/LibVLC-Ablauf stellt diese Daten nicht in der erforderlichen Engine-Schnittstelle bereit.
[NVIDIA: DLSS-Integration](https://github.com/NVIDIA-RTX/Streamline/blob/main/docs/ProgrammingGuideDLSS.md)

**Empfehlung:** Kein DLSS-Projekt für die heutige Oberfläche beginnen.
Falls später eine aufwendige 3D-Szene tatsächlich durch ihre Renderauflösung begrenzt ist, DLSS dort separat prüfen.
Schrift und Bedienelemente weiterhin in passender Bildschirmauflösung zeichnen.

### RTX Video Super Resolution / HDR

RTX Video ist ausdrücklich für die Verbesserung vorhandener Videobilder gedacht.
Es kann Auflösung, Kompressionsartefakte und HDR-Darstellung bearbeiten und nutzt Tensor-Kerne.
[NVIDIA: RTX Video SDK](https://developer.nvidia.com/rtx-video-sdk)

**Für SewerStudio ist das ein möglicher späterer Anzeigeversuch.** Er braucht eine passende Video-/Textur-Anbindung.
Aus den vorhandenen LibVLC-Optionen folgt nicht, dass RTX Video bereits aktiv ist oder sich ohne Umbau einschalten lässt.

Fachliche Bedingungen für den Versuch:

- Original bleibt jederzeit unmittelbar erreichbar; Verbesserung klar sichtbar kennzeichnen.
- Vermessung, OSD-Meterablesung, automatische Schadenserkennung und Befundfoto verwenden weiterhin den gebundenen Originalbeleg.
- Nebeneinander prüfen: feine Risse, Fugen, Wurzeln, Ablagerungen, Wasserreflexe und schlecht komprimierte Videos.
- Ein optisch erzeugtes Detail gilt nicht als nachgewiesener Schaden.
- Zusatzlast gegen gleichzeitig laufende KI messen. Bessere Anzeige darf die Auswertung nicht unbrauchbar verlangsamen.

## 9. Grafik-Engines im Vergleich

Bewertung aus Sicht des vorhandenen .NET-10-/WPF-Programms. Die Aufwandsspalte bewertet den Einbau in SewerStudio, nicht die Engine allgemein.

| Lösung | Passender Einsatz | Einbau/Pflege | Empfehlung |
|---|---|---|---|
| **WPF weiterentwickeln** | Tabellen, Formulare, Befundliste, 2D-Schemata | Geringster zusätzlicher Aufwand; vorhandene Infrastruktur | Hauptoberfläche behalten und gezielt verbessern. |
| **HelixToolkit mit DirectX 11** | Technischer 3D-Schacht, Leitungsnetz, Schnitte, drehbares Modell | Mittel; nah an C#/WPF, trotzdem neuer Renderer und Paketpflege | Erster Vergleichsversuch für eingebettetes 3D. |
| **SkiaSharp** | Viele 2D-Linien, Schemazeichnung, spezielle Zeichenflächen | Mittel; neue Zeichenfläche, Eingabe-/Treffertests selbst integrieren | Nur bei gemessenem 2D-Engpass prüfen. Kein automatischer GPU-Gewinn durch blosse Installation. |
| **Godot** | Eigenständige 3D-Kanalbegehung, interaktive Schulung, vereinfachte Simulation | Mittel bis hoch; eigener Szenenaufbau, Build und Datenaustausch | Bevorzugter Spiele-Engine-Kandidat für einen abgegrenzten Betrachter. |
| **Unity** | 3D, VR/AR, Plattformen über den heutigen Windows-Arbeitsplatz hinaus | Hoch; eigener Laufzeit-/Releaseweg und Lizenzprüfung | Erwägen, wenn solche Produktziele konkret werden. C# allein macht es nicht zu einem WPF-Steuerelement. |
| **Unreal Engine** | Fotorealistische Präsentation, aufwendige 3D-Szenen und Simulation | Höchster erwarteter Aufwand im vorhandenen C#-Produkt | Für eine kleine technische Ansicht derzeit kein ausreichender Zusatznutzen belegt. |

HelixToolkit dokumentiert sowohl WPF-3D als auch einen DirectX-11-Weg. Paketstand, Abhängigkeiten und .NET-10-Kompatibilität
müssen im Versuch geprüft werden. Eine Bibliothek beseitigt die Integrationsarbeit nicht.
[HelixToolkit: Varianten](https://helix-toolkit.github.io/helix-toolkit/articles/intro.html)
SkiaSharp ist eine 2D-API für .NET; der gewählte Host und Renderweg entscheiden über die tatsächliche Beschleunigung.
[SkiaSharp-Projekt](https://github.com/mono/SkiaSharp)

Godot unterstützt C# mit seiner .NET-Ausgabe. Die Engine steht unter MIT-Lizenz, auch kommerzielle Nutzung ist möglich;
Lizenzhinweise und fremde Bestandteile bleiben zu beachten.
[Godot C#](https://docs.godotengine.org/en/stable/tutorials/scripting/c_sharp/index.html), [Godot-Lizenz](https://godotengine.org/license/)

Unity dokumentiert die Einbindung in Windows-Anwendungen, auch als externen Prozess mit eingebettetem Fenster.
Das erfordert eigene Lebensdauer-, Eingabe- und Fensterverwaltung.
[Unity: Windows-Einbindung](https://docs.unity3d.com/Manual/UnityasaLibrary-Windows.html)
Für eine Fachanwendung müssen die Industry-Bedingungen und die konkrete Verteilung geprüft werden.
Die Abschaffung der Runtime Fee für Spiele ersetzt diese Prüfung nicht.
[Unity: Industry-Einstufung](https://support.unity.com/hc/en-us/articles/30863828337684-Should-I-have-an-Industry-License)

Unreal unterscheidet je nach Nutzung, Umsatz und Verteilung zwischen Ausnahmen, Sitzlizenzen und Umsatzbeteiligung.
Eine pauschale Aussage „Fachanwendung kostet immer nur eine Sitzlizenz“ wäre falsch.
[Epic: Lizenzmodelle](https://www.unrealengine.com/license)

### Architektur eines späteren 3D-Versuchs

```text
Bestehendes Projekt und Fachregeln
               │
       Lesbarer Szenen-Schnappschuss
       IDs · Koordinaten · Einheiten · Datenherkunft
               │
       Austauschbarer 3D-Betrachter
               │
       Auswahl einer vorhandenen Objekt-ID
               │
       Bestehende Objektakte / Video in SewerStudio
```

Der erste Betrachter liest Daten und meldet Auswahl zurück. Fachliche Änderungen erfolgen weiterhin über bestehende Anwendungsabläufe.
Eine ausgefallene oder geschlossene 3D-Ansicht darf das Projekt nicht beschädigen.
Bei externer Engine: begrenzte lokale Schnittstelle, eigene Prozesslebensdauer und bestehende lokale Zugriffsschutzmuster nutzen.

Für Schweizer Koordinaten braucht die Szene einen lokalen Ursprung. Echte LV95-Werte in der Fachschicht erhalten;
für den Renderer kleine relative Werte verwenden. Einheiten, Höhe, Achsen und Objektkennungen in Gegenproben sichern.
Ein aus Stammdaten erzeugtes Rohrmodell als **schematisch** kennzeichnen. Es ist keine Vermessung der tatsächlich gefilmten Rohrwand.

### Begrenzter Vergleichsversuch

Gleicher künstlicher Datensatz für HelixToolkit und Godot: 100 Schächte, 1'000 Leitungen, mehrere Rohrformen,
bekannte Höhen, Befundpunkte und eine Auswahlverknüpfung zur Objektakte.
Danach einmal mit 10'000 Leitungen skalieren. Keine Engine nur anhand einer leeren Demo auswählen.

Vergleichen: Startzeit, Speicher, Darstellungszeit beim Drehen, Objektwahl, Textlesbarkeit, Drucken/Export,
Monitorwechsel, Verhalten ohne starke GPU und Grösse des ausgelieferten Pakets.
Erst nach diesem Vergleich entscheiden, ob der fachliche Nutzen den Pflegeaufwand rechtfertigt.

## 10. Umsetzungsplan in fünf Etappen

### Etappe 1 — Vorhandene Arbeit und externe Änderungen schützen

**Inhalt:** A01/A02/A08, Nachprüfung der parallelen Korrekturen A04–A07, anschliessend A09/A16.

**Lieferung:** Zusammenhängende Korrekturen mit festen Gegenproben für Fehler während und nach einer Teilaktion.
**Fertig, wenn:** Fehlgeschlagenes Speichern lässt das aktive Projekt unverändert; übersprungene Quellen behalten ihre Sicherung;
WebGIS-Sperren gelten für Felder und Massnahmen; Ergebnisbericht unterscheidet geschrieben und nachgeprüft.
**Grenze:** Gegen gleichzeitige Serveränderungen nach dem letzten Lesen braucht es eine serverseitige Versionsprüfung.
Ein weiterer Clientvergleich allein garantiert keine atomare Sperre.

### Etappe 2 — Fachlich und optisch richtige Ergebnisse

**Inhalt:** A03/A12/A13, B01/B02/B03/B04 und erste Quick Wins.

**Lieferung:** Verlässliche KI-Zustände, richtige Bildpositionen, sichtbare dünne Konturen und stabile Kosten-/Materialzuordnung.
**Fertig, wenn:** Dieselben künstlichen Fehlerfälle dieses Audits bestehen als feste Tests; reale Originalbelege stimmen bei der Bedienprüfung.
**Aufteilung:** Je Problem ein begrenztes Paket. Neue Fachlogik in die Application-Schicht; bestehende UI/Ai-Klassen nicht weiter aufblasen.

### Etappe 3 — Schnell bedienbare Oberfläche

**Inhalt:** Q01/Q05/Q09/Q10/Q12, Galerievirtualisierung, bessere Platznutzung und verständliche Statusanzeigen.

**Lieferung:** Haltungen, Schächte und Player mit einheitlicher Darstellung; bestehende persönliche Ansichten erhalten.
**Fertig, wenn:** Tastatur und Maus funktionieren, lange Listen bleiben bedienbar, keine abgeschnittenen Pflichtaktionen in der Zielmatrix.
**Nachweis:** Vorher-/Nachher-Bilder derselben künstlichen Projekte, Bedienzeiten und Speicherwerte.

### Etappe 4 — GPU und Bildverarbeitung nach Messung verbessern

**Inhalt:** A10/A11, GPU-1 bis GPU-4, aktuelle Paket-/Treiberkombination dokumentieren.

**Lieferung:** Wiederholbarer Vergleich mit identischem Modell, Bildern, Treiber, Einstellungen und Lastprofil.
**Fertig, wenn:** Verbesserte Zeit oder weniger Speicher sind messbar und alle Qualitätsgrenzen eingehalten.
**Rückweg:** Jede geänderte Präzision, Auflösung oder Modellruntime bleibt einzeln rückstellbar.

### Etappe 5 — 3D und optionale Videoverbesserung entscheiden

**Inhalt:** HelixToolkit/Godot-Vergleich; Unity/Unreal bei entsprechendem zusätzlichen Produktziel;
separater RTX-Video-Versuch nur für die Anzeige.

**Lieferung:** Kurze Entscheidung mit Messwerten, Pflegeaufwand, Lizenzanforderungen und erkennbarem Nutzen im Arbeitsalltag.
**Fertig, wenn:** Ein kleiner, abgegrenzter Betrachter funktioniert oder begründet auf eine Engine verzichtet wird.
**Vorbereitung:** Neue Pakete und ein grosser Umbau benötigen gemäss Projektregeln eine separate Entscheidung.
Der vorliegende Auftrag erstellt dafür die Grundlage; er installiert nichts.

## 11. Offene Prüfungen und Freigaberegeln

- Neue Gegenproben in die passenden bestehenden Testprojekte übernehmen.
- Die 16 bekannten roten UI-Tests einzeln klären; veraltete Textsuche durch Verhalten ersetzen, Regeln nicht pauschal entfernen.
- Den sporadischen Prozessstopptest unter kontrollierten Bedingungen untersuchen.
- Bestehende Sicherheitsausnahmen mit Version, Grund und Ablösetest pflegen; A18 korrigiert nur den Kennungsvergleich.
- Einen vollständigen Release-Lauf auf dem fertig integrierten Stand durchführen.
- Danach Bedienabnahme mit Projektkopien: Speichern unter, Wiederöffnen, Medien, Sicherung/Wiederherstellung und WebGIS.
- GPU-Benchmarks brauchen einen ruhigen, dokumentierten Rechnerzustand und identische Eingabebilder.
- Kein Geschwindigkeitsgewinn darf Kundenoriginale, Bildbindung, Meterangaben oder manuelle Entscheidungen verändern.

## 12. Nachweise

- [Basis-Audit mit 18 Befunden](BASISAUDIT.md)
- [Prüfumfang und parallele Änderungen](nachweise/pruefumfang.json)
- [Neue Grafik-/Cache-/Galerie-Gegenproben](nachweise/grafik-gegenproben.json)
- [DINO-Gerätegegenprobe](nachweise/dino-gegenprobe.json)
- [Gelesene Hardware und Laufzeit](nachweise/hardware.json)
- [46 UI-Fokustests](nachweise/grafik-fokus.trx)
- [Python-Fokustests](nachweise/sidecar-fokus.xml)
- [Quelltext der WPF-Gegenprobe](nachweise/GrafikGegenprobe.cs)
- [Quelltext der DINO-Gegenprobe](nachweise/dino_probe_quellstand.py)

Ausführbare Probe und vollständige Datei-Prüfsummen liegen zusätzlich unter `.tmp/audit-erweiterung-2026-09-23/`.
Wiederholung aus dem Projektordner:

```powershell
dotnet run --project .tmp/audit-erweiterung-2026-09-23/probe/Probe.csproj -c Release
.\sidecar\.venv\Scripts\python.exe .tmp/audit-erweiterung-2026-09-23/dino_probe.py
```

Die Probe referenziert die im Basis-Audit gebauten Produktassemblies. Nach Produktänderungen zuerst den passenden Build erneuern.
Die Kopien der Prüfquelltexte in `nachweise` dienen als Beleg; ihre relativen Pfade gehören zum ursprünglichen Prüfwerkzeug.

**Abschluss:** Dokumentation und isolierte Prüfdateien erstellt. Kein Produktcode geändert, kein Commit oder Upload,
kein Kundenprojekt verändert, kein echtes WebGIS beschrieben und keine neue Engine installiert.
