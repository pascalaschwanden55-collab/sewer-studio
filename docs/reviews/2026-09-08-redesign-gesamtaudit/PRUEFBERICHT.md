# Gesamtprüfung und Redesign-Nachkontrolle · 8. September 2026

**Ergebnis: Das Redesign ist weiterhin nicht abnahmebereit.** Die sechs bekannten Schwachstellen sind erneut nachgewiesen. Zwei weitere Abweichungen betreffen die Feldzuordnung. Zusätzlich bleibt ein Oberflächentest auch bei Einzelwiederholung erfolglos.

Diese Prüfung wurde **am Morgen nachgeholt**. Der geplante Nachtlauf hat keine Programmprüfung durchgeführt.

## Warum nachts kein Bericht entstand

Der Zeitauftrag startete am 08.09. um 02:52:01 Uhr und brach um 02:52:05 Uhr ab. Codex CLI 0.149.0 wurde vom eingestellten Modell abgewiesen: Für `gpt-6-astra` war eine neuere Codex-Version erforderlich. Mein Einrichtungscheck hatte nur Programmstart und Anmeldung geprüft. Ein erfolgreicher Modellaufruf fehlte. Das war mein Fehler.

Die unveränderten Originalprotokolle stehen unter `.tmp/scheduled-audits/redesign-20260908-0252/`. Eine Kopie der Fehlernachweise liegt in `nachweise/nachtlauf/`. Der Bericht vom 7. September ist ausdrücklich kein Nachtbericht.

## Geprüfter Stand

- Nachprüfung ab etwa 06:31 Uhr am 08.09.2026, Europe/Zurich.
- Commit `cfccda46452607d2adfcffcf9f26a36caff07838`: Nova-Etappe 2b wurde am Vorabend in den Hauptbaum übernommen.
- Die bereits vorhandenen XTF-Arbeiten waren Bestandteil des geprüften Arbeitsstands. Es wurden keine Änderungen am Produktcode vorgenommen.
- Quellen: freigegebener Nova-Prototyp v2, dessen Prüftabelle, das Prototyp-Inventar, Abnahmen der Etappen 1/2/2b und der Etappe-2b-Plan. Genehmigte Abweichungen wurden berücksichtigt.
- Getestet mit künstlichen Projekten, eigenen Profilen und eigenem Wissensordner. Kein produktiver App-Startup, kein Trainingslauf, keine Änderung von Kundenoriginalen.
- Beginn und Ende zeigen denselben Commit und dieselbe Liste vorhandener Produktänderungen. Es wurde kein unveränderlicher Checkout angelegt; gleichzeitige Änderungen einzelner Dateiinhalte sind damit nicht vollständig ausgeschlossen.

## Befunde nach Bedeutung

### R1 · Hoch · Globale Suche übernimmt ein Objekt aus dem vorherigen Projekt

**Ablauf:** In Projekt A nach `10001-10002` suchen, Projekt B öffnen und den alten Treffer auswählen.

**Aktuell bestätigt:** `altTrefferNochVerwendet=true`, `auswahlIstImAktivenProjekt=false`. Die Auswahl gehört weiterhin zu A, obwohl B geöffnet ist.

[GlobaleSucheViewModel.cs:21](C:/Sewer-Studio_KI_4.5/src/AuswertungPro.Next.UI/ViewModels/GlobaleSucheViewModel.cs:21) erneuert die Treffer nur bei geändertem Suchtext. Projektwechsel und Zugehörigkeit des Trefferobjekts sind nicht abgesichert.

**Korrektur:** Bei Projektwechsel Treffer und Auswahl zurücksetzen beziehungsweise neu berechnen. Vor Navigation die Zugehörigkeit zum aktiven Projekt prüfen. Die Gegenprobe mit zwei Projekten als Verhaltenstest übernehmen.

### R2 · Hoch · Projektübersicht zeigt nach Projektwechsel den alten Stand

**Ablauf:** Über die Aktion für ein zuletzt geöffnetes Projekt von B nach C wechseln.

**Aktuell bestätigt:** Aktives Projekt C hat null Haltungen; dieselbe Übersichtsseite zeigt weiterhin Titel „Projekt B“ und eine Haltung.

[ProjektUebersichtPageViewModel.cs:56](C:/Sewer-Studio_KI_4.5/src/AuswertungPro.Next.UI/ViewModels/Pages/ProjektUebersichtPageViewModel.cs:56) beobachtet die ursprüngliche Haltungsliste, aber nicht den Wechsel des Projekts.

**Korrektur:** Projektwechsel abonnieren, alte Liste abmelden, neue Liste anmelden und alle Kennzahlen erneuern. Den echten Öffnen-Befehl im Test verwenden.

### R3 · Hoch · Schadensstufen 4 und 5 im Training Studio nicht vollständig erreichbar

Bei **1920 × 1080 und tatsächlichen 96 DPI / 100 Prozent** sind weiterhin nur die ersten drei Stufen sichtbar. Die Reihe setzt kleine Breiten, erbt aber eine Mindestbreite von 100 Pixeln pro Knopf. Stufe 4 beginnt bei x=1887, Stufe 5 bei x=1991. Das Fenster ist 1920 Pixel breit; die innere Inhaltsfläche endet früher.

[TrainingStudioWindow.xaml:509](C:/Sewer-Studio_KI_4.5/src/AuswertungPro.Next.UI/Views/Windows/TrainingStudioWindow.xaml:509)

**Korrektur:** Passende Mindestbreite oder fünf gleich breite Spalten verwenden. Im Layouttest für jede Stufe die tatsächlichen Grenzen innerhalb der sichtbaren Fläche prüfen.

**Belege:** [Bild](bilder/Light-TrainingStudio-1920x1080.png), [Messung](nachweise/messung-TrainingStudio-Light.json).

### R4 · Mittel · KI-Durchläufe werden projektübergreifend angezeigt

Nach Wechsel von A nach B zeigt die Übersicht von B weiterhin den Lauf „nur-in-Projekt-A“. Das Register verwendet Haltungsnamen ohne Projektkennung. Gleichnamige Haltungen können damit demselben Eintrag zugeordnet werden.

[CodingSuggestionRegistry.cs:14](C:/Sewer-Studio_KI_4.5/src/AuswertungPro.Next.Application/UseCases/CodingSuggestions/CodingSuggestionRegistry.cs:14)

**Korrektur:** Projekt und Haltung eindeutig gemeinsam zuordnen. Anzeige und Navigation auf das aktive Projekt begrenzen. Verspätete Ergebnisse eines alten Projekts berücksichtigen.

### R5 · Mittel · Viele Haltungstreffer verdrängen Schächte und Strassen

13 passende Haltungen und sechs Schächte zur Teststrasse liefern zwölf Haltungstreffer, keinen Schacht und keine Strasse. Die drei Trefferarten werden nacheinander angefügt und erst danach gemeinsam abgeschnitten.

[GlobaleSucheRegel.cs:48](C:/Sewer-Studio_KI_4.5/src/AuswertungPro.Next.Application/UseCases/Suche/GlobaleSucheRegel.cs:48)

**Präzisierung gegenüber gestern:** Dieselbe Reihenfolge und Begrenzung stehen bereits im Prototyp, Zeilen 1604–1614. Das ist eine übernommene Schwäche der Vorgabe, keine davon abweichende Umsetzung. Sie bleibt für einen grossen echten Bestand unzweckmässig.

**Korrekturhinweis:** Exakte Treffer priorisieren oder Plätze für mehrere Trefferarten vorsehen.

### R6 · Mittel · Rechteckiger Schacht wird oval dargestellt

Die erneute Gegenprobe ergibt bei „Rechteckig“: Oval sichtbar, Rechteck ausgeblendet.

[SchachtUebersichtPanel.xaml.cs:126](C:/Sewer-Studio_KI_4.5/src/AuswertungPro.Next.UI/Views/Pages/Schachtansicht/SchachtUebersichtPanel.xaml.cs:126)

**Präzisierung gegenüber gestern:** Auch das Prototyp-Inventar beschreibt in Abschnitt 5.6 ausdrücklich diese falsche Zuordnung. Die Umsetzung folgt hier der Vorlage. Die fachlich falsche Zeichnung sollte in Vorgabe und Programm korrigiert werden.

**Korrekturhinweis:** Rechteckig als Rechteck darstellen; unbekannte Form nicht als gesichert rund ausgeben.

### R7 · Mittel · Vorgesehene Schachtfelder landen in falschen Themen

Die Themen sind vorhanden, ihre Feldzuordnung entspricht aber nicht der vereinbarten Liste in Inventar 9.2. Eine Gegenprobe ruft die tatsächliche Zuordnungsregel der aktuellen Programmdatei auf:

| Feld | Vorgabe | Aktuelles Ergebnis |
|---|---|---|
| Baujahr | Stammdaten | Weitere Angaben |
| Belastungsklasse | Zustand und Inspektion | Weitere Angaben |
| Inspektionsdatum | Zustand und Inspektion | Weitere Angaben |
| Primäre Schäden | Zustand und Inspektion | Weitere Angaben |
| Fotos | Dokumente und Medien | Weitere Angaben |
| Bemerkungen | Sanierung und Kosten | Weitere Angaben |
| Ausgeführt durch | Sanierung und Kosten | Weitere Angaben |
| Eigentümer | Sanierung und Kosten | Stammdaten |

[SchaechteColumnPolicy.cs:185](C:/Sewer-Studio_KI_4.5/src/AuswertungPro.Next.UI/DataPage/SchaechteColumnPolicy.cs:185) gruppiert über unvollständige Wortbestandteile. Der Schacht-Detailbuilder verwendet diese Regel.

Im künstlichen Projekt zeigt der Schacht-Drawer 9/1/2/2 Felder in den vier Themen und 19 unter „Weitere Angaben“. Diese Bestandszahlen allein sind kein Vollständigkeitsbeweis; die acht konkret falsch zugeordneten Namen belegen den Fehler unabhängig vom Füllstand.

**Korrektur:** Die vereinbarte Feldliste mit kanonischen Namen und den tatsächlich vorkommenden Vorlagennamen abgleichen. Bekannte Felder verbindlich zuordnen; nur zusätzliche unbekannte Felder in „Weitere Angaben“ legen.

**Belege:** [Gegenprobe](nachweise/ressourcenprobe.json), [Schachtbild](bilder/tabellen/Dark-Schaechte-standard.png).

### R8 · Niedrig · Inspektionsrichtung fehlt in der Stammdaten-Spaltenansicht

Inventar 4.3 verlangt 14 Stammdatenspalten einschliesslich Inspektionsrichtung. Der aktuelle Katalog enthält 13 und lässt diese Angabe aus. Die direkte Gegenprobe bestätigt `inspektionsrichtungVorhanden=false`; der Chip zeigt „Stammdaten 13“.

[DataPageColumnViewCatalog.cs:55](C:/Sewer-Studio_KI_4.5/src/AuswertungPro.Next.UI/DataPage/DataPageColumnViewCatalog.cs:55)

Die Angabe existiert im Feldkatalog und in den Eingabefeldern. Es handelt sich um eine unvollständige Spaltenansicht, nicht um verlorene Daten.

**Korrektur:** Den bestehenden Feldschlüssel in die vorgesehene Ansicht aufnehmen und die vollständige Reihenfolge prüfen.

R1, R2, R4, R5 und R6: [gemeinsame Verhaltensprobe](nachweise/verhaltensprobe.json). R7/R8: [Ressourcen- und Feldprobe](nachweise/ressourcenprobe.json).

## Was die neue Tabellen-Etappe verbessert hat

Die neue Haltungsansicht startet mit zehn Kompaktspalten. Die Messung mit 40 künstlichen Haltungen und einem alten gespeicherten Layout bestätigt:

- zwölf vollständig sichtbare Zeilen bei Full HD und offener Feldschublade;
- 34 Pixel Zeilenhöhe, gewählte Zeile 36 Pixel;
- DN und Länge rechtsbündig in der Zahlenschrift, auch nach Übernahme des alten Layouts;
- Statusspalten, Zustandsmarken, Video-/PDF-Aktionen und Suchfeld in der Werkzeugleiste;
- Haltungsfelder in den genehmigten erweiterten Themen 17/9/11/3; zusätzliche Angaben getrennt;
- ohne Auswahl den passenden Leerzustand;
- zehn ganze Zeilen in „Alle Spalten“ bei den verwendeten mehrzeiligen Schadentexten.

Die dunkle Haltungsprobe zeigte 15 ganze Zeilen. Mindestens zwölf werden damit erreicht; unterschiedliche gespeicherte Fenster-/Schubladenstände erlauben keinen direkten Theme-Vergleich der Zeilenzahl.

Sechs neue Tabellenbilder und die zugehörigen Messdateien liegen unter `bilder/tabellen/` und `nachweise/tabellen/`.

## Build und Tests

| Prüfung | Ergebnis |
|---|---|
| Alltags-Build, Release, ohne Restore | **Bestanden**, 0 Warnungen, 0 Fehler |
| Vollständiger Release-Build | **Blockiert/fehlgeschlagen**: gesperrte Datei des laufenden MCP-Hilfsdiensts; MSB3027/MSB3021 |
| Derselbe MCP-Hilfsdienst mit Abhängigkeiten in getrennten Ausgabeordner gebaut | **Bestanden**, 0 Warnungen, 0 Fehler; ersetzt keinen erfolgreichen normalen Gesamt-Build |
| Infrastructure | 6’313 bestanden, 6 übersprungen, 0 Fehler |
| Pipeline | 2’562 bestanden, 3 übersprungen, 0 Fehler |
| Oberfläche | 6’748 bestanden, 11 übersprungen, **1 Fehler** |
| ProjectModernizer | 62 bestanden, 0 übersprungen, 0 Fehler |
| Sidecar, ohne GPU | 572 bestanden, 2 GPU-Fälle ausgeschlossen; zwei Warnungen |
| QGIS | 10 bestanden, 0 Fehler |
| Einzelwiederholung des fehlgeschlagenen Oberflächentests | Erneut fehlgeschlagen, nach 60 Sekunden Zeitlimit |

Die vier .NET-Hauptläufe ergeben **15’685 bestandene Tests, einen fehlgeschlagenen Test und 20 übersprungene Einträge**. Python ergänzt 582 bestandene Tests. Die gezielte Wiederholung wird nicht als zusätzlicher unterschiedlicher Test gezählt.

Die neun übersprungenen Infrastructure-/Pipeline-Fälle betreffen optionale Live-/Referenzprüfungen. Die elf UI-Einträge sind isolierte Kindprozess-Szenarien. Ihre übergeordneten Prüfungen liefen; eine davon scheiterte. Deshalb darf die gesamte UI-Gruppe nicht als grün bezeichnet werden.

**Offener Testfehler:** `NachschlagKontextmenueTests.Das_Nachschlagmenue_haengt_an_den_richtigen_Feldern`. Der Kindprozess beendet seine WPF-Prüfung nicht innerhalb von 60 Sekunden; die Szenario-Bestätigung fehlt. Der Fehler tritt auch ohne parallel laufende eigene Fensterprobe auf. Ob die Ursache im Testablauf oder im Programm liegt, ist damit nicht abschliessend geklärt. Er wird als offene Abnahmesperre geführt, nicht als zusätzlich bewiesener Benutzerfehler.

Die Sidecar-Warnungen betreffen eine Bibliotheks-Abkündigung und den nicht beschreibbaren pytest-Zwischenspeicher. Der Testlauf selbst bestand.

## Umfang der Oberflächenprüfung

Alle 15 Navigationseinträge wurden in beiden Erscheinungsbildern angesteuert. 28 von 30 Seitenaufbauten gelangen im isolierten Hauptprüfwerkzeug. „Medienkonflikte“ scheitert dort in beiden Themes an einer Ressource des Prüfwerkzeugs. Die getrennte Gegenprobe mit den echten kompilierten App-Ressourcen besteht. Daraus wird kein Produktfehler abgeleitet; der komplette Bedienablauf dieser Seite ist damit trotzdem nicht nachgewiesen.

Die Bildserie umfasst beide Themes sowie zusätzliche kleinere Fensterproben. Detailliert visuell beurteilt wurden insbesondere Haltungstabelle, Schachtseite und Training Studio. Ein erfolgreicher Seitenaufbau ist keine Bestätigung sämtlicher Schaltflächen und Abläufe dieser Seite.

Der erste Messversuch der Seitenserie konnte einen automatischen Knopfbreitenwert nicht als JSON schreiben. Das betraf nur den Messcode. Die Stufenmessung wurde auf das Training Studio begrenzt und die Seitenserie erneut abgeschlossen. Alte Fehlermeldungen bleiben im Prüfprotokoll erhalten.

## Vorgabenabgleich und verbleibende Prüflücken

[SOLL-IST.md](SOLL-IST.md) ordnet alle nummerierten Punkte der v2-Prüftabelle sowie die Gliederungspunkte des Inventars dem aktuellen Nachweisstand zu. „Teilweise“ und „offen“ sind ausdrücklich keine Abnahme.

Berücksichtigte genehmigte Abweichungen: bestehende Zustandsfarben, dunkler Flächenakzent, Projektwechsel statt Avatar, Zustandslegende statt Donut, vorhandener Trainingsfreigabeweg, de-CH-Zahlen, keine nachgebauten Demo-Daten und die erweiterten Haltungsfeldlisten. Prototyp-Simulationen dürfen die echten Fachregeln nicht ersetzen.

Noch nicht lückenlos nachgewiesen sind insbesondere:

- tatsächliche Windows-Skalierung 125/150 Prozent am aktuellen Stand; gemessen wurde 100 Prozent;
- vollständige manuelle Tastatur-/Fokus- und Screenreader-Prüfung aller Fenster;
- aktuelle Kontrastmessung jedes Elements in allen Zuständen;
- sämtliche zusammenhängenden Import-, Export-, Codier- und Trainingsfreigabeabläufe über die echte Oberfläche;
- Hardware-/GPU-Erkennung und fachliche Qualität an unabhängigen realen Prüfbeständen;
- produktiver Start mit laufenden optionalen Diensten, lange Sitzungen und grosse reale Datenbestände.

Die bestehenden Tests prüfen viele Teilabläufe. Sie ersetzen diese offenen Gesamtprüfungen nicht. Kundenoriginale wurden dafür nicht als bearbeitbare Testdaten verwendet.

## Nächste fachlich sinnvolle Arbeit

Zuerst Projektzuordnung R1/R2/R4 und die vollständige Stufenauswahl R3 korrigieren. Danach Feldzuordnung R7/R8 und die beiden übernommenen Konzeptschwächen R5/R6 klären. Den wiederholbar scheiternden WPF-Test auflösen. Anschliessend die kritischen Bedienabläufe und echte Skalierungen erneut prüfen.

Dieser Bericht liefert keine Freigabe und verspricht keine Fehlerfreiheit. Er dokumentiert den gemessenen Stand, die nachgewiesenen Probleme und die noch fehlenden Belege.
