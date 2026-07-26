# SewerStudio Integritätsaudit – 26. Juli 2026

## Urteil

Der geprüfte Stand ist **eingeschränkt gesund und deutlich robuster als vor dem
Audit**. Für die untersuchten Geld-, Speicher-, Import-, Backup- und KI-Pfade ist
kein offener kritischer oder hoher Fehler mehr belegt. Das ist trotzdem kein
Versprechen, dass das Programm fehlerfrei oder bereits „perfekt“ ist.

Geprüft wurde der saubere Git-Stand:

- Commit: `1d5989d3b424edb526a63229469ce941897bcebd`
- Tree: `b2b1fce3234ba29ceb32bf5b4c4d157f0c431bcf`
- Arbeitsbaum vor diesem Bericht: sauber
- Plattform: Windows, .NET 10, Python-Sidecar ohne GPU-Tests

## Automatische Prüfung

| Prüfung | Ergebnis |
|---|---:|
| Vollständiger Release-Build, 49 Solution-Projekte | 0 Fehler, 23 Warnungen |
| Infrastructure | 3’250 bestanden, 12 übersprungen |
| Pipeline | 1’999 bestanden, 1 übersprungen |
| UI | 5’426 bestanden, 1 übersprungen |
| ProjectModernizer | 62 bestanden |
| Sidecar, Marker `not gpu` | 162 bestanden, 2 übersprungen, 2 GPU-Tests abgewählt |
| Zusätzliche Python-Tests ausserhalb des Standardwegs | 56 bestanden |
| QGIS-Brückentests | 5 bestanden |

Damit wurden **10’960 Tests erfolgreich ausgeführt**. Es gab keinen Testfehler.
`git diff --check` war ebenfalls sauber. Alle zuvor ausserhalb der Solution
liegenden Werkzeuge sind jetzt in der Solution enthalten und wurden mitgebaut.

## Im geprüften Stand geschlossene Integritätsfehler

- Geldrelevante Zahlen werden in den betroffenen Kosten-, Matrix- und Exportpfaden
  kulturunabhängig gelesen. Mehrdeutige Werte werden abgelehnt.
- Fehlende, ungültige, null oder negative Haltungslängen erzeugen keine stillen
  Meterkosten mehr.
- Negative Mengen oder Preise in ausgewählten Kostenrechner-Zeilen werden weder
  summiert noch gespeichert, übernommen oder exportiert.
- Nichtpositive Schachtmengen werden nicht mehr still als Menge `1` berechnet.
- Beschädigte Kosten-, Katalog- und Vorlagendateien sperren das Speichern. Direkte
  Saves dürfen eine vorhandene beschädigte Datei nicht überschreiben.
- NPK-Positionsnummern bleiben in CSV und Excel Text und verlieren keine Endnullen.
- Der manuelle Import besitzt Staging, Journal, Hashprüfung und Wiederaufnahme.
  Der Ein-Knopf-Import prüft Projektinstanz, Projektpfad und Inhalt vor der
  Übernahme erneut.
- Backup und Spiegelung prüfen Quell- und Zielgrenzen, Marker sowie
  Verknüpfungen/Junctions, bevor sie schreiben, verschieben oder löschen.
- Die Gold-Daten-Trennung prüft exakte IDs, alle fachlichen Gold-Felder und
  Embeddings; ein unterbrochener Commit wird auf den geprüften Ausgangsstand
  zurückgesetzt.
- Sidecar-, Training- und Eval-Pfade scheitern bei unvollständigen oder
  widersprüchlichen Ergebnissen laut, statt plausible Ersatzwerte zu erzeugen.

## Verbleibende Grenzen und Restschuld

1. **Kein echter GPU-/Referenzvideo-Lauf.** Zwei GPU-Tests wurden bewusst
   abgewählt; der Golden-Test mit echtem Video war mangels Referenzvideo
   übersprungen.
2. **Kein echter manueller Windows-Smoke-Test.** Der Start von SewerStudio über
   die Windows-Automation erhielt innerhalb der Freigabezeit keine Zustimmung.
   Es wurde keine App gestartet und kein laufender Prozess beendet. Ein
   isolierter UI-Kindprozess-Test war ebenfalls übersprungen.
3. **Junction-Tests teilweise nicht ausführbar.** Elf Sicherheitsfälle konnten
   auf diesem Windows-Konto keine benötigten Junctions/Symlinks anlegen. Ein
   weiterer Test benötigt das externe VSA-KEK-Archiv.
4. **Ein-Knopf-Dateien noch nicht vollständig transaktional.** Direkte Archiv-
   und Medienkopien können bei einem sehr späten Projektkonflikt nicht automatisch
   zurückgenommen werden. Die Projektdaten selbst sind gegen die Übernahme in das
   falsche Projekt geschützt.
5. **23 Buildwarnungen in drei Hilfswerkzeugen.** Betroffen sind
   `MdbSchemaReaderApp` (Nullbarkeit/Windows-Plattform),
   `IbakPdfAnalyzer` (kleine Analyse-/Leistungswarnungen) und
   `CadasterDbReader` (ungenutzte lokale Funktionen). Das Hauptprogramm und seine
   Kernbibliotheken bauen ohne Warnung; die Tool-Warnungen bleiben trotzdem
   bereinigungswürdig.
6. **Kein serverseitiges CI-Gate.** Es gibt keinen Workflow unter
   `.github/workflows`. Der versionierte Pre-Push-Hook prüft Infrastructure,
   Pipeline und UI, aber nicht den vollständigen Build, ProjectModernizer,
   Sidecar oder QGIS.
7. **Architekturschuld bleibt.** Grosse ältere UI-Klassen und handverdrahtete
   Zusammensetzung sind weiterhin ein Wartbarkeitsrisiko. Sie sollten bei
   fachlichen Änderungen schrittweise verkleinert werden, nicht per Big Bang.

## Freigabeempfehlung

Der Stand ist ein belastbarer **Release-Kandidat**, aber noch keine endgültige
Produktionsfreigabe. Vor einer Freigabe an Kunden sind mindestens ein echter
Windows-Start-/Klicktest, ein Referenzvideo-Lauf auf der vorgesehenen GPU und ein
Test mit einer anonymisierten Kopie eines realen Projekts sinnvoll. Kundenoriginale
sollen dabei unverändert bleiben.

## Nachtrag vom 26. Juli 2026 (nach dem geprüften Commit)

Zwei Punkte der Restschuld wurden nach dem Audit-Commit `1d5989d3` geschlossen:

- **Punkt 5 (Buildwarnungen):** Die 23 Warnungen in `MdbSchemaReaderApp`,
  `IbakPdfAnalyzer` und `CadasterDbReader` sind bereinigt (Nullbarkeit,
  `[SupportedOSPlatform("windows")]`, LINQ-Vereinfachungen, ungenutzte lokale
  Delegat-Funktionen entfernt). Anschliessender Release-Build der Gesamtlösung:
  0 Fehler, 0 Warnungen. Verhalten unverändert, kein Testprojekt referenziert
  die drei Werkzeuge.
- **Punkt 6 (CI-Gate):** `.github/workflows/ci.yml` prüft jetzt serverseitig den
  vollständigen Release-Weg: Build der Gesamtlösung, alle vier
  .NET-Testprojekte, Sidecar-Tests (Marker `not gpu`, CPU-Torch) und
  QGIS-Brückentests.

Offen bleiben die Punkte 1–4 (Hardware, manuelle Freigaben, Rechte) und Punkt 7
(Architekturschuld, schrittweiser Abbau).
