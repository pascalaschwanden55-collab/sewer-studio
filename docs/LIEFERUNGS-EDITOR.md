# Gesamte SIA405-Lieferung bearbeiten

Stand: 12.09.2026. Die freie Lieferungsbearbeitung ergänzt die Projekt-Objektakten.
Sie kann sämtliche Objekte der gelieferten `order`-XTF öffnen, auch ohne zugehörige
Projektzeile, Namen oder Bauwerksverweis. Der vollständige WebGIS-Nachbau ist damit
noch nicht abgeschlossen.

## Bestehender Nova-Stil

Der Editor verwendet dieselben Nova-Seitenköpfe, Karten, Schaltflächen und Farben
wie die vorhandene Anwendung. Die Darstellung folgt dem hellen und dunklen Thema.
Der Einstieg auf der Exportseite sitzt im bestehenden Nova-Seitenkopf.
Die vorhandenen Objektmasken und gemeinsamen Dropdown-Kataloge bleiben die Grundlage;
dieser Abgleich betrifft den zuvor ergänzten Lieferungs-Editor und dessen Einstieg.

## Bedienung

1. In SewerStudio **Export → SIA405-Lieferung bearbeiten …** öffnen.
2. **Lieferung öffnen …** wählen. Bei einer XTF einen neuen Namen für die Arbeitsdatei
   mit Endung `.ssxtf` angeben. Eine bestehende `.ssxtf` lässt sich direkt weiterbearbeiten.
3. Objektart auswählen und nach Name, Beschriftung oder Originalkennung suchen.
   Die Liste zeigt jeweils 100 Treffer; **Weiter/Zurück** blättert durch den Bestand.
4. Ein Objekt auswählen, Angaben ändern und **Änderungen speichern** wählen.
   **Eingaben verwerfen** nimmt ausschliesslich noch ungespeicherte Eingaben zurück.
   Offene Eingaben sperren den Objektwechsel, die Prüfung und das Schliessen.
5. **Lieferung prüfen** nennt die betroffenen Objekte. **Nur Prüfprobleme → Suchen**
   zeigt diese in der Objektliste. **Bericht speichern …** schreibt den vollständigen
   Bericht; im Fenster ist nur eine begrenzte Vorschau sichtbar.
6. **Neue XTF schreiben …** prüft den aktuellen Stand erneut. Bei offenen Fehlern
   entsteht keine XTF. Vorhandene Dateien werden nicht überschrieben.

Die vorbereitete Arbeitsdatei dieser Lieferung liegt unter
`C:\Users\Besitzer\Documents\SewerStudio\Lieferungen\order-20260912.ssxtf`.
Der vollständige erste Prüfbericht liegt daneben als `order-20260912-pruefung.txt`.
Gespeicherte Änderungen gehören zur `.ssxtf`; diese Datei deshalb zusammen mit den
eigenen Arbeitsdaten sichern. Sie liegt getrennt vom Projekt-JSON und vom Original.

## Was bearbeitbar ist

- Sachfelder der vorhandenen Normklassen, einschliesslich Unterhalt, Einbauten,
  ARA-Bauwerken, beiden Beschriftungsarten und Messstellen.
- Vollständige Aufzählungen aus dem gelieferten DSS-Modell. Die gemeinsame Definition
  umfasst 23 Klassen mit 368 Attributen. Beziehungen ohne eigene TID kommen hinzu.
  Ungültige Altwerte bleiben sichtbar; sie werden nicht durch den ersten Eintrag ersetzt.
- Beziehungskennungen mit Prüfung auf vorhandene, eindeutige und passende Zielobjekte.
  Externe Organisationsverweise bleiben ausdrücklich als solche erhalten.
- Punktkoordinaten `Lage` und `TextPos` in LV95. Rechts- und Hochwert werden gemeinsam geprüft.
- Originalkennung und Objektklasse bleiben fest. `Letzte_Aenderung` wird beim Speichern
  einer tatsächlichen Änderung aktualisiert. Pflichtfelder dürfen nicht geleert werden.

Linien- und Flächengeometrien werden unverändert erhalten und hier nur angezeigt.
Neue Objekte, Klassenwechsel, Löschen von Dubletten, grafische Geometriebearbeitung,
Dokumentaktionen und die vollständige Bedienung aller WebGIS-Unterlisten fehlen noch.
Die neue Maske verwendet die Normattribute; sie ersetzt nicht den noch ausstehenden
Abgleich aller WebGIS-Ansichten und ihrer Funktionen. Nicht zugeordnete Zusatzfelder
bleiben in der Arbeitsdatei sichtbar und sperren eine Ausgabe, die sie verlieren würde.

## Prüfung an der tatsächlichen Lieferung

- **630’246 Objekte/Beziehungen, alle 22 gelieferten Objektarten**, vollständig übernommen.
- Jede Objektart wurde aus der Arbeitsdatei geöffnet; 124 Normlisten in diesen
  Klassen-Stichproben sind verfügbar. Die WebGIS-Listen werden zusätzlich im
  [Dropdown-Abgleich](reviews/2026-09-12-webgis/DROPDOWN-ABGLEICH.md) geprüft.
- Erstimport: etwa 11,3 Sekunden; erste Gesamtprüfung: etwa 15,5 Sekunden auf diesem
  Rechner. Gemessener maximaler Prozessspeicher: etwa 253 MiB. Arbeitsdatei nach Prüfung:
  731’283’456 Bytes. Diese Messung ist keine allgemeine Leistungszusage.
- **99’795 Objekte mit mindestens einem gemeldeten Fehler**. Der Bericht nennt je Objekt
  den ersten Fehler; nach Korrekturen kann die nächste offene Angabe sichtbar werden.
  Darunter 51’154 Deckel ohne Pflichtbezeichnung, 9’668 Einstiegshilfen mit dem nicht
  zugeordneten Art-Wert `1` und 2’005 Bauwerksbeschriftungen ohne Bauwerksverweis.
- Die fünf doppelten Haltungskennungen bleiben als zehn getrennte Zeilen erhalten.
  Ihre widersprüchlichen Bauwerksbezüge werden nicht automatisch aufgelöst.
- 26 externe Organisationskennungen sind referenziert, aber nicht als Stammdaten
  geliefert. Die Organisationen müssen im Ziel vorhanden sein oder korrekt ergänzt werden.
- ARA-Bauwerke benötigen zusätzlich einen belegten Verweis auf eine Kläranlage.
  Fehlende Bezugsobjekte und fachlich offene Material-/Unterhaltswerte werden nicht erfunden.
- Keine Originalobjekte wurden geändert. Die SHA-256-Prüfsumme vor und nach der Arbeit
  ist `ed6bfdc290f66e8bdefd5fcc6cff59013337adcd763df754d1eadec41da34830`.

[Zählungen, Klassen-Stichproben und Fehlergruppen](reviews/2026-09-12-webgis/lieferung-order-abnahme.json).
Die tatsächliche Gesamtausgabe bleibt gesperrt, bis die erforderlichen Korrekturen vorliegen.

## Technischer Nachweis und Grenzen

Die Arbeitsdatei ist eine eigene SQLite-Datei mit Formatversion 1. Original-XML,
geändertes XML, Original-TID, Korb und Bearbeitungsstand sind getrennt gespeichert.
Mehrfach vorkommende TIDs werden nicht zusammengeführt. Import und Ausgabe werden
erst nach vollständigem Abschluss unter dem gewählten Dateinamen veröffentlicht.
Gleichzeitige Änderungen desselben Objekts überschreiben sich nicht still.

`IXtfLieferungsAblage` liegt in `Application/Xtf/Lieferung`, die Dateioperationen in
`Infrastructure/Import/Xtf/Lieferung`. Der Normprüfer verwendet den bestehenden
DSS-Vertrag. Der Dienst ist zentral registriert; es gibt keine neue NuGet-Abhängigkeit.

`XtfLieferungsAblageTests` prüft Import, Bearbeitung, Wiederöffnung, Ausgabe,
Beziehungen ohne TID, mehrere Körbe, unveränderte Liniengeometrie, vollständige
Normauswahl, unbekannte Altwerte, Konflikte, Abbruch und Originalschutz.
`XtfLieferungUiTests` prüft zusätzlich das echte WPF-Fenster mit Speichern,
Schliessschutz und Auswahl bis zum letzten Dropdown-Eintrag.

Eine neu geschriebene synthetische Lieferung mit **29 Objekten/Beziehungen** besteht
`ilivalidator 1.15.0 --allObjectsAccessible` gegen die lokalen offiziellen Modelle.
Enthalten sind alle horizontalen/vertikalen Textausrichtungen, alle fünf Plantypen,
beide Textklassen, Messstelle, Unterhalt, Einbauten und ein Organisationsobjekt.
[XTF](reviews/2026-09-12-webgis/lieferung-normprobe/Neue-Lieferung.xtf),
[Validatorprotokoll](reviews/2026-09-12-webgis/lieferung-normprobe/ilivalidator.log).
Auch die erneute Ausgabe nach Änderungen an Textinhalt, Punktkoordinate, Ausrichtung
und Messstelle hat bestanden: [bearbeitete XTF](reviews/2026-09-12-webgis/lieferung-normprobe/Bearbeitete-Lieferung.xtf),
[Prüfprotokoll](reviews/2026-09-12-webgis/lieferung-normprobe/bearbeitet-ilivalidator.log).
Die eingebauten Textausrichtungen stammen aus dem
[INTERLIS-Datenmodell](https://www.interlis.ch/modelle/internes-datenmodell).

Abschliessende Regression: **718 Infrastruktur- und 146 UI-Tests bestanden**.
Ein vorhandener Live-Test bleibt ausdrücklich ausgenommen; vier WPF-Kindtests
werden über ihre erfolgreichen Elterntests ausgeführt. Der Dev-Build besteht mit
zwei bestehenden Nullbarkeitswarnungen in anderen Dateien.

Die interne Objektprüfung ist kein vollständiger INTERLIS-Compiler. Sie ersetzt weder
die Prüfung der endgültigen Gesamtdatei mit ilivalidator noch den GEONIS-/FME-Rückimport
mit LISAG/trigonet. Insbesondere ist die fehlerhafte Original-Gesamtlieferung dadurch
nicht abgenommen. Es fand kein Rückimport und keine Änderung im WebGIS statt.
