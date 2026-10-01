# SchachtPro-Archive in SewerStudio importieren

Stand: 19.09.2026.

Der vorhandene SchachtPro-Import liest jetzt auch aktuelle `.spro`-Archive
mit Format 3 und Datenbankschema 23. Alte Archive der Formate 1 und 2 bleiben lesbar.
Der bestehende Importbefehl genügt; eine Änderung in SchachtPro ist dafür nicht nötig.

Der Import übernimmt die bereits unterstützten Schachtdaten, Anschlüsse, Zustände,
LV95-Koordinaten und Fotos. Fotos werden im normalen Importlauf über die geschützte
Dateiablage ins Projekt kopiert. Ein direkter Dienstaufruf ohne Dateiablage kopiert
weiterhin keine Fotos und meldet dies ausdrücklich.

## Was korrigiert wurde

Der alte Leser erlaubte höchstens Format 1 und Schema 21. Die neue Datei
`integrity.json` wurde schon beim Öffnen als unerlaubter Archivinhalt abgewiesen.
Nur eine höhere Versionsnummer hätte deshalb nicht genügt.

Der neue Prüfschritt kontrolliert vor jeder Datenübernahme:

- Ab Format 2 muss `integrity.json` vorhanden sein. In Format 1 bleibt sie optional;
  ist sie vorhanden, wird sie ebenfalls geprüft.
- Jeder Dateieintrag muss genau einmal im Nachweis stehen, einschliesslich Manifest,
  Projektdateien, Fotos und Logos. SHA-256 muss mit den tatsächlichen Bytes übereinstimmen.
- Fehlende, zusätzliche oder veränderte Dateien stoppen den Import. Mehrdeutige Pfade,
  doppelte ZIP-Einträge und ungültige Prüfsummen werden abgelehnt.
- Die bisherigen Archivgrössen- und Pfadgrenzen bleiben bestehen. Der Nachweis ist
  auf 5 MB begrenzt. Die Dateiüberprüfung lässt sich abbrechen.

Bei einem solchen Archivfehler werden keine Schächte oder Fotos übernommen.
Die Quelldatei bleibt unverändert. Inhaltlich fehlerhafte einzelne Protokolle in einem
intakten Archiv werden weiterhin einzeln gemeldet; die anderen werden bearbeitet.

## Anschlussfoto und Grenzen

Das neuere separate Anschlussfoto wird ebenfalls übernommen, auch ohne normale
Fotoliste. Es erhält einen eigenen Namen mit `_connection`, damit beide Fotoarten
nebeneinander erhalten bleiben.

Übernommen werden die Originalbilddateien. Die SchachtPro-Ausrichtung und die
Überlagerung mit der Schachtgrafik werden nicht nachgebaut. Der Importbericht nennt
dies beim Anschlussfoto. Auch die bisherige Behandlung normaler Foto-Bearbeitungen
und von Projektlogos wurde hier nicht erweitert.

Der Abgleich erfolgte am aktuellen Quellcode in `C:\SchachtPro_5`, insbesondere
`ProjectArchive.kt`, `ProjectExporter.kt`, `ProjectImporter.kt` und `AppDatabase.kt`.
Die Tests verwenden künstliche ZIP-Archive nach diesem Vertrag. Ein frischer Export
von einem echten Handy wurde nicht zusätzlich erprobt.

Verschlüsselte Archive werden weiterhin nicht entschlüsselt. Unverschlüsselt
exportieren, um diesen Importweg zu verwenden. Der QR-Leser für `SPQR1` und Änderungen
am PDF-Textleser gehören nicht zu dieser Reparatur. Es wurden keine Pakete installiert.

## Prüfung

- Vor der Korrektur: 19 von 20 neuen Fällen fehlgeschlagen; der alte Format-1-Fall bestand.
- Danach: alle 79 gezielten SchachtPro-Importtests bestanden, keine Überspringungen.
  Darunter 27 neue Fälle; beschädigte Archive lassen Projekt und Fotos unverändert.
- Vollständiger Release-Build erfolgreich: 0 Fehler. Die vorhandene Warnung
  `CS8604` in `VsaFotoAblageTests.cs:81` bleibt bestehen; keine neue Warnung.

| Vollständige Testgruppe | Bestanden | Fehler | Übersprungen |
| --- | ---: | ---: | ---: |
| Infrastruktur | 6.829 | 0 | 6 |
| KI-Verarbeitung | 2.744 | 0 | 3 |
| Bedienoberfläche | 7.339 | 0 | 30 |
| Projektumstellung | 62 | 0 | 0 |
| **Gesamt** | **16.974** | **0** | **39** |

Die 39 bestehenden Ausnahmen betreffen externe Abnahmefälle und Einstiegspunkte
isolierter UI-Kindprozesse. Keine neue Überspringregel; alle 27 neuen Importfälle
wurden ausgeführt. Die gezielten Vorläufe werden in der Gesamtsumme nicht doppelt gezählt.
Ausgeführt: `dotnet build AuswertungPro.sln -c Release --no-restore`, danach
`dotnet test <Testprojekt> -c Release --no-build --no-restore` für alle vier Gruppen.
Der Architektur-Skill wurde aktualisiert und erfolgreich validiert; `git diff --check`
ist sauber. Grundlage des Reparaturpakets: Commit `f4d09df77`.

Nachweise: `C:\Users\Besitzer\Documents\SewerStudio-Spro-Import-2026-09-19\Pruefnachweise`.
Keine Kundenoriginale geändert, keine Änderungen an SchachtPro, kein Commit oder Push.
