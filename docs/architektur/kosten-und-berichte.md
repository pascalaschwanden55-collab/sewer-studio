# Kosten, Excel-Berichte und Stammdaten

> Aus `CLAUDE.md` ausgelagert am 30.09.2026 (Wartbarkeitsaudit, Befund Z1). Der Text ist
> **unverändert** übernommen: Geltende Regeln stehen neben datierten Arbeitsständen und
> Messverläufen. Bei Widersprüchen gilt der jüngere Abschnitt und im Zweifel der Code
> samt seinem Test. Veränderliche Zahlen (Dienstanzahl, Testanzahl) sind Momentaufnahmen.
>
> Neue Erkenntnisse zu diesem Bereich hier eintragen, **nicht** in `CLAUDE.md`.

## Inhalt

- (Fortsetzung aus «Begleitprotokolle der Sanierung und Videodopplung (Bürglen, 09.09.2026)»)

## (Fortsetzung aus «Begleitprotokolle der Sanierung und Videodopplung (Bürglen, 09.09.2026)»)

Geldrelevante Kosten-, Mengen- und Laengentexte in Kostenrechner, Matrix und Export
laufen zentral ueber `FachzahlParser` und nie ueber `CurrentCulture`: Punkt oder Komma
als Dezimaltrenner sowie korrekt gruppierte Schweizer Apostroph-/Leerzeichenwerte
werden auf de-DE, de-CH und en-US identisch behandelt; mehrdeutige Werte werden
abgelehnt. `CostCatalogStore` und
`MeasureTemplateStore` und `PositionTemplateStore` melden beschaedigte Default- oder
Override-Dateien mit
`loadError`. Kostenrechner, Haltungs-/Schachtmatrix und Builder sperren dann
Neuberechnung, Speichern und Geld-Exporte, statt mit leerem Katalog plausible
Nullwerte zu erzeugen. Fehlende, nichtpositive oder ungueltige Haltungslaengen
blockieren laengenbasierte Positionen im Kostenrechner und in der Matrix;
nichtpositive Schachtmengen blockieren Berechnung und Speichern ebenfalls.
Der Codiermodus darf `Haltungslaenge_m` nur aus einem bereits gueltigen Feld,
aus `Laenge_m` unter Erhalt seiner `FieldSource` oder aus genau einem aktiven
`BCE` ableiten. Ein BCE-Wert wird als `FieldSource.Protocol` markiert und bleibt
unterhalb echter Importquellen priorisiert. Schadensmeter und das daraus gebaute
Video-Overlay sind keine Laengenquelle; fehlt eine sichere Quelle, fragt der
Codiermodus nach einer manuellen Eingabe.
Nach einer bestaetigten Uebernahme fuegt `CodingApplyController` automatisch erzeugte
`BCD`-/`BCE`-Grenzereignisse derselben `ICodingSessionService`-Sitzung hinzu. Damit gehoeren
sie beim naechsten Uebernehmen zum echten Ausgangsstand und werden weder erneut vorgeschlagen
noch als geloeschte Ereignisse behandelt. Automatische Grenzen werden nicht als Trainingsfall
gespeichert; Abbrechen veraendert die Sitzung nicht.
`ServiceProvider` erzeugt genau eine live lesende `IProtocolPdfLayoutSettings`-Instanz aus
`AppSettings`. `ProtocolPdfExporter` sowie die produktiven Dossier-Dialogwege verwenden
dieselbe Instanz; beim Klick wird `settings.json` nicht erneut geladen. Erlaubt sind 1, 2, 4
oder 6 Fotos je Seite, unbekannte Werte fallen auf 2 zurueck; explizite Exportoptionen haben
Vorrang vor der Programmeinstellung.
Der aktive `CodeCatalog` wird dem gemeinsam genutzten `ProtocolPdfExporter` als Standard
mitgegeben; ein expliziter Katalog in `HaltungsprotokollPdfOptions` hat Vorrang.
`ObservationZustandBuilder` liefert fuer Befundetabelle, Haltungsgrafik und Fototitel
denselben deduplizierten Klartext aus Katalogtitel, Operateurtext und vorhandenen Parametern.
Uhrlagen werden nur aus gueltigen Uhrwerten gelesen; ein alter WinCan-Meterwert wie
`2.62136` darf weder als `2 Uhr` noch als `Schadenlage` weitergereicht werden.
Die Haltungsgrafik laeuft oben nach unten in Aufnahmerichtung. Deshalb spiegelt
`flowDown` nur den Fliesspfeil, nie die Kamera-Uhrlage: 1-5 Uhr liegen auf dem Blatt links,
7-11 Uhr rechts; der Stutzenwinkel folgt der erfassten Stunde in 30-Grad-Schritten.
Ausgewaehlte Kostenrechner-Zeilen mit negativer Menge oder negativem Preis werden
weder summiert noch gespeichert, uebernommen oder exportiert. NPK-Codes werden in
CSV und Excel als Text ausgegeben, damit etwa `612.110` nicht zu `612.11` gekuerzt
wird.

Die Haltungs- und Schachtberichte verwenden die datenfreien Vorlagen
`Export_Vorlage/Haltungen.xlsx` und `Export_Vorlage/Schächte.xlsx`. Ihre lesbare,
reproduzierbare Quelle liegt unter `tools/ExcelVorlagenBauer/`; die dort gepinnten
Werkzeuge erzeugen Logo, sieben Diagramme, Kennzahlenformeln, Bedeutungsfarben,
Druckeinrichtung und genau eine gestaltete Musterzeile. Titel, Kopfzeile und Daten
beginnen verbindlich in den Zeilen 25, 26 und 27. Formeln und bedingte Formatierung
reichen bis Zeile 5000, deshalb lehnt der Export mehr als 4'974 Datensaetze klar ab.
Beide im Bestand belegten Pruefresultat-Familien werden gezaehlt und gefaerbt, ohne
gespeicherte Werte umzudeuten.

`ExcelTemplateExportService` fuellt ausschliesslich eine geladene Arbeitsmappe:
fehlende Werte bleiben echte Leerzellen, Kennungen und Datumsangaben bleiben Text,
definierte Mengen und Kosten werden als Zahlen geschrieben. Ein nichtleerer,
ungueltiger Zahlenwert blockiert den Export mit Zeile und Spalte; ein bestehendes
Ausgabeziel bleibt dabei unveraendert. Schachtspalten verwenden einen expliziten
Aliasvertrag. Die Linkspalte liest der Reihe nach `Link`, `PDF_Path`, `PDF_Eigen` und
`PDF_All`. Relative Projektverweise werden mit dem beim Start gebundenen Projektpfad
zu absoluten Dateilinks aufgeloest; Pfadausbrueche bleiben unanklickbarer Text. Der
Dienst schreibt zuerst eine Temp-Datei im Zielordner, prueft XLSX-Pflichtteile und
Blatt erneut und veroeffentlicht erst danach. Die additiven
`IExcelExportService`-Overloads mit `CancellationToken` pruefen den Abbruch beim
Laden, je Datensatz und an jeder Veroeffentlichungsgrenze. Ein Abbruch waehrend des
synchronen ClosedXML-Schreibens wird am naechsten sicheren Punkt wirksam: Die
Temp-Datei wird entfernt und ein bestehendes Ausgabeziel bleibt unveraendert. Die
Vorlage darf nie selbst Ziel sein und bleibt bytegleich.

Der Haltungs-Export zieht abgeleitete Kosten nur auf einer unabhaengigen
`HoldingExcelExportSnapshotFactory`-Kopie nach. Das geoeffnete Projekt, seine
Feldmetadaten, Zeitstempel und sein Dirty-Status bleiben durch den reinen Export
unveraendert. Beide Exporte pruefen die ausgelieferte Vorlage vor Ziel- und
Kostenarbeit. Eine fehlende Vorlage oder unlesbare Kostendaten erscheinen bewusst
nur einmal als blockierender Dialog; Status und Ergebnistext werden weiterhin gesetzt.
`ExcelExportVorlagentreueTests` schuetzen Datenfreiheit, Formeln, Diagramme, Farben,
Logo, Fixierung, Druck und Neuberechnung fuer beide Blaetter.

Auf ausdruecklichen Nutzerwunsch bleibt seit der Korrektur vom 08.09.2026
alles auf genau einem Tabellenblatt je Export. Diagramme und Projektkennzahlen
stehen oben, die vollstaendige Liste darunter. Keine weiteren Blaetter und keine
zusaetzliche Lesefassung. Die 27 Haltungs-/17 Schachtspalten bleiben erhalten.
`arbeitsliste.py` ergaenzt SUBTOTAL(103/109) in Zeile 24 fuer sichtbare Anzahl,
Haltungslaenge und Kosten. Gesamtsummen in Zeile 23 bleiben unabhaengig vom Filter.
Titel/Kopf/Daten behalten die Vertragszeilen 25/26/27. Kennungen bleiben beim
Scrollen fixiert. Lange Texte bleiben vollstaendig in ihren Originalzellen;
der volle Inhalt ist in Excels Bearbeitungsleiste zugaenglich.
Alle Spalten werden gemeinsam auf einer A3-Seitenbreite gedruckt; die Hoehe
folgt der Zeilenzahl. `ExcelArbeitsansicht` begrenzt nur den Druckbereich und
setzt den Projektdruckkopf. `ExcelDrucktitel` normalisiert die Wiederholungs-
bereiche vor der erneuten Dateipruefung weiterhin auf absolute Bezüge.
Keine neue Registrierung und keine Aenderung von Projektformaten/Schnittstellen.
`ExcelArbeitsansichtTests` prueft genau ein Blatt, Spaltenbestand, Textinhalt,
Filter-/Gesamtsummen, Leerprojekte und Druckeinstellungen.

Die drei Stammdaten-Stores lehnen `null`-Strukturen, doppelte normalisierte
Kosten-/Vorlagen-Identitaeten und negative Mengen ab. Vor jedem Save wird auch eine
vorhandene Override-Datei neu gelesen; ein frisch erzeugter Store darf deshalb keine
beschaedigte Datei ueberschreiben, selbst wenn vorher kein Load aufgerufen wurde.
`CostStoreFileProbe` unterscheidet fehlende Dateien von Ordnern, Verknuepfungen und
unlesbaren Pfaden. `ProjectCostStoreRepository` verwendet diese Pruefung fuer
`costs.json`, `schacht_costs.json` und `schacht_empfehlungen.json`, liest ein
vorhandenes Ziel unmittelbar vor jedem Save erneut und ueberschreibt bei einem
Lesefehler nichts. Der Schacht-Massnahmendialog oeffnet in diesem Zustand nicht.

