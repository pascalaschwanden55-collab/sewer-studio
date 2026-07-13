# Wartbarkeits- und Belastbarkeits-Deepscan SewerStudio 4.5.0

## Umsetzungsstand vom 13.07.2026

Die wichtigsten Sofortmaßnahmen aus diesem Bericht sind umgesetzt und geprüft:

- **Release-Protokollierung repariert:** Im Produktionscode gibt es keine `Debug.WriteLine`-Stelle mehr. Wichtige Warnungen und Fehler aus KI, Training, Import, Backup und Dateizugriff landen über `BestEffort` im Tageslog. Reine Statusinformationen verwenden `Trace`.
- **Echter Log-Dateitest ergänzt:** Ein Integrationstest prüft, dass eine `BestEffort`-Warnung tatsächlich als Warnung in die Tageslogdatei geschrieben wird.
- **Schacht-Seite verkleinert:** Die Sanierungsmaßnahmen- und Speicherlogik wurde in `SchachtMassnahmenDialogController` ausgelagert. `SchaechtePage.xaml.cs` liegt nun mit 930 Zeilen wieder unter der Grenze von 1.000 Zeilen.
- **Protokoll-Editor entkoppelt (A1-04 erledigt):** Request-Aufbau, KI-Aufruf und Fehlerbehandlung liegen im testbaren `ProtocolEntryEditorKiViewModel`; die VSA- und Gesamtprüfung liegt im `ProtocolEntryEditorValidationViewModel`. Der Dialog zeigt nur noch Ergebnisse und Feldmarkierungen an. Laufende KI-Aufrufe werden beim Schließen abgebrochen; technische Fehlerdetails landen im Tageslog statt im Nutzerdialog. `ProtocolEntryEditorDialog.xaml.cs` sank von 943 auf 789 Zeilen.
- **Medien-Suche entkoppelt (A1-07, Teilpaket):** `BatchMediaSearchService` wird zentral im `ServiceProvider` bereitgestellt und dem Fenster zusammen mit nur den benötigten Dialog- und Einstellungsdiensten übergeben. Das Fenster erzeugt den Dienst nicht mehr bei jedem Suchlauf und hält den ganzen Container nicht mehr als Feld. Technische Suchfehler landen im Tageslog.
- **Fenster-Dienste zentralisiert (A1-07 erledigt):** Medien-Suche sowie alle Training-Center-Dienste werden zentral bereitgestellt. Review-Warteschlange, SAM und Few-Shot bleiben bedarfsgesteuert; die Review-Warteschlange wird über alle Fenster geteilt. Das Fenster erzeugt diese Datei- und Netzwerkdienste nicht mehr selbst.
- **Diagnose-ViewModel entkoppelt (A1-05, Teilpaket):** `DiagnosticsPageViewModel` erhält nur noch `ILogTailReader` statt des ganzen `ServiceProvider` und greift nicht mehr direkt auf Dateien zu. `DailyLogTailReader` liegt in Infrastructure, liefert höchstens die letzten 200 Zeilen und protokolliert technische Fehler, während die Oberfläche eine verständliche Meldung zeigt.
- **Schattenauswertung entkoppelt (A1-05, Teilpaket):** Das ViewModel erhält Projektzugriff, `ISchattenAuswertungStore`, Service-Fabrik und Projektpfad gezielt statt `ShellViewModel` und `ServiceProvider`. Laden, Berechnen, Speichern und Fehlerpfad sind ohne echte KI oder Dateien testbar; rohe technische Fehler erscheinen nicht mehr in der Oberfläche.
- **VSA-Seite entkoppelt (A1-05, Teilpaket):** `VsaPageViewModel` speichert weder `ShellViewModel` noch `ServiceProvider`. Projektzugriff, Statusaktionen sowie XTF-, PDF-, VSA- und Maßnahmendienst werden gezielt übergeben. Zwei Tests sichern die Abhängigkeiten und den vollständigen Lauf mit Wiederherstellungspunkt, relativen Quelldateien und Projektänderung.
- **Schacht-Matrix entkoppelt (A1-05, Teilpaket):** Das ViewModel erhält Projekt, Projektpfad, Dialoge und Aktualisierungsmeldung gezielt. Der gesamte Container und die Shell werden nicht mehr gespeichert; fünf Tests schützen Laden, Kostenberechnung, Speichern und die Abhängigkeitsgrenze.
- **Medienkonflikte entkoppelt (A1-05, Teilpaket):** Projektzugriff, Quellordner, Dialoge und Video-Start werden gezielt übergeben. Das ViewModel erzeugt weder den Konfliktdienst noch ein Player-Fenster selbst. Drei Tests schützen die Abhängigkeitsgrenze, den fehlenden Projektordner und den Video-Start.
- **Einstellungen entkoppelt (A1-05, Teilpaket):** Einstellungen, Diagnose, Dialoge, Sicherungsdienst, Sicherungsstatus, Meldungen und Programmbereinigung werden gezielt übergeben. Der gemeinsam genutzte Sicherungsstatus bleibt erhalten; die Programmbereinigung wird zentral erzeugt. 156 Einstellungs- und Schutztests sind grün.
- **Export entkoppelt (A1-05, Teilpaket):** Das Export-ViewModel erhält Einstellungen, Dialoge, Excel-Export, Meldungen und Kostenabgleich gezielt. Es speichert den zentralen Container nicht mehr. 14 Export- und Verteilungstests schützen die betroffenen Abläufe.
- **Karte entkoppelt (A1-05, Teilpaket):** Das Karten-ViewModel erhält Einstellungen, Kartendaten und den Video-Start gezielt. Es speichert den zentralen Container nicht mehr; der Video-Player wird außerhalb des ViewModels erzeugt. 34 Karten-Tests schützen Navigation, Pfade und Darstellung.
- **Übersicht entkoppelt (A1-05, Teilpaket):** Das Übersichts-ViewModel erhält Einstellungen, Aktualisierungsmeldung, Dialoge und Projektablage gezielt. Es speichert den zentralen Container nicht mehr. 27 Übersichts-Tests schützen Projektauswahl, Vorschau, Navigation und den Schutz ungespeicherter Änderungen.
- **Schacht-Maßnahmen entkoppelt (A1-05, Vorbereitung):** Der Maßnahmen-Controller erhält nur noch Einstellungen, Dialoge und den Maßnahmenkatalog. Er kennt den zentralen Container nicht mehr. Die bestehenden 24 Schacht- und Maßnahmen-Tests schützen Dialogübergabe, Berechnung und Katalogbearbeitung.
- **Schacht-Seite entkoppelt (A1-05, Teilpaket):** Das Schacht-ViewModel erhält Einstellungen, Dialoge sowie die drei Schacht-Dienste gezielt. ViewModel und Seite speichern den zentralen Container nicht mehr. 29 fokussierte Tests schützen Pflichtfeldwarnung, Protokollbefehle, Maßnahmen und Seitenaufbau.
- **Sanierungs-Matrix entkoppelt (A1-05, Teilpaket):** Das ViewModel erhält Einstellungen, Dialoge, Kostenabgleich und Cockpit-Aktualisierung gezielt. Es speichert den zentralen Container nicht mehr. Zwei neue ViewModel-Tests schützen Projektwurzel, Haltungsaufbau und die Abhängigkeitsgrenze.
- **Projekt-Portabilität zentralisiert (A1-05/A1-07, Vorbereitung):** Der dateibearbeitende Portabilitätsdienst besitzt jetzt einen Application-Vertrag und wird einmal zentral bereitgestellt. Das Import-ViewModel erzeugt ihn nicht mehr selbst. Sechs echte Datei-Tests schützen Pfadumstellung, Fotokopien und Namenskollisionen.
- **Import-Portabilitätsablauf ausgelagert (A1-05, Teilpaket):** Projektprüfung, Fortschritt, Speichern und Ergebnisaufbereitung liegen im testbaren `ImportProjectPortabilityController`. Das Import-ViewModel stößt den Ablauf nur noch an; zwei Verhaltenstests schützen Warnung und Erfolgsfall.
- **Projekt-Fotozuordnung zentralisiert (A1-05/A1-07, Vorbereitung):** Auch die dateibearbeitende Fotozuordnung besitzt jetzt einen Application-Vertrag und wird einmal zentral bereitgestellt. Das Import-ViewModel erzeugt sie nicht mehr selbst. Sechs echte Datei-Tests schützen Zuordnung, Kopieren und relative Projektpfade.
- **Import-Fotozuordnungsablauf ausgelagert (A1-05, Teilpaket):** Projektprüfung, Ordnerauswahl, Fortschritt, Speichern und Ergebnisaufbereitung liegen im testbaren `ImportProjectPhotoAssignmentController`. Das Import-ViewModel stößt den Ablauf nur noch an; zwei Verhaltenstests schützen Warnung und Erfolgsfall.
- **Import-Protokollwerkzeuge ausgelagert (A1-05, Teilpaket):** Verteilung und Neuerzeugung eigener Protokolle liegen in zwei kleinen, getrennt testbaren Controllern. Das Import-ViewModel stößt die Abläufe nur noch an; vier Verhaltenstests schützen Warnungen, Speichern, Status und sichere Fehleranzeige.
- **Protokoll-Neuerzeugung abstrahiert (A1-10, Vorbereitung):** Der dateibearbeitende Dienst ist über `IProtocolRegenerationService` erreichbar und zentral verdrahtet. Die bisherige öffentliche statische Fassade bleibt für bestehende Aufrufer unverändert.
- **Ein-Knopf-Import ausgelagert (A1-05/A1-10, Teilpaket):** Formatprüfung, Hintergrundlauf, Speichern und Ergebnisanzeige liegen im `ImportOneClickProjectController`; der Textbericht wird von einem dateibearbeitenden Dienst geschrieben und Fehler landen im Tageslog. Das Import-ViewModel sank dadurch auf 686 Zeilen. Drei UI-Tests und ein echter Dateitest schützen Start, Erfolg, unbekannte Formate und Berichtinhalt.
- **Import-Berichtsnavigation ausgelagert (A1-05, Teilpaket):** Projektpfad, letzter Bericht, Berichtsordner und sicheres Öffnen liegen im `ImportReportNavigationController`. Das Import-ViewModel kennt keine letzte Berichtsdatei mehr und sank auf 654 Zeilen; zwei Tests schützen fehlende Ordner und den erfolgreichen Öffnungspfad.
- **CSV-Importbericht ausgelagert (A1-05/A1-10, Teilpaket):** Steuerung, Fehlerschutz und Dateiausgabe liegen außerhalb des ViewModels; technische Schreibfehler gehen ins Tageslog. Der neue Adapter verwendet den vorhandenen, atomar schreibenden `ProjectFieldCsvExporter` statt einer zweiten CSV-Logik. Das Import-ViewModel sank auf 603 Zeilen; drei UI-Tests und ein Adaptertest ergänzen die acht bestehenden CSV-Tests.
- **VSA-Katalogstatus ausgelagert (A1-05, Teilpaket):** SEC-/NOD-Erkennung, Statusaufbau und Neuladen liegen im `ImportCatalogController`. Technische Ladefehler gehen ins Tageslog statt als Rohtext in die Oberfläche. Das Import-ViewModel sank auf 563 Zeilen; drei Tests schützen Startstatus, SEC+NOD und fehlenden NOD-Katalog.
- **VSA-Importnachlauf ausgelagert (A1-05, Teilpaket):** Hintergrundbewertung, Fortschritt und Ergebnistext liegen im `ImportVsaEvaluationController`. Technische Fehler gehen ins Tageslog und werden in der Oberfläche nicht mehr offengelegt. Zwei Tests schützen Erfolg und sicheren Fehlerfall; das Import-ViewModel liegt bei 559 Zeilen.
- **Importseite vom God-Container getrennt (A1-05, Teilpaket):** `ImportPageViewModel` speichert den `ServiceProvider` nicht mehr. Dialoge, Einstellungen, Projektablage und die fünf manuellen Importdienste werden gezielt gehalten; alle ausgelagerten Abläufe erhalten ebenfalls nur ihre benötigten Dienste. Ein Architekturtest verhindert die Rückkehr des Containers.
- **Datenseite schrittweise entkoppelt (A1-05, Teilpaket):** Dialoge und Einstellungen werden als eigene Abhängigkeiten gehalten; sämtliche entsprechenden Laufzeit-Zugriffe über den `ServiceProvider` sind entfernt. Ein Architekturtest schützt diese Grenze, während der restliche Container in weiteren kleinen Paketen abgebaut wird.
- **Service-Locator der Datenseite entfernt (A1-05, Teilpaket):** Ansicht und Teildateien greifen nur noch gezielt auf Dialoge, Einstellungen, VSA-Auswertung und Codekatalog zu. Das Beobachtungsfenster hält ebenfalls nur noch die benötigten Einstellungen; die bisherige öffentliche Fenster-Schnittstelle bleibt unverändert nutzbar. Der Architekturtest prüft alle Teildateien gemeinsam.
- **Datenseiten-Fachdienste entkoppelt (A1-05, Teilpaket):** PDF-Ausgabe, Kostenabgleich, Cockpit-Aktualisierung, Mediensuche, Protokollzugriff und Maßnahmenempfehlung werden als klar benannte Abhängigkeiten gehalten. Nur die zwei Erzeugungsfunktionen für Videoanalyse und Sanierungs-KI verbleiben bis zum nächsten Teilpaket am Container.
- **Datenseite vom God-Container getrennt (A1-05 erledigt):** Auch Videoanalyse und Sanierungs-KI werden über zwei kleine, getestete Fabriken erzeugt. Die Datenseite und sämtliche ihrer Teildateien speichern den `ServiceProvider` nicht mehr. Ein schmaler Fensterstarter kapselt nur noch die Verträglichkeit zu den bestehenden Player- und Protokollfenstern. Der Architekturtest prüft die gesamte Teilklasse statt nur die Hauptdatei.
- **Protokoll-PDF abstrahiert (A1-06, Teilpaket):** `IProtocolPdfExporter` bildet den stabilen Exportvertrag. Datenseite, Druckcenter und der aktive Player-/Protokollpfad verwenden die Schnittstelle; bisherige öffentliche Eigenschaften und Konstruktoren mit `ProtocolPdfExporter` bleiben als Verträglichkeitsfassade erhalten. Zwei Tests schützen den Vertrag und den Player-Pfad. Der Umzug der QuestPDF-Implementierung nach Infrastructure bleibt offen.
- **FFmpeg-Abbruch gehärtet (A2-01 erledigt):** Die Einzelbild-Extraktion nutzt den gemeinsamen Prozessleser. Bei Abbruch oder Fehler wird der gesamte FFmpeg-Prozessbaum beendet; ein echter Prozess-Test prüft, dass kein Hintergrundprozess weiterläuft.
- **Training-Center-Abbruch gehärtet (A2-02 erledigt):** Das Fenster besitzt einen eigenen Lebensdauer-Abbruchschutz. Beim Schließen werden SAM-Segmentierung sowie laufende Generierungs- und Batch-Aufgaben abgebrochen und die Token-Quellen freigegeben. Zwei Tests schützen Weitergabe und wiederholbares Aufräumen.
- **Dichtheitsimport gegen KI-Blockade gehärtet (A2-03 erledigt):** Der asynchrone KI-Aufruf ist vom aufrufenden Synchronisationskontext getrennt, verwendet `ConfigureAwait(false)` und endet auch bei einem nicht kooperierenden KI-Aufruf nach 25 Sekunden. Ein Regressionstest bildet einen blockierten Oberflächen-Kontext nach.
- **Videoanalyse-Verbindungen wiederverwendet (A2-04 erledigt):** Die Datenseite hält je Zeitlimit einen langlebigen `HttpClient`, statt bei jeder Analyse eine neue Verbindung aufzubauen. Beim Schließen werden alle Clients wiederholbar freigegeben. Zwei Tests schützen Wiederverwendung und Aufräumen.
- **Videoanalyse-Abbruchquelle freigegeben (A2-05 erledigt):** Beim Schließen des Pipeline-Fensters wird die laufende Analyse zuerst abgebrochen und die Token-Quelle danach freigegeben. Ein Lebensdauer-Guard schützt die Reihenfolge.
- **Datenseiten-Suchtimer gestoppt (A2-06 erledigt):** Beim Verlassen der Datenseite werden Such- und Layout-Verzögerungstimer gemeinsam gestoppt. Ein Lebensdauer-Guard verhindert verspätete Suchläufe auf der entladenen Ansicht.
- **Backup-Geheimnisse redigiert (A3-01 erledigt):** `umgebung.txt` und `RESTORE-ANLEITUNG.txt` schreiben Werte von Variablen mit `TOKEN`, `SECRET`, `KEY` oder `AUTH` nur noch als `***redigiert***`. Normale Wiederherstellungspfade bleiben lesbar; ein Integrationstest prüft beide Dateien.
- **Backup-Inhalte mit SHA-256 prüfbar (A3-02 erledigt):** `manifest.json` enthält für jede aktuelle Sicherungsdatei Pfad, Länge und SHA‑256. Der Proberestore prüft diese Nachweise vor Datenbank und Projekt; fehlende, zusätzliche oder veränderte Dateien werden gemeldet. Tests bestätigen eine gleich große beschädigte Datei und blockieren Manifest-Pfade außerhalb der Sicherung.
- **Sidecar-Token geschützt (A3-03 erledigt):** Nur `PipelineSidecarToken` wird in `settings.json` per Windows-DPAPI an das aktuelle Benutzerkonto gebunden. Alte Klartextwerte werden weiter gelesen und beim nächsten Speichern automatisch geschützt; ein beschädigter oder nach einem PC-Wechsel nicht mehr lesbarer Token verwirft nicht die übrigen Einstellungen. Drei Tests schützen Verschlüsselung, Migration und den sicheren Fehlerfall.
- **Wissensdatenbank-Pfad validiert (A3-04 erledigt):** Relative oder ungültige Pfade aus `SEWERSTUDIO_KNOWLEDGE_ROOT` werden nicht mehr als Datenbankordner verwendet; stattdessen bleibt der gespeicherte absolute Pfad oder der sichere Standard aktiv. Abweisungen und jeder gültige Umgebungs-Override werden im Release-tauglichen Tageslog festgehalten.
- **Lokales HTML nicht mehr direkt geöffnet (A3-05 erledigt):** `SafeShellOpen` erlaubt keine `.html`- oder `.htm`-Dateien mehr. Die tatsächlich benötigten PDF-, Bild-, Video-, Tabellen-, Text- und Ordnerpfade bleiben unverändert; zwei Tests schützen beide gesperrten Endungen.
- **IBAK-Originaldatenbank geschützt (A4-01 erledigt):** Firebird öffnet die Kunden-`.fdb` nicht mehr direkt, sondern ausschließlich eine eindeutige, schreibbare Temp-Kopie. Die Kopie wird nach dem Lesen wieder entfernt; zwei Tests sichern Inhaltsisolation, Schreibschutz des Originals und Aufräumen.
- **NPK-Export blockiert die Oberfläche nicht mehr (A4-02 erledigt):** CSV- und Excel-Leistungsverzeichnis erzeugen und schreiben ihre Dateien im Hintergrund. Ein gemeinsamer Laufstatus verhindert Doppelstarts; CSV wird zusätzlich atomar geschrieben. Zwei Tests schützen den Hintergrundlauf und die unveränderten Schaltflächen-Befehle.
- **Excel-Speicherverbrauch begrenzt (A4-03 erledigt):** Der speichergebundene ClosedXML-Vorlagenexport lehnt mehr als 20.000 Haltungen oder Schächte ab, bevor die Arbeitsmappe geladen oder eine Zieldatei angelegt wird. Die Oberfläche nennt die Grenze und empfiehlt das Aufteilen; drei Tests schützen Grenzwert, Meldung und den frühen Abbruch.
- **PDF-Textextraktion begrenzt (A4-04 erledigt):** Vor `pdftotext` wird die Seitenzahl gegen das vorhandene Budget geprüft. Externe und interne Extraktion laden insgesamt höchstens 16 Millionen Zeichen in den Speicher; die externe Textdatei wird dabei stückweise statt vollständig gelesen. Zwei Verhaltenstests und der vorhandene Nutzungs-Guard schützen beide Grenzen.
- **Browser-PDF-Export zeitlich begrenzt (A4-05 erledigt):** Angebot, Druckcenter und NPK-PDF besitzen ein hartes Gesamtlimit von zwei Minuten. Browserstart, HTML-Laden, PDF-Erzeugung und Chromium-Installation beachten den Abbruch; Installationsprozesse werden bei Ablauf samt Unterprozessen beendet, Seite und Browser begrenzt aufgeräumt. Zwei Tests unterscheiden Zeitablauf und Benutzerabbruch.
- **Firebird-Serverzugang fail-closed (A4-06 erledigt):** Die bekannten IBAK-Standarddaten bleiben ausschließlich für lokale Embedded-Dateien erlaubt. Server-, Netzwerk- und IPv6-Pfade erfordern ausdrücklich `IBAK_FDB_USER` und `IBAK_FDB_PASSWORD`; ohne beide Werte wird vor dem Verbindungsaufbau abgebrochen. Sieben neue Testfälle schützen Zugangspflicht und Pfaderkennung.
- **Selbst gestartete KI-Prozesse aufgeräumt (A5-01 erledigt):** Nur Ollama- und Sidecar-Prozesse, die Sewer Studio selbst startet, werden registriert. Beim Beenden der App schließt ein Windows-Job-Objekt diese Prozesse samt Unterprozessen; eine Prüfung von Prozess-ID und Startzeit schützt den Rückfallpfad vor Verwechslungen. Bereits vorher laufende KI-Dienste bleiben unangetastet. Zwei Tests schützen Prozessende und App-Verdrahtung.
- **Sidecar-Sicherheitsgrenze klargestellt (A5-02 erledigt):** Code und Betriebsdokumentation benennen die Host-Prüfung ausdrücklich nur als Schutz gegen DNS-Rebinding. Ausschließlich das geheime `X-Sidecar-Token` kontrolliert den Zugriff; selbst eine Host-Freigabe mit `*` umgeht das Token nicht. Drei gezielte und alle 86 GPU-freien Sidecar-Tests sind grün.
- **Sidecar bleibt lokal gebunden (A5-03 erledigt):** `SEWER_SIDECAR_HOST` akzeptiert nur `localhost`, IPv4-Loopback (`127.x.x.x`) oder IPv6-Loopback (`::1`). Python-Konfiguration und PowerShell-Startskript weisen `0.0.0.0`, LAN-Adressen und Hostnamen vor dem Dienststart ab. Acht neue und insgesamt 94 GPU-freie Sidecar-Tests sind grün.
- **CUDA-Ausfälle verständlich klassifiziert (A5-04 erledigt):** CUDA-, Treiber-, cuDNN-, cuBLAS- und Kernel-Ausfälle werden ohne interne Treiberdetails als vorübergehender 503-Fehler mit stabilem Code `cuda_unavailable` gemeldet. Der vorhandene C#-Client behandelt 503 nach genau einem Wiederholungsversuch als nicht verfügbaren KI-Dienst. Alle 95 GPU-freien Sidecar-Tests sind grün.
- **Leerer Sidecar-Token sperrt sicher (A5-05 erledigt):** Ein nicht initialisierter oder leerer Server-Token deaktiviert die Anmeldung nicht mehr. Jede Anfrage wird mit einem bereinigten 503 und `auth_unavailable` abgewiesen; normale Routentests verwenden nun ebenfalls einen ausdrücklichen Test-Token. Alle 95 GPU-freien Sidecar-Tests sind grün.
- **QGIS-Lesebrücke bewusst begrenzt (A5-06 erledigt):** Für den Einzelplatz bleibt die QGIS-Brücke kompatibel ohne Token, ist aber ausschließlich an IPv4-Loopback gebunden und akzeptiert nur `GET`/`HEAD`. Die Betriebsdokumentation nennt das lokale Leserisko und verlangt auf Mehrbenutzer-/Terminalservern die Deaktivierung. Ein Architekturtest schützt Code und Sicherheitsgrenze.
- **Druckcenter entkoppelt (A1-05, Teilpaket):** Das ViewModel erhält Einstellungen, Dialoge, PDF-Ausgabe und Kostenabgleich gezielt. Es speichert den zentralen Container nicht mehr. Zwölf fokussierte Tests schützen Hintergrund-Aktualisierung, Filter, Auswahl und Leistungsverzeichnis-Aufbau.
- **Push-Schutz repariert:** Der pre-push-Hook prüft jetzt Infrastruktur-, Pipeline- und UI-Tests. Ein roter UI- oder Wartbarkeitstest blockiert damit den Push.
- **Gesamtprüfung grün:** 8.734 Tests bestanden (2.512 Infrastruktur, 1.813 Pipeline, 4.347 UI und 62 ProjectModernizer). Zwei maschinengebundene Tests wurden planmäßig übersprungen. Der Release-Build endet mit 0 Fehlern und 0 Warnungen.

Die nachfolgenden Fundstellen beschreiben weiterhin den Zustand **vor** dieser Umsetzung und bleiben als nachvollziehbares Audit erhalten. Noch offene mittel- und langfristige Punkte stehen in der Roadmap dieses Berichts.

> Stand: 2026-07-13 · Methode: 7 spezialisierte KI-Agenten (parallel, rein lesende Code-Analyse) auf Basis des committeten Standes. Kritische Funde wurden zur Gegenprüfung vorgesehen — **es trat kein Fund der Stufe „Kritisch" auf**, daher waren keine Gegenprüfungen nötig. Kein Framework-/Plattformwechsel vorgeschlagen.
> Belegte Zusatzprüfungen des Orchestrators sind mit ✔ markiert.
>
> **Nachtrag 2026-07-13:** `A6-02` ist umgesetzt. `tools/SidecarE2eSmoke`
> dekodierte drei Bilder aus einem echten Projektvideo und prüfte YOLO, DINO, SAM,
> Quantifizierung sowie die produktive Mehrmodell-Kette gegen einen Golden-Vertrag.
> Ergebnis auf der Zielmaschine: **PASS** (3/3 Bilder, 0 Modellfehler).

---

## 1. Gesamtbewertung Wartbarkeit & Stabilität

**Schulnote: 2,5 (gut–befriedigend).**

Das Programm ist im Normalbetrieb **stabil**: Es gibt keinen Fund mit Datenverlust-, Absturz- oder offenem Sicherheitsrisiko. Speichern ist durchgehend atomar, das Backup ist überdurchschnittlich sorgfältig (SHA256-Verify, SQLite-Online-Snapshot mit `integrity_check`, Versionierung, Ziel-Guards), SQL ist parametrisiert (keine Injection gefunden), die Token-Absicherung des KI-Dienstes ist sauber, und die Schichtarchitektur ist zyklenfrei mit aktiven Fitness-Tests.

Zwei Dinge ziehen die Note nach unten:

- **Blindflug im Fehlerfall (Release-Diagnostik):** Ein großer Teil der Warn-/Fehlerpfade schreibt nur nach `Debug.WriteLine` — das wird im Release-Build vom Compiler **entfernt**. Wenn produktiv etwas still schiefgeht, steht im Tageslog nichts. Für einen Solo-Entwickler, der zugleich Support ist, ist das das praktisch gefährlichste Einzelthema.
- **Belastbarkeit ist unbewiesen:** Die ~4.300 Tests decken die Rechenlogik hervorragend ab, aber es gibt **keinen** echten Dauer-/Belastungstest, **keinen** End-to-End-Test gegen einen laufenden KI-Dienst mit echtem Video und **keine** WPF-UI-Automation. Der auftragsrelevante Schwerpunkt „Belastung" ist damit isoliert nur mit Note 4 bewertbar.

**Bereichsnoten der 7 Agenten:**

| Bereich | Note | Kurzurteil |
|---|---|---|
| Architektur / große Klassen / MVVM | 3 | solide, aber versteckte God-Classes + MVVM-Lecks |
| Nebenläufigkeit / Ressourcen | 2 | überdurchschnittlich, wenige klare Lücken |
| Datei- & Projektsicherheit | 2 | defensiv gebaut, nur Härtungspunkte offen |
| Schnittstellen (PDF/DB/QGIS/Excel) | 2 | robust, Restrisiken bei Fremd-DB & Extremgrößen |
| KI-Dienst / GPU / Netzwerk | 2,5 | Kern-Sicherheit stark, Resilienz-Lücken |
| Testabdeckung / Belastung | 4 | Logik top, Dauer/E2E/UI praktisch abwesend |
| Wartbarkeit (Solo) | 2,5 | gutes Fundament, Diagnostik-Blindfleck |

---

## 2. Top-3-kritische Funde

Kein Fund erreichte „Kritisch"; die folgenden drei sind die schwerwiegendsten „Hoch"-Funde mit dem größten realen Effekt.

**T1 — Diagnostik verschwindet im Produktions-Build** (Agent 7, `A8-01`)
`BestEffort.Report` und 81 `Debug.WriteLine`-Stellen (u.a. „Trainings-JSON korrupt" in `TrainingSamplesStore.cs:225`) schreiben über `System.Diagnostics.Debug.WriteLine` — durch `[Conditional("DEBUG")]` im Release entfernt. Der gute `FileLogger` existiert, wird von diesen Pfaden aber nicht genutzt. **Folge:** Stille Fehler hinterlassen im Tageslog keine Spur.
→ *Zentrale Sink von `Debug` auf `Trace`/`ILogger` umstellen (Quick Win < 1h), dann in den Hot-Paths den Logger durchreichen.*

**T2 — Belastbarkeit ist nicht getestet** (Agent 6, `A6-01`/`A6-02`/`A6-03`)
Die 1.072 „UI-Tests" sind zu großen Teilen Quelltext-Guards (137 Dateien lesen `.cs` als String); nur ~25 instanziieren wirklich ein ViewModel. Zum Scan-Zeitpunkt gab es keinen E2E-Test gegen echten Sidecar/Video und keine Dauer-/Leak-Test-Infrastruktur. **Nachtrag:** Der echte Sidecar-/Video-Vertragstest (`A6-02`) ist inzwischen vorhanden und grün. WPF-UI-Automation und der 8-Stunden-Nachtlauf bleiben offen. → *Nachtlauf-Konzept in Punkt 6.*

**T3 — Grossdatei-Guard ist rot, aber das Push-Gate prüft ihn nicht** (Agent 1, `A1-01`/`A1-02` + ✔ Orchestrator-Prüfung)
✔ Verifiziert: `SchaechtePage.xaml.cs` hat **1001 Zeilen** — genau über der Guard-Grenze (>1000, leere Whitelist). Der `MaintainabilityFitnessTests` ist damit **aktuell rot**. ✔ Der pre-push-Hook führt nur `Infrastructure.Tests` + `Pipeline.Tests` aus — die **UI-Tests laufen nicht im Gate**, deshalb blockiert der rote Guard keinen Push und die „alles grün"-Meldung deckte ihn nie ab. Zusätzlich versteckt der reine Datei-Zähler echte God-Classes: ✔ `PlayerWindow` = **119 Dateien / 9.479 Zeilen** über partielle Klassen verteilt.
→ *SchaechtePage-Fachlogik auslagern (bringt Datei unter die Grenze), UI-Tests ins Gate aufnehmen, Guard auf „Zeilen je Typ" erweitern.*

---

## 3. Detaillierte Einzelergebnisse pro Schwerpunkt

### Schwerpunkt 1 — Zu große Klassen & Dateien

| ID | Schweregrad | Fundstelle | Empfehlung |
|---|---|---|---|
| A1-01 | Hoch | `PlayerWindow*.cs` — 97 partielle Teildateien (✔ 119 Dateien/9.479 Z. gesamt) | Zusammengehörige Partials (Coding, LiveDetection, Playback) in echte Controller/Services mit Interface extrahieren |
| A1-02 | Hoch → **erledigt** | `SchaechtePage.xaml.cs` = ✔ 1001 Zeilen (Guard >1000, Whitelist leer → Test rot) | Fachlogik ist ausgelagert; die Datei liegt mit 930 Zeilen wieder unter der Grenze |
| A1-09 | Mittel | `MaintainabilityFitnessTests.cs:16-27` misst Zeilen pro Datei, nicht pro Typ | Regel „Zeilen je (partial) Typ aggregieren, Warnung ab ~1500-2000" ergänzen |
| A1-08 | Mittel | `ArchitectureFitnessTests.cs` (1747 Z., ~70 Token-Tests); `UiArchitectureGuardTests.cs` leer | Struktur- statt String-Token-Prüfungen; leeren Test entfernen/füllen |

### Schwerpunkt 2 — Versteckte UI-/Fachlogik-Abhängigkeiten

| ID | Schweregrad | Fundstelle | Empfehlung |
|---|---|---|---|
| A1-03 | Hoch → **erledigt** | `SchaechtePage.xaml.cs:929` `new ProjectCostStoreRepository(...)` + Persistenzlogik im Code-behind | In `SchachtMassnahmenDialogController` ausgelagert und testgeschützt |
| A1-04 | Hoch → **erledigt** | `ProtocolEntryEditorDialog.xaml.cs:540/609` KI-Vorschlag + VSA-Validierung im Dialog | KI-Ablauf und VSA-Gesamtprüfung sind in zwei kleine ViewModels ausgelagert und mit 10 fokussierten Tests geschützt; der Dialog sank auf 789 Zeilen |
| A1-05 | Hoch → **erledigt** | `ServiceProvider.cs:50` God-Container mit IO im Ktor; als Ganzes in ~15 ViewModels injiziert | Alle Fach-ViewModels speichern nur noch ihre gezielten Abhängigkeiten; auch die Datenseite samt Teildateien ist containerfrei. Die Shell nutzt den Container weiterhin bewusst als zentralen Aufbaupunkt für Seiten und Navigation |
| A1-07 | Mittel → **erledigt** | `TrainingCenterWindow.xaml.cs:83-84`, `MediaSearchWindow.xaml.cs:114` — Dienste per `new` | Medien-Suche, Training-Store, Import, KB-Diagnose, Review-Warteschlange, SAM und Few-Shot werden zentral verdrahtet; bedarfsgesteuertes Erzeugen ist testgeschützt |
| A1-06 | Mittel → **teilweise erledigt** | `Application/Reports/ProtocolPdfExporter.cs` — QuestPDF-Rendering in Application-Schicht | `IProtocolPdfExporter` ist eingeführt und an den aktiven UI-Pfaden verdrahtet; öffentliche Fassaden bleiben kompatibel. Offen ist der getrennte Umzug der konkreten QuestPDF-Implementierung nach Infrastructure |
| A1-10 | Niedrig | >50 `static class` mit Fachlogik; IO-behaftete ohne Interface (z.B. `ProtocolRegenerationService`) | IO-/seiteneffektbehaftete statisch → Instanz-Service mit Interface; reine Rechner statisch lassen |

### Schwerpunkt 3 — Nebenläufigkeit, Abbruch, Speicher

| ID | Schweregrad | Fundstelle | Empfehlung |
|---|---|---|---|
| A2-01 | Mittel → **erledigt** | `VideoFrameExtractor.cs:41-63` — ffmpeg-Prozess wird bei Abbruch nicht getötet | Einzelbild-Extraktion läuft über `ProcessOutputReader`; dieser beendet bei Abbruch/Fehler den gesamten Prozessbaum. Ein echter Abbruchtest schützt das Verhalten |
| A2-02 | Mittel → **erledigt** | `TrainingCenterWindow.xaml.cs` — keine CTS, SAM-Segmentierung ohne Token | Fenster-Lebensdauer-Token wird an SAM durchgereicht; Closing bricht Fenster- und ViewModel-Arbeit ab und gibt die Token-Quellen wiederholbar frei |
| A2-03 | Mittel → **erledigt** | `DichtheitImportDistributor.cs:112-134` — asynchroner KI-Aufruf konnte im Synchronisationskontext blockieren | KI-Aufruf läuft isoliert über `Task.Run`, wartet intern mit `ConfigureAwait(false)` und besitzt einen unabhängig wirksamen 25-Sekunden-Abbruch; Regressionstest mit blockiertem Oberflächen-Kontext ist grün |
| A2-04 | Mittel → **erledigt** | `DataPageVideoAnalysisController.cs:82` u.a. — `new HttpClient` pro Aufruf | Controller verwendet je konfiguriertem Zeitlimit einen langlebigen Client; `DataPageViewModel.Dispose()` gibt den Cache sicher und wiederholbar frei |
| A2-05 | Niedrig → **erledigt** | `VideoAnalysisPipelineWindow.xaml.cs:61-66` — CTS nie disposed | Fenster-Closing ruft erst `Cancel()` und anschließend `Dispose()` auf; Architekturtest schützt Reihenfolge |
| A2-06 | Niedrig → **erledigt** | `DataPage.xaml.cs` — Such-Debounce-Timer bei Unloaded nicht gestoppt | Unloaded stoppt Such- und Layout-Timer; Architekturtest schützt beide Aufräumpfade |

*Positiv bestätigt: SafeFireAndForget/BoundedBackgroundTaskRunner breit genutzt; PlayerWindow-LibVLC-Cleanup sauber; ShellViewModel meldet statische Events in Dispose ab; kein SkiaSharp-Bitmap-Leak (kommt im Code nicht vor).*

### Schwerpunkt 4 — Datei- & Projektsicherheit

| ID | Schweregrad | Fundstelle | Empfehlung |
|---|---|---|---|
| A3-01 | Mittel → **erledigt** | `FullBackupSourcesFactory.cs:53-70` — alle `SEWER*`-Env-Vars inkl. Tokens im Klartext in `umgebung.txt`/`RESTORE-ANLEITUNG.txt` | Gemeinsamer Redaktor schwärzt Werte von `*TOKEN*/*SECRET*/*KEY*/*AUTH*` in beiden Ausgabedateien; normale Werte bleiben wiederherstellbar |
| A3-02 | Mittel → **erledigt** | `FullBackupService.cs:434-493` — Manifest ohne Pro-Datei-Hashes; `DirectoryMirror` erkennt Änderung nur über Größe+Zeit | Manifest enthält Pfad, Länge und SHA-256 jeder aktuellen Datei; `FullBackupSmoke --verify-restore` prüft fehlende, zusätzliche und inhaltlich abweichende Dateien vor dem fachlichen Restore-Test |
| A3-03 | Niedrig → **erledigt** | `JsonProjectRepository.cs:94-104` / `AppSettings.cs:192` — Klartext-JSON inkl. `PipelineSidecarToken` | Token ist per DPAPI (`ProtectedData`, CurrentUser) geschützt; `projekt.json` bleibt wie vorgesehen Klartext |
| A3-04 | Niedrig → **erledigt** | `KnowledgeBasePaths.cs:131-147` — `SEWERSTUDIO_KNOWLEDGE_ROOT` ungeprüft als Wurzel | Leere, relative und ungültige Werte werden verworfen; gültige Overrides und Abweisungen landen im Tageslog |
| A3-05 | Niedrig → **erledigt** | `SafeShellOpen.cs:8-13` — `.html/.htm` in Whitelist, kein Pfad-Containment | `.html/.htm` sind aus der Freigabeliste entfernt; vorhandene Aufrufer benötigen diese Typen nicht |

### Schwerpunkt 5 — PDF-, Datenbank- & QGIS-Schnittstellen

| ID | Schweregrad | Fundstelle | Empfehlung |
|---|---|---|---|
| A4-01 | Mittel → **erledigt** | `IbakFdbConnectionOptions.cs:25-42` — IBAK-`.fdb` wird read-**write** (Embedded) geöffnet | Alle aktiven Firebird-Lesepfade arbeiten auf einer eindeutigen Temp-Kopie; das Original bleibt unangetastet |
| A4-02 | Niedrig → **erledigt** | `BuilderPageViewModel.Output.cs:269/224` — NPK-Export blockiert UI-Thread | CSV- und Excel-LV laufen als `async Task` über einen kleinen Hintergrund-Runner; Befehlsnamen bleiben kompatibel |
| A4-03 | Niedrig → **erledigt** | `ExcelTemplateExportService.cs` — ClosedXML hält gesamtes Workbook im RAM | Harte Obergrenze von 20.000 Datensätzen vor dem Laden der Arbeitsmappe; verständliche Fehlermeldung mit Aufteilungshinweis |
| A4-04 | Niedrig → **erledigt** | `PdfTextExtractor.cs:70/99` — Seiten-Budget nur im PdfPig-Fallback, nicht bei pdftotext | Seitenzahl wird vor `pdftotext` geprüft; beide Pfade begrenzen extrahierten Text auf 16 Millionen Zeichen |
| A4-05 | Niedrig → **erledigt** | `OfferHtmlToPdfRenderer.cs:84-145` — Playwright ohne hartes Gesamt-Timeout | Verknüpftes Zwei-Minuten-Limit durch Browser-, PDF- und Installationspfad; begrenztes Ressourcen-Aufräumen |
| A4-06 | Niedrig → **erledigt** | `IbakFdbConnectionOptions.cs:9-10` — SYSDBA/masterkey im Code (Env-übersteuerbar) | Defaults bleiben nur für lokale Embedded-Dateien; Serverpfade verlangen Benutzer und Passwort aus der Umgebung |

*Positiv bestätigt: SQL parametrisiert (keine Injection); WinCan `.db3` ReadOnly; QGIS-Bridge Loopback-only, GET-only, Exception-gekapselt, in `App.OnExit` disposed; große Haupt-Exports laufen korrekt via `Task.Run`.*

### Schwerpunkt 6 — KI-Dienst, GPU-Ausfälle & lokale Netzwerksicherheit

| ID | Schweregrad | Fundstelle | Empfehlung |
|---|---|---|---|
| A5-01 | Mittel → **erledigt** | `DefaultAiStartupLauncher.cs:68` / `App.xaml.cs:231` — Sidecar/Ollama bei App-Ende nicht beendet (~21 GB VRAM bleiben) | Von Sewer Studio gestartete KI-Prozesse werden per Windows-Job-Objekt verfolgt und beim App-Ende samt Unterprozessen beendet; bereits laufende Fremdprozesse bleiben unangetastet |
| A5-02 | Mittel → **erledigt** | `sidecar/main.py:92-144` — Trusted-Host-Check per Host-Header trivial umgehbar | Code, README und Regressionstest stellen klar: Host-Prüfung schützt gegen DNS-Rebinding; allein das verpflichtende Token ist die Zugriffssperre |
| A5-03 | Mittel → **erledigt** | `sidecar/config.py:9` + `start_sidecar.ps1:66` — `SEWER_SIDECAR_HOST` ungeprüft an uvicorn | Python-Konfiguration und Startskript erlauben ausschließlich Loopback; Nicht-Loopback bricht vor dem Dienststart mit verständlicher Meldung ab |
| A5-04 | Mittel → **erledigt** | `sidecar/main.py:77-89` + `VisionPipelineClient.cs:186-197` — CUDA-Fehler (kein OOM) → generischer 500, Client wrappt nicht | Typische CUDA-/Treiberfehler liefern einen bereinigten 503 mit stabilem Fehlercode; der bestehende 503-Pfad des Clients wiederholt einmal und meldet danach `SidecarUnavailableException` |
| A5-05 | Niedrig → **erledigt** | `sidecar/main.py:146-154` — Token-Enforcement fail-open bei leerem Token | Ohne initialisierten Server-Token werden alle Anfragen mit 503 und stabilem Fehlercode abgewiesen; ein leerer Token schaltet die Anmeldung nie aus |
| A5-06 | Niedrig → **erledigt** | `QgisBridgeServer.cs:68` — Bridge (8765) ohne Token, read-only Loopback | Einzelplatz-Annahme, reines Lesen und lokales Restrisiko sind dokumentiert und testgeschützt; auf Mehrbenutzer-Systemen ist die Bridge zu deaktivieren |
| A5-07 | Niedrig | `AiStartupOrchestrator.cs:76-82` — Ollama ohne Auth, keine Loopback-Erzwingung | Ollama mit `OLLAMA_HOST=127.0.0.1` starten, bei Nicht-Loopback warnen |

*Positiv bestätigt: `secrets.token_urlsafe(32)`, `hmac.compare_digest`, Token nur an Loopback gesendet, Decompression-Bomb-Schutz, OOM → LRU-Eviction + 503 + 1 Retry.*

### Schwerpunkt 7 — Fehlende Belastungs- & Bedienungstests

| ID | Schweregrad | Fundstelle | Empfehlung |
|---|---|---|---|
| A6-01 | Hoch | `ArchitectureFitnessTests.cs` u.a. — 137 UI-Testdateien lesen `.cs` als Text; keine UI-Automation | ~46 ViewModels per `new + Command` testen (Quick Win); StaFact-Fenster-Smoke mittelfristig |
| A6-02 | Hoch → **erledigt** | `tools/SidecarE2eSmoke` + `SidecarRealVideoIntegrationTests` | Echter Sidecar, reales Video, YOLO/DINO/SAM, Quantifizierung und Golden-Vertrag sind umgesetzt und auf der Zielmaschine grün |
| A6-03 | Hoch | Repo-weit kein `[Trait]`, keine Dauer-/Leak-Testlogik | Trait-Kategorien (Unit/Integration/Endurance) + `NightlySoakRunner` |
| A6-04 | Mittel | `integrations/qgis/sewerstudio_bridge/` — keine Tests | Python-Smoke für Bridge-Endpunkte (TestClient) + C#-Vertragstest |
| A6-05 | Mittel | `KnowledgeBaseContext.cs:176-186` — KB-Schema-Migration (ADD COLUMN) im Bestand ungetestet | Test mit Alt-DB (ohne neue Spalten) → Upgrade + Datenerhalt prüfen |
| A6-06 | Mittel | Nur `DbfTableTests` prüft kaputte Dateien; PDF/XTF/WinCan/FDB nicht | Je Importer 1 Negativtest (truncated/leer/gesperrt) → Skip statt Crash |
| A6-07 | Mittel | `sidecar/tests/test_sam.py:24` — SAM/DINO nur mit `pytest -m gpu` | CPU-Smoke (Loader + Antwortschema) oder `-m gpu` verpflichtend vor Batch |
| A6-08 | Niedrig | `KnowledgeBaseContext.cs:52-56` — WAL/busy_timeout-Schutz ungetestet | Parallel-Reindex-vs-Retrieve-Test (kein „database is locked") |

### Schwerpunkt 8 — Wartbarkeit für Solo-Entwickler

| ID | Schweregrad | Fundstelle | Empfehlung |
|---|---|---|---|
| A8-01 | Hoch | `BestEffort.cs:54` + 81× `Debug.WriteLine` — im Release entfernt | Sink auf `Trace`/`ILogger` umstellen; Logger in Hot-Paths durchreichen |
| A8-02 | Mittel | `FindAncestor<T>` in ~7 Dateien dupliziert; 2× rohes `VisualTreeHelper.GetParent` | Auf zentrales `VisualTreeSafe.FindAncestor` konsolidieren |
| A8-03 | Mittel | `AuswertungPro.sln` zieht 12 CLI-Tools in jeden Build; kein `.slnf` | Solution-Filter für den Alltag (4 src + 4 test) |
| A8-04 | Mittel | `AGENTS.md` veraltet (beschreibt reines PDF-Tool, kein KI-Wort) | Auf Ist-Stand heben oder als Weiterleitung auf CLAUDE.md |
| A8-05 | Niedrig | `DiagnosticsPageViewModel.cs:22-40` — nur heutiger Log-Tail, kein Export | „Log-Ordner öffnen" + „Diagnosepaket (ZIP)" ergänzen |
| A8-06 | Niedrig | `Directory.Build.props` — keine Roslyn-Analyzer/Warnungs-Gate | `EnableNETAnalyzers=true`, `AnalysisLevel=latest-recommended` |
| A8-07 | Niedrig | `DataPagePrintController.cs:234` u.a. — rohe `ex.Message` in Nutzer-Dialogen | Zentraler `UserError.Describe(ex)` (kurz für Nutzer, voll ins Log) |

---

## 4. Quick Wins (< 2 Stunden, sofort umsetzbar)

1. **`A8-01` (Teil 1):** `BestEffort.Report`-Standard-Sink von `Debug.WriteLine` auf `Trace.WriteLine`/`ILogger` umstellen — eine Datei, sofort sind Release-Warnungen wieder sichtbar. *Größter Sicherheitsgewinn pro Minute.*
2. **`A3-01`:** Token-Werte in `umgebung.txt`/`RESTORE-ANLEITUNG.txt` redigieren (Namensfilter).
3. **`A2-01`:** ffmpeg bei Abbruch killen (`p.Kill(entireProcessTree:true)`).
4. **`A2-05`/`A2-06`:** CTS disposen + Such-Timer im Unloaded stoppen.
5. **`A8-03`:** `.slnf`-Solution-Filter (nur 4 src + 4 test) → schnellere Builds im Alltag.
6. **`A8-06`:** Roslyn-Analyzer in `Directory.Build.props` aktivieren (ohne TreatWarningsAsErrors).
7. **`A8-04`:** `AGENTS.md` auf CLAUDE.md umleiten (verhindert widersprüchliche Einstiegs-Doku).
8. **`A4-02`:** NPK-Export in `Task.Run` auslagern (kein UI-Einfrieren).
9. **`A5-02`/`A5-03`:** Host-Check-Kommentar klarstellen + Warnung bei Nicht-Loopback-Host.
10. **`A6-03` (Teil 1):** `[Trait]`-Kategorien einführen, damit Endurance-Tests trennbar werden.

---

## 5. Strategische Empfehlungen

1. **Diagnose-Sichtbarkeit herstellen (`A8-01`, `A8-05`, `A8-07`):** Alle fachlich relevanten Warn-/Fehlerpfade auf `ILogger` heben, ein „Diagnosepaket exportieren" bauen, rohe `ex.Message` durch gemappte Nutzertexte ersetzen. Ohne verlässliche Logs bleibt jede Fehlersuche im Feld Rätselraten.
2. **Grossdatei-Guard reparieren und ins Gate nehmen (`A1-09`, `A1-02`, Push-Hook):** Guard auf „Zeilen je Typ" erweitern (schließt das Partial-Schlupfloch), `SchaechtePage` unter die Grenze bringen, und die UI-Tests in den pre-push-Hook aufnehmen — sonst laufen Architektur-Guards nie automatisch.
3. **Echte God-Classes zerlegen statt weiter fragmentieren (`A1-01`, `A1-03`, `A1-04`):** PlayerWindow-Partials und die Fachlogik in SchaechtePage/ProtocolEntryEditorDialog schrittweise in Services/Controller mit Interface überführen — testgeschützt, kein Big-Bang.
4. **DI-Hygiene (`A1-05`, `A1-07`, `A1-10`):** ViewModels nur die benötigten Interfaces geben statt des ganzen `ServiceProvider`; per-`new`-Streuung in Fenstern beenden. Das macht Kernabläufe unit-testbar.
5. **KI-Prozess-Lebenszyklus (`A5-01`, `A5-04`):** ✔ Selbst gestartete Sidecar-/Ollama-Prozesse werden beim App-Ende kontrolliert gestoppt; CUDA-Ausfälle erscheinen als klarer, vorübergehender KI-Fehler.
6. **Fremd-DB schützen (`A4-01`):** IBAK-`.fdb` nur über eine Temp-Kopie/read-only anfassen — schützt Kunden-Originaldaten.

---

## 6. Vorschlag für erweiterte Teststrategie

**Ziel:** Die risikoreichsten Ketten (echtes Video, echter Sidecar, QGIS, WPF-Runtime, DB-Migration im Bestand) automatisiert absichern — sie laufen heute in **keinem** Test.

**Stufe A — Fundament (Quick Wins, Tage):**
- `[Trait]`-Kategorien (Unit/Integration/Endurance); Standardlauf + Push-Gate = nur Unit; UI-Tests ins Gate aufnehmen.
- Die ~46 ViewModels per `new + Command` testen (kein UI-Thread nötig) — ersetzt echte Deckung, die die String-Guards nur vortäuschen.

**Stufe B — Integration (auf der Zielmaschine, nicht CI):**
- ✔ **Headless Pipeline-Treiber** (`tools/SidecarE2eSmoke`): startet bei Bedarf einen echten Sidecar, dekodiert drei Videobilder, fährt YOLO/DINO/SAM→Quantifizierung durch und prüft den Vertrag gegen `golden/pipeline-contract.v1.json`. Als `[Trait("Category","Integration")]` maschinengebunden ausführbar.
- **Negativtests je Importer** (truncated/leer/gesperrt) → Skip statt Crash (`A6-06`).
- **KB-Migrationstest** mit Alt-DB → Upgrade + Datenerhalt (`A6-05`).
- **QGIS-Bridge-Smoke** (Python-TestClient + C#-Vertragstest) (`A6-04`).
- **SAM/DINO CPU-Smoke** oder `pytest -m gpu` verpflichtend vor jedem Batch (`A6-07`).

**Stufe C — Nachtlauf / Belastung (`tools/NightlySoakRunner`, 8 h, maschinengebunden):**
- Schleife über N Test-Videos für 8 h: pro Runde Video-Import → KI-Auswertung → QualityGate → PDF-Export → KB-Index → QGIS-Sync.
- Nach jeder Runde: `GC.Collect`, Assertion auf **Prozess-RAM-/Handle-Obergrenze** und **Sidecar-Latenz-Perzentil**; **VRAM** via `nvidia-smi` mitschreiben; Abbruch bei Regression/Leak. Ausgabe als CSV.
- **UI-Runtime:** schmaler StaFact-Smoke pro Hauptfenster (öffnen/schließen ohne Exception), damit ein Oberflächen-Absturz im Dauerbetrieb auffällt.

**Wichtig:** LibVLC, GPU, QGIS und Ollama laufen nur auf der Zielmaschine — Stufe B/C sind bewusst **maschinengebundene Jobs**, kein CI. Das Push-Gate bleibt schnell (Unit).

---

## 7. Roadmap für die nächsten zwei Releases (nur Wartbarkeit, keine Technologiewechsel)

### Release N+1 — „Sichtbarkeit & Sicherheitsnetz" (~1 Woche)
- **Diagnose-Sichtbarkeit:** `A8-01` (Sink umstellen + Hot-Paths), `A8-05` (Diagnosepaket), `A8-07` (Fehler-Mapper) — schrittweise beginnen.
- **Test-Gate reparieren:** UI-Tests in den pre-push-Hook; `A1-09` (Guard je Typ); `A1-02`/`A1-03` (SchaechtePage-Fachlogik auslagern → Guard wieder grün).
- **Quick Wins:** `A3-01`, `A2-01`, `A2-05`, `A2-06`, `A8-03`, `A8-06`, `A4-02`, `A5-02`/`A5-03`.
- **Ergebnis:** Fehler werden im Feld sichtbar, Architektur-Guards laufen automatisch, Build schneller.

### Release N+2 — „Belastbarkeit & Entkopplung" (~2 Wochen)
- **Teststrategie Stufe B + C:** ✔ Headless Pipeline-Treiber (`A6-02`) erledigt; offen bleiben NightlySoakRunner (`A6-03`), Importer-Negativtests (`A6-06`), KB-Migrationstest (`A6-05`) und QGIS-Smoke (`A6-04`).
- **God-Class-Abbau (testgeschützt):** PlayerWindow-Partials in Services überführen (`A1-01`); der ProtocolEntryEditorDialog ist bei KI und VSA-Validierung erledigt (`A1-04`).
- **DI-Hygiene:** ViewModels auf Interface-Konstruktoren umstellen (`A1-05`, `A1-07`), beginnend bei den am häufigsten geänderten.
- **KI-Resilienz:** ✔ Prozess-Lebenszyklus (`A5-01`), CUDA-Fehler-Klassifizierung (`A5-04`) und Schutz der IBAK-Originaldatei (`A4-01`) sind erledigt.
- **Ergebnis:** Der 8-Stunden-Nachtlauf ist fahrbar, die größten Wartungsbremsen sind entschärft, das KI-Subsystem verhält sich bei Ausfällen berechenbar.

---

### Offene Prüfungen (Grenzen dieses Scans)
- Rein statische Analyse, kein Build/Testlauf/Profiler/echter Fuzz-/Pentest (auftragsgemäß). Der rote Grossdatei-Guard (`A1-02`) und die Push-Hook-Lücke wurden vom Orchestrator per `wc -l` / Hook-Inspektion ✔ verifiziert, der tatsächliche UI-Testlauf nicht.
- Nicht vertieft: die ~60 CLI-Tools, Sidecar-Interna der einzelnen Inferenz-Routen, QuestPDF-Speicherverhalten bei sehr vielen Fotos, echte Fremd-DB-Beispieldateien, Laufzeit-Leak-Messung.
