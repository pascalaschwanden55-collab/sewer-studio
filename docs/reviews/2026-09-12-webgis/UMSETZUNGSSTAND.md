# WebGIS und INTERLIS: Umsetzungsstand vom 12.09.2026

Der angeforderte vollständige WebGIS-Nachbau ist **noch nicht fertig**. Dieser Stand
behebt den Namensschutz, macht Exportlücken sichtbar, bindet allgemeinen Unterhalt an
und verbessert Feldprüfung, Bereiche und Listen. Er macht noch nicht sämtliche
Objektarten aus `order` bearbeitbar und liefert noch keine abgenommene Gesamtdatei.

## Umgesetzt

- **Namensschutz:** Auch der Einzelabgleich erkennt doppelte Haltungs- oder
  Schachtnamen im ganzen Projekt. Gross-/Kleinschreibung und äussere Leerzeichen
  machen keinen Unterschied. Eine nachträglich hinzugefügte oder umbenannte Zeile
  sperrt die Übernahme aus einer veralteten Vorschau. Gleichnamige Objekte anderer
  Art werden weiterhin unterschieden.
- **Exportumfang:** Nicht angebundene Aktenarten und Akten ohne gültigen Bezug
  sperren die Ausgabe. Alle betroffenen Namen und erfassten Felder werden genannt.
  Fehlende Einzelangaben und Quellobjekte erscheinen mit Namen/Wert im Bericht und
  mit ihrer Anzahl in der Vorschau. Die Exportoption verspricht keine pauschale
  Vollständigkeit mehr. Der getrennte Änderungsabgleich wird nicht umgedeutet.
- **Unterhalt:** Reinigung und andere belegte Ereignisse werden als eigene Akten
  importiert und mit zwölf zugeordneten Attributen exportiert. Originalkennungen,
  Firmenbezüge und mehrere Bauwerksbezüge bleiben erhalten. Wiederimport dupliziert
  keine Akte und überschreibt keine Handeingabe. Geänderte Angaben erreichen die XTF.
- **Material:** Alle 43 Haltungs- und 25 Schachtmaterialdetails sind eingeordnet.
  27 haben ein gegen das gelieferte DSS-Modell geprüftes Ziel, 41 sind fachlich
  offen. Die Auswahl zeigt den Stand; offene Werte sperren den Normexport.
  [Vollständige Entscheidungsliste](MATERIAL-ENTSCHEIDUNGEN.md).
- **Oberfläche:** Bestehende WebGIS-Metadaten werden gelesen. Belegte Pflicht-,
  Längen-, Zahl- und Datumsregeln gelten vor dem Schreiben. Pflichtfelder tragen
  einen Stern. 260 weitere Feldbereiche sind anhand der dokumentierten Masken
  zugeordnet. Einbauten haben ihren eigenen Bereich. Listen blättern mit sechs
  Zeilen; beim einzelnen Aufklappen schliesst der vorherige Bereich. Alle
  661 Katalogfelder der 16 Aktenarten bleiben erreichbar, wenn die
  betreffende Akte im Verbund vorhanden ist. Das ist kein Nachweis, dass alle
  Lieferobjekte bereits als solche Akten importiert werden.
- **Dropdowns:** Alle 269 dokumentierten Vorkommen mit ausgezählten Werten sind
  im Katalog enthalten. 24 fehlende Sanierungsverfahren, die Witterung und fünf
  Haltungspunkt-Auswahlen wurden ergänzt; Originalcodes bei Deckel/Sanierung
  nachgetragen. Alte Auswahlen bleiben sichtbar, gleichnamige Werte mit eigenen
  Codes bleiben getrennt. [Prüfliste und offene Nachweise](DROPDOWN-ABGLEICH.md).
- **Haltungspunkte:** Der Katasterabgleich ergänzt eigene Punktakten, auch für
  bereits abgeglichene Projekte. Die fünf Auswahlfelder und belegten Sachattribute
  sind bearbeitbar und für den XTF-Schreibweg geprüft. Original-TIDs und Handeingaben
  bleiben erhalten. Dokumentbeziehungen und vollständige Punktfunktionen bleiben offen.

## Befund zur Original-Lieferung

`C:\Users\Besitzer\Downloads\order\34UR_Abwasser_DSS_2020_1.xtf` wurde vollständig
und ausschliesslich lesend gezählt: **630246 Objekte/Beziehungen in 22 Klassen**.
Darunter sind 1448 Beziehungen ohne TID und 628793 eindeutige TIDs. Der
mitgelieferte SQLite-Index ersetzt diesen Nachweis nicht: Er bildet mehrere Objekte
mit derselben TID nicht getrennt ab.

Fünf Haltungs-TIDs kommen jeweils mit unterschiedlichen Inhalten vor:

| TID | Bezeichnung |
|---|---|
| ch24gwkdr3uE6xDU | 07.1050143-07.1061526 |
| ch24gwkdhlzs3Ci0 | 07.1061526-22187 |
| ch24gwkdd0hwFeXP | 07.1068824-10.44583 |
| ch24gwkdG1JCIz5W | keine Bezeichnung geliefert |
| ch24gwkdoo0hXB5G | ...........-07.1068824 |

Bei allen fünf Paaren unterscheidet sich genau der Verweis **AbwasserbauwerkRef**:
dieselbe Haltung ist jeweils zwei unterschiedlichen Kanalobjekten zugeordnet.
[Die beiden Kanal-TIDs je Haltung](order-dubletten-unterschiede.json).
Für diese Kennungen muss der gültige Bezug fachlich bestimmt oder eine korrigierte
Lieferung bereitgestellt werden. Eine automatische Auswahl der ersten/letzten Zeile
oder ein pauschaler TID-Präfixersatz wäre keine eindeutige Zuordnung.

Der gemeinsame DSS-Vertrag umfasst jetzt **23 Klassen mit 368 Attributdefinitionen**.
Die vier zusätzlichen Klassen ARABauwerk, Abwasserbauwerk_Text, Haltung_Text und
Messstelle sind über den neuen freien Lieferungs-Editor zugänglich. Dieser importiert
alle 630246 Objekte/Beziehungen und alle 22 tatsächlichen Lieferklassen in eine eigene
Arbeitsdatei; er benötigt keine Projektzeile und keinen Bauwerksbezug am Knoten.

Die tatsächliche Gesamtprüfung meldet **99795 Objekte mit mindestens einem Fehler**,
zusätzlich 26 externe Organisationskennungen. Je Objekt wird der erste Fehler genannt.
Die fünf doppelten Haltungs-TIDs bleiben als getrennte Zeilen erhalten. Keine
Originaldaten wurden verändert und keine fehlerhafte Gesamt-XTF freigegeben.
[Bedienung und Grenzen](../../LIEFERUNGS-EDITOR.md),
[Zählungen und Feldstichproben aller Klassen](lieferung-order-abnahme.json).

[Einbauten-Abnahme](EINBAUTEN-ABNAHME.md): Pumpen, Drosseln, beide Wehrklassen,
Fallrohre und Einstiegshilfen erhalten eigene Akten und behalten ihre Originalkennungen.
Alle Werte der 28 angebundenen Dropdownfälle werden über Export und Wiederimport geprüft.
Geerbte Schachtfelder werden am richtigen Schacht gelesen. Die echte Lieferstichprobe
zeigt zusätzlich eine bestehende Importgrenze bei Netzknoten ohne Bauwerksverweis.

Nachweise: [Inventar mit Klassen, Feldern und Dateiprüfsumme](order-inventar.json),
[Prüfsummen der widersprüchlichen Objekte](order-doppelte-kennungen.json).
Das Zählwerkzeug liegt in `tools/PruefeWebGisLieferung.py`. Es schreibt einen neuen
Bericht und verändert keine Originaldatei.

## Noch umzusetzen und abzustimmen

1. Die vollständigen WebGIS-Detailmasken und Objektfunktionen anhand der Vorlage
   vervollständigen, einschliesslich Strängen, hydraulischer Geometrie und weiteren
   Unterobjekten. Der freie Import und die Norm-Sachfeldbearbeitung aller tatsächlich
   gelieferten Klassen sind vorhanden. Neu anlegen, Dubletten bereinigen, Klassenwechsel
   sowie Linien-/Flächenbearbeitung fehlen im freien Editor noch.
2. Die noch fehlenden WebGIS-Funktionen ergänzen: durchgehender Entwurfs-/Speichern-/
   Verwerfen-Ablauf, vollständige Subtypsteuerung, kombinierte Eingabezeilen,
   Objekt-/Beziehungsbearbeitung sowie fachliche Karten-, Geometrie-, Dokument- und
   Prüfaktionen. Bestehende lokale Funktionen ersetzen diesen Abgleich nicht.
3. Die offenen Materialdetails sowie Unterhaltsarten/-status ohne DSS-Ziel fachlich
   entscheiden. Beispiele: Begehung, Georadar, Dichtheitsprüfung, Beauftragt,
   Nicht beauftragt. Die Namen allein begründen keine Ersatzzuordnung.
4. Auftrag/Nummer und weitere WebGIS-Angaben, die das gelieferte DSS-Modell nicht
   kennt, über einen vereinbarten Zielvertrag behandeln. Solche Angaben werden
   derzeit im Projekt und im begleitenden Objektakten-JSON erhalten.
5. Nach Korrektur der Quelldubletten die komplette neue Lieferung gegen die
   vereinbarten Modelle prüfen und den GEONIS-/FME-Rückimport mit LISAG/trigonet
   abnehmen. Es wurde keine E-Mail versendet und kein Rückimport ausgeführt.

## Prüfung

- Die neuen Namensschutz- und Exportabdeckungstests zeigten die Fehler vor der
  Reparatur und bestehen nach der Änderung.
- Früherer Gesamtlauf Infrastruktur vor der Lieferungs-Erweiterung: **6530 bestanden, 6 ausdrücklich übersprungen**.
- Abschliessender fokussierter Lauf einschliesslich Einbauten, Dropdown- und Punktänderungen:
  **718 bestanden, 1 ausdrücklich übersprungen** (XTF, GeoShop, Objektakten, Material und Feldprüfung).
- Oberfläche für Objektakten, GeoShop und XTF: **146 bestanden** einschliesslich Lieferungs-Editor und Dienstregistrierung; die vier isolierten
  Kindtests werden im Elternlauf übersprungen und von ihren erfolgreichen Elterntests ausgeführt.
- `dotnet build AuswertungPro.Dev.slnf -c Release --no-restore`: erfolgreich,
  keine Fehler. Zwei bestehende Nullbarkeitswarnungen in `VsaFotoAblageTests`
  und `ObjektaktenListenErgaenzungenStore`.
- Eine synthetische XTF mit 15 Objekten/Beziehungen besteht **ilivalidator 1.15.0
  mit `--allObjectsAccessible`** gegen die mitgelieferten Modelle. Enthalten sind
  Haltung, Schacht, Deckel, Einstiegshilfe, drei Unterhaltsereignisse und deren
  Beziehungen. Das ist eine Funktionsprobe, keine bereinigte Original-Gesamtlieferung.
  [Validatorprotokoll](normprobe-validierung.log), [Exportbericht](normprobe-bericht.txt).
- Die zusätzliche synthetische Einbauten-Datei enthält 18 Objekte/Beziehungen und
  sämtliche skalaren Attribute der sechs Einbauklassen. Sie besteht ebenfalls
  `ilivalidator --allObjectsAccessible`.
  [Validatorprotokoll](einbauten-normprobe-validierung.log).
- Freie Ausgabe mit 29 synthetischen Objekten/Beziehungen, beiden Textklassen und
  Messstelle besteht `ilivalidator --allObjectsAccessible`. Alle horizontalen/vertikalen
  Textausrichtungen und fünf Plantypen sind enthalten.
  [Validatorprotokoll](lieferung-normprobe/ilivalidator.log). Auch nach Bearbeitung
  von Textinhalt, Punktkoordinate, Ausrichtung und Messstellenangaben bestanden:
  [Bearbeitete Ausgabe](lieferung-normprobe/Bearbeitete-Lieferung.xtf),
  [Prüfprotokoll](lieferung-normprobe/bearbeitet-ilivalidator.log).
- Architekturkarte und Skill wurden abgeglichen; `quick_validate.py`: `Skill is valid!`.

Kundenoriginale und WebGIS-Daten wurden nicht verändert. Es wurden keine neuen
NuGet-Pakete installiert und keine Änderungen committed.
