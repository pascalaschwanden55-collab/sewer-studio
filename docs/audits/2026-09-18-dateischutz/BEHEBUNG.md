# Erstes Reparaturpaket: Originalschutz und Sicherungen

Stand: 18.09.2026. Bezieht sich auf die Befunde 01–03 des Gesamtaudits.
Grundlage ist Commit `4b85dfa319c9b5c2d4f55056c1cc7d1eb72e0921` einschliesslich
der bereits vorhandenen Änderungen zur Dichtheits-/PDF-Verteilung.

Die drei Fehler sind korrigiert. Kundenoriginale und echte Sicherungsbestände
wurden für die Prüfung nicht verändert. Die neu hinzugefügten Verhaltenstests
verwenden künstliche Dateien in eigenen temporären Ordnern.

## 01: Umbenennen darf keine fremden Originale verändern

Vorher konnte eine Ordnerverknüpfung aus dem Projekt herausführen. Das Umbenennen
einer Haltung änderte dann Dateinamen im fremden Zielordner.

`HoldingRenamePathGuard` nutzt jetzt die bestehende gemeinsame Schreibgrenze.
Haltungs- und Fotoordner werden vor der ersten Änderung vollständig geprüft.
Das schliesst Wurzeln, Elternordner, Unterordner und Dateiverknüpfungen ein.
Jeder anschliessende Verschiebe- und Rücknahmeschritt prüft Quelle und Ziel erneut.
Die nachgelagerte PDF-Korrektur prüft ihre Schreibpfade nochmals und meldet
unsichere Dateien sichtbar.

Nachweis: zwölf neue Fälle in `HoldingRenamePathSafetyTests`, darunter sieben
echte Windows-Ordnerverknüpfungen, Dateiverknüpfungen, defekte Zielverknüpfungen,
normaler Erfolg und Rücknahme bei gesperrtem Foto. Zwei zusätzliche UI-Fälle
prüfen Verknüpfungen vor der PDF-Korrektur. Der Verknüpfungswächter erwartet
nun 92 statt 85 vorhandene Tests; es wurde keine Überspringregel ergänzt.

## 02: Neue Sicherungsquellen dürfen bestehende Kopien nicht verdrängen

Vorher bestimmte die Reihenfolge der Quellen ihren Zielordner. Eine neue Quelle
konnte dadurch die letzte Kopie einer nicht erreichbaren Altquelle verdrängen.

`BackupProjectTargetMapping` liest die bisherige Zuordnung aus `manifest.json`.
Dies geschieht unter der bestehenden Laufsperre und nach der Wiederherstellung
eines abgebrochenen Laufs. `BackupPlanBuilder` bindet bekannte Quellen weiterhin
an ihre bisherigen Ziele. Neue Quellen erhalten freie Namen.
Eine weiterhin konfigurierte Kindquelle behält ihre Kopie auch neben einer
neu aufgenommenen Elternquelle. Unklare Zuordnungen stoppen den Lauf vor Kopie
und Bereinigung.

Eine unabhängige Gegenprüfung fand zwei Randfälle. Beide wurden zunächst mit
fehlschlagenden Tests belegt und danach korrigiert:

- Geschützte Altvideos einer entfernten Quelle behalten ihren Herkunftsnachweis.
  Die entfernte Quelle wird dadurch nicht erneut kopiert.
- Ein abgebrochener Erstlauf darf leere Ordner hinterlassen. Nach vollständiger
  Sicherheitsprüfung erlauben solche dateileeren Ordner einen erneuten Lauf.
  Unbekannte Dateien und Verknüpfungen bleiben gesperrt.

Nachweis: elf neue Fälle in `BackupProjectIdentityTests`. Dazu gehören alte
Manifestdaten, gleiche Ordnernamen, neue Elternquellen, sechs beschädigte
Zuordnungen sowie mehrere echte Sicherungs- und Versionsläufe.
Die alte Kopie bleibt auch nach fünf Folgeläufen im Spiegel erhalten.

## 03: Eine geprüfte Kopie muss einen stabilen Dateistand enthalten

Vorher durfte eine Quelldatei während des Kopierens verändert werden.
Der Inhaltsvergleich bestätigte dann auch eine Mischung aus alten und neuen Bytes.

`DirectoryMirror` hält normale Quelldateien während Kopie und Inhaltsprüfung
gegen Schreiben und Austausch gesperrt. Länge und Änderungszeit werden vom selben
offenen Dateizugriff gelesen. Die gemeldete Bytezahl wird tatsächlich mitgezählt.
Der Vergleich vermeintlich unveränderter Dateien verwendet denselben Schutz.
Bereits offene Schreiber führen zu einer Dateiwarnung; eine vorhandene alte Kopie
bleibt bestehen. Andere Dateien werden weiterhin gesichert.

Ein zusätzlich nachgewiesener Randfall betraf die Platzschätzung: Ist ein
Inhaltsvergleich nicht lesbar, wird konservativ Kopierbedarf angenommen.
Der anschliessende Kopierweg meldet die gesperrte Datei einzeln.
SQLite behält seinen bestehenden eigenen Online-Schnappschuss.

Nachweis: sechs neue Fälle in `DirectoryMirrorStableSourceTests`.
Sie prüfen offene Schreiber, Schreib- und Austauschversuche während der Prüfung,
gezählten Fortschritt, Abbruch und einen vollständigen Sicherungslauf.

## Prüfung und Nachweise

Die Fehler wurden vor den jeweiligen Korrekturen mit gezielten Tests nachgewiesen.
Die abschliessende gezielte Infrastrukturprüfung bestand alle 231 Fälle.
Die acht gezielten UI-Tests zur Haltungsumbenennung waren ebenfalls erfolgreich.
Eine unabhängige Code- und Dokumentationsprüfung wurde abgeschlossen.

Der vollständige Release-Build war erfolgreich: keine Fehler, eine bereits
vorhandene Warnung `CS8604` in `VsaFotoAblageTests.cs:81`.

Alle vier vollständigen .NET-Testgruppen waren erfolgreich:

| Testgruppe | Bestanden | Fehler | Übersprungen |
| --- | ---: | ---: | ---: |
| Infrastruktur | 6.773 | 0 | 6 |
| KI-Verarbeitung | 2.658 | 0 | 3 |
| Bedienoberfläche | 7.304 | 0 | 28 |
| Projektumstellung | 62 | 0 | 0 |
| **Gesamt** | **16.797** | **0** | **37** |

Die 37 Ausnahmen bestehen bereits: externe Abnahmefälle und im Hauptlauf
ausgelassene Einstiegspunkte für isolierte UI-Kindprozesse. Die neuen 31 Testfälle
dieses Pakets wurden ausgeführt. Die Gesamtzahlen stammen aus den vier TRX-Dateien;
gezielte Vorläufe werden darin nicht nochmals mitgezählt.

Ausgeführter vollständiger Prüfweg:

```powershell
dotnet build AuswertungPro.sln -c Release --no-restore
dotnet test tests\AuswertungPro.Next.Infrastructure.Tests\AuswertungPro.Next.Infrastructure.Tests.csproj -c Release --no-build --no-restore
dotnet test tests\AuswertungPro.Next.Pipeline.Tests\AuswertungPro.Next.Pipeline.Tests.csproj -c Release --no-build --no-restore
dotnet test tests\AuswertungPro.Next.UI.Tests\AuswertungPro.Next.UI.Tests.csproj -c Release --no-build --no-restore
dotnet test tests\ProjectModernizer.Tests\ProjectModernizer.Tests.csproj -c Release --no-build --no-restore
```

Protokolle, maschinenlesbare Testergebnisse und Prüfsummen liegen dauerhaft unter:
`C:\Users\Besitzer\Documents\SewerStudio-Audit-2026-09-18\Dateischutz-Behebung\Pruefnachweise`.
Der ursprüngliche Gesamtauditbericht wurde nicht verändert.

## Umfang und verbleibende Grenzen

- Keine neuen Pakete, Dienstregistrierungen oder gespeicherten Datenfelder.
  Die bisherigen öffentlichen Aufrufe bleiben verfügbar.
- Die zwölf bereits bearbeiteten Codedateien anderer Arbeiten sind laut
  SHA-256-Prüfsummen unverändert. `CLAUDE.md` erhielt die neue Beschreibung
  und eine zeitliche Präzisierung der früheren Testzahl. Der Architektur-Skill
  wurde mit dem tatsächlichen Code abgeglichen und validiert.
- Die Pfadprüfung ist nicht atomar mit dem Verschieben. Ein anderer Prozess
  könnte einen Pfad im verbleibenden Zeitfenster austauschen.
  Eine Rücknahme kann selbst scheitern; dies wird gemeldet.
  Ein dauerhaftes Umbenennungsjournal gegen Stromausfall gibt es weiterhin nicht.
- Die Schreibsperre einer Quelldatei dauert die gesamte Kopie und Inhaltsprüfung.
  Bei grossen Dateien oder langsamen Zielen kann dies länger dauern.
  Die Sicherung bildet keinen gemeinsamen Zeitpunkt aller Projektdateien ab.
- Quellidentität ist der normalisierte absolute Pfad. Verschobene Quellen oder
  andere Laufwerksbuchstaben werden nicht automatisch als dieselbe Quelle erkannt.
  Bereits früher verlorene Kopien werden durch diese Korrektur nicht wiederhergestellt.
- Die übrigen 15 Auditbefunde bleiben offen, darunter Videoanalyse und historische
  Medienverweise nach Haltungsumbenennung. Keine manuelle Abnahme der laufenden
  WPF-Anwendung und keine neue GPU-/Sidecar-Prüfung in diesem Reparaturpaket.
- SewerStudio wurde nicht beendet. Es wurde kein Commit erstellt und nichts veröffentlicht.
