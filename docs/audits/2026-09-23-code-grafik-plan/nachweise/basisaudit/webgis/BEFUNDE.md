# WebGIS-Teilaudit, aktueller Stand 927c9f1d3

Nur Prüfung. Keine Produktdateien oder Kundendaten geändert. Keine Anmeldung, kein Browser, kein echtes WebGIS aufgerufen. Gegenproben ausschließlich gegen künstliche Projekte und einen Fake-Client bzw. `HttpMessageHandler`. Das Konsolenprojekt verweist auf die vom Hauptaudit frisch gebauten Release-DLLs, ohne Projektverweise. Laufbefehl: `dotnet run --project .tmp/audit-gesamt-2026-09-23/webgis/Probe.csproj -c Release -v quiet`. Ergebnisse in `ergebnis.json`. Die fünf `safetyHolds=false` sind absichtliche Verletzungen der erwarteten Sicherheitsregel, keine Fehler des Probeprogramms; Prozess endet normal mit 0.

## W1 — Hoch: Der letzte gelesene Fremdstand wird trotzdem überschrieben

- **Stellen:** `src/AuswertungPro.Next.Infrastructure/WebGis/GeonisWebGisClient.cs:54`, `:65`, `:66`, `:114`; vorausgehende Prüfung `src/AuswertungPro.Next.Application/WebGis/WebGisExportUseCase.cs:295`, `:321`, `:343`.
- Der Ablauf prüft zunächst Altwerte und den ganzen Lesestand. Anschließend liest der HTTP-Client die Maske nochmals. Dieser letzte Stand wird nur gegen die Liste geschützter Felder geprüft; die bestätigten Altwerte/der bestätigte Änderungsstand kommen gar nicht am Client an. Eine inzwischen erkennbare Fremdänderung wird daher überschrieben.
- **Vollständige Gegenprobe über echten UseCase und echten HTTP-Client:** Vorschau Status 2, Kontrolle vor Schreiben Status 2, letztes Layout vor dem POST Status 5. Der Client sendet Status 1. Ergebnis: 4 Leseaufrufe, 1 Schreibaufruf, `StatusBeforeSave=5`, `StatusAfterSave=1`, `Geschrieben=true`, kein Fehler.
- **Behebung:** Den erwarteten Stand bis an die letzte Schreibgrenze mitgeben und dort prüfen. Für vollständigen Schutz gleichzeitiger Schreiber ist zusätzlich eine bedingte Speicherung/Versionsprüfung des Servers nötig; ohne belegte Serverunterstützung darf atomarer Schutz nicht behauptet werden.

## W2 — Hoch: Eine gesperrte Haltung kann trotzdem eine Sanierungsmassnahme erhalten

- **Stellen:** `src/AuswertungPro.Next.Application/WebGis/WebGisExportUseCase.cs:285`, `:434`, `:436`, `:442`, `:464`.
- Die Objektschreibung sperrt die Haltung korrekt bei geändertem Lesestand. Der anschließende Massnahmenlauf liest die Elternposition nur zum Ermitteln der gespeicherten GlobalID; ihren `SchreibFehler` ignoriert er. Der eigene Massnahmenweg prüft Name/ID und Doppelanlage, aber nicht den bestätigten Lesestand/Änderungsdatum.
- **Gegenprobe mit normalem Planbau aus Projekt und ausgeführter Sanierungsakte:** Nach Vorschau Änderungsdatum ändern. Objekt schreibt nichts und meldet ausdrücklich „seit der Prüfung geändert — nicht geschrieben“. Trotzdem ein Aufruf von `ErstelleSanierungAsync`, Massnahme `Geschrieben=true`.
- **Behebung:** Eine Konfliktsperre des Elternobjekts muss sämtliche abhängigen Schreibaktionen sperren. Auch bei einem Plan nur mit Massnahme muss der bestätigte Elternstand vor der Anlage geprüft werden. Eigene zuvor erfolgreich geschriebene Felder benötigen einen aktualisierten, bestätigten Vergleichsstand.

## W3 — Hoch: Bestätigte Schreibvorgänge werden nach Sitzungsfehler als „nicht geschrieben“ gemeldet

- **Stellen:** `src/AuswertungPro.Next.Application/WebGis/WebGisExportUseCase.cs:343`, `:345`, `:368`, `:409`, `:411`.
- Server-Schreiberfolg und erfolgreiche Nachkontrolle sind nicht getrennt gespeichert. Läuft die Sitzung erst bei der Nachkontrolle ab, wirft diese weiter. Der äußere Fangblock kennzeichnet das gesamte Objekt ausdrücklich als „nicht geschrieben“.
- **Gegenprobe:** Fake bestätigt einen Schreibvorgang, erst die folgende Leseprobe wirft `WebGisSitzungException`. Ergebnis: `ServerConfirmedWrites=1`, `Geschrieben=false`, Meldung „WebGIS-Sitzung abgelaufen — nicht geschrieben (Nachkontrolle: Sitzung abgelaufen).“
- **Auswirkung:** Anzahl/Abbruchbericht verschweigen eine vom Server bestätigte Änderung; der Anwender kann den tatsächlichen Stand falsch einschätzen.
- **Behebung:** Zustände „vom Server bestätigt“, „nachgeprüft“, „abweichend“ und „unbekannter Ausgang“ trennen. Nach einem bestätigten Schreiben darf ein Nachlesefehler niemals „nicht geschrieben“ erzeugen.

## W4 — Hoch: Eine nachträgliche GlobalID-Dublette verhindert nur die Kennung, nicht die Werte

- **Stellen:** `src/AuswertungPro.Next.Application/WebGis/WebGisImportUseCase.cs:96`, `:259`, `:269`, `:292`, `:329`.
- Der Planbau prüft Dubletten korrekt. Unmittelbar vor Übernahme gibt es aber keine erneute vollständige Prüfung des aktuellen Projekts. `GlobalIdSchonVergeben` wird ausschließlich beim Übernehmen der Kennungszeile geprüft; `continue` überspringt nur diese Zeile. Fachwerte und gegebenenfalls Akten folgen trotzdem.
- **Gegenprobe über Planbau und `UebernimmGeprueftAsync`:** H1 ohne GlobalID prüfen, der Plan findet G1. Danach neuen lokalen Datensatz mit G1 hinzufügen. Übernehmen vergibt H1 zwar keine ID, füllt aber Status aus G1. Ergebnis: `Uebernommen=1`, `Gestoppt=0`, `Sperren=[]`.
- **Behebung:** Eindeutigkeit vor jeder Mutation erneut gegen das gesamte aktuelle Projekt prüfen; bei Dublette die ganze Position samt abhängiger Akten sperren und sichtbar melden.

## W5 — Mittel: Materialabweichungen der Kanalfirma fehlen ohne Hinweis in der Auswahl

- **Stellen:** `src/AuswertungPro.Next.Application/WebGis/WebGisExportUseCase.cs:193`; `src/AuswertungPro.Next.Application/WebGis/WebGisExportPlanBuilder.cs:211`, `:214`.
- Abhängige Materiallisten werden nur für `Handwerte` nachgeladen. Der neue Kanalfirmenweg verwendet die gleiche Umrechnung, lädt aber seine Zielgruppe nicht nach. Hinweise werden dort ausdrücklich mit `hinweise: null` verworfen.
- **Gegenprobe:** Kanalfirma liefert „Beton, Fertigteil“, WebGIS-Maske zeigt Gruppe Kunststoff und nur Kunststoffdetails. Der Fake würde die Betonliste liefern. Ergebnis: 0 Katalogaufrufe, 0 Vorschläge, 0 Hinweise. Genau derselbe Wert als Handwert führt zu 1 Katalogaufruf und 2 Änderungen (Gruppe und Detail).
- **Behebung:** Die notwendigen Listen auch für zulässige Kanalfirmenwerte laden. Nicht zuordenbare Abweichungen sichtbar melden. Die Projektprüfung ist kein vollständiger Ersatz: Sie prüft die Schritt-A-Listen/Masse, nicht diese abhängigen Materiallisten.

## Weitere konkret gelesene Schwächen, hier nicht als Gegenprobe ausgeführt

1. **KINS-Herkunft:** `Infrastructure/Import/Kins/KinsDbfWhitelistEnrichmentService.cs:112`/`:130` füllt Schachtmaterial über den zweiparametrigen Setter. `Domain/Models/SchachtRecord.cs:144`/`:238` setzt dadurch Quelle Manual ohne Handmarke. `WebGisExportUseCase.cs:118` und `:136` nimmt diese Werte weder als Handwerte noch als Kanalfirmenvorschläge. Außerdem ist dieser KINS-Weg weiterhin ausdrücklich „nur leer“, ersetzt also einen vorhandenen Katasterwert nicht. Zur Einordnung mit dem Importaudit zusammenführen.
2. **Projektbindung/Log:** `UI/ViewModels/Pages/ExportPageViewModel.WebGis.cs:207` wartet beim Neubau des Sendeplans; erst `:217` wird der Ablageordner aus dem inzwischen aktuellen Projektpfad gebunden. Ein Projektwechsel während dieser Wartezeit kann den Bericht/Log am falschen Projekt ablegen. Eine Sperre gegen Projektwechsel fehlt. Beim Holen prüft `UI/Services/WebGisHolenAblauf.cs` den Projektwechsel erst nach `UebernimmGeprueftAsync`; das alte Projekt kann dann bereits verändert sein, obwohl die Meldung „nichts übernommen“ lautet.
3. **Zwei Quellfelder für eine Zielzelle:** `WebGisHandwertKarte` weist DN und lichte Breite derselben refId zu. `WebGisExportPlanBuilder.FuegeComboAn` ignoriert den zweiten widersprüchlichen Wert ohne Konflikthinweis. Dieses Verhalten ist im Quelltext ausdrücklich beschrieben, aber kein Beleg, dass die ausgewählte Dimension fachlich richtig ist.

## Die alten Schutzbefunde sind nicht unverändert offen

- `WebGisPlanVergleich.cs:40` bezieht jetzt Alt- und Neuwert, zusätzlich Fingerabdruck des gelesenen Stands ein. Der frühere einfache Überschreibfehler zwischen Vorschau und frischem Plan ist behoben. W1 betrifft die spätere Grenze zwischen Kontrolle und HTTP-Schreiben.
- `WebGisImportUseCase.cs:296`/`:309` und `:390` prüfen Handmarke und alten lokalen Wert vor dem Füllen. Bewusst leer wird beim Holen damit geschützt.
- `WebGisImportUseCase.cs:313` prüft die aktuelle Bauwerksart erneut; der alte Wechsel zum Spezialbauwerk ist berücksichtigt.
- `WebGisExportPlanBuilder.cs` prüft für Schritt-A-Felder die genaue lokale Listenmitgliedschaft und genau einen live gelesenen Schlüssel. Der frühere `Alt.In Betrieb`-Durchlass ist in diesem regulären Weg behoben (Vorlagenfeldnamen separat im Hauptaudit).
- `HaltungRecord.FuelleLeeresFeld` normalisiert inzwischen ebenfalls den WebGIS-Begriff.
- Dubletten schon beim Planbau werden beim Holen und Senden gesperrt. W4 betrifft eine spätere Änderung des lokalen Projekts.
- Kanalfirma-Vorschläge starten nicht angehakt. Auswahlübertragung bindet an Objekt, lokales Feld, lokalen Wert und Zielschlüssel. Ein geänderter lokaler Wert übernimmt den alten Haken nicht. Diese Regel ist im normalen Weg nachvollziehbar umgesetzt.
- `KatasterFeldschutz` schützt bestehende Koordinate_East/North aus Kataster weiterhin vor Protokollquellen; bei üblichen übrigen Fachfeldern lässt er bekannte Kanalfirmenquellen vor. Handwertschutz sitzt davor in den Record-Settern. Wie vollständig alle Importwege diese Setter erreichen, ist Aufgabe des Importteilaudits.

## Erforderliche Regressionstests bei einer später beauftragten Behebung

Die fünf vorhandenen Gegenproben sollten als Verhaltenstests mit diesen Erwartungen übernommen werden:

1. **W1:** Ändert sich der letzte gelesene Layoutstand, gibt es keinen `saveData`-Aufruf und einen sichtbaren Konflikt.
2. **W2:** Nach Konfliktsperre der Elternposition werden weder Objektfelder geschrieben noch Sanierungsmassnahmen angelegt; auch der reine Massnahmenplan prüft den erwarteten Elternstand.
3. **W3:** Sitzungsfehler erst bei der Nachkontrolle bewahrt die bestätigte Schreibinformation und meldet „geschrieben, nicht nachgeprüft“ statt „nicht geschrieben“.
4. **W4:** Eine neue lokale GlobalID-Dublette nach der Vorschau verhindert Kennung, Fachwerte und Akten für die betroffene Position; sie wird als gestoppt gezählt und erklärt.
5. **W5:** Auch Kanalfirmenmaterial lädt die benötigte Gruppenliste. Der gültige abweichende Wert erscheint ohne Haken; ist er nicht zuordenbar, erscheint stattdessen ein Hinweis.

## Grenzen

Keine Aussage über live gültige Eigentümer-/Betreiber-refIds, volatil wechselnde Maskenfelder, tatsächliche serverseitige Versionssperren oder neue Materialgruppencodes. Die Gegenproben zeigen zuverlässig den Programmablauf mit kontrollierten Antworten; sie behaupten keinen aktuellen Schaden in einem Kundenprojekt. Vollständige Feldinventur und sämtliche Formulare sind hier nicht nachgeprüft. Tests/Buildzahlen stammen aus dem zentralen Hauptaudit und werden dort berichtet.
