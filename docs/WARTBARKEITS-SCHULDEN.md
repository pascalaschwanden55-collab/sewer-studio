# Wartbarkeits-Schulden

Stand: 2026-07-12

Neue God-Klassen werden durch `MaintainabilityFitnessTests` verhindert: Eine neue
Produktionsdatei darf nicht unbemerkt mehr als 1.000 Zeilen bekommen. Die vier
bekannten statischen DI-Ausnahmen sind ebenfalls fest eingefroren.

Die beim Audit festgehaltene Altliste ist vollständig abgearbeitet. Keine
Produktionsdatei liegt mehr über 1.000 Zeilen. Kleinere Verantwortungen werden
trotzdem weiterhin nur in durch Tests geschützten Schritten verschoben.

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

Achte Aufräumrunde erledigt am 2026-07-12:

- Die Verarbeitung eines bereits geparsten Haltungs-PDFs liegt in
  `ParsedHoldingDistributionController`. Die Verteiler-Fassade sank auf 940 Zeilen.
- LV-Aufbereitung und Ausgabeaufgaben liegen außerhalb des
  `BuilderPageViewModel`. Das Haupt-ViewModel sank auf 918 Zeilen.
- Darstellung und Bedienregeln der Fotomessung liegen in getrennten Bausteinen.
  Das Fenster sank auf 787 Zeilen.
- Navigation und Detailbearbeitung der Sanierungsmatrix liegen in eigenen
  Modellen. Das Haupt-ViewModel sank auf 942 Zeilen.

Damit sank die feste Altliste seit Beginn von 20 auf 2 Produktionsdateien mit
mehr als 1.000 Zeilen. Diese beiden Dateien bleiben bewusste, offene
Wartbarkeitsschulden und werden einzeln weiter zerlegt.

Neunte Aufräumrunde erledigt am 2026-07-12:

- Docking, Beobachtungsfenster, Filter und Datensatz-Menübefehle liegen in
  `DataPage.RecordInteractions`. Die Datenseite sank von 1.328 auf 858 Zeilen;
  ein Test prüft alle 43 eindeutigen XAML-Ereignisse über sämtliche Teildateien.
- Netzaufbau, Rendering und Animationen liegen in
  `StartupSplashWindow.Animation`. Reine Projektions- und Farbregeln sind separat
  testbar; das Startfenster sank von 1.175 auf 487 Zeilen.

Damit ist die feste Altliste von 20 auf 0 Produktionsdateien über 1.000 Zeilen
gesunken. Die Ausnahmeliste ist leer; der Fitness-Test verhindert neue
Großdateien weiterhin automatisch.

Zehnte Aufräumrunde begonnen am 2026-07-17:

- Winkel-, Abzweig-, Kreis- und Bogenplanung der Fotomessung liegt im reinen
  `PhotoMeasurementAnglePlanBuilder`. Die öffentliche
  `PhotoMeasurementGeometryService`-Fassade und ihre Ergebnisse bleiben unverändert;
  die Hauptdatei sank von 837 auf 720 Zeilen. 52 Geometrie-Tests schützen den Schnitt.

- Die Rohr-Radar-Zeichnung der Videoanalyse liegt jetzt im zustandslosen
  `PipelinePipeRadarRenderer`. Das Fenster sank von 831 auf 552 Zeilen; Detail,
  Kompakt, Sortierung, Grenzen, Texte und kleine Zeichenflächen sind durch
  Verhaltenstests geschützt. Auch 250 neue Befunde lösen nur eine Radarzeichnung
  aus.
- Die dreifache Live-Ring-Zeichnung aus Hauptfenster, abgedocktem Fenster und
  Player-Rückfall liegt im `LiveFrameRingOverlayRenderer`. Die drei sichtbaren
  Stile bleiben getrennt; Uhrwinkel, Ringform und Schadensfarben haben je eine
  gemeinsame Quelle. Das Hauptfenster sank zunächst auf 406 Zeilen, das abgedockte
  Fenster von 218 auf 65 Zeilen. Pro Fortschrittsmeldung wird höchstens einmal
  gezeichnet.
- Die Fortschrittsabbildung der Videoanalyse liegt jetzt im laufbezogenen
  `PipelineProgressMapper`. Er übernimmt Phasen, ETA, Parserwerte, Maximalzähler,
  Bild und die ersten acht Live-Befunde, greift aber weder auf Canvas noch auf das
  abgedockte Fenster zu. Dafür liefert er nur zwei Wirkungshinweise zurück. Das
  Hauptfenster sank damit zunächst auf 312 Zeilen; fokussierte Tests schützen auch
  den Unterschied zwischen fehlender und bewusst leerer Befundliste.
- Die erfolgreiche Abschlussabbildung liegt jetzt im zustandslosen
  `PipelineResultPresenter`. Er berechnet Statistik, Telemetrie, Rohdaten-Zähler und
  höchstens 250 sichtbare `DetectionItem`-Zeilen. Gemappte Einträge haben dabei weiter
  Vorrang; die endgültigen Säulenzähler kommen weiterhin aus allen Rohbefunden. Das
  Fenster behält Fehler, Lifecycle, Sammelersetzung und genau eine Radarzeichnung und
  sank auf 285 Zeilen. Offen bleibt eine bestehende fachliche Grenze: Die 250 sichtbaren
  Zeilen steuern zugleich die spätere Übernahme. Weitere gemappte Befunde wären damit
  nicht auswählbar und würden verworfen. Das nicht still in einer Aufräumänderung ändern.

Regel für jeden Umbau: Erst bestehendes Verhalten durch Tests festhalten, dann
eine Verantwortung verschieben, vollständige Tests ausführen und erst danach das
nächste Teilstück beginnen. Kein Komplett-Umbau der gesamten Klasse in einem Zug.
