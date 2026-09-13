# Objektakten im Nova-Stil

Stand: 11.09.2026. Grundlage: WebGIS-Plan vom 10.09.2026.

## Bedienung

Bei **Haltungen** oder **Schächten** die Zeile aufklappen. Unter **Alle Angaben** steht
die vollständige Objektakte direkt in der Liste. **Kurzansicht** zeigt die bisherigen
Karten mit ihrer gespeicherten Anordnung. Die vollständige Ansicht ist beim Aufklappen
vorausgewählt; ein zusätzliches Fenster ist nicht nötig.

Die Akte verwendet die vorhandenen Nova-Farben, Karten und Schaltflächen.
Die Begriffe stammen aus der erfassten WebGIS-Maske. Ein breites Fenster zeigt zwei kompakte Feldspalten, ein schmales eine.
Jedes Feld hat eine Eingabe: Text oder Dropdown mit möglichem Sonderwert. Der bisherige Knopf
**Objektakte** öffnet dieselbe Maske weiterhin in einem separaten Fenster.

- Die Objektwahl wechselt zwischen Bauwerk, Deckeln und Sanierungen.
- Oben stehen die Kopfangaben wie im WebGIS. Darunter folgen **Daten I**, **Daten II**,
  **Bauwerksteile**, **Haltungspunkte**, **Administrativ**, **Unterhalt**, **Hydraulik** und **Metadaten**.
  Schächte haben zusätzlich **Stammkarte**. Die Bereiche lassen sich einzeln aufklappen.
- **Alle auf** und **Alle zu** schalten alle Bereiche. Die Suche öffnet passende Bereiche und findet auch ausgeblendete Werte.
- Unter **Mehr → Ansicht anpassen** lassen sich Felder ausblenden und für **Meine Übersicht** auswählen.
  Dies verändert nur die persönliche Ansicht. Gruppen behalten ihren Aufklappzustand.
- Unter **Mehr** erzeugen **Deckel hinzufügen** und **Sanierung hinzufügen** eigene, zunächst leere Datensätze.
  Hersteller, ausführende Firma, Operateurtext und bisheriges Gewerk sind getrennte Angaben.
- Am gewählten Deckel legt **Als Hauptdeckel setzen** den Bezug zur Schachtanzeige fest.
  Berechnete Schachttiefe und Gefälle sind zusätzliche Anzeigen. Eingelesene Höhen bleiben erhalten.
- Änderungen in der aufgeklappten Akte berücksichtigen die eingestellte automatische Speicherung.
  Mit **Speichern** lassen sich Änderungen auch ausdrücklich sichern.
  Namen und Schachtverbindungen werden über die bisherigen Umbenennungsabläufe geändert.

## Umfang

Alle 200 Felddefinitionen des Plans sind zugeordnet. Die 91 Dropdownstellen verwenden
78 Kataloge mit zusammen 1’158 unveränderten Einträgen. Dazu kommen zusätzliche
Haltungs-/Knotenbemerkungen, Liner- und Ereignisangaben. Bisherige Projektfelder und
freie Vorlagenspalten bleiben erreichbar. Der bisherige Excel-/CSV-Spaltenaufbau bleibt bestehen.

Material-Unterlisten werden nur für die belegten Elternwerte angeboten. Ein Elternwechsel
löscht den vorhandenen Detailwert nicht. Unbekannte Codes und freie Werte bleiben gespeichert.
Deckel- und Sanierungslisten besitzen keine erfundenen WebGIS-Originalcodes.

Die 24 bekannten verknüpften Tabellen werden angezeigt. Bestätigte Deckel, Ereignisse,
Ein-/Ausläufe und Einstiegshilfen erscheinen anhand ihrer tatsächlichen Beziehungen.
Nicht untersuchte Detailmasken sind ausdrücklich als offen bezeichnet.

## Import und Ausgabe

Im GeoShop-Abgleich genau eine XTF wählen. Die Eigentümer-JSON kann **ausdrücklich zusammen
mit der XTF** ausgewählt werden. Eine danebenliegende Datei wird nicht automatisch gelesen.
Namen werden nur anhand der Originalkennung ergänzt. Aus einer Eigentümerdatei werden
keine Betreiber- oder Firmenrollen abgeleitet.

Die Vorschau zeigt die übernommenen Felder und Quellwerte. Vorhandene Handeingaben werden
auch dann geschützt, wenn sie absichtlich leer sind. Bemerkungen über eine frühere
Sanierung erzeugen kein Ereignis. Nur ein tatsächlicher Unterhalt mit Sanierungsart tut das.

**Zusatzdatei exportieren** schreibt eine neue JSON-Datei mit Akten, Beziehungen,
Bestandsfeldern, Feldherkunft und Übertragungsbericht. **Zusatzdatei einlesen** prüft Format,
Projektkennung und Beziehungen vor der Übernahme. Es werden nur fehlende Werte ergänzt.
Der Weg legt keine Haltungen oder Schächte an und ersetzt keine vorhandene Ausgabedatei.
Er dient derselben Projektidentität, nicht einem Abgleich allein nach Namen.

Auch die vorhandenen XTF-Ausgabewege schreiben bei vorhandenen Akten eine solche Zusatzdatei
in den Ausgabeordner. Die Vorschau weist darauf hin. Scheitert die Zusatzdatei, erscheint die
Ausgabe als unvollständig. Unter **Neue XTF aus dem Projektstand – Exportumfang prüfen**
gehen GeoShop-Angaben jetzt im DSS-Modell hinaus, einschliesslich Deckeln, Koten,
Inliner-Angaben und Sanierungsereignissen. Hinweise stehen in der Vorschau und im XTF-Kopf.
Der reine Änderungsweg bleibt getrennt. Details: [DSS-XTF-Ausgabe](DSS-XTF-EXPORT.md).
Die Normprüfung ersetzt keinen tatsächlichen GEONIS-Rückimport.

## Speicherstand und offene Fachfragen

Projekte ohne neue Akten bleiben im bisherigen Format 2. Sobald Aktenwerte gespeichert
werden, gilt Format 3. Ältere SewerStudio-Versionen lehnen dieses Format ab, statt die neuen
Angaben zu verlieren. Projektkopien und Sicherungen enthalten die Akten in der Projektdatei.

Der [Dropdown-Abgleich vom 12.09.2026](reviews/2026-09-12-webgis/DROPDOWN-ABGLEICH.md)
ergänzt alle 37 Sanierungsverfahren nach Art, die Originalcodes für Deckel/Sanierung,
fünf Haltungspunkt-Auswahlen und neun Witterungswerte. Gleiche Anzeigetexte mit
verschiedenen Codes bleiben getrennt; alte gespeicherte Auswahlen bleiben sichtbar.
Die 269 dokumentierten Wertelisten sind im Katalog enthalten. Nicht ausgezählte
Quelllisten sind im Nachweis ausdrücklich aufgeführt.

Haltungspunkte sind nach erneutem Katasterabgleich über die Objektauswahl erreichbar.
Geänderte belegte Punktattribute werden mit ihrer Originalkennung exportiert.
Das gilt jetzt auch für vorhandene Pumpen, Absperr-/Drosselorgane, Streichwehre,
Leapingwehre, Einstiegshilfen und Trockenwetterfallrohre. Gemeinsam verwendete
Einbauten besitzen nur eine Akte mit mehreren Bezügen. Handeingaben und bewusstes
Leeren bleiben beim erneuten Abgleich erhalten. Eine eigene Bauwerksteilakte
unterdrückt die entsprechende rohe Doppelzeile in der Liste.
Geerbte Schachtfelder bleiben schreibgeschützt und zeigen den aktuellen zugehörigen
Schacht. Gleichnamige Originalattribute eines Pumpenobjekts werden dadurch nicht
umgedeutet. Noch nicht zugeordnete Sachfelder und neue Einbauten ohne Originalbezug sind im [Einbauten-Nachweis](reviews/2026-09-12-webgis/EINBAUTEN-ABNAHME.md) abgegrenzt.
Weiter offen sind vollständige Unterobjektfunktionen, Schachtsanierungsbeispiele,
zusätzliche Bauwerksarten und der genaue GEONIS-Übernahmevertrag. Die neuen
Zusatzangaben ohne belegtes Normziel werden deshalb verlustfrei lokal gehalten.
Bestätigte DSS-Zuordnungen stehen im neuen Exportvertrag. Es gab keine Änderung im WebGIS.

Technische Nachweise: [Prüfbericht](reviews/2026-09-11-objektakten/ABNAHME.md).

## Freie Bearbeitung der gesamten Lieferung

Unter **Export → SIA405-Lieferung bearbeiten …** öffnet der neue Lieferungs-Editor
alle Objekte der Original-XTF in einer getrennten `.ssxtf`-Arbeitsdatei. Er braucht
keine Projektzeile und keinen Schacht-Bauwerksbezug. Sachfelder, vollständige Normlisten,
Beziehungskennungen und Punktkoordinaten sind dort bearbeitbar; Linien und Flächen
bleiben erhalten. Die Projekt-Objektakten werden dadurch nicht automatisch verändert.
[Bedienung, Originalprüfung und verbleibende Aufgaben](LIEFERUNGS-EDITOR.md).
