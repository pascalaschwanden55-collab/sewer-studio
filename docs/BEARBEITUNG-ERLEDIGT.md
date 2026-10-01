# Bearbeitung als erledigt markieren

1. In **Haltungen** oder **Schächte** die gewünschte Zeile auswählen.
2. Oben auf **Erledigt** klicken. Neben dem Namen erscheint ein grünes Häkchen.
   In der Tabellenansicht steht das Häkchen am linken Zeilenrand.
3. Erneut auf **Erledigt** klicken, um die Markierung aufzuheben.

Der Knopf erscheint bei markierter Auswahl grün. Ohne Auswahl ist er gesperrt.
Die Markierung bleibt beim Speichern im Projekt erhalten. Die automatische
Speicherung folgt der Programmeinstellung; ist sie ausgeschaltet, **Speichern** klicken.
Alte Projekte beginnen ohne diese Markierung.

**Offen/abgeschlossen gehört weiterhin zur Sanierung.** Die persönliche
Erledigt-Markierung verändert keine Sanierungsangaben und keine KI-Prüfung.
Sie gilt jeweils für die ausgewählte Haltung oder den ausgewählten Schacht.
Spätere Änderungen heben die Markierung nicht automatisch auf.

Die Aufklappliste nutzt wieder die volle Höhe: Eine gespeicherte Höhe des
ausgeblendeten Eingabebereichs erzeugt beim Öffnen keinen leeren Bereich mehr.

Geprüft am 13.09.2026: Release-Build ohne Fehler und Warnungen; 7050 Oberflächentests
bestanden, 26 vorgesehene Überspringungen. Der neue WPF-Test prüft beide Seiten,
beide Ansichten, Hell/Dunkel, Rücknahme, fremde/entfernte Datensätze und Projektwechsel.
Der Leerraumfehler wurde vor der Korrektur mit 270 px nachgestellt.
Sieben gezielte Prüfungen für Speicherung, Projektkopie, Altprojekte und Inhaltssignatur bestanden.

Die vollständige Infrastrukturprüfung hatte 6605 erfolgreiche Tests und sechs
Überspringungen. Zwei maschinengebundene Prüfungen von `SanierungsprotokollEchteQuelleTests`
scheiterten beim Import gescannter Begleitprotokolle. Dieser Importweg wurde hier nicht geändert.
