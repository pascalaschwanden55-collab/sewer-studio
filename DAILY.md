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
