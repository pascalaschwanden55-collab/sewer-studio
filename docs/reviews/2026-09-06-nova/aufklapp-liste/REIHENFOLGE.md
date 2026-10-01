# Reihenfolge einfacher ändern

Haltungen und Schächte lassen sich in der Aufklappliste direkt an ihrer „Nr.“
ziehen. Die obere Hälfte einer Zielzeile bedeutet davor, die untere Hälfte
danach. Eine blaue Linie zeigt die Einfügestelle. Am oberen und unteren
Listenrand scrollt die Liste automatisch weiter. Freie Fläche unter der letzten
Zeile bedeutet ans Ende. Escape bricht das Ziehen ohne Änderung ab.

„Reihenfolge ändern“ öffnet zusätzlich die direkte Positionsangabe und die
Knöpfe „An den Anfang“ und „Ans Ende“. „Fertig“ schliesst diese Bedienleiste.
Eine Änderung gilt sofort, genau wie bisher bei „Nach oben“ und „Nach unten“.
Die laufenden Nummern und der bisherige Speicherweg werden dabei nachgeführt.

Bei aktiver Suche, Filterung oder Sortierung ist dieser Weg gesperrt; ein
Hinweis erklärt das. Ein zwischen Ziehbeginn und Ablegen geänderter Bestand
verwirft das alte Ziel. Fremde Dateien und Zeilen anderer Listen werden nicht
angenommen. Eingabefelder und Pfeile bleiben von der Ziehgeste getrennt.

`ListenReihenfolgeController` und `ListenReihenfolgeLeiste` sind gemeinsame
UI-Bausteine beider Listen. Die jeweiligen Aufklapplisten-Controller verbinden
sie mit `Records`, den bestehenden Bearbeitungssperren und `MoveToPosition`.
Es gibt keinen zusätzlichen Datenschreiber und kein neues Speicherformat.

`ListenReihenfolgeTests` prüfen die Zielberechnung. Der isolierte WPF-Test
`ListenReihenfolgeIsolatedTests` prüft beide Listen, Abbruch, Einfügen,
Positionsfeld samt Enter, Anfang/Ende, Nummerierung, Auswahl, Sperren und
Bestandsänderungen. Er verwendet den echten UI-Controller und die vorhandenen
Verschiebedienste. Die Windows-Ziehschleife mit echter gedrückter Maustaste wird
dadurch nicht vollständig nachgestellt.

Abschlussprüfung vom 09.09.2026: Release-Alltagsbuild ohne Fehler und Warnungen;
76 gezielte Tests bestanden. Die drei im Elternprozess ausgelassenen
WPF-Szenarien laufen über ihre jeweiligen Elternprüfungen in Kindprozessen.
Der vollständige UI-Lauf bestand 6916 Tests und meldete drei Fehler: die
inzwischen korrigierte feste Pfeil-Spaltenposition sowie den Zeitablauf im
Nachschlagmenü und die Grössengrenze des unveränderten Player-Codes. Die
Pfeilprüfung ist im abschliessenden gezielten Lauf bestanden; die beiden
anderen Befunde bleiben ausserhalb dieses Auftrags offen.
