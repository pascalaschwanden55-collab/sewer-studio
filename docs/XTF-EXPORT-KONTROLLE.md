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
- `Feld` nennt das geänderte Attribut bzw. den Verweis. **Der neue Wert steht am Objekt
  mit der genannten `ObjektTid`, nicht im Auftrag.** Ein Auftrag trägt selbst nie einen
  Wert. Nur wenn das genannte Attribut am Objekt fehlt, ist das Feld zu leeren. Ein Feld
  zu leeren, weil der Auftrag keinen Wert enthält, wäre Datenverlust an jedem beauftragten
  Feld. Der genauere aktuelle Wert kann zusätzlich im Eingabepaket stehen, etwa bei «Saniert».
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

Gegenprüfung der Bezeichnungen (14.09.2026, abends): Alle 19 Haltungen und 33 Schächte
tragen die GeoShop-Bezeichnung ihrer Originalkennung; null Abweichungen.

Die Vollprüfung vom Nachmittag meldete einen Namenskonflikt in der GeoShop-Quelle:

| Klasse | Bezeichnung | Original-TID | gehört zu |
|---|---|---|---|
| Haltungspunkt | A76157 | `ch24gwkdlWuXnSL4` | Projekthaltung 80475-80462 (Anfangspunkt) |
| Haltungspunkt | A76157 | `ch24gwkd86TKIHK8` | Hausanschluss 07.1036564-80475, **nicht im Projekt** |

Beide Punkte liegen am Knoten 80475 beim gleichen Datenherrn; `DSS_2020_1_LV95` verlangt
dort eine eindeutige Bezeichnung. Der Konflikt steckt in der GeoShop-Quelle, nicht in
SewerStudio. **Er sperrt die Lieferung nicht mehr:** Ein Haltungspunkt gehört zu genau einer
Haltung. Von den 167 Punktakten gehören 38 zu den 19 Projekthaltungen; die übrigen 129 sind
Punkte fremder Leitungen an den Projektknoten. Sie werden nur geliefert, wenn sie eigene
Eingaben tragen (Handwert, Unterliste oder ein im GeoShop-Vergleich bewusst behaltener,
abweichender Wert). In Bürglen trägt keiner davon eine Eingabe; sie bleiben unverändert in
GeoShop, und der Bericht nennt sie mit Bezeichnung und Kennung. Trägt ein fremder Punkt
eine eigene Eingabe, wird er geliefert, und ein dortiger Namenskonflikt sperrt weiterhin.

Zweiter Befund derselben Prüfung: Alle 19 Haltungen erhielten ein **neues eigenes
Rohrprofil**, obwohl es dem gemeinsamen Originalprofil «Kreisprofil 1.00» glich. Auslöser
war die DN aus dem alten Import (Quelle `Legacy`), die als Änderung galt. Ergibt sich dasselbe
Profil wie das Original (Profiltyp, Verhältnis, Datenherr, Datenlieferant), bleibt jetzt der
Originalverweis. In Bürglen bleiben zwei echte neue Profile: Ihr Original sagt «unbekannt»,
Pascal hat von Hand «Kreisprofil» gesetzt.

Ergebnis nach beiden Korrekturen (Export aus dem gespeicherten Projektstand, rein lesend):

| Lieferung | Objekte | davon Haltungspunkte | Rohrprofile | Aufträge |
|---|---|---|---|---|
| vollständig | 19 Haltungen, 19 Kanäle, 33 Knoten, 33 Normschächte, 33 Deckel, 25 Einstiegshilfen, 35 Unterhalt, 183 Zusatzangaben | 38 | 4 (2 Original, 2 neu) | – |
| Änderungen | dieselben Normobjekte als Kontext | 38 | 4 | 534 |

Die 534 Aufträge unterscheiden sich alle vom Originalwert; keiner ist ein Scheinauftrag.
Grosse Gruppen: 35 neue Sanierungsereignisse (Unterhalt mit Bezeichnung, Art, Status,
Datenherr, Bauwerksbezug), Standortname aus der Strasse (33 Schächte, 19 Kanäle),
Zustand Z4 nach Sanierung, «Saniert» als Leeren des Normbedarfs, Bemerkungen.
Die 35 Sanierungen tragen technische Bezeichnungen `Unterhalt_<Kennung>`, weil das
Feld «Bezeichnung» der Sanierungsakten in Bürglen leer ist.

Der Mailentwurf für Andreas vom Nachmittag beschreibt A76157 noch als Sperre; das ist
überholt. Der Quellkonflikt in GeoShop bleibt als Hinweis erwähnenswert, blockiert aber nichts.

## Kann der Empfänger die Datei einlesen?

Gemessen am 14.09.2026 mit `ilivalidator 1.15.0`, das denselben INTERLIS-Leser verwendet
wie die üblichen Importwerkzeuge:

| Datei | eigenes Zusatzmodell vorhanden | ohne dieses Modell |
|---|---|---|
| Änderungslieferung | 0 Fehler | **`SewerStudio_Zusatz_2026: model(s) not found` — nichts lesbar** |
| vollständige Lieferung mit Zusatzangaben | 0 Fehler | **ebenfalls nichts lesbar** |
| **reine Normlieferung** (`MitZusatzangaben: false`) | – | **0 Fehler** |

Ein fremdes Modell im selben Transfer wird nicht übersprungen, es bricht den ganzen
Lesevorgang ab. Wer eine Datei mit Zusatzmodell erhält, muss die mitgelieferte
`SewerStudio_Zusatz_2026.ili` in sein Modellverzeichnis legen. Das ist der Preis dafür,
dass Feldaufträge und nicht normierbare Eingaben verlustfrei mitgehen.

Die **reine Normlieferung** ist der Weg ohne diese Bedingung: ausschliesslich
`DSS_2020_1_LV95` und `SIA405_Base_Abwasser_1_LV95`, mit den Originalkennungen und den
in den Normfeldern eingearbeiteten Handänderungen. Sie war bis zum 14.09.2026 an drei
Stellen an das Zusatzmodell gekoppelt und liess sich aus einem echten Projekt gar nicht
erzeugen; jede dieser Regeln gilt jetzt unabhängig vom Zusatzmodell:

- Sanierungsbedarf «Saniert» entfernt den überholten Normwert (kein DSS-Wert dafür).
- Ein blosses Sanierungsjahr erzeugt keinen erfundenen Zeitpunkt.
- Ein ungültiger optionaler Quellcode aus dem Kataster wird abgetrennt statt geschrieben.
  In Bürglen tragen 25 Einstiegshilfen die Art «1»; dieser Fehler stammt aus der Quelle.
- Eine Akteneingabe, die nur einen solchen ungültigen Quellcode spiegelt, wird nicht
  als Normwert geschrieben.

Zwei Punkte muss der Empfänger bei der **vollständigen** Lieferung kennen, weil sie
keine Auftragsliste hat und deshalb nicht selbst zwischen Kontext und Änderung trennt:

- 33 Deckel haben in GeoShop keine Bezeichnung. INTERLIS verlangt sie, deshalb steht dort
  die Originalkennung als technische Bezeichnung. Diese Werte gehören nicht nach GEONIS.
- Die erfassten Sanierungen sind neue Unterhalt-Objekte, in GEONIS noch nicht vorhanden.

## Paket für GEONIS erstellen

Der Knopf unter «XTF erstellen» erzeugt in einem Durchgang alles, was der Empfänger
braucht, und packt es als ZIP zum Versenden:

```
GEONIS-Paket_<Projekt>_<Datum_Zeit>.zip
  LIESMICH.txt            Erklärung mit den Zahlen genau dieses Laufs
  1 Aenderungen\          Änderungslieferung, Bericht, alle Modelle inkl. SewerStudio_Zusatz_2026.ili
  2 Vollstaendig\         reine Normdatei, Bericht, nur die offiziellen Modelle
```

Der Ablauf prüft beide Fassungen, zeigt **eine** Vorschau mit der Tabelle der Feldaufträge
und schreibt erst nach der Bestätigung. Scheitert eine der beiden Fassungen, wird das
angefangene Paket wieder entfernt: Ein halbes Paket könnte versehentlich verschickt werden.
Bestehende Ordner und ZIP-Dateien werden nie überschrieben; bei Namensgleichheit entsteht
ein freier Name. `Verwirf` löscht ausschliesslich einen Ordner, den derselbe Lauf angelegt hat.

Die Liesmich-Datei nennt nur, was in den Berichten wirklich steht. Findet sie eine Angabe
nicht, lässt sie die Zeile weg statt eine Zahl zu erfinden. Beide Fassungen melden dieselben
Deckel ohne Bezeichnung; sie werden gezählt, nicht addiert.

Vier Punkte einer Gegenprüfung vom 14.09.2026 abends sind eingearbeitet:

- Die Übernahmeanleitung sagte «Fehlt bei einem Auftrag der Wert, Feld leeren». Ein Auftrag
  trägt aber **nie** einen Wert; er steht am Objekt mit der genannten `ObjektTid`. Wörtlich
  befolgt hätte die Anleitung jedes beauftragte Feld geleert, etwa das Material «Zement» an
  der Haltung 59604-59723. Die Anleitung nennt jetzt die drei Schritte und schliesst
  Feldnamen mit Doppelpunkt ausdrücklich aus.
- Die Objektakten-JSON entstand erst nach dem Packen. Ordner und Mailanhang hatten dadurch
  verschiedene Inhalte. `XtfExportActions.SchreibeBegleitdateien` läuft jetzt vor dem Packen;
  scheitert sie, wird das Paket verworfen.
- Ein Fehler beim Packen liess eine unbrauchbare halbe ZIP liegen. Sie wird jetzt entfernt,
  das Paket verworfen und der Fehler gemeldet. Bleibt dabei etwas liegen, nennt die Meldung
  den Ordner und dass er unvollständig ist.
- Die Liesmich nannte 447 Aufträge, die Datei enthielt 534. Die 87 Aufträge auf
  `Zusatz:Erfasste_Angaben` fehlten in Zählung und Vorschautabelle. Sie stehen jetzt beide
  Male mit.

Beteiligte Teile: `XtfKatasterPaketUseCase` (Ablauf), `KatasterPaketLiesmich` (reine
Textregel), `IXtfPaketAblage`/`XtfPaketAblage` (Ordner, Berichte, ZIP; im ServiceProvider
registriert). Die Exportseite leiht nur Dateiwahl und Vorschaufenster.

Abnahme am Projekt Bürglen (14.09.2026, rein lesend): Paket erzeugt, beide Fassungen mit
`ilivalidator 1.15.0` gegen die beigelegten Modelle geprüft, je 0 Fehler. Die
Änderungslieferung trägt 447 Feldaufträge, die reine Normdatei 274 Objekte.

## Nachweise

- Verhaltenstests prüfen Originalkennungen, «Alle Angaben», Deckelhöhen, Leeren,
  Zusatzwerte, Zustandstexte, Fertigteilbeton, Jahresangaben, Beziehungen, Quellergänzung,
  Dubletten und unveränderte Originaldateien/Projekte.
- Synthetischer vollständiger Export und synthetische Änderungslieferung bestehen
  `ilivalidator 1.15.0 --allObjectsAccessible` gegen die mitgelieferten Originalmodelle.
- Echte Bürglen-Lieferungen (vollständig und Änderungen, 14.09.2026 abends): `ilivalidator 1.15.0`
  null Fehler, null Warnungen. Mit `--allObjectsAccessible` bleibt genau eine Fehlerart:
  `No object found with OID ch20p3q400002009` (Datenherr/Datenlieferant Abwasser Uri). Die
  GeoShop-Quelle enthält **kein einziges** Organisationsobjekt; der Verweis ist echt extern und
  muss im Zielkataster vorhanden sein. Die Vollständigkeitsforderung ist deshalb für eine
  GeoShop-basierte Lieferung nicht erfüllbar. Prüfdateien liegen unter
  `Downloads\XTF_Buerglen_20260914_2043`.
- `XtfDssFremdeHaltungspunkteTests` (3) und `XtfDssRohrprofilTests` (2) halten beide Regeln fest.
- `XtfDssReinerNormexportTests` (2) hält fest, dass die reine Normlieferung ohne Zusatzmodell
  entsteht und `XtfDssVorschauTests` (5), dass die Vorschau die Feldaufträge zeigt.
- Prüfpaket für den Empfänger: `Downloads\XTF_Buerglen_fuer_Andreas_20260914` mit beiden
  Spielarten, Berichten, Prüfprotokollen und einer Liesmich-Datei.
- `XtfKatasterPaketUseCaseTests` (5), `KatasterPaketLiesmichTests` (6) und
  `XtfPaketAblageTests` (3) halten den Paketweg fest.
- Das ist ein Nachweis für das Dateiformat, **kein durchgeführter GEONIS-/FME-Rückimport**.
  Insbesondere die Zusatzpakete und Änderungsaufträge müssen dort passend verarbeitet werden.
- Vollständiger Release-Build: erfolgreich, 0 Fehler, 2 bestehende Nullable-Warnungen.
- Vollständige Tests: Infrastruktur 6661 bestanden / 6 übersprungen, Pipeline 2658 / 3,
  UI 7284 / 27, ProjectModernizer 62 / 0. Gesamt **16665 bestanden, 36 übersprungen,
  keine Fehler**. Die echten WPF-Kindläufe werden über ihre Elterntests geprüft.
- Prüfdateien: `.tmp/xtf-robust-probe/release-*.trx`; unabhängige Normproben unter
  `.tmp/xtf-robust-norm/{vollstaendig,aenderungen}/ilivalidator-final.log`.
- Architektur-Skill validiert und Git-Prüfung auf fehlerhafte Zeilenumbrüche bestanden.
