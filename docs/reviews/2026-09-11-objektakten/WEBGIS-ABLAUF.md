# WebGIS Abwasser Uri – Ablauf und Verhalten der Attributmaske

Stand 12.09.2026. Ergänzt das Gesamtlayout (`WEBGIS-LAYOUT-KOMPLETT.md`) um das, was die
Maske **tut**. Vorgabe für den 1:1-Nachbau im Nova-Stil.

Jede Aussage trägt eine Herkunft:
**[live]** am 12.09.2026 an der Haltung 80089-81361 im WebGIS beobachtet, ohne zu speichern
(Änderungen anschliessend mit „Aktualisieren" verworfen; Serverwerte danach geprüft: unverändert) ·
**[code]** aus dem Editor-Programm `attributeeditor.min.js` gelesen ·
**[layout]** aus den Maskendefinitionen des Servers.

---

## 1. Fenster und Kopf

- Titel „Attributmaske Haltung" bzw. „… Schacht". Oben eine Reiterleiste mit dem Objekttyp
  („Haltung"); öffnet man aus einer Liste heraus ein Unterobjekt, wird sie zum Pfad
  **Haltung › Sanierungsmassnahme** – die Detailmaske ersetzt die Hauptmaske im selben
  Fenster, ein Klick auf „Haltung" führt zurück. [live]
- Werkzeugleiste: Speichern · Löschen · Aktualisieren · Zoom auf Objekt · Schwenken ·
  Identifizieren · Wählen · Validierung ein/aus. Ein Zurück-Knopf gehört zur Historie. [layout]
- Der Kopf ist immer sichtbar (kein Bereich): Bezeichnung (Pflicht) mit Knopf „Beschriftung
  erstellen und positionieren", OBJECTID (nur lesen), Bezeichnung alter./hist. als zwei
  Kästchen, Knoten (von) und Knoten (bis) als Verweise mit Öffnen-Knopf, am Knoten (bis)
  zusätzlich „Knotenbeziehungen aktualisieren". [layout, live]

## 2. Bereiche (Menüs)

- Haltung: Daten I → Daten II → Bauwerksteile → Haltungspunkte → Administrativ → Unterhalt →
  Hydraulik → Metadaten. Schacht zusätzlich Stammkarte und Einbauten. [layout]
- Beim Öffnen sind **alle Bereiche zu**. Es ist **immer genau ein Bereich offen**: Öffnet man
  Daten II, schliesst sich Daten I (Akkordeon). [live]
- Bereichstitel tragen keinen Fehlerzähler; die Gültigkeit wird je Bereich nachgeführt
  (`updateSectionValidity`). [code]

## 3. Felder

- Beschriftung links, Eingabe rechts. Pflichtfelder tragen hinter der Beschriftung einen
  roten Stern; nur Pflichtfelder. [live]
- Kombinierte Beschriftungen (`Anfangs-/Endhöhe`, `Typ AA/Nutzungsart`, `Profiltyp/Breite/Höhe`)
  stehen einmal links; dahinter liegen zwei oder drei Kästchen in **einer Zeile**, getrennt
  durch „/". Das zweite Kästchen hat keine eigene Beschriftung. Einheiten (`[mm]`, `[m³/s]`)
  stehen als Text hinter dem Kästchen. Im Hydraulikteil des Einzugsgebiets bilden
  `SW ¦ RW ¦ MW` und `Geplant SW ¦ RW ¦ MW` zwei Matrixzeilen. [layout]
- Nur-Lese-Felder (OBJECTID, Länge geometrisch, Gefälle, Erstellt/Geändert, Knoten) sind grau
  und nicht editierbar. [layout, live]
- Zahlenfelder prüfen Format und Bereich (`min`/`max`, `dataFormat` wie `0.00`); ein falscher
  Wert macht das Feld ungültig und zeigt bei eingeschalteter Validierung eine Sprechblase
  mit der Meldung. [code]
- Leert man ein Pflichtfeld, wird es rot (`ng-invalid-required`), die Maske ungültig und
  **Speichern bleibt gesperrt**. [live]

## 4. Auswahllisten

- Auswahlfelder sind Suchlisten (Select2): Tippen filtert die Einträge. [live]
- **Nicht-Pflicht**-Auswahlfelder haben einen leeren Eintrag und ein „×" zum Leeren;
  **Pflicht**-Auswahlfelder haben keinen leeren Eintrag – man kann nur umschalten, meist auf
  „Unbekannt". [code: `toSelectOptions` fügt den leeren Eintrag nur bei `required=false` ein; live]
- Werte werden als Code gespeichert, als Text angezeigt (z. B. `102` = „Beton, armiert (BA)"). [layout]

### Abhängige Auswahllisten [live]

Sieben Kindlisten hängen an einem Elternfeld (Haltung und Schacht: Materialgruppe →
Materialdetail; Bauwerksteil: Art → Subart; Unterhalt: Art → Verfahren; Einzugsgebiet:
Entwässerungssystem → Versickerung). Verhalten, an der Haltung geprüft:

1. Der Wechsel der Gruppe lädt die Kindliste **vom Server** nach (`getControlValues` mit
   `filter=<Elterncode>`). Es gibt keine lokale Gesamtliste. [code, live]
2. Der bisherige Detailwert wird **nicht** behalten: das Kind springt auf den **ersten Eintrag
   der neuen Liste**.
   - Beton (Detail `101` Beton, unbekannt) → Kunststoff: Kind = `118` Kunststoff, unbekannt (KUU), 11 Einträge
   - → Unbekannt: Kind = `0` Unbekannt (U), 1 Eintrag
   - → Andere: Kind = `131` Verschiedene (V), 9 Einträge (darunter `132` Zement (Z), `110` Faserzement, `111` Asbestzement)
   - → zurück auf Beton: Kind = `101` (erster Eintrag), 14 Einträge
3. Danach ist die Maske geändert und Speichern freigegeben.
4. Bei einer **nicht-Pflicht**-Kindliste (Versickerung am Einzugsgebiet) ist der erste Eintrag
   der leere – das Kind wird also leer. [code; nicht live geprüft]
5. Elternwerte ohne Kindeinträge (Bauwerksteil „Trockenwetterrinne", Unterhalt „Reinigung",
   Entwässerungssystem „Nicht angeschlossen") liefern eine **leere Liste**; das Kind bleibt
   leer bzw. hat keine Auswahl. [layout]

Die vollständigen Kindlisten aller Elternwerte stehen in `webgis-layout-komplett.json`
(`jeElternwert`) und im Katalog.

### So macht es SewerStudio (12.09.2026)

Entscheid Pascal: gleiches Verhalten wie im WebGIS. `ObjektaktenBearbeitung.Schreibe` zieht nach
jedem Schreiben eines Elternfelds die abhängigen Felder nach
(`ZieheAbhaengigeFelderNach`) — ein Detail aus der alten Gruppe bleibt nicht sichtbar falsch
stehen, sondern springt auf den **ersten Eintrag der neuen Liste**. Drei Unterschiede zum
WebGIS, alle bewusst:

1. **Ein leeres Feld bleibt leer.** Im WebGIS ist das Detail ein Pflichtfeld und immer belegt;
   bei uns heisst leer «nicht erfasst». Ein Gruppenwechsel darf daraus keinen Wert erfinden.
2. **Ein Wert, der auch zur neuen Gruppe gehört, bleibt stehen** (Originalcode zuerst, sonst
   Text). Tritt nur bei eigenen Listeneinträgen auf, weil die Gruppen sonst disjunkt sind.
3. **Eine leere Kindliste** (Elternwert ohne Einträge) lässt den alten Wert stehen — es gibt
   nichts zu setzen. Das Feld zeigt weiter seinen Hinweis.

Nachgezogen wird nur auf dem Handeingabe-Weg der Maske. Importe (GeoShop-XTF, Objektakten-Pakete)
schreiben direkt in `ObjektAkte.Werte` und ziehen nichts nach — ein Import darf die gelieferten
Werte nicht gegenseitig überschreiben. Ein nachgezogener Wert wird wie eine Handeingabe
gespeichert (`VonHand`, bei Bestandsfeldern `FieldSource.Manual` mit `userEdited`) und geht
damit in die XTF-Änderungslieferung — richtig, denn das Material hat sich tatsächlich geändert.

Wächter: `ObjektaktenTests.Gruppenwechsel_zieht_das_abhaengige_Materialdetail_auf_den_ersten_Eintrag_nach`,
`…_erfindet_kein_Material_und_laesst_einen_weiterhin_gueltigen_Wert_stehen`,
`ObjektaktenUnterlistenTests.Wechsel_der_Elterngruppe_zieht_den_Wert_auf_den_ersten_Eintrag_der_neuen_Liste`,
`ObjektakteUiTests.Gruppenwechsel_zeigt_das_nachgezogene_Materialdetail_sofort_in_der_Maske`.

## 5. Aufklapplisten (Untermenüs)

- Jede Liste ist eine Tabelle mit den Spalten der Übersicht (z. B. Sanierungsmassnahmen:
  Beginn · Art · Status · Verfahren), **6 Zeilen je Seite** mit Blättern (Erste · Vorherige ·
  Nächste · Letzte) und einer Gruppieren-Ablage. [layout: `rowcount 6`; live]
- Einfachklick wählt die Zeile. **Doppelklick öffnet den Eintrag als Detailmaske im selben
  Fenster**; oben erscheint der Pfad `Haltung › Sanierungsmassnahme`, Zurück über den Pfad.
  Geprüft an „12.06.2018 · Renovierung · Ausgeführt · Schlauchverfahren": öffnet
  `AWZ_UNTERHALT`, Subtyp `art=4`, mit Kopf (Bezeichnung, Art, Status, Auftrag Nr./Bez.)
  und den Bereichen Daten I, Metadaten. [live]
- Unter jeder **bearbeitbaren** Liste stehen Knöpfe: „Neues Objekt erstellen", „Neue
  Beziehung erstellen" (vorhandenes Objekt zuordnen), „Alle verknüpften Objekte öffnen"
  (Objektliste); mit gewählter Zeile zusätzlich „Objekt löschen" und „Beziehung löschen".
  Inspektionen und GEP Massnahmen haben nur Erstellen und Öffnen. [live, layout]
- **Nur lesende** Listen (Ein-/Ausläufe, Absperr-/Drosselorgane, Pumpen, Überläufe,
  Einzugsgebiete) haben keine Knöpfe; ihre Einträge werden am jeweiligen Objekt gepflegt. [layout]
- Jede Liste kann **mehrere Zeilen** führen (1:n) – auch Sanierungsmassnahmen. [layout, live]

## 6. Neu, Speichern, Löschen

- **Neu** aus einer Liste: hat die Zieltabelle mehrere Masken (Unterhalt: 15 je Art), erscheint
  zuerst „Neuer Datensatz – Subtyp wählen" mit einer Pflicht-Auswahlliste; danach lädt die
  passende Maske. Hat sie nur eine Maske (Bauwerksteil, Deckel), öffnet sie direkt. Der neue
  Datensatz wird beim Anlegen mit dem Elternobjekt verknüpft (Sender-Tabelle/-Relation). [code]
- **Speichern** ist nur aktiv, wenn die Maske geändert **und** gültig ist. [live]
- **Löschen** fragt „Wirklich löschen?" nach. [code]
- **Aktualisieren** lädt die Maske neu und verwirft ungespeicherte Änderungen. [live]

## 7. Knöpfe mit Funktion (Auswahl)

Haltung: Beschriftung erstellen · Knotenbeziehungen aktualisieren · Objektliste öffnen:
Zuweisung · Sohlenkote von Haltungspunkten übernehmen · Bericht „Statistik nach Baujahr/Material".
Schacht: Beschriftung erstellen · Selektierte Objekte referenzieren (Einzugsgebiete) · Berichte
„Bericht", „Datenblätter". Deckel/Pumpe/Überlauf/GEP-Massnahme: Geometrie erstellen/bearbeiten.
Inspektionen: Video starten · Pfad eintragen · Mit Video verknüpfen. GEP-Massnahme: Objekte
referenzieren (3 Python-Aktionen), Referenzen löschen (3), Linie/Fläche erstellen (5).
Vollständige Liste mit Tooltips: `webgis-layout-komplett.json`. [layout]

## 8. Nicht geprüft

- Wechsel der **Art** (Subtyp) an einem bestehenden Unterhaltseintrag – ob die Maske sofort
  umschaltet oder erst nach Speichern.
- Verhalten der nicht-Pflicht-Kindliste beim Elternwechsel (Punkt 4.4 stammt aus dem Code).
- Datei-Anhänge und Upload.
