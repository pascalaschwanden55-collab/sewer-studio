# Wartbarkeits-Schulden

Stand: 2026-07-12

Neue God-Klassen werden durch `MaintainabilityFitnessTests` verhindert: Eine neue
Produktionsdatei darf nicht unbemerkt mehr als 1.000 Zeilen bekommen. Die vier
bekannten statischen DI-Ausnahmen sind ebenfalls fest eingefroren.

Das bedeutet nicht, dass der Altbestand bereits vollständig aufgeteilt ist. Die
größten offenen Pakete sind:

1. `HoldingFolderDistributor`: Die öffentliche Fassade beibehalten, intern aber
   Haltung, Schacht, Dichtheit, PDF-Parsing, PDF-Umschreiben und Video-Matching in
   eigenständige Dienste mit kleinen Schnittstellen trennen.
2. Große WPF-Code-behind-Dateien (`DataPage`, `SchaechtePage`,
   `PhotoMeasurementWindow`): Bedienlogik schrittweise in Controller verschieben.
3. Große ViewModels (`BuilderPageViewModel`, `CostCalculatorViewModel`,
   `SanierungsMatrixPageViewModel`): Laden, Berechnen, Exportieren und Dialoge
   nicht in derselben Klasse halten.
4. Große Importer: WinCan und `LegacyXtfImportService` liegen jetzt unter der
   1.000-Zeilen-Grenze. Die nächsten Importer nur in kleinen, getesteten Schritten
   anfassen.

Erster Schritt erledigt am 2026-07-12: Dateikopien, eindeutige Zieldateinamen
und Video-Konflikthinweise wurden aus `HoldingFolderDistributor` in
`DistributionFileTransfer` und `VideoConflictArtifacts` verschoben. Vier neue
Charakterisierungstests schützen das Verhalten; der vollständige
Infrastruktur-Testlauf blieb grün.

Zweiter Schritt erledigt am 2026-07-12: Die Erweiterung einer Schacht-PDF-Auswahl
auf zusammengehörige Dateien liegt nun in `ShaftPdfSelectionExpander` statt in
`HoldingFolderDistributor`. Drei eigene Tests schützen Einzeldatei, mehrere
Schächte und fehlende Dateien. Die öffentliche Fassade blieb unverändert.

Dritte Aufräumrunde erledigt am 2026-07-12:

- Interaktive Schacht-PDF-Formfelder liegen in `ShaftPdfFormFieldParser`.
- WinCan liest seine SQLite-Tabellen über `WinCanDbReader`. Der Hauptservice sank
  von 1.224 auf 984 Zeilen und wurde aus der Großdatei-Ausnahmeliste entfernt.
- Abgeschlossene Tabellenänderungen liegen in `DataPageCellEditController`.
- HWiNFO-Shared-Memory-Einlesen und Sensorzuordnung sind vom
  `SystemMonitorService` getrennt und ohne echte Hardware testbar.

Der Fitness-Test prüft nun zusätzlich, dass keine bereits verkleinerte Datei
unbemerkt in der Großdatei-Ausnahmeliste stehen bleibt.

Vierte Aufräumrunde erledigt am 2026-07-12:

- VSA-Protokollabgleich und VSA-Mediensuche liegen nicht mehr im
  `LegacyXtfImportService`. Der Hauptservice sank auf 990 Zeilen und wurde aus
  der Großdatei-Ausnahmeliste entfernt.
- Die PDF-Textkorrektur des `HoldingFolderDistributor` ist in einen Umschreiber
  und einen reinen Treffer-Ermittler getrennt. Schutztests verhindern Treffer
  über getrennte Textblöcke und prüfen das erzeugte PDF.
- Spaltennamen, Gruppen und Prioritäten der Schachtseite liegen in
  `SchaechteColumnPolicy`. Die Regeln sind ohne gestartete Oberfläche testbar.
- Fehlerhafte alte Umlautkodierungen werden zentral und unabhängig von
  Groß-/Kleinschreibung normalisiert.

Fünfte Aufräumrunde erledigt am 2026-07-12:

- Die WinCan-Beobachtungszuordnung des M150-Imports liegt gemeinsam für MDB und
  XML in `WinCanObservationAttacher`. Der M150-Hauptbaustein sank auf 897 Zeilen.
- Quantifizierung und Ergebnisabbildung der Mehrmodell-Analyse liegen in
  `MultiModelFrameAnalysisMapper`. Der Hauptservice sank auf 965 Zeilen.
- Projektwechsel, Karten-Auswahl und laufende Nummern der Datenseite liegen in
  `DataPageProjectBindingController`. Das ViewModel sank auf 971 Zeilen.
- Die öffentlichen Datenmodelle der EvalSet-Auswertung liegen getrennt von den
  Berechnungen in `EvalSetBenchmarkModels`. Die Logikdatei sank auf 910 Zeilen.

Sechste Aufräumrunde erledigt am 2026-07-12:

- Das WinCan-Kataloglesen liegt in `WinCanCatalogXmlParser`. Der öffentliche
  `XmlCodeCatalogProvider` sank auf 821 Zeilen.
- Sammeln, Nummerieren und Zeichnen der Protokollfotos liegt in
  `ProtocolPdfPhotoSection`. Der PDF-Hauptbaustein sank auf 950 Zeilen.
- Importhistorie, PDF-Nachlauf und Medienverteilung liegen in
  `ImportPostProcessingController`. Das Import-ViewModel sank auf 827 Zeilen.
- Die Wissensdatenbank-Anzeige des Trainingszentrums liegt in einem eigenen
  Dashboard-Controller. Das ViewModel sank auf 984 Zeilen.
- Der Import-Nachlauf arbeitet nun auf der neuen Projektkopie. Dadurch bleiben
  Metadaten und nachträglich zugeordnete Daten nach dem Projektwechsel erhalten.

Siebte Aufräumrunde erledigt am 2026-07-12:

- Seitenlesen und Zuordnung der Verteil-PDFs liegt in
  `DistributionPdfAssignmentController`. Die PDF-Datei sank auf 976 Zeilen.
- LibreHardwareMonitor-Initialisierung und Sensorauswahl liegt in
  `LibreHardwareMonitorSensor`. Der Systemmonitor sank auf 920 Zeilen.
- Maßnahmenblock, Kostenzeile und Auswahlmodelle liegen in eigenen Dateien. Der
  Kostenrechner sank auf 550 Zeilen.
- Aufbau der Schachtdetails und Schachtnummer-Umbenennung liegen in eigenen
  Bausteinen. Die Schachtseite sank auf 999 Zeilen.

Damit sank die feste Altliste seit Beginn von 20 auf 6 Produktionsdateien mit
mehr als 1.000 Zeilen. Die übrigen 6 Dateien bleiben bewusste, offene
Wartbarkeitsschulden und werden einzeln weiter zerlegt.

Regel für jeden Umbau: Erst bestehendes Verhalten durch Tests festhalten, dann
eine Verantwortung verschieben, vollständige Tests ausführen und erst danach das
nächste Teilstück beginnen. Kein Komplett-Umbau der gesamten Klasse in einem Zug.
