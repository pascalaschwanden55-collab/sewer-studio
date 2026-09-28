# SewerStudio – Umsetzungsplan für bessere Wartbarkeit

Stand: 27.09.2026. Grundlage: [Codeanalyse](BERICHT.md) und [Prüfnachweise](nachweise/README.md).

**Dieser Plan beschreibt künftige Änderungen. Die Analyse hat den Produktcode nicht verändert.** Die Umsetzung sollte paketweise erfolgen. Ein Paket ist erst abgeschlossen, wenn sein Verhalten geprüft und sein Ergebnis dokumentiert ist.

## 1. Ziel und Ausgangspunkt

Das Ziel ist, Änderungen an SewerStudio mit weniger Suchaufwand und geringerem Fehlerrisiko durchführen zu können. Dazu sollen die wichtigen Abläufe einen klaren Einstieg, überschaubare Phasen und verlässliche Prüfungen bekommen.

Der aktuelle Ausgangspunkt ist genauer als der ältere Gesamtaudit-Bericht:

- Vollständiger Release-Build: 0 Fehler, 0 Warnungen.
- Infrastruktur: 7.498 bestanden, 6 übersprungen.
- KI-Pipeline: 2.853 bestanden, 3 übersprungen.
- UI: 7.458 bestanden, **1 fehlgeschlagen**, 33 übersprungen.
- ProjectModernizer: 62 bestanden.
- Ausgewählte Trainingsskripte: 66 Tests und 9 Untertests bestanden.

Der verbliebene UI-Fehler ist `MaintainabilityFitnessTests.No_new_production_file_exceeds_1000_lines`. Er meldet `AnnotationWorkbenchService.cs` mit 1.034 und `MultiModelAnalysisService.cs` mit 1.016 Zeilen. Die zwölf weiteren Fehler des früheren Audits sind im aktuellen Lauf nicht mehr vorhanden.

## 2. Verbindliche Grenzen

- Öffentliche Einstiegspunkte und gespeicherte Datenformate bleiben kompatibel.
- Kundenoriginale bleiben unverändert. Tests verwenden synthetische Daten und eigene temporäre Ordner.
- Neue Anwendungsabläufe gehören nach `Application/UseCases`. Darstellung, Farben und Dispatcher bleiben im UI.
- Vor einem riskanten Umbau schützt ein Test das heutige Verhalten. Abbruch, Fehler und Teilerfolg sind dabei ebenso wichtig wie der Erfolgsfall.
- Die Grenzwerte werden nicht erhöht, um einen Umbau als erfolgreich erscheinen zu lassen. Zusätzliche Teil-Dateien oder verkürzte Formatierung gelten nicht als fachliche Verbesserung.
- Keine neuen NuGet-Pakete, kein Austausch der Architektur und keine großflächigen Änderungen als Nebenarbeit.
- Vor jedem Paket Status und Änderungen der betroffenen Dateien prüfen. Vorhandene Nutzeränderungen bleiben erhalten und werden in den Ausgangsstand einbezogen.
- Das laufende SewerStudio wird nicht beendet. Die Prüfregeln aus `AGENTS.md` gelten weiterhin.

## 3. Reihenfolge und Aufwand

Ein Arbeitstag bedeutet hier etwa **4–6 konzentrierte Stunden**, einschließlich Tests, Prüfung und Dokumentation. Die Spannen sind Planungsschätzungen für eine Person mit KI-Unterstützung. Fachliche Rückfragen, unterbrochene Arbeit und unerwartete Fehler können den Kalender verlängern.

| Paket | Inhalt | Befunde | Vorbedingung | Geschätzter Aufwand |
|---|---|---|---|---:|
| AP01 | Verbliebenes Wartbarkeits-Gate fachlich sauber schließen | W02, W03, W13 | Aktueller Prüfstand | 1–3 Tage |
| AP02 | Trainingsskripte in den automatischen Prüfweg aufnehmen | W08 | Testabhängigkeiten geprüft | 0,5–1,5 Tage |
| AP03 | Zwei gemeinsame Regeln zentral halten | W10 | AP01 für einen grünen Gesamtstand | 0,5–1,5 Tage |
| AP04 | Verhaltenstests und Produktabdeckung verbessern | W01, W11 | AP01 | 2–4 Tage |
| AP05 | Mehrmodell-Analyse in überschaubare Phasen gliedern | W02 | AP01, passende Tests aus AP04 | 3–5 Tage |
| AP06 | Goldsample-Speichern als eigenen Ablauf abgrenzen | W03 | AP01, passende Tests aus AP04 | 2–4 Tage |
| AP07 | Import und Medienverteilung gezielt vereinfachen | W04 | AP01, passende Tests aus AP04 | 3–5 Tage |
| AP08 | XTF-Parser nach Lesen, Beziehungen und Übernahme ordnen | W05 | AP07 oder unabhängig geprüfter Importstand | 2–4 Tage |
| AP09 | Einen Player-Ablauf und die zugehörige Diensterzeugung ordnen | W06, W07, W12 | AP04; abgestimmt mit AP05/AP06 | 3–5 Tage |
| AP10 | Python-Negativsatzprüfung nach Verantwortungen trennen | W09 | AP02 | 2–4 Tage |
| AP11 | Aktuelle Architekturübersicht und dauerhafte Messung | W13, W14 | Erste umgesetzte Pakete; begleitend | 1–2 Tage |

**Gesamtbereich: 20–40 konzentrierte Arbeitstage.** Das ist der gesamte hier vorgeschlagene Schwerpunkt, kein notwendiger Sofortumbau. Die ersten drei Pakete benötigen geschätzt 2–6 Tage und liefern bereits einen grünen Strukturprüfstand, einen zusätzlichen automatischen Schutz und zwei bereinigte Regeln. Danach sollte der tatsächliche Nutzen erneut bewertet werden.

AP05 bis AP10 sind bewusst getrennte Vorhaben. Es besteht kein Grund, alle gleichzeitig anzufangen. Besonders bei Import, Player und Training sollte jeweils nur ein fachlicher Ablauf aktiv umgebaut werden.

## 4. Pakete mit konkreter Abnahme

### AP01 – Den einen verbleibenden Strukturfehler beheben

**Zieldateien:**

- `src/AuswertungPro.Next.UI/Services/AnnotationWorkbenchService.cs`
- `src/AuswertungPro.Next.Infrastructure/Ai/Pipeline/MultiModelAnalysisService.cs`
- Zugehörige Workbench-, Masken-, Resilienz- und Checkpoint-Tests.

**Vorgehen:**

1. Den bekannten Fehler reproduzieren und die vorhandenen Verhaltenstests der beiden Bereiche ausführen.
2. Je Klasse genau eine zusammenhängende Verantwortung auswählen. Beim Workbench-Dienst ist die Maskenentscheidung um `EvaluateGoldMask` ein überschaubarer Kandidat. Bildlesen und fachliche Gültigkeitsentscheidung dürfen dabei nicht verwechselt werden. Bei der Analyse ist eine klar begrenzte Phase aus Vorbereitung, Wiederaufnahme oder Fehlerabschluss auszuwählen, deren Ein-/Ausgaben vollständig festgehalten werden können.
3. Den gewählten Schritt anhand seiner Verantwortung auslagern. Die bereits vorhandenen Hilfsmethoden nicht erneut nachbauen. Öffentliche Fassade und Reihenfolge erhalten.
4. Diff prüfen: weniger Verantwortung in der Ausgangsklasse, keine bloße Dateiverschiebung, keine neue Abhängigkeit auf WPF in Application.

**Abnahme:** Beide Dateien liegen wieder innerhalb der bestehenden Grenze; der Teilklassen-Wächter bleibt grün. Vorhandene Ergebnis-, Fehler- und Abbruchtests bestehen. Der gesamte Release-Weg ist grün. Eine Grenze allein genügt nicht: Der ausgelagerte Schritt muss eigenständig verständlich und sinnvoll prüfbar sein.

**Rücknahme:** Der neue interne Baustein und seine Delegation werden zusammen zurückgenommen. Es gibt keine Datenmigration und keinen neuen gespeicherten Zustand.

### AP02 – Tests der Trainingsskripte automatisch ausführen

**Zieldateien:** `.github/workflows/ci.yml`, gegebenenfalls die dokumentierten Entwicklungsbefehle und klar begrenzte Testkonfiguration. Produktive Trainingsskripte werden für dieses Paket nicht strukturell verändert.

**Vorgehen:**

1. Die 38 Testdateien unter `training/scripts/tests` inventarisieren: reine CPU-Prüfung, optionale Abhängigkeit, echter Prozess, echte Hardware.
2. Einen klaren CPU-Aufruf festlegen und in einer frischen Umgebung ausführen. Die drei bereits geprüften Testdateien bilden die erste bekannte Grundlage, nicht automatisch den ganzen Umfang.
3. Im Python-Job einen eigenen Schritt ergänzen. Für optionale Gruppen sichtbare Auswahlregeln verwenden; fehlerhafte Imports nicht still überspringen.
4. Prüfbefehl und benötigte Testabhängigkeiten dokumentieren. Das tatsächliche Modelltraining bleibt ein eigener Ablauf.

**Abnahme:** Ein absichtlich beschädigter Klassenkarten- oder Negativsatzbeleg lässt den neuen automatischen Schritt fehlschlagen. Alle vorgesehenen CPU-Tests laufen ohne Kundendaten, aktive Modelle oder GPU. Der Schritt erscheint auch bei einer reinen Änderung an `training/scripts`.

**Rücknahme:** Nur den neuen CI-/Testschritt zurücknehmen; bestehende Sidecar- und QGIS-Prüfungen erhalten. Ein technischer Fehler bleibt sichtbar und wird nicht durch bedingungsloses Fortsetzen verdeckt.

### AP03 – Gemeinsame Regeln vereinheitlichen

**Teil A: Preisermittlung.** `CatalogPriceResolver` bleibt die fachliche Quelle. Die bestehenden öffentlichen Helfer von `CostCalculatorLogicService` delegieren für Mengenprüfung, nächste DN-Gruppe und Hinweistext dorthin. Die Kulturbehandlung der unterschiedlichen `ParseDn`-Varianten bleibt zunächst separat.

**Teil B: Klassenkartenbindung.** Die identische Prüfung aus `TrainingExportPlanService` und `TrainingExportPlanInputBuilder` erhält eine gemeinsame reine Regel. Beide bisherigen Prüfstellen rufen sie weiterhin auf. Die Eingangsprüfung und die Planprüfung werden nicht zu einer einzigen entfernten Prüfung reduziert.

**Tests:** Leere Preisliste, exakte DN, gleiche Entfernung nach oben/unten, Mengengrenzen, stabiler Hinweistext. Für die Klassenkarte falsche Version, falscher Hash, falscher VSA-Hash, fehlende oder vertauschte Klasse und korrekte Karte.

**Abnahme:** Eine Regeldefinition je Fall, bisherige öffentliche Aufrufe bleiben verfügbar, beide Schutzgrenzen bleiben aktiv. Kein geändertes Preis- oder Exportergebnis.

**Rücknahme:** Delegation und gemeinsame Regel gemeinsam zurücknehmen. Die bestehenden Tests bleiben erhalten.

### AP04 – Umbauten durch Verhalten absichern

**Zieldateien:** Die konkret betroffenen Player-Strukturtests, bestehende Tests zur Aufnahmebindung sowie `.github/scripts/check-coverage.ps1` und ergänzende Konfiguration für eine separate Produktcode-Auswertung.

**Vorgehen für Tests:**

1. Für jeden ausgewählten Quelltexttest einen Satz formulieren: „Welche falsche Wirkung soll dieser Test verhindern?“
2. Den Datenfluss mit vorhandenen Fakes oder kleinen kontrollierten Aktionen prüfen. Beispiel: Frame A wird analysiert; danach ändert sich der globale Meterwert; das Ereignis muss trotzdem Frame A und dessen Meter erhalten.
3. Durch eine vorübergehende Gegenprobe zeigen, dass ein falscher Meter, eine falsche Bildquelle oder ein übersprungener Schutz den Test brechen würde. Diese Gegenprobe wird nicht als Produktänderung übernommen.
4. Den empfindlichen Quelltexttest erst dann ersetzen. Verbote wie neue `UI/Ai`-Dateien oder unerlaubtes `App.Services` bleiben Strukturtests.

**Vorgehen für Abdeckung:**

1. Produktdateien von Tests und generiertem Code unterscheiden.
2. Ergebnisse nach normalisiertem Dateipfad und Zeile vereinigen. Eine in mehreren Testprojekten ausgeführte Produktzeile wird einmal gezählt.
3. Teilwerte für die aktuell umgebauten Bereiche anzeigen.
4. Den bisherigen Verlaufsschutz erhalten und erst nach einem echten CI-Lauf ergänzende Grenzen festlegen.

**Abnahme:** Gleichwertige interne Umbenennungen ändern das Testergebnis nicht. Falsche Wirkungen werden erkannt. Die Produktabdeckung ist mit einer kleinen handprüfbaren Beispieldatei nachvollziehbar und wird nicht mit der bisherigen Mischquote verwechselt.

### AP05 – Mehrmodell-Analyse schrittweise ordnen

**Ziel:** `AnalyzeAsync` beschreibt den Ablauf in klaren Phasen, während Modellschritte und Fehlerentscheidungen ihre eigenen, begrenzten Verantwortungen haben.

**Reihenfolge:**

1. Laufzustand explizit halten: Fortschritt, Fehlerzahlen, Wiederaufnahme und Abschluss. Lebensdauer je Analyselauf; kein neuer globaler Zustand.
2. Verarbeitung eines Bildes als klaren Schritt mit Ergebnis modellieren: Befunde, regulär übersprungen, erneut nötig oder Abbruch.
3. Zuerst einen Modellschritt auslagern; YOLO-, DINO- und SAM-Regeln nicht vorschnell gleichsetzen.
4. Trace, Zusammenführung und Checkpoint in ihrer belegten Reihenfolge halten.
5. Am Schluss den äußeren Ablauf vereinfachen und unnötige Durchreichungen entfernen.

**Tests:** Sauberer Treffer; sauberer Negativfall; ungültiges Bild; Transportfehler; VRAM-Mangel; Modellantwort mit Einschränkung; Nutzerabbruch; Restart; Abbruch/Fortsetzung; Checkpoint-Schreibfehler. Referenzfälle vor und nach dem Umbau vergleichen.

**Abnahme:** Gleiches Ergebnis und gleiche Warnungen für dieselben kontrollierten Eingaben. Technischer Fehler bleibt von „kein Schaden“ getrennt. Die lange Methode verliert tatsächlich Entscheidungsverantwortung. Ein Zielbereich von etwa 100–150 Zeilen für die äußere Steuerung ist eine Orientierung, keine Aufforderung zum Verstecken der Logik.

**Rücknahme:** Einen Modellschritt pro überprüfbarer Änderung umstellen. Öffentliche Fassade, Checkpoint-Vertrag und gespeicherte Felder bleiben stabil.

### AP06 – Speichern geprüfter Trainingsdaten abgrenzen

**Ziel:** Ein Anwendungsfall steuert das Speichern. Das UI zeigt seinen Status an. Vorschlagserzeugung und Segmentierung sind getrennte Vorgänge.

**Phasen:** Eingaben prüfen → Herkunft/Schutz prüfen → Bild vorbereiten und binden → Sample dauerhaft speichern → KB-Nachtrag → Teacher-Nachlauf → gemeinsames Ergebnis.

Der Übergang „Sample dauerhaft gespeichert“ muss ausdrücklich erkennbar sein. Danach melden nachgelagerte Fehler einen gespeicherten Zustand mit Warnung. Bestehende Sonderfälle für Entwurf, Duplikat und Reparatur bleiben erhalten.

**Tests:** Fehlende persönliche Bestätigung; ungültiger Code; unpassende Maske; manipuliertes Bild; geschützte Prüfdaten; fremde Bestandsversion; doppeltes Sample; Abbruch vor Speicherung; KB-Fehler und Teacher-Abbruch nach Speicherung. Vorhandene Testhilfen wiederverwenden.

**Abnahme:** Keine zweite Speicherung nach bloßem KB-Fehler, keine falsche Goldfreigabe, keine verlorene Warnung. Die öffentliche `IAnnotationWorkbenchService`-Fassade bleibt kompatibel. Die neue Application-Logik importiert weder WPF noch konkrete Infrastructure-Typen.

**Rücknahme:** Der bestehende UI-Dienst bleibt bis zur Abnahme als Fassade erhalten. Keine Datenmigration.

### AP07 – Import und Medienverteilung in Phasen gliedern

**Ziel:** Der Hauptimport entscheidet über die Reihenfolge; die einzelne Phase hat begrenzte Eingaben und ein verständliches Ergebnis.

**Vorgehen:**

1. Aktuelle Fehlergrenzen und Abbruchstellen als Tabelle festhalten. Besonders die Medienphase besitzt heute eine gemeinsame Fehlergrenze.
2. Eine Phase mit ihrem Ergebnis auslagern, vorzugsweise die bereits gut getestete Medienkoordination. Die vorhandenen Verteildienste weiterverwenden.
3. Für den PDF-/Video-Verteiler Suchentscheidung, Ablageplan und Ausführung getrennt betrachten. Zuerst einen davon isolieren.
4. Die 17 Einzelparameter in fachlich zusammengehörige Eingaben ordnen. Ergebnis und Warnungen bleiben explizit.

**Tests:** Fehler je Datei, bereits vorhandenes Ziel, falscher Videotreffer, Gegenbefahrung, Sammelprotokoll, defektes PDF, Abbruch und Wiederholung. Die Testfälle zu Originalschutz, Verknüpfungen und Rücknahme sind Pflicht.

**Abnahme:** Gleiche zugeordnete Dateien, gleiche Schutzentscheidungen, gleiche Fehlerbilanz. Vorhandene Kundenoriginale werden weder verändert noch als Testeingabe verwendet. Ein neuer Wiederholungsversuch erzeugt keine zusätzliche unerwünschte Ausgabe.

**Rücknahme:** Je Phase eine kleine Änderung mit unverändertem öffentlichen Importvertrag. Kein gemeinsamer Umbau sämtlicher Importformate.

### AP08 – XTF-Parser verständlicher machen

**Vorgehen:** SIA405 zunächst von VSA-KEK getrennt behandeln. Innerhalb eines Formats Objektlesen, Beziehungsauflösung und Projektabbildung trennen. Erst vorhandene interne Datensätze nutzen, bevor neue angelegt werden.

**Tests:** Kleine synthetische Dateien mit Kanal, Haltung, Rohrprofil, Organisationen, mehreren Untersuchungen, fehlenden Beziehungen und Gegenbefahrung. Zusätzlich bestehende Feldschutz-/Herkunftsregeln und Videozählerstand prüfen.

**Abnahme:** Die vorbereiteten Referenzdateien erzeugen identische Projektwerte und Herkunftsangaben. Fehlende oder widersprüchliche Bezüge bleiben erkennbar. Die Grenze zwischen Formatlesen und fachlicher Übernahme ist im Code sichtbar.

**Rücknahme:** Nur einen Objektleser bzw. eine Abbildung pro Schritt umstellen. Originalkennungen und gespeichertes Projektformat bleiben erhalten.

### AP09 – Einen Player-Ablauf und seine Einrichtung vereinfachen

**Pilot:** Gebundenes Einzelbild analysieren und Ergebnis/Ereignisse anzeigen. Nicht gleichzeitig Live-Erkennung, manuellen Codiermodus, Training Center und Wiedergabe neu ordnen.

**Vorgehen:**

1. Den heutigen Aufrufweg mit seinen Daten und Zustandsbesitzern auf einer Seite dokumentieren.
2. Einen klaren Application-Einstieg samt Ergebnis definieren; WPF-Farben und konkrete UI-Aktionen aus diesem Vertrag heraushalten.
3. Adapter im UI übersetzen das fachliche Ergebnis in Anzeigeaktionen. Eine Weiterleitung ohne eigene Aufgabe kann innerhalb derselben Verantwortung zusammengefasst werden.
4. Den zugehörigen Dienstverbund getrennt zusammensetzen, nach dem Muster der vorhandenen Backup-/Export-Komposition.
5. Wissenspfade und Einstellungen pro Verbund binden. Globale Altzugriffe zunächst kompatibel halten.

**Abnahme:** Derselbe Ablauf lässt sich im Test ohne sichtbares Fenster ausführen. Ein Leser kann ihn vom Einstieg zum Ergebnis verfolgen, ohne viele fast leere Zwischenbausteine öffnen zu müssen. Zwei unabhängige Testprofile teilen keine veränderliche Pfadkonfiguration. Aufnahmebindung, menschliche Änderungen und Abbau beim Schließen bleiben geschützt.

**Rücknahme:** Den alten Einstieg delegieren lassen und den Pilot hinter dieser Fassade umstellen. Keine flächige Umbenennung oder Verschiebung aller 599 `UI/Ai`-Dateien.

### AP10 – Python-Vertragsprüfung aufteilen

**Einstieg:** `_read_reviewed_negative_set` und `_read_proto_reviewed_negative_set` in `training/scripts/gold_stock_audit.py`.

**Vorgehen:**

1. Die bestehenden Prüfregeln in Gruppen benennen: JSON-Struktur, Rollen/Versionen, Klassenkarte, Schutzmengen, Dateien/Hashes und Beziehungen.
2. Einen reinen Prüfschritt mit einem kleinen Ergebnis auslagern. Fehlermeldung und Ablehnungsregel erhalten.
3. Wiederkehrende Verträge mit synthetischen gültigen und manipulierten Beispielen absichern. Für gemeinsame C#-/Python-Regeln dieselben Beispieldaten prüfen.
4. Dateilesen an wenigen klaren Grenzen halten. Die endgültige Freigabe erst nach allen notwendigen Prüfungen erteilen.

**Abnahme:** Kein vormals abgelehnter Fall wird akzeptiert. Kein Schutz wird nur wegen vermeintlicher Doppelprüfung entfernt. Alle vorgesehenen CPU-Tests laufen über AP02 automatisch.

**Rücknahme:** Einen Prüfblock pro Schritt auslagern. Manifest-, Receipt- und Klassenkartenformate bleiben unverändert.

### AP11 – Architekturwissen und Messung aktuell halten

**Dokumentation:** Kurze aktuelle Übersicht in `CLAUDE.md`; bereichsbezogene Regeln in passenden Dokumenten; historische Berichte klar datiert. `AGENTS.md` bleibt der kurze Einstieg. Die persönliche Architekturkarte wird nach echten Änderungen mit dem Code abgeglichen und validiert.

**Messung:** Die in diesem Audit verwendete Messung für Dateigrößen, Teilklassen, lange Methoden und ausgewählte Python-Funktionen wiederholbar halten. Zunächst eine kleine Ausgangsliste verwenden. Verbesserungen nach Verantwortung beurteilen; Zahlen dienen als Warnsignal.

**Abnahme:** Wichtige aktuelle Zahlen haben eine Quelle. Bereits erledigte Planpunkte sind erledigt markiert. Eine neue problematische Langmethode wird sichtbar. Normale fachliche Erweiterungen werden nicht durch beliebige absolute Grenzwerte blockiert.

## 5. Gemeinsamer Prüfweg je Paket

1. Ausgangsstand der Zieldateien und relevante bestehende Tests prüfen.
2. Bei riskanter Änderung zunächst einen fehlenden Verhaltenstest ergänzen und am Ist-Zustand prüfen.
3. Kleine Änderung vornehmen; gezielte Tests erneut ausführen.
4. Passendes Testprojekt und schnellen Release-Build prüfen.
5. Vor einem Code-Commit den vollständigen Weg aus `AGENTS.md` ausführen: Gesamtbuild und alle vier .NET-Testprojekte. Bei Sidecar-/QGIS-Arbeit deren zusätzliche Prüfungen; bei Trainingsskripten den vorgesehenen Python-Prüfweg.
6. Diff, öffentliche Verträge, Dateischutz, Fehlerverhalten und Dokumentation kontrollieren.

`--no-build` ist nur nach einem erfolgreichen passenden Build zulässig. Ein Testfehler wird entweder behoben oder bleibt als offener Befund sichtbar; eine gelockerte Grenze ist kein Ersatz.

## 6. Steuerung und Abschluss

Ein kleines Arbeitsprotokoll je Paket sollte enthalten: Ausgangsstand, konkrete Verantwortung, geänderte Dateien, erhaltene Verträge, ausgeführte Prüfungen, Ergebnis und Restgrenze. Die eigenen Änderungen werden so getrennt prüfbar, auch wenn im Projekt weitere Arbeit läuft.

Nach AP01–AP03 sollte entschieden werden, welcher große Ablauf im Alltag die meisten Änderungen verursacht. Danach folgt eines der Pakete AP05–AP10. Nach jedem zweiten größeren Paket die Messwerte und den tatsächlichen Such-/Testaufwand erneut vergleichen. Die Zahl neuer Klassen allein ist kein Erfolgskriterium.

Die Umsetzung ist erfolgreich, wenn Änderungen lokal verständlich bleiben, Fehler-/Abbruchfälle verlässlich geprüft werden und die bestehenden Schutzregeln erhalten sind. Eine neue Gesamtarchitektur ist dafür nicht nötig.
