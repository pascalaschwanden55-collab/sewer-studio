# Wiederholungsaudit SewerStudio — 2026-08-14

Geprüfter Stand: Commit `dda6cb28e2ca8c1b8125a1d29a93f2e1b0ae3952`
auf Branch `feature/eval-pruefsatz-review`.

Dieses Audit wiederholt die Prüfung nach den Sicherheitskorrekturen und den
grossen Architekturumbauten. Geprüft wurden Architektur, Kernfunktionen,
Dateisicherheit, Abhängigkeiten, CI, Testabdeckung und KI-Freigabefähigkeit.
Am Programmcode wurde während dieser Prüfung nichts geändert.

## Kurzurteil

Der lokale Programmstand ist deutlich robuster als vor dem ersten Audit. Build,
11.899 .NET-Tests, 280 Sidecar-Tests und 10 QGIS-Tests sind lokal erfolgreich.
Die neuen Importtransaktionen, die ehrlichere Programmsicherung, die
QGIS-Anmeldung und die unabhängigen KI-Belegquellen sind im Code und durch Tests
belegt.

Der Stand ist trotzdem **nicht vollständig grün und noch nicht freigabefertig**:

- Die GitHub-CI des geprüften Commits ist rot.
- Der Python-Job kann das Sidecar in einer frischen Umgebung nicht installieren.
- Zwei Pipeline-Tests hängen an lokalen, nicht eingecheckten Voraussetzungen.
- Die Abdeckung liegt reproduzierbar bei 45,58 %, die CI-Grenze bei 45,60 %.

Es wurde keine neue kritische Datenverlust- oder Fernzugriffslücke gefunden. Die
Prio-1-Punkte dieses Berichts sind Freigabe- und CI-Blocker.

## Messergebnisse

| Prüfung | Ergebnis |
|---|---:|
| Release-Build Gesamtlösung | 0 Fehler, 0 Warnungen |
| Infrastructure-Tests | 3.825 bestanden, 1 übersprungen |
| Pipeline-Tests lokal | 2.327 bestanden, 2 übersprungen |
| UI-Tests | 5.685 bestanden, 1 übersprungen |
| ProjectModernizer-Tests | 62 bestanden |
| .NET gesamt | **11.899 bestanden** |
| Sidecar ohne GPU | **280 bestanden**, 2 abgewählt |
| QGIS-Brücke | **10 bestanden** |
| Alle bestandenen Tests | **12.189** |
| NuGet-Sicherheitsprüfung | 51 Projekte, keine bekannten Lücken |
| Python-Sperrdatei | 88 PyPI-Pins geprüft; 5 bekannte Ausnahmen |
| Nicht durch `pip-audit` prüfbar | Torch, Torchvision und SAM-2 |
| Frische .NET-Abdeckung, Lauf 1 | 292.502 / 641.687 = **45,58 %** |
| Frische .NET-Abdeckung, Lauf 2 | 292.511 / 641.687 = **45,58 %** |
| Geforderte Abdeckung | **45,60 %** |
| GitHub-CI für den geprüften Commit | **fehlgeschlagen** |

Die beiden Abdeckungsläufe unterschieden sich bei neun ausgeführten Zeilen,
rundeten aber beide auf 45,58 %. Der rote Grenzwert ist damit kein einmaliger
Messausreisser.

## Priorität 1 — vor einer Freigabe beheben

### P1-1: Das Python-Paket lässt sich in der frischen CI nicht installieren

**Beleg**

- Die CI installiert zuerst ungesperrtes CPU-Torch und danach das Sidecar mit
  `pip install -e ".[dev]"` (`.github/workflows/ci.yml:75-80`).
- Das Build-Backend ist als `setuptools.backends._legacy:_Backend` eingetragen
  (`sidecar/pyproject.toml:24-26`). Dieses Modul existiert in der verwendeten
  Setuptools-Version nicht.
- Der aktuelle GitHub-Lauf endet deshalb mit
  `BackendUnavailable: Cannot import 'setuptools.backends._legacy'`.
- Lokal bestätigt: `setuptools.backends` fehlt; das normale Legacy-Backend ist
  als `setuptools.build_meta:__legacy__` vorhanden.

**Auswirkung**

Die Sidecar- und QGIS-Tests werden serverseitig gar nicht gestartet. Ein schon
fertig eingerichtetes lokales `.venv` verdeckt den Fehler.

**Verbesserung**

1. Das korrekte Setuptools-Backend verwenden.
2. Einen sauberen Paketbau und eine Editable-Installation in einem leeren
   Python-Umfeld als eigenen CI-Schritt prüfen.
3. Erst danach die Sidecar-Tests starten.

### P1-2: Pipeline-Tests sind nicht unabhängig vom Entwicklerrechner

**Beleg A — YOLO-Klassenkarte**

- `YoloClassVsaMapperTests` sucht die produktive
  `sidecar/models/yolo26m/yolo26m.names.json` und bricht bei Fehlen ab
  (`tests/AuswertungPro.Next.Pipeline.Tests/YoloClassVsaMapperTests.cs:87-137`).
- `*.names.json` ist bewusst ignoriert (`sidecar/models/.gitignore:7`). Die
  lokale Datei ist daher auf dem GitHub-Runner nicht vorhanden.

**Beleg B — FFmpeg-Erkennung**

- `FfmpegFactAttribute` betrachtet jeden nicht absoluten Namen wie `ffmpeg`
  bereits als verfügbar, ohne den Prozess auszuführen
  (`tests/AuswertungPro.Next.Pipeline.Tests/FfmpegFactAttribute.cs:22-29`).
- Der Test startet danach `ffmpeg` wirklich
  (`tests/AuswertungPro.Next.Pipeline.Tests/TrainingSampleOsdMeterSourceTests.cs:98-113`).
- Auf dem GitHub-Runner fehlt FFmpeg. Der Test wird deshalb nicht übersprungen,
  sondern schlägt mit `Win32Exception` fehl.

**Auswirkung**

Im aktuellen GitHub-Lauf scheitern zwei Pipeline-Tests. UI-Tests,
ProjectModernizer und Abdeckungsprüfung werden danach übersprungen.

**Verbesserung**

1. Eine kleine, versionierte Klassenkarten-Fixture ohne Modellgewichte für den
   Mapping-Test einchecken.
2. FFmpeg in der CI in einer festen Version installieren oder den Test in einen
   reinen Verhaltenstest und einen klar markierten Integrationstest teilen.
3. Die FFmpeg-Prüfung muss `ffmpeg -version` mit Zeitlimit wirklich ausführen,
   statt einen nicht absoluten Namen automatisch als vorhanden zu werten.

### P1-3: Der Abdeckungswächter ist rot

**Beleg**

- Die CI verlangt 45,60 % (`.github/coverage-baseline.json:3-10`).
- Zwei vollständige lokale Messungen ergaben jeweils 45,58 %.
- Der GitHub-Lauf erreicht diesen Schritt wegen P1-2 noch nicht. Nach Behebung
  der früheren Fehler würde die Prüfung nach aktuellem Stand ebenfalls rot.

**Auswirkung**

Die Aussage „alles grün“ stimmt für den geprüften Commit nicht. Die Tests selbst
laufen, aber die eigene Freigaberegel wird verletzt.

**Verbesserung**

- Die Grenze nicht absenken. Fehlende Verhaltenstests ergänzen, besonders für
  die neuen Import-, Speicher- und PDF-Wege.
- Danach den Ratchet neu messen und nur nach oben anpassen.
- Mittelfristig Test- und generierten Code aus der Kennzahl herausfiltern. Die
  heutige Zahl ist als Verlaufsschutz brauchbar, aber kein reines Mass für den
  Produktivcode.

## Priorität 2 — Sicherheit und Wartbarkeit

### P2-1: Python-Prüfung und Python-Testumgebung sind zwei verschiedene Welten

**Beleg**

- `security/audit_lock.py` prüft die produktive `requirements-lock.txt`.
- Die CI installiert danach nicht diese Sperrdatei, sondern aktuelle CPU-Versionen
  von Torch/Torchvision und die offenen Mindestversionen aus
  `sidecar/pyproject.toml:6-22` (`.github/workflows/ci.yml:68-80`).
- Im fehlgeschlagenen Lauf wurde zum Beispiel Torch 2.13.0+cpu gewählt, während
  die Produktion Torch 2.12.0.dev...+cu128 sperrt.
- Die Sperrdatei enthält feste Versionen, aber keine einzige `--hash=`-Zeile.
  `sidecar/README.md:72-76` behauptet dagegen, die Hashes seien vorhanden.

**Auswirkung**

Die Sicherheitsprüfung gilt nicht für genau die Paketkombination, gegen die die
CI-Tests laufen. Ein zukünftiges Paketupdate kann Tests ohne Codeänderung brechen.
Die Installation ist versionsgebunden, aber nicht an konkrete Paketdateien
gebunden.

**Verbesserung**

1. Einen eigenen, gesperrten CPU-CI-Lock erzeugen.
2. Den Sidecar in der CI mit `--no-deps` aus diesem Lock installieren und testen.
3. Produktions- und CI-Lock beide prüfen und die direkten Abhängigkeiten
   automatisch gegeneinander abgleichen.
4. Hashes dort erzeugen, wo die verwendeten Indizes sie dauerhaft anbieten. Für
   CUDA-Nightlies und Git-Pins zusätzlich Commit- und Artefaktprüfungen behalten.

### P2-2: Lokale HTTP-Server begrenzen den Body, aber nicht die Kopfzeilen

**Beleg**

- Live-Control und QGIS lauschen nur auf `IPAddress.Loopback` und verwenden ein
  Token. Das ist gut.
- Beide lesen Anfragezeile und Header mit unbegrenztem `ReadLineAsync`
  (`LiveControlServer.cs:193-222`, `QgisBridgeServer.cs:167-190`).
- Live-Control begrenzt erst danach den Body auf 64 KiB
  (`LiveControlServer.cs:224-239`).
- Der gemeinsame Schutz setzt 15 Sekunden Zeitlimit
  (`Helpers/LoopbackHttpServerSafety.cs:7-10`), aber keine maximale Zeilenlänge,
  Headeranzahl oder Gesamtheadergrösse.
- Die Anmeldung wird erst nach dem vollständigen Einlesen geprüft.

**Auswirkung**

Kein normaler Fernangriff: Die Server sind nur lokal erreichbar. Ein bösartiger
oder defekter Prozess desselben Windows-Benutzers kann aber vor der Anmeldung
sehr grosse Header senden und unnötig Speicher binden.

**Verbesserung**

Einen gemeinsamen begrenzten Request-Leser verwenden, zum Beispiel mit maximaler
Anfragezeile, maximaler Headerzeile, maximaler Headeranzahl und 16–32 KiB
Gesamtgrenze. Für beide Server echte Socket-Tests ergänzen. Die vorhandenen
QGIS-Grenztests prüfen überwiegend Quelltextzeichenfolgen
(`QgisBridgeSecurityBoundaryTests.cs:8-67`), nicht das Serververhalten.

### P2-3: Der NPK-PDF-Weg umgeht die vorhandene Architekturschicht

**Beleg**

- `BuilderPageViewModel` besitzt bereits einen injizierten PDF-Exportdienst
  (`BuilderPageViewModel.cs:43-46`, `:145-155`).
- Der normale Kosten-PDF-Weg verwendet ihn korrekt
  (`BuilderPageViewModel.Output.cs:165-168`).
- Der NPK-Weg baut Vorlagenpfade selbst und erzeugt direkt
  `new OfferHtmlToPdfRenderer()`
  (`BuilderPageViewModel.Output.cs:559-563`).
- Der Druckweg startet ausserdem direkt `Process.Start`
  (`BuilderPageViewModel.Output.cs:618-626`).
- Für die beiden UI-Befehle wurden keine direkten Verhaltenstests gefunden.

**Auswirkung**

Datei-, Vorlagen-, Browser- und Prozesslogik liegt wieder im ViewModel. Das macht
Fehlerbehandlung und Tests schwieriger und widerspricht der sonst erreichten
Schichtentrennung. Ein konkreter Sicherheitsangriff wurde hier nicht gefunden.

**Verbesserung**

Einen kleinen `INpkOfferPdfExportService` und einen PDF-Druckdienst hinter
Application-Verträgen einführen. Pfade und konkrete Renderer bleiben in
Infrastructure. Beide Commands mit Erfolgs-, Abbruch- und Fehlerfall testen.

### P2-4: Der .NET-SDK-Stand ist noch nicht vollständig reproduzierbar

**Beleg**

- `global.json` nennt 10.0.108, erlaubt aber `latestFeature`
  (`global.json:3-4`).
- GitHub installiert allgemein `10.0.x` (`.github/workflows/ci.yml:23-25`).

**Auswirkung**

NuGet-Pakete sind gesperrt, Compiler und SDK können sich auf dem Runner trotzdem
ohne Repositoryänderung verschieben.

**Verbesserung**

In GitHub genau 10.0.108 installieren und das Roll-Forward bewusst enger setzen.
SDK-Updates danach als eigene, geprüfte Änderung durchführen.

## Codequalität und Architektur

### Was gut ist

- Keine Produktiv-C#-Datei liegt über 1.000 Zeilen.
- Die Importtransaktion verwendet denselben Journal- und Recovery-Weg wie der
  manuelle Import. Veröffentlichung, Projektstempel und Rücknahme sind getrennt
  und umfassend getestet.
- Training-Center-Speicher und Wissenssicherung wurden aus der UI ausgelagert,
  ohne die öffentlichen Fassaden zu brechen.
- Nicht-Event-`async void` wurde entfernt beziehungsweise über den zentralen
  sicheren Fire-and-forget-Weg geführt.
- CSV-Formelzellen und Medienpfade werden zentral geschützt.
- QGIS verlangt auf beiden Wegen ein Token und vergleicht es konstantzeitnah.
- Die grüne KI-Ampel verlangt zwei unterschiedliche Belegquellen. Mehrere Werte
  aus demselben Sprachmodell zählen gemeinsam nur als eine Quelle
  (`EvidenceSourceGrouping.cs:21-52`, `QualityGateService.cs:81-104`).
- Keine auffälligen fest eingebauten Zugangsdaten, keine TLS-Abschaltung und
  keine gefährliche Binärdeserialisierung wurden gefunden.

### Verbleibender Grössendruck

Mehrere Produktivdateien stehen sehr nahe an der 1.000-Zeilen-Grenze:

| Datei | Zeilen |
|---|---:|
| `AnnotationWorkbenchService.cs` | 1.000 |
| `TrainingStudioViewModel.cs` | 997 |
| `TrainingCenterViewModel.cs` | 997 |
| `MultiModelAnalysisService.cs` | 982 |
| `SanierungsMatrixPageViewModel.cs` | 976 |
| `WinCanDbImportService.cs` | 974 |
| `ServiceProvider.cs` | 973 |

Das ist noch kein Fehler, aber der Wächter lässt praktisch keinen Platz mehr.
Neue Fachlogik sollte diese Klassen nicht weiter vergrössern. Besonders die
beiden Training-ViewModels und der Annotation-Workbench-Dienst sind die nächsten
Kandidaten für kleine Controller- oder Use-Case-Auslagerungen.

Auch zwei Werkzeuge sind gross (`CadasterDbReader/Program.cs` rund 1.600 Zeilen,
`EvalSetBenchmark/Program.cs` rund 1.200 Zeilen). Sie sollten bei der nächsten
fachlichen Änderung in Parser, Use Case und Ausgabe getrennt werden; ein Umbau
ohne Anlass ist nicht nötig.

## Sicherheitsstand der Abhängigkeiten

### .NET

Die echte Prüfung über alle 51 Projekte meldet keine bekannten verwundbaren
NuGet-Pakete. Actions sind auf vollständige Commit-Hashes gepinnt; die drei
verwendeten Hashes wurden gegen die offiziellen `actions/*`-Repositories
aufgelöst. Der Restore verwendet `--locked-mode`.

### Python

Der Lock-Audit meldet fünf bekannte, dokumentierte Ausnahmen:

- vier Meldungen für `transformers 4.57.6`; zwei ohne verfügbaren Fix, zwei erst
  in Version 5.x, die den real geprüften Grounding-DINO-Weg bricht;
- eine Meldung für `setuptools 81.0.0`; der Fix verlangt >=83, während der
  gesperrte Torch-Stand `<82` verlangt.

Das ist eine begründete Ausnahme, aber nicht dasselbe wie „keine Lücken“.
Ausserdem sind Torch, Torchvision und der Git-Pin von SAM-2 nicht durch
`pip-audit` prüfbar (`sidecar/security/lock_audit_exceptions.json:48-60`).
Die installierte lokale Umgebung ist mit 91 Paketen intern konsistent.

## KI-Funktionsbewertung

### Technische Sicherheit

Die technische Begrenzung ist gut: nicht qualifizierte Modelle werden nicht
still freigegeben, KI-Vorschläge bleiben prüfpflichtig, Eval-Daten sind gegen
Training geschützt und die Ampel kann sich nicht mehr mit derselben
Sprachmodellquelle selbst bestätigen.

### Fachliche Modellqualität

Die KI ist weiterhin **nicht für automatische Entscheidungen freigegeben**:

- `sidecar/models/model_qualification.json:4-6` markiert den allgemeinen
  Detektor ausdrücklich als `qualified: false`.
- Der letzte dokumentierte allgemeine Detektor erreicht Precision 37,9 %, Recall
  10,3 % und F1 16,2 %
  (`docs/quality/DETECT-RELEASE-DIAGNOSTIC-2026-08-03.md:41-42`).
- Der Bogen-Copilot erreicht auf seinem breiteren PDF-Vergleich 77,6 % Recall
  und 60,3 % Precision. Das reicht als Hilfswerkzeug mit Pflichtbestätigung,
  nicht als Automatik
  (`docs/quality/BCC-PDF-RECALL-2026-08-09.md:11-24`, `:129-131`).
- Der als „aktuell“ abgelegte zentrale Qualitätsbericht stammt vom 11. Juli und
  enthält 0 von 0 geprüfte grüne Entscheidungen; das Freigabekriterium ist nicht
  erfüllt (`docs/quality/aktuell/ai_quality_20260711_150603.md:3-28`).

**Verbesserung**

1. Den zentralen Qualitätsbericht mit dem heutigen Datenstand neu erzeugen.
2. Automatische Freigabe weiterhin ausgeschaltet lassen.
3. Mindestens 300 blind geprüfte grüne Fälle aus 20 Haltungen sammeln und die
   bereits definierte obere Fehlergrenze einhalten.
4. BCC weiterhin als Copilot mit menschlicher Bestätigung verwenden.
5. Einen neuen allgemeinen Detektor erst nach unabhängigem Holdout und
   Artefakt-SHA freigeben.

## Nicht vollständig praktisch geprüft

Folgende Punkte sind durch Code, Unit-Tests oder vorhandene Messberichte belegt,
aber in diesem Audit nicht als vollständiger realer Anwenderlauf wiederholt
worden:

- GPU-Lauf mit YOLO, Grounding DINO und SAM auf echter Hardware;
- Ollama/Qwen mit realem Modell;
- maschinengebundener Video-Golden-Test und BCC-Live-Abnahme;
- WPF-Kindprozess-Smoke-Test;
- VSA-KEK-Archivtest mit externer Fixture;
- vollständiger manueller Klickweg von Import bis PDF-Druck;
- Programmabsturz mitten in einer laufenden Importtransaktion.

Diese Grenzen sind wichtig: 12.189 grüne Tests beweisen viel, aber nicht jeden
realen Kundenablauf und nicht die fachliche Qualität eines KI-Modells.

## Empfohlene Reihenfolge

1. **Python-Build-Backend korrigieren** und Installation in leerer Umgebung
   prüfen.
2. **CI-Pipeline-Tests hermetisch machen:** Klassenkarten-Fixture und FFmpeg.
3. **Abdeckung wieder über 45,60 % bringen**, ohne die Grenze abzusenken.
4. **CPU-CI-Lock einführen** und Python-Dokumentation zu Hashes korrigieren.
5. **Lokale HTTP-Header begrenzen** und echte Socket-Tests ergänzen.
6. **NPK-PDF und Drucken aus dem ViewModel auslagern.**
7. **SDK exakt pinnen.**
8. **Aktuellen KI-Qualitätsbericht erzeugen** und Freigabedaten weiter sammeln.

## Schlussfazit

Die grossen Umbauten waren fachlich sinnvoll und haben die wichtigsten
Sicherheits- und Dateirisiken des ersten Audits deutlich reduziert. Lokal ist
der Stand stabil. Die erneute Prüfung hat aber gezeigt, warum eine unabhängige
CI wichtig ist: Der Server kann den Stand derzeit nicht vollständig bauen und
prüfen, obwohl er auf dem Entwicklerrechner grün erscheint.

Darum lautet das ehrliche Ergebnis: **Programm lokal funktionsfähig und deutlich
verbessert, aber CI und KI-Freigabe noch nicht grün.**
