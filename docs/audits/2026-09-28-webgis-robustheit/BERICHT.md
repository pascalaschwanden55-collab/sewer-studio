# WebGIS-Import und direkter Export: Robustheitsprüfung

**Stand:** 28.09.2026  
**Bereich:** „Vom WebGIS holen“ und „Direkt ins WebGIS übertragen“ (GEONIS/WebOffice). Der GeoShop-/XTF-Abgleich ist hier nicht geprüft.  
**Art der Prüfung:** Quellcode, bestehende Tests und vier kurze Tests mit nachgebildeten HTTP-Antworten. **Keine Verbindung zur produktiven WebGIS-Datenbank, keine Anmeldung, kein echter Schreibaufruf.**

## Ergebnis

Der übliche Ablauf ist gut geschützt: Vorschau, eindeutige Zuordnung, erneutes Lesen vor dem Schreiben, Schutz wichtiger Felder und Nachkontrolle der meisten Objektänderungen sind vorhanden. Der Import verändert nur das offene SewerStudio-Projekt; ins WebGIS schreibt allein der direkte Export.

**Für eine Zusage „100 % robust“ reicht der aktuelle Stand nicht.** Vier Randfälle wurden mit einem nachgebildeten Server tatsächlich ausgelöst. Besonders kritisch sind unvollständige Sanierungsmassnahmen, falsch als Erfolg gewertete Serverantworten und die Möglichkeit eines Schreibens ohne funktionierendes Änderungslog. Ein gleichzeitiger Fremdzugriff auf die WebGIS-Datenbank bleibt ohne eine atomare Bedingung auf dem Server grundsätzlich ein Restrisiko.

**Empfehlung:** Grössere produktive WebGIS-Schreibläufe erst nach Behebung der Befunde W1–W4 und einer kontrollierten Abnahme mit einer Test- oder gesicherten Datenbank durchführen. Reines Holen ist weniger riskant, weil dabei keine WebGIS-Datenbank verändert wird; auch dafür sind die unten genannten Randfälle zu prüfen.

## Nachweis und Grenzen

- Zielgerichteter Release-Testlauf: **516 Infrastrukturtests bestanden** (`FullyQualifiedName~WebGis|FullyQualifiedName~Geonis`). Dieser Filter erfasst auch einzelne benachbarte Tests mit „WebGis“ im Namen.
- UI: **17 bestanden, 2 planmässig übersprungen**. Die beiden übersprungenen Fälle sind Hilfsprozesse der Fenstertests.
- Vier zusätzliche, temporäre Tests mit einem nachgebildeten Server bestätigten W1, W2 und W5: **4 von 4 bestanden**. Die temporäre Testdatei wurde anschliessend entfernt; es bleibt dadurch keine Änderung am Produkt oder an den Tests zurück.
- Die Tests beweisen den **Codepfad bei den angegebenen Antworten/Formularen**, nicht, dass der produktive GEONIS-Server genau diese Antworten bereits gesendet hat.
- Nicht geprüft: produktive Rechte, echte Kataloge und Feldzuordnungen, Servertransaktionen, Backup/Wiederherstellung, Last bei grossen Projekten und gleichzeitige Bearbeitung durch andere Benutzer. Ohne kontrollierten Schreibtest kann die tatsächliche Speicherung nicht abschliessend bestätigt werden.

## Was bereits zuverlässig angelegt ist

1. Beim Holen und Senden wird eine gespeicherte GlobalID bevorzugt; ohne sie wird nur ein eindeutiger exakter Suchtreffer akzeptiert. Doppelte GlobalIDs im Projekt werden gesperrt (`WebGisObjektLesen`, `WebGisEindeutigkeit`).
2. Der Export erstellt eine Vorschau und baut vor „Jetzt schreiben“ einen frischen Plan. Vor jedem Objektschreiben werden GlobalID, Name, alte Feldwerte und der gesamte gelesene Maskenstand nochmals verglichen (`WebGisExportUseCase`).
3. Eigentümer, Betreiber, Länge, vorhandenes Baujahr und Identitätsfelder sind gegen direktes Überschreiben geschützt (`WebGisGeschuetzteFelder`). Das Senden setzt nur geplante Komponenten und kontrolliert Objektfelder nach dem Servererfolg nochmals durch Lesen (`GeonisWebGisClient`, `WebGisExportUseCase`).
4. Der Import prüft vor der Übernahme erneut den WebGIS-Stand, den Projektbezug, doppelte IDs und lokale Änderungen seit der Vorschau (`WebGisImportUseCase`, `WebGisHolenAblauf`). Handwerte bleiben nach den dokumentierten Regeln geschützt; Eigentümer und Betreiber sind die ausdrücklich festgelegte Ausnahme.
5. Ein fehlender sicherer Berichtsordner verhindert den Start eines Schreibvorgangs. Berichtspfade werden gegen Verknüpfungen und Ausbruch aus dem Projektordner geprüft (`WebGisBerichtAblage`).

## Befunde

### W1 – Kritisch: Sanierungsmassnahme kann mit fehlenden geplanten Feldern angelegt werden

**Nachweis:** `GeonisWebGisClient.Sanierung.cs`, `ErstelleSanierungAsync`, Zeilen 108–125. Der Code zählt gefundene Komponenten, bricht aber nur bei `gesetzt == 0` ab. Im Nachbildungstest waren **Art und Status** geplant, das gelesene Formular enthielt nur **Art**. Trotzdem wurde `saveData` aufgerufen und Erfolg gemeldet.

**Folge:** Bei geändertem oder unvollständigem Serverformular kann eine Massnahme nur teilweise in der Datenbank landen. Der Plan verlangt ausdrücklich eine 1:1-Übernahme (`WebGisSanierungPlanBuilder`).

**Behebung:** Vor dem POST die Menge **aller** geplanten `refId`s mit den Formular-Komponenten vergleichen. Fehlende und doppelte `refId`s müssen das gesamte Anlegen sperren. Den Fall als dauerhaften Verhaltenstest aufnehmen.

### W2 – Kritisch: Eine Antwort ohne sicheren Erfolgsnachweis kann als Erfolg gelten

**Nachweis:** `GeonisWebGisClient.Sanierung.cs`, `AntwortAuswerten`, Zeilen 183–222. Die Antworten `{"message":"Validation failed"}` und `{"newId":null}` ergaben im Nachbildungstest beide `Erfolg=true`. Ein vorhandenes `isFailure` mit falschem Typ zählt ebenfalls als Nachweis, solange es nicht exakt `true` ist.

**Folge:** Besonders beim **Anlegen** kann der Ergebnisbericht „ANGELEGT (ID ?)“ ausgeben, obwohl weder eine neue ID noch eine eindeutige Speicherbestätigung vorliegt. Beim Ändern eines Objekts hilft die spätere Nachkontrolle, sofern sie gelingt; beim Anlegen einer Massnahme fehlt sie ganz.

**Behebung:** Das beobachtete GEONIS-Erfolgsformat strikt prüfen: `isFailure` muss ein boolesches `false` sein; beim Anlegen muss eine gültige neue ID vorhanden sein. Eine beliebige `message` und `newId:null` dürfen nie Erfolg bedeuten. Negativfälle als Tests festhalten.

### W3 – Kritisch: Der Schreibvorgang läuft trotz ausgefallenem Änderungslog weiter

**Nachweis:** `WebGisBerichtAblage.cs`, `HaengeAnLog`, Zeilen 61–74, verschluckt jede Datei-Ausnahme und gibt kein Ergebnis zurück. `ExportWebGisBereich.cs`, Zeilen 264–286, prüft nur, ob ein **Ordnerpfad** existiert; `LogStart` und spätere Logzeilen können bereits fehlgeschlagen sein, bevor der Datenbank-POST erfolgt.

**Folge:** Eine gesperrte oder volle Platte, eine blockierte Logdatei oder ein später geänderter Ordner kann zu WebGIS-Änderungen ohne verlässlichen lokalen Beleg führen. Das widerspricht der im Code und in `CLAUDE.md` dokumentierten Regel „ohne Log kein Schreiben“.

**Behebung:** Logstart vor dem ersten POST tatsächlich und überprüfbar schreiben. Scheitert das, keinen POST beginnen. Fehler beim Loggen nach einem POST als eigenständigen, sichtbaren „Ausgang offen/Beleg fehlt“-Zustand behandeln und den Lauf stoppen. Eine eindeutige Lauf-ID und atomare Berichtdateien erleichtern die spätere Zuordnung.

### W4 – Hoch: Projektwechsel während der frischen Prüfung kann einen alten Plan schreiben

**Nachweis aus dem Ablauf:** `ExportWebGisBereich.cs`, Zeilen 232–273. `BaueFrischenPlanAsync(_d.Shell.Project, …)` beginnt vor der Projektsperre und wartet auf Netzantworten. Erst danach liest `Ablageordner()` den **aktuellen** Projektpfad und ruft `BeginneProjektvorgang()` auf. Ein Vergleich mit der beim Start gebundenen Projektreferenz fehlt. Auch `PruefenAsync` (Zeilen 204–215) bindet den Plan nach einem `await` ohne Projektvergleich.

**Folge unter der Bedingung eines Projektwechsels während des Wartens:** Der Export kann einen Plan aus Projekt A nach dem Wechsel zu Projekt B ausführen und den Bericht bei B ablegen. Der Planvergleich prüft die WebGIS-Werte, nicht die Identität des offenen Projekts (`WebGisPlanVergleich.cs`, Zeilen 30–57). Dies ist ein durch Code belegtes Zeitfenster; ein automatisierter UI-Nachbildungstest fehlt noch.

**Behebung:** Projektreferenz und Projektpfad zu Beginn von Vorschau und Schreiben binden. Nach jedem Netz-`await` prüfen, ob noch genau dieses Projekt offen ist. Die Projektsperre spätestens **vor dem frischen Planbau für „Jetzt schreiben“** erwerben und bis zum Ende halten. Ein UI-Test muss den Projektwechsel zwischen Planstart und Netzantwort auslösen.

### W5 – Hoch: Doppelte Formular-Komponenten können ein fehlendes Objektfeld verdecken

**Nachweis:** `GeonisWebGisClient.cs`, Zeilen 90–115, prüft `modified.Count == felder.Count`. Im Nachbildungstest waren zwei Felder geplant; die Servermaske enthielt dieselbe `refId` zweimal und die andere gar nicht. Beide Zähler waren 2, daher wurde `saveData` gesendet und Erfolg gemeldet.

**Folge:** Bei einer fehlerhaften oder geänderten Maske wird ein Teil des Plans nicht gesendet. Die bestehende Nachkontrolle kann das erkennen, wenn sie gelingt; der POST ist dann bereits erfolgt.

**Behebung:** Auf **eindeutige Mengen** von `refId`s prüfen, doppelte Komponenten sperren und erst bei vollständiger Deckung senden.

### W6 – Hoch: Angelegte Massnahmen werden nicht aus der Datenbank zurückgelesen

**Nachweis:** `WebGisExportUseCase.cs`, Zeilen 539–542, setzt `san.Geschrieben = res.Erfolg` direkt nach dem POST. Für Objektfelder gibt es `PruefeNachAsync` (Zeilen 367–411); für neu angelegte Massnahmen fehlt eine entsprechende Kontrolle.

**Folge:** Serverbestätigung, zugehöriges Elternobjekt und sämtliche gespeicherten Felder werden nach dem Anlegen nicht geprüft. In Verbindung mit W2 ist ein falsches OK möglich.

**Behebung:** Nach `saveData` die neue Massnahme anhand einer validierten ID lesen, Elternbezug und alle geplanten Werte vergleichen. Wenn das Lesen nicht gelingt, „vom Server bestätigt, Speicherung nicht verifiziert“ melden und keine grüne Erfolgsmeldung geben. Auch einen Verbindungsabbruch nach dem POST mit erneuter Suche nach der Massnahme aufklären, bevor ein Benutzer erneut anlegt.

### W7 – Mittel: HTTP-Fehler beim Lesen werden teils als „nicht gefunden“ behandelt

**Nachweis:** `GeonisWebGisClient.cs`, `LiesLayoutAsync`, Zeilen 260–272, gibt bei jedem Nicht-2xx-Status `null` zurück. `getEmptyData` und `getControlValues` verhalten sich ähnlich (`GeonisWebGisClient.Sanierung.cs`). Die Suche unterscheidet dagegen 401/403 als Sitzungsfehler (Zeilen 281–287).

**Folge:** Bei 401/403 im Layout kann ein ganzer Lauf aus gesperrten Objekten oder fehlenden Katalogen entstehen, statt mit einer klaren Anmeldemeldung abzubrechen. Es wird dabei nicht geschrieben, aber Diagnose und Wiederaufnahme sind unzuverlässig.

**Behebung:** 401/403 bei allen Endpunkten einheitlich als Sitzungsfehler behandeln; 5xx/Timeout ausdrücklich als Server- oder Netzfehler melden. Mit nachgebildeten Antworten testen.

### W8 – Mittel, bedingt: Beim Holen wird der Elternbezug einer Massnahme nicht erneut geprüft

**Nachweis:** Der Import findet eine Massnahme zunächst über die Liste des Elternobjekts (`WebGisImportUseCase.cs`, Zeilen 147–171). Vor der Übernahme liest er das Elternobjekt und die Massnahme nochmals, vergleicht aber nur deren **Felder** (Zeilen 214–249). Die Massnahmenliste ist in `GeonisWebGisClient.StandAus` ausdrücklich kein Feld (`GeonisWebGisClient.cs`, Zeilen 224–227); der direkte Massnahmen-Lesestand enthält keinen überprüften Elternbezug (Zeilen 205–209).

**Folge, falls GEONIS das Umhängen einer Massnahme erlaubt:** Eine zwischen Vorschau und Übernahme zu einem anderen Elternobjekt verschobene Massnahme kann als lokale Akte beim alten Objekt landen. Ob und wie die produktive Schnittstelle den Elternbezug liefert, ist noch offen.

**Behebung:** Beim erneuten Lesen prüfen, dass die GlobalID weiterhin in der aktuellen Massnahmenliste desselben Elternobjekts steht; möglichst zusätzlich den Relationseintrag der Einzelmaske gegen die Eltern-GlobalID prüfen. Einen Umhängefall mit nachgebildeter Antwort und im Testsystem prüfen.

### W9 – Restgrenze: Kein atomarer Schutz gegen gleichzeitige Fremdänderungen

**Nachweis:** `GeonisWebGisClient.cs`, Zeilen 68–84 und 126–136, liest und vergleicht zuletzt, sendet danach aber einen separaten `saveData`-POST. Der Code benennt dieses Restfenster selbst. Beim Anlegen liegt zwischen der erneuten Doppelprüfung (`WebGisExportUseCase.cs`, Zeilen 506–537) und dem POST dasselbe Fenster.

**Folge:** Ändert ein anderer Benutzer genau dazwischen denselben Datensatz oder legt dieselbe Massnahme an, kann das Programm den Konflikt nicht sicher verhindern. Eine rein lokale Prüfung kann dieses Zeitfenster nicht auf null reduzieren.

**Behebung:** Mit GEONIS/Trigonet klären, ob `saveData` eine serverseitige Versionsbedingung oder einen eindeutigen Idempotenzschlüssel unterstützt. Falls nicht, bleibt dies ein offen ausgewiesenes Restrisiko; vor grossen Läufen andere Bearbeitung organisatorisch ausschliessen und jeden Schreibschritt danach verifizieren.

## Zusatzpunkt zum Import

`WebGisSanierungImportRegel.Baue` übernimmt eine Massnahme auch dann als neue lokale Akte, wenn **Status oder Verfahren** im SewerStudio-Katalog nicht eindeutig zugeordnet werden können; es gibt dafür einen Hinweis und das Feld bleibt leer (Zeilen 72–93). Das ist im aktuellen Code offenbar eine bewusste Regel, aber für eine verlangte vollständige 1:1-Übernahme fachlich zu entscheiden. Die Akte sollte entweder vollständig sein oder in der Vorschau klar als **teilweise übernommen** erscheinen. Der Import speichert das Projekt anschliessend nicht automatisch; die Oberfläche fordert zum Speichern auf.

## Umsetzungs- und Abnahmeplan

| Reihenfolge | Arbeit | Nachweis vor Freigabe |
|---|---|---|
| 1 | W1, W2 und W5: vollständige Feldmengen und strikte Serverantworten | Nachgebildete Formulare/Antworten: kein POST bei fehlenden oder doppelten Feldern; kein Erfolg bei unklarer Antwort. |
| 2 | W3 und W4: Schreibbeleg und Projektbindung absichern | Simulierter Logfehler verhindert den ersten POST; Projektwechsel während Netzwartezeit verhindert jeden POST und legt keinen Bericht im anderen Projekt ab. |
| 3 | W6–W8: Nachlesen neuer Massnahmen, klare Sitzungs- und Netzfehler, Elternbezug beim Holen | Server bestätigt, speichert aber nicht: sichtbar fehlgeschlagen. 401/403 stoppen den Lauf mit Anmeldemeldung. Umgehängte Massnahme wird nicht übernommen. |
| 4 | Bestehende WebGIS-Tests und vollständigen Release-Weg gemäss `AGENTS.md` laufen lassen | Build ohne Warnungen/Fehler; Infrastruktur-, Pipeline-, UI- und ProjectModernizer-Tests grün. |
| 5 | Kontrollierte Abnahme mit GEONIS-Testsystem oder gesicherter Kopie | Kleine bekannte Objekte beider Arten; Hin- und Rückweg; Kataloge; zwei gleichzeitige Benutzer; Timeout direkt nach POST; Berechtigungsentzug; Logdatei blockiert; Vergleich der Datenbankwerte und Berichte. |
| 6 | Produktiver Kleinstlauf mit Sicherung und klarer Rückfallmöglichkeit | Vorher/Nachher-Abgleich jedes Felds, neue Massnahmen samt Elternbezug, Log und Bericht; erst danach grössere Läufe. |

**Freigabekriterium:** Kein geplanter Wert darf still entfallen. „Erfolg“ setzt eine eindeutige Serverbestätigung **und** eine erfolgreiche Nachprüfung voraus; sonst muss der Ausgang sichtbar offen bleiben. Jeder tatsächlich gestartete POST muss einem nachvollziehbaren Lauf und Objekt zugeordnet sein.
