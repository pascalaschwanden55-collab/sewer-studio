# Importfortschritt (09.09.2026)

Der Ein-Knopf-Import zeigt Schritt, aktuelle Datei bzw. Haltung, Zaehler und eine
ungefaehre Restzeit fuer den laufenden Schritt. Die Restzeit beginnt erst nach drei
erledigten Einheiten. Sie ist keine Schaetzung fuer den ganzen Import.

Die sieben Anzeigeschritte buendeln den unveraenderten technischen Ablauf:

| Anzeige | Bestehende Arbeit |
| --- | --- |
| 1 Vorbereiten | Projektstruktur, Wiederherstellungspunkt, Formaterkennung |
| 2 Archivieren | Quellarchiv und Plan-PDFs |
| 3 Quelldaten | Hauptquelle, ergaenzende Quellen, KINS und Kataster/SIA405 |
| 4 Medien | Fotos je Haltung verteilen |
| 5 Haltungsprotokolle | Namenszuordnung, Sammelprotokolle, Videos, Dichtheit |
| 6 Schachtprotokolle | Archiv-PDFs pruefen, aufteilen und verknuepfen |
| 7 Abschliessen | Dateipruefung, Ergebnispruefung und bestehender Speicherweg |

`ImportFortschrittText` und `ImportRestzeitSchaetzer` liegen WPF-frei in
`Application/Import`. Es sind reine Anzeigeregeln; der Schaetzer gehoert jeweils zu
einem Lauf und benoetigt keine gemeinsame Dienstregistrierung.
`ImportOneClickProjectController` erstellt den `Progress<ImportProgress>` auf dem
UI-Kontext. Seine optionalen Aktionen verbinden Phase, Text, Prozent, Zaehler,
Restzeit und unbestimmten Fortschritt. Nach dem Hintergrundlauf werden verspaetete
Meldungen verworfen; beim Abschluss wird die Anzeige geleert.

`ProjectImportOrchestrator` meldet die Schrittgrenzen. Seine synchronen Adapter
reichen die vorhandenen Medien- und Schachtmeldungen durch und halten beim Einlesen
den uebergeordneten Schritt. Log, Abbruch, DryRun, CollectionLock und Staging bleiben
im Lesekontext erhalten.

Zwei Unterschiede zum urspruenglichen Vorschlag sind im echten Code belegt:

- Der Mediendienst meldet erledigte **Haltungen**, keine einzelnen Fotodateien.
  Deshalb zeigt die Karte dort `Haltung` und schaetzt nach erledigten Haltungen.
- Der vorbereitete Schachtimport ruft den Splitter je PDF einzeln auf.
  Dessen `1 von 1` ist keine Gesamtzahl. `ShaftDistributionService` meldet deshalb
  vor und nach jedem Quellenversuch den Stand ueber alle Quell-PDFs. Ausgelassene
  PDFs und Lesefehler zaehlen als bearbeitet, nicht als erfolgreicher Import.
  Vor der anschliessenden Dateivorbereitung wird der Zaehler wieder unbestimmt.

Ohne bekannte Anzahl pulsiert der Balken. Die Karte verwendet `RadiusBar`, einen
10-px-Hoehentoken, die Akzentfarbe und den vorhandenen `NeuralPulseDot`. Der Punkt
respektiert die bestehende Einstellung fuer reduzierte Bewegung. Die unbestimmte
Balkenanimation bleibt als Arbeitsanzeige sichtbar. Lange Dateinamen lassen sich
im Hinweis vollstaendig lesen; der Ordnerpfad wird nicht angezeigt.

Verarbeitung, Auswahl, Fehlerbilanz, gespeicherte Datenformate und Transaktion
bleiben unveraendert. Keine neuen Pakete. Ein grosser echter Kundenimport wurde
fuer diese Anzeigearbeit nicht erneut ausgefuehrt.

Nachweise: `ImportFortschrittTextTests`, `ImportRestzeitSchaetzerTests`,
`ProjectImportOrchestratorTests`, `ShaftDistributionServiceTests`,
`ImportPageViewModelProjectToolsTests` und `ImportFortschrittIsolatedSmokeTests`.

## Pruefergebnis

- Abschliessender Release-Alltagsbuild: 0 Warnungen, 0 Fehler.
- Vollstaendiger Release-Build vor dem Commit: 0 Warnungen, 0 Fehler im getrennten
  Ausgabeordner `.tmp/import-push-build` (`--artifacts-path`), da der normale
  Ausgabeordner durch laufende Hilfsprogramme gesperrt war.
- Abschliessende fokussierte Pruefung: 47 Infrastrukturtests und 305 UI-/Design-
  Tests erfolgreich. Der isolierte Karten-Kindtest wird im Elternlauf planmaessig
  ausgelassen und vom Elterntest separat erfolgreich ausgefuehrt.
- Karte in Hell und Dunkel gerendert und angesehen; 10 px Hoehe, Zaehler, Rundung
  und bestimmter/unbestimmter Balken im echten WPF-Aufbau geprueft.
- Zusaetzlicher vollstaendiger Infrastrukturtest: 6346 bestanden, 6 vorgesehene
  Auslassungen.
- Zusaetzlicher vollstaendiger UI-Test: 6902 bestanden, 19 vorgesehene Auslassungen,
  ein Fehler ausserhalb dieses Auftrags. `QgisPluginPackagingTests` erwartet noch
  `D:\QGIS_V4.03\AWU_Plugins`; das parallel geaenderte Installationsskript verwendet
  `D:\QGIS_V4.2\AWU_Plugins`. Keine QGIS-Datei fuer diesen Auftrag geaendert.
- Architektur-Skill ergaenzt und mit `quick_validate.py` erfolgreich validiert.
- Vollstaendige Pipeline-Tests: 2648 bestanden, 3 vorgesehene Auslassungen.
- Vollstaendige ProjectModernizer-Tests: 62 bestanden.
- Nach dem separat vorliegenden QGIS-4-Commit wurden ausschliesslich die veralteten
  Pfaderwartungen in `QgisPluginPackagingTests` angepasst (eigener Test-Commit).
  Erneuter vollstaendiger Release-Build: 0 Warnungen, 0 Fehler; vollstaendige
  Release-UI-Tests danach: 6903 bestanden, 19 vorgesehene Auslassungen, 0 Fehler.

Kein laufendes SewerStudio wurde beendet.
