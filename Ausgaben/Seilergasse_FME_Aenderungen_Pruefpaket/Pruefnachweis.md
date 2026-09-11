# Was geprüft wurde

Stand: 7. September 2026.

Die Seilergasse-Datei enthält 13 Änderungsaufträge und drei Zusatzwerte.
Sie betrifft eine Haltung und einen Schacht.
Zusammen mit den Hilfsobjekten enthält die XTF 25 Objekte.

| Prüfung | Ergebnis |
|---|---|
| Aufbau und Regeln der Seilergasse-XTF | Bestanden. |
| Eigener Test mit allen vier Bauwerksarten | Bestanden. Die erfundenen Testdaten gehören nicht zur Kundenlieferung. |
| Neue Programmversion erstellen | Erfolgreich, ohne Warnungen oder Fehler. |
| Tests für Datenverarbeitung und Dateiabläufe | 6192 bestanden. Sechs dafür vorgesehene externe oder optionale Tests wurden ausgelassen. |
| Tests für Oberfläche und XTF-Abläufe | 244 bestanden. |
| Nachprüfung: FME-Export immer nur mit Änderungsaufträgen | Fünf Tests bestanden. Vorschau und Schreiben verwenden beide den Änderungsmodus. |
| Interne Architektur-Anleitung | Aktualisiert und geprüft. |

Die Programmtests prüfen beispielsweise:

- Schachtform und Bauwerksart bleiben beim erneuten Einlesen erhalten.
- Vorhandene Handeingaben werden nicht durch Zusatzangaben überschrieben.
- Doppelte Zusatzangaben und falsche Zielkennungen werden nicht übernommen.
- Nicht erlaubte Zusatzfelder werden abgewiesen.
- Vorhandene Standardfelder haben Vorrang.
- Vorhandene fremde Modelldateien werden nicht überschrieben.
- Nur als bearbeitet markierte Felder erhalten einen Änderungsauftrag.

## Woher die Werte stammen

Die Werte stammen aus diesem gespeicherten Projekt:

`D:/Projekte/Seilergasse Test export/Projektdateien/projekt.json`

Einige Schachtkennungen fehlten dort in der benötigten Form.
Für die Prüflieferung wurden sie aus der ursprünglichen Test-XTF ergänzt.
Das geschah nur an der geladenen Kopie. Die gespeicherte Projektdatei wurde nicht geändert.

Auch die ursprüngliche XTF blieb unverändert:

`C:/Users/Besitzer/Documents/Seilergasse Test export_20260904_210310.xtf`

Die Dateiprüfsumme war vor und nach der Arbeit gleich.
Sie lautet:

```text
58270107E3A28F7FB58A1867ECA0C6758D04156442E9F0859CD32E5E582A2E58
```

Das verwendete Verfahren heisst SHA-256. Es prüft, ob der Dateiinhalt gleich geblieben ist.

## Was noch offen ist

Die Kennnummern stammen noch aus dem alten Stand.
Sie sind noch nicht gegen die aktuelle GEONIS-Datenbank bestätigt.

Es wurde keine echte GEONIS-Datenbank verändert.
Der FME-Import muss von Andreas noch eingerichtet und getestet werden.
Auch die Zuordnung zu den GEONIS-Feldern und Auswahlcodes muss er prüfen.

SewerStudio erkennt bisher die Bearbeitungsmarkierung.
Ein Vergleich mit dem gespeicherten Ausgangswert fehlt noch.
Deshalb kann auch 500 löschen und wieder 500 eingeben mitgeliefert werden.
FME muss identische Werte unverändert lassen.

Eine erfolgreiche Übernahme wird SewerStudio noch nicht automatisch bestätigt.
Frühere Änderungen können deshalb erneut geliefert werden.

## Angaben für Andreas zum Datei-Prüfprogramm

Verwendet wurde ilivalidator 1.15.0 mit `--allObjectsAccessible`.
Damit wurden Dateiaufbau, Modellregeln und Verweise geprüft.
Es wurden keine Modellprüfungen abgeschaltet.

Das technische Prüfprotokoll liegt in `INTERLIS_Pruefung.log`.
Dieses Protokoll bestätigt keine Übereinstimmung mit der echten GEONIS-Datenbank.

Die interne Architektur-Anleitung wurde mit `quick_validate.py` geprüft.
