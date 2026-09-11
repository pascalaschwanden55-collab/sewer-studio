# Redesign-Audit: Felder, Dropdowns und SIA-405-XTF

**Ergebnis: Noch nicht abnahmebereit.** Die Haltungs-Wertelisten stimmen weitgehend mit der Norm überein. Bei den Schachtfeldern fehlen vier Dropdown-Zuordnungen. Eine XTF mit unzulässigen Schachtmassen wird als erfolgreich ausgegeben. Zudem passt die deklarierte Modellfassung nicht zu allen geschriebenen Begriffen. Bei neuen Haltungen fehlen Anfangs- und Endschacht im Formular.

Stand: 08.09.2026, Abschluss am Abend. Geprüft wurde der lokale Arbeitsstand auf `feature/eval-pruefsatz-review`, Basiscommit `0d3ea8d89`, einschliesslich der vorhandenen uncommitteten Änderungen. Produktcode und Kundenoriginale wurden bei diesem Audit nicht geändert.

## Befunde nach Bedeutung

### F01 · Hoch · Vier Schachtfelder sind Texteingaben statt Dropdowns

Betroffen: **Funktion, Material, Status und Sanierungsbedarf**. Die Laufzeitprobe ruft den tatsächlichen Schacht-Detailbuilder auf. Für alle vier Felder ergibt sich `IsCombo=false`. Bauwerksart, Versickerungsart, Schachtform und Belastungsklasse ergeben dagegen korrekt `IsCombo=true`.

[SchaechteColumnPolicy.cs:68](C:/Sewer-Studio_KI_4.5/src/AuswertungPro.Next.UI/DataPage/SchaechteColumnPolicy.cs:68) ordnet die vier Feldnamen keiner Auswahl zu. Der gemeinsame Detailbuilder erzeugt deshalb Texteingaben. Funktion und Material haben bereits Wertelisten. Für Status und Sanierungsbedarf fehlt zusätzlich die Weitergabe der passenden Optionen auf der Schachtseite.

**Auswirkung:** Die vollständigen Begriffe werden nicht angeboten; beliebige Schreibweisen lassen sich erfassen. Das betrifft die aufgeklappte Liste und den gemeinsamen Detailweg. Auch die Tabellenzuordnung verwendet diese Regel.

**Korrektur:** Alle vier Felder vollständig anschliessen. Funktionen zusätzlich nach Bauwerksart filtern: Normschacht und Spezialbauwerk besitzen unterschiedliche Wertelisten. Die derzeit zusammengeführte Funktionsliste einfach anzuschliessen genügt nicht.

### F02 · Hoch · Bei neuen Haltungen fehlen Schacht oben und Schacht unten

Eine neue Haltung wurde über `Project.CreateNewRecord()` erzeugt, also denselben Weg wie der Neu-Befehl. Der echte Formularbuilder liefert 53 Felder, aber weder `Schacht_oben` noch `Schacht_unten`.

Die beiden Felder stehen zwar in der vorgesehenen Stammdaten-Reihenfolge, werden aber nur erzeugt, wenn sie schon im Datensatz vorhanden sind. Sie fehlen in `FieldCatalog.ColumnOrder`, aus der der neue Datensatz und die Grundliste des Formulars entstehen. Bei importierten Haltungen mit vorhandenen Schachtfeldern fällt die Lücke deshalb nicht auf.

Belege: [DataPageRecordDetailsBuilder.cs:77](C:/Sewer-Studio_KI_4.5/src/AuswertungPro.Next.UI/DataPage/DataPageRecordDetailsBuilder.cs:77), [FieldCatalog.cs:12](C:/Sewer-Studio_KI_4.5/src/AuswertungPro.Next.Domain/Models/FieldCatalog.cs:12), [Project.cs:216](C:/Sewer-Studio_KI_4.5/src/AuswertungPro.Next.Domain/Models/Project.cs:216).

**Auswirkung:** Anfangs- und Endschacht können im neuen Formular nicht getrennt erfasst werden. Diese Angaben werden für Bezeichnung und Netzverknüpfung gebraucht. Auch „Ansicht anpassen“ kann ein gar nicht erzeugtes Feld nicht sichtbar machen.

**Korrektur:** Beide Felder wie das Gefälle unabhängig vom bisherigen Dateninhalt erzeugen. Den Neuanlagefall zusätzlich zu importierten Beständen prüfen.

### F03 · Hoch · Schachtmasse ausserhalb der Norm werden erfolgreich exportiert

**Gegenprobe:** Künstlicher Normschacht mit beiden Innenmassen `4500` mm. Der echte Planer und XTF-Schreiber schreiben `Dimension1=4500` und `Dimension2=4500`. Ergebnis: `Ok=true`, kein Fehler und kein Hinweis zum unzulässigen Mass.

Das verwendete Basismodell erlaubt für **Abmessung nur 0 bis 4000 mm**. Normschacht.Dimension1/Dimension2 und die entsprechenden Masse der Versickerungsanlage verwenden diesen Typ. Dieser Bereich unterscheidet sich von der lichten Rohrhöhe. Belege: [offizielles Basismodell 2020](https://www.vsa.ch/models/2020/SIA405_Base_Abwasser-20201103.ili), Zeile 56, und [Basismodell 2020_1](https://www.vsa.ch/models/2020_1/SIA405_Base_Abwasser_1_2_d_LV95-20231018.ili), Zeile 59.

[XtfSchachtPlanBuilder.cs:189](C:/Sewer-Studio_KI_4.5/src/AuswertungPro.Next.Application/Xtf/XtfSchachtPlanBuilder.cs:189) prüft nur positive Masse. [XtfBauwerkFelder.cs:33](C:/Sewer-Studio_KI_4.5/src/AuswertungPro.Next.Application/Xtf/XtfBauwerkFelder.cs:33) übernimmt sie unverändert. Auch der Revisionsweg schreibt über dieselbe Massregel.

**Korrektur:** Bereich anhand von Objektklasse und Zielmodell prüfen, schon am Eingabefeld erklären und vor dem Export erneut absichern. Reale grössere Bauwerke dürfen nicht durch Kürzen oder Raten passend gemacht werden.

### F04 · Hoch · Neuere Begriffe unter älterem Modellkopf

Der Exportkopf nennt `SIA405_ABWASSER_2020_LV95`, Version **26.06.2021**: [XtfNeuWriter.cs:148](C:/Sewer-Studio_KI_4.5/src/AuswertungPro.Next.Infrastructure/Import/Xtf/XtfNeuWriter.cs:148).

Die Spezialbauwerk-Liste enthält `Kombischacht`: [AbwasserbauwerkVokabular.cs:22](C:/Sewer-Studio_KI_4.5/src/AuswertungPro.Next.Domain/Models/AbwasserbauwerkVokabular.cs:22). In der deklarierten Fassung 2021 gehört dieser Wert nur zu Normschacht.Funktion, **nicht** zu Spezialbauwerk.Funktion. In der Fassung 2025 ist er auch dort enthalten.

**Gegenprobe:** Bauwerksart Spezialbauwerk, Funktion Kombischacht. Der echte Schreiber meldet Erfolg und schreibt diese Kombination unter dem Modellkopf 2021.

**Auswirkung:** Die Datei widerspricht ihrer deklarierten Modellfassung. Ein Empfänger mit der älteren Fassung kann sie zurückweisen. Ein Empfänger mit der neuesten gleichnamigen Fassung kann den Wert akzeptieren; die falsche Versionsangabe bleibt bestehen.

**Korrektur:** Zielmodell festlegen und Wertelisten, Objektklassen, Kopf sowie Prüfung gemeinsam daran binden. Quellen: [Modell 2021](https://www.vsa.ch/models/2020/precursorVersion/old/SIA405_Abwasser_2020_2_d_LV95-20210626.ili), [Modell 2025](https://www.vsa.ch/models/2020/SIA405_Abwasser_2020_2_d_LV95-20251129.ili).

### F05 · Hoch · Schadensstufen 4 und 5 im Training Studio abgeschnitten

Eine erneute isolierte Sichtprobe mit dem aktuellen Programm bei 1920 × 1080 zeigt rechts nur die Stufen 1–3. Stufen 4 und 5 liegen ausserhalb der sichtbaren Fläche. Die fünf Knöpfe besitzen kleine Wunschbreiten, erben aber eine grössere Mindestbreite.

[TrainingStudioWindow.xaml:507](C:/Sewer-Studio_KI_4.5/src/AuswertungPro.Next.UI/Views/Windows/TrainingStudioWindow.xaml:507). Neues Bild: [Training Studio](nachweise/TrainingStudio.png). Der frühere Befund besteht damit weiterhin.

**Korrektur:** Fünf tatsächlich passende Spalten beziehungsweise passende Mindestbreiten; ihre sichtbaren Grenzen auf Full HD und bei Windows-Skalierung prüfen. Die Schadensstufen des Trainings sind eine andere Skala als die Zustandsklassen Z0–Z4.

### F06 · Mittel · Millimeter werden teilweise als Meter interpretiert

Die neuen Massfelder sind ausdrücklich mit Millimetern beschriftet. Der gemeinsame Konverter multipliziert Zahlen bis einschliesslich 10 dennoch mit 1000. Selbst **`10 mm` wird zu `10000` mm**. Die Laufzeitprobe bestätigt das.

[SiaAbmessung.cs:68](C:/Sewer-Studio_KI_4.5/src/AuswertungPro.Next.Domain/Models/SiaAbmessung.cs:68). Die Regel ist für alte gemischte Meter-/Millimeterangaben gedacht, passt aber nicht eindeutig zu neuen Eingaben in beschrifteten Millimeterfeldern.

**Korrektur:** Neue Eingaben mit ihrer tatsächlichen Einheit verarbeiten. Die alte Heuristik nur für nachweisliche Alt-/Importfelder verwenden. Ungültige Werte direkt melden.

### F07 · Mittel · Bekannte Schachtfelder stehen in falschen Themen

Die erneut aufgerufene Zuordnungsregel liefert:

| Feld | Aktuell | Redesign-Vorgabe |
|---|---|---|
| Baujahr | Weitere Angaben | Stammdaten |
| Belastungsklasse | Weitere Angaben | Zustand und Inspektion |
| Inspektionsdatum | Weitere Angaben | Zustand und Inspektion |
| Primäre Schäden | Weitere Angaben | Zustand und Inspektion |
| Fotos | Weitere Angaben | Dokumente und Medien |
| Bemerkungen | Weitere Angaben | Sanierung und Kosten |
| Ausgeführt durch | Weitere Angaben | Sanierung und Kosten |
| Eigentümer | Stammdaten | Sanierung und Kosten |

Keine verlorenen Daten, aber unnötig schwer auffindbare Felder. [SchaechteColumnPolicy.cs:185](C:/Sewer-Studio_KI_4.5/src/AuswertungPro.Next.UI/DataPage/SchaechteColumnPolicy.cs:185) verwendet unvollständige Wortvergleiche. Die Vorgabe ist im früheren [Gesamtaudit](../2026-09-08-redesign-gesamtaudit/PRUEFBERICHT.md) mit dem Prototyp-Inventar abgeglichen.

### F08 · Niedrig · Inspektionsrichtung fehlt in „Stammdaten“-Spaltenansicht

Die Angabe existiert im Formular und Katalog, fehlt aber im festen Spaltensatz: [DataPageColumnViewCatalog.cs:55](C:/Sewer-Studio_KI_4.5/src/AuswertungPro.Next.UI/DataPage/DataPageColumnViewCatalog.cs:55). Kein Datenverlust; die geplante Ansicht ist unvollständig.

## Bewusste Bestandsregeln mit fachlicher Bedeutung

Diese Regeln sind nicht heimlich korrigiert worden. Sie dürfen aber nicht als exakte Normabbildung gelten:

- **Fettabscheider → andere:** Beim Normschacht geht der genaue Begriff verloren, obwohl die Norm Fettabscheider kennt. Codekommentare dokumentieren eine frühere Nutzerentscheidung. Beim Spezialbauwerk bleibt Fettabscheider erhalten. [SchachtFunktionVokabular.cs](C:/Sewer-Studio_KI_4.5/src/AuswertungPro.Next.Domain/Models/SchachtFunktionVokabular.cs).
- **Unbekannt und leer:** Schachtfunktion/-material schreiben `unbekannt` nicht. Z0–Z4 werden korrekt geschrieben; ein expliziter Normwert `unbekannt` fehlt in der Zustandsauswahl. Leer und unbekannt sind unterschiedliche Aussagen. Leeren bedeutet bei der Revision auch keine Löschung: Eine geleerte Haltungsbemerkung erzeugte null Änderungen und null Hinweise; der alte XTF-Text bleibt stehen. Das entspricht der bestehenden Regel, muss im Bedienablauf aber klar sein.
- **GFK und Guss:** Lokale Materialbegriffe ohne eindeutigen Standardwert im untersuchten Modell. Der Export nennt sie; der Ergänzungsweg kann sie im Zusatzmodell erhalten. Das ist keine exakte Abbildung in SIA405.Haltung.Material.
- **Zusatzmodell:** Sanierungsangaben, Prüfungsresultate, Schachtform, Belastungsklasse und weitere eigene Angaben stehen teilweise in `SewerStudio_Zusatz_2026`. Ein Empfänger muss dieses Modell und seine Feldzuordnung unterstützen. „In derselben XTF“ bedeutet nicht „normales SIA-405-Attribut“. Der Export nennt diese Grenze bereits.

## Was nachweislich richtig ist

- Alle **53 Katalogfelder einschliesslich Gefälle** erscheinen im neu aufgebauten Standardformular. Das ist kein Vollständigkeitsbeweis: Die zusätzlich vorgesehenen Schachtfelder fehlen bei neuen Haltungen (F02). Gewünschte persönliche Ausblendungen wurden nicht als fehlende Felder gewertet.
- Die Haltungslisten decken die Normwerte für Rohrmaterial (24), Profiltyp (7), Nutzungsart (9), funktionale Hierarchie (14), Verbindungsart (13), Bettung/Umhüllung (14), hydraulische Funktion (12), Status (5), Sanierungsbedarf (6) und Lagebestimmung (3) ab. GFK/Guss sind zusätzliche lokale Ausnahmen. Z0–Z4 werden korrekt übertragen.
- Diese Listen wurden gegen die deklarierte Fassung 2021 und die beiden Varianten 2020/2020_1 vom 29.11.2025 verglichen. Sie stimmen in diesen Fassungen überein. Die elf Versickerungsarten stimmen ebenfalls überein. Bei Spezialbauwerken wurde durch den Versionsvergleich F04 sichtbar.
- Lesbare Anzeigen mit Umlauten oder Leerzeichen sind zulässig, wenn der Export den exakten Normwert schreibt. Die vorhandenen Vokabulare erfüllen das für die genannten regulären Haltungswerte.
- Schachtmaterial und Rohrmaterial sind fachlich getrennt. Lichte Rohrhöhe und Höhen-/Breitenverhältnis besitzen unterschiedliche Exportziele. Zustandsklasse und Sanierungsbedarf sind getrennte Felder.

Alle geprüften Feldnamen, Dropdownbegriffe und Exportwerte stehen im [Feld- und Dropdown-Inventar](FELD-UND-DROPDOWN-INVENTAR.md). Die [Feldliste als CSV](HALTUNGSFELDER.csv) eignet sich zum Abhaken.

## Sind alle notwendigen SIA-Felder vorhanden?

**Für einen vollständigen SIA-405-Katastereditor: nein.** SewerStudio bildet eine Inspektions- und Auswertungsmaske mit ausgewählten Katasterfeldern ab. Zusätzliche Modellattribute wie geplante Nutzungsart, Einzelrohrlänge, Reliner-Art/-Material, Deckelkote und Sohlenkote besitzen nicht durchgehend einen bestätigten eigenen Eingabe- und Exportweg.

Diese Attribute sind in den untersuchten Klassen optional. Ihr Fehlen bedeutet allein keine ungültige XTF. Das Modell verlangt insbesondere Bezeichnungen und bestimmte Objektverweise. Der Export erzeugt dafür einen Objektverbund und prüft Namen sowie Organisationen. Weitere notwendige Angaben ergeben sich aus dem konkreten Lieferprofil und den Anforderungen des Empfängers. Der Redesign-Prototyp allein definiert diese fachliche Vollständigkeit nicht.

Die Beispiele fehlender optionaler Felder sind daher keine pauschale Pflichtfeld-Mängelliste. Die Fehler bei bereits unterstützten Schachtfeldern sind dagegen konkret nachgewiesen.

## Prüfung und Grenzen

| Prüfung dieses Audits | Ergebnis |
|---|---:|
| Oberflächen-, Dropdown-, Formular-, Listen- und ausgewählte Redesign-Tests | 394 bestanden, 3 übersprungen |
| XTF-, SIA-, Vokabular- und Dropdown-Exporttests | 814 bestanden, 1 übersprungen |
| Tatsächlicher Formularbuilder für 16 Schachtfelder | F01 bestätigt |
| Exakter Begriffsvergleich mit offiziellen Modellen | F04 und Bestandsabweichungen bestätigt |
| Echter XTF-Schreiber mit künstlichen Problemfällen | F03/F04 bestätigt |
| Isoliertes Training Studio bei Full HD | F05 bestätigt |

**1’208 vorhandene Tests bestanden, keiner schlug fehl. Trotzdem bestehen die beschriebenen Fehler.** Die vorhandenen Tests decken die betroffenen Grenzfälle und die tatsächliche Dropdown-Verkabelung nicht ausreichend ab.

Kein eigenständiger INTERLIS-Validator wurde ausgeführt. Massen- und Begriffsbefunde beruhen auf den offiziellen Modelldateien und tatsächlich geschriebenen XML-Dateien. Eine formelle Vollprüfung aller Geometrien, Beziehungen, Eindeutigkeiten und Kundenlieferungen ist damit nicht behauptet. Der gesamte VSA-KEK-Ereigniskatalog wurde in diesem Feldabgleich nicht neu zertifiziert.

Die Grafiken und Fotoklicks wurden im unmittelbar vorherigen Änderungslauf geprüft: [Grafik und Fotos](../2026-09-06-nova/aufklapp-liste/GRAFIK-UND-FOTOS.md). Diese Ergebnisse sind getrennt von den neuen Gegenproben dokumentiert. Frühere Berichte werden nicht pauschal als aktuelle Abnahme übernommen.

Alle neuen Gegenproben verwenden künstliche Daten und ein eigenes Prüfprofil. Lokale Skripte, Modellkopien und Laufprotokolle liegen unter `.tmp/redesign-sia405-audit/`; wesentliche Ergebnisse sind unter `nachweise/` kopiert. Die Prüfhilfe meldete NU1900 wegen einer nicht erreichbaren NuGet-Sicherheitsabfrage, lief aber mit den vorhandenen Programmdateien erfolgreich. Keine neuen Programmpakete. Kein Commit oder Upload.

## Weg zur Abnahme

1. Ungültige Masse und gemischte Modellfassungen verhindern; die Gegenproben als feste Tests übernehmen.
2. Die vier Schacht-Dropdowns vollständig verbinden, fehlende Schachtfelder bei neuen Haltungen ergänzen, je Bauwerksart richtige Begriffe anbieten und Einheiten eindeutig behandeln.
3. Bewusste Abweichungen wie Fettabscheider/andere und unbekannt/leer fachlich festlegen und sichtbar erklären.
4. Feldgruppen, Spaltenansicht und Training-Studio-Breite korrigieren. Danach repräsentative Lieferungen mit dem festgelegten Zielmodell formal validieren und beim vorgesehenen Empfänger prüfen.
