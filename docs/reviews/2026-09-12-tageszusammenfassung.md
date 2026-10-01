# Tageszusammenfassung zum Prüfen – 12. September 2026

Stand: Ende des Arbeitstags, Europe/Zurich. Zusammenfassung der heutigen SewerStudio-Aufgaben
„Prüfe WebGIS-Integration“, „WebGIS-Abgleich und XTF-Export“ und
„Spalte Nr. bei Schächten ergänzen“ (heute: Sicherung).
Grundlage sind die Aufgabenverläufe, Prüfberichte und vorhandenen Testprotokolle.
Für diese Zusammenfassung wurden keine neuen Programmtests oder Kundenänderungen ausgeführt.

**Ergebnis:** Mehrere Fehler sind behoben, der Lieferungs-Editor ist eingebaut und die Sicherungen sind geprüft.
Der vollständige WebGIS-Nachbau und eine freigegebene Gesamt-XTF sind noch nicht fertig.

## Heute umgesetzt

| Bereich | Änderung und Verhalten |
| --- | --- |
| Katasterabgleich | Auch der Einzelabgleich sperrt doppelte Haltungs- und Schachtnamen. Nachträgliche Änderungen machen eine alte Vorschau ungültig. |
| Exportprüfung | Fehlende Objektarten, ungültige Bezüge und nicht übertragbare Angaben werden sichtbar genannt. Unvollständige Ausgaben werden gesperrt. |
| Unterhalt | Belegte Unterhaltsereignisse lassen sich importieren, bearbeiten und als eigene Objekte exportieren. Kennungen und Beziehungen bleiben erhalten. |
| Material | 68 angebotene Materialdetails eingeordnet: 27 mit geprüftem Normziel, 41 fachlich offen. Offene Zuordnungen werden nicht still ersetzt. |
| Objektmasken | Pflicht-, Längen-, Zahlen- und Datumsregeln ergänzt. Weitere Feldbereiche zugeordnet; Listen mit sechs Zeilen und einzeln aufklappbaren Bereichen. |
| Dropdowns | 269 dokumentierte Dropdown-Vorkommen abgeglichen. Ergänzt: 24 Sanierungsverfahren, fünf Haltungspunkt-Auswahlen und neun Witterungswerte. Originalcodes und alte Auswahlen bleiben erhalten. |
| Haltungspunkte | Eigene Punktakten mit 15 Feldern angebunden. Belegte Sachattribute und Originalkennungen werden im Export berücksichtigt. |
| Einbauten | Pumpen, Drosselorgane, beide Wehrarten, Einstiegshilfen und Fallrohre im vorhandenen Objektverbund angebunden. Falsche Klassenwechsel werden gesperrt. |
| Lieferungs-Editor | Gesamte gelieferte XTF in einer eigenen Arbeitsdatei öffnen, durchsuchen, bearbeiten, prüfen und bei fehlerfreiem Stand neu ausgeben. |
| Nova-Darstellung | Der neue Editor verwendet die vorhandenen Nova-Bausteine. Hell/Dunkel und zwei Fensterbreiten wurden geprüft. Bestehende Masken und Kataloge wurden weiterverwendet. |
| Sicherungsfehler | Mehrere PDFs im Feld `PDF_All` werden jetzt einzeln gesichert. Semikolonlisten, gespeicherte JSON-Listen und echte Arrays sind berücksichtigt. |

## Lieferungs-Editor: Was du prüfen kannst

Einstieg: **Export → SIA405-Lieferung bearbeiten …**

Vorbereitete Arbeitsdatei:
`C:\Users\Besitzer\Documents\SewerStudio\Lieferungen\order-20260912.ssxtf`

- Alle **630'246 Objekte und Beziehungen aus 22 gelieferten Objektarten** sind übernommen.
- Sachfelder, Norm-Dropdowns, Beziehungskennungen und Punktkoordinaten sind bearbeitbar.
- Speichern, Verwerfen, Wiederöffnen und Schutz vor ungespeichertem Objektwechsel sind vorhanden.
- Die Trefferliste zeigt 100 Einträge je Seite. Fehler lassen sich filtern und als Bericht speichern.
- Originalkennungen und Klassen bleiben fest. Linien und Flächen bleiben erhalten, sind aber noch nicht bearbeitbar.
- Änderungen liegen in der `.ssxtf`, getrennt vom Original und vom normalen Projekt-JSON.

**Die tatsächliche Gesamt-XTF bleibt gesperrt:** Die aktuelle Prüfung meldet **99'795 Objekte mit mindestens einem Fehler**.
Das ist die Anzahl betroffener Objekte, nicht die Gesamtzahl aller einzelnen Fehler.
Beispiele sind fehlende Pflichtbezeichnungen, ungeklärte Werte und fehlende Beziehungen.
Die Originaldatei wurde nicht verändert. Ein Rückimport ins WebGIS/GEONIS fand nicht statt.

## Heute geprüfte und ergänzte Sicherungen

| Sicherung | Ergebnis und Ablage |
| --- | --- |
| Bestehende Vollsicherung von 00:15 Uhr | Rund 533 GB; **233'556 Dateien vollständig durch Prüfsummen geprüft, keine Inhaltsabweichungen**. Ablage: `G:\Systemschutz\SewerStudio_Datensicherung`. |
| Ladeprobe | Wissensdatenbank mit 1'684 Beispielen lesbar. Gesichertes Projekt Dorfstrasse Seelisberg mit 41 Haltungen erfolgreich geladen. |
| PDF-Ergänzung | Zwölf in der Vollsicherung fehlende PDF-Kopien ergänzt, zusammen rund 11 MB. Jede Kopie geprüft. Ablage: `G:\Systemschutz\Ergaenzung_PDF_2026-09-12`. |
| Neuer Programmstand | **19'303 Dateien, rund 5,1 GB**, Archivprüfung bestanden. Ablage: `G:\Programmsicherungen\SewerStudio\SewerStudio_Programm_2026-09-12_223403.zip`. Prüfsummendatei liegt daneben. |
| Prüfberichte | Zusätzlich abgelegt unter `G:\Systemschutz\Pruefberichte\2026-09-12_2240`. |

Die Vollsicherung enthält Videos, GeoShop und eigene QGIS-Erweiterungen.
Sie wurde heute vollständig geprüft, aber während dieser Arbeit nicht neu erstellt.
Die ZIP enthält den später korrigierten Programmstand; sie ersetzt keine Projektsicherung.
Alte Sicherungen wurden nicht gelöscht. Auf G: bleiben rund **61,9 GiB frei**.
Ein echter Stromausfall und ein kompletter Ersatz-PC wurden nicht getestet.

## Offene Punkte – wichtig für die Freigabe

1. **Lieferungs-Arbeitsdatei sichern.** Die neue `.ssxtf` umfasst rund 731 MB und liegt ausserhalb der eingetragenen Zusatzordner. Ihr Dateiname fehlt im Manifest der geprüften Vollsicherung. Ihre Aufnahme in die regelmässige Sicherung ist noch offen.
2. **127 Dateiverweise klären.** An diesen gespeicherten Pfaden liegt keine Datei. Für 109 gibt es gleichnamige Treffer an anderen Stellen, für 46 genau einen. Das beweist noch nicht die richtige Zuordnung. Projektverweise wurden nicht automatisch ersetzt.
3. **Fünf doppelte Haltungskennungen klären.** Jede steht in der Original-Lieferung bei zwei unterschiedlichen Kanalzuordnungen. Der richtige Bezug muss fachlich festgelegt werden.
4. **41 Materialzuordnungen entscheiden.** Auch offene Unterhaltsarten und Statuswerte benötigen eine fachliche Normzuordnung. Details werden nicht einfach zu „andere“ oder „unbekannt“ umgeschrieben.
5. **Lieferungsfehler bereinigen.** Die 99'795 beanstandeten Objekte und 26 externen Organisationskennungen müssen geklärt werden. Danach folgen die Prüfung der Gesamt-XTF und der abgestimmte GEONIS-/FME-Rückimport.
6. **WebGIS-Nachbau vervollständigen.** Offen sind unter anderem weitere Objektfunktionen, Dokumentaktionen, Unterlisten sowie Linien- und Flächenbearbeitung. Im freien Editor fehlen Neuanlage, Dublettenlöschung und Klassenwechsel. Fünf Dropdown-Quelllisten haben noch keinen vollständigen direkten Nachweis.
7. **Änderungen als geprüften Entwicklungsstand festhalten.** Der aktuelle Arbeitsbaum enthält noch nicht eingecheckte Änderungen. Die heutigen Pakete wurden nicht als eigener Commit veröffentlicht. Ein vollständiger abschliessender Testlauf aller Projekte am Endstand ist nicht nachgewiesen.

## Deine praktische Prüfliste

Bitte Änderungen auf einer Kopie der Arbeitsdatei oder in einem Testprojekt prüfen.

- [ ] Lieferungs-Editor über die Exportseite öffnen; Arbeitsdatei laden und mehrere Objektarten aufrufen.
- [ ] Nach einem bekannten Objekt suchen; auch die nächste Trefferseite prüfen.
- [ ] Einen Sachwert ändern, speichern, schliessen und wieder öffnen: Wert bleibt erhalten.
- [ ] Einen ungespeicherten Wert ändern und das Objekt wechseln: Verlust der Eingabe wird verhindert.
- [ ] Änderungen verwerfen: Nur die offenen Eingaben werden zurückgenommen.
- [ ] Dropdowns bis zum letzten Eintrag prüfen; auch abhängige Unterlisten, Leerwerte und alte Werte ansehen.
- [ ] Nova in Hell und Dunkel sowie bei schmalem Fenster prüfen: Inhalte bleiben erreichbar, Auswahl bleibt erhalten.
- [ ] In einem Testprojekt doppelte Haltungsnamen anlegen: Der Einzelabgleich muss die Zuordnung sperren.
- [ ] Unterhalt oder Einbau bearbeiten: Exportprüfung nennt offene Angaben und erhält die ursprüngliche Kennung.
- [ ] Lieferung prüfen und Fehlerfilter öffnen: Betroffene Objekte und Bericht sind erreichbar.
- [ ] Bei offenen Fehlern „Neue XTF schreiben …“ versuchen: Es darf keine freigegebene Gesamtdatei entstehen.
- [ ] `.ssxtf`-Sicherung, mögliche Dateitreffer und offene Materialentscheidungen separat nachprüfen.

## Nachgewiesene Prüfungen

| Paket | Protokolliertes Ergebnis |
| --- | --- |
| WebGIS/XTF, letzter Fachtestlauf | 718 bestanden; ein Live-Test ausgelassen. |
| WebGIS/XTF, letzter Oberflächenlauf | 146 bestanden. Vier Kindtests wurden über ihre erfolgreichen Elterntests ausgeführt. |
| Nova-Anpassung | 22 gezielte Tests bestanden. Ein Kindtest lief über den Elterntest. Sichtprüfung in Hell/Dunkel bei 1240 und 900 Pixeln. |
| Sicherungskorrektur | 227 Tests bestanden, keiner ausgelassen. Die drei neuen Listentests scheiterten vor der Korrektur und bestehen danach. |
| Normausgabe | Künstliche Testdateien bestanden ilivalidator. Auch eine bearbeitete Lieferung mit 29 Objekten/Beziehungen besteht. Das ist keine Freigabe der echten Gesamtlieferung. |
| Programm-Build | Dev-Release-Build erfolgreich. Je nach Lauf ein bis zwei bestehende Nullbarkeitswarnungen in anderen Dateien; keine Buildfehler. |

**Die Testzahlen nicht addieren:** Die Auswahl überschneidet sich zwischen den Läufen.
Frühere Zwischenzahlen sind durch die späteren Paketprüfungen ersetzt.

## Belege zum Nachlesen

- [WebGIS: Umsetzungsstand und Grenzen](C:/Sewer-Studio_KI_4.5/docs/reviews/2026-09-12-webgis/UMSETZUNGSSTAND.md)
- [Lieferungs-Editor: Bedienung und tatsächliche Lieferungsprüfung](C:/Sewer-Studio_KI_4.5/docs/LIEFERUNGS-EDITOR.md)
- [Dropdown-Abgleich](C:/Sewer-Studio_KI_4.5/docs/reviews/2026-09-12-webgis/DROPDOWN-ABGLEICH.md)
- [Materialentscheidungen](C:/Sewer-Studio_KI_4.5/docs/reviews/2026-09-12-webgis/MATERIAL-ENTSCHEIDUNGEN.md)
- [Nova-Sichtprüfung](C:/Sewer-Studio_KI_4.5/docs/reviews/2026-09-12-webgis/NOVA-LIEFERUNGS-EDITOR.md)
- [Sicherungsbericht](C:/Sewer-Studio_KI_4.5/docs/reviews/2026-09-12-sicherung-nachpruefung.md)
- [Mögliche Treffer zu fehlenden Dateiverweisen](C:/Sewer-Studio_KI_4.5/docs/reviews/2026-09-12-sicherung-moegliche-dateitreffer.csv)
