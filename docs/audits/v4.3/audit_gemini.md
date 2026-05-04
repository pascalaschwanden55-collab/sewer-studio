# Audit Gemini — SewerStudio KI 4.2

**Datum:** 2026-05-04
**Quelle:** Gemini AI

---

SewerStudio ist funktional ein extrem beeindruckendes, tiefgreifendes Fachsystem (insbesondere für ein Solo-Projekt). Die Kombination aus lokaler Multi-Modell-KI (YOLO, DINO, SAM, Qwen) und fachspezifischer VSA-KEK-Normierung ist herausragend. Dennoch leidet das Projekt unter seinem eigenen Wachstum („Scope Creep") und architektonischen Altlasten, die die Stabilität gefährden.

## 1. Architektur

**Resultat:** Das Programm behauptet von sich, eine saubere "Clean Architecture" zu nutzen, bricht diese Regel aber in der Praxis massiv. Fast die gesamte KI- und Geschäftslogik liegt im UI-Projekt (WPF). Dateien wie das TrainingCenterViewModel.cs (fast 3000 Zeilen) oder die erwähnte PlayerWindow.xaml.cs (über 9400 Zeilen!) sind sogenannte "God-Classes". Sie machen alles gleichzeitig: Datenbankzugriffe, Benutzeroberfläche updaten, HTTP-Anfragen senden und KI-Modelle steuern. Zudem wird das Service-Locator-Anti-Pattern (App.Services) exzessiv genutzt, anstatt saubere Abhängigkeiten zu übergeben (Dependency Injection).

**Verbesserungsvorschläge:**
- **Klassen aufteilen:** Zerschneide riesige Dateien. Das TrainingCenterViewModel sollte nur noch UI-Klicks entgegennehmen und die schwere Arbeit an einen separaten BatchImportService in der Application-Schicht abgeben.
- **KI-Schicht auslagern:** Verschiebe alle Klassen unter AuswertungPro.Next.UI.Ai.* in ein eigenes Projekt (z.B. Infrastructure.Ai), damit die KI unabhängig von der Windows-Benutzeroberfläche getestet werden kann.
- **Dependency Injection nutzen:** Vermeide App.Services mitten im Code. Übergebe benötigte Services stattdessen im Konstruktor der ViewModels.

## 2. Fehler (Bugs & Sicherheit)

**Resultat:** Es gibt mehrere kritische Fehler, die das Programm im Hintergrund verlangsamen oder zum Absturz bringen können. Einige davon sind in den Audit-Logs bereits gut dokumentiert:

- **Speicher-Bombe (Out of Memory):** Beim YOLO-Export werden Tausende Bilder gleichzeitig in Base64-Text umgewandelt und in den RAM geladen. Das crasht das Programm bei großen Datenmengen.
- **Deadlocks (Aufhänger):** Das Programm blockiert sich manchmal selbst, z.B. wenn man beim Selbsttraining auf "Pause" und dann auf "Abbrechen" klickt, oder wenn FFmpeg-Prozesse nicht asynchron ausgelesen werden.
- **Sicherheitslücken:** Bei Dateiaufrufen (z.B. an FFmpeg oder Python) werden Dateipfade einfach als Text aneinandergehängt ($"\"{path}\""). Ein Dateiname mit speziellen Zeichen könnte hier das System manipulieren (Command Injection).

**Verbesserungsvorschläge:**
- **YOLO-Export reparieren:** Bilder nicht als Base64 im Arbeitsspeicher sammeln, sondern nacheinander lokal auf die Festplatte kopieren (wurde als Workaround teils schon gemacht, muss Standard werden).
- **Sichere Prozessaufrufe:** Ändere alle Process.Start Aufrufe so, dass Argumente sicher über ArgumentList.Add(path) übergeben werden.
- **Deadlocks beheben:** Das synchrone Warten in UI-Komponenten (z.B. beim Fenster-Schließen im WindowStateManager) in echte, asynchrone Aufgaben (async/await ohne UI-Blockade) umbauen.

## 3. Konsistenz

**Resultat:** Die Art und Weise, wie Daten zwischen Hintergrundaufgaben (z.B. KI-Erkennung) und der Benutzeroberfläche hin- und hergereicht werden, ist inkonsistent. Es gibt viele unsaubere Dispatcher.Invoke-Aufrufe, um Listen (wie ObservableCollection) abzugleichen. Zudem schluckt das System an vielen Stellen Fehler einfach heimlich weg (catch { } ohne Logging), wodurch Probleme unentdeckt bleiben.

**Verbesserungsvorschläge:**
- **Fehler sichtbar machen:** Entferne alle leeren catch-Blöcke. Jeder abgefangene Fehler muss zumindest ins Log geschrieben werden (z.B. _logger.LogWarning(ex, "...");), sonst ist die Fehlersuche unmöglich.
- **Thread-Sicherheit:** Nutze für das updaten von Listen aus dem Hintergrund Threads sichere Methoden, oder wechsle zu modernen MVVM-Toolkits, die das automatische Dispatching für Collections übernehmen.

## 4. Qualität von Datenbank, KI-Pipeline & Sanierungsvorschlägen

**Resultat:** Die Multi-Modell KI-Pipeline (YOLO, DINO, SAM, Qwen) ist beeindruckend konzipiert, aber die Trainingsdatenbasis hat Schieflagen. Fast 70% der Trainingssamples fallen laut Quality Gate ins Raster "Rot" (Unbrauchbar). Die SQLite-Datenbank (Wissensdatenbank) limitiert die Geschwindigkeit, da sie mit SemaphoreSlim globale Schreib-Locks erzwingt, was parallele KI-Analysen ausbremst. Die Sanierungsvorschläge sind gut durchdacht, verlassen sich aber stellenweise zu stark auf das freie "Nachdenken" der KI.

**Verbesserungsvorschläge:**
- **Datenbank-Flaschenhals lösen:** Anstatt dass jeder KI-Thread selbst in die SQLite-Datenbank schreibt (und wartet), sollten Ergebnisse in einen schnellen Kanal (System.Threading.Channels) gepusht werden. Ein einziger Hintergrund-Dienst schreibt diese dann nacheinander weg.
- **Trainingsdaten bereinigen:** Fokussiere dich auf Qualität statt Quantität. Das Quality-Gate ist extrem streng (z.B. fehlt oft der Frame-Pfad oder die BBox). Verbessere die automatische YOLO-Vorab-Filterung, damit weniger Schrott in der Qwen-Analyse landet.
- **Sanierungsmassnahmen festzurren:** Erweitere die "Hard Constraints" (harte Leitplanken), sodass die KI nur noch aus einer streng vorselektierten Liste von Sanierungsmaßnahmen (basierend auf Material und Durchmesser) wählen darf.

## 5. Verschlankung des Programms (Selten genutzte Funktionen)

**Resultat:** SewerStudio leidet unter "Feature Creep" (zu viele Funktionen). Das System will ein KI-Tool, ein GIS-System, eine Videobearbeitung, ein Kalkulationsprogramm und ein Hydraulik-Werkzeug gleichzeitig sein.

**Kandidaten zur Entfernung / Verschlankung:**
- **Hydraulikberechnung (DWA-A 110):** Wird für reine Inspektionsauswertungen praktisch nie direkt in der Inspektionssoftware gemacht, sondern in spezialisierten GIS- oder Ingenieur-Tools.
- **Eigendevis (Detaillierte Kostenvoranschläge):** Das Programm sollte grobe Sanierungskosten schätzen. Für centgenaue Leistungsverzeichnisse nach Gewerken gibt es Bauadministrations-Software (Messerli, Sorba etc.).
- **Komplexe Medien-Konfliktlösung:** Die manuelle Zuordnung fehlender Videos über 6 heuristische Abstufungen ist komplex. Ein einfacher Datei-Dialog "Ordner wählen" reicht oft aus.

**Verbesserungsvorschläge:**
- Entferne Hydraulik und Eigendevis aus dem Hauptcode. Falls dringend gewünscht, lagere sie als optionale, externe Plugins aus. Konzentriere 100% der Energie auf das Kernprodukt: Video rein -> Perfektes KI-Protokoll raus.

## 6. Optische Beurteilung des Layouts & Aufbaus

**Resultat:** Das Layout (sichtbar in SettingsPage.xaml) ist funktional, wirkt aber eher wie ein technisches Handbuch als eine moderne Software. Riesige Textblöcke ("Programm-Anleitung" mitten in den Einstellungen), viele Checkboxen und verschachtelte Textboxen überladen das Auge. Das Custom-Styling (NeonCyanBrush, manuelle ToggleButtons) ist mutig, weicht aber oft von modernen Standards ab.

**Verbesserungsvorschläge:**
- **Texte verbannen:** Die komplette Sektion "Programm-Anleitung — 20 Sektionen" hat in den Systemeinstellungen nichts verloren. Lagere dies in eine separate "Hilfe"-Seite oder in eine PDF/Web-Dokumentation aus.
- **Modernes Design-System nutzen:** Nutze eine fertige UI-Bibliothek wie MaterialDesignInXAML oder FluentWPF. Das sorgt automatisch für einheitliche Abstände, professionelle Schattenwürfe und saubere Schalter (Toggles), ohne dass du sie selbst programmieren musst.
- **Luft und Struktur ("White Space"):** Gruppiere Einstellungen in Karten (Cards) mit viel Randabstand, anstatt alles in einer endlosen Scroll-Liste untereinander zu klatschen.

## Zusammenfassung auf einen Blick

SewerStudio ist ein technologisches Meisterwerk für eine Einzelperson, das aber dringend aufgeräumt werden muss, bevor neue Features eingebaut werden.

1. **Räume den Code auf:** Spalte die über 9000-Zeilen Monster-Dateien auf und ziehe die KI aus der Benutzeroberfläche raus.
2. **Behebe die Blockaden:** Asynchrone Abläufe reparieren und SQLite-Schreibzugriffe kanalisieren.
3. **Schlanker machen:** Lösche Hydraulik, Eigendevis und Anleitungen aus der App heraus.
4. **Professionelles UI:** Tausche Textwüsten gegen eine saubere Karteikarten-Optik mit einer etablierten Design-Bibliothek.
