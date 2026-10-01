# Änderungsliste: SewerStudio-Nova-Optimiert.html

Stand 06.09.2026. Neue Datei; die Originale `SewerStudio-Nova-Komplett.html` und
`SewerStudio-Nova-Vorschau.html` sind unverändert (SHA-256 gegen die Arbeitskopien
verglichen). Die Datei liegt auf dem Desktop und unter `docs/reviews/2026-09-06-nova/optimiert`.

## Was gleich geblieben ist

- Nova-Gestaltung: Leiste links mit den 15 deutschen Navigationspunkten in vier Gruppen,
  Hell · Glas, Dunkel · Cockpit, System, Zustandsfarben Z0 bis Z4 aus den Excel-Vorlagen,
  Haltungen mit Liste links, Übersicht rechts und Eingabefeldern unten in vier Themen.
- Alle 15 Seiten und die drei Fenster Player, VSA-Codierung, Training Studio. Die
  Funktionsliste steht in `FUNKTIONSLISTE.md`.

## Bestätigte Fehler, behoben

| Befund | Änderung |
|---|---|
| N01 Auswahl und Daten | Ein zentraler Beispielbestand mit stabilen Kennungen (14 Haltungen, 8 Schächte, Konflikte, Dossiers). Liste, Übersicht, Eingabefelder, Video und Player folgen der Auswahl. Ungespeicherte Änderungen zeigen Badge, Zeilenmarke und orangen Feldrand; Speichern, Verwerfen und die Rückfrage beim Wechsel (Speichern, Verwerfen, Abbrechen) funktionieren. Speichern schreibt nur in den Browserspeicher; das steht in der Vorschauleiste und in jeder Meldung. |
| N02 Suchen | Feldsuche, „Alle auf/zu" und Spaltenansichten arbeiten je Seite auf ihrem eigenen Bereich. Die Schachtsuche filtert die Schachtliste, die Haltungssuche die Haltungsliste. |
| N04 Zahlen | Kennzahlen, Ring, Legende, Text, Schadensbalken, Kosten und Dossier-Kennzahlen werden aus demselben Datenbestand gerechnet. Prozent immer mit Zähler und Nenner. Getrennt ausgewiesen: fachlich geprüft, KI analysiert und noch nicht geprüft, ohne Analyse. Fehlende Klassen heissen „nicht berechnet". |
| N05 Ablauf | „Nächste Haltung prüfen" wählt die erste KI-analysierte, noch nicht geprüfte Haltung, wechselt auf Haltungen und öffnet den Player. Globale Suche mit Ctrl+K, Pfeiltasten und Enter springt zu Haltung, Schacht oder Strasse. Player → Ereignis erfassen → Übernehmen speichert den Befund und kehrt zum Player mit gleicher Haltung und Videoposition zurück; Abbrechen übernimmt nichts. Aus dem Training Studio kehrt der Katalog mit dem Code ins Studio zurück. |
| N08 Ruhig | „Ruhig" hält die Hintergrund-Engine wirklich an, die Wahl wird gespeichert, die Systemeinstellung „Bewegung reduzieren" wird beachtet. Die Engine pausiert ausserdem bei verdecktem Tab und bei offenem Fenster. |
| N09 Datei | DOCTYPE, UTF-8, `lang="de"`, viewport. Systemschriften Segoe UI und Cascadia Mono, keine externen Ressourcen. Offline per Doppelklick lauffähig, Standardmodus statt BackCompat. |

## Arbeitsfläche

- Haltungen und Schächte: Liste, Übersicht rechts und Eingabefelder unten mit zwei
  Trennlinien (Maus oder Pfeiltasten). Standardhöhe der Eingabefelder rund ein Drittel; die
  Liste behält Platz für mindestens sieben Zeilen. Breiten und Höhen bleiben gespeichert,
  ebenso Spaltenansicht, geöffnete Themen und Zuklappen der Eingabefelder.
- Werkzeugleisten kompakt: eine Hauptaktion (Speichern), wenige direkte Knöpfe, alles Seltene
  unter „Weitere Aktionen". Keine Fachfunktion entfernt.
- Player: Video passt sich der Höhe an und drückt die Seitenspalte nicht heraus. Wiedergabe,
  Zeitposition, Übernehmen und Seitenspalte sind bei 1366 × 768 ohne Scrollen sichtbar.
- Training Studio: dreispaltig, die Codier- und Freigabespalte ist vollständig erreichbar.

## Lesbarkeit, Tastatur, Ehrlichkeit

- Schriftskala 12 / 13 / 14 / 15 / 18 / 22 / 28. Kleinster sichtbarer Text 11 px
  (Tastenkürzel-Kästchen). Tabellenköpfe und Gruppen 12 px statt 10,5 px.
- Kontrast: alle sichtbaren Texte in Hell und Dunkel mindestens 4,5:1; die Z-Marken haben je
  Thema eine eigene Textfarbe. Glas nur noch in Leiste und Kopfzeile; Arbeitsflächen sind
  deckend. Zustände auch ohne Farbe: Text in der Ampel, „nicht berechnet", „nur lesbar",
  „● Ungespeichert", Kennzeichen ◌ an Vorschau-Aktionen.
- Echte Schaltflächen und Eingabefelder überall; Navigation mit Tab und Enter; Zeilen mit
  Pfeiltasten und Enter. Dialoge mit `role`, `aria-modal`, Namen, Fokus im Dialog, Fokusfalle,
  Esc schliesst die oberste Ebene, Fokus kehrt zum Auslöser zurück oder zur gewählten Zeile,
  wenn der Auslöser nicht mehr sichtbar ist. Jedes Symbol hat Text, `aria-label` oder Titel.
- Nicht umgesetzte Aktionen tragen ◌ und erklären beim Klick, was das Programm täte. Kein
  Import, kein Dateizugriff, kein Export, kein Modelltest wird vorgetäuscht. Export nennt Ziel
  und Ergebnis und sagt, dass keine Datei geschrieben wurde. Revidierte XTF und
  GEONIS-Rückabgleich sind ausdrücklich unterschieden.
- Leer-, Lade-, Fehler- und Erfolgszustände: leere Filter, Schattenlauf mit Fortschritt und
  Abbruch (als Simulation benannt), Fehlermeldung im Codierfenster bei Ende vor Start,
  Erfolgsmeldungen als Toast.
- KI-Prozent: „Konfidenz = Modellsicherheit, keine Trefferwahrscheinlichkeit", „Abnahme =
  Trefferquote der Lernstufe auf Prüfclips". KI-Vorschlag, fachliche Bestätigung und Freigabe
  für Training sind drei getrennte Schritte, im Player und im Training Studio.
- Modellnamen und Rechnerauslastung stehen in aufklappbaren Details (Leiste unten links,
  Player „KI-Pipeline"). Der Alltag zeigt „Analyse bereit" und die nächste Aufgabe.

## Was diese Datei nicht ist

- Kein 9/10-Nachweis für SewerStudio. Es ist ein Prototyp mit Beispieldaten. Die Prüftabelle
  belegt die geprüften Punkte im Browser; Windows-Skalierung, Screenreader und echte Daten
  sind nicht geprüft.
- Die Simulationen (Schattenlauf, VSA-Bewertung) verwenden eine vereinfachte Regel
  „höchste Stufe bestimmt die Klasse" und nicht den Regelsatz des Programms.
- Der Einbau in WPF ist ein eigener Schritt (Schriftskala 11/12/13/15/18/22/28, Farb-Tokens,
  FluentIcon, DPI 125 % und 150 % in der echten Anwendung prüfen).
