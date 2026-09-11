# DSS-Neuexport: Prüfstand 11.09.2026

Der vollständige neue Export übernimmt belegte GeoShop-Angaben als DSS-Normobjekte.
Nova-Ressourcen und der getrennte Änderungsmodus bleiben erhalten. Der WebGIS-Plan mit
200 Quellfeldern und 91 Dropdownstellen ist unverändert; keine Werte wurden aus lokalen
Dropdown-Indizes als vermeintliche Normcodes erzeugt.

## Unabhängige Normprüfung

Offizielle lokale Modelle: DSS_2020_1_LV95 und SIA405_Base_Abwasser_1_LV95,
jeweils 18.10.2023, mit Base_LV95 und Units. Modellprüfsummen stehen in
`src/AuswertungPro.Next.Application/Xtf/Dss/ExportSchema.json`.
Prüfer: ilivalidator 1.15.0. Keine abgeschaltete Fach-/Geometrieprüfung.

| Probe | Ergebnis |
| --- | --- |
| Vollständiger synthetischer Verbund mit Deckel, Einstieg, zwei Ereignissen und Firma | 0 Fehler, mit `--allObjectsAccessible` |
| Gegenrichtung mit Kreisbogen | 0 Fehler, mit `--allObjectsAccessible` |
| Alle unterstützten Sachfelder | 0 Fehler, mit `--allObjectsAccessible` |
| Aus GeoShop-Haltung 79929-79969 neu erzeugte XTF | 0 Fehler; externe Organisationsverweise ausgewiesen |
| Aus GeoShop-Schacht 79969 neu erzeugte XTF | 0 Fehler; externe Organisationsverweise ausgewiesen |

**239 von 239 skalaren Attributdefinitionen in 14 Klassen stehen tatsächlich in der
Alle-Felder-Testdatei.** Dazu kommen neun Geometrie-Attributdefinitionen im Vertrag,
also insgesamt 248. Koordinaten und Kreisbögen wurden zusätzlich geprüft. Die Zählung
ist keine Behauptung, dass jeder reale Datensatz alle Felder gefüllt hat.

Der Normprüfer hat während der Entwicklung die zunächst separat geschriebene Firmenbeziehung
abgewiesen. Die finale Fassung schreibt `Ausfuehrende_FirmaRef` direkt am Unterhalt;
die erneute Prüfung ist bestanden. Bauwerks-Ereignis-Beziehungen bleiben eigene Links ohne TID.

## Programmprüfungen

| Prüfung | Ergebnis |
| --- | --- |
| Dev-Release-Build | 0 Fehler, 0 Warnungen |
| Vollständiger Release-Build, separater Ausgabeordner | 0 Fehler, 0 Warnungen |
| Infrastruktur-Gesamtlauf | 6477 bestanden, 6 übersprungen |
| Abschliessende XTF-/GeoShop-/Objektakten-/JSON-Prüfung | 621 bestanden, 1 übersprungen |
| UI-Gesamtlauf | 6953 bestanden, 22 übersprungen; eine Codeprüfung zunächst beanstandet, danach korrigiert |
| Abschliessende UI-/Export-/Objektakten-/Architekturprüfung | 446 bestanden, 1 übersprungen; Codeprüfung jetzt bestanden |
| Pipeline | 2658 bestanden, 3 übersprungen |
| ProjectModernizer | 62 bestanden |
| Katalogabgleich | 200 Quellfelder, 91 Dropdownstellen, 1158 Einträge unverändert |
| Architektur-Skill | mit echtem Code abgeglichen und validiert |

Der zuerst beanstandete vollständig leere Catch-Block wurde durch die ausdrückliche
Übersetzung bekannter Kurztexte mit anschliessender Wertprüfung ersetzt. Fehler werden
nicht als leere Normwerte ausgegeben. Die letzten Prüfungen decken ausserdem beschädigte
Quellwerte, aktuelle Verwaltungsfelder und die doppelte Anzeige des Ereignisdatums ab.

Spezifische Verhaltenstests schützen Originalkennungen, getrennte Bemerkungen, Koten,
beabsichtigtes Leeren, Pflichtfelder, Wertebereiche/Präzision, fehlende Referenzen,
widersprüchliche Belege, gemeinsame und wieder importierte eigene Profile, mehrere Deckel/Ereignisse, Koordinaten,
Gegenrichtung mit Kreisbogen sowie das Nichtüberschreiben vorhandener Dateien.

## Quellschutz und Grenzen

Die vorhandene 456586997-Byte-GeoShop-Datei wurde nur gelesen. SHA256 unverändert:
`ed6bfdc290f66e8bdefd5fcc6cff59013337adcd763df754d1eadec41da34830`.
Die automatisierten Verhaltenstests verwenden synthetische Daten. Originaldateien
wurden weder geändert noch ins WebGIS zurückgespielt. Laufendes SewerStudio und MCP
wurden nicht beendet; der vollständige Build nutzte einen separaten Ausgabeordner.

Normprüfung ist kein Nachweis der tatsächlichen GEONIS-/FME-Rückübernahme. Externe
Organisations-TIDs benötigen vorhandene Organisationsstammdaten im Zielkataster.
WebGIS-interne Felder ohne belegtes DSS-Ziel bleiben im Objektakten-JSON und erscheinen
als Hinweis. Fehlende Pflichtnamen aus GeoShop werden ausdrücklich als technische
Original-TID-Bezeichnung ausgewiesen. Andere Objektklassen ausserhalb des freigegebenen
Vertrags werden nicht still übernommen.

## Nachweise und Wiederholung

Logs, synthetische XTF-Dateien, Attributabdeckung und Prüfsummen liegen unter
[nachweise/dss](nachweise/dss/). `Pruefprobe.cs` archiviert den ausgeführten lokalen
Prüfaufbau und seine Pfade; die synthetischen Grunddaten kommen aus `XtfDssVerbundTests`.
`tools/ErzeugeDssExportSchema.py` erzeugt den Feldvertrag reproduzierbar.

Normprüfung einer archivierten synthetischen Datei:

```powershell
java -jar C:/Users/Besitzer/Downloads/ilivalidator-1.15.0/ilivalidator-1.15.0.jar --allObjectsAccessible --modeldir C:/Sewer-Studio_KI_4.5/src/AuswertungPro.Next.Infrastructure/Import/Xtf/Models/Dss C:/Sewer-Studio_KI_4.5/docs/reviews/2026-09-11-objektakten/nachweise/dss/alle-felder.xtf
```

Bedienung und Lieferumfang: [DSS-XTF-Ausgabe](../../DSS-XTF-EXPORT.md).
Kein Commit erstellt.
