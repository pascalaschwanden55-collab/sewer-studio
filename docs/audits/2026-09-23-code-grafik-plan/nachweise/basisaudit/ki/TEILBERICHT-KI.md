# Teil-Audit KI-Auswertung, Wissensabruf und Sidecar

Stand: 23.09.2026. Geprueft wurde der aktuelle Arbeitsbaum einschliesslich der uncommitteten Detektor-, Aufnahmebindungs- und Folgebeleg-Aenderungen. Kein Produktcode geaendert. Kuenstliche Daten in diesem Auditordner; kein laufender Dienst, keine Kundenbilder, keine echte GPU und kein Modell angesprochen.

## Ergebnis

Drei neue Schutzluecken sind durch isolierte Gegenproben bestaetigt. Die meisten Schwaechen liegen hier an den Uebergaengen: Eine technisch korrekte Antwort verliert beim Weiterreichen ihre fachliche Bedeutung, oder eine fehlende Schutzinformation wird als erlaubter Zustand behandelt.

### KI-01 — P1: Unbrauchbares Bild erscheint als gruener Negativbefund

- `sidecar/sidecar/models/yolo_wrapper.py:401`: Der Qualitaetsfilter erkennt `too_dark`, `too_bright`, `too_uniform`, `too_blurry`.
- `sidecar/sidecar/models/yolo_wrapper.py:476`: Ein unbrauchbares Bild liefert ohne YOLO-Inferenz `is_relevant=false` und den Grund in `frame_class`.
- `src/AuswertungPro.Next.Infrastructure/Ai/Pipeline/SingleFrameMultiModelService.cs:209`: Bei qualifiziertem, hashgleichem YOLO wird jede irrelevante Antwort unveraendert zum nicht degradierten Ergebnis. Der Bildqualitaetsgrund geht verloren; DINO/SAM werden uebersprungen.
- `src/AuswertungPro.Next.UI/Ai/Coding/CodingMultiModelAnalysisResultWorkflow.cs:82`: Dieses Ergebnis wird als `NoDamage`, Text **Kein Schaden erkannt**, Farbe **#FF22C55E** angezeigt.

**Reproduktion:** `dotnet run --project .tmp\audit-gesamt-2026-09-23\ki\Probe.csproj -c Release -v quiet`.

Der echte Analyse-Service und der echte UI-Ergebnisworkflow laufen gegen einen reinen HTTP-Testhandler. `/health` und YOLO-Antwort melden denselben qualifizierten Modellhash. Der Klassifikator meldet `usable=false`, YOLO `is_relevant=false` und den jeweiligen Qualitaetsgrund. Alle vier Fehlergruende erscheinen gruen, ohne `Error` oder `Degraded`. Der positive Gegenfall `frame_class=empty` erscheint erwartungsgemaess ebenfalls gruen.

**Auswirkung:** Eine nicht beurteilbare Aufnahme sieht aus wie ein ausgewertetes Bild ohne erkannten Schaden. Das betrifft den Pfad mit qualifiziertem YOLO; die aktuelle reale Modellfreigabe wurde nicht abgefragt. Kein konkreter uebersehener Kundenschaden wurde behauptet.

**Verbesserung:** Bildqualitaet als ausdruecklichen Ergebniszustand durch die ganze Kette tragen. Unbrauchbare Bilder erhalten gelb **Bild nicht beurteilbar**. Nur ein fachlich ausgewertetes leeres Ergebnis darf den gruenen Negativstatus erhalten. Auch der Batchpfad sollte den gleichen Unterschied im Laufbericht bewahren.

**Passender Test:** Parametrisierter Service-plus-UI-Verhaltenstest fuer alle vier Qualitaetsgruende, jeweils ohne DINO/SAM; gesunder leerer Gegenfall bleibt `NoDamage`.

### KI-02 — P2: Fehlender konfigurierter Evalordner schaltet den Wissensschutz still aus

- `src/AuswertungPro.Next.Application/Ai/Training/EvalContaminationGuard.cs:212`: `LoadEvalHaltungKeys` gibt bei fehlendem Ordner eine leere Menge zurueck.
- `src/AuswertungPro.Next.Infrastructure/Ai/KnowledgeBase/GuardedRetrievalFactory.cs:36`: Die gemeinsame Fabrik verwendet diese tolerante Ladefunktion ohne einen Fehlerstatus.
- `src/AuswertungPro.Next.UI/ServiceProvider.KnowledgeBase.cs:45`: Auch der tatsaechliche Programmstart laedt auf diesem Weg.
- `src/AuswertungPro.Next.Infrastructure/Ai/KnowledgeBase/RetrievalService.cs:326`: Bei leerer Menge entfaellt der Ausschluss reservierter Pruefhaltungen.

**Reproduktion:** Derselbe `dotnet run`-Befehl fuehrt `EvalProbe.cs` mit einer echten, ausschliesslich hier erzeugten SQLite-Datei und kuenstlichen Embeddings aus. Darin liegt absichtlich ein historischer Datensatz aus der reservierten Haltung `111111-222222`.

- Gueltiger Evalordner mit `_candidates.json`: kein Treffer, Schutz greift.
- Nicht vorhandener, aber ausdruecklich konfigurierter Evalordner: `eval-sample` wird als Vergleichswissen zurueckgegeben, ohne Fehlermeldung.

**Auswirkung:** Nach falscher Konfiguration, verschobenem Ordner oder nicht verfuegbarem Laufwerk koennen reservierte Prueffaelle aus einer alten Wissensdatenbank in KI-Vergleiche einfliessen. Dadurch wird die Trennung zwischen Lernwissen und unabhaengiger Bewertung unzuverlaessig. Das ist ein reproduzierter Wissensabrufpfad, kein Nachweis, dass der aktuelle reale Datenbestand kontaminiert ist.

**Verbesserung:** Konfiguriert-aber-fehlend von bewusst-deaktiviert unterscheiden. Die gemeinsame Fabrik soll einen nachweislich vollstaendigen Schutzsnapshot verlangen und bei Ladefehlern ohne Vergleichswissen arbeiten, mit sichtbarer Meldung. Keine still leere Sperrliste bei Schutzfehlern.

**Passender Test:** Gueltiger Ordner, fehlender konfigurierter Ordner, unlesbare/beschaedigte Schutzdatei und bewusst leerer Konfigurationswert. Bei den beiden Fehlerfaellen darf kein reserviertes Sample geliefert werden.

**Wichtige Abgrenzung:** Der neue YOLO-Trainingsexport besitzt einen strengeren Schutz: `TrainingExportPlanInputBuilder.cs:141` verlangt vollstaendige Evalpruefung, Bildhashschutz und ein deckungsgleiches Schutzregister. Dieser Export darf nicht pauschal als ungeschuetzt bezeichnet werden. Auch `EvalContaminationSetProvider` und `CodingTrainingSampleEvalProtector` sperren bei Ladefehlern; gerade deshalb ist der verbleibende Unterschied im Wissensabruf eine Wartungsschwaeche.

### KI-03 — P2: Paralleles Modellladen kann die reservierte GPU-Kapazitaet unterschreiten

- `sidecar/sidecar/gpu_manager.py:618`: Freier Speicher wird vor dem gemeinsamen Lock gemessen.
- `sidecar/sidecar/gpu_manager.py:316`: Ein anderer Ladevorgang kann anschliessend seine laufende Reservierung entfernen.
- `sidecar/sidecar/gpu_manager.py:620`: Die Zulassung kombiniert dann einen alten freien Speicherwert mit der inzwischen geleerten Reservierungsliste.

**Reproduktion:** `python .tmp\audit-gesamt-2026-09-23\ki\gpu_admission_probe.py`.

Die Probe importiert nur die originale Managerdatei und die Python-Standardbibliothek. Messung und Loader sind kuenstlich. Zwei Threads erzwingen genau das moegliche Zeitfenster: 20 GB frei, 12 GB Reserve, DINO braucht 4 GB, SAM 6 GB. Beide Modelle werden zugelassen, danach sind rechnerisch 10 GB frei. Eine genaue aktuelle Messung von 16 GB sperrt SAM dagegen korrekt, weil 18 GB erforderlich waeren.

**Auswirkung:** Die versprochene Reserve fuer Ollama wird nicht in jedem Ablauf eingehalten. Moeglich sind Speicherfehler und Analyseabbrueche; Datenverlust oder ein echter GPU-Absturz wurden nicht festgestellt.

**Verbesserung:** Speichermessung und Reservierungsstand konsistent halten, etwa durch eine gepruefte Generation mit erneuter Messung nach Aenderung. Den gemeinsamen Lock dabei weiterhin kurz halten, damit der Prozesswaechter erreichbar bleibt.

**Passender Test:** Der deterministische Zwei-Thread-Ablauf aus der Probe muss den zweiten Loader sperren oder eine neue Messung erzwingen. Dazu ein normaler Parallelfall mit genug Kapazitaet, der zugelassen bleibt.

## Schutz, der im geprueften Code vorhanden ist

- Die Korrekturen vom 18.09. zur unvollstaendigen Videoauswertung sind vorhanden: `MultiModelRunCompleteness` erfasst abgeschnittene Extraktion, technische SAM-Verluste und Qwen-Fehler. Ein fehlerhafter Lauf darf sein Wiederaufnahmejournal nicht als komplett abschliessen.
- Rohrdurchmesser werden aus dem Auftrag aufgeloest; fruehere pauschale DN300-Kritik trifft den aktuellen Batchpfad nicht mehr.
- `SingleFrameMultiModelService.cs:186` bindet qualifizierte YOLO-Antworten an den Modellhash. Fremde/fehlende Hashes verlieren die Filterberechtigung und fuehren zu manueller Pruefung.
- `CodingMultiModelInferenceWorkflow.cs:60` bindet Bild, Aufnahmezeit und einmalig aufgeloesten Meter vor der Inferenz zusammen.
- `CodingPointFollowUpPolicy.cs:89` sperrt automatisches Ersetzen nach menschlichen Aenderungen und bei abweichendem Fingerabdruck. Weitere Schranken pruefen Meterquelle, Geometrie, Modellhash und Maskenqualitaet; vorheriger Beleg wird archiviert. In diesem Audit wurde dort keine neue Umgehung bestaetigt.
- GPU-Modelle besitzen gemeinsame Vorhersage-Locks und Besitzkennungen fuer laufende Arbeit. Der geteilte YOLO-Testslot verwendet ein gemeinsames Lock. Der oben beschriebene Restfehler betrifft die Speicherzulassung, nicht einen erneut belegten Modelltausch waehrend einer Inferenz.
- `SidecarRestartService.cs:264` prueft Prozessidentitaet und Besitz vor dem Beenden. Fremde Dienste und Ollama bleiben geschuetzt; fehlgeschlagenes Beenden verhindert einen blinden zweiten Start.
- Neue Trainingsexporte pruefen Datenbestand, menschliche Freigabe, Klassenkarte, Schutzregister und Evalvollstaendigkeit.

## Teststand und Grenzen

Der Hauptaudit hat einen frischen Release-Build ohne Fehler sowie **2'839 Pipeline-Tests** und **572 Sidecar-Tests ohne GPU** bestaetigt. Die hier dokumentierten Gegenproben ergaenzen deren Abdeckung; bestandene Tests pruefen nur ihre vorhandenen Faelle.

Keine echten Modellgewichte geladen, keine Erkennungsqualitaet gemessen, keine echte GPU-Leistung gemessen, keine reale Modellfreigabe festgestellt. Fachliche Erkennungsqualitaet und produktive Modellfreigabe bleiben eigene Abnahmeschritte.

Die wiederholbaren Proben und die aufgezeichneten Werte stehen in `Probe.csproj`, `Program.cs`, `EvalProbe.cs`, `gpu_admission_probe.py` und `ergebnisse.json` in diesem Ordner. Produktdateien wurden nicht veraendert.
