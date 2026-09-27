# Tagesbericht

## 2026-09-25

1. Sicherung abgeschlossen, 128 fehlende Verknüpfungen offen, darunter 15 Videos.
2. Originale mit 206 offenen Dateien lokal gesichert und nicht verändert.
3. Saubere Arbeitskopie vom Commit `630a11c61` angelegt.
4. Build grün; 5 bereits vorhandene Warnungen im ersten Build.
5. Cline/Qwen und vier Videos geprüft; Claude prüfte Sidecar/SQLite. Zwei Ferienläufe vorbereitet, aber bis zur Zugriffstrennung und zum Probelauf pausiert.
6. Eigenständige Arbeit bestätigt: Claude prüfte die Lauf-Sperre, Qwen prüfte lokal auf der CPU gegen. Bestätigte Fehlerfälle der Sperre behoben; Ferienläufe weiter pausiert.
7. Sicherungsmanifest nur lesend ausgewertet: 128 aktuell fehlende Verknüpfungen nach Laufwerk und Art dokumentiert. Keine Originale geändert.
8. SQLite-Import mit künstlichen Daten getestet: offener Stand bleibt erhalten, nach Schließen klappt der Import; drei gezielte Tests und Release-Build grün.
9. Drei ältere KI-Audit-Befunde waren schon behoben (31 gezielte Tests grün). Zwei neue QGIS-Schnittstellentests und 14 Python-Brückentests grün; echter Kartenlauf noch offen.
10. Excel-LV mit künstlichem Beispiel für beide Reiter und nachgerechnete Summen geschützt; 28 gezielte Tests grün, Claude prüfte den Test.
11. Fünf bisherige Compiler-Warnungen gezielt bereinigt; 20 passende Tests und Release-Build grün.
12. Auf Wunsch des Nutzers zwölf eigene Dateien ins direkte VS-Code-Projekt übertragen, ohne die 206 offenen Dateien zu überschneiden. Build und gezielte Tests dort erfolgreich.
13. Zwei tägliche Code-Läufe im direkten Projekt aktiviert. 206 offene Pfade lokal geschützt, Überschneidungssperre geprüft, Git-Push lokal gesperrt. Technische Lesesperre für andere Laufwerke bleibt offen; deshalb keine Kundendaten- oder Videoläufe.
14. Ersten Code-Lauf gestartet: variablen NPK-Excel-Preis und separate Pauschale mit künstlichen Daten geprüft. Claude sichtete nur den Diff; 8 Tests und Release-Build grün.
15. Excel-Export ohne Positionen geschützt: 9 gezielte Tests und Release-Build grün, 0 Warnungen. Claude prüfte die Lücke in VS Code; Qwen lief lokal in Cline ohne Werkzeugfreigaben. Keine Kundendaten genutzt.
16. Auftrag auf Wartbarkeit eingegrenzt. Datumsfehler der Lauf-Sperre behoben und NPK-Excel-Methode gegliedert; 9 Tests und Release-Build grün, 0 Warnungen. Qwen nach lokalem Blick entladen; kein Push.
17. Excel-Vorlagenexport gegliedert: Haltungs- und Schacht-Feldzuordnung getrennt; 45 passende Tests und Release-Build grün, 0 Warnungen. Claude prüfte den Diff über das vorhandene Abo.
18. QGIS: normalisierten Schachtindex einmal beim Netzladen aufgebaut statt für Ausgaben erneut. Claude fand die Stelle; 58 Tests und Release-Build grün. Unbeaufsichtigter Zeitplan weiterhin noch nicht nachgewiesen.
19. QGIS-Router: GET/POST bereinigen den Pfad jetzt gemeinsam; unabhängiger Diff-Befund verhaltensgleich. Laut Claude 47/47 synthetische QGIS-Tests grün und Release-Build mit 0 Fehlern/0 Warnungen; hier nicht erneut ausgeführt.
20. QGIS: gemeinsame XTF-Pfadauflösung für Fingerprint und Netzladen; laut unabhängigem Review verhaltensgleich, missverständlichen `File.Exists`-Kommentar korrigiert. Laut Claude 140/140 synthetische QGIS-UI-Tests und Release-Build mit 0 Fehlern/0 Warnungen; nicht erneut ausgeführt.
21. `SchachtFeldnamen`: gemeinsame Filterlogik für `Feld`/`Schreibweisen`, laut unabhängiger Prüfung verhaltensgleich und kleiner echter Wartungsgewinn. Laut Claude 41/41 gezielte Tests und Release-Build mit 0 Fehlern/0 Warnungen; hier nicht erneut ausgeführt.
22. `FachzahlParser`: doppelten Ablauf aus Normalisieren und Parsen zusammengeführt; laut unabhängiger Prüfung verhaltensgleich. Laut Claude 22/22 gezielte Tests und Release-Build mit 0 Fehlern/0 Warnungen; hier nicht erneut ausgeführt.
23. Schacht-Empfehlung: Claude führte die Auswahl markierter Kostenzeilen zusammen; Codex prüfte Diff und neuen Leertext-Preisfall. 5/5 synthetische Tests und vollständiger Release-Build mit 0 Fehlern/0 Warnungen; echter Projektlauf offen.
24. Ferienlauf 17 Uhr: Claude trennte die NPK-Positionszeile in `WritePositionRow`; `WriteSheet` behält Kapitel, Zeilenfortschritt und Summen. Codex-Review bestanden, 9/9 synthetische Tests und Release-Build mit 0 Fehlern/0 Warnungen. `TASKS.md`-Punkt 5 erledigt.
25. Ferienlauf 21 Uhr: Claude vereinte Komma-Normalisierung und `double.TryParse` beider Meterfelder; Codex-Review verhaltensgleich. Baseline 6/6 laut Claude, Nachprüfung 6/6; Release-Build 0 Fehler/0 Warnungen. Kein passender offener `TASKS.md`-Punkt.

## 2026-09-26

- Ferienlauf: Claude vereinte die Dezimalregel in `MeasureRecordParser`; Codex gab den Diff frei. Laut Claude Baseline 59/59, danach 61/61 synthetische Tests und Release-Build 0 Fehler/0 Warnungen. Overflow-Test bleibt offen.
- 03-Uhr-Lauf: `PipelineStatusParser` nutzt eine Zählregel statt zwei. Claude: 24/24 vor und 26/26 nachher, Release-Build 0 Fehler/0 Warnungen; Codex-Review bestanden. Punkt 7 erledigt.
- 05-Uhr-Lauf: `GroundTruthFieldParser` nutzt eine Dezimalregel statt zwei; Codex-Review bestanden. Laut Claude 30/30 vor, 31/31 danach und Release-Build 0 Fehler/0 Warnungen. Punkt 8 erledigt.
- 07-Uhr-Lauf: `WinCanValueNormalizer` vereint drei Treffer-/Komma-Blöcke; Codex-Review bestanden. Gemeldet: 70/70 vor, 73/73 danach, Release-Build 0 Fehler/0 Warnungen. Punkt 9 erledigt.
- Wartbarkeitsplan Etappe 1: `ProtocolEntryInputNormalizer` nutzt eine gemeinsame Ganzzahl-/Bereichsregel; Codex-Review bestanden, 79/79 gezielte Tests vor und nach der Änderung, vollständiger Release-Build laut Nutzer erfolgreich. Punkt 10 erledigt; kein projektübergreifender Gesamttest.
- Wartbarkeitsplan Etappe 2: Section/Node-Katalogpfade nutzen eine parametrisierte Suchfolge; Codex-Review bestanden und 11/11 projektlokale synthetische Tests grün. Release-Build laut Claude 0 Fehler/0 Warnungen; Default-Fallback weiter ohne isolierten Test. Punkt 11 erledigt.
- Wartbarkeitsplan Etappe 3, TEIL 1: sechs gleiche `RetryRequired`-Checkpoints vereint, SAM-VRAM und Journalfolge getestet; Codex 45/45, Claude meldete 54/54 und Release-Build 0/0. Weitere gemeinsame Fehlerregel offen; Testpfade künftig über `TEMP`/`TMP`/`SEWERSTUDIO_TELEMETRY_DIR` im Projekt halten.
- Wartbarkeitsplan Etappe 3, TEIL 2: sechs gleiche Trace/Dedup/Checkpoint-Folgen vereint und allgemeine Fehler von YOLO/DINO/SAM zusätzlich getestet; Codex 47/47, Claude Release-Build 0 Fehler/0 Warnungen. Zwei AppData-Metadatenabfragen vor Korrektur der Projektgrenze im JOURNAL festgehalten.
- Wartbarkeitsplan Etappe 4, TEIL 1: Teacher-Export als eigenen Schritt nach dauerhaftem Goldsample gefasst; zwei Ist-Tests fuer `OperationCanceledException` ergaenzt. Codex-Review verhaltensgleich, 6/6 gezielte Tests und Release-Build 0 Fehler/0 Warnungen.
- Wartbarkeitsplan Etappe 4, TEIL 2: KB-Nachtrag als eigenen Schritt nach dauerhaftem Sample gefasst und Status-Nachtragsfehler synthetisch geschützt. Codex-Review verhaltensgleich, 8/8 gezielte Tests; Claude meldete Release-Build 0 Fehler/0 Warnungen.
- Wartbarkeitsplan Etappe 4, TEIL 3: Maskenprüfung und Flächenzählung als benannten Schritt gefasst; unlesbare Bildmaße als Entwurf geschützt. Claude meldete 51/51 Baseline-Tests; Codex-Review verhaltensgleich, 8/8 Nachtests und vollständiger Release-Build 0 Fehler/0 Warnungen.
- Wartbarkeitsplan Etappe 4, TEIL 4: 21 identische Abweisungsergebnisse in einer privaten Ergebnisfabrik vereint; Codex-Review verhaltensgleich, 3/3 synthetische Nachtests und Release-Build mit 0 Fehlern/0 Warnungen.
- Wartbarkeitsplan Etappe 4, TEIL 5: Goldbildkopie, Pfadprüfung und SHA-256 als einen Schritt gekapselt; neuer synthetischer Lesefehler-Test, 4/4 gezielte Nachtests und Release-Build 0 Fehler/0 Warnungen. Claude-Lauf wegen Shell-Nutzung gestoppt, Codex setzte um.
- Wartbarkeitsplan Etappe 4, TEIL 6: drei Sample-Speicherwege als einen Schritt gekapselt; 5/5 Baseline- und 6/6 synthetische Nachtests, Release-Build 0 Fehler/0 Warnungen. Keine fachliche Änderung, nur lokaler Commit.

## 2026-09-27

- Wartbarkeitsplan Etappe 6, TEIL 1: Hauptquellenwahl im Import von Bilanzierung getrennt; IKAS, IBAK und KINS je vor/nach grün (3/3), Release-Build 0 Fehler/0 Warnungen. WinCan-Zweig nur per Diff geprüft, kein Push.
- Etappe 6, TEIL 2: WinCan-Hauptzweig synthetisch für Erfolg, Importfehler und Abbruch abgesichert; 4/4 ausgewählte Tests und Release-Build grün, 0 Warnungen. Kein Produktionscode oder Kundendatenlauf.
