# Umsetzungsplan zum Zielniveau 9/10

Stand: 06.09.2026. Paket 1 ist umgesetzt und gezielt geprüft: [Ergebnis](PAKET-1-ERGEBNIS.md). Pakete 2–5 bleiben offen. Aufwände sind grobe Schätzungen für eine mit dem Projekt vertraute Person. Trainingslaufzeiten und externe Termine kommen hinzu.

Die Sicherheitskorrekturen kommen zuerst. Produktcode wurde während des Audits nicht verändert. Für neue Pakete oder einen grossen Umbau ist gemäss AGENTS.md vor der Umsetzung Rücksprache erforderlich. Kleine gezielte Korrekturen brauchen keine neue Architektur.

## Paket 1 – Originale und Projektdateien schützen

Aufwand: 2–4 Arbeitstage. Abhängigkeit: Kann zuerst beginnen. Zugehörige Befunde: A01, A02, A03.

1. Fehlerfälle als kleine Verhaltenstests festhalten.

2. Eigentums- und Verknüpfungsprüfung vor Schachtänderungen ergänzen.

3. Ordner, Dateinamen und gespeicherte Verweise gemeinsam planen und zurückrollen.

4. Projektstruktur und Sicherungen vor Annahme prüfen.

**Fertig, wenn:** Alle drei Gegenproben bestehen mit korrektem Ergebnis. Originalhashes sind gleich. Öffnen, Speichern und Wiederherstellen gültiger Altprojekte funktionieren.

## Paket 2 – Den vollständigen Prüfweg wieder grün machen

Aufwand: 2–4 Arbeitstage. Abhängigkeit: Nach Paket 1; Ursachenanalyse kann früher beginnen. Zugehörige Befunde: A04, A05, A06, A10.

1. Neue Logik aus HoldingFolderDistributor auslagern, Fassaden behalten.

2. Hängepunkt des Kontextmenütests mit Schrittmarken finden und gezielt korrigieren.

3. Python-Namenskonflikt beseitigen und die fehlenden Tests in CI aufnehmen.

4. Die bestehende Abdeckungsgrenze anhand des belegten CI-Werts 46,61 % aus Lauf 33967264002 nachziehen; Produktcode-Messung getrennt planen.

**Fertig, wenn:** Vollständiger Release-Build, vier .NET-Suiten, Sidecar, QGIS, Trainingshilfen und alle Architektur-/Abdeckungstore bestehen. Kein unbegründeter Skip.

## Paket 3 – KI-Qualität und Modellumgebung nachweisen

Aufwand: 3–6 Arbeitstage plus Mess- und Trainingszeit. Abhängigkeit: Paket 1 und 2; geeigneter Abnahmedatensatz erforderlich. Zugehörige Befunde: A08, A09.

1. Mit dem Fachanwender Grenzwerte je Schadenklasse und für Fehlalarme festlegen.

2. Trainings-, Entwicklungs- und Abnahmehaltungen sauber trennen.

3. Detektor, Klassifikator, DINO, SAM, Meteranzeige und Gesamtablauf getrennt messen.

4. Sicherheitsausnahmen überprüfen und eine kompatible Aktualisierung getrennt erproben.

5. Nur gemessene Modelle mit Hash und Bericht freigeben; Rückfall verständlich anzeigen.

**Fertig, wenn:** Abnahmebericht mit vorher festgelegten Grenzwerten liegt vor. Referenzvideos werden richtig ausgewertet. Modell- und Klassenstände sind eindeutig. Keine offene unbegründete Sicherheitsausnahme.

## Paket 4 – Offene Arbeitsabläufe schliessen

Aufwand: 3–6 Arbeitstage; GEONIS-Termin zusätzlich. Abhängigkeit: Stabile Export- und Trainingsverträge aus Paket 2/3. Zugehörige Befunde: A07, A11.

1. Vier Agentenschritte mit bestehenden Diensten verbinden oder den unfertigen Agenten aus dem normalen Ablauf herausnehmen.

2. GEONIS-Rückabgleich mit Ausgangswerten, Konfliktvorschau und Änderungsplan entwerfen.

3. Rückweg gemeinsam in einer getrennten Ziel-Testdatenbank abnehmen.

4. Vollständigen Import mit PDF, XTF und Video auf Kopien repräsentativer Lieferantenprojekte ausführen.

**Fertig, wenn:** Jeder angebotene Arbeitsablauf hat einen erfolgreichen vollständigen Test. Offene Produktteile sind klar bezeichnet. Kein stiller Konflikt beim Rückabgleich.

## Paket 5 – Bedienung, Leistung und Auslieferung abnehmen

Aufwand: 2–4 Arbeitstage. Abhängigkeit: Nach den funktionsrelevanten Paketen. Zugehörige Befunde: Gesamtabnahme.

1. Alle Navigationsseiten und wichtigen Dialoge mit einem Testprojekt bedienen.

2. Neues Projekt → Import → Korrektur → Videoauswertung → Bericht → Export → Sicherung vollständig durchspielen.

3. Grosse Projekte auf Laufzeit, Speicherbedarf, Abbruch und Fortschrittsanzeige prüfen.

4. Wiederherstellung und Programmstart auf einem getrennten Windows-Profil oder Testrechner prüfen.

5. Anleitung und Architekturkarte mit dem fertigen Code abgleichen.

**Fertig, wenn:** Abnahmeprotokoll ohne offene kritische Fehler. Alle Kernausgaben sind fachlich und optisch geprüft. Ein neuer Rechner kann aus der Sicherung wieder arbeitsfähig werden.

## Verbindliche Abnahme vor 9/10

- Kein offener Fehler, der Originale verändert, Daten unbemerkt verliert oder Wiederherstellung falsch bestätigt.

- Alle automatischen Qualitätsprüfungen grün; Ausnahmen sind einzeln begründet.

- Alle kritischen Abläufe mit künstlichen Fehlerfällen und repräsentativen Projektkopien durchgespielt.

- KI erreicht vorher festgelegte fachliche Grenzwerte auf unabhängigen Daten.

- Oberfläche, Ausgabe, Leistung und Wiederherstellung auf Zielsystemen nachgewiesen.

- Dokumentation, Klassenkarte, Modellfreigabe und Programmstand passen zusammen.

## Technische Verbesserungen ohne Grossumbau

- **Kleinere Verantwortungsbereiche:** AnalyzeAsync umfasst 836 Zeilen; Import umfasst 536; SaveCoreAsync umfasst 559. Neue Änderungen in kleine Dienste legen und schrittweise mit Verhaltenstests entflechten.

- **Fehler sichtbar machen:** Stille Fehler beim Entfernen abgelehnter Trainingsbeispiele aus der Wissensdatenbank protokollieren. Erst prüfen, ob ein erneuter Versuch nötig ist. Ein falscher KI-Vorschlag durch diesen Pfad wurde nicht reproduziert.

- **Fortschritt und Abbruch:** Grosse Kopier- und Exportvorgänge mit messbarem Fortschritt und Abbruch zwischen Dateien prüfen. Keine geschätzte Restzeit als Gewissheit anzeigen.

- **Frühe Grössenbegrenzung:** Die globale Sidecar-Grenze beträgt 4 GiB. Für einzelne Inferenzbilder eine kleinere Grenze vor JSON-Aufbau vorsehen. Der vorhandene Grössenschutz ist umgesetzt; dies ist eine weitere Verbesserung.

- **Eine erkennbare Arbeitskopie:** In der Oberfläche klar anzeigen, ob eine Datei Kundenoriginal, Projektkopie oder erzeugte Ausgabe ist. Schreibende Befehle sollen diese Einordnung verwenden.

- **Messwerte statt Gesamtnote:** Zuverlässigkeit, Datenverlustschutz, Erkennungsqualität, Bedienbarkeit und Leistung getrennt abnehmen. Eine pauschale 9/10-Zahl allein ist kein Abnahmekriterium.
