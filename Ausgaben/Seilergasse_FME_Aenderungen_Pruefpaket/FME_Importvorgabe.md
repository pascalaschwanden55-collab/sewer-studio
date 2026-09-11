# Anleitung für Andreas: Änderungen aus SewerStudio in GEONIS übernehmen

Stand: 7. September 2026.

**Die wichtigste Regel: Nur die ausdrücklich genannten Felder ändern. Niemals alle Felder eines Objekts überschreiben.**

Die XTF-Datei ist vorbereitet. Andreas muss den Ablauf in FME noch einrichten und mit GEONIS testen.

## 1. So läuft die Übergabe

SewerStudio schreibt eine Liste der bearbeiteten Felder in die XTF-Datei.

Jeder Eintrag sagt:

- Welches Objekt ist gemeint?
- Welches Feld soll geändert werden?
- Wann wurde dieses Feld bearbeitet?

Der neue Wert steht ebenfalls in der Datei. FME sucht das passende Objekt in GEONIS. Danach vergleicht FME nur die genannten Felder.

**Ein Objekt ohne Änderungsauftrag darf nicht geändert werden.** Das gilt auch für zusätzliche Hilfsobjekte in der Datei. Diese erklären nur die Verbindungen zwischen den Objekten.

Der FME-Export in SewerStudio ist fest auf Änderungen eingestellt. Ein Vollexport lässt sich dort nicht auswählen. Enthält eine Datei keine Änderungsaufträge, darf dieser FME-Ablauf nichts schreiben.

## 2. Diese Beispiele gelten für jedes Feld

| Situation | Verhalten von FME |
|---|---|
| GEONIS enthält 500. Geliefert wird 600 mit Änderungsauftrag. | Nur dieses Feld auf 600 setzen. |
| GEONIS enthält 500. Geliefert wird ebenfalls 500. | Nichts schreiben. Im Bericht steht „bereits identisch“. |
| Ein Feld wurde nicht bearbeitet. | Das Feld nicht ändern. |
| Ein Feld fehlt in der Lieferung oder ist leer. | Den bisherigen GEONIS-Wert behalten. |
| Die Kennnummer passt zu keinem Objekt. | Überspringen und im Bericht melden. |
| Die Kennnummer passt zu mehreren Objekten. | Überspringen und im Bericht melden. |

**Wichtig zum Beispiel 500 → 500:** SewerStudio merkt sich bisher, dass ein Feld bearbeitet wurde. Es vergleicht noch nicht mit einem gespeicherten Ausgangswert.

Wird 500 gelöscht und wieder 500 eingegeben, kann das Feld deshalb mitgeliefert werden. FME muss den identischen Wert erkennen und unverändert lassen.

Das gilt auch für frühere Lieferungen. SewerStudio bekommt noch keine automatische Bestätigung, dass GEONIS die Änderung erfolgreich übernommen hat.

## 3. Die Dateien öffnen

Die Daten stehen zusammen in einer `.xtf`.

Die Datei `SewerStudio_Zusatz_2026.ili` erklärt FME den Aufbau der zusätzlichen Angaben. Andreas richtet diese Beschreibung einmal in FME ein. Sie enthält keine zweite Datenlieferung.

Schachtform und Änderungsliste sind eigene Ergänzungen von SewerStudio. Sie gehören nicht zum offiziellen SIA405-Modell.

Zum Einlesen verwendet Andreas den FME-Lesebaustein **Swiss INTERLIS (ili2fme)**. Der genaue Name ist wichtig für die Einrichtung.

Dabei gelten diese Einstellungen:

- Die benötigten Modelle aus dem Kopf der XTF-Datei lesen.
- Den Ordner der mitgelieferten `.ili` als Modellordner angeben.
- Auch die offiziellen SIA405-Modelle und ihre benötigten Grundmodelle bereitstellen.
- Den Zusatzbereich der XTF mit einlesen. Er darf nicht ausgefiltert werden.

Die Modellordner beschreibt die [ili2fme-Anleitung](https://www.ili2fme.ch/guide).

Vor der Übernahme prüft FME die Datei. Doppelte Kennnummern, doppelte Änderungsaufträge und ungültige Dateiinhalte müssen gemeldet werden. Eine fehlerhafte Datei darf nicht teilweise übernommen werden.

## 4. Das richtige Objekt finden

Jedes Objekt hat eine Kennnummer. In der XTF heisst diese `TID`. FME zeigt sie am eingelesenen Objekt als `xtf_id`.

FME muss diese Kennnummer mit der **unveränderten internen `SIA405_ID` in GEONIS** vergleichen.

| Schritt | Aufgabe |
|---|---|
| 1 | Die Kennnummer aus dem Änderungsauftrag lesen. |
| 2 | Das zugehörige Objekt in der XTF finden. |
| 3 | In GEONIS dieselbe `SIA405_ID` und die passende Bauwerksart suchen. |
| 4 | Nur bei genau einem Treffer weitermachen. |
| 5 | Den gefundenen Datensatz über seine bestehende `GLOBALID` oder den bestätigten Datenbankschlüssel aktualisieren. |

`GLOBALID` ist eine weitere interne Kennung in GEONIS. Sie wird nicht neu vergeben und nicht verändert.

**Nicht nach Namen ausweichen.** Eine Schachtnummer oder Haltungsbezeichnung kann mehrfach vorkommen.

Bauwerk und Knoten haben eigene Kennnummern. Dasselbe gilt für Kanal und Haltung. Deshalb darf FME diese Kennungen nicht verwechseln.

Auch die ersten Zeichen einer Kennnummer dürfen nicht einfach ausgetauscht werden.

Ein Feld namens `xtf_id` aus dem LISAG-Dienst ist noch kein ausreichender Nachweis. LISAG muss bestätigen, dass es genau die interne `SIA405_ID` enthält.

In FME gibt es ausserdem Sammelbehälter namens `XTF_BASKETS`. Deren `xtf_id` bezeichnet den Behälter, nicht ein Bauwerk. Diese Kennung darf nicht für den GEONIS-Abgleich verwendet werden. Die Unterscheidung steht in der [FME-Beschreibung](https://docs.safe.com/fme/html/FME-Form-Documentation/FME-ReadersWriters/ch.ehi.fme.main/Feature_Representation.htm).

## 5. Die Änderungsliste lesen

Die Änderungsliste heisst in der Datei:

`SewerStudio_Zusatz_2026.Zusatzdaten.Aenderung`

| Technischer Feldname | Einfache Bedeutung |
|---|---|
| `ObjektTid` | Kennnummer des Objekts, das geändert werden soll. |
| `Feld` | Name des Felds, das geändert werden soll. |
| `GeaendertAm` | Gespeicherter Zeitpunkt der Bearbeitung in SewerStudio. Die Zeit ist in UTC angegeben. |

Ein Beispiel aus der Prüflieferung:

| Angabe | Inhalt |
|---|---|
| Objekt | `ch23h1a4Umcgr2UF` |
| Feld | `Zusatz:Schachtform` |
| Neuer Wert | Oval |
| Bearbeitet am | `2026-09-03T13:15:53.2171864Z` |

Bei einem normalen SIA405-Feld steht der neue Wert direkt am betreffenden XTF-Objekt.

Beginnt der Feldname mit `Zusatz:`, steht der Wert hier:

`SewerStudio_Zusatz_2026.Zusatzdaten.Zusatzangabe`

FME sucht dort nach `ObjektTid` und dem Feldnamen ohne `Zusatz:`. Beim Beispiel lautet der Feldname also `Schachtform`. Der neue Inhalt steht im Feld `Wert`.

Die Zusatzangabe besitzt selbst auch eine TID. **Diese TID ist nicht die Kennnummer des GEONIS-Objekts.**

Unsere Änderungsliste ist eine eigene Vereinbarung zwischen SewerStudio und FME. FME darf sie nicht mit dem INTERLIS-Verfahren `xtf_operation` verwechseln.

## 6. Diese Felder können zugeordnet werden

Andreas muss die passenden GEONIS-Felder und Auswahlcodes festlegen. Die Tabelle nennt die fachliche Zuordnung. Sie ist keine Freigabe, immer alle aufgeführten Felder zu schreiben.

**Geschrieben wird ein Feld nur dann, wenn dafür ein Änderungsauftrag vorliegt.**

| Objekt und Feld in der XTF | Bedeutung in GEONIS |
|---|---|
| Kanal: `BaulicherZustand`, `Bemerkung`, `Status`, `Sanierungsbedarf`, `Baujahr` | Zustand, Bemerkung, Betriebsstatus, Sanierungsbedarf und Baujahr. |
| Kanal: `FunktionHierarchisch`, `FunktionHydraulisch`, `Nutzungsart_Ist`, `Verbindungsart`, `Bettung_Umhuellung` | Die jeweiligen Kanalangaben. Die GEONIS-Auswahlcodes müssen dazu passen. |
| Bauwerk oder Kanal: `Standortname`, `Bruttokosten`, `Zustandserhebung_Jahr` | Standort, Bruttokosten und Untersuchungsjahr. Bruttokosten sind nicht die geschätzten Sanierungskosten. |
| Haltung: `Material`, `Lichte_Hoehe`, `LaengeEffektiv`, `Lagebestimmung` | Rohrmaterial, lichte Höhe in mm, Länge in m und Lagegenauigkeit. |
| Normschacht: `Funktion`, `Material`, `Dimension1`, `Dimension2` | Funktion, Schachtmaterial, grösstes und kleinstes Innenmass in mm. |
| Spezialbauwerk: `Funktion` | Funktion des Spezialbauwerks. Dafür gilt eine eigene Auswahlliste. |
| Versickerungsanlage: `Art`, `Dimension1`, `Dimension2` | Art der Versickerungsanlage und ihre Masse. |
| Einleitstelle | Nur die passenden allgemeinen Bauwerksangaben übernehmen. |
| `Zusatz:Schachtform` | Schachtform, zum Beispiel Oval. |
| `Zusatz:Lichte_Breite_mm`, `Zusatz:Profiltyp` | Rohrbreite in mm und Profilform der betroffenen Haltung. |
| Andere Felder mit `Zusatz:` | Nur übernehmen, wenn das GEONIS-Zielfeld vereinbart ist. Sonst „geliefert, nicht zugeordnet“ melden. |
| `Bauwerksart` | Bauwerksart prüfen. Einen Wechsel der Art zuerst zur Prüfung melden. |
| `EigentuemerRef`, `DatenherrRef`, `DatenlieferantRef` | Eigentümer, Datenherr und Datenlieferant. Nur nach ausdrücklich vereinbartem Abgleich ändern. |

Zustand, Bemerkung, Betriebsstatus, Sanierungsbedarf und Baujahr können auch zu den anderen Bauwerksarten gehören. Die in der XTF vorhandene Bauwerksart bestimmt, welche Felder zulässig sind.

Spezialbauwerke haben in diesem Standard keine eigenen Schachtmaterial- oder Dimensionsfelder. Einleitstellen ebenfalls nicht. Versickerungsanlagen haben eine eigene Art statt der Normschacht-Funktion.

FME darf ein bestehendes Bauwerk nicht automatisch in eine andere Bauwerksart umwandeln. Das muss Andreas zuerst prüfen.

## 7. Besondere Regeln für die Werte

**Schachtform:** Wird Oval ausdrücklich geliefert, darf FME daraus nicht Rund machen. Die Form darf nicht allein aus den Massen berechnet werden.

**Bemerkung:** Eine bearbeitete Schachtbemerkung darf ebenfalls übernommen werden. Eine nicht bearbeitete Bemerkung bleibt in GEONIS unverändert.

Das normale SIA405-Feld `Bemerkung` erlaubt höchstens 80 Zeichen. Längere Bemerkungen können als Zusatztext mitkommen. Dafür braucht GEONIS ein passendes, ausreichend langes Feld. Texte nicht abschneiden, sondern einen fehlenden Platz melden.

**Leere Werte:** Fehlend, leer oder `NULL` bedeutet immer: bisherigen Wert behalten. `NULL` bedeutet hier „kein Wert“. Ein bewusster Löschauftrag ist bisher nicht vorgesehen.

**Unbekannte Werte:** Eine Auswahl wie „unbekannt“ darf einen bekannten GEONIS-Wert nicht ungeprüft ersetzen. In einer Bemerkung kann das Wort „unbekannt“ dagegen sinnvoll sein.

**Material und andere Auswahlfelder:** Andreas muss die gelieferten Begriffe den richtigen GEONIS-Codes zuordnen. Ein unbekannter Code wird gemeldet.

**Rohrprofil:** `RohrprofilRef` verweist auf eine Profilbeschreibung in der XTF. Eine Kennung mit `chSST…` darf nicht einfach als GEONIS-Verknüpfung gespeichert werden.

Aus Höhe und Höhen-Breiten-Verhältnis lässt sich die Rohrbreite berechnen: Breite = Höhe / Verhältnis. Beim Kreisprofil ist das Verhältnis 1. FME darf trotzdem nur ausdrücklich beauftragte Breiten- oder Profiländerungen übernehmen.

**Organisationen:** Die XTF kann Eigentümer und andere Organisationen zur Erklärung mitliefern. Das ist kein Auftrag, diese Organisationen in GEONIS anzulegen. Bei einem vereinbarten Rollenwechsel muss FME die passende bestehende GEONIS-Organisation finden.

Geometrie, Netzverbindungen, Hilfspunkte und Kennnummern dürfen nicht nebenbei verändert werden.

## 8. Erst prüfen, dann schreiben

Andreas richtet zuerst einen Probelauf ein. Dieser erstellt einen Bericht und verändert GEONIS nicht.

Im freigegebenen Schreiblauf gilt:

- Nur eindeutig gefundene bestehende Objekte aktualisieren.
- Nur die beauftragten und vereinbarten Felder schreiben.
- Keine neuen Objekte anlegen.
- Keine Objekte, Felder oder Tabellen löschen.
- Zusammengehörige Änderungen gemeinsam übernehmen. Bei einem Fehler darf keine halbe Änderung stehen bleiben.
- Beschriftungen und andere davon abhängige GEONIS-Angaben nachführen. Fehler dabei melden.

Für Andreas heisst die Schreibart in FME `Feature Operation = Update`. Unter `Match Columns` wird der bestätigte Datenbankschlüssel eingetragen.

Die Schreibarten `Insert`, `Upsert` und `Delete` sind für diesen Abgleich nicht erlaubt. Die genaue Einrichtung hängt vom verwendeten GEONIS-Schreibweg ab. Die Begriffe beschreibt die [FME-Anleitung](https://docs.safe.com/fme/2024.2/html/FME-Form-Documentation/FME-ReadersWriters/DatabaseWriterMode/feature_operations.htm).

FME muss vor dem Schreiben prüfen, ob jemand das Objekt inzwischen in GEONIS verändert hat. Bei einem Konflikt wird die Änderung zuerst zur Prüfung gemeldet.

## 9. Drei verschiedene Datumsangaben auseinanderhalten

| Feld | Bedeutung |
|---|---|
| `GeaendertAm` | Wann das Feld in SewerStudio bearbeitet wurde. |
| `Letzte_Aenderung` in dieser XTF | Wann SewerStudio die Datei exportiert hat. |
| `GN_LAST_EDITED_DATE` | Wann das Objekt in GEONIS zuletzt geändert wurde. |

Diese Zeitangaben sind nicht austauschbar.

Den ursprünglichen GEONIS-Zeitpunkt liefert unsere XTF bisher nicht verlässlich. Sobald LISAG diesen Wert bereitstellt, können wir den Vergleich mit dem ursprünglichen GEONIS-Stand ergänzen.

Der Bearbeitungszeitpunkt aus SewerStudio allein schützt also noch nicht vor einem veralteten Ausgangsstand.

## 10. Jede Übernahme protokollieren

Der Bericht soll je Feld zeigen:

| Angabe | Zweck |
|---|---|
| Datei und Laufnummer | Die Lieferung später wiederfinden. Eine Prüfsumme erkennt die genaue Datei. |
| Bauwerksart und Kennnummern | Das betroffene Objekt wiederfinden. `SIA405_ID` und `GLOBALID` festhalten. |
| Feldname | Zeigen, welche Angabe betroffen ist. |
| Alter und neuer Wert | Die Änderung verständlich machen. |
| `GeaendertAm` | Den Bearbeitungszeitpunkt aus SewerStudio festhalten. |
| Ergebnis und Fehlergrund | Zeigen, ob die Übernahme erfolgreich war. |

Mögliche Ergebnisse sind „geändert“, „bereits identisch“, „Kennnummer unbekannt“, „mehrdeutig“, „Bauwerksart passt nicht“ oder „Wert ungültig“. Auch nicht freigegebene Felder und Schreibfehler gehören in den Bericht.

FME soll sich bereits übernommene Änderungen dauerhaft merken. Eine ältere Lieferung darf keine später erfolgreich übernommene Feldänderung zurücksetzen.

SewerStudio setzt seine Bearbeitungsmarkierungen beim Export nicht zurück. Frühere Änderungen können deshalb erneut geliefert werden. Identische Werte darf FME nicht erneut schreiben.

Eine automatische Erfolgsrückmeldung von FME an SewerStudio ist noch nicht eingebaut. Diese Rückmeldung muss später gemeinsam festgelegt werden.

## 11. Die beiliegende Seilergasse-Datei

Datei: `Seilergasse_Aenderungen_Prueflieferung.xtf`

Sie enthält:

- 13 Änderungsaufträge für eine Haltung und einen Schacht.
- Drei betroffene Fachobjekte: Haltung, Kanal und Normschacht.
- Drei Zusatzwerte: Schachtform, Rohrbreite und Profiltyp.
- Weitere Hilfsobjekte für die Verbindungen innerhalb der Datei.

Schachtbemerkung, Strasse und Inspektionsdatum sind hier nicht als Änderungen beauftragt. Sie waren im gespeicherten Projekt nicht als bearbeitet markiert.

**Die Kennnummern sind noch der alte Stand. Vor einem echten GEONIS-Import müssen sie geklärt werden.**

| Objekt | Kennung in der Prüflieferung | Von Andreas genannte aktuelle Kennung |
|---|---|---|
| Haltung | `ch23h1a4uL3A2Sjp` | `ch24gwkdH2Ny5Esg` |
| Kanal | `ch23h1a46oVbkGmT` | `ch24gwkd6oVbkGmT` |
| Knoten | `ch23h1a4ftlGdbHU` | `ch24gwkdftlGdbHU` |
| Bauwerk | `ch23h1a4Umcgr2UF` | `ch24gwkdUmcgr2UF` |

Bei der Haltung sind auch die letzten acht Zeichen anders. Nur die ersten Zeichen auszutauschen wäre falsch.

Die beiden Haltungspunktkennungen sind ebenfalls noch nicht bestätigt. Vor dem Schreiblauf braucht es aktuelle Kennungen oder eine ausdrücklich bestätigte Zuordnung durch Trigonet.

Die Datei hat die INTERLIS-Prüfung bestanden. Geprüft wurden Dateiaufbau, Modellregeln und Verweise. Verwendet wurde ilivalidator 1.15.0 mit `--allObjectsAccessible`.

Das bestätigt noch keine passenden Kennungen oder Werte in der echten GEONIS-Datenbank. Der FME-Import selbst wurde dort noch nicht getestet.
