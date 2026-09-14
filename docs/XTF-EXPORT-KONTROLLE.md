# XTF-Export kontrollieren

Stand: 14.09.2026. Gilt für «Neue eigenständige XTF» mit einem aus GeoShop ergänzten
Projekt, sowohl als vollständige Lieferung als auch als Änderungslieferung.

## Was die Funktion macht

1. Sie liest den aktuellen gespeicherten bzw. im Programm geöffneten Projektstand.
   Projekt, Original-XTF und bisherige Ausgaben werden nicht überschrieben.
2. Sie verwendet den DSS-Objektverbund auch im Änderungsmodus. Schächte werden nicht
   mehr wegen einer als TID gespeicherten Organisation ausgelassen. Alle Projektzeilen
   müssen lieferbar sein; andernfalls wird keine unvollständige Datei geschrieben.
3. Bestehende Haltungen, Knoten, Bauwerke, Deckel, Punkte und Einbauten behalten ihre
   Original-TIDs. Verweise zeigen auf diese Kennungen, nicht auf die angezeigte
   Schachtnummer oder OBJECTID. Widersprechende Bauwerkskennungen sperren die Ausgabe.
   Neue Deckel/Ereignisse und nötige eigene Profile erhalten wiederholbare neue Kennungen.
4. Fehlende Bezugsobjekte können aus einer noch erreichbaren Originalquelle oder aus
   einer ausgewählten vollständigen GeoShop-XTF ergänzt werden. Das passiert nur in
   einer Kopie für die Ausgabe. Doppelte Quell-TIDs werden nicht zusammengeführt.
5. Feldnamen, Klassen, Werte, Pflichtangaben und Bezüge werden gegen den eingebetteten
   DSS-Vertrag geprüft. Die passende `.ili`-Datei samt abhängigen Modellen wird mitgeliefert.

## Wohin die Angaben gehen

| Eingabe | Ziel in der Lieferung |
|---|---|
| Schachtbezeichnung | Bezeichnung am Knoten und zugehörigen Bauwerk; Original-TID bleibt erhalten |
| Zustand, Material, Funktion, Baujahr, Bemerkung | Zugehörige DSS-Attribute am Bauwerk bzw. an der Haltung |
| Deckelhöhe | `Deckel.Kote` am zugeordneten Deckel |
| Sohlenhöhe | `Abwasserknoten.Sohlenkote` |
| Rechts-/Hochwert | INTERLIS-Koordinatenstruktur `Lage` |
| Eigentümer, Datenherr, Datenlieferant | Organisationsverweise mit Original-TIDs, soweit belegt |
| Sanierungen, Unterhalt, Einbauten und Haltungspunkte | Ihre eigenen Normobjekte und modellierten Beziehungen |
| Form, Rotation, historische Bezeichnung und weitere Angaben ohne belegtes Normziel | Separates Zusatzmodell, Feld `Erfasste_Angaben` |
| «Beton, Fertigteil» am Normschacht | Normmaterial `Beton`; genaue Eingabe bleibt zusätzlich erhalten |
| «Keine Mängel (Z4)» und andere Zustandstexte | Richtiger Normcode `Z4` bzw. `Z0`–`Z3` |
| Sanierungsbedarf «Saniert» | Genau so im Zusatzmodell. Kein erfundener DSS-Code; ein überholter Normbedarf wird entfernt |
| Nur Sanierungsjahr, z.B. 2026 | Jahr im Zusatzmodell; kein erfundener 1. Januar im Datumsfeld |

Das Eingabepaket enthält die aktuellen Projektfelder und Objektfelder, Originalcodes,
gespeicherte Bearbeitungszeiten, bewusst geleerte Felder, Unterlisten und Bezüge. Alte
Anzeigetexte ersetzen keine neueren Projektwerte. Zusätzlich werden ungültige optionale
Auswahlcodes aus der Quelle unter `Quellabweichungen` erhalten. Sie werden nicht als
zulässige DSS-Werte ausgegeben. Ungültige Handeingaben und Pflichtlücken sperren weiterhin.

PDF- und Videoinhalte sind nicht in der XTF eingebettet. Reine Dateipfade und persönliche
Arbeitsmarkierungen sind keine Normattribute. Die bestehende Objektakten-Begleitdatei
und Medienablage bleiben davon getrennt. Der reine Normexport über die Programmschnittstelle
(`MitZusatzangaben: false`) bleibt möglich; dann werden keine Eingabepakete beigefügt.

## Änderungslieferung richtig übernehmen

Die DSS-Datei enthält vollständige Normobjekte als Bezugskontext. **Nur die Einträge
`SewerStudio_Zusatz_2026.Zusatzdaten.Aenderung` sind Schreibaufträge.** Der Empfänger
darf nicht sämtliche Kontextwerte als Änderungen in seinen Bestand schreiben.

- `ObjektTid` bezeichnet das konkrete Normobjekt.
- `Feld` nennt das geänderte Attribut bzw. den Verweis. Fehlt bei vorhandenem Auftrag
  der optionale Wert, soll dieser entfernt werden. Der genauere aktuelle Wert kann
  zusätzlich im Eingabepaket stehen, etwa bei «Saniert».
- `Zusatz:Erfasste_Angaben` bezeichnet das zugehörige Eingabepaket, Version 1. Es ist
  JSON innerhalb des bestehenden Zusatzmodells, kein zusätzliches DSS-Attribut.
  Angaben darin brauchen eine ausdrücklich passende Zuordnung beim Empfänger.
- `Beziehung:Erhaltungsereignis_AbwasserbauwerkAssoc` bezeichnet die Bauwerksbezüge
  des angegebenen Ereignisses. Die passenden Normassoziationen stehen ohne künstliche
  TID in derselben Datei. Originalbeziehungen werden nicht aus einem fehlenden
  Feldauftrag gelöscht.
- `GeaendertAm` ist im DSS-Änderungsweg die Erzeugungszeit des Auftrags. Die tatsächlich
  gespeicherten Eingabezeiten bleiben zusätzlich im Paket erhalten.

Ein erneuter Export bestätigt keinen Import in GEONIS und setzt keine Handmarkierung
zurück. Die bisherige kleinere SIA405-Ausgabe für Projekte ohne GeoShop-Objektakten
bleibt als Altweg erhalten; sie besitzt nicht den vollständigen DSS-Feldumfang.

## Prüfung am Projekt Bürglen

Der gelesene Stand enthält **19 Haltungen, 33 Schächte**, 33 Deckel, 35 Sanierungsakten,
167 Haltungspunktakten und 25 Bauwerksteilakten. Die frühere Datei von 16:42 Uhr enthielt
keinen Schacht. Ursache war die falsche Behandlung von Organisations-TIDs im alten
Änderungsplaner. Dieser Fehler ist im Code behoben.

Die vollständige Prüfung findet einen zusätzlichen Konflikt in der GeoShop-Quelle:

| Klasse | Bezeichnung | Original-TID |
|---|---|---|
| Haltungspunkt | A76157 | `ch24gwkdlWuXnSL4` |
| Haltungspunkt | A76157 | `ch24gwkd86TKIHK8` |

Beide Punkte gehören zum gleichen Datenherrn. `DSS_2020_1_LV95` verlangt in dieser
Klasse eine eindeutige Kombination aus Bezeichnung und Datenherr. Die richtige
abweichende Bezeichnung ist nicht bekannt. **Beide Originalnamen und Kennungen bleiben
erhalten. Für diesen Stand wird daher keine neue XTF als normgerecht freigegeben.**
Andreas muss diesen Quellkonflikt klären. Die bisherige Datei ist dadurch nicht nachträglich
korrigiert. Es wurde keine Mail versendet.

## Nachweise

- Verhaltenstests prüfen Originalkennungen, «Alle Angaben», Deckelhöhen, Leeren,
  Zusatzwerte, Zustandstexte, Fertigteilbeton, Jahresangaben, Beziehungen, Quellergänzung,
  Dubletten und unveränderte Originaldateien/Projekte.
- Synthetischer vollständiger Export und synthetische Änderungslieferung bestehen
  `ilivalidator 1.15.0 --allObjectsAccessible` gegen die mitgelieferten Originalmodelle.
- Das ist ein Nachweis für das Dateiformat, **kein durchgeführter GEONIS-/FME-Rückimport**.
  Insbesondere die Zusatzpakete und Änderungsaufträge müssen dort passend verarbeitet werden.
- Vollständiger Release-Build: erfolgreich, 0 Fehler, 2 bestehende Nullable-Warnungen.
- Vollständige Tests: Infrastruktur 6661 bestanden / 6 übersprungen, Pipeline 2658 / 3,
  UI 7284 / 27, ProjectModernizer 62 / 0. Gesamt **16665 bestanden, 36 übersprungen,
  keine Fehler**. Die echten WPF-Kindläufe werden über ihre Elterntests geprüft.
- Prüfdateien: `.tmp/xtf-robust-probe/release-*.trx`; unabhängige Normproben unter
  `.tmp/xtf-robust-norm/{vollstaendig,aenderungen}/ilivalidator-final.log`.
- Architektur-Skill validiert und Git-Prüfung auf fehlerhafte Zeilenumbrüche bestanden.
