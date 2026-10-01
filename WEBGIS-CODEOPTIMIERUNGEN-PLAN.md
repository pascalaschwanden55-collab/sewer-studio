# Plan: Direkten WebGIS-Import und -Export absichern

**Stand:** 28.09.2026  
**Grundlage:** [WebGIS-Robustheitsprüfung](docs/audits/2026-09-28-webgis-robustheit/BERICHT.md) und aktueller Code.  
**Status:** Nur Plan. Kein Produktcode und keine WebGIS-Daten wurden geändert.

## Ziel

Ein WebGIS-Schreibauftrag darf nur vollständig und eindeutig an GEONIS gehen. Danach muss sichtbar sein, ob die Daten tatsächlich gespeichert wurden oder der Ausgang noch offen ist. Beim Holen dürfen Daten nur ins richtige, weiterhin offene SewerStudio-Projekt gelangen. Laufzeit und Codeaufbau werden erst nach diesen Sicherungen verbessert.

**Grenze:** Eine absolute 100-%-Garantie gegen gleichzeitige Fremdänderungen braucht eine serverseitige Versionsprüfung oder Sperre. Ob GEONIS das bietet, muss mit Trigonet geklärt werden. Bis dahin bleibt zwischen letztem Lesen und `saveData` ein Restrisiko.

## Unveränderte Fachregeln

- GlobalID und Name müssen eindeutig passen. Doppelte Zuordnungen werden gesperrt.
- Eigentümer, Betreiber und Länge gehen nie ins WebGIS; ein vorhandenes Baujahr bleibt geschützt.
- Beim Holen führt WebGIS Eigentümer und Betreiber auch gegenüber Handwerten. Andere Handwerte behalten ihren dokumentierten Schutz.
- Werte der Kanalfirma gehen nur mit ausdrücklich angehaktem Vorschlag hinaus. Unbekannte Katalogwerte werden nie geraten.
- Der Standard von `WebGisExportUseCase.FuehreAusAsync` bleibt ein Probelauf. Es gibt keinen automatischen erneuten POST nach Timeout.
- Öffentliche Einstiege, gespeicherte Projektformate, Kundenoriginale und der geprüfte Berichtsordner bleiben erhalten.

## Pakete und Reihenfolge

| Paket | Änderung | Nachweis vor Abschluss |
|---|---|---|
| WG01 | Die vier im Audit kurz bestätigten Fehlerfälle als dauerhafte Tests festhalten. | Tests zeigen am Ausgangscode gezielt den Fehler; gültige GEONIS-Antwort bleibt grün. |
| WG02 | Felder und Serverantwort vor Erfolg vollständig prüfen. | Kein POST mit fehlenden oder doppelten Feldern; unklare Antwort ist kein Erfolg. |
| WG03 | Änderungslog zur echten Schreibvoraussetzung machen. | Ohne erfolgreiches Startlog null POSTs; Logfehler während eines Laufs wird sichtbar. |
| WG04 | Projekt vor der frischen Schreibprüfung binden. | Projektwechsel während einer Netzwartezeit verhindert jeden POST. |
| WG05 | Neue Massnahmen zurücklesen und unklare Ausgänge kennzeichnen. | „Angelegt“ erst nach Prüfung aller Werte und des Elternbezugs; kein blinder Wiederholungs-POST. |
| WG06 | Holen und Lesefehler absichern. | 401/403 als Sitzungsfehler; umgehängte Massnahme wird nicht am alten Objekt übernommen. |
| WG07 | Laufzeit messen und nur belegte Engpässe beheben. | Vorher/Nachher-Zahlen, gleiche Ergebnisse, keine zusätzlichen POSTs. |
| WG08 | Kontrollierte GEONIS-Abnahme. | Kleine gesicherte Testfälle mit Abgleich von Datenbank, Bericht und Log. |

WG01 bis WG06 sind Sicherheitsarbeit. WG07 beginnt erst danach. Jedes Codepaket bleibt klein und getrennt rücknehmbar.

## WG01 – Erst das Ist-Verhalten durch Tests belegen

**Testdateien:** `tests/AuswertungPro.Next.Infrastructure.Tests/WebGis/GeonisWebGisClientRobustheitTests.cs` und `GeonisWebGisClientTests.cs`.

Vier dauerhafte Tests erwarten das gewünschte Verhalten und werden am alten Code zunächst rot:

1. Von zwei geplanten Massnahmenfeldern fehlt eines im Serverformular: kein `saveData`.
2. `{"message":"Validation failed"}` ohne eindeutige Bestätigung: kein Erfolg.
3. `{"newId":null}` ohne eindeutige Bestätigung: beim Anlegen kein Erfolg.
4. Die Objektmaske liefert dieselbe `refId` zweimal und eine geplante gar nicht: kein `saveData`.

Ein vorhandenes, bereinigtes echtes Erfolgsbeispiel bleibt als Gegenprobe. Fake-HTTP-Tests zählen POSTs und prüfen den gesendeten Inhalt. Keine Zugangsdaten oder Kundenprojekte als Testdaten. **Rückweg:** Nur diese neuen Tests zurücknehmen; keine Datenänderung.

## WG02 – Nichts Halbes senden oder als Erfolg melden

**Code:** `src/AuswertungPro.Next.Infrastructure/WebGis/GeonisWebGisClient.cs` und `GeonisWebGisClient.Sanierung.cs`.

Geplante und vorhandene `refId`s als eindeutige Mengen vergleichen. Fehlende oder doppelte benötigte Komponenten sperren das gesamte Objekt beziehungsweise die ganze Massnahme. Der heutige Zähler `gesetzt == 0` beim Anlegen und der reine Anzahlvergleich beim Objektschreiben reichen nicht. `AntwortAuswerten` verlangt eine eindeutig gültige GEONIS-Bestätigung. Eine beliebige Nachricht oder `newId:null` allein belegt keinen Erfolg. Beim Anlegen muss eine neue ID im belegten Serverformat vorliegen.

**Tests:** WG01 grün; gültige Objekt- und Massnahmenantwort, falscher Typ bei `isFailure`, Fehlerantwort, doppelte und fehlende Komponenten. **Risiko:** Zu strenge Prüfung könnte ein bislang unbekanntes gültiges Antwortformat abweisen. Darum zuerst echte bereinigte Fixtures prüfen; unbekannte Antworten bleiben offen, niemals grün. Keine Änderung am gespeicherten Format.

## WG03 – Ohne Beleg kein Datenbank-POST

**Code:** `WebGisBerichtAblage.cs`, `ExportWebGisBereich.cs`; Tests in `WebGisBerichtAblageTests.cs` und einem passenden UI-Ablauftest.

Einen streng prüfbaren Log-Aufruf ergänzen und die bisherige öffentliche Methode für bestehende Aufrufer erhalten. Vor dem ersten POST Laufkennung und Startzustand nachweisbar im Projektordner schreiben. Scheitert das, startet kein POST. Vor jedem POST den geplanten Schritt, danach Ergebnis oder „Ausgang ungeklärt“ protokollieren. Scheitert das Log später, Lauf stoppen und den fehlenden Beleg anzeigen. Bereits gesendete Änderungen nicht als „nicht geschrieben“ bezeichnen.

**Tests:** Gesperrte Logdatei trotz vorhandenem Ordner: null POSTs. Logfehler nach bestätigtem POST: sichtbarer offener Zustand und kein nächster POST. Normalfall: Start, Objekte, Massnahmen und Abschluss tragen dieselbe Laufkennung. Pfadschutz gegen Verknüpfungen bleibt grün. **Rückweg:** Paket einzeln rücknehmbar; nach einem echten unklaren POST zuerst GEONIS lesen.

## WG04 – Projekt und Berichtspfad beim Schreiben festhalten

**Code:** `src/AuswertungPro.Next.UI/ViewModels/Pages/ExportWebGisBereich.cs`; Test in `tests/AuswertungPro.Next.UI.Tests/ExportWebGisBereichTests.cs` oder als isolierter WPF-Ablauftest.

Projektinstanz und Projektpfad vor Vorschau und „Jetzt schreiben“ binden. Nach jedem Netz-`await` prüfen, ob genau dieses Projekt noch offen ist. Die vorhandene Projektsperre für „Jetzt schreiben“ bereits vor `BaueFrischenPlanAsync` erwerben und auf jedem Rückweg freigeben. Eine verspätete Vorschau aus Projekt A darf nicht zu Projekt B gehören. Reines Prüfen bleibt lesend und lässt das nicht modale Fenster offen.

**Test:** Netzantwort anhalten, von A zu B wechseln, Antwort freigeben: null POSTs und kein Bericht bei B. Zusätzlich Normalfall, Abbruch und Ausnahme auf freigegebene Projektsperre prüfen. **Risiko:** Die Sperre darf normales Bearbeiten ausserhalb des Schreibvorgangs nicht unnötig blockieren.

## WG05 – Massnahmen nach dem Anlegen verifizieren

**Code:** `src/AuswertungPro.Next.Application/WebGis/WebGisExportUseCase.cs`, `WebGisExportBericht.cs`, Infrastruktur-Client; `IGeonisWebGisClient.cs` nur falls eine additive Lesefunktion nötig ist. Tests in `WebGisExportUseCaseTests*.cs`.

**Vorbedingung:** Mit einem bereinigten echten Antwortbeispiel klären, ob `newId` eine Objekt-ID oder GlobalID ist. `LeseMassnahmeAsync` nimmt heute eine GlobalID; beide Kennungen dürfen nicht gleichgesetzt werden.

Nach bestätigtem Anlegen die neue Massnahme, ihren Elternbezug und sämtliche geplanten Werte erneut lesen. Nur eine erfolgreiche Gegenprobe erhält „angelegt“ und grünen Status. Bei fehlender Antwort, Timeout direkt nach POST oder nicht möglicher Gegenprobe den Ausgang sichtbar offen lassen und den Lauf stoppen. Vor jedem manuellen neuen Versuch beim Elternobjekt nachsehen; nie blind automatisch wiederholen.

**Tests:** Vollständig gespeichert; Server bestätigt, verwirft aber ein Feld; falsches Elternobjekt; Lesefehler nach POST; Timeout nach POST; Massnahme ist bei erneutem Versuch bereits vorhanden. **Fachliche Entscheidung:** Bestehende Tests erlauben eine Massnahme trotz verworfenem Elternfeld und zählen ein bestätigtes Objekt bei fehlender Gegenprobe als „geschrieben“ (`WebGisExportUseCaseTests.Audit.cs`). Vor Änderung festlegen, ob die Massnahme dann gesperrt wird. Anzeige künftig zwischen „bestätigt“, „nachgeprüft“ und „ungeklärt“ unterscheiden. Einen echten Servererfolg nie nachträglich als sicher „nicht geschrieben“ ausgeben.

## WG06 – Import und Sitzungsfehler schliessen

**Code:** `GeonisWebGisClient.cs`, `GeonisWebGisClient.Sanierung.cs`, `src/AuswertungPro.Next.Application/WebGis/WebGisImportUseCase.cs`; Tests in `GeonisWebGisClientRobustheitTests.cs`, `WebGisImportUseCaseTests.cs` und `WebGisSanierungImportTests.cs`.

HTTP 401/403 bei Layout, leerer Massnahme und Katalog wie bei der Suche als Sitzungsfehler melden. 5xx und ungültige Antworten davon trennen. Vor dem lokalen Anlegen einer Sanierungsakte prüfen, dass ihre GlobalID weiterhin in der Liste **desselben** Elternobjekts steht; wenn verfügbar zusätzlich die Relation der Einzelmaske prüfen. Unbekannter Status oder unbekanntes Verfahren erscheinen klar als teilweise Übernahme. Ob diese Felder die ganze Akte sperren sollen, ist eine eigene fachliche Entscheidung.

**Tests:** 401/403 stoppen mit verständlicher Anmeldemeldung; ein einzelner Serverfehler bleibt am Objekt nachvollziehbar. Künstlich umgehängte Massnahme erzeugt keine Akte am alten Objekt. Handwerte und Eigentümer-/Betreiberregel vom 24.09.2026 bleiben unverändert.

## WG07 – Leistung nur nach Messung ändern

Mit Fake-HTTP und synthetischen Projekten von etwa 50 und 200 Objekten Laufzeit, Zahl der GET/POST-Aufrufe und Speicherbedarf von Vorschau und frischer Prüfung messen. Kein Kundendatensatz, kein Live-WebGIS. Reale Serverzeiten erst in der freigegebenen Abnahme messen.

**Kandidaten:** `WebGisExportUseCase.BauePlanAsync` und `WebGisSaniertKriterium` durchsuchen Sanierungsakten wiederholt pro Objekt. Bei messbarem Engpass die ausgeführten Akten einmal pro Planlauf nach Record-ID ordnen. `BaueSanierungenAsync` merkt erfolgreiche Sanierungskataloge je Objektart, fehlgeschlagene aber nicht: Anfragezahl und Wiederholungsverhalten messen, bevor eine Sperre nur für denselben Planlauf erwogen wird. Der bestehende Zwischenspeicher für Material-Gruppenlisten bleibt erhalten. Keine parallelen POSTs und keine ungemessene Parallelisierung der GETs.

**Fertig, wenn:** Gleiche Pläne und Schutzentscheidungen, keine zusätzliche Serverlast und ein dokumentierter Vorher/Nachher-Gewinn. Ohne klaren Gewinn bleibt der einfache Code stehen. Diese Änderung bekommt eigene Tests und einen eigenen Rückweg.

## WG08 – Kontrollierte GEONIS-Abnahme

**Vor jedem Code-Commit:** Betroffenen Test, schnellen Release-Build und den vollständigen Release-Weg aus `AGENTS.md` ausführen. Nach Änderungen an Diensten, Schnittstellen, Import/Export den Skill `sewer-architektur` am echten Code abgleichen, aktualisieren und validieren. Jedes Paket getrennt committen; fremde uncommittete Dateien nicht mitnehmen und nicht automatisch pushen.

**Testumgebung:** Nur ein GEONIS-Testsystem oder eine gesicherte, ausdrücklich freigegebene Kopie. Je eine Haltung und ein Schacht, normales Feld, geschütztes Feld, gesperrtes Objekt und neue Massnahme. Nach jedem Schritt tatsächliche GEONIS-Werte, GlobalID, Elternbezug, Bericht und Log vergleichen. Danach 401/403, gesperrtes Log, unklare Antwort, Timeout nach POST und gleichzeitige Bearbeitung prüfen. Backup und Rückfallzuständigkeit stehen vor dem ersten echten POST fest.

**Produktiv:** Erst ein kleiner gesicherter Lauf mit vollständigem Vorher/Nachher-Abgleich. Grössere Läufe nur ohne offene oder falsch grüne Ergebnisse. Serverseitige Versionsbedingung beziehungsweise atomarer Konfliktschutz bleibt bis zur Klärung ein ausgewiesenes Restrisiko.

## Prüf- und Rückfallregel pro Paket

1. Erst fokussierter Test des alten Fehlers und eines gültigen Gegenfalls.
2. Kleinste vollständige Änderung, dann denselben Test erneut ausführen.
3. Diff, Fehlermeldungen, Abbruch, geschützte Felder, öffentliche Schnittstellen und Berichte prüfen.
4. Vor Code-Commit voller Release-Weg; Änderungen an der Architekturkarte mit dem Code abgleichen.
5. Nur das eigene Paket committen. Bei Rückfall den Paket-Commit zurücknehmen. Nach einem unklaren echten POST zuerst GEONIS lesen und niemals blind wiederholen.

**Fertig ist der Gesamtplan erst dann:** Kein geplanter Wert fällt still weg. Kein ungeprüfter Vorgang erhält grünes „geschrieben/angelegt“. Jeder POST gehört zu einem Projekt, Lauf und Objekt. Der Import schreibt nur ins gebundene offene Projekt. Leistungsverbesserungen sind gemessen statt vermutet.
