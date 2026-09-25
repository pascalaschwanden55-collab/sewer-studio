# Arbeitsprotokoll

## 2026-09-25: Vorbereitung

- Sicherung vom 24./25.09. beendet: 276590 Dateien kopiert und geprüft, 653,1 GB. 128 verknüpfte Quellen fehlten: 19 PNG, 16 JPG, 78 PDF und 15 MP4. Keine Originale geöffnet oder verändert.
- Ursprünglicher Projektordner: `C:\Sewer-Studio_KI_5.0`, Branch `feature/webgis-uebertragung`, letzter Commit `630a11c61`.
- Offener Zustand: 84 geänderte und 122 neue Dateien, zusammen 17.848.087 Byte. Bytegenaue Kopie samt Diff und Prüfsummen in `C:\Users\Besitzer\Documents\ChatGPT\SewerStudio_10Tage_20260925_Snapshot`. Zustand blieb während der Kopie unverändert.
- Tag `baseline-10tage-20260925` gesetzt; saubere Arbeitskopie auf Branch `codex/ferien-20260925` angelegt. Kein Push.
- .NET 10.0.112. Schneller Release-Build: 0 Fehler, 5 vorhandene Warnungen. Vollständiger Folge-Build: 0 Fehler, 0 neue Warnungen.
- Infrastrukturtests: 2697 bestanden, 6 übersprungen; der Test `SanierungsprotokollEchteQuelleTests.EingescannteBegleitprotokolle_LandenBeiIhrenHaltungen` hing und wurde nach 2 Minuten ohne Fortschritt abgebrochen. Pipeline-Tests: 2758 bestanden, 3 übersprungen. UI-Gesamtlauf aus Datenschutzgründen abgebrochen, nachdem ein Infrastrukturtest eine echte Kundenquelle auf D: nutzte. ProjectModernizer: 62 bestanden. Weitere Gesamtläufe erst nach Prüfung der Datenpfade. Zweifache Bildmessung abgeschlossen. Unbeaufsichtigte Verbesserungen sind nicht freigegeben.
- Cline-Erweiterung 4.1.20 und Ollama mit lokalem `qwen38-128k:latest` sind vorhanden. Das Modell hat einen Werkzeugaufruf mit den richtigen Argumenten in 10,6 s erzeugt. Cline in VS Code ist auf Ollama unter `http://localhost:11434` mit leerem API-Schlüssel eingestellt. Das Kontextfenster wurde auf 16384 begrenzt; die automatische Freigabe erlaubt nur Dateilesen. Ein harmloser Probelauf im Plan-Modus antwortete mit `OK`, ohne Werkzeuge auszuführen. Danach wurde Qwen aus dem Grafikspeicher entladen.

## Entscheidungen nach den Ferien

- Die 206 offenen Originaldateien werden nach den Ferien einzeln mit den neuen Änderungen abgeglichen. Der Nutzer hat die Wahl des Arbeitsstands delegiert; für die laufende Arbeit wurde der saubere Start-Commit gewählt. Eine pauschale Übernahme wäre bei unbekannter Absicht der offenen Dateien riskant.


- Freier Speicher vor den Messungen: C: 614 GB, E: 1191 GB. Codex-Wochenkontingent: 18 % genutzt, keine Zusatzguthaben. Claude Code ist über ein Max-Abo angemeldet. Die VS-Code-Anzeige meldete 37 % Wochenverbrauch und 0 % im Fünfstundenfenster. Keine Zahlungen oder Guthaben gekauft.


## Bild-Ausgangsmessung (nur Qwen, keine volle KI-Kette)

- EVAL_SET: 120 Bilder; beide Läufe 29/120 exakte Codes (24,2 %), 0 Nullantworten, 0 abweichende Vorhersagen zwischen den Läufen.
- Mittlere Laufzeit ohne erstes Bild: 797,2 ms und 772,4 ms (Unterschied 3,11 %). Das erste Bild des ersten Laufs enthält das Laden des Modells.
- Vergleichsgrenze für genau diesen Bildweg: Codes je Bild gleich, keine zusätzlichen technischen Fehler, mittlere Zeit ohne Erstbild höchstens 5 % langsamer als der erste Lauf. Für QualityGate und Videos gibt es noch keine belastbare Grenze.
- Messdateien und Prüfsummen: C:\Users\Besitzer\Documents\ChatGPT\SewerStudio_10Tage_20260925_Snapshot\eval-baseline-summary.json.

## Video-Ausgangsmessung (technischer Teil der KI-Kette)

- Der Nutzer hat `D:\Haltungen` als Videoordner genannt. Die 17 Haltungen des eingefrorenen EVAL_SET haben dort jeweils ein Video. Vier davon wurden als fester lokaler Referenzsatz gewählt; Originale blieben unverändert. Pfade, Kennungen und SHA-256-Prüfsummen liegen nur im lokalen Snapshot, nicht im Git-Repository.
- Der erste Start scheiterte, weil in der getrennten Arbeitskopie die Python-Umgebung fehlte. Die vorhandene Umgebung (8,02 GB) und Modellgewichte (2,89 GB) wurden aus dem ursprünglichen Projekt kopiert, ohne Pakete herunterzuladen. Zwei dabei überschriebenen Git-Dateien wurden sofort auf den Stand des Arbeitsbranches zurückgesetzt.
- Vier Videos zweimal geprüft, pro Durchlauf drei Bilder: 8/8 technische Läufe bestanden; alle Pflichtprüfungen für Dekodierung, YOLO, DINO, SAM, Quantifizierung und produktive Mehrmodell-Verarbeitung grün. Erkennungszahlen je Video waren zwischen beiden Läufen gleich. Der erste Lauf eines Videos war bei der Summe der drei Produktionsbilder rund 29 % langsamer; damit gilt für diesen Wert noch keine ±5-%-Grenze.
- Diese Prüfung misst keine endgültigen EN-Codes und keine QualityGate-Entscheidung. Dafür bleibt eine eigene Ausgangsmessung nötig. Berichte und Zusammenfassung: `C:\Users\Besitzer\Documents\ChatGPT\SewerStudio_10Tage_20260925_Snapshot\video-baseline-summary.json`.

## Sidecar-Testwerkzeug: Prozessstart und Aufräumen

- Claude Code über das vorhandene Max-Abo prüfte ausschließlich `SidecarProcessLease.cs` ohne Werkzeugzugriff und ohne Kundendaten. Codex prüfte die Hinweise gegen den Code und setzte die Korrektur um. Ein zweiter Claude-Diff-Review meldete zwei berechtigte Nacharbeiten; eine behauptete Prozesslücke bei Nutzerabbruch war durch den allgemeinen `catch` bereits abgedeckt.
- Das Startzeitlimit begrenzt nun auch den Health-Aufruf und das Lesen der Startausgabe. Prozessausgaben werden nur mit begrenzter Länge behalten. Fehler beim Beenden des selbst gestarteten Sidecars werden gemeldet; wenn Start und Aufräumen fehlschlagen, bleiben beide Ursachen sichtbar.
- Release-Gesamtbuild nach der letzten Nacharbeit grün, 0 neue Warnungen; beim vorherigen vollständigen Neuaufbau dieselben fünf vorhandenen Warnungen. Drei gezielte Vertragstests grün, echter Drei-Bilder-Videotest nach der Hauptkorrektur grün mit unveränderten Erkennungszahlen. Ein absichtlich auf eine Sekunde begrenzter Start scheiterte korrekt; danach waren weder Port 8100 noch ein Sidecar-Prozess aktiv.
- Kontingentprüfung nach der Arbeit: Codex-Wochenfenster 20 % genutzt, keine Zusatzguthaben vorhanden oder gekauft. Claude Code ist per `claude.ai` am Max-Abo angemeldet; der aktuelle Claude-Prozentwert wurde bei dieser Prüfung nicht erneut ausgelesen.

## SQLite-Schnappschuss bei offener Wissensdatenbank

- Neuer Regressionstest mit künstlicher SQLite-Datei: Ein bereits benutzter Verbindungspool und eine offene `KnowledgeBaseContext`-Verbindung bleiben bestehen, während ein Eintrag aus dem WAL in eine selbstständige Sicherungsdatei übernommen wird. Die Quelle lässt sich danach weiter beschreiben; die wieder geöffnete Sicherung enthält nur den früheren Stand.
- Claude Code prüfte ausschließlich den synthetischen Test-Diff. Zwei konkrete Lücken (benutzter Pool, weitere Schreibbarkeit der Quelle) wurden ergänzt. Zwei gezielte Tests bestanden. Release-Gesamtbuild: 0 Fehler, dieselben fünf vorhandenen Warnungen.
- Dies belegt Sicherung und Wiederöffnen der Wissensdatenbank, keinen fachlichen Import aus einem Fremdformat. Ein solcher Import benötigt einen eigenen, gezielten Test.

## Ferienläufe: Vorbereitung ohne Aktivierung

- In der Codex-App sind zwei tägliche Läufe als pausierte Automation `sewerstudio-ferienarbeit` angelegt. Es wurde kein unbeaufsichtigter Lauf gestartet.
- `scripts/ferien-lock.ps1` legt die Lauf-Sperre atomar an und protokolliert einen zweiten Start als übersprungen. Ein fremder Token kann die Sperre nicht freigeben. Diese Fälle wurden lokal geprüft; die Sperre ist danach wieder frei.
- Ohne `tmp/ferien/READY.json` wird jeder Start technisch abgelehnt. Ein unvollständiger Testmarker wurde ebenfalls abgelehnt und sofort wieder entfernt. Der echte Marker existiert nicht. Er muss alle sechs Prüfungen, Ablaufdatum, Arbeitsordner und SHA-256 des lokalen Videoreferenz-Manifests enthalten. Die Zugriffstrennung und der Neustart-/Anmelde-/Fehler-/Fortsetzungs-Probelauf fehlen weiterhin.
- Der Nutzer hat die Entscheidungsregel danach präzisiert: Während seiner Ferien eigenständig innerhalb der schon vereinbarten Grenzen arbeiten. Kleine geprüfte Änderungen im getrennten Branch brauchen keine erneute Einzelentscheidung. Die Aufgabenliste und die pausierte Automation werden daran angepasst. Die Sicherheitsbedingungen vor unbeaufsichtigten Läufen gelten weiter.
- Claude Code prüfte nur `scripts/ferien-lock.ps1` über das vorhandene Max-Abo und ohne Werkzeugzugriff. Konkrete Treffer: gleichzeitige Log-Schreibversuche, ungültige Freigabedaten, unbemerkter Git-Fehler und unklare Freigabefehler. Der Code wiederholt kurz bei Log-Kollisionen, blockiert ungültige Daten ausdrücklich und prüft beide Git-Rückgaben. Eine nach Anlage der Sperre fehlschlagende Protokollierung räumt die eigene Sperre auf. PowerShell-Parser: 0 Fehler; ohne READY.json endet Acquire mit Code 5 und ohne Sperre. Ein unvollständiger Testmarker blockierte ebenfalls und wurde entfernt.
- Qwen 27B startete wegen eines lokalen CUDA-Initialisierungsfehlers nicht auf der Grafikkarte. Die Gegenprüfung lief daraufhin lokal auf der CPU und das Modell wurde danach entladen. Seine drei Hinweise wurden geprüft: Die Log-Kollision ist bereits behoben; der relative Manifestpfad scheitert bei falschem Ort geschlossen; die gemeldete zweite Sperr-Race ist in diesem Ablauf kein belegter Doppelstart. Keine Kundendaten an Claude oder Qwen gegeben.
- Eine verwaiste Sperrdatei wird bewusst nicht automatisch entfernt, weil ein langer noch aktiver Lauf sonst überlappt werden könnte. Ihr Zustand muss vor einer Freigabe der unbeaufsichtigten Ferienläufe als Stoppsignal dokumentiert und bei Bedarf gemeldet werden.
- Vollständiger Release-Build nach der Sperrenkorrektur: 0 Fehler, dieselben fünf vorhandenen Warnungen. Codex-Wochenkontingent danach 21 % genutzt, keine Zusatzguthaben.

## Fehlende Verknüpfungen der Komplettsicherung

- Das lokale Sicherungsmanifest wurde nur lesend ausgewertet: 128 fehlende verknüpfte Dateien. C: 19 PNG; D: 16 JPG und 46 PDF; F: 4 PDF; G: 28 PDF und 15 MP4. Alle 128 Quellpfade waren bei der Nachprüfung weiterhin nicht vorhanden. F: war nicht eingebunden. Keine Kundendatei an Claude oder Codex gesendet.
- Bericht: `docs/reviews/2026-09-25-sicherungsluecken.md`. Er belegt die Lücke der aktuellen Sicherung; ältere Kopien sind nicht nachgewiesen.
- Prüfung der möglichen Zugriffsgrenze: Die lokale Codex-CLI bietet einen Windows-Sandbox-Befehl. Ein separates Testprofil mit `:root = deny` und sogar ausdrücklichem `deny` für eine ungefährliche Datei im Originalprojekt wurde eingerichtet. Der Sandbox-Prozess lief unter dem eigenen Konto `CodexSandboxOffline`, konnte die Originaldatei aber weiterhin öffnen und den Ordner `D:\Haltungen` auflisten. Das Testprofil wurde wieder entfernt; die normalen Einstellungen sind unverändert. Der Versuch belegt auf diesem Rechner **keine** ausreichende Lesesperre. WSL und Docker sind nicht eingerichtet. Die Automation bleibt pausiert. Offizielle Anleitung: https://learn.chatgpt.com/docs/permissions.

## SQLite-Import bei offener Wissensdatenbank

- Neuer synthetischer Verhaltenstest `KnowledgeBackupOpenDatabaseImportTests`: Eine offene, im WAL beschriebene Wissensdatenbank bleibt beim abgewiesenen Import lesbar. Eine zweite Verbindung sieht denselben alten Wert; `PRAGMA integrity_check` liefert `ok`, und temporäre Importdateien bleiben nicht zurück. Nach Schließen aller Verbindungen gelingt derselbe Import und der neue Wert ist sichtbar.
- Claude Code prüfte ausschließlich den Testtext ohne Werkzeuge. Der sichtbare Fehlertext und die Inhaltsprüfung wurden nach seinen Hinweisen ergänzt. Seine pauschale Forderung nach einem Datei-Hash wäre bei WAL/Checkpoint kein stabiler Beleg; der Test prüft die wieder geöffnete SQLite-Datenbank direkt. Drei gezielte Backup-Tests bestanden. Vollständiger Release-Build: 0 Fehler, 1 bereits vorhandene Warnung im inkrementellen Lauf; kein neuer Warnungstyp.
- Produktcode blieb unverändert. Der Import darf während einer noch offenen Verbindung scheitern und meldet dies. Ein automatischer Austausch einer aktiv benutzten Datenbank wäre eine größere Verhaltensänderung und wurde nicht vorgenommen.

## Älteren KI-Audit gegen den Start-Commit geprüft

- Die drei Befunde KI-01 bis KI-03 des Audits vom 23.09. sind im sauberen Start-Commit bereits umgesetzt: unbrauchbare Bilder werden als nicht beurteilbar markiert; ein fehlender konfigurierter Eval-Ordner sperrt die Wissenssuche; gleichzeitige GPU-Ladungen nutzen eine gemeinsame Reservierung. Dazu bestanden 9 Suchschutz-, 6 Bildqualitäts- und 16 Sidecar-Speichertests. Kein doppelter Produktcode geschrieben.
- Die vier Video-Proben messen derzeit nur den technischen Sidecar-Weg. Ein vollständiger Vergleich der endgültigen EN-Codes und QualityGate-Entscheidungen aus dem Video ist noch offen. Das `CodingReplay`-Werkzeug liegt nur in den geschützten offenen Originaländerungen, nicht im sauberen Start-Commit; es wurde nicht pauschal übernommen.

## QGIS-Videoposition mit künstlichen Daten

- Der bestehende Router-Endpunkt `/qgis/video_position.json` erhält zwei neue Verhaltenstests: 30 Sekunden von 100 bei 50 Metern liefern 15 Meter und kennzeichnen die grobe Quelle; ohne Länge und Stützstellen kommt 404 statt eines erfundenen Markers. Beide UI-Tests und 14 Python-Brückentests bestanden.
- Das Python-Testpaket enthält noch keinen QGIS-Kartenlauf mit dem echten Plugin. Diese sichtbare Prüfung bleibt offen. Weder Kundenprojekt noch QGIS-Projekt wurden geändert.


## Excel-Leistungsverzeichnis mit stabilem Beispiel

- Die bestehenden ClosedXML-Tests für die Haltungs- und Schachtvorlagen prüfen bereits Spalten, Zahlen, Formeln, Diagramme und Formatierung. Zusätzlichen Schutz brauchte das zweiteilige NPK-Leistungsverzeichnis.
- Neuer Test mit rein künstlichen Positionen aus zwei Kapiteln: Die Firmenansicht enthält keine internen Einheitspreise; beide Ansichten behalten Positionsnummern, Mengen, Preisformat und Rechenformeln. Die interne Ansicht errechnet 250 und 120 CHF als Zwischentotale, 370 CHF netto, 29.97 CHF MwSt und 399.97 CHF brutto. 50 CHF ausgeschlossene Pauschale bleiben separat ausgewiesen.
- Claude Code prüfte nur den Test-Diff ohne Dateiwerkzeuge und ohne Kundendaten. Seine Hinweise zu Zwischenwerten, Textnummern und Pauschalen wurden ergänzt. 28 gezielte Excel-Tests bestanden; der abschliessende vollständige Release-Build war grün mit 0 Warnungen. Kein Produktcode wurde geändert.

## Kleine Wartbarkeitskorrektur: Compiler-Warnungen

- Der Fototest prüft vor Verwendung ausdrücklich, dass der erfolgreiche Aufnahmevorgang einen Fotopfad geliefert hat. Die Projektprüfung benennt beide Einträge ihrer kleinen Feld-/Katalogliste vollständig; die bisher ignorierten Tupelnamen erzeugen keine Compiler-Warnung mehr. Das Prüfverhalten und öffentliche Verträge bleiben gleich.
- Drei gezielte Fototests und 17 Tests der Projektprüfung bestanden. Vollständiger Release-Build nach den Änderungen: 0 Fehler, 0 Warnungen. Keine Kundendaten verwendet.

## Wechsel ins direkte VS-Code-Projekt (25.09.)

- Auf ausdrücklichen Wunsch des Nutzers wurden die zwölf geänderten/neuen Dateien aus der getrennten Arbeitskopie in `C:\Sewer-Studio_KI_5.0` übertragen. Vorher wurde geprüft, dass keine dieser Dateien zu den 206 bereits offenen Dateien gehört; der Patch passte ohne Konflikte. Die offenen Arbeiten wurden nicht gestaged oder umgeschrieben.
- VS Code zeigt das direkte Projekt. Sein Terminal meldete für den vollständigen Release-Build sowie die gezielten Infrastruktur- und UI-Tests jeweils Rückgabecode 0. Der bisherige separate Branch bleibt als Rückfallstand erhalten. Die unbeaufsichtigte Automation ist weiterhin pausiert.

## Ferienläufe im direkten Projekt aktiviert (25.09.)

- Die bestehende Codex-Automation `sewerstudio-ferienarbeit` wurde auf das direkte VS-Code-Projekt umgestellt und für 09:00 und 19:00 aktiviert. Pro Lauf ist nur eine kleine Code-Aufgabe vorgesehen. Claude erhält bei Bedarf ausschliesslich einen bereinigten Diff ohne Dateiwerkzeuge; Qwen bleibt optional und lokal. Codex-Kontingent vor Aktivierung: 23 % des Wochenfensters genutzt; Claude zeigte 39 % Wochenverbrauch. Keine Zusatzguthaben.
- Der lokale Startschutz erkennt den Branch, ein befristetes Code-Modus-Merkmal und eine Liste von 206 geschützten offenen Pfaden. `Acquire`/`Release`, zweiter gleichzeitiger Start sowie `CheckPath` für geschützte, erlaubte und ausserhalb liegende Pfade wurden geprüft. Claude prüfte nur den Script-Diff und meldete Pfad- und Verknüpfungsrisiken; diese wurden eingegrenzt.
- `origin` hat im lokalen Repository eine absichtlich ungültige Push-Adresse; die ursprüngliche Adresse liegt nur lokal zur Rückstellung. Kein Push ausgeführt. Die technische Lesesperre ausserhalb des Projekts ist weiter nicht belegt. Darum sind die automatischen Läufe ausdrücklich auf Code und künstliche Tests begrenzt; Kundendaten, Video-/GPU-Messungen und Laufwerke D:/F:/G: bleiben für sie tabu. Das ist eine Verhaltensgrenze, keine nachgewiesene Betriebssystem-Sperre.

## Erster begrenzter Ferienlauf: variabler Excel-Preis

- Die Lauf-Sperre wurde gesetzt; die neue Testdatei und die drei Protokolldateien waren laut `CheckPath` erlaubt. Ein künstlicher NPK-Fall mit variablem Preis und separat ausgewiesener Pauschale prüft beide Reiter. Die Firmenansicht behält Position, Menge und Einheit und zeigt keinen internen Schätzwert im Total. Der interne Reiter zeigt 789.45 CHF; 22.20 CHF Pauschale bleiben ausserhalb der Summe.
- Claude erhielt im VS-Code-Terminal nur den Test-Diff ohne Dateiwerkzeuge. Sein Hinweis auf Menge/Einheit im Firmenblatt wurde umgesetzt. Der interne Gesamtbetrag bei leerem Einheitspreis ist beim variablen Sammelpreis absichtlich ein fester Wert; hier war der Hinweis keine Änderung am Produkt. Acht gezielte Tests und der vollständige Release-Build bestanden, 0 Warnungen.
- Die eigene Sperre wurde nach dem Lauf freigegeben. Keine geschützte Datei, Kundendatei oder Fernadresse wurde geändert oder hochgeladen.

## Excel-Leistungsverzeichnis ohne Positionen (25.09.)

- Codex in VS Code nahm einen lesenden Prüfauftrag an, konnte die Datei wegen einer zu engen Formulierung ohne Lesebefehle aber nicht öffnen. Claude Code las nur die synthetische Testdatei und bestätigte die Testlücke. Qwen prüfte die Testidee lokal in Cline ohne Werkzeugfreigaben. Sein Vorschlag setzte die Kopfzeile auf Zeile 1; der echte Export setzt sie auf Zeile 7. Die Zeilenannahme wurde nicht übernommen. Danach wurde Qwen aus dem Grafikspeicher entladen.
- Neuer synthetischer Test prüft in beiden Reitern die neun Kopfzellen in Zeile 7, das Fehlen von Positionszeilen und einen Gesamtbetrag von null. Keine Produktlogik geändert. Alle 9 Tests der Excel-Testklasse und der vollständige Release-Build bestanden; Build mit 0 Warnungen und 0 Fehlern.
- Der vollständige Infrastruktur-Testlauf blieb aus, weil er auf diesem Rechner echte Kundendaten auf D: liest. Nur die gezielte Klasse wurde ausgeführt. Die 206 geschützten offenen Dateien blieben unangetastet; kein Push und keine Käufe.

## Wartbarkeit als neuer Schwerpunkt (25.09.)

- Nutzerauftrag auf bestehenden Code und Wartbarkeit eingegrenzt. Die bestehende Automation `sewerstudio-ferienarbeit` auf kleine verhaltensgleiche Aufräumarbeiten umgestellt; Zeitplan 09:00/19:00, Kosten- und Datenschutzgrenzen bleiben bestehen.
- Die Lauf-Sperre las das Freigabedatum `2026-10-05` durch erneutes Parsen eines bereits deserialisierten Datums als 10. Mai und blockierte den Lauf. `scripts/ferien-lock.ps1` übernimmt JSON-Datumswerte jetzt direkt als UTC-Zeitpunkt und behandelt rohe ISO-Werte mit fester, sprachunabhängiger Form. Geprüft: Acquire erfolgreich, Status zeigt die Sperre mit korrektem Datum, falscher Release-Token wird abgewiesen.
- `NpkLeistungsverzeichnisExcelExporter.cs`: `WriteSheet` erstellt weiter dieselben zwei Blätter, aber Kopfbereich und Drucklayout liegen nun in benannten privaten Methoden. Die Kopfzeile 7 ist als gemeinsame Konstante definiert. Keine öffentliche Schnittstelle und keine fachliche Formel geändert. Neun gezielte Excel-Tests bestanden; vollständiger Release-Build: 0 Fehler, 0 Warnungen.
- Qwen wurde für einen lokalen Diff-Blick gestartet. Seine ausführliche Ausgabe lieferte vor dem Abbruch keinen belegten Fehler; die Entscheidung beruht auf eigenem Diff-Abgleich und den Tests. Qwen wurde danach entladen. Keine Cloud-Daten, Käufe oder Pushes.

## Excel-Vorlagenexport: Feldzuordnung getrennt (25.09.)

- Die beiden Exportmethoden enthielten die jeweilige Schleife zur Feldzuordnung mitten im Ablauf für Vorlagenladen, Zeilenstil, Verweise und sicheres Speichern. Die Feldzuordnung für Haltungen und Schächte liegt jetzt in je einer benannten privaten Methode; Reihenfolge, Bedingungen und Fehlerbehandlung blieben gleich. Die öffentlichen Verträge und gespeicherten Formate wurden nicht geändert.
- 45 gezielte synthetische Excel-Tests bestanden, darunter Datenübertragung, Links, Zahlen und Schutz des bestehenden Ziels. Vollständiger Release-Build: 0 Fehler, 0 Warnungen. Claude prüfte ausschliesslich den Code-Diff ohne Werkzeuge mit niedrigem Denkaufwand und meldete keine belegte Verhaltensänderung. Kein API-Schlüssel gesetzt; Nutzung über das vorhandene Abo.
- Der Nutzer möchte Claude künftig bevorzugt für konkrete Codevorschläge und Prüfungen einsetzen, damit Codex-Kontingent für Auswahl, Integration und Abschlusskontrolle bleibt. Qwen bleibt lokal für kurze mechanische Aufgaben.

## QGIS-Schachtindex und ehrlicher Stand der Selbstständigkeit (25.09.)

- Nutzerhinweis: Die bisherigen Methodenauslagerungen allein belegen noch keine deutliche Verbesserung der Wartbarkeit. Zwei geplante Läufe sind aktiv, aber ein vollständig unbeaufsichtigter Lauf ist noch nicht nachgewiesen; die bisherigen Einträge in `tmp/ferien/run.log` stammen aus manuellen Arbeiten. Das bleibt offen und darf nicht als bestanden gemeldet werden.
- Claude prüfte den QGIS-Schnappschuss lesend und fand drei Kandidaten. Der Vorschlag, den vorhandenen Rohindex umzubenennen, hätte die Fallback-Geometrie gefährdet. Stattdessen bleibt der Rohindex erhalten und ein normalisierter Schachtindex wird beim Netzladen einmal aufgebaut. Die Auswahl und der Sanierungstyp nutzen denselben Index; zwei wiederholte Aufbereitungen pro Aufruf entfallen. Bei gleichen Normalnamen gewinnt weiterhin der erste Katasterpunkt.
- Neuer synthetischer Verhaltenstest für einen Schachtnamen mit Leerzeichen bestand vor und nach dem Umbau. Insgesamt 58 gezielte QGIS-Tests bestanden; vollständiger Release-Build: 0 Fehler, 0 Warnungen. Claude prüfte den Diff ohne Werkzeuge; sein Hinweis auf leere Normalnamen wurde gegen den tatsächlichen Code abgeglichen. Die bisherigen Datenformate und Roh-Lookups sind unverändert.

## QGIS-Router: gemeinsame Pfadbereinigung (25.09.)

- GET und POST verwenden jetzt dieselbe private Methode, die den Abfrageteil ab dem ersten `?` entfernt. Der unabhängige Diff-Abgleich ergab gleiches Verhalten auch für Randfälle; die doppelte Logik entfällt.
- Laut Claude: 47/47 synthetische QGIS-Tests grün, Release-Build mit 0 Fehlern und 0 Warnungen. Diese Läufe wurden hier nicht erneut ausgeführt.

## QGIS: gemeinsame XTF-Pfadauflösung (25.09.)

- Fingerprint und Netzladen nutzen dieselbe private XTF-Pfadauflösung. Der unabhängige Diff-Abgleich bestätigte gleiches Verhalten für null, leere und reine Whitespace-Pfade sowie die `File.Exists`-Prüfungen; der Kommentar beschreibt nun auch die Prüfung im Resolver korrekt.
- Laut Claude: 140/140 synthetische QGIS-UI-Tests grün; Release-Build mit 0 Fehlern und 0 Warnungen. Für den reinen Kommentar-Nachtrag wurden die Läufe nicht wiederholt.

## SchachtFeldnamen: gemeinsame Filterlogik (25.09.)

- `Feld` und `Schreibweisen` nutzen dieselbe Filterlogik für gleich gefaltete Feldnamen. Die unabhängige Prüfung bestätigte unveränderte Reihenfolge, Treffer und Rückgabewerte; die entfernte Doppelregel ist ein kleiner echter Wartungsgewinn.
- Laut Claude: 41/41 gezielte Tests grün; Release-Build mit 0 Fehlern und 0 Warnungen. Hier nicht erneut ausgeführt.

## FachzahlParser: gemeinsamer Parse-Ablauf (25.09.)

- `TryParseDecimal` und `TryParseMeasurement` nutzen jetzt denselben Ablauf aus Normalisieren und Parsen; ihre unterschiedlichen Regeln für drei Dezimalstellen bleiben erhalten. Die unabhängige Prüfung bestätigte gleiches Verhalten bei Kultur, Zahlenformat, leeren Eingaben, Fallback-Reihenfolge und Rückgabewerten.
- Laut Claude: 22/22 gezielte Tests grün; Release-Build mit 0 Fehlern und 0 Warnungen. Hier nicht erneut ausgeführt.

## Schacht-Empfehlung: gemeinsame Auswahl (25.09.)

- Ziel: Die doppelte Auswahl markierter Kostenzeilen für Maßnahmentext und Nettosumme an einer Stelle führen. Claude änderte `SchachtEmpfehlungTextFormatter` und ergänzte einen synthetischen Test; Codex prüfte den Diff unabhängig.
- Ergebnis: Reihenfolge und Null-Verhalten bleiben gleich. Eine markierte Zeile ohne Text fehlt nur im Maßnahmentext und zählt mit `Qty * UnitPrice` weiter zur Summe (Testfall: 350 + 2 × 60 = 470). **5/5 gezielte Tests bestanden**; der von Codex ausgeführte vollständige Release-Build endete mit **0 Fehlern und 0 Warnungen**.
- Grenze: Geprüft wurden die synthetischen Formatter-Fälle; kein echter Projektlauf und kein vollständiger Testlauf mit möglichen Kundenquellen.

## Ferienlauf 17 Uhr: NPK-Positionszeile getrennt (25.09.)

- Vorher enthielt `WriteSheet` auch die gesamte Positionszeile. Claude lagerte Zellwerte, Formatierung und die drei Preisfälle in `WritePositionRow` aus; `WriteSheet` behält Kapitel, Zeilenfortschritt und Summen. Codex bestätigte im unabhängigen Diff-Review unveränderte Reihenfolge, Formeln und Ausgabe.
- 9/9 gezielte synthetische Tests bestanden; vollständiger Release-Build: 0 Fehler, 0 Warnungen. Kein Kunden- oder Medienlauf.

## Ferienlauf 21 Uhr: Meterparser zusammengeführt (25.09.)

- Ziel: Die doppelte Komma-Normalisierung und `double.TryParse`-Regel für erstes und zweites Meterfeld an einer Stelle halten. Claude führte dafür den privaten Helper `ParseMeterGroup` ein; Codex prüfte den Diff unabhängig. Trefferwahl, Regex, Suffix-Treffer, `mm`-Ausschluss und Null-Exception blieben gleich.
- Baseline laut Claude: 6/6 synthetische Tests; danach bei Codex erneut 6/6. Vollständiger Release-Build bei Codex: 0 Fehler, 0 Warnungen. Kein Kunden- oder Medienlauf.

## Offene Freigaben und Grenzen

- Die 128 Sicherungslücken umfassen 15 MP4 und 78 PDF; ältere Kopien können existieren, sind hier aber nicht nachgewiesen.
- Der vollständige Infrastrukturtest liest auf diesem Rechner eine echte Kundenquelle auf D:. Ein solcher Gesamtlauf ist für die Ferienarbeit ungeeignet. Weitere Testauswahl nur nach Datenpfad-Prüfung.
- VS Code ist nach dem Entsperren erreichbar. Cline und Qwen wurden im getrennten Arbeitsordner nachgewiesen. Ein unbeaufsichtigter Probelauf mit Neustart und Fehlerfällen steht noch aus.
- Für neue Arbeit wurde der saubere Start-Commit gewählt. Die 206 offenen Originaldateien bleiben unangetastet. Der Nutzer hat `D:\Haltungen` als lokalen Videoordner genannt; vier passende Videos sind im Referenz-Manifest erfasst.


- Ausgewählte Tests ohne Kundenquelle: 32 UI-Prüfungen (QGIS, QualityGate-Anzeige, Sicherungstext) und 7 Infrastrukturprüfungen (SQLite-Schnappschuss, Sicherungsprotokoll) bestanden.
- Das ursprüngliche Projekt blieb nach dem Snapshot unverändert: Commit und Prüfsummen aller 206 Dateien stimmen noch.
