# Player: zeitgebundener OSD-Meter beim Handeintrag – 03.10.2026

## Kleines Fehlerpaket

Basis `master 72d47f298`, Zweig `codex/player-osd-meter-cache`. Die offenen Player-Pakete
PR 84/86 und Training-Center-PR 85 sind nicht enthalten. PR 86 hatte beim Start alle vier
GitHub-Prüfungen bestanden. Dieses Paket ändert keine Zeichnungs-/KI-/Importabläufe.

Der UI-Resolver verwendet einen Cachemeter nur mit plausibler Entfernung und Zeitbeleg.
Die vorhandene Application-Regel wird öffentlich wiederverwendet, ihr Körper bleibt
unverändert. 0–500 m und absoluter Zeitabstand höchstens 1,5 s; Vorwärts-/Rückwärtssprung
gleich behandelt. Unbekannte/nicht endliche Cachezeit ignoriert. Keine neue negative
Zeitprüfung; bestehende relative Altersregel unverändert.

In der ursprünglichen Lieferung blieb die sechsstellige Fassade erhalten. Nach dem
Reviewhinweis zu PR 87 wird sie auf ausdrücklichen Wunsch entfernt: Jeder Aufrufer
muss den nicht optionalen siebten Zeitparameter übergeben. Bei unbekannter Zeit wird
null ausdrücklich angegeben; unbelegte Cachewerte werden bewusst ignoriert. Frischer OSD, Rundung, Untergrenze und unbeschränkte bisherige
UI-Videorechnung bleiben erhalten. Das Fenster reicht nur eine zusätzliche Zeitquelle
weiter. Keine neuen UI/Ai-Typen, Pakete, Dienste, Registrierung oder Datenformate.

## Fehler zuerst gezeigt

Drei zusätzliche compilebare Regressionen wurden vor Produktionsänderungen ausgeführt;
alle bisherigen Erwartungen blieben zunächst unverändert:

- Rückwärtssprung von 90 auf 5 Sekunden: erwartet 4 m, tatsächlich 70 m.
- Unbelegter Cache ohne brauchbare Videodauer: erwartet Sitzung 4,57 m, tatsächlich 70 m.
- Isolierter Fensteraufruf: LastMeter vorhanden, LastTimestampSeconds fehlt.

Rotlauf: 21 bestanden, genau diese drei Fehler, null übersprungen. Alle drei Original-
Programmquellen waren vor und nach diesem Lauf bytegleich (SHA-256-Abgleich).
Die beiden Verhaltenserwartungen bleiben nach der Korrektur unverändert; der
Anschlusswächter wurde um die ausdrückliche benannte Übergabe präzisiert.

19 zusätzliche Fälle schützen ±1,5 s/±1,501 s, fehlende/NaN/±Inf Zeit, Cachemeter
0/500/negativ/>500/NaN/±Inf/null, frischen OSD samt negativer Untergrenze und
Videorückfall jenseits der Dauer. Der bestehende gültige Cachetest behält seine
Erwartung und erhält nur einen ausdrücklichen frischen Zeitbeleg.

## Zuständigkeit und Abschluss

Zwei Sol-Agenten mit mittlerem Aufwand bearbeiteten getrennt drei Programmdateien und
zwei Testdateien. Ein Sol-Agent mit hohem Aufwand prüft den eingefrorenen Stand
unabhängig. Root passt den Größenwächter an, dokumentiert und führt alle Builds/Tests
und Gitaktionen zentral aus. Keine Programmumstellung und keine Kundendatenänderung.

Endgültige Gegenprüfung einschließlich korrigierter Testdatentypen freigegeben.
Alle sechs eingefrorenen Quell-/Testdateien nach den zentralen Läufen SHA-256-identisch.
Architekturkarte mit dem tatsächlichen Code abgeglichen und validiert.

| Vollständige Release-Prüfung | Bestanden | Übersprungen | Fehler |
| --- | ---: | ---: | ---: |
| Infrastruktur | 8.256 | 6 | 0 |
| Pipeline | 3.048 | 3 | 0 |
| Oberfläche | 7.928 | 49 | 0 |
| ProjectModernizer | 62 | 0 | 0 |
| Gesamt | 19.294 | 58 | 0 |

Vollständiger Release-Build: null Warnungen und Fehler. Endfokus: 83 bestanden,
null übersprungen/Fehler. Push-/GitHub-Status gehört in die getrennten Liefernachweise.

## Korrektur der Testdaten im ersten Endfokus

Der vollständige Release-Build war ohne Warnungen/Fehler. Im ersten Endfokus bestanden
81 Prüfungen; zwei weitere konnten wegen eines falschen Inline-Datentyps nicht starten:
`Int32` ließ sich nicht an `Nullable<Double>` übergeben. Nur die beiden Angaben
0/500 wurden auf 0.0/500.0 gesetzt; Erwartungen und Programmlogik bleiben unverändert.
Der erste Lauf und seine Prüfsummen sind erhalten. Build und alle Prüfungen wurden
auf dem nachgeführten eingefrorenen Stand erneut erfolgreich ausgeführt.

## Größenmessung

Alle 73 PlayerWindow-Teildateien gemeinsam, inklusive Leer-/Kommentarzeilen:
4.246 → 4.247. Hauptdatei unverändert 568 Zeilen. Die zusätzliche Fensterzeile übergibt
nur den Zeitbeleg; Fachlogik liegt weiterhin im Resolver. Der Wachstumswächter ist
nachvollziehbar auf 4.247 angepasst. Dies ist eine Fehlerkorrektur, keine Verkleinerung.
Ziel unter 1.000 für die ganze Klasse bleibt offen; offene Pakete nicht zusammengezählt.

## Grenzen

Kein echter Video-/OCR-/KI-Lauf. Tests prüfen Berechnungen und den tatsächlichen
Fensteranschluss. Kein Merge nach master, keine Änderung des alten Hauptarbeitsstands
oder der Originaldatensicherung. Negative Zeitregeln und ursprüngliche frische
OSD-Behandlung werden nicht nebenbei verändert.

## Reviewrevision: Zeitparameter verpflichtend (03.10.2026)

Der noch ungelieferte CodingEventActionsPresenter im Hauptordner ruft die alte
sechsstellige Variante auf. Sie würde auch frische Cachewerte ignorieren. Die alte
Überladung ist deshalb entfernt; der Build verlangt bei jeder späteren Portierung den
Zeitparameter. Der Hauptordner wird in dieser Revision nicht verändert. Der Presenter
muss beim Übernehmen LastTimestampSeconds seines vorhandenen OsdMeterController
übergeben. Der produktive Fensteraufruf auf diesem PR tut das bereits.

Der API-Wächter war vor der Entfernung rot (zwei öffentliche Überladungen statt einer).
Sieben bestehende Testaufrufe bekommen ausdrücklich null; sämtliche bestehenden
Erwartungen und der siebenstellige Berechnungskörper bleiben unverändert. Die erste
Lieferung samt ihren Prüfzahlen oben bleibt als historischer Nachweis erhalten.
Neue Release-, Hook- und Liefernachweise werden getrennt erfasst.

Abschluss dieser Revision: unabhängige Gegenprüfung ohne Befund. Vollständiger
Release-Build null Warnungen/Fehler, Endfokus 84 bestanden/0 übersprungen/0 Fehler.
Infrastruktur 8.256/6, Pipeline 3.048/3, Oberfläche 7.929/49, Modernizer 62/0
(bestanden/übersprungen); insgesamt 19.295 bestanden, 58 übersprungen, null Fehler.
Alle sechs eingefrorenen Quell-/Testhashes nach dem Gesamtlauf identisch. Architekturkarte
mit Code abgeglichen und validiert. Push und GitHub-Stand gehören in die getrennten
Revisionsnachweise. Die Fenstergröße bleibt gegenüber der ersten Lieferung unverändert.
