> Historischer Ausgangsbericht vom 23.09.2026. Zur Veröffentlichung am 24.09.2026 wurden Nachweise beigelegt. Spätere Produktänderungen wurden hier nicht erneut geprüft. Siehe [Audit und Plan](AUDIT-UND-PLAN.md).

# SewerStudio – projektweites Code- und Fehleraudit

**Stand: 23.09.2026 · Zweig: `feature/webgis-uebertragung` · Commit: `927c9f1d3`**

Geprüft wurde der aktuelle Arbeitsordner einschließlich der bereits vorhandenen, noch nicht committeten Änderungen.
Die Quelltexte blieben während der Prüfung unverändert. Es wurden keine Produktänderungen, Commits oder Uploads vorgenommen.

## Ergebnis

**Die meisten bestätigten Ablaufprobleme liegen im WebGIS-Abgleich. Das grösste unmittelbare Risiko für gespeicherte Arbeit liegt bei Sicherung und Speichern unter.**
Bei der KI ist die falsche grüne Anzeige für unbrauchbare Bilder besonders wichtig.
Auch Kosten- und Hydraulikberechnung enthalten reproduzierbare Zuordnungsfehler.

Dieses Audit enthält **18 konkrete Befunde**:

- **7 mit hoher Priorität:** Daten- oder Sicherungsverlust, ungewollte externe Schreibvorgänge oder fachlich falsche Entwarnung.
- **11 mit mittlerer Priorität:** falsche Zuordnung/Berechnung, lückenhafte Herkunft, unvollständige Diagnose oder unzuverlässige Schutzprüfung.
- 15 Befunde wurden durch gezielte Gegenproben nachgestellt. Drei weitere sind am vollständigen Aufrufweg beziehungsweise Prüflauf belegt.

Mehrere frühere WebGIS-Fehler sind inzwischen behoben. Sie wurden nicht einfach erneut als offen gezählt.
Die neuen Befunde zeigen vor allem Lücken zwischen zwei bereits einzeln abgesicherten Schritten.

## Wo liegen die meisten Schwächen?

| Bereich | Befunde in diesem Audit | Meine Bewertung |
|---|---:|---|
| WebGIS-Ablauf | 5 | Höchste Dichte bestätigter Ablaufprobleme. Freigabe, letzter Lesestand, abhängige Massnahmen und Ergebnisbericht passen noch nicht vollständig zusammen. |
| Speichern und Sicherung | 4 | Höchstes Risiko für vorhandene Arbeit. Dateischutz ist vorhanden, aber Speicherortwechsel und übersprungene Ordner haben Lücken. |
| KI, Wissensabruf und GPU-Verwaltung | 3 | Qualitäts- und Schutzinformationen verlieren auf dem Weg ihre Bedeutung. Unbrauchbar wird teilweise zu unauffällig. |
| Fachliche Berechnungen | 2 | Freitext und frühere Einstellungen beeinflussen die Auswahl von Kosten-/Materialregeln. |
| Tabellen, Vorlagen und Importherkunft | 3 | Derselbe fachliche Wert erreicht das System über unterschiedliche Schreibwege. Nicht jeder Weg bewahrt Begriff und Herkunft gleich. |
| Sicherheitsprüfwerkzeug | 1 | Unterschiedliche Kennungen derselben Schwachstelle werden als neuer und gleichzeitig erledigter Fall behandelt. |

Die Zahlen beschreiben die untersuchten Abläufe. Sie sind keine statistische Fehlerquote pro Programmteil.

## Hohe Priorität

### A01 – Sicherung löscht die alte Kopie eines übersprungenen Quellordners

**Nachweis:** Zwei echte Sicherungsläufe mit künstlichen Dateien.

Nach dem ersten Lauf wurde der Quell-Unterordner durch eine Ordnerverknüpfung ersetzt.
Der zweite Lauf überspringt diese Quelle korrekt, löscht aber ihre bisherige Sicherungskopie.
Er meldet trotzdem Erfolg mit Warnung. Die vorhandene Einstellung mit null alten Versionsständen macht die Entfernung endgültig.
Diese Einstellung ist eine bewusste Entscheidung und für sich kein Fehler.

Die Originaldatei blieb erhalten. Verloren ging ihre bestehende Sicherung.

**Fundstellen:** `Infrastructure/Backup/DirectoryMirror.cs:757`, `:280`, `:299`; `Infrastructure/Backup/FullBackupService.cs:299`.

**Verbesserung:** Bei einem aus Sicherheitsgründen übersprungenen Quellordner dessen alte Sicherung erhalten oder das Aufräumen sperren.
Den verknüpften fremden Ordner weiterhin nicht lesen.

**Fester Test:** Normalen Ordner sichern, danach in eine Verknüpfung umwandeln, erneut sichern. Alte Kopie muss bestehen bleiben.

### A02 – Fehlgeschlagenes Speichern unter verändert trotzdem Fotoverweise

**Nachweis:** Echter Speicheraufruf mit absichtlich nicht beschreibbarem Ziel.

Vor dem eigentlichen Schreiben bereitet das Repository die Fotoverweise im laufenden Projekt auf.
Dabei verwendet es bereits den neuen Zielordner. Scheitert danach das Speichern, bleibt der alte Projektordner aktiv.
Die Fotoverweise wurden aber schon verändert und können dadurch ins Leere zeigen.

Im Prüffall wechselte ein gültiger Verweis von `Originalfotos/beleg.jpg` zu `Fotos/Haltungen/H1/beleg.jpg`.
Das neue Speichern scheiterte. Der neue Verweis war im weiterhin aktiven alten Projektordner ungültig.
Die Originaldatei wurde nicht gelöscht.

**Fundstellen:** `Infrastructure/Projects/JsonProjectRepository.cs:148`; `ProjectPhotoReferenceNormalizationService.cs:50`, `:123`; `UI/ViewModels/ShellViewModel.ProjectSaving.cs:347`.

**Verbesserung:** Speicheraufbereitung auf einer unabhängigen Projektkopie durchführen. Den bisherigen Projektordner mitgeben.
Erst nach erfolgreichem Schreiben die vorbereiteten Änderungen übernehmen.

**Fester Test:** Fehler beim Speichern unter anderem Ordner. Vorherige Medienverweise und aktiver Speicherpfad müssen konsistent erhalten bleiben.

### A03 – Unbrauchbare KI-Bilder erscheinen grün als „Kein Schaden erkannt“

**Nachweis:** Echte Analyse- und Anzeigeklassen mit künstlichen Modellantworten.

Der Sidecar erkennt zu dunkle, zu helle, unscharfe oder gleichförmige Bilder.
Er liefert dafür `is_relevant=false` mit einem Qualitätsgrund.
Bei qualifiziertem YOLO geht dieser Grund in der weiteren Verarbeitung verloren.
Alle vier Fälle erschienen grün als `NoDamage` mit „Kein Schaden erkannt“.

**Fundstellen:** `sidecar/sidecar/models/yolo_wrapper.py:401`, `:476`; `Infrastructure/Ai/Pipeline/SingleFrameMultiModelService.cs:209`; `UI/Ai/Coding/CodingMultiModelAnalysisResultWorkflow.cs:82`.

**Verbesserung:** „Bild nicht auswertbar“ als eigenen Zustand durch alle Schichten weiterreichen. Keine grüne Entwarnung.

**Fester Test:** Alle vier Qualitätsgründe bis zur tatsächlichen Anzeige prüfen. Ein gesundes, tatsächlich leeres Bild darf weiterhin grün sein.

### A04 – Eine Fremdänderung im letzten WebGIS-Lesestand wird überschrieben

**Nachweis:** Echter WebGIS-Ablauf und echter HTTP-Client gegen künstliche Antworten.

Die Prüfung des bestätigten Plans ist inzwischen vorhanden. Danach liest der HTTP-Client die Maske nochmals.
Ändert sich der Status erst zwischen diesen beiden Lesevorgängen, erkennt der Client die Änderung nicht als Konflikt.
Im Prüffall enthielt dieser letzte Stand schon Status 5. Trotzdem wurde Status 1 gesendet und Erfolg gemeldet.

**Fundstellen:** `Infrastructure/WebGis/GeonisWebGisClient.cs:54`, `:65`, `:114`; `Application/WebGis/WebGisExportUseCase.cs:321`.

**Verbesserung:** Den bestätigten Ausgangsstand bis zum letzten Schreibschritt mitgeben und dort erneut vergleichen.
Für lückenlosen Schutz gleichzeitiger Schreiber ist zusätzlich eine belegte serverseitige Versionsprüfung nötig.

**Fester Test:** Fremdänderung genau beim letzten Layout-Lesen. Es darf keinen Schreibaufruf geben.

### A05 – Eine gesperrte Haltung kann trotzdem eine Sanierungsmassnahme erhalten

**Nachweis:** Normaler Planbau aus Projekt und ausgeführter Sanierungsakte, danach künstliche Fremdänderung.

Die Haltung wird wegen geändertem WebGIS-Stand korrekt gesperrt.
Der nachfolgende Massnahmenlauf ignoriert diesen Schreibfehler des Elternobjekts.
Er legt trotzdem dessen Sanierungsmassnahme an.

**Fundstellen:** `Application/WebGis/WebGisExportUseCase.cs:434`, `:436`, `:442`, `:464`.

**Verbesserung:** Eine Konfliktsperre muss auch die davon abhängigen Schreibaktionen sperren.
Ein Plan nur mit Massnahme braucht denselben Schutz des bestätigten Elternstands.

**Fester Test:** Konflikt der Haltung führt zu null Feldschreibungen und null neuen Massnahmen.

### A06 – Bestätigtes Schreiben kann als „nicht geschrieben“ protokolliert werden

**Nachweis:** Server bestätigt das Schreiben. Erst das anschliessende Zurücklesen meldet eine abgelaufene Sitzung.

Ergebnis der Probe: ein bestätigter Schreibvorgang, aber `Geschrieben=false`.
Der Fehlertext behauptet ausdrücklich „Sitzung abgelaufen — nicht geschrieben“.
Damit können Anzahl und Abbruchbericht tatsächliche Änderungen verschweigen.

**Fundstellen:** `Application/WebGis/WebGisExportUseCase.cs:343`, `:368`, `:409`, `:411`.

**Verbesserung:** „Vom Server bestätigt“ und „anschliessend nachgeprüft“ getrennt speichern.
Ein Fehler beim Nachlesen darf die bestätigte Schreibinformation nicht zurücksetzen.

**Fester Test:** Sitzungsfehler nach bestätigtem Schreiben muss „geschrieben, nicht nachgeprüft“ ergeben.

### A07 – Später hinzugekommene GlobalID-Dublette verhindert nicht die gesamte Übernahme

**Nachweis:** Plan erstellen, danach zweiten lokalen Datensatz mit derselben GlobalID hinzufügen, geprüft übernehmen.

Beim Planbau vorhandene Dubletten werden inzwischen gesperrt.
Bei einer später hinzugekommenen Dublette wird nur die Kennungszeile übersprungen.
Die Fachwerte werden dennoch übernommen, ohne sichtbare Sperre.

**Fundstellen:** `Application/WebGis/WebGisImportUseCase.cs:259`, `:269`, `:292`, `:329`.

**Verbesserung:** Unmittelbar vor der Übernahme die Eindeutigkeit gegen das aktuelle gesamte Projekt prüfen.
Bei Konflikt die ganze Position samt abhängiger Akten sperren.

**Fester Test:** Neue Dublette nach der Vorschau verhindert Kennung, Fachwerte und Akten und erscheint als gestoppt.

## Mittlere Priorität

| ID | Befund und Beleg | Verbesserung und passender Test |
|---|---|---|
| **A08** | **Speichern unter anderem Ordner verliert relative Medienverknüpfungen.** Echtes Speichern/Laden meldet Erfolg, aber `Videos/beleg.mp4` existiert am neuen Projektort nicht. Gemeinsame Ursache mit A02: Der bisherige Projektordner fehlt. `UI/ViewModels/ShellViewModel.ProjectSaving.cs:347`, `:355`; `Infrastructure/Projects/ProjectVideoReferenceNormalizer.cs:42`. | Alle Medienverweise gegen den alten Ordner auflösen. Beim Wechsel korrekt neu beziehen oder Medien kontrolliert mitnehmen. Video/PDF/Foto und gleichnamige fremde Datei testen. |
| **A09** | **Materialabweichungen der Kanalfirma können vollständig aus der Häkchenliste verschwinden.** Bei anderer WebGIS-Materialgruppe wird die benötigte Liste nur für Handwerte nachgeladen. Gegenprobe: Kanalfirma 0 Vorschläge/0 Hinweise; gleicher Handwert 2 Änderungen. `Application/WebGis/WebGisExportUseCase.cs:193`; `WebGisExportPlanBuilder.cs:211`. | Benötigte Gruppenlisten auch für Kanalfirmenwerte laden. Nicht zuordenbare Werte sichtbar melden. Derselbe Materialfall muss ohne Haken erscheinen. |
| **A10** | **Fehlender konfigurierter Prüfdatensatz-Ordner schaltet den Schutz beim Wissensabruf still aus.** Mit gültigem Ordner bleibt das künstliche reservierte Beispiel ausgeschlossen, mit fehlendem Ordner wird es zurückgegeben. `Application/Ai/Training/EvalContaminationGuard.cs:212`; `Infrastructure/Ai/KnowledgeBase/GuardedRetrievalFactory.cs:36`; `RetrievalService.cs:326`. | Fehlende Schutzdaten als Fehler behandeln. Dann ohne Vergleichswissen arbeiten und den Grund melden. Gültigen, fehlenden, unlesbaren und bewusst deaktivierten Schutz getrennt testen. Der strengere neue YOLO-Trainingsexport ist davon getrennt. |
| **A11** | **Paralleles Modellladen kann die GPU-Reserve unterschreiten.** Messung vor der gemeinsamen Sperre wird mit späterem Reservierungsstand kombiniert. Deterministische Probe: 20 GB frei, 12 GB Reserve, zwei Modelle mit 4 und 6 GB zugelassen; danach nur 10 GB frei. `sidecar/sidecar/gpu_manager.py:618`, `:620`, `:316`. | Speichermessung und Reservierungen konsistent prüfen. Bei zwischenzeitlicher Änderung neu messen. Den Zwei-Thread-Ablauf als festen Test aufnehmen. Keine echte GPU wurde belastet. |
| **A12** | **Zusatztext ändert die Kostenart eines Kurzliners.** `Kurzliner` ergibt im künstlichen Fall 1'150 CHF. `Kurzliner DN 300` ergibt bei sonst gleichen Daten 2'178 CHF; bei 1 m nur 218 CHF. Der allgemeine Teiltreffer `Liner` wird vor `Kurzliner` gefunden: Stückpreis wird zum Meterpreis. `Infrastructure/Ai/Sanierung/CostOptimizationEngine.cs:118`, `:121`; echter Aufrufer `AiSanierungOptimizationService.cs:161`. | Massnahme über eine feste Kennung auswählen; mindestens spezifische Begriffe vor allgemeinen prüfen. Kurzliner mit Zusatztext muss Stückmassnahme bleiben, unabhängig von Haltungslänge. Die Zahlen prüfen die interne Logik, keine Marktpreise. |
| **A13** | **Normalbeton kann mit Kunststoffwerten berechnet werden.** Der Materialabgleich erkennt `Normalbeton` nicht und benutzt die vorherige Einstellung. Echtes Berichtsergebnis bei denselben Haltungsdaten: Material Kunststoff, Kb 0,0005 und Q voll 0,07977 m³/s statt Beton, Kb 0,0015 und 0,06912 m³/s. `Application/Hydraulik/HydraulikMaterialCatalog.cs:35`, `:38`; `Application/DataPage/DataPageHydraulikReportCalculator.cs:47`. | Material über das vorhandene Vokabular auflösen. Unbekanntes Material sichtbar bestätigen lassen; keine stille Übernahme vom vorherigen Objekt. Materialwechsel und gültige Importbegriffe testen. |
| **A14** | **Abweichende Vorlagen-Feldnamen umgehen die Begriffsumwandlung.** `SchachtRecord.SetFieldValue("STATUS", "in_Betrieb", …)` speichert weiterhin `in_Betrieb`. Die vorhandene Feldnamensauflösung findet `STATUS` korrekt. `Domain/Models/SchachtRecord.cs:210`. | Erst unterstützten Feldnamen auflösen, danach umwandeln und Konflikte vergleichen. Dieselben Tests für `Status`, `STATUS` und erlaubte Vorlagenschreibweisen. |
| **A15** | **Schachteditor zeigt vorhandenen Status leer.** Mit dem echten erzeugten WPF-Steuerelement sind sowohl `In Betrieb` als auch der Altwert `weitere` ohne sichtbare Auswahl. Der gespeicherte Wert bleibt erhalten. Erfolgreiche Kontrollprobe mit funktionierender Auswahlauflösung. `UI/Views/Pages/DataGridComboColumnFactory.cs:123`, `:154`; `SchaechtePage.xaml.cs:220`. | Auswahlbindung passend setzen und vorhandene Altwerte sichtbar anbieten. Öffnen/Verlassen des Editors muss Anzeige und Inhalt erhalten. Dies ist eine Steuerelementprobe, keine komplette Bedienabnahme. |
| **A16** | **KINS-Schachtwerte erreichen die neue Quellenregel nicht vollständig.** Der echte DBF-Pfad füllt Material nur bei leerem Feld und verwendet den Setter ohne Herkunft. Ergebnis wäre `Manual` ohne Handmarke: weder Handwert noch Kanalfirmenvorschlag. Vorhandener Katasterwert bleibt entgegen der allgemeinen Zusage stehen. Statisch am vollständigen Aufrufweg belegt. `Infrastructure/Import/Kins/KinsDbfWhitelistEnrichmentService.cs:113`, `:124`, `:130`; `Domain/Models/SchachtRecord.cs:144`; `Application/WebGis/WebGisExportUseCase.cs:130`. | KINS-Herkunft ausdrücklich setzen und die gemeinsame Quellenregel anwenden. Test: Katastermaterial wird durch KINS ersetzt, Handmaterial bleibt, KINS-Wert erscheint bei Abweichung als ungeprüfter Vorschlag. |
| **A17** | **Sicherungslog ist trotz Vollständigkeitsversprechen auf 200 Warnpfade gekürzt.** Dienst kappt vor Rückgabe und Manifest; UI protokolliert nur diese Liste. Statisch am gesamten Weg belegt. `Infrastructure/Backup/FullBackupService.cs:245`, `:316`; `UI/Settings/SettingsFullBackupWorkflow.cs:129`, `:138`. | Vollständige Warnungen fortlaufend in eine Datei schreiben. Nur die Anzeige begrenzen. Test mit 517 Warnungen: 517 Pfade im genannten Log, korrekte Gesamtzahl. |
| **A18** | **Python-Sicherheitsprüfung verwechselt zwei Kennungen derselben Schwachstelle.** Aktueller Prüflauf meldet PYSEC-2026-3929 als neu und GHSA-xrqw-3rrv-vx5w als veraltet. Die Primärquelle führt beide als Aliase derselben CVE. `sidecar/security/audit_lock.py:131`, `:132` vergleicht nur die Hauptkennung. | Offizielle Alias-Kennungen beim Abgleich berücksichtigen. Keine neue pauschale Ausnahme hinzufügen. Test: bekannte Lücke unter anderer offizieller Kennung bleibt derselbe Fall; wirklich neue Lücke bleibt gesperrt. |

Für A18 bestätigt die [OSV-Meldung](https://osv.dev/vulnerability/PYSEC-2026-3929) die gemeinsame Kennung CVE-2026-9856.
Auch der [GitHub-Sicherheitshinweis](https://github.com/advisories/GHSA-xrqw-3rrv-vx5w) nennt diese CVE.
Damit ist keine zusätzliche siebte Paketlücke belegt. Die sechs dokumentierten Paketlücken sind dadurch aber nicht behoben.

Alle verkürzten C#-Fundstellen liegen unter `src/AuswertungPro.Next.*` in der jeweils genannten Schicht.
Ausführliche vollständige Pfade stehen in den Teilberichten; die Dateinamen sind eindeutig.

## Strukturelle Schwächen und Verbesserungsvorschläge

### 1. Schutzregeln enden zu früh

Der häufigste gemeinsame Fehler liegt zwischen Vorbereitung und tatsächlicher Änderung.
Beispiele: eine neue Dublette nach der Vorschau, ein zusätzlicher WebGIS-Lesevorgang, ein anderer Projektordner beim Speichern.

**Vorschlag:** Jede Änderung braucht unmittelbar vor ihrer Ausführung dieselben geprüften Voraussetzungen.
Werte, Herkunft, Objektkennung, Projektbindung und erwarteter Ausgangsstand gehören gemeinsam zum Auftrag.
Wird eine Voraussetzung ungültig, muss auch jede davon abhängige Änderung gesperrt werden.

Das lässt sich schrittweise an den betroffenen Diensten umsetzen. Ein grosser Gesamtumbau ist dafür nicht nötig.

### 2. Erfolg und unvollständige Prüfung werden vermischt

„Gespeichert“, „nachgeprüft“, „nicht auswertbar“ und „nicht verändert“ sind unterschiedliche Zustände.
Bei A03 und A06 verliert das Programm diese Unterscheidung.

**Vorschlag:** Diese Zustände ausdrücklich im Ergebnis speichern. Anzeige und Bericht müssen denselben Zustand verwenden.
Eine technische Antwort ohne Fehler beweist noch kein fachlich gültiges Ergebnis.

### 3. Regeln hängen noch zu oft an Text und Nebenwegen

Kostenart, Material und Herkunft werden teilweise aus Freitext, Feldschreibweise oder Setter-Auswahl abgeleitet.
Dadurch entstehen A12 bis A16.

**Vorschlag:** Vorhandene gemeinsame Vokabulare und Herkunftsregeln an allen fachlichen Schreibwegen verwenden.
Für Kostenmassnahmen eine feste Kennung durchreichen. Freitext darf die Beschreibung ergänzen, aber nicht die Berechnungsart verändern.

### 4. Viele Tests, aber wichtige Gegenfälle fehlen

Die vorhandenen Tests sind umfangreich. Trotzdem konnten die oben genannten Fälle mit kleinen Gegenproben nachgestellt werden.
Besonders oft fehlen Änderungen zwischen zwei Schritten und Fehler nach einem teilweise erfolgreichen Ablauf.

Von den 16 roten UI-Prüfungen suchen zwölf erwartete Quelltextstellen im Player.
Eine Umstrukturierung kann solche Prüfungen veralten lassen. Das macht sie nicht automatisch harmlos.
Die übrigen vier betreffen zwei Abhängigkeitsregeln, Klassengrösse und leere Fehlerbehandlung.

**Vorschlag:** Die Gegenproben dieses Audits als feste Verhaltenstests übernehmen.
Die zwölf Player-Prüfungen auf den tatsächlich beabsichtigten Schutz abstimmen und durch Verhalten absichern.
Bekannte rote Prüfungen dürfen nicht dauerhaft zum normalen Freigabezustand werden.

### 5. Zu viel Verantwortung bleibt in zentralen Oberflächenklassen

Die Bestandsaufnahme zählt physische Quelltextzeilen einschließlich Kommentaren:

| Klasse | Zeilen | Teildateien |
|---|---:|---:|
| PlayerWindow | 4'260 | 73 |
| HoldingFolderDistributor | 2'888 | 6 |
| ExportPageViewModel | 2'105 | 7 |
| SchaechtePageViewModel | 2'028 | 14 |

Grösse allein beweist keinen Fehler. Bei Export und Schächten schlägt aber bereits die eigene Grenze von 2'000 Zeilen fehl.
Der Export hält zusätzlich den gesamten `ServiceProvider`; zwei Tests beanstanden genau diese Kopplung.

**Vorschlag:** Jeweils einen zusammenhängenden Ablauf in einen kleinen Dienst überführen.
Nur benötigte Abhängigkeiten übergeben. Die Zahlen nicht durch weitere Teildateien oder höhere Grenzen schönrechnen.
Zuerst die konkreten Fehler beheben, danach Struktur anhand dieser Abläufe verbessern.

## Prüfung der Aussagen zu Commit 927c9f1d3

| Aussage | Ergebnis dieses Audits |
|---|---|
| Kanalfirma-Vorschläge starten ohne Haken. | Im regulären Ablauf vorhanden. |
| Geänderter lokaler Wert verliert seine alte Freigabe. | Die Freigabebindung berücksichtigt Objekt, Feld, Wert und Zielschlüssel. Im regulären Weg nachvollziehbar umgesetzt. |
| Alle relevanten Kanalfirma-Abweichungen werden angeboten. | Nicht vollständig: A09 bei abhängigen Materiallisten und A16 beim KINS-Schachtweg. |
| Kanalfirma ersetzt Kataster, Handarbeit bleibt. | Die zentrale Regel ist umgesetzt; nicht alle Importe erreichen sie korrekt. A16 belegt einen verbleibenden Nebenweg. |
| Koordinaten aus dem Kataster bleiben erhalten. | Der zentrale Schutz ist im Code vorhanden. Die bewusste Ausnahme wurde nicht als Fehler gewertet. |
| Doppelte GlobalID verhindert jede Übernahme. | Beim Planbau vorhanden; eine spätere Dublette lässt noch Fachwerte durch, siehe A07. |
| 7'254 Infrastructure-Tests grün. | Im vollständigen Auditlauf 7'253 bestanden, ein Prozessstopptest fehlgeschlagen. Dieser bestand einzeln beim Wiederholen. Kein durchgehend grüner Gesamtlauf behauptet. |
| 16 UI-Tests rot. | Im aktuellen Lauf genau so reproduziert. |
| Eigentümer-/Betreiber-Feldnummern live geprüft. | Laut Abschlussbericht offen und in diesem Audit nicht live geprüft. |
| Flüchtige WebGIS-Felder ändern sich beim Lesen nicht. | Nicht am echten WebGIS geprüft. Die Verwendung des gesamten Lesestands braucht diese Abnahme weiterhin. |

Frühere Befunde, die inzwischen korrigiert sind: Alt-/Neuwertvergleich des bestätigten Plans, bewusst leere Felder beim Holen,
erneute Bauwerksartprüfung, exakte zulässige Schritt-A-Begriffe und Umwandlung beim Nachfüllen einer Haltung.
A04 betrifft eine spätere Schreibgrenze. A14 und A15 betreffen weiterhin offene andere Wege.

## Build, Tests und Paketprüfung

Frischer vollständiger Release-Build: **0 Fehler, 5 Warnungen**.
Vier Warnungen betreffen ignorierte Tuple-Namen in der Projektprüfung, eine einen möglichen Nullwert in einem Fototest.

| Prüflauf | Bestanden | Fehlgeschlagen | Übersprungen / ausgeschlossen |
|---|---:|---:|---:|
| Infrastructure vollständig | 7'253 | 1 | 6 |
| Einzelwiederholung des fehlgeschlagenen Prozessstopptests | 1 | 0 | 0 |
| Pipeline vollständig | 2'839 | 0 | 3 |
| UI vollständig | 7'412 | 16 | 32 |
| ProjectModernizer vollständig | 62 | 0 | 0 |
| Sidecar ohne GPU | 572 | 0 | 2 GPU-Tests ausgeschlossen |
| QGIS-Brücke | 14 | 0 | 0 |

Die Einzelwiederholung ist keine zusätzliche unabhängige Abdeckung und ersetzt keinen grünen Gesamtlauf.
Beim fehlgeschlagenen Prozessstopptest lief der eigene Testprozess nach fünf Sekunden noch.
Beim Wiederholen bestand er sofort. Die Ursache dieses sporadischen Fehlers wurde nicht abschliessend ermittelt.

Die Sidecar-Tests meldeten zusätzlich eine Bibliotheks-Abkündigungswarnung und einen nicht beschreibbaren pytest-Zwischenspeicher.
Beides verhinderte den Testlauf nicht.

### Abhängigkeiten

- **NuGet:** 53 Projekte einschließlich indirekter Pakete geprüft; keine bekannten verwundbaren Pakete gemeldet.
- **Python-Sperrdatei:** 88 Paketversionen geprüft; sechs Meldungen bei sechs dokumentierten Ausnahmen.
- Der Python-Prüfer endet trotzdem mit Fehler wegen A18, dem nicht erkannten Kennungswechsel.
- Drei Komponenten sind vom Paketprüfer nicht bewertbar: die lokalen CUDA-Versionen von Torch/Torchvision und der Git-Pin von SAM-2.
- Bestehende Ausnahmen dokumentieren Kompatibilitätsgrenzen. Dieses Audit hat deren vollständige Beseitigung oder Nichtausnutzbarkeit nicht nachgewiesen.

## Was bereits gut abgesichert ist

- Projektdateien werden über eine dauerhaft geschriebene Zwischenkopie und einen atomaren Tausch veröffentlicht.
- Nicht lesbare Projekte werden von inhaltlich beschädigten Projekten unterschieden. Neuere Projektformate werden nicht still überschrieben.
- Importdateien werden vorbereitet; Rücknahme löscht nur unverändert gebliebene selbst angelegte Dateien.
- Import- und Backupjournale sowie Zielpfadprüfungen sind vorhanden.
- Die Sicherung prüft Dateiinhalte, auch bei gleicher Grösse und Zeit. Normale Quelldateien werden während der Kopie gegen Schreiben gesperrt.
- Viele KI-Schutzkorrekturen sind vorhanden: unvollständige Läufe, auftragsbezogener Durchmesser, gebundene Bild-/Zeit-/Meterbelege und Schutz menschlich bearbeiteter Befunde.
- Der neue YOLO-Trainingsexport besitzt einen strengeren Prüfdatenschutz als der in A10 betroffene Wissensabruf.
- Lokale Sidecar-/QGIS-Zugriffe sind mit Token, Eingabegrenzen und Tests abgesichert. Ein neuer praktischer Zugriff ohne Berechtigung wurde hier nicht belegt.

Diese Stärken bleiben bei den vorgeschlagenen Korrekturen erhalten.

## Empfohlene Reihenfolge

1. **Vorhandene Arbeit schützen:** A01, A02 und A08 gemeinsam angehen. Sicherung und Speicherortwechsel zuerst absichern.
2. **WebGIS-Schreibweg schliessen:** A04 bis A07, danach die fehlenden Kanalfirma-Vorschläge A09/A16.
3. **Fachlich falsche Ergebnisse verhindern:** A03, A12 und A13. Unbrauchbare Bilder müssen klar erkennbar bleiben.
4. **Schutz- und Diagnosewege vereinheitlichen:** A10, A11, A14, A15, A17 und A18.
5. **Freigabe wieder belastbar machen:** Gegenproben fest aufnehmen, rote Tests klären, danach reale Bedienabnahme mit Projektkopien.

Keine neue Funktion und kein Material-Grossumbau ist Voraussetzung für diese Reparaturen.
Die Koordinaten-Ausnahme sollte dabei unverändert als ausdrücklich dokumentierte Fachentscheidung erhalten bleiben.

## Umfang und Grenzen

Die automatische Bestandsaufnahme erfasst **3'249 C#/Python-Dateien mit 382'814 physischen Zeilen** unter `src`,
`sidecar/sidecar` und `integrations/qgis`, einschließlich der dort liegenden QGIS-Tests.
Für diese Dateien wurden Prüfsummen gespeichert. Bis zum Abschluss der Quelltextprüfung gab es keine Änderungen.

Das ist ein projektweites, nach Risiken vertieftes Code-Audit. Nicht jede der rund 383'000 Zeilen wurde einzeln manuell geprüft.
Vertieft wurden Speichern/Laden, Import-Transaktionen, Sicherung, WebGIS, KI-Ausfallwege, Wissensschutz, GPU-Zulassung,
Tabellenbindung, Material-/Kostenlogik und die Freigabeprüfungen untersucht.
Die vollständige Lösung und alle vorgesehenen Testprojekte wurden ausgeführt.

Nicht durchgeführt: echtes WebGIS-Schreiben, Prüfung mit Kundenoriginalen, echte GPU-/Modellmessung, Stromausfallversuch,
Wiederherstellung auf Ersatz-PC, vollständige Bedienung sämtlicher Fenster oder fachliche Normabnahme aller Formeln.
Es wurde kein aktueller Schaden in Kundenprojekten behauptet oder dafür nach Kundendaten gesucht.

## Nachweise und Wiederholung

- [WebGIS-Teilbericht](nachweise/basisaudit/webgis/BEFUNDE.md), [Ergebniswerte](nachweise/basisaudit/webgis/ergebnis.json), [Probe](nachweise/basisaudit/webgis/Program.cs)
- [Dateisicherheits-Teilbericht](nachweise/basisaudit/dateien/TEILBERICHT-DATEISICHERHEIT.md), [Probe](nachweise/basisaudit/dateien/Program.cs)
- [KI-Teilbericht](nachweise/basisaudit/ki/TEILBERICHT-KI.md), [Ergebniswerte](nachweise/basisaudit/ki/ergebnisse.json), [GPU-Probe](nachweise/basisaudit/ki/gpu_admission_probe.py)
- [Kosten, Hydraulik und Vorlagenfeldnamen](nachweise/basisaudit/fachlogik/results.json), [Probe](nachweise/basisaudit/fachlogik/Program.cs)
- [Schachteditor](nachweise/basisaudit/ui-probe/results.json)
- [Bestandsaufnahme und Quelltext-Prüfsummen](nachweise/basisaudit/inventory.json)
- [Paketprüfungen](nachweise/basisaudit/sicherheitspruefung.json), [NuGet-Rohbericht](nachweise/basisaudit/nuget-audit.json)
- Build: `build.log`; Testprotokolle: `results/*.trx` sowie `sidecar-tests.xml`.

Die Konsolen-Gegenproben verwenden echte frisch gebaute Programmklassen und künstliche Eingaben.
Sie geben Schutzverletzungen als Daten aus; Prozesscode 0 bedeutet dort nicht, dass der Schutz bestanden hätte.
Sie sind noch keine fest eingebauten xUnit-Regressionstests.

Die Befehle zur Wiederholung stehen in den Teilberichten. Nach Codeänderungen zuerst die Produktassemblies neu bauen.
Eine synthetische Ordnerverknüpfung verbleibt ausschliesslich innerhalb der Backup-Testfixture.
Beim späteren Aufräumen zuerst nur diese Verknüpfung entfernen; nicht rekursiv durch sie löschen.
