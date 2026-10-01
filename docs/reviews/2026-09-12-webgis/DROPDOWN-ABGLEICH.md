# Dropdown-Abgleich vom 12.09.2026

Alle **269 Dropdown-Vorkommen mit ausgezählten Originalwerten** aus
`WEBGIS-LAYOUT-KOMPLETT.md` sind im aktuellen Katalog enthalten: Codes, Texte,
Elterngruppen, Häufigkeit und Reihenfolge stimmen. 47 Vorkommen haben zusätzlich
eine leere Auswahl. Gleiche Texte mit verschiedenen Codes bleiben getrennt.

Der Katalog umfasst jetzt 661 Felder für 16 Aktenarten, davon 235 Dropdownfelder.
148 Kataloge enthalten zusammen 1924 Einträge. Dabei werden wiederverwendete und
nach Elternwert wiederholte Einträge mitgezählt; dies sind keine 1924 verschiedenen
fachlichen Werte.

## Ergänzt und geschützt

- Sanierungsverfahren folgen der Art. Alle 37 Verfahren sind enthalten: 2 für
  Unbekannt, 7 für Erneuerung, 15 für Reparatur, 13 für Renovierung. Zuvor waren nur
  die 13 Renovierungsverfahren vorhanden. Die bisherige Leerwahl bleibt erhalten.
  Die anderen acht Arten haben laut Vorlage keine Verfahrensliste.
- 20 bestehende Deckel-/Sanierungskataloge erhalten ihre dokumentierten Originalcodes.
  Kein alter Text, Eintrag oder gespeicherter Index wurde entfernt. Die zwei
  gleich beschrifteten Fabrikate mit Code 14 und 15 bleiben zwei Einträge.
- Alte gespeicherte Auswahlen ohne Originalcode werden anhand von Position **und**
  Text wiedererkannt. Beim Öffnen werden Kundendaten nicht nachträglich umgeschrieben.
  Ein mehrdeutiger Text wird nicht willkürlich dem ersten Eintrag zugeordnet.
- Haltungspunkte erhalten 15 Felder und ihre fünf Dropdowns, jeweils einschliesslich
  der im laufenden WebGIS bestätigten Leerwahl. Nach dem Katasterabgleich lassen
  sie sich in der Objektauswahl öffnen. Ein erneuter Abgleich ergänzt diese Akten
  auch in bereits abgeglichenen Projekten, ohne doppelte Punkte anzulegen.
- Änderungen an belegten Punktattributen erreichen die ursprüngliche TID in der XTF.
  Widersprechende Handeingaben desselben Punkts sperren die Ausgabe. Die fünf
  Höhengenauigkeiten haben geprüfte Normziele. Lagebestimmung, Lagegenauigkeit und
  Höhenbestimmung haben im gelieferten DSS-Haltungspunkt kein eigenes Zielfeld;
  gesetzte Werte werden im Exportbericht ausdrücklich genannt.
- Witterung enthält alle neun Werte für Untersuchung, Begehung, Deformationsmessung
  und Georadar. Sie bleibt lokal bearbeitbar. Für das allgemeine DSS-Unterhaltsobjekt
  ist keine Witterungszuordnung erfunden worden.
- Materialdetails ohne fachlich geklärte Normzuordnung bleiben auswählbar. Die
  vorhandene persönliche Listenbearbeitung wird weiterhin berücksichtigt.

## Direkte Kontrolle im WebGIS

Am vorhandenen Objekt Haltung 80089-81361 wurde ausschliesslich gelesen.
Die Ortsauswahl unter Administrativ zeigt leer und beim Aufklappen
„Keine Übereinstimmungen gefunden!“. Daraus lässt sich keine vollständige,
dauerhaft leere Ortsliste für alle Projekte ableiten.

Der vorhandene Von-Punkt A78308 wurde geöffnet. Seine fünf Listen enthalten
einschliesslich Leerwahl 6 / 4 / 6 / 4 / 6 Einträge. Anschliessend wurde zur
ursprünglichen Haltung zurückgekehrt; alle Bereiche sind wieder geschlossen.
Es wurde kein Wert gewählt oder gespeichert.

## Grenzen und verbleibende Nachweise

Fünf dokumentierte Dropdown-Vorkommen haben keine ausgezählte Quellliste:
die Ortsauswahl sowie jeweils Art und Status bei den Ereignismasken Reinigung
und Andere. Für Art/Status stehen die belegten gemeinsamen Unterhaltskataloge
bereit; deren maskenspezifischer Direktnachweis ist weiter offen. Die aktuelle
Haltung enthält keine entsprechenden Ereignisse für eine lesende Kontrolle.

Der maschinelle Abgleich prüft Kataloginhalte. Die Tests prüfen zusätzlich, dass
alle Einträge über ihre jeweiligen Felder und Elternwerte erreichbar sind. Das ist
keine Vollabnahme sämtlicher Feldbindungen, dynamischer Objektlisten oder
WebGIS-Funktionen. Dokumentbeziehungen, vollständige Subtypmasken und weitere
Import-/Exportarten bleiben im [Gesamtstand](UMSETZUNGSSTAND.md) aufgeführt.

Nachweis: [vollständiger Abgleich mit Quellzeilen und Prüfsummen](dropdown-abgleich.json).
Das Werkzeug `tools/PruefeWebGisDropdowns.py` liest nur und schreibt neue Berichte;
es leitet aus Ähnlichkeit keine automatischen Änderungen ab.

## Tests

- Vor der Reparatur: 18 der 19 neuen Dropdownprüfungen schlagen an; die fehlenden
  Punktakten werden von drei weiteren Tests erkannt.
- Geprüft werden sämtliche Verfahrensgruppen, alte Auswahlen, gleichnamige Fabrikate,
  Gruppenwechsel, Witterung, Feldsichtbarkeit sowie die Erreichbarkeit aller Einträge.
- Punktprüfungen decken Import, erneuten Abgleich, Handeingabeschutz, Original-TIDs,
  Normausgabe, Anzeige importierter Normtexte und widersprechende Änderungen ab.
- Abschliessender Fachlauf: 649 bestanden, ein vorhandener Live-Test übersprungen.
- Die abschliessenden Oberflächen- und Build-Ergebnisse stehen im Gesamtstand.
