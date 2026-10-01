# Gesamtprogramm: Code- und Fehleraudit

**Stand:** 28.09.2026, Arbeitsbaum auf `ff07a9bbc` mit parallel laufenden, noch nicht eingecheckten WebGIS-Änderungen.  
**Umfang:** Windows-WPF-Anwendung, Projekt- und Dateiverarbeitung, Import/Export und Verteilung, WebGIS, Sicherungen, KI-Pipeline samt Python-Sidecar und Trainingsskripten, QGIS-Brücke, Werkzeuge und CI.  
**Art:** Quellcodeprüfung, vollständiger Release-Build, alle vier .NET-Testprojekte, Python- und QGIS-Tests sowie Paketprüfung. Keine Kundendaten, kein produktives WebGIS und keine GPU-Modelle ausgeführt.

## Urteil

Das Programm **baut und besteht die ausführbaren automatisierten Tests**. Die Normalwege sind deutlich abgesichert. Eine Aussage, dass **das ganze Programm fehlerfrei oder WebGIS zu 100 % robust** ist, lässt sich daraus nicht ableiten. Besonders der direkte WebGIS-Schreibweg und der Abgleich der Verteilordner haben noch offene Datenrisiken. Für sie sind gezielte Randfalltests und eine kontrollierte Abnahme nötig.

Diese Prüfung ist breit, aber keine manuelle Zeile-für-Zeile-Abnahme aller 3'244 C#-Quelldateien. Ein großer Teil der Funktion hängt von echten Projektdaten, Windows-Dateisystemzuständen, GEONIS, QGIS, FFmpeg, Ollama und GPU-Modellen ab. Diese Grenzen werden unten ausgewiesen.

## Prüfstand und Ergebnisse

| Prüfung | Ergebnis |
| --- | --- |
| Vollständiger Release-Build `AuswertungPro.sln` | **Bestanden**, 0 Fehler, 0 Warnungen; nach WebGIS-Änderungen erneut bestanden |
| Infrastrukturtests | **7'566 bestanden**, 6 übersprungen, 0 Fehler |
| Pipeline-Tests | **2'882 bestanden**, 3 übersprungen, 0 Fehler |
| UI-Tests | **7'496 bestanden**, 34 übersprungen, 0 Fehler |
| ProjectModernizer | **62 bestanden**, 0 Fehler |
| Sidecar ohne GPU | **577 bestanden**, 2 abgewählt; zwei Umgebungswarnungen ohne Testfehler |
| Trainingsskripte, eigene frische CPU-Umgebung | **325 bestanden, 46 Subtests bestanden** |
| QGIS-Brücke | **14 bestanden** |
| WebGIS-Fokus nach erneutem Build | **534 bestanden**, 0 Fehler |
| Verteil-Fokus nach erneutem Build | **8 bestanden**, 0 Fehler |
| Zwei temporäre Abgleich-Proben | **2 bestanden**; beide beschriebenen Fehlverhalten ausgelöst, Testdatei danach entfernt |
| NuGet-Abfrage mit transitiven Paketen | Für alle Projekte **keine bekannten anfälligen Pakete** gemäß aktueller NuGet-Quelle |
| Python-Sperrdatei mit `pip-audit` | **88 PyPI-Pins geprüft**; 10 Rohmeldungen fallen unter 6 dokumentierte Ausnahmen; 3 CUDA-/Git-Pins nicht automatisch prüfbar; Exit-Code 0 |

Der erste Build war nur durch einen bereits hängenden `testhost` blockiert. Nach Prüfung des Prozesses wurde ausschließlich dieser Testprozess beendet; der Build lief danach sauber. Ein späterer **temporärer Probetest-Build** kollidierte erneut mit einem inzwischen parallel gestarteten Infrastruktur-Testlauf eines anderen Vorgangs. Das ist eine Dateisperre zwischen Prüfprozessen, kein Compiler- oder Produkttestfehler. Dieselben zwei Proben wurden danach mit getrenntem Ausgabeordner erfolgreich ausgeführt und anschließend aus dem Repo entfernt.

**Wichtig zum Zeitbezug:** Während dieses Audits wurde WebGIS-Code parallel geändert. Die erste Gesamt-Testreihe prüfte einen früheren kompilierten Stand. Danach wurden der aktuelle Quellstand noch einmal gebaut und die 534 WebGIS-Tests ausgeführt. Die neuen WebGIS-Dateien sind weiterhin uncommittet; die endgültige Abnahme muss nach Abschluss dieser Arbeit erneut den vollen Release-Weg prüfen.

## Befunde nach Priorität

### 1. Hoch: Neue WebGIS-Sanierungsmaßnahmen werden nach dem Anlegen nicht zurückgelesen

In [`WebGisExportUseCase.cs`](../../../src/AuswertungPro.Next.Application/WebGis/WebGisExportUseCase.cs) setzt `LegeEineAnAsync` nach `ErstelleSanierungAsync` direkt `Geschrieben = res.Erfolg` und übernimmt die neue ID. Anders als bei Objektfeldern folgt **kein erneutes Lesen** der Maßnahme. Damit sind gespeicherte Feldwerte und Elternbezug nicht nachgewiesen. Ein Server kann den Schreibaufruf bestätigen, aber Daten anders ablegen; ein Verbindungsabbruch nach dem POST lässt den Ausgang offen und kann bei Wiederholung zur Dopplung führen. Der striktere Antwortprüfer in den laufenden WebGIS-Änderungen reduziert falsche OK-Meldungen, ersetzt aber diese Nachkontrolle nicht.

**Umsetzung:** Neue ID prüfen, Maßnahme erneut lesen, Eltern-GlobalID und jedes geplante Feld vergleichen. Bei fehlendem Nachweis ausdrücklich „Ausgang offen“ statt „geschrieben“ melden. Vor einem erneuten Anlegeversuch anhand der Elternliste und Merkmale nach vorhandener Maßnahme suchen. Mit nachgebildeter Serverantwort „bestätigt, aber nicht gespeichert“ und „Timeout direkt nach POST“ testen. Siehe auch den [ausführlichen WebGIS-Bericht](../2026-09-28-webgis-robustheit/BERICHT.md).

### 2. Hoch: Der Verteilabgleich führt einen veralteten Plan ohne erneute Fachprüfung aus

[`DistributionReconciliationService.Plan`](../../../src/AuswertungPro.Next.Infrastructure/Export/DistributionReconciliationService.cs) bestimmt anhand des offenen Projekts, welche Ordner keine Haltung oder keinen Schacht haben. [`Apply`](../../../src/AuswertungPro.Next.Infrastructure/Export/DistributionReconciliationService.cs) erhält danach **nur noch den Plan**, nicht das aktuelle Projekt. Es prüft vor dem Verschieben bloß, ob der Quellpfad unter dem Projektordner liegt und existiert. Wird in der Zwischenzeit eine passende Haltung oder ein Schacht ergänzt, verschiebt `Apply` dessen Ordner trotzdem in den Papierkorb. Die Oberfläche baut den Plan asynchron, zeigt eine Bestätigung und startet erst danach `Apply` ([`ExportPageViewModel.Reconciliation.cs`](../../../src/AuswertungPro.Next.UI/ViewModels/Pages/ExportPageViewModel.Reconciliation.cs)). Eine temporäre Probe ergänzte nach der Vorschau eine passende Haltung: Der Ordner wurde trotzdem verschoben. Die Probe verwendete ausschließlich künstliche Dateien in einem temporären Ordner.

**Umsetzung:** Vor jedem Verschieben die aktuelle Projektbindung und die Zuordnung frisch prüfen; bei Änderung Vorschau verwerfen und neu anzeigen. Einen Test „Plan: Waise; vor Apply passende Haltung ergänzt“ aufnehmen. Die Ausgabe muss dann unberührt bleiben.

### 3. Hoch, bedingt: Der Zielpfad des Verteil-Papierkorbs hat keinen Verknüpfungsschutz

`Apply` kontrolliert nur den **Quellpfad** textuell. Das Ziel `Papierkorb/<Zeit>/...` wird mit `Directory.CreateDirectory` angelegt und mit `Directory.Move`/`File.Move` beschrieben, ohne die Zielkette auf Junctions oder symbolische Links zu prüfen ([`DistributionReconciliationService.cs`](../../../src/AuswertungPro.Next.Infrastructure/Export/DistributionReconciliationService.cs)). Zeigt `Papierkorb` als Verknüpfung nach außerhalb, kann der Vorgang entgegen der Benutzerzusage „Papierkorb des Projekts“ Daten dorthin verschieben. Dieser bedingte Pfad wurde im Code nachgewiesen, **nicht** mit einer Junction-Probe ausgeführt.

**Umsetzung:** Den im Projekt bereits verwendeten Pfadschutz für Quell- **und** Zielkette verwenden, auch unmittelbar vor der Bewegung. Mit einem künstlichen Projektordner und einer Junction auf einen anderen temporären Ordner testen: keinerlei Bewegung außerhalb des Projekts.

### 4. Mittel: Der Verteilabgleich mischt Haltungs- und Schachtnamen

`SammleBekannteNamen` wirft Namen beider Objektarten in **dieselbe** Menge. `PruefeOrdner` verwendet diese Menge für `Haltungen_Verteilt` und `Schächte_Verteilt` ([`DistributionReconciliationService.cs`](../../../src/AuswertungPro.Next.Infrastructure/Export/DistributionReconciliationService.cs)). Hat das Projekt eine Haltung `A-B`, aber keinen Schacht `A-B`, bleibt ein verwaister Schachtordner `A-B` fälschlich liegen. Eine temporäre Probe bestätigte genau diesen Fall. Das verursacht kein Löschen, macht den Abgleich aber sachlich unvollständig.

**Umsetzung:** Zwei getrennte Namensmengen und Tests für gleiche Kennungen in beiden Objektarten. Die bisherige Umkehrregel `A-B`/`B-A` nur bei Haltungen anwenden.

### 5. Mittel: WebGIS bleibt bei gleichzeitiger Fremdänderung ohne serverseitige Bedingung offen

[`GeonisWebGisClient.cs`](../../../src/AuswertungPro.Next.Infrastructure/WebGis/GeonisWebGisClient.cs) liest unmittelbar vor `saveData` erneut und vergleicht den Stand. Zwischen diesem Lesen und dem getrennten POST kann ein anderer Benutzer denselben Datensatz ändern. Das ist im Code selbst als Restfenster benannt. Auch beim Anlegen einer Maßnahme liegt zwischen Dublettenprüfung und POST ein solches Fenster. Mehr lokale Prüfungen können es nicht vollständig schließen.

**Umsetzung:** Mit dem GEONIS-Anbieter einen bedingten Schreibaufruf mit Versionskennung oder einen Idempotenzschlüssel klären. Ohne Serverfunktion bleibt eine organisatorische Sperre plus Nachkontrolle für produktive Schreibläufe notwendig.

### 6. Mittel: WebGIS-Lesefehler sind nicht durchgehend nach Ursache getrennt

[`GeonisWebGisClient.LiesLayoutAsync`](../../../src/AuswertungPro.Next.Infrastructure/WebGis/GeonisWebGisClient.cs) liefert bei Nicht-2xx `null`. Ein abgelaufener Login (401/403), Serverfehler (5xx) und „nicht gefunden“ können dadurch im Aufrufer ähnlich aussehen. Der Suchendpunkt unterscheidet Sitzungsfehler bereits ausdrücklich. Das Risiko ist hier vor allem ein unklarer oder unvollständiger Lese-/Importlauf; die fragliche Antwort löst selbst keinen Schreibaufruf aus.

**Umsetzung:** 401/403 an jedem GEONIS-Endpunkt als Sitzungsfehler, 5xx/Timeout als Netz- oder Serverfehler und 404 als „nicht gefunden“ behandeln. Ein kleiner Satz nachgebildeter Antworten genügt als Test.

### 7. Mittel: Python-Paketlücken sind bewusst offen; drei Modellpins sind nicht automatisch geprüft

Die aktuelle Prüfung der produktiven [`requirements-lock.txt`](../../../sidecar/requirements-lock.txt) fand 10 Rohmeldungen, die auf 6 Einträge in [`lock_audit_exceptions.json`](../../../sidecar/security/lock_audit_exceptions.json) abgebildet werden. Betroffen sind `transformers` und `setuptools`. Die Ausnahmedatei begründet die derzeitigen Versionszwänge. `torch`, `torchvision` (lokale CUDA-Builds) und SAM 2 (Git-Pin) bleiben außerhalb dieser automatischen Prüfung. Für NuGet meldete die aktuelle Abfrage keine bekannten Funde. Eine fehlende Meldung ist kein Sicherheitsbeweis: [`pip-audit`](https://github.com/pypa/pip-audit) prüft bekannte Paketmeldungen aus der [PyPA-Advisory-Datenbank](https://github.com/pypa/advisory-database), nicht den gesamten Anwendungscode oder jede eingebettete Bibliothek.

**Umsetzung:** Die sechs Ausnahmen regelmäßig fachlich neu bewerten, den Grounding-DINO-Paketzwang gezielt ablösen und für die drei Modellpins einen eigenen nachvollziehbaren Herkunfts- und Sicherheitsnachweis pflegen. Ausnahmen nicht ohne Kompatibilitätsprobe entfernen.

### 8. Wartbarkeit und Nachweisgrenzen

Die zwei früher über der 1'000-Zeilen-Regel liegenden Produktionsdateien wurden verkleinert; der entsprechende UI-Test ist nun grün. Längere Abläufe bestehen weiterhin, etwa `MultiModelAnalysisService.AnalyzeAsync` und verschiedene UI-ViewModels. Größe allein ist kein Fehler, erhöht aber die Kosten für sichere Änderungen. Die CI sammelt Codeabdeckung, die bestehende Mindestgrenze umfasst laut [`coverage-baseline.json`](../../../.github/coverage-baseline.json) noch Test- und erzeugten Code. Die neue Messung nur für Produktcode ist in [`measure-product-coverage.ps1`](../../../.github/scripts/measure-product-coverage.ps1) zunächst reine Anzeige; eine belastbare Grenze fehlt noch.

**Umsetzung:** Kleine, fachlich geschlossene Phasen mit Verhaltenstests auslagern; nach einer echten CI-Messung eine Produktcode-Grenze festlegen. Keine Großumstellung allein wegen Dateilänge.

## Bereiche ohne neu bestätigten Funktionsfehler

| Bereich | Was geprüft wurde | Aussagegrenze |
| --- | --- | --- |
| Projektdatei, Öffnen, Speichern, Rettung | `JsonProjectRepository`, `ProjectRecoveryService`, Wiederherstellungs- und Serialisierungstests; atomare Zwischendatei und `.bak` vorhanden | Kein Stromausfall- oder Netzlaufwerkstest mit echtem Projekt |
| Import (PDF, XTF, WinCan, IBAK, SchachtPro, Medien) | Transaktionsmarker, Staging, Kopier- und Archivpfade, XML- und Archivgrenzen; breite Infrastrukturtests | Keine vollständige Abnahme mit Kundenexporten aller Fremdformate |
| Verteilung | Haltungs-/Videoverteilung, wiederverwendete PDF nach Teilfehler, Fehlermeldungen, Zielpfadschutz; fokussierte Tests | Der gesonderte **Abgleich** hat die oben genannten Lücken; TXT-Auswahl und seltene Dateisystemwechsel nur punktuell geprüft |
| Sicherungen und Wiederherstellung | Sicherungsdienste, Pfadwachen, Journal und Tests | Keine echte vollständige Rücksicherung auf einem zweiten Rechner |
| KI-Pipeline, Training, Sidecar | Pipeline-Tests, 577 CPU-Sidecartests, 325 Trainingsskripttests, Checkpoint-/Degraded-Wege im Code | Keine GPU-Modelle, keine Qualitätsmessung an neuen Videodaten, keine Last- oder Langzeittests |
| QGIS | 14 Bridge-Tests und Schnittstellen-Code | Kein gestartetes QGIS mit realem Projekt |
| UI und Werkzeuge | 7'496 UI-Tests, 62 ProjectModernizer-Tests, Architektur- und Wartbarkeitsregeln | 34 UI-Tests übersprungen; kein vollständiger manueller Bediengang |

## Umsetzungsplan

| Paket | Arbeit | Fertig, wenn … |
| --- | --- | --- |
| **GA01** | Laufende WebGIS-Änderungen abschließen; Neuanlagen nachlesen und offene POST-Ausgänge sicher behandeln | Simulierter Server speichert falsch oder antwortet nach POST nicht: kein grünes OK und kein ungeprüftes doppeltes Anlegen; vollständiger Release-Weg grün |
| **GA02** | Verteilabgleich gegen Projektwechsel, veralteten Plan und Ziel-Junction härten; Haltungen/Schächte getrennt vergleichen | Vier Randfalltests belegen: inzwischen gültiger Ordner bleibt, Verknüpfung führt nicht nach außen, gleiche Kennung verdeckt keine Waise, normaler Abgleich bleibt richtig |
| **GA03** | WebGIS-Lesefehler eindeutig melden; GEONIS-Versions-/Idempotenzmöglichkeiten klären | 401/403/404/5xx/Timeout unterscheiden sich sichtbar; Serververtrag oder dokumentiertes Restrisiko liegt vor |
| **GA04** | Produktabdeckung aus CI als Basis übernehmen; lange KI-/UI-Abläufe paketweise verkleinern | Vergleichbare Produktcode-Messung mit Grenze und Verhaltenstests je ausgegliederter Phase |
| **GA05** | Python-Ausnahmen und Modellpins nachprüfen | Für jedes Paket ist der aktuelle Befund, Einsatzweg, Versionszwang und nächste Aktualisierungsbedingung dokumentiert |
| **GA06** | Kontrollierte Gesamtabnahme auf Kopien echter Daten | Import → Projekt speichern → Verteilung/Export → Sicherung/Rückspielung; WebGIS nur im Testsystem bzw. auf gesicherter Kopie; vor/nach jedem Datenbankwert prüfen |

**Freigabe:** Die grünen automatisierten Tests rechtfertigen die weitere Entwicklung. Für einen produktiven WebGIS-Schreibablauf mit der Forderung „100 % robust“ fehlen noch GA01, GA03 und die kontrollierte Datenbankabnahme. Absolute Fehlerfreiheit kann auch danach nicht versprochen werden; die verbleibenden Grenzen müssen sichtbar sein.
