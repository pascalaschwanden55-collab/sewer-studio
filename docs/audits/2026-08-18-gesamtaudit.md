# Gesamtaudit SewerStudio

**Datum:** 18.08.2026  
**Branch:** `feature/eval-pruefsatz-review`  
**geprüfter Commit:** `50d5aa7e57f4cee579296733be92bcff2a486485`  
**zusätzlich geprüft:** der nicht eingecheckte Druckcenter-/Schacht-Arbeitsstand  
**Audit-Art:** Code-, Architektur-, Funktions-, Robustheits-, Sicherheits-, Qualitäts-, Werkzeug- und statisches Oberflächen-Audit

## Kurzurteil

SewerStudio ist insgesamt **produktionsnah und ungewöhnlich gut abgesichert**. Der eingecheckte Stand baut vollständig, seine GitHub-CI ist grün, die zentralen Datei-, Prozess-, Sidecar-, Modell- und Eval-Schutzregeln sind überwiegend real im Code verankert und durch sehr viele Tests geschützt.

Der **aktuelle lokale Arbeitsstand ist trotzdem nicht freigabefähig**. Zwei Architekturtests schlagen wegen der neuen Druckcenter-Änderungen fehl. Zusätzlich bestehen ein bestätigter Sidecar-Speicher-/Fehlerantwort-Befund und eine wahrscheinliche Kostenlücke beim Zusammenführen zweier Schachtquellen. Diese Punkte sollten vor einer Freigabe des Druckcenters behoben werden.

Es gibt keinen kritischen Hinweis auf gestohlene Geheimnisse, Kommandoinjektion, unsichere Netzwerkfreigabe oder eine allgemein instabile Anwendung. Die größten Restthemen sind enger begrenzt:

1. den neuen Druckcenter-Code wieder innerhalb der vorhandenen Architekturgrenzen bringen,
2. Trainings-HTTP-Anfragen früh begrenzen und Base64-Daten nie in Fehlerantworten spiegeln,
3. Schachtkosten nur dann als „belegt“ behandeln, wenn wirklich ausgewählte, gültige Kostenzeilen vorhanden sind,
4. Python-CI, SDK und Modellgewichte reproduzierbarer binden,
5. Barrierefreiheit und echte visuelle End-to-End-Prüfung ausbauen.

## Bewertung

| Bereich | Bewertung | Kurzbegründung |
|---|---:|---|
| Funktionen | **8/10** | Sehr breite Fachabdeckung; OSD-Rückfall unabhängig bestätigt. Neuer Druckcenter-Stand noch nicht freigabefähig. |
| Robustheit | **7/10** | Viele Fail-closed- und Atomaritätsregeln; zwei bestätigte Sidecar-Fehlerverträge und ein Kosten-Fallback-Risiko bleiben. |
| Sicherheit | **7,5/10** | Loopback, Pflicht-Token, Pfadschutz, generische 500er und Abhängigkeitsprüfungen sind stark; Trainingsrequest kann große Daten spiegeln und vor Limits vollständig materialisieren. |
| Codequalität | **8/10** | Keine TODO-/NotImplemented-Reste, breite Tests und Fitness-Gates; aktuelles ViewModel überschreitet bewusst gesetzte Grenzen. |
| Architektur | **8/10** | Klare Schichten, Application-UseCases, Schnittstellen und Wächter; Kompatibilitätsfassaden bleiben Altlast, im aktuellen Stand gerade angewachsen. |
| Optik/Bedienung | **7/10, vorläufig** | Einheitliches Theme, Dark/Light, Mica, Motion-Reduktion, Layouttests und gute Gruppierung; hohe Dichte und kaum explizite Barrierefreiheitsnamen. Keine echte Bildschirmbegehung möglich. |
| Qualitätssicherung | **8,5/10** | 12.014 bestandene .NET-Tests plus 555 Sidecar- und 10 QGIS-Tests; 45,54 % .NET-Zeilenabdeckung. Zwei lokale UI-Tests rot. |
| Werkzeuge/CI | **7,5/10** | 43 .NET-Werkzeuge im Gesamtbuild, Security-Gates und gepinnte Actions; Python-Testumgebung und SDK-Auswahl sind noch nicht vollständig reproduzierbar. |

**Gesamt:** etwa **8/10** für den eingecheckten Stand, aber **Freigabe gesperrt** für den aktuellen lokalen Druckcenter-Arbeitsstand.

## Geprüfter Umfang

### Größe

| Bereich | Dateien | Zeilen |
|---|---:|---:|
| `src` | 2.615 | 262.800 |
| `tests` | 2.060 | 225.987 |
| `tools` | 193 | 33.450 |
| Python-Sidecar produktiv | 32 | 5.861 |
| Python-Sidecar-Tests | 57 | 7.735 |
| QGIS-Brücke | 4 | 1.130 |

Zusätzlich wurden `AGENTS.md`, `CLAUDE.md`, die Architekturkarte, die letzten drei Gesamtaudits, CI, Sperrdateien, Sicherheitsausnahmen, der aktuelle Git-Diff und die relevanten neuen Druckcenter-Dateien geprüft.

### Nicht eingecheckte Änderungen

Der Arbeitsbaum enthielt zu Auditbeginn 15 geänderte sowie 9 neue Dateien rund um:

- Druckcenter für Haltungen und Schächte,
- PDF-Kostenabschnitte,
- Schachtkosten-Zusammenführung,
- Tabellenzeilen und Konverter,
- neue Infrastruktur- und UI-Tests.

Diese Änderungen stammen nicht aus diesem Audit und wurden nicht verändert.

### Methodische Grenze Optik

SewerStudio lief als Debug-Prozess. Die vorgesehene Windows-Steuerverbindung des Prüfwerkzeugs war jedoch nicht verfügbar (`native pipe ... Datei nicht gefunden`). Deshalb wurden keine Klicks oder Bildschirmaufnahmen vorgetäuscht. Der Optikteil basiert auf:

- XAML und Theme-Ressourcen,
- Layout-, Theme-, Fenster- und Tastaturtests,
- Größen-, Dichte- und Barrierefreiheitsprüfung,
- erfolgreich geladenen WPF-Testtypen innerhalb der UI-Suite.

Eine manuelle Bildschirmbegehung bei 100 %, 125 %, 150 % und kleinem Fenster bleibt offen.

## Ausgeführte Prüfungen

### Build und Tests

| Prüfung | Ergebnis |
|---|---|
| `dotnet restore AuswertungPro.sln --locked-mode` | bestanden |
| `dotnet build AuswertungPro.sln -c Release --no-restore` | bestanden, 0 Warnungen, 0 Fehler |
| Infrastructure.Tests | 3.891 bestanden, 1 übersprungen |
| Pipeline.Tests | 2.329 bestanden, 2 übersprungen |
| UI.Tests | 5.732 bestanden, 2 übersprungen, **2 fehlgeschlagen** |
| ProjectModernizer.Tests | 62 bestanden |
| **.NET gesamt** | **12.014 bestanden, 5 übersprungen, 2 fehlgeschlagen** |
| Sidecar `pytest -m "not gpu"` | 555 bestanden, 2 abgewählt |
| QGIS-Unittests | 10 bestanden |

Der aktuelle eingecheckte Commit hat einen erfolgreichen GitHub-Actions-Lauf vom 18.08.2026. Beide Jobs (`dotnet`, `python`) sind grün. Die zwei lokalen Fehler betreffen ausschließlich den danach veränderten Arbeitsbaum.

### Abhängigkeiten

- 51 .NET-Projekte geprüft: keine bekannte verwundbare NuGet-Abhängigkeit gemeldet.
- Python-Sperrdatei: 88 PyPI-Pins geprüft.
- 5 bekannte, dokumentierte Ausnahmen in 2 Paketen:
  - `transformers 4.57.6`: vier Meldungen; zwei ohne verfügbare Korrektur, zwei durch die aktuell inkompatible 5.x-Linie blockiert,
  - `setuptools 81.0.0`: eine Meldung; Korrektur ab 83, aber aktuelles Torch verlangt `<82`.
- 3 nicht über PyPI prüfbare Pins: SAM-2-Git-Commit sowie lokale CUDA-Nightly-Versionen von Torch und Torchvision.
- `uv pip check`: 91 Pakete kompatibel.

Das Ergebnis bedeutet **nicht „keine Risiken“**, sondern: keine undokumentierte neue Abhängigkeitslücke gegenüber der gepflegten Ausnahmeakte.

### Aktuelle CI-Abdeckung

Der erfolgreiche CI-Lauf meldet:

- 292.678 von 642.664 Zeilen abgedeckt,
- **45,54 % Zeilenabdeckung**,
- Mindestwert **45,35 %**.

Das Gate schützt vor Rückgang, liegt aber nur 0,19 Prozentpunkte über der Grenze.

## Wichtigste Befunde

### P1 – vor Freigabe beheben

#### A-01: Neuer Druckcenter-Arbeitsstand verletzt zwei Architekturgrenzen

**Status:** bestätigt  
**Bereiche:** Code, Architektur, Qualität  
**Auswirkung:** lokaler Release-Weg rot

Die UI-Suite meldet:

1. `BuilderPageViewModel.cs` ist auf **1.067 physische Zeilen** angewachsen; erlaubt sind höchstens 1.000 für neue Überschreitungen.
2. `CostStoreCompatibility` wird dort jetzt viermal statt der erlaubten zweimal verwendet.

Belegstellen:

- `src/AuswertungPro.Next.UI/ViewModels/Pages/BuilderPageViewModel.cs:226-227`
- `src/AuswertungPro.Next.UI/ViewModels/Pages/BuilderPageViewModel.cs:284-286`
- `tests/AuswertungPro.Next.UI.Tests/MaintainabilityFitnessTests.cs:79`
- `tests/AuswertungPro.Next.UI.Tests/ViewModelInfrastructureBoundaryTests.cs:28-30`

**Bewertung:** Die Tests arbeiten richtig. Die Allowlist darf nicht auf vier erhöht und die 1.000-Zeilen-Grenze nicht aufgeweicht werden.

**Verbesserung:**

- Schachtkosten-Repositories über bestehende Application-Verträge und den `ServiceProvider` einspeisen.
- Konstruktion/Kompatibilitätsweg aus dem ViewModel entfernen.
- Bereichswechsel, Filterzustand oder Schacht-Refresh als kleine bestehende Partial-/Controller-Verantwortung ausgliedern.
- Danach die beiden fehlgeschlagenen Tests und die gesamte UI-Suite erneut ausführen.

**Abnahme:** 0 lokale Testfehler; keine neue Allowlist; `BuilderPageViewModel.cs` unter 1.000 Zeilen.

#### R-01: Trainingsrequest wird zu spät begrenzt und in 422-Antwort gespiegelt

**Status:** bestätigt durch Laufzeittest  
**Bereiche:** Robustheit, Sicherheit  
**Auswirkung:** unnötiger Speicherverbrauch, große Fehlerantworten, lokaler Denial-of-Service bei gültigem Token

`TrainingExportRequest` erlaubt bis zu 500 Bilder. `image_base64` besitzt nur `min_length=1`. Das 25-MB-Limit pro Bild wird erst in der Route geprüft, nachdem FastAPI/Pydantic den vollständigen JSON-Body samt Strings und Objektbaum aufgebaut hat.

Noch problematischer: Der Standard-422-Handler nimmt bei einem Modellvalidierungsfehler das ungültige Eingabeobjekt in die Antwort auf. Ein sicherer 100-KB-Test ergab:

- Request: 100.706 Bytes,
- Antwort: 100.345 Bytes,
- der Base64-Inhalt stand in der Antwort,
- Status 422.

Belegstellen:

- `sidecar/sidecar/schemas/segmentation.py:130-135`
- `sidecar/sidecar/schemas/segmentation.py:165-175`
- `sidecar/sidecar/routes/training.py:190-192`
- `sidecar/sidecar/routes/training.py:238-246`
- `sidecar/sidecar/main.py:59-64` – kein eigener `RequestValidationError`-Handler

Der Sidecar ist durch Loopback und Token geschützt; das begrenzt die Angriffsfläche. Ein fehlerhafter oder kompromittierter lokaler Client kann den Prozess trotzdem stark belasten.

**Verbesserung:**

- Request-Body-Limit als ASGI-Middleware **vor** JSON/Pydantic setzen.
- Zusätzlich maximale Base64-Länge pro Bild im Schema begrenzen.
- Das Gesamtbudget des Auftrags begrenzen; 500 × 25 MB darf nicht implizit möglich sein.
- Eigenen `RequestValidationError`-Handler einführen, der nur `loc`, stabilen Fehlercode und kurze Meldung ausgibt – niemals `input`, Base64 oder Manifestinhalt.
- Tests für 413, Aggregate-Limit und „Antwort enthält kein Bildfragment“ ergänzen.

**Abnahme:** übergroßer Body wird früh als 413 verworfen; 422-Antwort bleibt klein und enthält keine Nutzdaten.

#### F-01: Schachtquellen können gültige Empfehlungen still verdrängen

**Status:** statisch sehr wahrscheinlich; gezielter Grenztest fehlt  
**Bereiche:** Funktionen, Datenqualität  
**Auswirkung:** fehlende Schachtpositionen bzw. zu niedrige Kosten im Leistungsverzeichnis

Die neue Zusammenführung lässt die Matrix vor der Empfehlung gewinnen. Ob eine Matrix den Schacht „belegt“, wird nur mit `cost.Measures.Count > 0` entschieden.

Im übrigen Kostencode bedeutet „hat Maßnahmen“ strenger: mindestens eine ausgewählte Zeile mit positiver Menge. Ein `MeasureCost` darf leere, nicht ausgewählte oder mengenlose Zeilen enthalten. In diesem Fall blockiert der Matrixeintrag die Empfehlung, obwohl aus der Matrix keine wirksame Kostenposition exportiert wird.

Belegstellen:

- `src/AuswertungPro.Next.Infrastructure/Costs/SchachtLvCostLoader.cs:60-81`
- `src/AuswertungPro.Next.Domain/Models/Costs.cs:68-101`
- Vergleichsregel: `src/AuswertungPro.Next.Application/DataPage/SanierungCostFieldMapper.cs:159-160`

**Verbesserung:**

- Eine zentrale Fachregel „hat exportierbare Kostenzeile“ verwenden.
- Mindestens `Selected && Qty > 0` verlangen; zusätzlich negative Werte gemäß bestehender Kostenregeln abweisen.
- Drei Tests ergänzen: leere Maßnahme, nur abgewählte Zeile, ausgewählte positive Matrixzeile.

**Abnahme:** Eine unwirksame Matrix verdrängt keine gültige Empfehlung; eine wirksame Matrix bleibt eindeutig vorrangig und wird nie doppelt gezählt.

### P2 – nächster Qualitätsblock

#### R-02: Erwartbarer Lernstufenfehler wird als interner 500er behandelt

**Status:** bestätigt durch Laufzeittest  
**Bereiche:** Robustheit, API-Vertrag

`list_lernstufen` fängt `LernstufeError` ab. `classify_lernstufe` tut dies nicht. Ein simulierter ungültiger Klassen-/Hashzustand ergab `500 {"detail":"internal error"}` und einen vollständigen Trace im lokalen Log.

Beleg: `sidecar/sidecar/routes/yolo.py:217-251`.

**Verbesserung:** Erwartbare Kandidaten-, Klassen- und Hashfehler in einen stabilen 409/422- oder 503-Vertrag übersetzen und mit einem Endpoint-Test schützen.

#### Q-01: OSD-Dokumentation widerspricht der späteren frischen Abnahme

**Status:** bestätigt  
**Bereiche:** Funktionen, Dokumentation, Freigabeprozess

Commit `a0f9b2c44f59fd0d75dfb01ba172c612ca027921` dokumentiert einen frischen Satz `osd_abnahme_v1`:

- 120 Bilder aus 120 physischen Haltungen,
- keine Überschneidung mit `osd_mix_v1`,
- Vorlagenleser 44 richtig / 1 falsch,
- Kette 52 richtig / 1 falsch,
- **+8 richtige, 0 neue falsche**.

`CLAUDE.md` und `docs/quality/OSD-KETTENMESSUNG-2026-08-17.md` behaupten weiterhin, vor dem Einschalten werde erst noch ein frischer Bestand benötigt. Das ist sachlich überholt. Korrekt bleibt: Der Schalter ist standardmäßig aus, der Kandidatenstatus lautet weiterhin `diagnostic_not_deployed`, und die Produktaktivierung ist eine eigene Freigabeentscheidung.

Belegstellen:

- `CLAUDE.md:1185-1215`
- `docs/quality/OSD-KETTENMESSUNG-2026-08-17.md:59-66`
- `sidecar/sidecar/config.py:48-51`
- `sidecar/sidecar/models/osd_model_wrapper.py:28-32`

**Verbesserung:** Kanonische Dokumentation um die frische Messung und ihren Beleg-Hash ergänzen. Danach bewusst entscheiden:

- freigeben: Statuswechsel, Standardschalter, Regressionstest und Rollback-Schalter,
- oder nicht freigeben: fachlichen Grund und nächste Entscheidungsschwelle festhalten.

#### T-01: Python-CI prüft den Lock, testet aber andere, lose Versionen

**Status:** bestätigt  
**Bereiche:** Werkzeuge, Reproduzierbarkeit

Die CI auditiert `requirements-lock.txt`, installiert für den Testlauf aber:

- `pip install torch torchvision ...`
- `pip install -e ".[dev]"`

Die Abhängigkeiten im `pyproject.toml` sind überwiegend Mindestversionen. Damit kann ein grüner CI-Lauf eine andere Umgebung prüfen als die produktive Sperrdatei.

Belegstellen:

- `.github/workflows/ci.yml:64-84`
- `sidecar/pyproject.toml:6-27`

**Verbesserung:** CPU-kompatible, vollständig gepinnte CI-Sperrdatei oder Constraints-Datei erzeugen; danach Sidecar plus Dev-Zusätze exakt daraus installieren und mit `pip check`/`uv pip check` prüfen.

#### T-02: .NET-SDK ist nur auf die 10.0-Linie, nicht bytegenau gebunden

**Status:** bestätigt  
**Bereiche:** Build, Reproduzierbarkeit

`global.json` nennt 10.0.108 mit `latestFeature`; lokal wurde tatsächlich 10.0.111 verwendet, die CI nimmt `10.0.x`. NuGet ist gesperrt, aber das SDK kann sich ändern.

Microsoft weist ausdrücklich darauf hin, dass Paket-Locks den Einfluss einer SDK-Änderung nicht ausschließen; für vollständige Reproduzierbarkeit werden exakte SDK-Version und deaktiviertes Roll-forward empfohlen: [Microsoft – kontrollierte .NET-Versionen](https://learn.microsoft.com/en-us/dotnet/core/install/upgrade).

**Verbesserung:** CI und `global.json` auf dieselbe bewusst gewählte SDK-Version stellen. Wenn Roll-forward gewünscht ist, diesen Trade-off als bewusste Updatepolitik dokumentieren.

#### S-01: DINO- und SAM-Gewichte sind verfügbarkeits-, aber nicht hashgebunden

**Status:** bestätigt  
**Bereiche:** Sicherheit, KI-Reproduzierbarkeit

Der aktive YOLO-Detektor, BCC-, Lernstufen- und OSD-Kandidaten besitzen starke ID-/SHA-/Klassenkarten-Bindung. DINO und SAM suchen dagegen passende Gewichtdateien und melden deren Vorhandensein, aber kein festes Artefakt-Hash im API-/Trace-Vertrag.

Das ist bei lokaler Installation kein akutes Einfallstor. Es erschwert jedoch reproduzierbare Fehleranalyse und kann einen still ausgetauschten Modellstand unbemerkt lassen.

**Verbesserung:** Erwartete SHA-256-Werte in ein lokales Modellinventar aufnehmen, beim Laden prüfen und in Health/Trace ausgeben. Keine frei vom Client übergebenen Modellpfade.

#### O-01: Barrierefreiheit ist gegenüber der visuellen Qualität unterentwickelt

**Status:** bestätigt im XAML; echte Screenreader-Prüfung offen  
**Bereiche:** Optik, Bedienung

Positiv sind Schriftgrößen, Theme-Ressourcen, Textumbruch, Tooltips, Standardkurzbefehle und die Einstellung „Bewegung reduzieren“. In vier zentralen Oberflächen wurden jedoch keine expliziten `AutomationProperties.Name` gefunden; sichtbare Alt-Zugriffstasten sind ebenfalls nicht systematisch vorhanden.

Betroffene zentrale Dateien:

- `BuilderPage.xaml`: 650 Zeilen, 11 Buttons, 10 Checkboxen, 7 Comboboxen, 12 Menüeinträge,
- `SettingsPage.xaml`: 1.541 Zeilen, 22 Buttons, 14 Gruppen/Karten,
- `TrainingStudioWindow.xaml`: 563 Zeilen, 32 Buttons,
- `MainWindow.xaml`: 28 Menüeinträge.

Bei Textbuttons leitet WPF oft einen brauchbaren Namen aus `Content` ab. Symbolbuttons, Glyphen und komplexe Vorlagen brauchen dagegen explizite Namen/Hilfetexte.

**Verbesserung:** Zuerst Hauptnavigation, Druckcenter, Einstellungen und Training Studio mit Automation-Namen, Beschreibungen, Fokusreihenfolge und sichtbaren Fokuszuständen versehen; anschließend Windows Narrator und reine Tastaturbedienung prüfen.

### P3 – geplanter Abbau

#### C-01: Große XAML- und Orchestrierungsdateien bleiben Änderungshotspots

**Status:** bestätigt

| Datei | Zeilen |
|---|---:|
| `Theme/Controls.xaml` | 1.677 |
| `Views/Pages/SettingsPage.xaml` | 1.541 |
| `Views/Windows/PlayerWindow.xaml` | 1.178 |
| `ViewModels/Pages/BuilderPageViewModel.cs` | 1.067 aktuell |
| `ServiceProvider.cs` | 977 |

Große XAML-Dateien werden vom vorhandenen 1.000-Zeilen-C#-Gate nicht erfasst. Das ist kein unmittelbarer Fehler, erhöht aber Konflikt-, Such- und Reviewkosten.

**Verbesserung:** Kein Großumbau. Bei der nächsten echten Änderung jeweils einen abgeschlossenen Ressourcenblock bzw. ein UserControl herauslösen und mit einem Layouttest sichern.

#### C-02: Direkte `XDocument.Load`-Wege sind inkonsistent zum sicheren XML-Lader

**Status:** bestätigt, niedrige Priorität

Mehrere XTF-Helfer und ein Tool verwenden `XDocument.Load` direkt, während andere Importpfade den zentralen `SafeXmlDocumentLoader` nutzen.

Betroffen unter anderem:

- `XtfStammdatenElementReader.cs`,
- `XtfKanalschadenElementReader.cs`,
- `XtfRevisionWriter.cs`,
- `tools/FachwissenIndexer/Program.cs`.

.NETs Standardverhalten verhindert die klassische DTD-Ausführung in der üblichen Konfiguration; das ist daher kein bestätigter XXE-Durchbruch. Die direkte Nutzung umgeht aber einheitliche Grenzen und Tests für sehr große oder speziell präparierte XML-Dateien.

**Verbesserung:** Bei der nächsten XTF-Änderung auf den zentralen Lader umstellen und Größen-/DTD-Grenztests ergänzen.

#### T-03: 43 Werkzeuge sind gebaut, aber nicht als bedienbarer Werkzeugkatalog auffindbar

**Status:** bestätigt

Positiv: Alle 43 `.csproj` unter `tools` liegen in `AuswertungPro.sln` und werden im Release-Build mitgebaut. Funktionen, Ein-/Ausgaben, Schreibwirkung und typische Aufrufe sind jedoch hauptsächlich über das sehr große `CLAUDE.md`, einzelne README-Dateien und Quellcode verteilt.

**Verbesserung:** Eine kurze, generierbare `docs/WERKZEUGKATALOG.md` mit diesen Spalten pflegen:

- Name und Zweck,
- nur lesend / schreibt Daten,
- Eingaben und Ausgaben,
- typische Kommandozeile,
- benötigte Modelle/Programme,
- Test bzw. letzter verifizierter Lauf,
- Eigentümer/Status: produktiv, Diagnose, historisch.

#### Q-02: Abdeckungs-Gate ist knapp und misst nicht alle wichtigen Risikoarten

45,54 % sind für eine große WPF-/Integrationsanwendung respektabel, aber das Gate liegt nur knapp über 45,35 %. Hohe Testanzahl ersetzt keine gezielte Risikoabdeckung.

**Verbesserung:** Grenze nur nach nachweislichem Anstieg erhöhen. Priorität haben:

- Sidecar-Requestgrenzen und Fehlerverträge,
- Schachtkosten-Merge-Grenzfälle,
- Druckcenter-Auswahl und Totalkonsistenz,
- Modellstatus-/Hashwechsel,
- Barrierefreiheits-Smokes.

## Funktionsaudit

### Stark bzw. nachweislich vorhanden

- Projekt-, Haltungs- und Schachtdaten mit VSA-KEK-/EN-13508-2-Regeln.
- PDF-, XTF-, WinCan-, IBAK- und Medienimport mit vielen pro-Datei-Schutzwegen.
- Videoarbeit mit LibVLC, Checkpoint/Resume und Ausfallwächtern.
- Lokaler Sidecar für YOLO, DINO und SAM; Ollama/Qwen getrennt behandelt.
- QualityGate, zeitlicher Dedup, kontrollierte Degradierung und nachvollziehbare Traces.
- Kosten, Sanierungsmatrix, NPK/LV, Offerten und PDF-/Excel-Ausgaben.
- Wissen, Training, Gold-/Eval-Trennung und mehrere hashgebundene Prüfstrecken.
- QGIS-Brücke, Sicherung, Wiederherstellung und Diagnosepakete.

### KI-Funktionsstand

- Die allgemeine DINO → SAM → Qwen → Mapping → QualityGate-Kette ist produktiver Hauptweg.
- Der allgemeine eigene Mehrklassen-YOLO-Detektor bleibt zu Recht nicht freigegeben.
- BCC-Copilot und spezialisierte Kandidaten sind getrennt und stark gebunden.
- Der OSD-Modellrückfall ist technisch sauber additiv: Er ersetzt nie eine vorhandene Lesung.
- Auf verbrauchten Sätzen: +30 richtige, 0 neue falsche.
- Auf frischem `osd_abnahme_v1`: +8 richtige, 0 neue falsche.
- Aktivierung bleibt bewusst aus; Dokumentation und Entscheidung sind nachzuziehen.
- Die bekannte Zeichenfindungs-Lücke wird durch das Modell nicht gelöst.

### Aktueller Druckcenter-Funktionsstand

Die neue Richtung ist fachlich sinnvoll:

- Haltung/Schacht als Bereich,
- getrennte Exportabschnitte,
- Schachtmatrix plus freie Empfehlungen,
- Detail-, Maßnahmen-, Sonder-, Eigentümer-, Ausführungs- und Positionsübersichten,
- passende neue Tests.

Vor Freigabe fehlen aber die drei oben genannten Sicherheiten:

1. Architekturtests wieder grün,
2. Merge-Semantik für unwirksame Matrixeinträge klären,
3. echte PDF-Sichtprobe mit Haltung, Schacht, Mischkosten, fehlendem Preis und leerer Auswahl.

## Robustheitsaudit

### Positiv

- Kundenoriginale werden in den geprüften Revisions-/Importwegen nicht überschrieben.
- Viele Stores prüfen bestehende Dateien erneut, schreiben über Tempdatei und blockieren bei Lesefehlern.
- Sidecar-GPU-Slots besitzen Lease, Inhaltskennung, Watchdog und kontrollierte 503-Verträge.
- HTTP-Listener für LiveControl/QGIS besitzen feste Zeilen-, Header- und Mengenlimits.
- Sidecar-500er geben keine internen Details an den Client aus.
- Prozessausgaben werden in zentralen Wegen parallel gelesen; Argumente werden getrennt übergeben.
- Abbruch, Modellfehler und Kapazitätsfehler werden fachlich getrennt behandelt.

### Offen

- Requestgröße des Trainingsexports wird zu spät begrenzt.
- Lernstufenfehler besitzt keinen passenden API-Vertrag.
- Schachtkosten-Merge kann bei formal vorhandenen, fachlich unwirksamen Maßnahmen die zweite Quelle sperren.
- BCC-/Lernstufen-/OSD-Kandidaten werden zur Laufzeit wiederholt vollständig gehasht. Das stärkt Integrität, kostet aber bei großen Gewichten unnötig Zeit. Ein sicherer Cache müsste Dateigröße, Änderungszeit, Pfadidentität und privaten Snapshot binden; Integrität darf nicht für Tempo entfernt werden.

## Sicherheitsaudit

### Positiv

- Sidecar nur lokal, Trusted-Host-Schranke plus verpflichtender geheimer Token.
- Konstantzeitvergleich des Tokens.
- Keine frei wählbaren Modellpfade für die freigaberelevanten Kandidaten.
- Kandidaten-ID, SHA-256, Status und Klassenkarten werden in wichtigen YOLO-Pfaden geprüft.
- Pfad-Sandbox und Symlink-/Junction-Prüfungen im Trainingsexport.
- Keine gefundenen echten API-Keys, privaten Schlüssel, `.env`-Dateien oder Zertifikate im Git-Bestand.
- Keine `BinaryFormatter`-, TLS-Bypass- oder `AllowAnyOrigin`-Treffer.
- Shell-Öffnen besitzt Dateityp-Whitelist; zentrale Prozesswege nutzen `ArgumentList`.
- NuGet- und Python-Abhängigkeitsprüfung ist CI-Bestandteil.

### Restschuld

- bestätigte Base64-Spiegelung in 422,
- fünf dokumentierte Python-CVEs bleiben technisch offen,
- drei zentrale KI-Pakete/Commits sind durch den PyPI-Scanner nicht prüfbar,
- DINO/SAM ohne Gewichtshash im Laufzeitvertrag,
- bekannte Firebird-Werkseinstellung `SYSDBA/masterkey` bleibt für lokale Embedded-Dateien erhalten; Serverzugriffe verlangen hingegen explizite Werte.

`pip-audit` ist ein Abhängigkeits-, kein Quellcode-Scanner und garantiert keinen Schutz vor bösartigen Paketen. Das entspricht auch dem offiziellen Sicherheitsmodell: [PyPA pip-audit](https://github.com/pypa/pip-audit).

## Code- und Architekturaudit

### Positiv

- Domain, Application, Infrastructure und UI sind real getrennt.
- Neue Workflow-Orchestrierung wird durch Architekturtests Richtung `Application/UseCases` gedrückt.
- Zahlreiche Schnittstellen erlauben Verhaltenstests ohne reale Datei, GPU, Shell oder Fenster.
- Kompatibilitätsfassaden sind gemessen und dürfen laut Test nur sinken.
- Es gibt keine `TODO`, `FIXME`, `HACK` oder `NotImplementedException` in den geprüften Produktivbereichen.
- Die meisten `async void`-Treffer sind notwendige WPF-Ereignishandler; der zentrale Fire-and-forget-Weg protokolliert Fehler.
- Geldwerte verwenden `decimal`; Preise, Mengen und negative Werte besitzen Schutzregeln.
- Der neue `CostSummaryPdfSections`-Ansatz trennt PDF-Inhalte sinnvoll von der großen Factory-Aufrufsignatur.

### Druckstellen

- `ServiceProvider.cs` bleibt nahe an 1.000 Zeilen.
- Die UI besitzt weiter mehrere Kompatibilitätswege statt vollständiger Konstruktorverdrahtung.
- Training Studio ist funktional stark, aber mit 32 sichtbaren Aktionen auf einer Oberfläche sehr dicht.
- `OfferPdfModelFactory` enthält weiterhin mehrere unterschiedliche Zusammenfassungslogiken und ist ein fachlicher Hotspot. Weitere Abschnitte sollten als reine Builder/Sections entstehen, nicht durch mehr Verzweigungen in der Factory.
- Große XAML-Dateien haben kein entsprechendes Größen-/Komplexitätsgate.

## Optik- und Bedienungsaudit

### Positiv

- Einheitliche helle und dunkle Farbtokens.
- Klare Typografiestufen für Seite, Abschnitt, Text und Bildunterschrift.
- Dynamische Theme-Ressourcen statt verstreuter Festfarben.
- Mica nur auf passenden Fenstern; Videofenster bleiben aus Leistungsgründen davon ausgenommen.
- Bewegungsreduktion ist in Einstellungen und Animationen verdrahtet.
- Hover-, Fokus-, Karten- und Statussysteme sind zentralisiert.
- Tabellen besitzen Zeilenhöhe, Zoom, Spaltenauswahl und gespeicherte Ansichten.
- Das neue Druckcenter erklärt viele Optionen per Tooltip und trennt Datentabelle, Kennzahlen und Exportabschnitte.
- Settings wurden in sechs arbeitsbezogene Bereiche mit fester Navigation und scrollbarem Inhalt gegliedert.

### Verbesserungsbedarf

- Druckcenter und Training Studio haben hohe Aktionsdichte. Primäraktion, seltene Aktionen und Diagnosefunktionen sollten visuell noch klarer getrennt werden.
- 11-Punkt-Bildunterschriften sind bei hoher DPI bzw. längerer Arbeit klein; 12 Punkt als Untergrenze prüfen.
- Status darf nie nur durch Grün/Gelb/Rot vermittelt werden; Text oder Symbol muss immer parallel vorhanden sein.
- Symbol-/Browse-Buttons brauchen explizite Automation-Namen.
- Für das Druckcenter fehlt ein visueller Test der erzeugten PDF-Seiten. Template-Tests prüfen Inhalte, nicht Umbruch, Seitenwechsel, abgeschnittene Tabellen oder leere Überschriften.

## Werkzeugaudit

### Gut

- Gesamt-Solution baut Anwendung, Tests und alle 43 .NET-Werkzeuge.
- GitHub Actions sind auf Commit-Hashes statt bewegliche Tags gepinnt.
- Restore läuft im Locked Mode.
- NuGet- und Python-Sicherheitsprüfungen sind serverseitige Gates.
- Codeabdeckung darf nicht sinken.
- Sidecar-, QGIS- und alle vier .NET-Testprojekte laufen in CI.
- Eigene Werkzeuge existieren für Eval-Schutz, Dateninventar, Holdouts, Goldmigration, Sidecar-Smoke, Soak und Sicherungs-Smoke.

### Verbessern

- Python-CI exakt aus einer Test-Sperrdatei installieren.
- `pip-audit`-Version regelmäßig und bewusst aktualisieren; die CI pinnt 2.9.0, upstream dokumentiert inzwischen 2.10.1.
- Statische Quellcodeanalyse für Python ergänzen, zum Beispiel Ruff plus Bandit oder Semgrep mit kleiner, gepflegter Regelmenge. Nicht als Ersatz für Review oder Laufzeittests behandeln.
- SBOM für Releaseartefakte erzeugen; mindestens NuGet, Python, native Programme und Modellgewichte mit Hash erfassen.
- Werkzeugkatalog erzeugen.
- Nightly-Soak und Sidecar-E2E als planbaren, nicht für jeden Commit nötigen Workflow dokumentieren.

## Priorisierter Verbesserungsplan

### Phase 0 – Freigabeblockaden des aktuellen Arbeitsstands

**Ziel:** lokaler Release-Weg wieder vollständig grün.

1. Schachtkosten-Repositories sauber injizieren; keine neue Kompatibilitäts-Allowlist.
2. `BuilderPageViewModel.cs` unter die vorhandene Grenze bringen.
3. Schacht-Merge auf exportierbare, ausgewählte positive Zeilen umstellen.
4. Grenztests für leere/abgewählte Matrix und gültige Empfehlung ergänzen.
5. vollständigen Release-Build und alle Testprojekte erneut laufen lassen.

**Fertig, wenn:** 0 Fehler, 0 Warnungen, keine aufgeweichten Architekturtests.

### Phase 1 – Sidecar-Requesthärtung

**Ziel:** Speicher- und Fehlerantwort-Risiko schließen.

1. frühes Gesamt-Body-Limit,
2. Auftrag-, Bild- und Manifestbudget,
3. redigierter 422-Handler,
4. stabiler Lernstufen-Fehlervertrag,
5. CPU-Sidecar-Suite plus neue Missbrauchstests.

**Fertig, wenn:** 413 vor JSON-Aufbau; kein Base64 in Fehlerantworten; erwartbare Lernstufenfehler nie 500.

### Phase 2 – OSD-Entscheidung und Dokumentation

**Ziel:** technischer Stand und Freigabestatus stimmen wieder überein.

1. frische Abnahme in `CLAUDE.md` und Qualitätsbericht nachtragen,
2. Bericht-/Manifest-SHA verlinken,
3. Aktivierungsentscheidung treffen,
4. bei Aktivierung: Statuswechsel, Defaultschalter, Telemetrie für Leseweg/Fehlerquote, einfacher Rollback,
5. Zeichenfindungs-Lücke als getrenntes Vorhaben behandeln.

**Fertig, wenn:** Dokumentation, Konfiguration und Kandidatenstatus dieselbe Wahrheit ausdrücken.

### Phase 3 – Reproduzierbarkeit und Lieferkette

**Ziel:** lokal, CI und produktiv möglichst derselbe Bestand.

1. Python-CPU-CI-Lock,
2. exakte SDK-Politik,
3. DINO-/SAM-Hashes im Modellinventar,
4. SBOM und Hashliste,
5. Scanner-Updateprozess und Ablaufdatum für bekannte Ausnahmen.

**Fertig, wenn:** ein frischer Rechner dieselben Versionen und Hashes erhält oder bei Abweichung klar scheitert.

### Phase 4 – Oberfläche und Barrierefreiheit

**Ziel:** hohe Fachleistung bleibt auch bei langer täglicher Nutzung klar bedienbar.

1. Druckcenter manuell bei vier Skalierungen prüfen,
2. Narrator- und Tastaturrunde für Hauptnavigation, Druckcenter, Einstellungen, Training Studio,
3. Automation-Namen und Fokusreihenfolge ergänzen,
4. Druckcenter-PDFs rendern und visuell vergleichen,
5. große XAML-Blöcke nur bei Berührung schrittweise zerlegen.

**Fertig, wenn:** Kernaufgaben ohne Maus möglich, keine abgeschnittenen Inhalte, PDFs bei typischen und extremen Daten sauber.

### Phase 5 – laufende Qualitätssteigerung

**Ziel:** Gates langsam strenger machen, nicht nur mehr Tests zählen.

1. Abdeckung nach echten neuen Tests stufenweise erhöhen,
2. Werkzeugkatalog pflegen,
3. Nightly-Soak und Sidecar-E2E regelmäßig ausführen,
4. bekannte CVE-Ausnahmen monatlich prüfen,
5. Architektur-Allowlisten nur verkleinern.

## Empfohlene Reihenfolge

Wenn nur wenig Zeit vorhanden ist:

1. **Druckcenter-Gates und Schacht-Merge**, weil sie den aktuellen Arbeitsstand und Geldwerte betreffen.
2. **Trainingsrequest und 422-Redaktion**, weil der Befund bestätigt und leicht testbar ist.
3. **Lernstufen-Fehlervertrag**.
4. **OSD-Dokumentation und Freigabeentscheidung**.
5. **Python-CI-Lock und SDK-Bindung**.
6. **Barrierefreiheit und visuelle PDF-/DPI-Prüfung**.

## Schluss

SewerStudio braucht keinen Neuaufbau. Die Architekturtests zeigen gerade ihren Wert: Sie verhindern, dass eine fachlich sinnvolle Druckcenter-Erweiterung still wieder mehr Infrastruktur in ein großes ViewModel zieht. Die beste nächste Runde ist deshalb klein und konkret: den lokalen Arbeitsstand wieder grün machen, die drei bestätigten/hoch wahrscheinlichen Robustheitslücken schließen und danach OSD bewusst freigeben oder bewusst weiter gesperrt lassen.

