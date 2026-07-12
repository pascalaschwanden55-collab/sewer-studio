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
4. Große Importer: Bei WinCan ist das Datenbanklesen jetzt getrennt;
   `LegacyXtfImportService` weiterhin in Lesen, Zuordnen und Schreiben teilen.

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

Regel für jeden Umbau: Erst bestehendes Verhalten durch Tests festhalten, dann
eine Verantwortung verschieben, vollständige Tests ausführen und erst danach das
nächste Teilstück beginnen. Kein Komplett-Umbau der gesamten Klasse in einem Zug.
