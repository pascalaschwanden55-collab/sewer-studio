# Einbauten: Import, Masken und DSS-Ausgabe

Stand: 12.09.2026. Dieses Paket erweitert den vorhandenen Abgleich. Es ist keine
Abnahme des vollständigen WebGIS oder der gesamten order-Lieferung.

## Umgesetzt

| DSS-Originalklasse | Bearbeitungsmaske | Originalbezug |
|---|---|---|
| FoerderAggregat | Pumpe | AbwasserknotenRef |
| Absperr_Drosselorgan | Absperr-/Drosselorgan | AbwasserknotenRef |
| Streichwehr | Überlauf | AbwasserknotenRef, optional UeberlaufNachRef |
| Leapingwehr | Überlauf | AbwasserknotenRef, optional UeberlaufNachRef |
| Einstiegshilfe | Bauwerksteil | AbwasserbauwerkRef |
| Trockenwetterfallrohr | Bauwerksteil | AbwasserbauwerkRef |

Der Leser ergänzt Einbauten am angefragten Schacht und an den Anschlussknoten
einer Haltung. Fremde, nicht verbundene Einbauten werden nicht zugeordnet.
`GeoShopEinbautenImport` verwendet die Originalklasse und -kennung; wiederholter
Abgleich und gemeinsame Nutzung erzeugen keine zweite Akte. Bereits abgeglichene
Projekte erhalten fehlende Einbauakten beim nächsten Abgleich.

`DssEinbautenZuordnung` verbindet belegte Sachfelder und Rollen. Alle Werte der
28 angebundenen Dropdownfälle werden durch die vorhandene Bearbeitung geschrieben,
exportiert und wieder als gleicher Auswahltext importiert. Dazu gehören Antrieb,
Aufstellung, Bauart, Funktion, Steuerung, Signalübermittlung, Verstellbarkeit,
Öffnungsform, Überfallkante, Einstiegshilfenart und Instandstellung. Die übrigen
Dropdowninhalte bleiben vollständig angeboten; ohne Normziel erscheint ihr Wert
im Exportbericht. WebGIS-Zahlencodes werden nicht als INTERLIS-Werte ausgegeben.

Ein Wechsel zwischen Streichwehr und Leapingwehr oder zwischen unterschiedlichen
Bauwerksteilklassen unter derselben Originalkennung wird gesperrt. Bewusstes Leeren
optional modellierter Angaben bleibt erhalten; fehlende Pflichtverweise, falsche
Zielklassen und widersprechende Bearbeitungen gemeinsamer Objekte sperren die Ausgabe.

`ObjektaktenSchachtVererbung` liest schreibgeschützte Schachtanzeigen über die
belegte Knotenkennung. Aktuelle Werte und bewusst geleerte Felder des zugehörigen
Schachts haben Vorrang. Namen und Bemerkungen des Einbaus werden nicht mit solchen
Schachtangaben verwechselt. Bauwerksteillisten zeigen importierte Akten einmalig.

## Grenzen und echte Lieferstichprobe

- Der Schreibvertrag enthält jetzt 19 Klassen und 323 Attributdefinitionen. Neu
  hinzugekommen sind fünf Lieferklassen mit zusammen 96 Objekten. Einstiegshilfen
  waren bereits schreibbar und erhalten jetzt eigene Bearbeitungsakten.
- Die Originaldatei enthält 18045 Einstiegshilfen, 37 Drosselorgane, 44 Pumpen,
  je vier Streich-/Leapingwehre und sieben Trockenwetterfallrohre.
- Eine lesende Stichprobe findet Originalbelege aus fünf Klassen im jeweiligen
  Verbund. Vier der fünf gewählten Knoten haben keinen Bauwerksverweis und werden
  vom bisherigen **Schachtabgleich** deshalb gesperrt. Das ist eine Importgrenze,
  kein pauschaler Verstoss gegen DSS: am Netzknoten ist dieser Bezug optional.
  Die synthetische Haltungsprobe schützt den bereits möglichen Weg über einen
  Anschlussknoten ohne eigenes Bauwerk. Drei Pumpen und ein Streichwehr haben in
  der Quelle keinen benannten Anschlussknoten; für die sieben Fallrohre wurde
  kein benannter Knoten am referenzierten Bauwerk gefunden. Sie wurden deshalb
  nicht als erfolgreiche reale Schachtstichproben gezählt.
  [Stichprobenbericht](einbauten-order-stichprobe.json).
- Ein freier Import der gesamten Lieferung, Neuanlagen ohne Originalbezug und
  die Bearbeitung aller Rohattribute sind noch offen. Insbesondere eigene
  Bezeichnung/Bemerkung des Pumpen-/Wehrobjekts sind keine schreibgeschützte
  Schachtanzeige. Diese Originalwerte werden erhalten, aber noch nicht durch
  einen zusätzlichen eigenen Normfeldeditor bearbeitet.
- `[m³]` beim WebGIS-Arbeitspunkt wird nicht ungeprüft als DSS-Volumenstrom
  `m³/s` interpretiert. Anzahl, verschiedene hydraulische Angaben und weitere
  nicht belegte Felder bleiben mit Namen/Wert als Exportlücke sichtbar.
- Im Schreibvertrag fehlen weiterhin ARABauwerk, Abwasserbauwerk_Text,
  Haltung_Text und Messstelle (66901 Objekte). Die fünf widersprüchlichen
  Haltungs-TIDs und offenen Materialentscheidungen bleiben ungelöst.

## Prüfungen

- 46 neue Infrastrukturtests für Einbauten, einschliesslich 28 vollständiger
  Dropdownfälle; vor der Umsetzung scheiterten die ersten elf Verhaltenstests.
- Abschliessender Fachlauf XTF/GeoShop/Objektakten/Material: 695 bestanden,
  ein vorhandener Live-Test ausdrücklich übersprungen.
- Vier neue Oberflächentests; gesamter betroffener UI-Lauf: 139 bestanden,
  drei Kindtests werden von ihren erfolgreichen Eltern ausgeführt.
- Release-Entwicklungsbuild erfolgreich. Bestehende Nullbarkeitswarnungen
  ausserhalb dieser Änderungen bleiben dokumentiert.
- Synthetische Datei mit 18 Objekten/Beziehungen und allen skalaren Attributen
  der sechs Einbauklassen, einschliesslich einer aktuellen Pumpenänderung:
  **ilivalidator 1.15.0, `--allObjectsAccessible`: bestanden**.
  [Normprotokoll](einbauten-normprobe-validierung.log).

Keine Kundenoriginale, WebGIS-Daten oder gespeicherten Kundenprojekte geändert.
Keine neuen NuGet-Pakete und kein tatsächlicher GEONIS-/FME-Rückimport.
