# Code-Qualitaet: Zwischenstand 2026-07-15

Datum: 2026-07-15
Branch: `feature/gis-karte`
Ausgangsstand vor diesem Arbeitspaket: `aad83da9c6`
Grundlage: verdichtete Bewertung von sieben Pruefungen am echten Code

## Kurzurteil

SewerStudio steht architektonisch auf einem soliden Fundament. Die Schichten sind echt getrennt, die Domain ist rein und viele Grenzen werden durch Tests erzwungen. Die Gesamtnote bleibt **B (solide)**.

Der groesste Abstand zu einer A-Bewertung entsteht nicht durch schlechten Code, sondern durch drei noch unvollstaendige Netze:

1. Die UI besitzt noch Kompatibilitaetsfassaden fuer alte oeffentliche Konstruktoren.
2. Der echte End-to-End-Test der KI-Pipeline ist im normalen Testlauf ausgeschaltet.
3. Veraenderbare globale `Current`-/`Use`-Zustaende verhindern weiterhin eine sichere parallele Testausfuehrung.

## Bewertung je Bereich

| Bereich | Note |
|---|---:|
| Schichtenarchitektur und Logik-Platzierung | B |
| DI-Komposition und statisches `Current`/`Use` | C |
| God-Klassen und PlayerWindow-Zerlegung | B |
| KI-Pipeline und Thin-AI | B |
| Test- und Fitness-Architektur | B |
| Domaenenmodell und Konsistenz | B |
| Abgleich mit dem Selbst-Assessment | B |

## Was bereits stark ist

- Die Richtung `Domain <- Application <- Infrastructure <- UI` ist ohne Zyklen umgesetzt. Unerlaubte Abhaengigkeiten sickern nicht nach unten; die Domain bleibt rein.
- Vertraege liegen ueberwiegend in Application, konkrete Umsetzungen darunter. Das ist echtes Ports-and-Adapters und nicht nur eine Ordnerstruktur.
- Fitness- und Verdrahtungstests verhindern viele Rueckschritte automatisch.
- Die KI bleibt duenn: Zusammenfuehrung, Abstimmung, Code-Zuordnung und Schweregrad laufen kontrolliert in C#. Qwen liefert streng strukturierte Daten und darf bestaetigte Codes nicht ueberschreiben.
- Fehler werden sichtbar behandelt. Leere `catch`-Bloecke sind selten und fast nur fuer Aufraeumarbeiten vorhanden.

## Die drei wichtigsten offenen Bremsen

### 1. UI-zu-Speicher-Grenze

**Status: zwei Pakete am 2026-07-15 umgesetzt.**

Die direkten Erzeugungen der Kosten-, Vorlagen- und Dropdown-Speicher in den normalen ViewModels und Views wurden entfernt. Die UI erhaelt jetzt Application-Vertraege ueber Konstruktoren. Die konkreten Speicher werden am zentralen Zusammensetzungspunkt erzeugt.

Umgesetzt wurden:

- Application-Vertraege fuer Projektkosten, Kostenkatalog, Massnahmenvorlagen und Positionsvorlagen.
- Eine zentrale `ICostStoreFactory` mit Umsetzung in Infrastructure.
- Konstruktor-Injektion in Kostenrechner, Builder, Uebersicht, Export, Sanierungsmatrizen, Schachtseite und Editoren.
- Der Dossier-Druckcontroller laedt Projektkosten ebenfalls nur noch ueber den injizierten Application-Vertrag.
- Eine kleine Application-Schnittstelle fuer die Fall-IDs des Training Centers, damit `DataPageViewModel` keinen konkreten `TrainingCenterStore` mehr kennt.
- Ein verschaerfter Schutztest. Er erkennt auch voll qualifizierte Typnamen und die Kurzschreibweise `Store x = new()`.
- Der Schutz umfasst jetzt ViewModels, Views und die DataPage-Controller.
- Die direkte Allowlist ist auf genau einen bewusst getrennten Notfall-Fallback des Training-Center-Fensters reduziert.
- Bestehende oeffentliche Konstruktoren bleiben erhalten. Ihre zentralen Uebergangsfassaden sind separat gezaehlt und duerfen durch den Ratchet-Test nur noch sinken.

Keine gespeicherten Datenformate und keine oeffentlichen Programmfassaden wurden geaendert.

Zusaetzlich wurde `GetService` von der langen `if`-Kette auf eine zentrale Registrierung umgestellt. Unbekannte Dienste liefern nicht mehr still `null`, sondern einen sichtbaren Fehler. Die Dienstsuche liegt in einer kleinen Teildatei; ein Registrierungstest prueft alle eingetragenen Dienste.

### 2. Echter Golden-Lauf der KI-Pipeline

**Status: offen.**

Der Test `EchtesVideo_ErfuelltGoldenVertrag` existiert, wird im normalen Lauf aber uebersprungen. Er startet nur mit `SEWERSTUDIO_RUN_MACHINE_INTEGRATION=1` und braucht ein echtes Video ueber `SEWERSTUDIO_E2E_VIDEO`.

Damit ist der technische Test vorhanden, aber noch kein verpflichtendes Release-Gate. Eine schleichende Verschlechterung von YOLO, DINO, SAM oder Quantifizierung kann deshalb im normalen Testlauf unbemerkt bleiben.

### 3. Globale veraenderbare Zustaende

**Status: Obergrenze abgesichert, Abbau begonnen.**

Statische `Current`-/`Use`-Zugriffe und Kompatibilitaetsfassaden machen Abhaengigkeiten teilweise unsichtbar. Die Testpakete muessen deshalb weiterhin nacheinander laufen. Ein neuer Ratchet-Test friert alle bekannten veraenderbaren Service-Fassaden ein: Neue sind verboten, entfernte Altstellen muessen aus der Liste geloescht werden.

Als erstes kleines Paket wurde das VSA-Schattenprotokoll bereinigt. `VsaEvaluationService` erhaelt den Schreiber als Instanz, die zentrale Zusammensetzung verdrahtet ihn direkt und der globale `Use`-Umschalter ist entfernt. Die alte oeffentliche Fassade bleibt fuer bestehende Aufrufer erhalten, kann ihren Dienst aber nicht mehr global austauschen. Ein Test sichert diese Eigenschaft und die Ratchet-Obergrenze wurde entsprechend gesenkt.

Als zweites Paket wurde die rein interne `SqliteSnapshotCopier`-Fassade entfernt. Vollsicherung und Verzeichnisspiegel verwenden weiterhin denselben Application-Vertrag, erzeugen im Kompatibilitaetskonstruktor aber eine normale Instanz. Die zentrale Anwendung verdrahtet nach wie vor genau ihren registrierten Dienst. Oeffentliche Konstruktoren und das Sicherungsformat bleiben unveraendert; die Ratchet-Obergrenze sank erneut.

Als drittes Paket wurden die globalen Umschalter fuer das Oeffnen von Ordnern und das Finden des Programmordners entfernt. Der normale Einstellungs- und Diagnoseweg erhaelt beide Dienste direkt vom `ServiceProvider`. Bestehende Konstruktoren verwenden weiterhin feste Ersatzdienste, koennen diese aber nicht mehr global veraendern. Zwei Verdrahtungstests sichern das ab.

Als viertes Paket wurden `RepoRootLocator` und `FullBackupSourcesFactory` unveraenderlich gemacht. Die bestehenden Lese- und Hilfsmethoden bleiben erhalten. Der `ServiceProvider` registriert seine Projektordnersuche und Sicherungsquellen weiterhin als normale Instanzen; ein globaler Austausch ist nicht mehr moeglich. Damit sank die Ratchet-Obergrenze in diesem Arbeitspaket insgesamt um sechs Altstellen.

Als fuenftes Paket wurde die Bildvorschau fuer KI-Befunde direkt in den Player verdrahtet. Inline-Vorschau und Fotoanzeige verwenden im normalen Programmweg jetzt den registrierten `ICodingDefectPreviewRenderer`. Die alte statische Hilfsmethode bleibt mit einem festen Ersatzdienst erhalten, besitzt aber keinen globalen Umschalter mehr. Die Player-Groessengrenze blieb unveraendert; die Ratchet-Obergrenze sank damit insgesamt um sieben Altstellen.

Als sechstes Paket wurde der globale Umschalter der Trainingsbild-Vorschau entfernt. Das Training-Center erhaelt im normalen Programmweg weiterhin den registrierten `ITrainingPreviewFrameExtractor`. Die alte statische Fassade nutzt nur noch einen festen Ersatzdienst und kann nicht mehr auf eine andere Instanz umgeschaltet werden. Die Ratchet-Obergrenze sank damit insgesamt um acht Altstellen.

Als siebtes Paket wurde die Suche nach dem FFmpeg-Programm direkt durch den Trainingsablauf weitergegeben: vom Training-Center ueber die Auftrags- und Laufzeitvorbereitung bis zur Trainingssitzung. Der normale Programmweg verwendet damit den registrierten `ITrainingFfmpegPathResolver`. Bestehende oeffentliche Aufrufe bleiben erhalten und verwenden einen festen Ersatzdienst, koennen ihn aber nicht mehr global austauschen. Die Ratchet-Obergrenze sank damit insgesamt um neun Altstellen.

Als achtes Paket wurde der globale Umschalter der Projektdatei-Suche fuer Drag-and-drop entfernt. Die Uebersicht verwendet im normalen Programmweg weiterhin den direkt uebergebenen `IProjectDropPathResolver`. Die alte statische Hilfsmethode bleibt funktionsfaehig, nutzt aber nur noch einen festen Ersatzdienst. Die Ratchet-Obergrenze sank damit insgesamt um zehn Altstellen.

Als neuntes Paket wurde der globale Umschalter der Schacht-Dateizielsuche entfernt. Die Schacht-Seite verwendet im normalen Programmweg weiterhin den direkt uebergebenen `ISchachtFileTargetResolver`. Die alten Hilfsmethoden fuer PDF- und Explorer-Ziele bleiben funktionsfaehig und verwenden einen festen Ersatzdienst. Die Ratchet-Obergrenze sank damit insgesamt um elf Altstellen.

Als zehntes Paket wurde der globale Umschalter der Dichtheitsprotokoll-Suche entfernt. Die Datenseite verwendet im normalen Programmweg weiterhin den direkt uebergebenen `IDichtheitProtocolFileLocator`. Die bestehende statische `Resolve`-Hilfsmethode bleibt funktionsfaehig und verwendet einen festen Ersatzdienst. Die Ratchet-Obergrenze sank damit insgesamt um zwoelf Altstellen.

Als elftes Paket wurde der globale Umschalter der normalen Protokoll-Pfadsuche entfernt. Datenseite, Druckablauf, Builder und Karte verwenden weiterhin den direkt uebergebenen `IInspectionProtocolFileLocator`. Die bestehenden statischen Hilfsmethoden bleiben funktionsfaehig und verwenden einen festen Ersatzdienst. Die Ratchet-Obergrenze sank damit insgesamt um dreizehn Altstellen.

Als zwoelftes Paket wurde der globale Umschalter der VSA-Katalog-Pfadsuche entfernt. Der Programmstart verwendet weiterhin den registrierten `IVsaCatalogPathResolver` direkt. Die bestehenden statischen Hilfsmethoden, Katalognamen und Umgebungsvariablen bleiben funktionsfaehig und verwenden einen festen Ersatzdienst. Die Ratchet-Obergrenze sank damit insgesamt um vierzehn Altstellen.

Als dreizehntes Paket wurden die drei globalen Umschalter fuer Kataster-XTF-Pfade, Offline-Kartenordner und Kartenebenen entfernt. Karte, Export, Einstellungen und QGIS verwenden im normalen Programmweg weiterhin die direkt uebergebenen Dienste. Die bestehenden statischen Hilfsmethoden und Kartenbezeichnungen bleiben funktionsfaehig und verwenden feste Ersatzdienste. Die Ratchet-Obergrenze sank damit insgesamt um siebzehn Altstellen.

Als vierzehntes Paket wurde der globale Umschalter des Protokoll-Trainingsspeichers entfernt. Das Training-Center erhaelt den registrierten `IProtocolTrainingStore` direkt und gibt ihn bis zur Export-Fabrik weiter. Bestehende statische Speicheraufrufe bleiben mit einem festen Ersatzdienst funktionsfaehig. Die Ratchet-Obergrenze sank damit insgesamt um achtzehn Altstellen.

Als fuenfzehntes Paket wurde der globale Umschalter der Telemetrie-Pfadsuche stillgelegt. Seitenwagen-, Pipeline- und VSA-Protokollschreiber erhalten im normalen Programmweg weiterhin denselben registrierten `ITelemetryPathResolver` direkt. Die bestehende oeffentliche Fassade bleibt lesbar, kann den Dienst aber nicht mehr global austauschen. Die Ratchet-Obergrenze sank damit insgesamt um neunzehn Altstellen.

Als sechzehntes Paket wurde der globale Umschalter des NPK-Excel-Exports stillgelegt. Der Builder verwendet im normalen Programmweg weiterhin den registrierten `INpkLeistungsverzeichnisExcelExporter` direkt. Die bestehende oeffentliche `BuildWorkbook`-Fassade bleibt funktionsfaehig, kann den Dienst aber nicht mehr global austauschen. Die Ratchet-Obergrenze sank damit insgesamt um zwanzig Altstellen.

Als siebzehntes Paket wurde der globale Umschalter des PDF-Zusammenfuegedienstes stillgelegt. Datenseite und Builder verwenden im normalen Programmweg weiterhin den registrierten `IPdfMergeService` direkt. Die bestehenden statischen PDF-Hilfsmethoden bleiben funktionsfaehig, koennen den Dienst aber nicht mehr global austauschen. Die Ratchet-Obergrenze sank damit insgesamt um einundzwanzig Altstellen.

Als achtzehntes Paket wurde der globale Umschalter der Sicherungs-Manifestpruefung stillgelegt. Die Vollsicherung verwendet im normalen Programmweg weiterhin den direkt uebergebenen `IBackupManifestIntegrityService`. Die bestehenden statischen Hilfsmethoden fuer Manifestaufbau und -pruefung bleiben funktionsfaehig, koennen den Dienst aber nicht mehr global austauschen. Die Ratchet-Obergrenze sank damit insgesamt um zweiundzwanzig Altstellen.

Als neunzehntes Paket wurden die drei globalen Umschalter der KINS-Textanreicherung, DBF-Anreicherung und Gesamtprotokollsuche stillgelegt. Der Ein-Knopf-Import erhaelt alle drei registrierten Dienste weiterhin direkt. Die bestehenden statischen KINS-Hilfsmethoden bleiben funktionsfaehig, koennen ihre Dienste aber nicht mehr global austauschen. Die Ratchet-Obergrenze sank damit insgesamt um fuenfundzwanzig Altstellen.

Als zwanzigstes Paket wurden die globalen Umschalter der IBAK-Verbindungsoptionen und der KIAS-Exporterkennung stillgelegt. IBAK-Import und Kanalexport-Erkennung erhalten ihre registrierten Dienste weiterhin direkt. Die bestehenden statischen Hilfsmethoden bleiben funktionsfaehig, koennen ihre Dienste aber nicht mehr global austauschen. Die Ratchet-Obergrenze sank damit insgesamt um siebenundzwanzig Altstellen.

Als einundzwanzigstes Paket wurden die globalen Umschalter fuer Kataster-Tabelle und Kataster-Index stillgelegt. Der zentrale Index wird weiterhin mit der registrierten Tabellenablage aufgebaut und der Exportseite direkt uebergeben. Die bestehenden statischen Kataster-Hilfsmethoden bleiben funktionsfaehig, koennen ihre Dienste aber nicht mehr global austauschen. Die Ratchet-Obergrenze sank damit insgesamt um neunundzwanzig Altstellen.

Als zweiundzwanzigstes Paket wurde der globale Umschalter des KI-Pipeline-Traces stillgelegt. Die Videoanalyse-Fabrik erhaelt den registrierten `IPipelineTraceWriter` weiterhin direkt und gibt ihn an die eigentliche Pipeline weiter. Die statischen Trace-Hilfsmethoden bleiben funktionsfaehig, koennen den Schreiber aber nicht mehr global austauschen. Die Ratchet-Obergrenze sank damit insgesamt um dreissig Altstellen.

Als dreiundzwanzigstes Paket wurde der globale Umschalter der Wissensdatenbank-Pruefung stillgelegt. Der Programmstart verwendet den registrierten beziehungsweise gezielt injizierten `IKnowledgeBaseHealthInspector` weiterhin direkt und zeigt erkannte Schaeden wie bisher als klare Startwarnung. Die statische Pruefhilfe bleibt funktionsfaehig, kann den Dienst aber nicht mehr global austauschen. Die Ratchet-Obergrenze sank damit insgesamt um einunddreissig Altstellen.

Als vierundzwanzigstes Paket wurde der globale Umschalter der KI-Pipeline-Umgebungswerte stillgelegt. Die Videoanalyse-Fabrik erhaelt die registrierten `IPipelineEnvironmentOptions` weiterhin direkt und gibt sie an die Pipeline weiter. Die statischen Hilfsmethoden fuer Umgebungsvariablen bleiben funktionsfaehig, koennen den Dienst aber nicht mehr global austauschen. Die Ratchet-Obergrenze sank damit insgesamt um zweiunddreissig Altstellen.

Als fuenfundzwanzigstes Paket wurde der globale Umschalter der GPU-Modellwahl stillgelegt. Der zentrale KI-Einstellungsdienst erhaelt den registrierten `IGpuModelSelector` weiterhin direkt. Die statischen Regeln und die automatische Modellwahl bleiben funktionsfaehig, koennen den Erkennungsdienst aber nicht mehr global austauschen. Die Ratchet-Obergrenze sank damit insgesamt um dreiunddreissig Altstellen.

Als sechsundzwanzigstes Paket wurde der globale Umschalter des Speichers fuer KI-Sanierungssitzungen stillgelegt. Die Datenseite erhaelt den registrierten `IAiOptimizationSessionStore` direkt und reicht ihn an die Optimierungsansicht weiter. Die statischen Speicherhilfen bleiben funktionsfaehig, koennen den Dienst aber nicht mehr global austauschen. Die Ratchet-Obergrenze sank damit insgesamt um vierunddreissig Altstellen.

Als siebenundzwanzigstes Paket wurden die drei globalen Umschalter fuer KI-Prozessverwaltung, Sidecar-Startpfadsuche und Sidecar-Token-Aufloesung stillgelegt. Automatischer Start, manueller Start und Start aus den Einstellungen erhalten die registrierten Dienste jetzt direkt; beim Programmende wird weiterhin dieselbe Prozessverwaltung verwendet. Die bestehenden statischen Hilfsmethoden bleiben funktionsfaehig, koennen ihre Dienste aber nicht mehr global austauschen. Die Ratchet-Obergrenze sank damit insgesamt um siebenunddreissig Altstellen.

Als achtundzwanzigstes Paket wurde der globale Umschalter des KI-Einstellungsdienstes stillgelegt. Die zentralen Startwege erhalten den registrierten `IAiPlatformSettingsResolver` jetzt zusammen mit den uebrigen KI-Startdiensten direkt. Die bestehenden statischen Parser und Ladehilfen bleiben funktionsfaehig, koennen den Dienst aber nicht mehr global austauschen. Die Ratchet-Obergrenze sank damit insgesamt um achtunddreissig Altstellen.

Als neunundzwanzigstes Paket wurde der globale Umschalter des Prozessausgabe-Lesers stillgelegt. Videoanalyse, Schnellscan, Videopruefung und Selbsttraining erhalten den registrierten `IProcessOutputReader` jetzt direkt. Die statische Kompatibilitaetshilfe bleibt funktionsfaehig, kann den Leser aber nicht mehr global austauschen. Die Ratchet-Obergrenze sank damit insgesamt um neununddreissig Altstellen.

Als dreissigstes Paket wurde der nicht mehr benoetigte globale Umschalter des Video-Frame-Extraktors stillgelegt. Der zentrale ServiceProvider registriert `IVideoFrameExtractor` weiterhin direkt; die bestehende statische Extraktionshilfe bleibt fuer Altaufrufer funktionsfaehig, kann ihre Instanz aber nicht mehr global austauschen. Die Ratchet-Obergrenze sank damit insgesamt um vierzig Altstellen.

Als einunddreissigstes Paket wurde der globale Umschalter des Speichers fuer Trainings-Einstellungen stillgelegt. Einzeltraining, Stapelimport und Sample-Erzeugung erhalten den registrierten `ITrainingCenterSettingsStore` jetzt direkt. Die statischen Lade- und Speicherhilfen bleiben fuer Altaufrufer funktionsfaehig, koennen ihre Instanz aber nicht mehr global austauschen. Die Ratchet-Obergrenze sank damit insgesamt um einundvierzig Altstellen.

Als zweiunddreissigstes Paket wurde der globale Umschalter des Selbsttraining-Verlaufsspeichers stillgelegt. Trainingslauf, letzte Trefferquote und Wissensdatenbank-Trend erhalten den registrierten `ISelfTrainingHistoryStore` jetzt direkt. Die statischen Verlaufshilfen bleiben fuer Altaufrufer funktionsfaehig, koennen ihre Instanz aber nicht mehr global austauschen. Die Ratchet-Obergrenze sank damit insgesamt um zweiundvierzig Altstellen.

Als dreiunddreissigstes Paket wurde der globale Umschalter des Trainings-Frame-Speichers stillgelegt. Einzeltraining, Sample-Erzeugung und Stapelimport erhalten den registrierten `ITrainingFrameStore` jetzt direkt. Die statischen Frame-Hilfen bleiben fuer Altaufrufer funktionsfaehig, koennen ihre Instanz aber nicht mehr global austauschen. Die Ratchet-Obergrenze sank damit insgesamt um dreiundvierzig Altstellen.

Als vierunddreissigstes Paket wurde der globale Umschalter der Ablage fuer Lehrer-Annotationen stillgelegt. Trainingsfenster, YOLO-Export, Live-Erkennung und Importbestaetigung erhalten den registrierten `ITeacherAnnotationStore` jetzt direkt. Die statischen Annotationshilfen bleiben fuer Altaufrufer funktionsfaehig, koennen ihre Instanz aber nicht mehr global austauschen. Die Ratchet-Obergrenze sank damit insgesamt um vierundvierzig Altstellen.

Als fuenfunddreissigstes Paket wurde der globale Umschalter des VSA-Code-Nutzungszaehlers stillgelegt. Player, Training-Center und Beobachtungsfenster geben den registrierten `ICodeUsageTracker` jetzt direkt an das Codefenster weiter. Die Kompatibilitaetsfassade bleibt fuer alte Aufrufer lesbar, kann den Zaehler aber nicht mehr global austauschen. Die Ratchet-Obergrenze sank damit insgesamt um fuenfundvierzig Altstellen.

Als sechsunddreissigstes Paket wurde der globale Umschalter des Sidecar-Telemetrieschreibers stillgelegt. Videoanalyse, Player-KI, Trainings-Review und YOLO-Export erhalten den registrierten `ISidecarTelemetryWriter` jetzt direkt. Die statischen Schreib- und Pfadhilfen bleiben fuer Altaufrufer funktionsfaehig, koennen ihren Schreiber aber nicht mehr global austauschen. Die Ratchet-Obergrenze sank damit insgesamt um sechsundvierzig Altstellen.

Als siebenunddreissigstes Paket wurde der globale Umschalter des Statusfarben-Dienstes stillgelegt. Die Farblogik ist zustandslos und verwendet weiterhin dieselben zentral geprueften Farbwerte. Die bestehende Lese-Fassade bleibt fuer Anzeige-Modelle und Renderer funktionsfaehig, kann den Dienst aber nicht mehr global austauschen. Die Ratchet-Obergrenze sank damit insgesamt um siebenundvierzig Altstellen.

Als achtunddreissigstes Paket wurde der globale Umschalter des sicheren Datei- und Ordneroeffners stillgelegt. Die fest vorgegebenen Sicherheitsregeln und alle bestehenden `TryOpen`-Aufrufe bleiben unveraendert. Der registrierte `ISafeShellOpenService` wird weiterhin fuer direkt verdrahtete Wege verwendet; die statische Kompatibilitaetsfassade kann ihren Dienst nicht mehr global austauschen. Die Ratchet-Obergrenze sank damit insgesamt um achtundvierzig Altstellen.

Als neununddreissigstes Paket wurden die globalen Umschalter der PDF-Groessenpruefung und der atomaren PDF-Ersetzung stillgelegt. Groessen- und Seitenlimits, Sicherungskopien sowie der Wiederherstellungs-Fallback bleiben unveraendert. Die registrierten Dienste werden in den direkt verdrahteten Importwegen weiterhin gemeinsam verwendet; die statischen Kompatibilitaetsfassaden koennen ihre Instanzen nicht mehr global austauschen. Die Ratchet-Obergrenze sank damit insgesamt um fuenfzig Altstellen.

Als vierzigstes Paket wurden die globalen Umschalter der M150-Quell- und MDB-Leser stillgelegt. XTF- und WinCan-Import erhalten die registrierten Leser jetzt direkt; auch der WinCan-XTF-Rueckfall verwendet den bereits aufgebauten XTF-Importdienst. Die grossen Importklassen wurden fuer ihren Aufbau in kleine Teildateien getrennt und bleiben unter 1.000 Zeilen. Statische Altaufrufe verwenden weiterhin feste Ersatzleser, koennen diese aber nicht mehr global austauschen. Die Ratchet-Obergrenze sank damit insgesamt um zweiundfuenfzig Altstellen; dreizehn veraenderbare Fassaden bleiben.

Als einundvierzigstes Paket wurden die drei globalen Umschalter fuer PDF-Text, PDF-OCR und PDF-Formularfelder stillgelegt. PDF-Import, Schachtprotokoll, IBAK-Stammdaten und PDF-Verteilung erhalten die zentral registrierten Leser im normalen Programmweg weiterhin direkt. Die statischen Altaufrufe bleiben mit festen Ersatzlesern funktionsfaehig, koennen diese aber nicht mehr global austauschen. Die Ratchet-Obergrenze sank damit insgesamt um fuenfundfuenfzig Altstellen; zehn veraenderbare Fassaden bleiben.

Als zweiundvierzigstes Paket wurden die fuenf globalen Umschalter des PDF-Verteilungsblocks stillgelegt: Dateiuebertragung, PDF-Seitenleser, Text-Layer-Korrektur, Schacht-PDF-Auswahl und Videokonflikt-Kandidaten. Die registrierten Dienste sind weiterhin direkt miteinander verdrahtet; statische Altaufrufe verwenden feste, zustandslose Ersatzdienste. Dateiablage, PDF-Aufteilung, Korrektur und Konflikthinweise bleiben unveraendert. Die Ratchet-Obergrenze sank damit insgesamt um sechzig Altstellen; nur noch fuenf breiter genutzte Fassaden bleiben.

Als dreiundvierzigstes Paket wurde der globale Umschalter des FFmpeg- und FFprobe-Finders stillgelegt. Die Suche bleibt zustandslos und prueft weiterhin Umgebungsvariable, lokale Installation und `PATH`. Direkt verdrahtete Dienste behalten den registrierten `IFfmpegExecutableLocator`; statische Altaufrufe verwenden einen festen Finder. Die Ratchet-Obergrenze sank damit insgesamt um einundsechzig Altstellen; vier zustandsbehaftete oder besonders breit genutzte Fassaden bleiben.

Als vierundvierzigstes Paket wurde der globale Umschalter der persistierten VSA-zu-YOLO-Klassenkarte stillgelegt. Live-Erkennung im Player und lokaler YOLO-Export im Training-Center erhalten jetzt die am zentralen Zusammensetzungspunkt erzeugte `IVsaYoloClassMapStore`-Instanz direkt. Gespeicherter Kartenpfad, bestehende Klassen-IDs und Exportformat bleiben unveraendert. Statische Altaufrufe verwenden eine feste Kompatibilitaetsinstanz, koennen die Karte aber nicht mehr global austauschen. Die Ratchet-Obergrenze sank damit insgesamt um zweiundsechzig Altstellen; drei zustandsbehaftete oder besonders breit genutzte Fassaden bleiben.

Als fuenfundvierzigstes Paket wurde der globale Umschalter des persistierten Trainingssample-Speichers stillgelegt. Training-Center, Selbsttraining, Stapelimport, Review, Protokoll-Startdaten, Wissensdiagnose und Codiermodus erhalten jetzt den registrierten `ITrainingSampleStore` direkt. Der vorhandene Eval-Schutz, atomare Schreibweg, Dateipfad und das JSON-Format bleiben unveraendert. Statische Altaufrufe verwenden eine feste Kompatibilitaetsinstanz, koennen den Speicher aber nicht mehr global austauschen. Die Ratchet-Obergrenze sank damit insgesamt um dreiundsechzig Altstellen; nur noch `KnowledgeBasePaths` und `DialogHost` bleiben als globale veraenderbare Altstellen.

Als sechsundvierzigstes Paket wurde der globale Umschalter des Dialogdienstes entfernt. Hauptfenster, Player-Schnellscan und Trainingsimport erhalten im normalen Programmweg den registrierten `IDialogService` direkt. `DialogHost` bleibt nur als fester Notfall-Ersatz fuer alte Fensteraufrufe und kann nicht mehr umkonfiguriert werden. Bestaetigungen, Warnungen und Fehlermeldungen bleiben unveraendert. Die Ratchet-Obergrenze sank damit insgesamt um vierundsechzig Altstellen; nur noch `KnowledgeBasePaths` bleibt als globale veraenderbare Altstelle.

Als siebenundvierzigstes Paket wurde auch der letzte globale Dienstaustausch in `KnowledgeBasePaths` gesperrt. Der `ServiceProvider` verwendet weiterhin genau dieselbe Pfaddienst-Instanz und setzt den gespeicherten Wissensordner wie bisher beim Start. Umgebungsvariable, gespeicherter Pfad, Cache, Altdatei-Uebernahme und alle Dateinamen bleiben unveraendert. Nur ein spaeter globaler Austausch des ganzen Dienstes ist nicht mehr moeglich. Damit sank die Ratchet-Obergrenze insgesamt um fuenfundsechzig Altstellen auf **null**.

## Weitere mittlere Themen

- Das Test-Gate ist noch nicht als getrackter CI-Ablauf auf einer zweiten Umgebung abgesichert.
- Viele Architekturpruefungen arbeiten mit Quelltext-Suche. Fuer echte Strukturgrenzen ist das nuetzlich, fuer Verhalten bleiben normale Tests vorzuziehen.
- Mehrere grosse ViewModels liegen knapp unter der erlaubten Groessengrenze. Neue Verantwortung darf dort nicht mehr hinzukommen.
- `HaltungRecord` und `SchachtRecord` enthalten weiterhin viele frei geschriebene Feldnamen. Nur ein kleiner Teil der fachlichen Schluessel ist typisiert.

## Pruefung dieses Arbeitspakets

- Vollstaendiger Release-Build: **0 Warnungen, 0 Fehler**.
- Infrastrukturtests: **2.740 bestanden, 1 bewusst uebersprungen**.
- Pipeline-Tests: **1.856 bestanden, 1 bewusst uebersprungen**.
- UI-Tests: **4.734 bestanden**.
- ProjectModernizer-Tests: **62 bestanden**.
- Zusaetzlich: **32** gezielte Wissenspfad-, Speicher- und Architekturpruefungen bestanden.

Insgesamt sind damit **9.392 Tests bestanden**. Zwei maschinengebundene beziehungsweise datenabhaengige Tests wurden wie vorgesehen uebersprungen.

## Empfohlene naechste Reihenfolge

1. Den echten KI-Goldenlauf vor jedem Release verpflichtend ausfuehren und Ergebnis/Artefakte speichern.
2. Das komplette Test-Gate in einen getrackten, auf einer zweiten Umgebung wiederholbaren Ablauf bringen.
3. Direkte Infrastruktur-Erzeugung in Views und ViewModels hinter die bereits vorhandenen Schnittstellen legen.
4. Danach grosse ViewModels und die frei geschriebenen Domaenenfelder in kleinen, verhaltenstest-geschuetzten Paketen angehen.
