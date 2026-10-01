# Wartbarkeitsanalyse SewerStudio – 27.09.2026

- [Ausführlicher Bericht](BERICHT.md): Bewertung, Messwerte, 14 priorisierte Befunde und Fundstellen.
- [Umsetzungsplan](UMSETZUNGSPLAN.md): 11 begrenzte Pakete mit Reihenfolge, Aufwand, Tests und Abnahme.
- [Prüfnachweise](nachweise/README.md): Dateibestand, Methoden, Prüfsummen und Testergebnisse.

**Ausgangsstand vom 27.09.:** Release-Build erfolgreich. Bei 17.914 .NET-Testfällen: 17.871 bestanden, 1 fehlgeschlagen, 42 übersprungen. Der Fehler betraf zwei Dateien oberhalb der vorhandenen 1.000-Zeilen-Grenze. Zusätzlich bestanden 66 gezielte Python-Tests und 9 Untertests.

**Stand nach AP01–AP04 und AP07 am 28.09.:** Vollständiger Release-Build mit 0 Warnungen und 0 Fehlern. Alle vier .NET-Testprojekte grün: 18.057 bestanden, 43 planmäßig übersprungen, 0 fehlgeschlagen. AP07 schützt nun auch die zwei bisher offenen Videosuchfälle und reicht Abbrüche aus der KINS- und SIA405-Anreicherung weiter. Die einzelnen Ergebnisse und Restgrenzen stehen in den [Paketprotokollen](AP07-PROTOKOLL.md) und im [Umsetzungsplan](UMSETZUNGSPLAN.md).

Für den ursprünglichen Bericht vom 27.09. wurden Produktcode und gespeicherte
Datenformate nicht verändert. Die späteren Umsetzungen sind in den
Paketprotokollen dokumentiert.
