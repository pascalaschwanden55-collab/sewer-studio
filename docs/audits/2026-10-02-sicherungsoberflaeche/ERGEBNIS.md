# Sicherungsoberfläche und Protokollschutz – 02.10.2026

## Umfang

Ein kleines Lieferpaket mit Ausgangspunkt master (`ea49eb3b9`). Die bereits im älteren Arbeitsstand
vorhandene Zielwahlkorrektur wurde gezielt übernommen und um Wurzel-/Abbruchfälle
ergänzt. Die Hauptkopie mit den übrigen Wartbarkeitswellen bleibt unangetastet.

- Eine direkt ausgewählte Sicherung wird aktualisiert. Es entsteht kein zweiter
  gleichnamiger Sicherungsordner darunter.
- Laufwerks- und Netzwerkfreigabewurzeln bleiben gültige Elternziele.
- Die Bestätigung erklärt Gesamtbestand und zusätzlich benötigten Platz getrennt.
- Die unabhängige Prüfung fand einen vorhandenen Protokollfehler: Start- und
  Fehlermeldungen konnten vor der Sicherungspfadprüfung durch eine Verknüpfung
  eine fremde Protokolldatei verändern. Der bestehende Pfadwächter schützt jetzt
  jeden solchen Schreibzugriff. Ein blockiertes Protokoll ersetzt keine ursprüngliche Fehlermeldung.

Die Sicherungspolitik bleibt auf den aktuellen Stand begrenzt (`MaxStaende = 0`).
Der bisher absolute Hinweis zur Entfernung alter Kopien wurde präzisiert: Bei
blockierter Löschung können ältere Kopien trotz erfolgreichem Lauf bleiben;
der Dienst meldet dies bereits als Warnung. Das Löschverhalten wurde nicht geändert.
Vorübergehende Vorherkopien dienen dem Rücksetzen des laufenden Vorgangs. Kundenoriginale,
echte Sicherungen, Pakete, öffentliche Schnittstellen und Datenformate wurden nicht geändert.

## Prüfung vor der Korrektur

- Unveränderter Ausgang: Release-Build ohne Warnungen/Fehler; 17 bestehende UI-Fälle bestanden.
- Neue Ziel-/Textprüfungen vor der Produktionsänderung: 11 erwartete Fehler, 21 bestanden.
  Die Fehler belegen doppelte Ziele, fehlenden Markerzugriff am tatsächlichen Ziel,
  ungeprüfte ungültige Pfade und den bisher fehlenden Klartext.
- Protokollschutz vor der Produktionsänderung: alle fünf neuen Schutzfälle rot;
  fünf vorhandene Protokoll-/Wächterfälle bestanden. Ausschließlich eigene temporäre
  Dateien und Verknüpfungen wurden verwendet. Keine Schutzfälle übersprungen.

## Erweiterte Schutzfälle

20 neue UI-Fälle schützen Direktwahl, Gross-/Kleinschreibung, abschliessende Separatoren,
Laufwerks- und UNC-Wurzeln, ungültige Pfade, vorhandene Einstellungen, Markerfehler
und vorab angeforderten Abbruch. Ein kleiner echter Sicherungslauf mit synthetischen
Dateien muss eine vorhandene unveränderte Datei weiterverwenden: null Kopien, eine unverändert.

Fünf Infrastrukturregressionen schützen verknüpfte Ziele/Vorfahren sowohl mit vorhandenem
als auch mit fehlendem Fremdprotokoll und eine verknüpfte Logdatei bei Start/Quellfehler.
Die normalen Protokollfälle bleiben erhalten. Der Junction-Zählwächter steigt von 118 auf 123.

## Endstand vor dem Commit

- Unabhängige Quellprüfung freigegeben. Der dabei gefundene Protokollfehler und
  der ungenaue Hinweis zur Entfernung alter Kopien wurden korrigiert.
- Vollständiger Release-Build: null Warnungen und null Fehler.
- Gezielte Prüfungen vor der letzten Textpräzisierung: 45 Infrastruktur- und
  37 UI-Fälle bestanden. Der vollständige Endlauf enthält auch den präzisierten Texttest.

| Vollständiger Release-Testbereich | Bestanden | Übersprungen | Fehler |
| --- | ---: | ---: | ---: |
| Infrastruktur | 8.203 | 6 | 0 |
| Pipeline | 3.048 | 3 | 0 |
| Oberfläche | 7.914 | 48 | 0 |
| ProjectModernizer | 62 | 0 | 0 |
| **Summe** | **19.227** | **57** | **0** |

Die 57 vorhandenen übersprungenen Fälle betreffen optionale Live-/Kindprozessfälle;
keiner der fünf neuen Verknüpfungsschutzfälle wurde übersprungen. Die Prüfsummen
der acht Quell-/Testdateien blieben während des endgültigen Prüfwegs unverändert.
`git diff --check` ohne Befund. Die persönliche Architekturkarte ist abgeglichen
und mit dem vorhandenen Skill-Validator geprüft.

Während der Prüfung wurde `master` um den getrennten Schacht-Konfliktschutz
(`09ca20450`) erweitert. Keine seiner geänderten Dateien überschneidet sich mit
diesem Paket. GitHub prüft den PR zusätzlich; lokale Ergebnisse ersetzen diese CI nicht.

## Grenzen

Dies behebt keine fehlenden verknüpften Originaldateien in einer vorhandenen Sicherung.
UNC-Pfadabbildungen werden mit kontrollierten Dienstfakes geprüft; kein echter Netzlauf.
Die verwaltete Pfadprüfung verhindert keinen gleichzeitigen Austausch durch einen anderen
Prozess zwischen Prüfung und Dateizugriff.
