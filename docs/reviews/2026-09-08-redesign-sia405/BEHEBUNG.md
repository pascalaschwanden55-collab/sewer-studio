# Redesign: Korrekturen und Abschlussprüfung

Stand: 08.09.2026. Lokaler Arbeitsstand auf `feature/eval-pruefsatz-review`, einschliesslich der vorher vorhandenen Änderungen. Kein Commit, Merge oder Push durch diesen Korrekturlauf. Kundenoriginale und das echte Benutzerprofil wurden nicht verändert.

**Die bestätigten Programmfehler aus den beiden Feld-Audits sind korrigiert.** Die Prüfung umfasst Formularaufbau, Dropdown-Verbindungen, Gruppierung, Massumrechnung, XTF-Erstexport und Änderungsabgleich. Die alten Auditberichte bleiben als Vorher-Nachweis erhalten.

## Korrekturen

| Befund | Ergebnis |
|---|---|
| F01 / B2: Schacht-Dropdowns | Funktion, Material, Status und Sanierungsbedarf sind angeschlossen. Fehlende Normspalten werden bei älteren Vorlagen ergänzt. Funktion wird in Tabelle und Formular passend zur Bauwerksart gefiltert. |
| B3: Nutzungsart in der Tabelle | Feste Auswahl wie im Formular. „Regenwasser“ wird weiterhin korrekt zu Niederschlagsabwasser übersetzt. |
| F02: Neue Haltung | Schacht oben/unten erscheinen auch ohne vorherigen Import. Die feste CSV-/Excel-Spaltenfolge bleibt erhalten. |
| F03: Schachtmasse | Werte über 4000 mm und unbrauchbare getrennte Angaben werden nicht mehr als gültige Masse geschrieben. Beide Masse werden dann mit Hinweis ausgelassen. Das Formular markiert den Fehler; Originalwerte bleiben im Projekt. Kein Rückfall auf einen alten Ersatzwert. |
| F06: Einheiten | Getrennte mm-Felder verwenden keine Meter-Heuristik. 10 im mm-Feld bleibt 10 mm. Ausdrückliche Einheiten gelten auch ohne Leerzeichen und am Ende eines Wertepaars. |
| F04: Modellfassung | Der neue Export nennt jetzt die tatsächlich verwendete Fassung 29.11.2025 der Modellfamilie 2020. Damit passt auch Spezialbauwerk/Kombischacht. |
| B1: Fettabscheider | Der genaue Normwert bleibt erhalten. Die frühere bewusste Vereinfachung auf andere ist aufgehoben. |
| F07 / B4 / B5: Auffindbarkeit | Eigentümer zusätzlich in der kompakten Tabelle. Die acht Haltungs-Katasterfelder sind Fachgruppen zugeordnet. Schachtfelder wie Baujahr, Inspektionsdatum, Schäden und Fotos stehen in passenden Gruppen. Eigentümer bleibt bei Stammdaten, damit „Sanieren = Nein“ ihn nicht ausblendet. |
| F08: Inspektionsrichtung | Auch in der Haltungs-Spaltenansicht Stammdaten enthalten. |
| F05: Training Studio | Alle fünf Stufen passen in die rechte Spalte. Mit Full-HD-Bild nachgeprüft. |
| B6: Erstexport | Bewusst wählbarer vollständiger Erstexport als reine SIA405-Datei, auch ohne Importvorlage. Der Änderungsabgleich bleibt Standard. Vorschau und tatsächlicher Export erhalten dieselbe Auswahl. |
| B7: Zusatzmodell | Standardexport ohne Zusatzmodell ist erreichbar. Beim Änderungsabgleich bleiben Zusatzmodell und Feldaufträge erhalten. Die Oberfläche erklärt die Lieferarten und die benötigte .ili-Datei. |
| B8: Organisationstyp | Dokumentation unterscheidet die sechs Werte des Basismodells 2020 von den sieben Werten in 2020_1. Gemeindeabteilung gehört nur zur neueren Basisfamilie. |
| Bildkontrolle: dunkles Thema | Hervorhebungen verwenden passende Themenfarben. Ein gespeicherter Altwert ausserhalb der Auswahl bleibt ausdrücklich sichtbar; die Liste wird nicht um unzulässige neue Werte erweitert. |

Persönliche Reihenfolge und Ausblendung bleiben erhalten. Ausblenden ist eine Anzeigeentscheidung und löscht keine Daten. Der Benutzer darf weiterhin selbst entscheiden, welche Felder sichtbar sind.

## Prüfungen

| Testpaket | Erfolgreich | Übersprungen | Fehler |
|---|---:|---:|---:|
| Infrastructure | 6326 | 6 | 0 |
| UI | 6901 | 18 | 0 |
| Pipeline | 2648 | 3 | 0 |
| Modernizer | 62 | 0 | 0 |

**Gesamt: 15’937 erfolgreiche Tests, 27 übersprungen, 0 Fehler.**

Der vollständige Release-Build besteht mit **0 Warnungen und 0 Fehlern**. Ein laufender MCP-Hilfsdienst sperrt seine Ausgabedatei. Nur dessen Build-Ausgabe wurde deshalb in einen eigenen Prüfungsordner gelenkt; der Dienst wurde nicht beendet. Der zunächst eingeschränkte Lauf konnte eine vorhandene Zwischenstandsdatei nicht schreiben. Der abschliessende vollständige Lauf mit den nötigen Dateirechten war erfolgreich.

Die neuen Exporttests reproduzierten vor der Korrektur die sieben Ausgangsfehler. Hinzugekommen sind Prüfungen für den tatsächlichen Formularbuilder, den WPF-Tabelleneditor, den Bauwerkswechsel ohne Datenverlust, die Vorprüfung des reinen Erstexports und die Sichtbarkeit alter Werte. Alte Tests wurden nur dort angepasst, wo sie die ausdrücklich korrigierte Anordnung oder Fettabscheider-Vereinfachung erwarteten.

Die isolierten Bilder zeigen Training Studio, die Haltungs-Aufklappliste und die Schacht-Aufklappliste. Der Prüfhost verwendet ein eigenes Profil und künstliche Daten. Der produktive Programmstart mit KI, Spiegelung oder QGIS wurde nicht ausgeführt.

## INTERLIS-Prüfung

Geprüft mit **ilivalidator 1.15.0**, ohne abgeschaltete Fachprüfungen und mit festgehaltenen lokalen Kopien der offiziellen Modelle. Die synthetischen Daten enthalten Haltung, Kanal, Haltungspunkte, Rohrprofil, Normschacht, Spezialbauwerk/Kombischacht, Versickerungsanlage, Einleitstelle und Organisationsverweise.

| Lieferung | Ergebnis |
|---|---|
| Vollständiger reiner SIA405-Erstexport | 0 Fehler, `validation done` |
| Vollständiger Export mit Zusatzmodell | 0 Fehler, `validation done` |
| Änderungsabgleich mit Zusatzmodell | 0 Fehler, `validation done` |
| Zusatzdatei ohne auflösbares Zusatzmodell | Erwarteter Abbruch: `SewerStudio_Zusatz_2026: model(s) not found` |

Die Negativprobe verwendet einen Modellpfad ohne Zusatzmodell. Die zugehörige XTF ist im Nachweisordner enthalten. Sie belegt die Abhängigkeit vom auflösbaren Modell; das blosse Fehlen einer .ili-Datei neben der XTF ist kein allgemeiner Fehler, wenn der Empfänger das Modell anderweitig kennt.

Quellen: [SIA405 Abwasser 2020, Fassung 29.11.2025](https://www.vsa.ch/models/2020/SIA405_Abwasser_2020_2_d_LV95-20251129.ili), [Basismodell 2020](https://www.vsa.ch/models/2020/SIA405_Base_Abwasser-20201103.ili), [Basismodell 2020_1](https://www.vsa.ch/models/2020_1/SIA405_Base_Abwasser_1_2_d_LV95-20231018.ili), [ilivalidator-Anleitung](https://github.com/claeis/ilivalidator/blob/master/docs/ilivalidator.rst).

## Bewusste Grenzen

- B9 war kein bestätigter Fehler: „nicht berechnet“ bleibt von Z0–Z4 getrennt. Ein ausdrückliches „unbekannt“ als zusätzliche Zustandsklasse wurde nicht neu eingeführt.
- Bestehende Regeln zur Leerung und zum Schutz besserer Katasterwerte bleiben erhalten. Ein leeres Formularfeld ist kein automatischer Löschauftrag an GEONIS.
- Das Programm ist kein vollständiger Katastereditor für jedes optionale SIA405-Attribut. Die korrigierten Felder und die erzeugten Beispieldateien sind geprüft; daraus folgt keine Abnahme aller denkbaren Kundenbestände.
- Der tatsächliche Import bei Trigonet/GEONIS wurde nicht ausgeführt. Die technische INTERLIS-Prüfung belegt Dateikonformität der Prüffälle, nicht die Wirkung eines fremden FME-Abgleichs.

Nachweise: [Buildprotokoll](nachweise-behebung/abschluss-build.txt), [Testübersicht](nachweise-behebung/tests.json), [INTERLIS-Protokolle](nachweise-behebung/xtf/standard.log), [korrigierte Schachtansicht](nachweise-behebung/bilder/Schaechte-Dark-auf.png), [Training Studio](nachweise-behebung/bilder/TrainingStudio-Light.png). Quellstand-Prüfsummen und die übrigen Protokolle liegen im selben Nachweisordner. `CLAUDE.md`, Wertelistendokumentation und Architektur-Skill wurden abgeglichen; die Skill-Prüfung besteht.
