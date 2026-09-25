# Ferienarbeit: Aufgaben

Start-Commit: `630a11c61f4e54d5e1e3c594cf0f1cf893f8ebe8` (`baseline-10tage-20260925`).
Aktueller Arbeitsordner: `C:\Sewer-Studio_KI_5.0`, Branch `feature/webgis-uebertragung` (Wunsch des Nutzers vom 25.09.). Die 206 bereits offenen Dateien bleiben unangetastet; neue Änderungen werden einzeln zugeordnet und geprüft. Die frühere getrennte Arbeitskopie bleibt als Rückfallstand liegen.
Während der Ferien eigenständig innerhalb der vereinbarten Grenzen weiterarbeiten. Große Umbauten, Käufe und Push bleiben ausgeschlossen. Die Ferienläufe bleiben pausiert, bis ihre technischen Startbedingungen nachweislich erfüllt sind.

## Startbedingungen

- Bildsatz zweimal gemessen. Vier passende Videos aus `D:\Haltungen` sind lokal festgelegt und technisch zweimal geprüft. EN-Codes und QualityGate am Video müssen noch als Ausgangswerte gemessen werden; die Zeitgrenze muss die beobachtete Startschwankung berücksichtigen.
- Vor unbeaufsichtigten Läufen: Zugriffe technisch begrenzen, Sperre gegen Überlappung einrichten und Fehlerfälle testen.
- Zwei tägliche Läufe sind in der Codex-App vorbereitet, aber pausiert. `scripts/ferien-lock.ps1` verweigert Starts ohne vollständige lokale `tmp/ferien/READY.json`; diese Freigabedatei existiert noch nicht. Sie darf erst nach nachgewiesener Zugriffstrennung, fachlicher Messung und allen vier Probelauf-Fällen mit Ablaufdatum und Prüfsumme des Referenzsatzes erstellt werden.
- Kein Push, kein Merge nach `main`, keine Käufe, keine Kundendaten an Cloud-Modelle.
- Qwen während KI-Messungen und Videotests entladen.

## Reihenfolge

1. **Sicherungslücken klären (Daten) – Bericht erstellt:** Die 128 fehlenden Verknüpfungen aus dem Lauf vom 25.09. sind nach Laufwerk und Dateityp in `docs/reviews/2026-09-25-sicherungsluecken.md` geordnet. Erfolg: Wiederherstellungsrisiko dokumentiert, ohne Originale zu ändern. Ältere Kopien bleiben ungeprüft.
2. **SQLite-Sicherung und Import (Daten) – Schutztests ergänzt:** Der Schnappschuss lässt sich bei offenem Pool sichern und wieder öffnen. Ein eigener synthetischer Importtest belegt: Bei noch offener Verbindung bleibt der alte Datenbankstand samt Inhaltsprüfung erhalten; nach dem Schließen gelingt der Import. Ein offener Import wird derzeit sichtbar abgewiesen, nicht während der Nutzung erzwungen.
3. **Codierung und QualityGate (Fachlichkeit):** Einen nachweisbaren Schwachpunkt mit dem Bild-Referenzsatz auswählen. Erfolg: Codes und QualityGate-Entscheidungen bleiben im zweimal gemessenen Streubereich oder verbessern sich.
4. **Sidecar-Ausfälle (Absturzschutz):** Nichterreichbarkeit, Zeitüberschreitung und Wiederanlauf prüfen. Erfolg: keine stillen Erfolge oder Abstürze; betroffene Tests grün.
5. **VRAM-Mangel (Absturzschutz):** Bestehende Rückfallregeln und Tests prüfen. Erfolg: Speichermangel wird sichtbar gemeldet und beendet keine Sicherung oder Codiersitzung unkontrolliert.
6. **QGIS-Live-Position (Bedienung) – Schnittstelle geschützt:** Der Endpunkt existiert bereits. Zwei neue Routertests prüfen mit künstlichen Daten den Meterwert samt Quelle und 404 ohne belastbaren Meterwert. Die 14 Python-Brückentests sind grün. Eine sichtbare Prüfung im echten QGIS-Plugin bleibt offen.
7. **Excel-Export (Daten) – Beispiel-LV geschützt:** Vorlagen- und Datenübertragungstests waren bereits vorhanden. Ein neuer synthetischer NPK-Excel-Test prüft beide Reiter, Positionen, Preisfelder, Formate, Zwischen- und Gesamtsummen sowie den Ausschluss der separat ausgewiesenen Pauschale. Eine Sichtprüfung in Excel bleibt offen.
8. **Wartbarkeit – erste Warnungen bereinigt:** Die Null-Warnung im Fototest und die vier Tupel-Warnungen der Projektprüfung sind durch explizite Prüfung bzw. Namen beseitigt. Weitere Strukturarbeit folgt nur an konkreten, abgesicherten Stellen.

## Fertig-Regel

Betroffener Test und vollständiger Release-Build grün; keine neue Warnung; Referenzvergleich innerhalb der festgelegten Grenzen; Änderung und Messwert im JOURNAL. Bei zwei gescheiterten Versuchen: eigene Änderung zurücknehmen, Grund notieren, nächste Aufgabe. Bereits vorher rote Tests getrennt erfassen. Große oder riskante Entscheidungen kommen auf eine Liste für nach den Ferien.
