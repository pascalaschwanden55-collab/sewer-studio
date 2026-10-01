# SewerStudio – umfangreiche Codeanalyse zur Wartbarkeit

Stand: 27.09.2026 · Projekt: `C:\Sewer-Studio_KI_5.0` · Schwerpunkt: sichere und verständliche Weiterentwicklung.

Dieser Bericht gehört zum [Umsetzungsplan](UMSETZUNGSPLAN.md). Die [Messdaten und Prüfnachweise](nachweise/README.md) machen Zahlen und Fundstellen nachvollziehbar. Es wurden keine Änderungen am Produktcode vorgenommen.

## 1. Bewertung

**SewerStudio hat eine brauchbare Architektur und einen großen Bestand an Schutztests. Die Wartbarkeit wird vor allem durch sehr lange Abläufe, aufwendige Verdrahtung und Prüfungen belastet, die zu stark am geschriebenen Quelltext hängen.** Ein kompletter Neubau wäre aus diesen Befunden nicht begründbar. Den größten Nutzen versprechen klar begrenzte Verbesserungen an den häufig geänderten und fachlich empfindlichen Stellen.

Die bisherigen Aufräumarbeiten haben bereits Wirkung: Die fachlichen Kernschichten verweisen nicht auf WPF, die Projektverweise verlaufen in einer Richtung, wiederkehrende Dateioperationen besitzen Schutzbausteine, und neue KI-Abläufe haben einen vorgesehenen Platz in `Application/UseCases`. Diese Grundlagen sollten erhalten bleiben.

Die Grenze von 1.000 Zeilen pro Datei erfasst jedoch nur einen Teil des Problems. Es gibt **191 Methoden mit mehr als 100 Zeilen**, darunter **33 mit mehr als 200 Zeilen**. Der Hauptablauf der Mehrmodell-Analyse umfasst allein **825 Zeilen**. Umgekehrt sind Teile des Players auf sehr viele kleine Bausteine verteilt. Wartbarkeit bedeutet deshalb hier: zusammengehörige Abläufe verständlich machen, ihre Zustände begrenzen und ihr Verhalten zuverlässig prüfen.

Der neue vollständige Testlauf hat **einen fehlgeschlagenen UI-Test**: den Wächter für die beiden Dateien über 1.000 Zeilen. Die zwölf weiteren Fehler des früheren Audits treten im jetzigen Stand nicht mehr auf. Die folgenden Befunde gehen über diese Dateigrenze hinaus.

Die Prioritäten dieses Berichts sind **Wartungsprioritäten**, keine behaupteten Schweregrade nachgewiesener Laufzeitfehler. Insbesondere beweisen weder eine große Methode noch eine leere Fehlerbehandlung für sich einen Produktfehler.

## 2. Umfang und Vorgehen

### 2.1 Untersuchte Grundlage

- Branch: `feature/webgis-uebertragung`.
- Ausgangscommit: `0c2d5c17b8dcd8854900e9fbe6b3b80e740560a4`.
- Bewertet wurde der **Arbeitsstand mit vorhandenen, noch nicht eingecheckten Änderungen**. Der Commit allein reicht deshalb nicht zur Wiederherstellung dieses Stands.
- Die Statusaufnahme enthält 215 geänderte oder neue Pfade; dazu gehört bereits der zuvor gespeicherte Gesamtaudit-Bericht. Die Aufstellung steht in `nachweise/git-status-start.txt`.
- Für die untersuchten C#- und Python-Dateien wurden SHA-256-Prüfsummen erfasst. Kundenprojekte, Bilder, Videos und Modellgewichte waren kein Bestandteil der Inhaltsanalyse.

### 2.2 Prüfung

1. Projektregeln, Architekturkarte, vorhandene Wartbarkeitspläne und automatische Prüfungen abgeglichen.
2. C# mit dem vorhandenen Roslyn-Parser des .NET-SDK untersucht: Dateien, Typen, zusammengehörige `partial`-Klassen, Methoden, Parameter, Entscheidungsstellen und identische Methodenrümpfe.
3. Python-Dateien mit dem eingebauten Python-Parser untersucht.
4. Änderungshäufigkeit der letzten 90 Tage ab 29.06.2026 aus Git ausgewertet.
5. Auffällige Stellen einschließlich Aufrufern, Verträgen und vorhandenen Tests gezielt gelesen.
6. Vollständigen Release-Build und die vier .NET-Testprojekte ausgeführt. Zusätzlich drei relevante Testdateien der Trainingsskripte ausgeführt.

**Grenze:** Die automatisierte Bestandsaufnahme ist breit; die manuelle Prüfung konzentriert sich auf die unten belegten Stellen. Dies ist keine manuelle Einzelprüfung aller rund 375.000 C#-Produktzeilen und keine vollständige Bedienabnahme.

### 2.3 Messwerte

| Bereich | C#-Dateien | Physische Zeilen |
|---|---:|---:|
| Domain – gespeicherte und fachliche Modelle | 64 | 6.275 |
| Application – Verträge, Regeln und Anwendungsabläufe | 781 | 75.614 |
| Infrastructure – Dateien, Datenbanken, Importe und KI-Anbindungen | 706 | 131.905 |
| UI – WPF-Oberfläche und zugehörige Steuerung | 1.667 | 161.427 |
| **Produktcode gesamt** | **3.218** | **375.221** |
| C#-Testcode | 2.620 | 363.392 |
| C#-Werkzeuge unter `tools` | 157 | 22.280 |

Weitere Messwerte:

| Merkmal | Ergebnis | Einordnung |
|---|---:|---|
| C#-Methoden/Konstruktoren mit Körper im Produktcode | 14.879 | Eigenschaften, lokale Funktionen und Lambdas sind keine eigenen Einträge |
| Methoden über 100 / über 200 Zeilen | 191 / 33 | Geeignete Kandidaten für eine fachliche Prüfung |
| Methoden mit Entscheidungszähler über 20 | 163 | Eigene syntaktische Messung; keine normierte Komplexitätsnote |
| Methoden/Konstruktoren mit mehr als 8 Parametern | 155 | Ohne primäre Record-Konstruktoren |
| Produktionsdateien über 1.000 Zeilen | 2 | `AnnotationWorkbenchService.cs`, `MultiModelAnalysisService.cs` |
| Weitere Dateien zwischen 900 und 1.000 Zeilen | 23 | Nähe zur Grenze allein ist kein Fehlverhalten |
| Dateien unter `UI/Ai` | 599 | Zusammen 34.200 Zeilen |
| Dateien dort mit einer Workflow-Klassen-/Record-Deklaration | 220 | Textsuche als Strukturindikator |
| Exakt gleiche Methodenrümpfe ab 100 Syntax-Token | 12 Gruppen | Kleine und nur ähnliche Wiederholungen werden nicht erfasst |
| Testdateien mit ausgewählten Quelltext-/Dateilesemustern | 383 | Kandidatenmenge, keine Zahl ausschließlich quelltextbasierter Tests |
| Python-Dateien im ausgewählten Umfang | 305 | 93.189 Zeilen einschließlich Tests und Werkzeugen |
| XAML-Dateien unter `src` | 104 | Bei den C#-Zeilen oben nicht enthalten |

Der Entscheidungszähler zählt unter anderem `if`, Schleifen, `catch`, Fallzweige sowie `&&` und `||`, zuzüglich eines Ausgangswerts. Lange Zuordnungstabellen erhalten dadurch ebenfalls hohe Werte. Die fachliche Bewertung berücksichtigt diesen Unterschied.

## 3. Priorisierte Befunde

P1 = vor größeren Eingriffen angehen; P2 = gezielt verbessern; P3 = nachführen bzw. bei Gelegenheit erledigen.

| ID | Priorität | Befund | Hauptnutzen einer Verbesserung |
|---|---|---|---|
| W01 | P1 | Quelltextabhängige UI-Prüfungen erschweren sichere Umbauten | Prüfungen melden fachliche Brüche zuverlässiger |
| W02 | P1 | Mehrmodell-Analyse bündelt einen sehr langen, zustandsreichen Ablauf | Fehler-, Abbruch- und Wiederaufnahmewege werden überschaubar |
| W03 | P1 | Goldsample-Speichern ist eng mit dem UI-Dienst verbunden | Dauerhaftes Speichern und nachgelagerte Schritte werden klar getrennt |
| W04 | P1 | Import und Medienverteilung bündeln viele Phasen und Fehlergrenzen | Änderungen lassen sich je Phase absichern |
| W05 | P2 | XTF-Parser verbinden Lesen, Beziehungen und fachliche Zuordnung | Neue Felder gefährden weniger bestehende Importregeln |
| W06 | P2 | Player-Abläufe sind über viele kleine Bausteine verteilt | Ein Ablauf wird mit weniger Sprüngen verständlich |
| W07 | P2 | Zentrale Diensterzeugung ist ein häufig geänderter Sammelpunkt | Neue Funktionen berühren weniger zentrale Stellen |
| W08 | P1 | Trainingsskripte haben Tests, aber keinen entsprechenden CI-Schritt | Automatischer Schutz für wichtige Python-Fachabläufe |
| W09 | P2 | Große Python-Prüffunktionen bündeln zahlreiche Vertragsregeln | Änderungen an Trainingsnachweisen werden besser prüfbar |
| W10 | P2 | Gemeinsame Fachregeln sind teilweise doppelt implementiert | Regeln können künftig nur an einer Stelle auseinanderlaufen |
| W11 | P1 | Abdeckungsgrenze misst keinen eindeutigen Produktcode-Anteil | Aussagekräftigere Entscheidung über fehlende Tests |
| W12 | P2 | Globaler Zustand erhöht den Aufwand der Testisolation | Kleinere, unabhängig ausführbare Tests |
| W13 | P2 | Strukturgrenzen erfassen Methoden und Python nur unzureichend | Neue Wartungsschulden werden früher sichtbar |
| W14 | P3 | Architekturwissen und historische Arbeitsstände sind vermischt | Schnellerer Einstieg und weniger widersprüchliche Annahmen |

## 4. Befunde im Detail

### W01 – Quelltextprüfungen binden Tests an konkrete Schreibweisen

**Belege:** [PlayerWindowCodingMultiModelArchitectureTests.cs](../../../tests/AuswertungPro.Next.UI.Tests/PlayerWindowCodingMultiModelArchitectureTests.cs#L45), Zeile 45 ff.; [DesignAuditPlayerCodingSidePanelTests.cs](../../../tests/AuswertungPro.Next.UI.Tests/DesignAuditPlayerCodingSidePanelTests.cs#L376), Zeile 376 ff.; [UiArchitectureGuardTests.cs](../../../tests/AuswertungPro.Next.UI.Tests/UiArchitectureGuardTests.cs#L11), Zeile 11 ff.

Die Player-Prüfungen lesen mehrere Dateien und verlangen konkrete Aufruftexte, Parameternamen und Ausdrücke. Beispielsweise werden die Schreibweisen von `ResolveMeterForFrame` und `request.FrameOsdMeter` geprüft. Eine fachlich gleichwertige Auslagerung muss daher zusätzlich die Suchtexte berücksichtigen. Umgekehrt beweist ein passender Text nicht, dass zur Laufzeit wirklich Bild, Zeit und Meter derselben Aufnahme verwendet werden.

Das ist bei wichtigen Architekturverboten sinnvoll: Ein neuer Zugriff auf `App.Services` außerhalb der vorgesehenen Stelle soll auffallen. Für konkrete Datenflüsse ist dagegen das beobachtbare Ergebnis entscheidend. Der Bestand enthält bereits Verhaltenstests, auf die aufgebaut werden kann. Die hier genannten Quelltexttests bestehen im aktuellen Lauf; dieser Befund betrifft ihre Aussagekraft und den Aufwand bei künftigen Umbauten.

**Empfehlung:** Die betroffenen Tests einzeln nach geschützter Regel ordnen. Bildbindung, Abbruch, Ereigniserzeugung und menschliche Änderungen über Verhalten prüfen. Nur stabile Architekturverbote als Strukturtest behalten. Tests erst ersetzen, wenn der neue Test denselben Fehler nachweislich erkennt.

**Abnahme:** Ein absichtlich falsch weitergereichter Meter muss den Verhaltenstest brechen; eine reine Umbenennung oder gleichwertige interne Delegation soll ihn bestehen lassen. Das Ergebnis des aktuellen Gesamtlaufs steht in Abschnitt 6.

### W02 – Mehrmodell-Analyse hat zu viele Zustände in einem Ablauf

**Beleg:** [MultiModelAnalysisService.cs](../../../src/AuswertungPro.Next.Infrastructure/Ai/Pipeline/MultiModelAnalysisService.cs#L140), `AnalyzeAsync`, Zeile 140: **825 Zeilen**, Entscheidungszähler **103**. Zusammen mit zwei weiteren Teil-Dateien umfasst die Klasse 1.609 Dateizeilen.

Der Ablauf vereint Bildgewinnung, Wiederaufnahme, Modellaufrufe, Qualifikation, Messzeiten, Fehlerbehandlung, Zähler, Zusammenführung von Befunden und das abschließende Ergebnis. Insbesondere die Reihenfolge von Trace, Alterung der Befunde und Checkpoint ist fachlich wichtig.

Bereits verbessert sind `RecordRetryRequiredFrameAsync` und `RecordGeneralModelErrorAsync` ab Zeile 984. Der ältere Wartbarkeitsplan ist hier teilweise umgesetzt. Ein erneutes Herausziehen genau dieser Wiederholung wäre kein sinnvoller nächster Schritt.

**Empfehlung:** Einen klaren Zustand je Analyselauf und ein Ergebnis je verarbeitetem Bild einführen. Zuerst die bestehende Entscheidung über Fehler/Abbruch/Wiederholung isolieren. Danach jeweils einen Modellschritt hinter einen bereits passenden Vertrag stellen. Der äußere Ablauf behält die Reihenfolge und entscheidet über Fortsetzung und Abschluss. Gleichartige Fehlerfolgen teilen; modellabhängige Regeln ausdrücklich erhalten.

**Schutz:** Die vorhandenen `MultiModelAnalysisServiceResilienceTests`, `MultiModelAnalysisServiceVramTests`, `MultiModelAnalysisServiceSidecarRestartTests` und Checkpoint-Tests bilden den Einstieg. VRAM-Mangel darf weiterhin keinen Transport-Neustart auslösen. Abbruch und Fortsetzung müssen dieselben endgültigen Befunde wie ein durchgehender Lauf ergeben.

### W03 – Goldsample-Speichern braucht eine eigene Anwendungsgrenze

**Beleg:** [AnnotationWorkbenchService.cs](../../../src/AuswertungPro.Next.UI/Services/AnnotationWorkbenchService.cs#L327), `SaveCoreAsync`, Zeile 327: **339 Zeilen**, Entscheidungszähler **54**. Der Konstruktor ab Zeile 42 nimmt **18 Parameter** an. Die Klasse umfasst zwei Dateien mit insgesamt 1.144 Dateizeilen.

Der Dienst übernimmt Segmentierung, Vorschlagserzeugung und das Speichern geprüfter Trainingsdaten. Beim Speichern durchläuft er persönliche Bestätigung, Codes, Quellen, Schutz vor Prüfdatenvermischung, Bildkopie, Maskenprüfung, Sample-Speicherung sowie KB- und Teacher-Nachlauf. Diese Verantwortungen sind über Schnittstellen teilweise gut vorbereitet, bleiben aber in einem UI-Dienst zusammengezogen.

Private Schritte wie `StoreGoldImageAsync`, `PersistSampleAsync`, `RecordKbIndexAsync` und `RecordTeacherCandidateAsync` sind bereits vorhanden. Entscheidend ist jetzt eine klare Grenze: **Ab der dauerhaften Sample-Speicherung darf ein Fehler im Nachlauf nicht mehr „nicht gespeichert“ bedeuten.** Der Code dokumentiert das ausdrücklich. Auch die unterschiedliche Behandlung eines Abbruchs vor und nach diesem Punkt ist bestehendes Verhalten, kein bloßer Stilfehler.

**Empfehlung:** Einen Speichern-Anwendungsfall unter `Application/UseCases` planen. Er arbeitet mit den vorhandenen Speicherverträgen und liefert einen klaren Status für Ablehnung, Entwurf, Gold sowie Warnungen nach dem Speichern. Segmentierung und UI-Meldungsdarstellung verbleiben bei ihren eigenen Verantwortungen. Die bestehende öffentliche Fassade delegiert weiter.

**Abnahme:** Fehler vor der Sample-Speicherung, Duplikat, geänderte Bestandsversion, KB-Fehler nach dem Speichern und Teacher-Abbruch getrennt prüfen. Eine Strukturänderung darf keine neue Abbruchsemantik einführen.

### W04 – Import und Verteilung sind weiterhin große Abläufe

**Belege:** [ProjectImportOrchestrator.cs](../../../src/AuswertungPro.Next.Infrastructure/Import/ProjectImportOrchestrator.cs#L162), `Import`, Zeile 162: **490 Zeilen**, Entscheidungszähler **52**; [ParsedHoldingDistributionController.cs](../../../src/AuswertungPro.Next.Infrastructure/HoldingDistribution/ParsedHoldingDistributionController.cs#L14), `Distribute`, Zeile 14: **457 Zeilen**, **17 Parameter**, Entscheidungszähler **83**.

Der Import verbindet Vorbereitung, Wiederherstellungspunkt, Erkennung, Archivierung, Herstellerdaten, Anreicherung, Foto-/Video-/Protokollverteilung und Abschlussprüfung. Der Verteiler verbindet PDF-Korrektur, Suche nach Videos, Ausweichsuche, Zielpfade, Konflikte und Dateiausgabe. Die bloße Auslagerung in einen „Controller“ hat diese Komplexität nicht aufgelöst.

Positiv sind die vorhandene Fehlerbilanz, der Abbruch an Schrittgrenzen, gemeinsame Dateivorbereitung und abschließende Prüfung. Diese Regeln dürfen beim Aufteilen nicht über mehrere neue Stellen verteilt werden. Besonders die gemeinsame Fehlergrenze der Medienphase ist aktuell durch Tests festgehalten; ein Weiterlaufen nach jedem Teilschritt wäre eine bewusste Funktionsänderung.

**Empfehlung:** Zuerst Ein-/Ausgaben einer Phase benennen und durch einen kleinen Ergebnisdatensatz ausdrücken. Eine Phase nach der anderen auslagern, beginnend mit einem gut geschützten Teil des Medienwegs. Für die 17 Parameter fachliche Gruppen wie Quelle, Ziel und Suchkontext bilden; kein Sammelobjekt für alle Dienste und Zustände schaffen.

**Abnahme:** Bisherige Meldungen, Fehlerzahlen, Abbruchpunkte, Originalschutz, Wiederholbarkeit und Rücknahme bleiben gleich. Die Tests `ProjectImportOrchestratorTests`, `ProjectImportOrchestratorKinsTests` sowie Wiederherstellungs- und Dateischutztests bleiben maßgeblich.

### W05 – XTF-Lesen, Beziehungsauflösung und Abbildung trennen

**Belege:** [LegacyXtfImportService.cs](../../../src/AuswertungPro.Next.Infrastructure/Import/Xtf/LegacyXtfImportService.cs#L460), `ParseSia405`: **355 Zeilen**, Zähler **123**; [LegacyXtfImportService.VsaKek.cs](../../../src/AuswertungPro.Next.Infrastructure/Import/Xtf/LegacyXtfImportService.VsaKek.cs#L104), `ParseVsaKek`: **400 Zeilen**, Zähler **111**.

Die Methoden lesen mehrere Objektarten, sammeln Referenzen und übertragen die Ergebnisse in das Projektmodell. Viele Entscheidungen sind notwendige Feldzuordnungen. Problematisch ist die Vermischung mit Beziehungen, Ersatzwerten und Medienzuordnung im selben Lesefluss. Bei einer neuen Feldregel muss dadurch ein großer Teil des Ablaufs mitgedacht werden.

**Empfehlung:** Objektarten getrennt lesen, die Originalwerte samt Kennungen in kleinen internen Ergebnissen halten und danach Beziehungen auflösen. Fachliche Vorrangregeln und die Übernahme ins Projekt bleiben davon getrennt. Kein allgemeines Import-Framework und kein neues gespeichertes Format einführen.

**Abnahme:** Synthetische XTF-Dateien mit Organisationen, Rohrprofil, fehlendem Bezug, mehrfacher Untersuchung, Gegenbefahrung und Videozählerstand verwenden. Vorhandene `XtfImportTests`, `XtfInspectionDirectionImportTests` und die VSA-KEK-Schachtprüfungen bleiben erhalten. Parser-Zähler allein ist kein Grund, fachlich unterschiedliche Formate zu vereinheitlichen.

### W06 – Viele kleine Player-Bausteine erschweren den Überblick

**Belege:** [PlayerWindow.xaml.cs](../../../src/AuswertungPro.Next.UI/Views/Windows/PlayerWindow.xaml.cs#L16), Konstruktor: **539 Zeilen**; [PlayerWindow.Coding.Ai.MultiModel.cs](../../../src/AuswertungPro.Next.UI/Views/Windows/PlayerWindow.Coding.Ai.MultiModel.cs#L10); [CodingMultiModelInferenceWorkflow.cs](../../../src/AuswertungPro.Next.UI/Ai/Coding/CodingMultiModelInferenceWorkflow.cs#L1).

`PlayerWindow` verteilt sich auf **73 Dateien mit 4.260 Dateizeilen**. Unter `UI/Ai` liegen 599 Dateien. Der konkrete Mehrmodellweg verdrahtet Start, Laufzeitprüfung, Meterermittlung, Inferenz und Ergebnisbehandlung über verschachtelte Aktionen. Ein neues Teammitglied muss mehrere kleine Dateien verfolgen, bevor der Gesamtablauf klar wird.

Diese Aufteilung hat Logik aus dem Fenster entfernt und ist damit ein Fortschritt. Weitere Ein-Zeilen-Weiterleitungen würden den Überblick aber nicht zwangsläufig verbessern. Einige Ablaufverträge enthalten weiterhin WPF-Farben oder konkrete Infrastruktur-Ergebnisse; ein bloßes Verschieben nach Application würde falsche Abhängigkeiten mitnehmen.

**Empfehlung:** Einen vollständigen Ablauf als Pilot neu ordnen: „ein gebundenes Bild analysieren und Ereignisse erzeugen“. Fachlicher Ablauf und fachliches Ergebnis gehören in Application; Farben, Dispatcher, Steuerelemente und Anzeige bleiben im UI. Bestehende Einstiegspunkte delegieren. Kleine Regeln behalten, wenn sie eine eigene fachliche Bedeutung oder Wiederverwendung haben.

**Abnahme:** Der Pilot ist über einen Einstieg und wenige klar benannte Phasen verständlich. Die Zahl der weiterzureichenden Einzelaktionen sinkt, ohne einen neuen großen Dienst zu schaffen. Aufnahmebindung und menschliche Bearbeitung bleiben durch Verhaltenstests geschützt.

### W07 – Die Diensterzeugung bleibt ein Änderungsengpass

**Beleg:** [ServiceProvider.cs](../../../src/AuswertungPro.Next.UI/ServiceProvider.cs#L354), Konstruktor: **404 Zeilen**. Die gesamte Klasse umfasst 16 Dateien mit 1.509 Dateizeilen und 241 syntaktischen Property-Deklarationen. Das sind nicht 241 registrierte Dienste; der Registrierungstest erwartet **171** Einträge.

Die Hauptdatei wurde in der ausgewerteten 90-Tage-Historie in **170 Commits** berührt. Sie erzeugt Dienste, verbindet Konfiguration, bestimmt Pfade, migriert Einstellungen und baut mehrere Fachbereiche auf. Diese Häufigkeit allein beweist keinen Fehler, macht die zentrale Kopplung aber praktisch relevant.

`FullBackupComposition` und `TrainingYoloExportComposition` sind bereits passende Vorbilder. Ein zentraler Aufbaupunkt ist richtig; seine Verantwortung sollte auf das Zusammensetzen begrenzt bleiben.

**Empfehlung:** Eine zusammengehörige Gruppe nach der anderen als Aufbau-Baustein bündeln, zuerst Wissenspfade/Trainingsspeicher oder Importdienste. Die effektiven Pfade vor der Diensterzeugung einmal festhalten. Vorhandene öffentliche Eigenschaften und Dienstidentitäten erhalten. Ein neuer DI-Container ist dafür nicht erforderlich.

**Abnahme:** Derselbe Vertrag liefert weiterhin dieselbe langlebige Instanz. Der Aufbau lässt sich mit einem temporären Profil prüfen. Änderungen an einer Fachgruppe erfordern weniger Anpassungen in der zentralen Hauptdatei.

### W08 – Trainingsskripte fehlen im automatischen Python-Prüfweg

**Belege:** [.github/workflows/ci.yml](../../../.github/workflows/ci.yml#L79), Python-Job; [sidecar/pyproject.toml](../../../sidecar/pyproject.toml#L47), `testpaths = ["tests"]`; [training/scripts/tests](../../../training/scripts/tests).

Der konfigurierte Python-Job führt die Tests im Sidecar-Ordner und die QGIS-Tests aus. Die **38 `test_*.py`-Dateien unter `training/scripts/tests`** werden durch diese Befehle nicht automatisch gefunden. Auch der gelesene Pre-Push-Hook enthält dafür keinen Schritt.

Das betrifft wichtige Abläufe für Goldbestand, Klassenkarten, Prüfdatentrennung, Freigabenachweise und Trainingsvorbereitung. Die Tests sind vorhanden; es fehlt ihre sichtbare Einbindung in den gemeinsamen Prüfweg. Eine externe, hier nicht konfigurierte Automation ist damit nicht ausgeschlossen.

**Lokale Gegenprobe:** Drei ausgewählte Dateien – Goldbestandsprüfung, BCC-Vorbereitung und Detect-Gold-Vorbereitung – bestanden mit **66 Tests und 9 Untertests**. Daraus folgt keine Aussage über alle 38 Dateien.

**Empfehlung:** Einen ausdrücklich benannten CPU-Testschritt für diese Skripte ergänzen, zunächst nach Prüfung ihrer Abhängigkeiten und Seiteneffekte. GPU-Training und echte Daten bleiben separat. Der Testschritt soll auch dann laufen, wenn der Sidecar selbst unverändert ist.

### W09 – Python-Vertragsprüfung enthält sehr große Einzelfunktionen

**Beleg:** [gold_stock_audit.py](../../../training/scripts/gold_stock_audit.py#L928): `_read_reviewed_negative_set` mit **768 Zeilen**, Entscheidungszähler **183**; `_read_proto_reviewed_negative_set` ab Zeile 1.698 mit **682 Zeilen**, Zähler **196**. Die Datei hat 2.917 Zeilen.

Diese Funktionen prüfen zahlreiche sinnvolle Schutzregeln: exakte Felder, Rollen, Dateibindungen, Prüfsummen, Klassenkarten und Herkunft. Gerade weil diese Regeln die Trainingsdaten schützen, ist eine Änderung in einem so großen Ablauf aufwendig. Ähnliche Prüfkonzepte existieren zusätzlich in C# und in weiteren Python-Werkzeugen.

**Empfehlung:** Dateilesen, syntaktischen Vertrag, semantische Beziehungen, Schutzmengen und Inhaltsprüfung in nachvollziehbare Phasen trennen. Jede Phase bekommt einen klaren Eingang und ein geprüftes Ergebnis. Sprachübergreifende Regeln durch kleine gemeinsame Beispieldateien absichern, nicht durch unkontrollierte Code-Erzeugung ersetzen.

**Abnahme:** Manipulierte Prüfsumme, fremde Haltung, falsche Rolle, unpassende Klassenkarte, fehlende Datei und widersprüchliche Metadaten bleiben gesperrt. Zuerst W08 umsetzen, damit jeder folgende Umbau automatisch geprüft wird.

### W10 – Doppelte Regeln mit unterschiedlicher Bedeutung behandeln

**Belege:** [CatalogPriceResolver.cs](../../../src/AuswertungPro.Next.Application/Cost/CatalogPriceResolver.cs#L86) und [CostCalculatorLogicService.cs](../../../src/AuswertungPro.Next.Infrastructure/Costs/CostCalculatorLogicService.cs#L162): `FindNearestDnCandidates` hat einen exakt gleichen Methodenrumpf. Auch Mengenprüfung und Hinweiserzeugung sind in beiden Klassen vorhanden.

Die gezielte Aufrufsuche fand für die drei Infrastruktur-Helfer direkte Aufrufe in Tests, aber keine qualifizierten Aufrufe im Produktcode. Das spricht für verbliebenen Kompatibilitätsbestand und eine geeignete kleine Aufräumaufgabe; es belegt keinen aktuellen Preisfehler. Die öffentliche Fassade sollte zunächst an die Application-Regel delegieren. Die beiden `ParseDn`-Varianten unterscheiden sich bei der Kulturbehandlung und dürfen nicht ungeprüft zusammengezogen werden.

Eine zweite, empfindlichere Wiederholung ist `ValidateStrictNegativeClassMapBinding` in [TrainingExportPlanService.cs](../../../src/AuswertungPro.Next.Application/Ai/Training/ExportPlans/TrainingExportPlanService.cs#L496) und [TrainingExportPlanInputBuilder.cs](../../../src/AuswertungPro.Next.Infrastructure/Ai/Training/ExportPlans/TrainingExportPlanInputBuilder.cs#L110). Hier ist die Prüfung an zwei Grenzen sinnvoll; nur ihre Definition sollte gemeinsam sein.

**Abnahme:** Preisgleichstand, Bereichsgrenzen, leere Liste und Mengenregeln bleiben gleich. Die Klassenkartenprüfung wird weiterhin an beiden Grenzen ausgeführt und weist dieselben Manipulationen ab.

### W11 – Die Abdeckungsgrenze beantwortet die Produktcode-Frage nicht

**Belege:** [.github/coverage-baseline.json](../../../.github/coverage-baseline.json), ausdrücklich dokumentierte Einschränkung; [.github/scripts/check-coverage.ps1](../../../.github/scripts/check-coverage.ps1#L41), Addition der Berichtssummen.

Die aktuelle Untergrenze beträgt 45,35 Prozent. Laut eigener Dokumentation zählen Testcode und generierter Code mit. Das Skript summiert außerdem die Gesamtzahlen der einzelnen Testberichte. Es vereinigt Treffer nicht nach Quelldatei und Zeile; mehrfach erfasste gemeinsame Assemblies können dadurch mehrfach eingehen.

Diese Zahl ist ein bestehender Verlaufsschutz. Sie darf aber nicht als „45 Prozent des Produktcodes sind getestet“ erklärt werden. Für die Planung eines riskanten Umbaus fehlen damit verlässliche Teilwerte für den konkret betroffenen Produktbereich und seine Fehlerzweige.

**Empfehlung:** Zusätzlich eine eindeutige Produktcode-Sicht erzeugen: gleiche Quelldatei/Zeile nur einmal, Test- und generierter Code separat. Bestehende Grenze zunächst weiterführen. Neue Grenzen erst nach einer tatsächlichen, stabilen CI-Messung festlegen; keine frei gewählten Zielprozente.

**Grenze dieser Analyse:** Es wurde keine neue Coverage-Messung ausgeführt und keine aktuelle Abdeckungsquote behauptet. Testanzahl und grüne Tests ersetzen diese Messung nicht.

### W12 – Globaler Zustand macht Tests schwerer isolierbar

**Belege:** [AppSettings.cs](../../../src/AuswertungPro.Next.UI/AppSettings.cs#L39), statische Speicherkoordination; [KnowledgeBasePaths.cs](../../../src/AuswertungPro.Next.Infrastructure/Ai/KnowledgeBase/KnowledgeBasePaths.cs#L8), gemeinsamer Standarddienst; [ServiceProvider.cs](../../../src/AuswertungPro.Next.UI/ServiceProvider.cs#L408), Konfiguration dieses Dienstes. Testbelege: [Infrastructure/AssemblyInfo.cs](../../../tests/AuswertungPro.Next.Infrastructure.Tests/AssemblyInfo.cs#L1) und [AiPlatformConfigTests.cs](../../../tests/AuswertungPro.Next.UI.Tests/AiPlatformConfigTests.cs#L11).

Die UI- und Infrastrukturtests deaktivieren Parallelität auf Ebene der gesamten Testassembly. In der Infrastruktur ist geteilte Konfiguration ausdrücklich als Grund genannt. Der zentrale Aufbau verwendet zudem einen globalen Wissenspfad-Dienst mit veränderbarer Konfiguration. Viele Tests sichern Umgebungsvariablen von Hand und stellen sie anschließend zurück.

Das ist derzeit eine Schutzmaßnahme. Parallelität einfach einzuschalten würde diesen Schutz entfernen. Ziel sollte sein, mehr Abläufe unabhängig aufbauen zu können. Die bereits vorhandenen instanzbezogenen Dienste und injizierbaren Funktionen bieten dafür gute Ansatzpunkte.

**Empfehlung:** Pfade und Einstellungen pro aufgebautem Dienstverbund binden. Globale Standardfassaden aus Kompatibilitätsgründen zunächst erhalten. Tests mit unvermeidlichem globalem Zustand ausdrücklich gruppieren; reine Tests danach getrennt ausführen.

**Abnahme:** Zwei Testprofile können im selben Prozess unabhängig arbeiten. Die neue Aufteilung besteht auch wiederholt mit wechselnder Reihenfolge. Ein messbarer Zeitgewinn ist erst danach zu bewerten.

### W13 – Strukturregeln brauchen einen ergänzenden Blick auf Methoden

**Beleg:** [MaintainabilityFitnessTests.cs](../../../tests/AuswertungPro.Next.UI.Tests/MaintainabilityFitnessTests.cs#L8). Die Datei prüft große Einzeldateien, zusammengehörige Teilklassen und bestimmte globale Fassaden. Diese Prüfungen sind sinnvoll und sollen bleiben.

Es fehlt in diesem Prüfweg eine Grenze für das Wachstum bereits sehr langer Methoden. `AnalyzeAsync` zeigt das deutlich: Eine Datei kann knapp an der Dateigrenze liegen und fast vollständig aus einem einzigen Ablauf bestehen. Die Python-Skripte fallen außerdem nicht unter die C#-Dateigrenze.

**Empfehlung:** Eine kleine, nachvollziehbare Ausgangsliste der tatsächlich geprüften Problemmethoden pflegen. Zunächst nur weiteres Wachstum dieser Stellen und neue außergewöhnlich große Abläufe sichtbar machen. Für jeden Abbau fachlichen Nutzen und Verhaltenstest verlangen. Keine pauschale Jagd nach kurzen Methoden und keine Verteilung auf zusätzliche Teil-Dateien als Abnahmeersatz.

Die 40 auf IDE-Hinweise herabgestuften Analyzer-Regeln in [.editorconfig](../../../.editorconfig) sollten ebenfalls schrittweise geprüft werden. Ein Build ohne Warnungen bedeutet nicht, dass diese Hinweise verschwunden sind. Eine globale Verschärfung aller Regeln wäre ein eigenes, schlecht begrenztes Vorhaben.

### W14 – Aktuelle Regeln und Historie sind schwer auseinanderzuhalten

**Belege:** [CLAUDE.md](../../../CLAUDE.md), 5.260 Zeilen und 411.362 Bytes; [WARTBARKEITS-SCHULDEN.md](../../WARTBARKEITS-SCHULDEN.md), historischer Stand vom 12.07.; [MAINTAINABILITY_PLAN.md](../../../MAINTAINABILITY_PLAN.md), Stand vom 26.09.

Die Architekturdatei enthält aktuelle Regeln neben langen Berichten über abgeschlossene Schritte. Beispielsweise steht in der Klassenübersicht noch eine Beschreibung mit 141 Registrierungstypen, während der aktuelle Test 171 erwartet. Die historische Wartbarkeitsdatei erklärt, dass keine Produktionsdatei mehr über 1.000 Zeilen liegt; aktuell sind es wieder zwei. Datiert sind solche Aussagen erklärbar, als Einstieg erschweren sie dennoch die Orientierung.

**Empfehlung:** Eine kurze verbindliche Übersicht mit Schichten, Regeln, Buildweg und Verweisen führen. Detaillierte Fachregeln je Bereich ablegen. Historische Arbeitsstände bleiben als datierte Nachweise erhalten. Den persönlichen Architektur-Skill auf diese aktuellen Quellen ausrichten und überprüfbare Zahlen möglichst aus Messungen erzeugen.

**Abnahme:** Ein neuer Bearbeiter findet zu Import, KI, Speicherung und Tests jeweils eine klare aktuelle Quelle. Regeln bleiben erhalten; ältere Erfolge werden nicht als aktueller Zustand ausgegeben. Der Architektur-Skill ist nach tatsächlichen Strukturänderungen abzugleichen und zu validieren.

## 5. Bewertung der Bereiche

| Bereich | Was bereits trägt | Wichtigste Wartungsaufgabe |
|---|---|---|
| Domain | Kleiner Kern, keine WPF- oder Infrastructure-Imports im Suchlauf | Gespeicherte Modelle bei Umbauten stabil halten |
| Application | Richtige Projektabhängigkeit, viele reine Regeln und neue UseCases | Verbleibende Dateizugriffe bewusst begrenzen, neue Abläufe hier zusammenführen |
| Infrastructure | Viele spezialisierte Dienste, umfangreiche Fehler-/Dateischutztests | Lange Import-, Analyse- und Validierungsabläufe aufteilen |
| UI | Viel Logik bereits aus Fenstern ausgelagert; Zugriff auf globalen Service-Locator begrenzt | Ablaufketten vereinfachen, Speichern und fachliche Orchestrierung weiter entkoppeln |
| Sidecar | Kleine getrennte Modellrouten; eigener GPU-/Fehlerzustand und Tests | GPU-Lebenszyklus bei späteren Eingriffen zusammenhängend schützen |
| Trainingswerkzeuge | Strenge Herkunfts- und Schutzregeln sowie vorhandene Tests | CI-Anbindung und kleinere Vertragsprüfungen |
| QGIS-Brücke | Kleiner eigener Bereich und vorhandener CI-Testschritt | Bei Vertragsänderungen C#-/Python-Seiten gemeinsam prüfen |
| Prüfmittel | Vier .NET-Suiten, Architekturwächter, gesperrte Pakete und CI | Verhalten vor Quelltextform; klare Produktabdeckung |

Eine Textsuche findet in 34 Application-Dateien und 78 UI-Dateien Muster direkter Dateioperationen. Diese Zahl enthält auch Kommentare und gehört deshalb nur zur Kandidatenliste. Konkrete Application-Beispiele sind `AtomicTextFileWriter` und `ManifestCodeCatalogProvider`. Ein Teil dieser Platzierung ist ausdrücklich dokumentierter Bestand. Ein pauschales Verschieben würde derzeit Risiko und Aufwand erzeugen; neue Anwendungsabläufe sollen dagegen klare Speicherverträge nutzen.

Auch die **232 syntaktisch leeren `catch`-Blöcke im Produktcode** sind keine 232 bewiesenen Fehler. Einige dienen bewusst optionaler Suche oder Aufräumarbeiten, andere führen nach dem Block zu einer sichtbaren Meldung. Bei berührten kritischen Abläufen ist jeweils zu entscheiden: Abbruch weitergeben, Warnung liefern oder einen gesperrten Zustand zurückgeben. Vorhandene `BestEffort`- und Ergebnisverträge wiederverwenden.

## 6. Ausgeführte Prüfungen

Der vollständige Release-Build wurde in dieser Analyse erneut ausgeführt: **0 Fehler, 0 Warnungen**. SDK: **10.0.112**. Die Ergebnisse der vier Testprojekte werden aus ihren gespeicherten TRX-Dateien übernommen; der Detailnachweis steht in [testergebnisse.json](nachweise/testergebnisse.json).

| Prüfung | Bestanden | Fehlgeschlagen | Übersprungen | Laufzeit laut Protokoll |
|---|---:|---:|---:|---|
| Infrastruktur | 7.498 | 0 | 6 | 5 Min. 50 Sek. |
| KI-Pipeline | 2.853 | 0 | 3 | 13 Sek. |
| UI | 7.458 | **1** | 33 | 3 Min. 53 Sek. |
| ProjectModernizer | 62 | 0 | 0 | 93 ms |
| **.NET gesamt** | **17.871** | **1** | **42** | Keine Leistungsmessung |
| Trainingsskripte, gezielte Auswahl | 66 und 9 Untertests | 0 | 0 | 4,16 Sek. |

Der eine Fehler lautet `MaintainabilityFitnessTests.No_new_production_file_exceeds_1000_lines` und nennt die beiden Dateien mit 1.034 bzw. 1.016 Zeilen. Das ist ein bestätigter Verstoß gegen die bestehende Strukturregel. Er ist kein Nachweis einer falschen fachlichen Berechnung. Ein vollständig grüner Release-Prüfstand ist damit noch nicht erreicht.

Gegenüber dem früheren [Gesamtaudit vom selben Tag](../2026-09-27-gesamtaudit.md) wurden die zwölf zusätzlichen UI-Fehler nicht erneut beobachtet. Der frühere Bericht bleibt als historischer Stand erhalten.

Die Tests prüfen viele Verhaltens- und Schutzregeln. Eine große Anzahl grüner Tests beweist keine vollständige Testabdeckung. Übersprungene Tests und nicht ausgeführte GPU-/Live-Wege bleiben eigene Abnahmegrenzen. Parallel war ein fremder Infrastruktur-Testprozess im Debug-Modus sichtbar; Laufzeiten sind deshalb keine kontrollierten Leistungsvergleiche.

## 7. Bereits erledigte Schritte aus dem älteren Plan

Der [Plan vom 26.09.2026](../../../MAINTAINABILITY_PLAN.md) wurde mit dem jetzigen Code abgeglichen:

| Älterer Vorschlag | Heutiger Code | Konsequenz |
|---|---|---|
| Gemeinsame begrenzte Ganzzahlprüfung | `TryParseRangedInt` ist vorhanden | Nicht erneut als offenen Hauptbefund führen |
| Gemeinsame Katalog-Pfadsuche | `ResolveCatalogPath` ist vorhanden | Suchreihenfolge erhalten; keine zweite Auslagerung nötig |
| Wiederholte Modellfehlerfolge extrahieren | Zwei gemeinsame Hilfsmethoden vorhanden | Nächster Schritt ist der Gesamtzustand des Ablaufs |
| Speicherphasen benennen | Private Speicher-/KB-/Teacher-Methoden vorhanden | Als Nächstes den fachlichen Speichern-Anwendungsfall abgrenzen |
| Import, zentrale Diensterzeugung und Exportvalidierung vereinfachen | Weiterhin erkennbare Wartungsschwerpunkte | In kleine Pakete mit eigenen Abnahmen aufteilen |

Die neue Priorisierung setzt auf diesem Fortschritt auf. Kleine Wiederholungen in wenigen Zeilen sind gegenüber langen Zustandsabläufen und fehlenden Prüfwegen nachrangig.

## 8. Empfohlener Einstieg

Zuerst den aktuellen Prüfstand als verlässlichen Ausgangspunkt herstellen und die Trainingsskripte automatisch prüfen lassen. Danach eine kleine gemeinsame Fachregel als überschaubaren Einstieg bearbeiten. Anschließend jeweils **einen** großen Ablauf verbessern: Import, Mehrmodell-Analyse oder Goldsample-Speichern.

Der [Umsetzungsplan](UMSETZUNGSPLAN.md) beschreibt dafür Reihenfolge, konkrete Dateien, Schutztests, Abnahmekriterien, geschätzten Aufwand und Rücknahme. Neue Pakete, neue gespeicherte Formate, ein Austausch des UI-Frameworks und ein vollständiger Neubau sind aus dieser Untersuchung nicht erforderlich.

## 9. Grenzen der Aussage

- Es wurde kein neuer Produktfehler allein aus einer Kennzahl abgeleitet.
- Abhängigkeiten wurden über Projektverweise und gezielte Quelltextprüfung untersucht; es wurde kein vollständiger semantischer Laufzeit-Aufrufgraph berechnet.
- Die Duplikatsuche erfasst nur exakt gleiche Methodenrümpfe ab 100 Syntax-Token. Sie ist eine Untergrenze.
- Git-Häufigkeiten umfassen den lokalen Commitverlauf im Zeitraum; sie messen weder Entwicklungszeit noch Fehlerquote. Umbennenungen und Sammelcommits können die Einordnung beeinflussen.
- Nicht erneut geprüft wurden echte GPU-Inferenz, Kundenimporte, Live-WebGIS, vollständige Sicherungswiederherstellung und alle Trainingsskripte.
- Aufwandsschätzungen im Plan sind begründete Planungsbereiche, keine gemessenen Ausführungszeiten oder Zusage eines Fertigstellungstermins.
