# Ferienarbeit: Aufgaben

Start-Commit: `630a11c61f4e54d5e1e3c594cf0f1cf893f8ebe8` (`baseline-10tage-20260925`).
Aktueller Arbeitsordner: `C:\Sewer-Studio_KI_5.0`, Branch `feature/webgis-uebertragung` (Wunsch des Nutzers vom 25.09.). Die 206 bereits offenen Dateien bleiben unangetastet; neue Änderungen werden einzeln zugeordnet und geprüft. Die frühere getrennte Arbeitskopie bleibt als Rückfallstand liegen.
Während der Ferien eigenständig innerhalb der vereinbarten Grenzen weiterarbeiten. Große Umbauten, Käufe und Push bleiben ausgeschlossen. Seit dem 25.09. sind zwei tägliche, auf Quellcode begrenzte Ferienläufe aktiv.

## Startbedingungen

- Bildsatz zweimal gemessen. Vier passende Videos aus `D:\Haltungen` sind lokal festgelegt und technisch zweimal geprüft. EN-Codes und QualityGate am Video müssen noch als Ausgangswerte gemessen werden; die Zeitgrenze muss die beobachtete Startschwankung berücksichtigen.
- Die zweimal täglichen Läufe arbeiten bis 05.10. nur an Quellcode und künstlichen Tests. `scripts/ferien-lock.ps1` verhindert Überlappung, falschen Branch und den Start ohne befristete lokale `tmp/ferien/READY.json`. Eine lokale Liste schützt die 206 bereits offenen Pfade vor Änderungen durch die Läufe.
- Die technische Lesesperre für andere Laufwerke ist auf diesem Windows-PC weiterhin **nicht nachgewiesen**. Deshalb dürfen diese Läufe keine Medien, Kundendaten oder Laufwerke ausserhalb des Projektordners lesen. Fachliche Video- und GPU-Messungen sowie der vollständige unbeaufsichtigte Neustart-Probelauf bleiben gesperrt. Die Freigabedatei bestätigt ausdrücklich nur den begrenzten Code-Modus.
- Der normale Git-Push über `origin` ist lokal für die Ferien gesperrt; die frühere Push-Adresse liegt nur lokal in `tmp/ferien` zur späteren Wiederherstellung.
- Kein Push, kein Merge nach `main`, keine Käufe, keine Kundendaten an Cloud-Modelle.
- Qwen während KI-Messungen und Videotests entladen.

## Kleine Code-Aufgaben für die aktiven Ferienläufe

1. NPK-Excel – erledigt: variable Preise und ausgeschlossene Pauschalen mit künstlichen Beispielen geprüft. Die Firmenansicht zeigt Menge und Einheit, aber keinen internen Einheitspreis oder Schätzwert im Total; die interne Ansicht zeigt den aggregierten Betrag. Die separat ausgewiesene Pauschale wird nicht zum Gesamtbetrag addiert.
2. Excel-Vorlagenexport: vorhandene Abbruch- und Dateisicherheitsprüfungen lesen, eine konkrete Lücke mit synthetischem Test schliessen. Erfolg: keine beschädigte Ausgabedatei nach Fehler oder Abbruch.
3. Projektprüfung: eine ungeschützte Fachregel mit künstlichen Haltungs- und Schachtdaten prüfen. Erfolg: verständliche Meldung ohne Änderung gespeicherter Daten.
4. QGIS-Brücke: die vorhandenen rein künstlichen Schnittstellentests um einen belegten Fehlerfall ergänzen. Erfolg: ungültige oder veraltete Position wird nicht als gültig ausgegeben.
5. Wartbarkeit: eine kleine doppelte Logik ausserhalb der 206 geschützten Dateien entfernen. Erfolg: gleicher öffentlicher Vertrag, betroffene Tests und Release-Build grün.

Vor jeder Änderung `scripts/ferien-lock.ps1 -Action CheckPath -CandidatePath <relativer Pfad>` ausführen. Eine gesperrte Datei oder Aufgabe überspringen und die nächste wählen.

## Reihenfolge

1. **Sicherungslücken klären (Daten) – Bericht erstellt:** Die 128 fehlenden Verknüpfungen aus dem Lauf vom 25.09. sind nach Laufwerk und Dateityp in `docs/reviews/2026-09-25-sicherungsluecken.md` geordnet. Erfolg: Wiederherstellungsrisiko dokumentiert, ohne Originale zu ändern. Ältere Kopien bleiben ungeprüft.
2. **SQLite-Sicherung und Import (Daten) – Schutztests ergänzt:** Der Schnappschuss lässt sich bei offenem Pool sichern und wieder öffnen. Ein eigener synthetischer Importtest belegt: Bei noch offener Verbindung bleibt der alte Datenbankstand samt Inhaltsprüfung erhalten; nach dem Schließen gelingt der Import. Ein offener Import wird derzeit sichtbar abgewiesen, nicht während der Nutzung erzwungen.
3. **Codierung und QualityGate (Fachlichkeit):** Einen nachweisbaren Schwachpunkt mit dem Bild-Referenzsatz auswählen. Erfolg: Codes und QualityGate-Entscheidungen bleiben im zweimal gemessenen Streubereich oder verbessern sich.
4. **Sidecar-Ausfälle (Absturzschutz):** Nichterreichbarkeit, Zeitüberschreitung und Wiederanlauf prüfen. Erfolg: keine stillen Erfolge oder Abstürze; betroffene Tests grün.
5. **VRAM-Mangel (Absturzschutz):** Bestehende Rückfallregeln und Tests prüfen. Erfolg: Speichermangel wird sichtbar gemeldet und beendet keine Sicherung oder Codiersitzung unkontrolliert.
6. **QGIS-Live-Position (Bedienung) – Schnittstelle geschützt:** Der Endpunkt existiert bereits. Zwei neue Routertests prüfen mit künstlichen Daten den Meterwert samt Quelle und 404 ohne belastbaren Meterwert. Die 14 Python-Brückentests sind grün. Eine sichtbare Prüfung im echten QGIS-Plugin bleibt offen.
7. **Excel-Export (Daten) – Beispiel-LV geschützt:** Vorlagen- und Datenübertragungstests waren bereits vorhanden. Synthetische NPK-Excel-Tests prüfen beide Reiter, Positionen, Preisfelder, Formate, Zwischen- und Gesamtsummen, den Ausschluss der separat ausgewiesenen Pauschale sowie leere Positionslisten. Eine Sichtprüfung in Excel bleibt offen.
8. **Wartbarkeit – erste Warnungen bereinigt:** Die Null-Warnung im Fototest und die vier Tupel-Warnungen der Projektprüfung sind durch explizite Prüfung bzw. Namen beseitigt. Weitere Strukturarbeit folgt nur an konkreten, abgesicherten Stellen.

## Fertig-Regel

Betroffener Test und vollständiger Release-Build grün; keine neue Warnung; Referenzvergleich innerhalb der festgelegten Grenzen; Änderung und Messwert im JOURNAL. Bei zwei gescheiterten Versuchen: eigene Änderung zurücknehmen, Grund notieren, nächste Aufgabe. Bereits vorher rote Tests getrennt erfassen. Große oder riskante Entscheidungen kommen auf eine Liste für nach den Ferien.
