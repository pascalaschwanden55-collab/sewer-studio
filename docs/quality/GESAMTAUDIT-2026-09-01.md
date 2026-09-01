# Gesamtaudit SewerStudio

**Datum:** 1. September 2026  
**Geprüfter Stand:** `a34aa33bbeab952d3e4b352241c7a9f778e08eef`  
**Zweig:** `feature/eval-pruefsatz-review`  
**Umfang:** gesamte WPF-Anwendung, Fachlogik, Importe/Exporte, Datei- und
Projektzugriffe, PDF/XTF/Medien, KI-Pipeline, Sidecar, Training, Wissensdatenbank,
QGIS-Brücke, Werkzeuge, Tests, CI, Abhängigkeiten und Dokumentation.

## Kurzurteil

SewerStudio besitzt heute einen ungewöhnlich starken Schutz gegen stillen
Datenverlust: Kundenoriginale werden grundsätzlich nicht verändert, Importe und
Publikationen sind vielfach transaktional, Modellfreigaben arbeiten fail-closed und
eine grosse Testsuite schützt Architektur und Verhalten.

Der geprüfte Stand ist trotzdem **noch nicht vollständig freigabegrün**:

- Es gibt **keinen bestätigten P0-Befund** und keinen nachgewiesenen Verlust von
  Kundendaten.
- Die Python-Sicherheitsprüfung ist wegen einer neuen, nicht ausgenommenen Lücke in
  `hydra-core 1.3.3` rot. Das ist vor einer Auslieferung des Sidecars zu bereinigen.
- Der V2-Eval-Satz kann in einem seltenen Parallelfall bereits veröffentlicht sein,
  obwohl danach erkannt wird, dass V1 während des Baus verändert wurde.
- Die .NET-Tests werden im zweiten Gesamtlauf vollständig grün. Ein WPF-Test ist im
  ersten Gesamtlauf jedoch reproduzierbar am internen 15-Sekunden-Limit gescheitert.
  Das ist ein Testzuverlässigkeitsproblem, keine bestätigte Produktregression.
- Die produktive Mehrklassen-Erkennung ist fachlich weiterhin nicht freigegeben.
  Die gemessene Qualität reicht nicht. Wichtig und richtig: Das Programm behandelt
  diese Modelle nicht still als qualifiziert.

Die Kernanwendung ist damit **stabil und gut abgesichert**, aber der gesamte
Auslieferungsweg ist wegen des Python-Sicherheitsbefunds und einzelner
Zuverlässigkeitslücken noch gelb.

## Prioritäten

- **P0:** sofort handeln; akuter Datenverlust, schwere Sicherheitslücke oder
  Hauptfunktion unbenutzbar.
- **P1:** vor der nächsten betroffenen Freigabe oder Nutzung beheben.
- **P2:** zeitnah beheben; reale Robustheits-, Qualitäts- oder Wartbarkeitslücke.
- **P3:** kleine Lücke oder technische Schuld; beim nächsten passenden Paket
  erledigen.

| Priorität | Anzahl bestätigter Punkte |
|---|---:|
| P0 | 0 |
| P1 | 2 |
| P2 | 6 |
| P3 | 8 |

Der fachlich noch nicht freigegebene KI-Stand wird getrennt bewertet. Er ist kein
zusätzlicher Programmfehler, sondern eine korrekt sichtbare Leistungsgrenze.

## Prüfgrundlage und Grenzen

### Geprüft

- `CLAUDE.md` und die aktuelle Architekturkarte vollständig gelesen.
- Frühere Gesamtaudits vom 18. August gegen den heutigen Code gegengeprüft.
- 52 .NET-Projekte erfasst: 4 Produkt-, 4 Test- und 44 Werkzeugprojekte.
- 5'210 C#-, 296 Python- und 82 XAML-Dateien statisch durchsucht.
- Schichten, Service-Aufbau, UI-Bindungen, Dateioperationen, Prozesse,
  Netzwerkzugriffe, Fehlerbehandlung, Kulturregeln und Modellpfade stichprobenartig
  sowie über vorhandene Wächter geprüft.
- Abhängigkeiten online gegen die aktuellen NuGet- und Python-Sicherheitsdaten
  geprüft.
- Alle vorgesehenen lokalen Testgruppen ausgeführt.

### Nicht vollständig geprüft

- Keine manuelle Klickprüfung aller Fenster und keine echte Prüfung mit Screenreader.
- Keine realen GPU-/E2E-Modelltests mit DINO, SAM, YOLO und Qwen.
- Keine vollständigen Importe mit sämtlichen Kundenformaten und sehr grossen
  Originalbeständen.
- Kein echter GitHub-CI-Lauf; die CI-Datei und ihre lokalen Gegenstücke wurden geprüft.
- Der normale Gesamtbuild konnte die bereits laufende SewerStudio-MCP-DLL nicht
  ersetzen. Die Anwendung wurde regelkonform nicht beendet. Der Entwicklungsfilter
  und das MCP-Projekt in einem getrennten Ausgabeordner bauen beide sauber.

### Bereits vorhandener fremder Arbeitsstand

Vor dem Audit waren `CLAUDE.md`, zwei Detect-Skripte, ein Detect-Bericht sowie
Ausgabeordner bereits geändert oder unversioniert. Diese Dateien wurden nicht
verändert, nicht bereinigt und nicht als Audit-Fix ausgegeben. Der Zweig lag zu
Prüfbeginn 22 Commits vor dem Server.

## Build-, Test- und Sicherheitsnachweis

| Prüfung | Ergebnis |
|---|---|
| `dotnet build AuswertungPro.Dev.slnf -c Release --no-restore` | **bestanden**, 0 Warnungen, 0 Fehler |
| MCP-Projekt, getrenntes Ausgabeziel | **bestanden**, 0 Warnungen, 0 Fehler |
| Infrastrukturtests | **5'525 bestanden**, 5 übersprungen |
| Pipelinetests | **2'447 bestanden**, 2 übersprungen |
| UI-Tests, erster Gesamtlauf | 6'227 bestanden, **1 fehlgeschlagen**, 3 übersprungen |
| UI-Tests, zweiter Gesamtlauf | **6'228 bestanden**, 3 übersprungen |
| ProjectModernizer | **62 bestanden** |
| Letzter sauberer .NET-Gesamtstand | **14'262 bestanden**, 10 übersprungen, 0 fehlgeschlagen |
| Sidecar ohne GPU | **571 bestanden**, 2 abgewählt, 2 Warnungen |
| QGIS-Integration | **10 bestanden** |
| NuGet, direkt und transitiv | **keine bekannte Lücke gefunden** |
| Python-Sperrdatei | **rot**: 1 neue, nicht ausgenommene Lücke; 5 dokumentierte Ausnahmen |

Der erste rote UI-Lauf darf nicht unterschlagen werden. Der gleiche Test bestand
danach zweimal allein und im zweiten Gesamtlauf. Das grenzt die Ursache auf Last und
Zeitlimit ein, macht den ersten Lauf aber nicht nachträglich grün.

## Was bereits gut gelöst ist

### Daten- und Dateischutz

- Kundenoriginale werden bei Import, XTF-Revision, Dossierverarbeitung und Training
  grundsätzlich nur gelesen. Veröffentlichungen verwenden häufig Arbeitskopien,
  Hashes, Besitzmarker, atomare Wechsel und Rücknahme.
- Projektwechsel, Schliessen und Speichern sind während kritischer Importe und
  Exporte über gemeinsame Vorgangsregeln abgesichert.
- PDF-Verarbeitung besitzt Grössen-, Seiten- und Textbudgets sowie begrenzte externe
  Prozesse.
- Pfad-, Link- und Junction-Prüfungen sind an den gefährlichen Import-, Backup- und
  Trainingsgrenzen breit getestet.

### Architektur und Wartbarkeit

- Nullable-Analyse und aktuelle .NET-Analyzer sind projektweit aktiv.
- Architekturtests verhindern neue Fachlogik im eingefrorenen UI-KI-Ordner, falsche
  Schichtabhängigkeiten, übergrosse Klassen und unerreichbare UI-Befehle.
- Der neue UI-Wächter prüft beide Richtungen: Knopf ohne Aktion und Befehl ohne
  passenden Aufrufer in derselben Ansicht.
- Viele frühere God-Class-Anteile wurden in kleine Services, Controller und
  Workflows aufgeteilt. Die öffentlichen Fassaden blieben dabei erhalten.

### KI-Sicherheitskultur

- Das alte YOLO-Modell ist ausdrücklich als nicht qualifiziert markiert. Fehlende,
  falsche oder nicht lesbare Freigabe bleibt gesperrt.
- Gewichte des produktiven YOLO-Detektors, der Klassifikatoren und der
  Trainingsexporte werden an SHA-256-Prüfsummen gebunden.
- DINO-/SAM-Ausfälle werden nicht als sauberer Negativbefund ausgegeben.
- Gold, Training, Eval-Sätze und Modellkandidaten werden nach Bild- und
  Haltungsidentität getrennt; erkannte Kontaminationen werden offen dokumentiert.
- Kandidaten werden nicht automatisch aktiviert. Die Anwendung bleibt bei
  unzureichender Beweislage eingeschränkt statt Sicherheit vorzutäuschen.

### Netzwerk und Geheimnisse

- Lokale Steuerdienste und QGIS-Brücke sind an Loopback und Token gebunden.
- Für das Video-Label-Werkzeug werden freie Clientpfade nicht akzeptiert; Host,
  Methode und Pfad sind eingeschränkt.
- In der statischen Prüfung wurden weder globale CORS-Freigaben noch eine
  TLS-Zertifikatsumgehung oder `BinaryFormatter` gefunden.

## Bestätigte P1-Befunde

### P1-01 – Python-Sicherheitsprüfung ist wegen `hydra-core` rot

**Beleg**

- `sidecar/requirements-lock.txt` bindet `hydra-core==1.3.3`.
- `sidecar/security/audit_lock.py` meldet
  `GHSA-2cp2-2r3c-7p7r` als neue Lücke ohne dokumentierte Ausnahme.
- Betroffen sind laut offizieller Meldung Versionen bis einschliesslich 1.3.3;
  Version 1.3.4 enthält die Korrektur:
  <https://github.com/advisories/GHSA-2cp2-2r3c-7p7r>.
- Im eigenen Python-Code wurde kein direkter Aufruf von `hydra.utils.instantiate`
  gefunden. Die installierte SAM-2-Abhängigkeit nutzt Hydra jedoch selbst:
  `sam2/build_sam.py` importiert `compose` und `instantiate` und erzeugt damit das
  Modell aus `cfg.model`; `sam2/__init__.py` initialisiert das Hydra-Konfigurationsmodul.
- SewerStudio lädt dabei die mit SAM 2 ausgelieferten YAML-Konfigurationen und nimmt
  keine freie Hydra-Konfiguration von aussen entgegen. Die praktische
  Ausnutzbarkeit ist deshalb niedrig. Der betroffene Code liegt trotzdem direkt im
  produktiven Ladeweg des Segmentierungsmodells.

**Auswirkung**

Der Python-Sicherheitswächter und damit der vorgesehene Auslieferungsweg sind rot.
Ein indirektes Paket mit bekannter Codeausführungslücke soll nicht unbegründet im
Produkt bleiben. Gleichzeitig kann ein Versionswechsel den echten SAM-2.1-Start
beeinflussen. Die 571 schnellen Sidecar-Tests laden keine produktiven Modellgewichte
und können diesen Ladeweg deshalb nicht ausreichend absichern.

**Empfehlung**

1. Zuerst den reproduzierbaren Aktualisierungsweg festlegen. Im vorhandenen
   Sidecar-venv ist kein `pip` installiert. Die Umgebung daher nicht von Hand
   verändern, sondern die Sperrdatei wie dokumentiert mit `uv pip compile` erneuern
   und das venv anschliessend über `sidecar/setup.ps1` reproduzierbar aufbauen.
2. `hydra-core 1.3.4` auflösen, die Sperrdatei bewusst neu erzeugen und
   `sidecar/security/audit_lock.py` erneut ausführen.
3. Die 571 schnellen Sidecar-Tests wiederholen.
4. Grounding DINO mit den bekannten drei Vergleichsbildern und echten Gewichten
   prüfen.
5. SAM 2.1 mit den produktiven Gewichten wirklich laden und mindestens eine echte
   Segmentierung auf einem bekannten Bild und einer bekannten Box ausführen. Start,
   CUDA-Nutzung und Ergebnis müssen geprüft werden; ein reiner Importtest reicht
   nicht.
6. Nur falls das Update nachweislich inkompatibel ist, eine enge, befristete Ausnahme
   mit Beleg und Ablaufdatum eintragen. Eine pauschale Ausnahme wäre nicht angemessen.

### P1-02 – EvalSet V2 wird vor der letzten V1-Prüfung veröffentlicht

**Beleg**

In `src/AuswertungPro.Next.Application/Ai/Evaluation/EvalSetV2Builder.cs` wird der
V1-Hash vor dem Bau ermittelt. Danach wird der Arbeitsordner in Zeile 172 bereits als
fertiges `outputRoot` verschoben. Erst in den Zeilen 174 bis 176 wird geprüft, ob V1
während des Baus verändert wurde. Bei Abweichung entsteht eine Ausnahme, aber der
veröffentlichte Ausgabeordner bleibt liegen; `finally` löscht nur den inzwischen
nicht mehr vorhandenen Arbeitsordner.

**Auswirkung**

In einem Parallelfall kann ein V2-Satz sichtbar und vollständig gehasht aussehen,
obwohl seine unveränderliche V1-Grundlage während des Baus gewechselt hat. Das
betrifft keine Kundenoriginale, kann aber eine Modellmessung oder Freigabe ungültig
machen und den nächsten Bau blockieren.

**Empfehlung**

- V1 unmittelbar **vor** dem Veröffentlichen erneut prüfen.
- Eine zusätzliche Prüfung nach dem Wechsel behalten; schlägt sie fehl, nur den
  nachweislich vom eigenen Lauf veröffentlichten Ordner sicher zurücknehmen.
- Einen Parallelitätstest ergänzen, der V1 zwischen Staging und Veröffentlichung
  verändert und beweist, dass kein fertiger V2-Ordner übrig bleibt.

## Bestätigte P2-Befunde

### P2-01 – Abbruch kann ffmpeg-/ffprobe-Prozesse weiterlaufen lassen

**Beleg**

Der gemeinsame `ProcessOutputReaderService` leert beide Ausgabekanäle gleichzeitig
und beendet bei Abbruch den ganzen Prozessbaum. Drei produktive Wege umgehen diesen
Schutz noch:

- `VideoFrameSequenceExtractor.RunFfmpegAsync`
- ffmpeg-Rückfall in `QuickScanService.TryFfmpegDurationAsync`
- ffprobe und ffmpeg in `TrainingSampleGenerator.GetDurationAsync`

`VideoFrameSequenceExtractor` leitet zusätzlich Standardausgabe um, liest aber nur
Fehlerausgabe. Bei Abbruch werfen `ReadToEndAsync` oder `WaitForExitAsync`; das
anschliessende `Dispose` des `Process` beendet ffmpeg nicht.

**Auswirkung**

Nach „Abbrechen“ kann CPU-/Datenträgerarbeit im Hintergrund weiterlaufen. Im
ungünstigen Fall füllt eine nicht gelesene Standardausgabe den Puffer und der Prozess
wartet dauerhaft.

**Empfehlung**

Alle drei Wege über `IProcessOutputReader` oder `ExternalProcessRunner` führen,
beide Kanäle parallel leeren, ein endliches Zeitlimit setzen und bei Abbruch den
gesamten gestarteten Prozessbaum beenden. Je Weg einen echten Abbruchtest ergänzen.

### P2-02 – Uhrlage `3:00` geht beim Goldfoto-Export verloren

**Beleg**

`PhotoAnnotationUseCase.ResolveClockPosition` liest `vsa.uhr.von` ausschliesslich
mit `double.TryParse(..., InvariantCulture)`. Das funktioniert für `3`, nicht für
`3:00`. Andere produktive Wege schreiben und erwarten ausdrücklich Werte wie
`3:00`, `6:00`, `9:00` und `12:00`.

**Auswirkung**

Ein manuell korrekt lokalisierter Schaden kann im erzeugten Goldsample seine
Uhrlage verlieren. Das verschlechtert Trainings- und Prüfdaten still.

**Empfehlung**

Die vorhandene zentrale Uhrlagen-Normalisierung verwenden und Tests für `3`,
`3:00`, `12:00` sowie ungültige Werte ergänzen.

### P2-03 – Zwei Verteilungswege melden Fehler nicht gezielt an den Benutzer

**Beleg**

`ExportPageViewModel.DistributeHoldingsAsync` und `DistributeDichtheitAsync` besitzen
nur `try/finally`. Der gemeinsame Vorgangsschutz gibt die Sperre zuverlässig frei,
fängt den fachlichen Fehler aber ebenfalls nicht ab. Der vergleichbare
`DistributeShaftsAsync` verwendet dagegen `UserError.DescribeAndReport`, setzt ein
verständliches Ergebnis und zeigt eine Warnung.

**Auswirkung**

Die Sperre bleibt nicht hängen, aber ein Fehler bei Haltungs- oder
Dichtheitsverteilung hat keine gleichwertige, vorgangsspezifische Rückmeldung. Ob die
Command-Bibliothek den Fehler zusätzlich global weitergibt, ändert diese Lücke nicht.

**Empfehlung**

Dasselbe Fehler- und Dialogmuster wie bei der Schachtverteilung verwenden und je
einen Fehlerpfadtest ergänzen.

### P2-04 – Ein WPF-Test ist unter Gesamtlast unzuverlässig

**Beleg**

`NachschlagKontextmenueTests.Das_Nachschlagmenue_haengt_an_den_richtigen_Feldern`
startet einen Kindprozess mit 120 Sekunden Grenze. Im Kindprozess ruft der Test
`StaTestRunner.Run` ohne eigenen Wert auf. Dessen Standardgrenze beträgt nur 15
Sekunden. Im ersten UI-Gesamtlauf lief genau diese Grenze ab. Der Elternfall bestand
danach zweimal allein und im zweiten vollständigen UI-Lauf.

**Auswirkung**

Ein künftiger echter Fehler kann zwischen wechselnden roten und grünen Läufen
schwerer erkannt werden. Der Befund beweist keinen Fehler im Kontextmenü selbst.

**Empfehlung**

Dem inneren STA-Lauf ausdrücklich eine für den Kindprozess passende Grenze geben
oder die doppelte Zeitüberwachung entfernen. Danach mehrere vollständige UI-Läufe
hintereinander ausführen.

### P2-05 – DINO- und SAM-Gewichte sind nicht an eine freigegebene Identität gebunden

**Beleg**

- `dino_wrapper.py` sucht passende Konfigurations- und Gewichtsdateien und nimmt die
  nach Namen bevorzugte erste Datei.
- `sam_wrapper.py` nimmt die alphabetisch erste passende Datei im SAM-2.1-Ordner.
- `/health` prüft für beide Modelle nur, ob irgendeine `*.pth`- oder `*.pt`-Datei
  vorhanden ist.
- Im Gegensatz dazu bindet YOLO aktive Gewichte und Freigabe per SHA-256.

**Auswirkung**

Eine versehentlich ausgetauschte, zusätzliche oder beschädigte Datei kann geladen
werden, ohne dass Health oder C# die genaue Modellidentität kennt. Da SewerStudio
lokal arbeitet und die Modellordner nicht aus freien Benutzerpfaden kommen, ist das
kein bestätigter Fernangriff, aber eine echte Reproduzierbarkeits- und
Lieferkettenlücke.

**Empfehlung**

Für DINO-Konfiguration, DINO-Gewichte und SAM-Gewichte ein Manifest mit Dateiname,
Modellart und SHA-256 einführen. Beim Laden erneut hashen; `/health` soll Identität
und Prüfergebnis melden. Abweichungen müssen wie bei YOLO fail-closed sein.

### P2-06 – Python-CI prüft andere Versionen als die Sicherheitsprüfung

**Beleg**

Die CI untersucht `sidecar/requirements-lock.txt`. Für die Tests installiert sie
anschliessend jedoch ungebunden `torch torchvision` vom CPU-Index und
`pip install -e ".[dev]"`. `pyproject.toml` enthält überwiegend Mindestversionen.

**Auswirkung**

Die Sicherheitsprüfung kann grün sein, während die Tests mit neueren anderen
Versionen laufen. Umgekehrt ist nicht belegt, dass die exakt ausgelieferten Pakete
in CI gemeinsam funktionieren.

**Empfehlung**

Eine reproduzierbare CPU-CI-Sperrdatei erzeugen, sie ebenfalls prüfen und exakt aus
ihr installieren. Die produktive Windows/CUDA-Sperrdatei bleibt getrennt, aber
gemeinsame Paketversionen müssen bewusst synchronisiert werden.

## Bestätigte P3-Befunde

### P3-01 – Der eigene Player-OSD-Leser verwendet noch freie Qwen-Antworten

Der allgemeine `OllamaVisionFindingsService` verwendet inzwischen ein striktes
JSON-Schema. Der getrennte `CodingOsdMeterService.CreateDefault` ruft jedoch weiter
`OllamaClient.ChatAsync` auf und bittet nur im Text um „nur die Zahl“.

Die Auswirkung ist begrenzt: Der Leser hat ein 8-Sekunden-Limit, entfernt Datum und
Uhrzeit, verlangt genau einen Kandidaten zwischen 0 und 500 Metern und verwirft
Sprünge über 3 Meter. Ein Schema würde den Vertrag trotzdem eindeutiger und
testbarer machen. Empfohlen ist `{ "meter": Zahl oder null }` mit deterministischen
Optionen und anschliessender gleicher Plausibilitätsprüfung.

### P3-02 – Uhrlagen-Deduplizierung vergleicht Rohtexte

`CodingFindingCoveragePolicy.IsSamePosition` vergleicht für Befunde ohne Box
`existingClock` und `newClock` direkt als Text. `3` und `3:00` gelten damit als
verschiedene Positionen, obwohl sie fachlich gleich sind. Das kann doppelte
Anschlussereignisse erzeugen. Vor dem Vergleich zentral normalisieren und den
gemischten Formatfall testen.

### P3-03 – Vier XTF-Pfade umgehen den gemeinsamen sicheren XML-Lader

`XtfStammdatenElementReader`, `XtfKanalschadenElementReader` und
`XtfRevisionWriter` verwenden zusammen vier direkte `XDocument.Load`-Aufrufe. Die
anderen Importwege verwenden `SafeXmlDocumentLoader` mit gesperrten DTDs und ohne
externen Resolver.

Es wurde kein funktionierender XXE-Angriff nachgewiesen; moderne .NET-Vorgaben sind
bereits restriktiv. Die direkte Nutzung umgeht aber den ausdrücklich eingeführten,
testbaren Schutzvertrag. Alle fremden XTF-Dateien sollten denselben Lader verwenden.
Zusätzlich ist zu entscheiden, welche Dokumentgrösse für echte Kantons-XTF zulässig
ist, bevor pauschal ein zu kleines Limit eingeführt wird.

### P3-04 – .NET-SDK ist nicht exakt reproduzierbar

`global.json` nennt 10.0.108, erlaubt aber `latestFeature`; lokal wurde 10.0.111
verwendet. Die CI installiert `10.0.x`. Compiler und Analyzer können dadurch ohne
Codeänderung wechseln. Entweder exakt pinnen und bewusst aktualisieren oder das
gewollte Gleiten samt Prüfregel klar dokumentieren.

### P3-05 – Barrierefreiheitswächter deckt nur einen kleinen Ausschnitt ab

Es gibt sichtbaren Tastaturfokus, Navigationstests und gezielte Screenreader-Namen
für die elf Foto-Messwerkzeuge. Von 82 XAML-Dateien enthalten derzeit jedoch nur zwei
explizite `AutomationProperties`-Angaben. Textknöpfe sind dadurch nicht automatisch
schlecht; mehrere reine Symbolknöpfe verlassen sich aber nur auf Glyphen und
Tooltips. Der heutige Wächter prüft im Wesentlichen nur das Foto-Messfenster.

Empfohlen ist ein zweiter XAML-Wächter: Symbolknöpfe müssen sichtbaren Text oder
einen expliziten Automation-Namen besitzen. Danach die wichtigsten Arbeitsabläufe
einmal mit Tastatur und Windows-Sprachausgabe prüfen.

### P3-06 – Die Abdeckungszahl misst nicht gezielt den Produktcode

Die CI-Grenze liegt bei 45,35 Prozent; die letzte dokumentierte CI-Messung war 45,42
Prozent. Laut eigener Baseline werden Testcode und erzeugter Code mitgezählt. Die
Zahl ist als Verschlechterungssperre brauchbar, beantwortet aber nicht, wie gut
Importe, Projektpersistenz, Exporte und KI-Freigabelogik tatsächlich abgedeckt sind.

Die bestehende Gesamtschranke behalten und zusätzlich eine getrennte
Produktcode-Messung für die vier Produktprojekte einführen. Kritische Bereiche
sollten eigene Mindestwerte oder Verhaltenstests erhalten, nicht nur eine globale
Prozentzahl.

### P3-07 – Die 44 Werkzeuge besitzen keinen aktuellen Werkzeugkatalog

Die Lösung baut 44 Werkzeugprojekte. `docs/WERKZEUGKATALOG.md` existiert weiterhin
nicht. Zweck, Schreibwirkung, benötigte Daten, GPU-Bedarf und empfohlener Startbefehl
sind dadurch nur über viele Einzeldokumente und Quelltexte auffindbar.

Ein generierter Katalog mit diesen Spalten wird empfohlen: Werkzeug, Zweck,
schreibend/lesend, Eingaben, Ausgaben, Voraussetzungen, Schutzstatus und Beispiel.

### P3-08 – Sidecar-Testumgebung meldet zwei Wartungshinweise

- Starlettes `TestClient` verwendet eine von `httpx` bereits als veraltet gemeldete
  Aufrufart. Die Tests laufen noch, können bei einem späteren Paketwechsel aber
  brechen.
- pytest kann im Sidecar-Ordner seinen Cache wegen fehlender Berechtigung nicht
  anlegen. Das verändert das Testergebnis nicht, erschwert aber Wiederholungen und
  Warnungsverwaltung.

Beides beim nächsten Python-Paketupdate bereinigen; nicht als Produktfehler behandeln.

## Wartbarkeitsbeobachtung ohne eigenen Fehlerstatus

Der 1'000-Zeilen-Wächter wirkt: Die grössten Produktdateien liegen knapp darunter,
unter anderem zwei ViewModels mit je 997 Zeilen, `ProtocolPdfExporter` mit 994 und
`ServiceProvider` mit 992 Zeilen. Das ist kein Beweis für falsches Verhalten, zeigt
aber hohen Änderungsdruck. Neue Logik soll diese Dateien nicht wieder wachsen lassen;
bei der nächsten fachlichen Änderung jeweils einen kleinen Service oder Workflow
herauslösen.

## Fachlicher Stand der KI-Pipeline

### Mehrklassen-Detektor

Der aktuelle dokumentierte Release-Diagnoselauf auf 400 Bildern war technisch
vollständig, aber fachlich schwach:

- 350 Soll-Boxen
- TP 36, FP 59, FN 314
- Precision 37,9 Prozent
- Recall 10,3 Prozent
- F1 16,2 Prozent
- Fehlalarm auf 9 von 74 echten Negativbildern
- `BCC_bogen` 27/37, `BCA_anschluss` 8/39, `BAF_oberflaeche` 1/89
- elf weitere gemessene Klassen ohne exakten Treffer

Der Prüfbestand ist zusätzlich nicht vollständig releasebereit: 400/400 Bilder sind
beurteilt, aber mit 74 statt 75 Negativbildern und unvollständiger Klassenabdeckung.
Die ältere Messung war für diesen Kandidaten zudem nach oben verzerrt, weil acht
Samples aus zwei Haltungen in dessen Trainingsregister standen.

**Urteil:** nicht aktivieren. Material gezielt verbreitern, einen frischen
unberührten Holdout aufbauen und erst dann erneut messen. Die bestehende Sperre ist
richtig.

### Ereignisbasierte Freigabe

Die technische Auswertung nach Ereignis statt Einzelbild ist vorhanden. Das
120er-Eval-Set besitzt aber noch nicht vollständig menschlich bestätigte Severity und
Event-ID. AP 0.4 ist daher nicht abgeschlossen. Keine Modellfreigabe allein aus der
technischen Messlogik ableiten.

### BCC-Bogen

Die beiden relevanten Kandidaten erreichen auf dem bisherigen Vergleichsbestand
Balanced Accuracy 55,9 beziehungsweise 54,5 Prozent und erzeugen zu viele
Fehlalarme. Beide bleiben zu Recht `not_deployed`. Da derselbe Bestand mehrere
Kandidaten auswählte, braucht ein späterer Sieger einen frischen Bestätigungsholdout.

### OSD-Meter

Der Vorlagenweg liefert auf den vier verwendeten Sätzen 194 richtige und einen
falschen Wert. Die Kette mit diagnostischem Modell-Rückfall erreicht 224 richtige
und ebenfalls einen falschen Wert. Das Modell bringt damit 30 zusätzliche richtige
Lesungen ohne zusätzlichen Fehlwert, ist standardmässig aber weiterhin aus.

Diese vier Sätze wurden bereits zur Auswahl verwendet. Vor dem produktiven
Einschalten ist ein frischer, unberührter Satz nötig. Die konservative
Nicht-Aktivierung ist korrekt. Der separate freie Qwen-Antwortvertrag im Player ist
der kleine technische P3-01-Befund und darf nicht mit dieser Modellmessung
verwechselt werden.

### Tracking und zeitliche Zusammenführung

Es gibt weiterhin kein echtes ByteTrack/OC-SORT-Tracking. Die Anwendung verwendet
framebasierte und zeitliche Deduplizierung. Das ist dokumentiert und kein verdeckter
Fehler, begrenzt aber die Zuverlässigkeit bei längeren, wiederholt sichtbaren
Schäden.

## Gegenprüfung früherer Befunde

Folgende früheren Punkte sind am heutigen Stand erledigt oder nicht mehr als Fehler
zu werten:

- `.wmv` und `.mp2` sind im zentralen Öffnen-Dienst berücksichtigt.
- Der Training-Studio-KB-Zugang hält keinen eigenen HttpClient mehr offen.
- Der alte XTF-Import verwendet die zentrale Medienerkennung.
- Planbreite und weitere Geld-/Messwerte verwenden eindeutige Kulturregeln.
- Die allgemeine Qwen-Bildanalyse erzwingt inzwischen ein JSON-Schema.
- Der beidseitige UI-Wächter ist vorhanden und die entfernten Befehle werden nirgends
  mehr gebunden.
- Sidecar-Anfragegrössen, 422-Antworten, VRAM-Reservierungen, Video-Label-Pfade,
  VSA-Code-Länge und Schachtkosten-Zusammenführung sind durch aktuelle Tests geschützt.
- Der frühere Verdacht am `VideoFrameStream` hat sich nicht bestätigt: Er leert seine
  Ausgabe und beendet den Prozessbaum. P2-01 betrifft andere Prozesswege.
- `HaltungRecordCloner.CloneForEvaluation` ist eine absichtlich reduzierte Kopie für
  die Schattenauswertung; fehlende Export-IDs sind dort kein allgemeiner Datenverlust.
- Leere `catch`-Blöcke an Temp-Bereinigungen sind überwiegend bewusstes Best-Effort
  und kein verschluckter Hauptfehler.

## Empfohlene Reihenfolge

### Paket 1 – Freigabesperren sauber machen

1. `hydra-core` aktualisieren oder eng und befristet begründen.
2. Python-Sicherheitsprüfung erneut ausführen.
3. Python-CI aus einer festen CPU-Sperrdatei testen.
4. DINO-/SAM-Identität mit Hash-Manifest absichern.

### Paket 2 – Daten- und Prozessrobustheit

1. EvalSet-V2 erst nach V1-Gegenprüfung veröffentlichen.
2. ffmpeg-/ffprobe-Wege an den gemeinsamen Prozessdienst anschliessen.
3. Uhrlage beim Goldfoto-Export normalisieren.
4. Haltungs- und Dichtheitsverteilung mit sichtbarer Fehlermeldung versehen.

### Paket 3 – Test- und UI-Vertrauen

1. WPF-Zeitlimit stabilisieren und mehrere Gesamtläufe prüfen.
2. OSD-Playerantwort per JSON-Schema binden.
3. Uhrlagen vor der Deduplizierung normalisieren.
4. Symbolknopf-Wächter sowie eine manuelle Tastatur-/Screenreader-Runde ergänzen.

### Paket 4 – Nachlaufende Wartbarkeit

1. Produktcode-Abdeckung getrennt messen.
2. Werkzeugkatalog generieren.
3. SDK-Strategie festlegen.
4. Python-Warnungen und Dateien nahe der 1'000-Zeilen-Grenze beim nächsten passenden
   Umbau abbauen.

## Freigabeentscheidung

| Bereich | Urteil |
|---|---|
| Kernanwendung und Datenwege | **grün mit kleinen offenen Robustheitspunkten** |
| .NET-Build und Tests | **gelb** wegen eines lastabhängigen WPF-Testfehlers; letzter Lauf grün |
| NuGet-Sicherheit | **grün** |
| Python-Sicherheit | **rot** bis `hydra-core` geklärt ist |
| Importe/Exporte und Kundenoriginale | **grün**, P2-/P3-Nachlauf empfohlen |
| KI-Schutzmechanismen | **grün** |
| KI-Erkennungsqualität | **rot für produktive automatische Freigabe** |
| CI-Reproduzierbarkeit | **gelb** |
| Bedienbarkeit/Barrierefreiheit | **gelb**, weil keine vollständige manuelle Abnahme vorliegt |

**Gesamt:** Eine normale Weiterentwicklung ist vertretbar. Für eine neue
Auslieferung mit Sidecar zuerst P1-01 klären. Für den nächsten V2-Eval-Lauf zuerst
P1-02 beheben. Kein aktuelles KI-Kandidatenmodell als produktiv qualifiziert
aktivieren.
