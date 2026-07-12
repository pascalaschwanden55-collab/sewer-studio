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
4. Große Importer (`WinCanDbImportService`, `LegacyXtfImportService`): Lesen,
   Zuordnen und Schreiben getrennt testen und danach extrahieren.

Erster Schritt erledigt am 2026-07-12: Dateikopien, eindeutige Zieldateinamen
und Video-Konflikthinweise wurden aus `HoldingFolderDistributor` in
`DistributionFileTransfer` und `VideoConflictArtifacts` verschoben. Vier neue
Charakterisierungstests schützen das Verhalten; der vollständige
Infrastruktur-Testlauf blieb grün.

Zweiter Schritt erledigt am 2026-07-12: Die Erweiterung einer Schacht-PDF-Auswahl
auf zusammengehörige Dateien liegt nun in `ShaftPdfSelectionExpander` statt in
`HoldingFolderDistributor`. Drei eigene Tests schützen Einzeldatei, mehrere
Schächte und fehlende Dateien. Die öffentliche Fassade blieb unverändert.

Regel für jeden Umbau: Erst bestehendes Verhalten durch Tests festhalten, dann
eine Verantwortung verschieben, vollständige Tests ausführen und erst danach das
nächste Teilstück beginnen. Kein Komplett-Umbau der gesamten Klasse in einem Zug.
