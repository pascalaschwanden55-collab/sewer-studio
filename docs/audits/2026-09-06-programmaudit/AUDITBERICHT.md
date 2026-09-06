# SewerStudio – Programmaudit vom 06.09.2026

## Ergänzung nach Paket 1

A01–A03 sind inzwischen behoben: [Reparaturbericht](PAKET-1-ERGEBNIS.md). Aktuell 15.120 Tests bestanden, A04 rot, zwölf übersprungen. A05 bestand diesmal; die Ursache früherer Hänger bleibt offen. Die nachfolgenden Zahlen und Fundstellen dokumentieren den ursprünglichen Auditstand. Ausgangscode und neue Prüfergebnisse liegen unter `nachweise/paket1`.

**Ergebnis: Breites Code- und Testaudit durchgeführt; vollständige fachliche und manuelle Abnahme noch offen. Das Zielniveau 9/10 ist derzeit nicht nachgewiesen.**

Geprüfter Stand: c1021e76e06a7e0ba659cdff855a5c8dabce813a plus der bereits vorhandene Arbeitsbaum mit Importänderungen. Alle inventarisierten C#- und Python-Dateihashes sind am Ende der Messauswertung unverändert. Es wurde kein Produktcode geändert und keine laufende SewerStudio-Instanz beendet.

## Umfang und Grenzen

Alle 52 Lösungsprojekte inventarisiert und im Release-Modus gebaut. Unter src: 2.875 C#-Dateien und 21.772 Deklarationen von Methoden, Konstruktoren, lokalen Funktionen und Zugriffen. Insgesamt 44.275 C#-Deklarationen einschliesslich Tests und Hilfsprogrammen; zusätzlich Python-Funktionen und 605 XAML-Aktionen erfasst. 0 Python-Syntaxfehler, keine verbliebenen Merge-Marker und keine NotImplementedException in den untersuchten aktiven C#-/Python-Bereichen.

Das ist keine Behauptung, jede Deklaration manuell gelesen oder jede Kombination ausgeführt zu haben. Die breite Prüfung kombiniert Inventar, Architekturtests, vorhandene Verhaltenstests, gezielte Codeprüfung und neue Gegenproben. Kundenoriginale wurden für Gegenproben nicht verwendet. Lange reale Trainingsläufe, vollständige Lieferanten-/GEONIS-Rundreisen, optische Abnahme aller Berichte und jeder UI-Klick bleiben als konkrete Abnahmeaufgaben offen.

Die laufende Anwendung war ein bereits gestarteter Debug-Stand. Der neue Release-Stand wurde getrennt gebaut. Ein Desktop-Screenshot wurde von der automatischen Freigabeprüfung abgewiesen, weil er private, sachfremde Fenster erfasste. Die zulässige reine Textauslesung funktionierte. Ein Klick ohne Bildgeometrie wurde vom Werkzeug nicht ausgeführt. Die WPF-Prüfungen ersetzen diese vollständige manuelle Bedienprobe nicht.

## Tests und Build

Release-Build aller Projekte in getrenntem Artefaktordner: **0 Fehler, 0 Warnungen**. Der Standard-Build war durch die DLL des laufenden MCP-Hilfsdienstes gesperrt; er beweist keinen Compilerfehler. Gesperrtes Restore der bestehenden Abhängigkeiten erfolgreich, keine Paketänderung.

Erster vollständiger .NET-Lauf: **15.085 bestanden, 12 fehlgeschlagen, 12 übersprungen = 15.109**. Zehn der zwölf Fehler verschwinden bei gezielter Wiederholung mit normalen Windows-Testrechten und normalem Temp-Ordner. Bereinigter Stand derselben ursprünglichen Testfälle: **15.095 bestanden, 2 weiterhin fehlgeschlagen, 12 übersprungen**. Keine Wiederholung wird als zusätzlicher Erfolg gezählt. Die roten Tests betreffen Klassenwachstum und WPF-Kontextmenü-Hänger.

Python: **572 Sidecar-Tests ohne GPU**, **168 Agent-/Review-Tests**, **312 weitere Trainingsprüfungen** und **10 QGIS-Tests** bestanden. Weitere **13 Tests** bestehen nur mit einem diagnostischen Audit-Namensraum-Lader; der normale Importkonflikt bleibt ein Befund. 166 zusätzliche Unterfälle werden getrennt geführt. GPU-Versuch: siehe GPU-Nachweis; ein technischer Startnachweis ist keine Qualitätsmessung.

Übersprungene .NET-Fälle umfassen echte externe Auskunfts-/Video-/Katalogprüfungen und isolierte Kindprozess-Einstiegspunkte. Letztere laufen teilweise über ihre Elternprüfungen. Die Zahl 12 bedeutet deshalb nicht zwölf fehlende normale Funktionen.

## Testabdeckung

Acht erzeugte Cobertura-Berichte nach Dateipfad und Zeile vereinigt. Für erfassten Produktcode unter src: **130.115 von 174.430 messbaren Zeilen = 74,59 %**. Domain 96,18 %, Application 90,60 %, Infrastructure 78,23 %, UI 65,08 %. Nicht geladene Module fehlen im Messnenner; die Wiederholungsläufe wurden nicht erneut instrumentiert. Die Rohquote der bestehenden CI-Prüfung beträgt 46,58 % und hat einen anderen Nenner.

Deklarationen im Zeilenabgleich: 13.810 mit allen messbaren Zeilen ausgeführt; 2.880 teilweise; 4.239 ohne ausgeführte messbare Zeile; 843 ohne messbare Zeile. Verschachtelte Funktionen können Zeilen gemeinsam enthalten. Daher sind dies keine unabhängigen Funktionsabdeckungs-Prozente. Insbesondere sind 4.239 Einträge kein Beleg für 4.239 Fehler.

## Priorisierte Befunde

### A01 – Schachtumbenennung kann Kundenoriginale verändern (Sofort; Fehler bestätigt)

**Auswirkung:** Beim Ändern einer Schachtnummer kann ein Ordner ausserhalb des Projekts verschoben werden. Auch fremde PDF-Dateien gelangen ungefiltert zur Textkorrektur.

**Nachweis:** Künstliches Projekt und getrennte Kundenquelle angelegt. Umbenennung 123 → 456 meldet Erfolg. Der ursprüngliche Quellenordner verschwindet; die Datei liegt danach ausserhalb des Projekts unter 456/456.pdf. Die Oberfläche ruft genau diesen Dienst auf. Zusätzlich sammelt CollectPdfPaths absolute fremde PDF-Pfade ohne Eigentumsprüfung. Diese zweite Variante ist durch den Aufrufpfad belegt; eine echte Kunden-PDF wurde nicht verändert.

**Vorschlag:** Vor jeder Änderung Projektgrenze und Verknüpfungen prüfen. Pfade ausserhalb des Projektordners abweisen. PDF-Korrektur nur in eigenen Arbeitsdateien; Importarchive bleiben geschützt. Alle beteiligten Ordner gemeinsam vorprüfen.

**Abnahme:** Originalpfad und SHA-256 bleiben bei Umbenennung unverändert. Tests für absolute Fremdpfade, fehlenden Projektpfad, Junctions und vorhandene Zielordner bestehen. Änderungen treffen nur eigene Projektkopien.

**Fundstellen:** `src/AuswertungPro.Next.Application/Common/ShaftRenameService.cs:127`, `src/AuswertungPro.Next.UI/DataPage/SchaechteShaftRenameController.cs:33`, `src/AuswertungPro.Next.UI/DataPage/SchaechteShaftRenameController.cs:63`, `src/AuswertungPro.Next.Infrastructure/HoldingDistribution/PdfTextLayerRewriter.cs:55`

### A02 – Schacht-PDF im Unterordner wird nach Umbenennung nicht mehr gefunden (Hoch; Fehler bestätigt)

**Auswirkung:** Die gespeicherte Verknüpfung zeigt auf eine Datei, die so nicht existiert. Das Programm meldet trotzdem Erfolg.

**Nachweis:** Testdatei Schaechte_Verteilt/789/Sanierung/789.pdf. Umbenennung 789 → 987: Ordner heisst danach 987, Datei weiterhin 789.pdf. Gespeicherter Verweis erwartet jedoch Sanierung/987.pdf. File.Exists liefert false.

**Vorschlag:** Dateiumbenennungen und Verweise aus demselben geprüften Änderungsplan ableiten. Unterordner und Fotoordner einbeziehen. Fehler eines Teilordners als Fehler melden und vorherige Änderungen zurückrollen.

**Abnahme:** Nach erfolgreichem Umbenennen existiert jeder zuvor existierende verknüpfte Pfad. Unterordner, mehrere PDFs und Zielkollisionen sind abgedeckt.

**Fundstellen:** `src/AuswertungPro.Next.Application/Common/ShaftRenameService.cs:226`, `src/AuswertungPro.Next.Application/Common/ShaftRenameService.cs:278`

### A03 – Ungültige Projektdatei wird als leeres Projekt akzeptiert (Hoch; Fehler bestätigt)

**Auswirkung:** Ein beschädigtes Projekt kann scheinbar leer geöffnet werden. Die Wiederherstellung kann mit einer unbrauchbaren Sicherung Erfolg melden.

**Nachweis:** JsonProjectRepository.Load mit Dateiinhalt null liefert Ok=true und null Haltungen. Gegenprobe {broken wird korrekt abgewiesen. TryRecover mit beschädigter Hauptdatei und einer null-Sicherung liefert Recovered=true, ein leeres Projekt und verschiebt die Hauptdatei in Quarantäne. Die Quarantäne bleibt erhalten; eine endgültige Löschung wurde nicht beobachtet.

**Vorschlag:** JSON-null und unpassende Wurzeltypen ablehnen. Vor der Wiederherstellung die Struktur prüfen. Ungültige Sicherungen überspringen und die nächste gültige versuchen. Alte zulässige Projektformate weiterhin unterstützen.

**Abnahme:** null-Hauptdatei wird nicht normal geöffnet. null-Sicherung wird übersprungen. Eine ältere gültige Sicherung wird verwendet. Ein absichtlich leeres, gültiges Projekt bleibt lesbar.

**Fundstellen:** `src/AuswertungPro.Next.Infrastructure/Projects/JsonProjectRepository.cs:44`, `src/AuswertungPro.Next.Infrastructure/Projects/ProjectRecoveryService.cs:63`

### A04 – Eine zu grosse Klasse ist weiter gewachsen (Hoch; Testfehler bestätigt)

**Auswirkung:** Die vereinbarte Architekturprüfung schlägt fehl. Weitere Änderungen an dieser zentralen Importklasse werden schwerer überprüfbar.

**Nachweis:** MaintainabilityFitnessTests.Partial_types_cannot_hide_growth_across_many_small_files schlägt im Gesamtlauf und im Wiederholungslauf fehl. HoldingFolderDistributor: 3.071 Zeilen in sechs Dateien; erlaubter Bestand 3.064. Auch sieben zusätzliche Zeilen verletzen die bewusst feste Wachstumsgrenze. Die sieben zusätzlichen Zeilen stammen aus dem bereits vorhandenen, nicht committeten Import-Arbeitsbaum; nicht aus dem unveränderten Commit vom Vortag.

**Vorschlag:** Die neu hinzugekommene Verantwortung in einen passenden kleinen Dienst verlagern. Bestehende öffentliche Aufrufe behalten. Die Grenze nicht einfach hochsetzen.

**Abnahme:** Architekturtest besteht ohne Erhöhung der erlaubten Klassengrösse. Betroffene Import- und Verteilungstests bleiben grün.

**Fundstellen:** `tests/AuswertungPro.Next.UI.Tests/MaintainabilityFitnessTests.cs:17`, `tests/AuswertungPro.Next.UI.Tests/MaintainabilityFitnessTests.cs:118`, `src/AuswertungPro.Next.Infrastructure/HoldingFolderDistributor.cs:1`

### A05 – Ein Oberflächentest bleibt wiederholt hängen (Hoch; Prüfung blockiert)

**Auswirkung:** Die Funktionsprüfung der Nachschlagmenüs ist auf diesem Rechner nicht erfolgreich abgeschlossen. Der vollständige Testweg bleibt rot.

**Nachweis:** NachschlagKontextmenueTests.Das_Nachschlagmenue_haengt_an_den_richtigen_Feldern scheitert zweimal am 60-Sekunden-Limit im getrennten WPF-Prozess. Die Wiederholung ohne Abdeckungsmessung und mit normalen Windows-Rechten scheitert ebenfalls. Der genaue Hängepunkt und ein entsprechender Fehler der laufenden Anwendung sind damit noch nicht bewiesen. Die Nachprüfung des Nutzers bestätigt das Limit auch einzeln. Eine reine Lastabhängigkeit ist damit keine ausreichende Erklärung. Der namentliche Auszug aus ui-rerun.log liegt unter nachweise/paket1/nachschlag-nachweis.md.

**Vorschlag:** Den getrennten Testprozess mit Schrittmarken oder Thread-Aufzeichnung untersuchen. Den wartenden Oberflächenaufruf eingrenzen. Erst dann Test oder Produktcode gezielt korrigieren.

**Abnahme:** Der isolierte Kontextmenütest und danach die ganze UI-Suite bestehen. Die Menüs erscheinen bei leeren Feldern und erhalten fremde Eigentümerwerte.

**Fundstellen:** `tests/AuswertungPro.Next.UI.Tests/NachschlagKontextmenueTests.cs:48`, `tests/AuswertungPro.Next.UI.Tests/StaTestRunner.cs:33`

### A06 – Trainingsprüfungen kollidieren mit einem gleichnamigen Python-Paket (Hoch; Konflikt bestätigt)

**Auswirkung:** Drei Testdateien werden bei normalem Aufruf gar nicht geladen. Fehler in diesen Hilfsprogrammen könnten unbemerkt bleiben.

**Nachweis:** In der vorhandenen Sidecar-Umgebung wird training aus dem installierten SAM-2-Paket geladen. training.scripts existiert dort nicht. Betroffen sind test_osd_archiv_abdeckung_messung.py, test_osd_layout_review_bericht.py und test_osd_prefix_fallback_bericht.py. Ein nur für den Audit verwendeter Namensraum-Lader führt ihre 13 Tests erfolgreich aus. Der normale Aufruf bleibt fehlerhaft.

**Vorschlag:** Den lokalen Trainingsbereich eindeutig paketieren oder eindeutige Importe verwenden. Trainings- und Review-Tests in die automatische Prüfung aufnehmen. Die produktive Paketkombination zusätzlich reproduzierbar testen.

**Abnahme:** Alle Trainingsdateien werden mit dem normalen dokumentierten Aufruf ohne besonderen Audit-Lader gesammelt und geprüft. Der gleiche Aufruf läuft in CI.

**Fundstellen:** `training/scripts/tests/test_osd_archiv_abdeckung_messung.py:8`, `training/scripts/tests/test_osd_layout_review_bericht.py:3`, `training/scripts/tests/test_osd_prefix_fallback_bericht.py:3`, `.github/workflows/ci.yml:80`

### A07 – Der zusätzliche Trainingsagent besitzt noch vier leere Arbeitsschritte (Mittel; Komponente fehlt)

**Auswirkung:** Dieser Hilfsagent kann Export, Training, Bewertung und Vergleich noch nicht durchgängig ausführen. Das betrifft den zusätzlichen Python-Agenten; die WPF-Trainingsfunktionen sind eigenständig vorhanden.

**Nachweis:** training/agent/schicht1.py ruft export_dataset.py, train_detect.py, run_eval.py und doppellauf.py unter training/scripts auf. Alle vier Dateien fehlen. Der Code meldet das ehrlich mit ok=false und TODO. Bestehende Agententests machen daraus keinen fertigen Trainingsablauf.

**Vorschlag:** Die vier Schritte an vorhandene geprüfte Dienste anbinden oder den Agenten sichtbar als unfertig kennzeichnen. Keine zweite, abweichende Export- oder Freigabelogik aufbauen.

**Abnahme:** Ein kleiner vollständiger Agentendurchlauf erzeugt einen Datensatz, einen Kandidaten und einen Vergleichsbericht. Modellfreigabe bleibt ein gesonderter, gemessener Schritt.

**Fundstellen:** `training/agent/schicht1.py:19`, `training/agent/schicht1.py:56`

### A08 – 9/10-Erkennungsqualität ist nicht belegt (Hoch; Freigabe fehlt)

**Auswirkung:** Viele bestandene Softwaretests belegen noch keine zuverlässige Schadenserkennung. Die KI braucht eine getrennte fachliche Abnahme.

**Nachweis:** sidecar/models/model_qualification.json kennzeichnet den alten YOLO-Detektor ausdrücklich als qualified=false wegen nahezu gleicher Boxen auf unterschiedlichen Bildern. active.json beschreibt einen separaten Klassifikator und historische 57-Bilder-Messungen; das ist kein aktueller Gesamtnachweis. DINO und SAM sind zusätzliche Verfahren. Die Sperre des ungeeigneten Detektors ist eine sinnvolle Schutzfunktion.

**Vorschlag:** Klare Abnahmedaten nach Haltungen trennen. Fehler je Schadenklasse, Leerbild-Fehlalarme, Meter- und Zeitfehler sowie Laufzeiten messen. Nur Kandidaten mit vollständigem Bericht freigeben. Bei Rückfall oder fehlendem Modell einen verständlichen Status anzeigen.

**Abnahme:** Ein unabhängiger, versionierter Videosatz erreicht vorher fachlich festgelegte Grenzwerte. Datenaufteilung, Modellhash, Klassenkarte und Ergebnisbericht sind nachvollziehbar. Keine Freigabe allein wegen erfolgreichem GPU-Start.

**Fundstellen:** `sidecar/models/model_qualification.json:1`, `sidecar/models/active.json:1`, `tests/AuswertungPro.Next.Pipeline.Tests/SidecarE2eSmokeContractTests.cs:103`

### A09 – Die Python-Sicherheitsprüfung enthält weiterhin Ausnahmen (Hoch; Bekannte Restlücke)

**Auswirkung:** Der Sicherheitscheck ist dokumentiert, aber nicht frei von bekannten Meldungen. Ein Teil der Modellumgebung ist nicht durch diesen Paketcheck abgedeckt.

**Nachweis:** Aktueller Online-Lauf: 88 PyPI-Pins geprüft, sechs Meldungen durch sechs vorhandene Ausnahmen gedeckt. Betroffen sind setuptools 81.0.0 und transformers 4.57.6. Drei CUDA-/Git-Pins bleiben ungeprüft. NuGet meldet für 52 Projekte keine bekannten verwundbaren Pakete. uv bestätigt die Verträglichkeit aller 91 installierten Python-Pakete; das ist keine Sicherheitsfreigabe.

**Vorschlag:** Für jede Ausnahme Verantwortlichen, Begründung, betroffenen Aufrufpfad und erneuten Prüftermin festhalten. Eine kompatible modernisierte Modellumgebung getrennt erproben. Die drei Sonderpakete separat beurteilen.

**Abnahme:** Keine neue unbegründete Meldung. Ausnahmen besitzen aktuellen Nachweis und Termin. Die tatsächlich verwendete Kombination besteht Modell-, Sicherheits- und Integrationstests.

**Fundstellen:** `sidecar/security/lock_audit_exceptions.json:1`, `sidecar/requirements-lock.txt:1`, `sidecar/security/audit_lock.py:1`

### A10 – Die Abdeckungsgrenze ist veraltet und misst nicht nur Produktcode (Mittel; Qualitätsprüfung rot)

**Auswirkung:** Die automatische Abdeckungsprüfung scheitert trotz gestiegener Rohquote. Ihre Zahl eignet sich nicht als direkte Aussage über alle Programmfunktionen.

**Nachweis:** GitHub-Lauf 33967264002 vom 05.09.2026, Commit c1021e76: Alle vier .NET-Testschritte bestanden; der anschliessende Schritt Testabdeckung pruefen scheitert. Er meldet 358.420 von 768.970 Zeilen = 46,61 % bei 45,35 % Grenze und verlangt das Nachziehen auf 46,61 %. Der lokale Auditlauf liefert 46,58 %. Die getrennte lokale Produktcode-Auswertung vereinigt Dateipfad und Zeilennummer: 74,59 % unter src. Diese Quoten haben verschiedene Nenner. CI-Metadaten und Originalprotokoll wurden am 06.09.2026 direkt über GitHub CLI nachgeprüft und gesichert.

**Vorschlag:** Die bestehende CI-Grenze anhand des bereits belegten GitHub-Laufs 33967264002 mit 46,61 % nachziehen; einen beibehaltenen Puffer ausdrücklich dokumentieren. Keinen lokalen Messwert verwenden. Eine spätere getrennte Produktcode-Grenze benötigt eine eigene belastbare CI-Messung.

**Abnahme:** Dokumentierter Messumfang, reproduzierbare Quote und grünes Abdeckungstor. Ein mehrfach eingebundenes Modul zählt nur einmal in der Produktquote.

**Fundstellen:** `.github/scripts/check-coverage.ps1:47`, `.github/coverage-baseline.json:1`

### A11 – GEONIS-Rückabgleich ist noch kein vollständiger Arbeitsablauf (Hoch; Schnittstelle offen)

**Auswirkung:** Ein gültiger XTF-Export allein verhindert keine Konflikte mit zwischenzeitlichen Änderungen in GEONIS.

**Nachweis:** Der aktuelle Import-Fortschrittsbericht nennt zwei offene Teile ausdrücklich: kein Änderungsmanifest mit Ausgangswerten je Objekt und keine vorhandene INTERLIS2→GEONIS-Abgleichsschnittstelle. Vorhandene Export-, Kennungs- und Rundreisetests bestehen. Es wurde im Audit keine echte GEONIS-Datenbank beschrieben.

**Vorschlag:** Neu-Export und Rückabgleich im Programm klar unterscheiden. Einen Plan mit Ausgangswerten und Konfliktbehandlung entwerfen. Den Import in das Zielsystem gemeinsam an einer getrennten Testdatenbank abnehmen.

**Abnahme:** Zwischenzeitliche Änderungen werden als Konflikte angezeigt. Ein freigegebener Abgleich verändert ausschliesslich die vorgesehenen Objekte. Unbeteiligte Objekte bleiben gleich.

**Fundstellen:** `docs/PROJEKTIMPORT-TERRA-FORTSCHRITT.md:1370`

## Prüfflächen

| Bereich | Stand | Nachweis | Grenze |

|---|---|---|---|

| Start, Projektwechsel, Navigation | Automatisch geprüft | WPF-Bindungs- und Navigationsprüfungen; Registrierungen und Startpfade untersucht. | Bedienprobe jeder Seite im frisch gebauten Programm bleibt offen. |

| Projekt öffnen, speichern, retten | Befund | JSON-Speicherweg und Sicherungsauswahl gelesen; null-Gegenprobe ausgeführt. | A03: Falscher Erfolg bei null-Projekt und null-Sicherung. |

| Haltungen und Felder | Automatisch geprüft | Feldmodelle, Bearbeitung, Mehrfachauswahl und Umbenennungsprüfungen in .NET-Suiten. | Alle realen Feldkombinationen und Tastaturwege nicht manuell durchgespielt. |

| Schächte und Umbenennung | Befund | Echter Dienst mit künstlichen Ordnern ausgeführt; UI-Aufrufpfad nachgewiesen. | A01 und A02: Originalschutz und Unterordnerverweise. |

| PDF-Import und OCR | Automatisch geprüft | Parser-, OCR-, Seiten-, Wiederholungs- und Verteilungstests; Fehlerbehandlung untersucht. | Keine neue vollständige Abnahme aller Lieferanten-PDFs. |

| XTF, M150 und VSA-KEK Import | Automatisch geprüft | Parser und neue Importänderungen geprüft; zugehörige Suite besteht nach Umgebungswiederholung. | Kundendaten-Rundreise in eigener Kopie als Freigabetest ergänzen. |

| WinCan und IBAK | Automatisch geprüft | Erkennung, Befahrungsrollen, Datenbankleser und künstliche Importfälle geprüft. | Echte vollständige Orchestratorläufe aller Bestände fehlen; bestehende Reader-Nachweise sind enger. |

| Import verteilen und Konflikte | Befund | Verteilungsdienste, Wiederholung, Konfliktbilanz und Architekturgrenzen untersucht. | A04; Abbruch erfolgt teilweise nur an Schrittgrenzen. Grosse Kopierlast noch nicht gemessen. |

| Medienzuordnung, Fotos, Gegenbefahrung | Automatisch geprüft | Zuordnung, Konfliktfälle und neue Befahrungsrollen über Tests erfasst. | Gleichnamige Medien und Gegenfahrten mit Kundensatz vollständig abnehmen. |

| Video und Protokollbearbeitung | Automatisch geprüft | Player-Controller, Zeit-/Meterzuordnung, Protokollabläufe und Bindungen getestet. | Keine durchgehende manuelle Aufnahme bis zum fertigen Protokoll; native Videoformate nicht alle abgespielt. |

| VSA-Codierung und Schattenauswertung | Automatisch geprüft | Regeln, Codes, Vorschläge und Berichtsteile in Domain-, Application- und Pipeline-Tests. | Fachliche Vollständigkeit aller Regelkombinationen nicht neu gegen Normoriginale abgenommen. |

| PDF-Ausgabe und Druckcenter | Automatisch geprüft | Export- und Modelltests; PDF-Korrektur-Aufrufpfade geprüft. | A01; optische Druckprüfung aller Vorlagen und Drucker offen. |

| XTF-Ausgabe und GEONIS | Befund | Export, stabile Kennungen und synthetische Rundreise vorhanden und getestet. | A11: Rückabgleich und Konflikte mit neuerem Zielbestand offen. |

| Dossiers und kantonale Auskünfte | Teilweise geprüft | Lokale Tests, Datenaufbereitung und Quellregeln erfasst. | Vier Live-Tests der externen Auskunft wurden übersprungen. |

| Sanierungs- und Schachtmatrix | Automatisch geprüft | Fachmodelle, Berechnung, Vorgaben und UI-Workflows über vorhandene Suiten. | Fachliche Vergleichsrechnung mit einem freigegebenen kompletten Auftrag ergänzen. |

| Kosten, Devis, Angebote, NPK | Automatisch geprüft | Kostenmodelle und Ausgabeprüfungen; grosse Berechnungsfunktionen inventarisiert. | Keine neue Preis- oder Tarifabnahme, kein neues vollständiges Devis gegengerechnet. |

| Karte, Kataster und QGIS | Teilweise geprüft | 10 QGIS-Brückentests bestanden; Kataster- und Kennungsprüfungen in .NET-Suiten. | QGIS-Bedienung mit echter Karte und vollständiger Hin-/Rückübertragung offen. |

| Datensicherung und Wiederherstellung | Automatisch geprüft | Backup-, Pfadschutz-, SQLite-Schnappschuss- und Goldarchivprüfungen ausgeführt. | A03; kein Wiederaufbau auf einem leeren zweiten Windows-System durchgeführt. |

| Einstellungen und Diagnose | Teilweise geprüft | Speicher-/Diagnosetests; laufendes Fenster gezielt als Text gelesen. | Vollständiger Klicktest durch Sicherheits-/Geometriegrenze des UI-Werkzeugs nicht möglich. |

| Nachschlagmenüs | Befund | Isolierter WPF-Test zweimal ausgeführt. | A05: wiederholter Hänger; genaue Ursache noch offen. |

| Sidecar: Zugriff und Fehler | Automatisch geprüft | 572 Tests ohne GPU: Token, Host, Eingabegrössen, Pfade, Fehlermeldungen, Last- und Zustandsregeln. | Route-spezifisch kleinere frühe Grenzen und längeren Lasttest vorschlagen. |

| YOLO: Schäden und Bildklassen | Befund | Qualifikationsdatei, aktive Klassenmodellwahl, Verträge und Klassenkarten geprüft. | A08: alter Detektor gesperrt; fachliche 9/10-Abnahme fehlt. |

| DINO und SAM | Teilweise geprüft | Ladepfade, Sperren, Grafikspeicherverwaltung und Fehlerverträge geprüft. GPU-Versuch separat protokolliert. | Ein Start-/Schematest belegt keine richtige Schadensbox oder Maske. |

| Ollama, Qwen und Wissensdatenbank | Teilweise geprüft | Client-, Kontext-, Speicher-, Such- und Fehlerpfade in Tests; Trainingsablehnung gelesen. | Keine neue fachliche Antwortbewertung. Stille Deindexierungsfehler zusätzlich instrumentieren. |

| Tracking, Zusammenführung und KI-Ablauf | Automatisch geprüft | 2.562 Pipeline-Tests bestanden. Zusammenführung, Rückfälle, Abbruch und Zustandsregeln erfasst. | Drei maschinen-/datengebundene Videoprüfungen übersprungen. |

| Meteranzeige, Rohranfang, Rohrende, Bogen | Teilweise geprüft | Regeln, Trainingshilfen und vorhandene Mess-/Abnahmewerkzeuge untersucht. | Historische Messungen nicht als neue Qualitätsmessung gewertet. Klassenweise Videoabnahme offen. |

| Trainingsstudio, Gold-Daten und Export | Automatisch geprüft | Freigabe-, Export-, Status-, Trennungs- und Rückgewinnungstests. 37 gezielte Infrastrukturtests im Wiederholungslauf grün. | Vollständiges reales Training bis zur Freigabe wurde nicht ausgelöst. |

| Python-Trainingshilfen und Review | Befund | 168 Agent-/Reviewtests, 312 weitere Tests, 13 nur mit Audit-Lader. 166 Unterfälle zusätzlich. | A06 und A07: Paketkonflikt und fehlende Agentenschritte. |

| Architektur, Schnittstellen, Registrierung | Befund | Alle 52 Projekte in der Lösung. Schichtentests und UI-Ai-Freeze-Prüfung laufen mit; Dateien und Deklarationen inventarisiert. | A04. Dateisystemlogik der Schachtumbenennung liegt weiterhin in Application. |

| Hilfsprogramme und Modernisierung | Teilweise geprüft | Alle Lösungsprojekte gebaut; 62 ProjectModernizer-Tests bestanden. Hilfsprogramme im Funktionsinventar. | Nicht jedes schreibende Kommando gegen echte Daten ausgeführt. |

| Pakete, Build und automatische Prüfung | Befund | Isolierter Release-Build: 0 Warnungen, 0 Fehler. NuGet-/Python-Prüfung ausgeführt. | A09/A10; Standardausgabe durch laufenden MCP-Dienst gesperrt. |

## Architektururteil

Die Schichtung Domain → Application → Infrastructure/UI ist vorhanden und durch viele Architekturprüfungen abgesichert. Öffentliche Fassaden, UseCases und kleine Controller sind eine gute Grundlage. Keine Lösungsprojekte fehlen. Die WPF-Seite und einzelne Orchestrierungen bleiben umfangreich; die Schacht-Dateioperationen in Application und das bestätigte Klassenwachstum sind konkrete Verbesserungsstellen.

## Weiteres Vorgehen

Der [Umsetzungsplan](UMSETZUNGSPLAN.md) beschreibt fünf Pakete mit Reihenfolge und Abnahmekriterien. Die [HTML-Übersicht](ueberblick.html) erklärt die Befunde ohne Programmierwissen. Die JSON-/CSV-Dateien und Logs im Ordner nachweise machen die Zählungen und Gegenproben nachvollziehbar.

## Start- und Hilfsskripte

31 PowerShell-/CMD-/BAT-Skripte erfasst, 25 PowerShell-Funktionen inventarisiert. Der Windows-PowerShell-5.1-Parser meldet keine Syntaxfehler. Start-, Publish- und Sidecar-Setup-Pfade gezielt gelesen. Der Publish-Weg fordert einen passenden Golden-Beleg und einen sauberen Quellstand. Keine Neuinstallation und kein Publish wurden ausgelöst. Die CMD-/BAT-Dateien wurden inventarisiert, nicht alle ausgeführt.
