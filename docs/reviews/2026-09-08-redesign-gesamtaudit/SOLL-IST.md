# Soll-Ist-Abgleich des Redesigns

Stand: 08.09.2026, nachgeholte Morgenprüfung. Dies ist die Nachweisliste für die WPF-Anwendung. Die historischen grünen Browser-Prüfungen werden nicht als aktuelle WPF-Ergebnisse übernommen.

**Teilweise** bedeutet: Ein Teilnachweis liegt vor, die ganze Anforderung ist nicht abgenommen. **Offen** bedeutet: kein ausreichender aktueller Gesamtbeleg. **Bestanden** wird nur für den konkret gemessenen Umfang verwendet.

Genehmigte Abweichungen und die acht aktuellen Befunde sind im [Prüfbericht](PRUEFBERICHT.md) erläutert. Full HD ist die Mindestauflösung; echte 125/150-Prozent-Prüfungen bleiben offen.

## Nummerierte Prüfpunkte der freigegebenen Vorschau

Alle 82 nummerierten Zeilen aus [PRUEFTABELLE.md](../2026-09-06-nova/optimiert/v2/PRUEFTABELLE.md) sind übernommen. Quelle und ursprünglicher Prüfpunkt bleiben zugeordnet; bewertet wird hier der heutige WPF-Nachweis.

| Punkt | Anforderung | Aktueller Stand | Nachweis / Grenze |
|---|---|---|---|
| A1 (Quelle Z. 12) | Dokument: DOCTYPE, UTF-8, lang=de, viewport | Nur Prototyp | HTML-Dokumentmerkmale; für die WPF-App nicht direkt anwendbar. |
| A2 (Quelle Z. 13) | Keine externen Schriften oder Skripte | Teilweise | Systemschriften im Programm und in den Tabellenbildern; kein vollständiger Netzwerkmitschnitt. |
| A3 (Quelle Z. 14) | Umlaute im lokalen Aufruf lesbar | Teilweise | Umlaute in aktuellen Bildern und DesignAudit-Laufzeittexttests; keine Prüfung jedes Texts. |
| A4 (Quelle Z. 15) | Alle 15 Seiten über die Navigation erreichbar | Teilweise | 28/30 Aufbauten in seitenprobe.json; Medienkonflikte zusätzlich mit echtem App-BAML erfolgreich. |
| A5 (Quelle Z. 16) | Drei Fenster öffnen: Player, Training Studio, VSA-Codierung | Offen | Training Studio aufgebaut; heutiger Gesamt-Klickweg durch alle drei Fenster nicht nachgewiesen. |
| A6 (Quelle Z. 17) | Keine Skriptfehler beim Durchklicken | Teilweise | Seitenprobe und echte Ressourcenprobe; gesonderter WPF-Testfehler bleibt offen. |
| B1 (Quelle Z. 23) | Wechsel auf 07.6588-6587: Formular, Übersicht und Medien folgen | Teilweise | Speicher-/Auswahlregeln durch vorhandene .NET-Tests mitgeprüft; keine vollständige aktuelle Bedienfolge aller Varianten. R1/R2 sperren Projektwechsel-Abnahme. |
| B2 (Quelle Z. 24) | Zurück auf 78998-79002: Beton, 300, 42.30 | Teilweise | Speicher-/Auswahlregeln durch vorhandene .NET-Tests mitgeprüft; keine vollständige aktuelle Bedienfolge aller Varianten. R1/R2 sperren Projektwechsel-Abnahme. |
| B3 (Quelle Z. 25) | Ungespeicherte Änderung sichtbar (Badge, Zeilenmarke, Speichern aktiv) | Teilweise | Speicher-/Auswahlregeln durch vorhandene .NET-Tests mitgeprüft; keine vollständige aktuelle Bedienfolge aller Varianten. R1/R2 sperren Projektwechsel-Abnahme. |
| B4 (Quelle Z. 26) | Wechsel bei Änderungen fragt nach; Abbrechen bleibt auf der Haltung | Teilweise | Speicher-/Auswahlregeln durch vorhandene .NET-Tests mitgeprüft; keine vollständige aktuelle Bedienfolge aller Varianten. R1/R2 sperren Projektwechsel-Abnahme. |
| B5 (Quelle Z. 27) | Verwerfen und wechseln: Ziel gewählt, Ursprung unverändert | Teilweise | Speicher-/Auswahlregeln durch vorhandene .NET-Tests mitgeprüft; keine vollständige aktuelle Bedienfolge aller Varianten. R1/R2 sperren Projektwechsel-Abnahme. |
| B6 (Quelle Z. 28) | Speichern und wechseln: Wert im Bestand, Ziel gewählt | Teilweise | Speicher-/Auswahlregeln durch vorhandene .NET-Tests mitgeprüft; keine vollständige aktuelle Bedienfolge aller Varianten. R1/R2 sperren Projektwechsel-Abnahme. |
| B7 (Quelle Z. 29) | Gespeicherter Wert überlebt Neuladen (Browserspeicher, kein Projektschreiben) | Teilweise | Speicher-/Auswahlregeln durch vorhandene .NET-Tests mitgeprüft; keine vollständige aktuelle Bedienfolge aller Varianten. R1/R2 sperren Projektwechsel-Abnahme. |
| B8 (Quelle Z. 30) | Verwerfen ohne Wechsel setzt Feld zurück | Teilweise | Speicher-/Auswahlregeln durch vorhandene .NET-Tests mitgeprüft; keine vollständige aktuelle Bedienfolge aller Varianten. R1/R2 sperren Projektwechsel-Abnahme. |
| C1 (Quelle Z. 36) | Feldsuche „Baujahr" bei Haltungen lässt Schachtfelder unberührt | Teilweise | Getrennte Ansichten und Feldbereiche vorhanden, aktuelle Tests und Tabellenbilder. Die fünf vollständigen Bedienfolgen nicht einzeln protokolliert. |
| C2 (Quelle Z. 37) | Ansichtswechsel bei Haltungen ändert Schachtansicht und Schachtauswahl nicht | Teilweise | Getrennte Ansichten und Feldbereiche vorhanden, aktuelle Tests und Tabellenbilder. Die fünf vollständigen Bedienfolgen nicht einzeln protokolliert. |
| C3 (Quelle Z. 38) | „Alle zu" wirkt nur auf die eigene Seite | Teilweise | Getrennte Ansichten und Feldbereiche vorhanden, aktuelle Tests und Tabellenbilder. Die fünf vollständigen Bedienfolgen nicht einzeln protokolliert. |
| C4 (Quelle Z. 39) | Schachtsuche arbeitet eigenständig | Teilweise | Getrennte Ansichten und Feldbereiche vorhanden, aktuelle Tests und Tabellenbilder. Die fünf vollständigen Bedienfolgen nicht einzeln protokolliert. |
| C5 (Quelle Z. 40) | Feldsuche bei Schächten lässt Haltungsfelder unberührt | Teilweise | Getrennte Ansichten und Feldbereiche vorhanden, aktuelle Tests und Tabellenbilder. Die fünf vollständigen Bedienfolgen nicht einzeln protokolliert. |
| D1 (Quelle Z. 46) | Dringend = Z0 + Z1 | Teilweise | Kennzahlenregeln in aktuellen Infrastructure-/UI-Tests; R2 zeigt veraltete Kennzahlen nach Projektwechsel. |
| D2 (Quelle Z. 47) | Legende summiert auf die Gesamtzahl | Teilweise | Kennzahlenregeln in aktuellen Infrastructure-/UI-Tests; R2 zeigt veraltete Kennzahlen nach Projektwechsel. |
| D3 (Quelle Z. 48) | Prozent mit Zähler und Nenner, korrekt gerundet | Teilweise | Kennzahlenregeln in aktuellen Infrastructure-/UI-Tests; R2 zeigt veraltete Kennzahlen nach Projektwechsel. |
| D4 (Quelle Z. 49) | Kosten-KPI = Summe Haltungen + Schächte | Teilweise | Kennzahlenregeln in aktuellen Infrastructure-/UI-Tests; R2 zeigt veraltete Kennzahlen nach Projektwechsel. |
| D5 (Quelle Z. 50) | Text unterscheidet fachlich geprüft, KI analysiert und ohne Analyse | Teilweise | Kennzahlenregeln in aktuellen Infrastructure-/UI-Tests; R2 zeigt veraltete Kennzahlen nach Projektwechsel. |
| E1 (Quelle Z. 56) | „Nächste Haltung prüfen" wählt eine offene Haltung und öffnet den Player | Teilweise | Player-/Codier-/Aufgabenregeln in aktuellen Tests; vollständiger Fenster- und Medienablauf heute offen. |
| E2 (Quelle Z. 57) | Player: Dialogrolle, aria-modal, Name, Fokus im Dialog | Teilweise | Player-/Codier-/Aufgabenregeln in aktuellen Tests; vollständiger Fenster- und Medienablauf heute offen. |
| E3 (Quelle Z. 58) | Ereignis erfassen → Abbrechen: kehrt zum Player zurück, nichts übernommen, Position gleich | Teilweise | Player-/Codier-/Aufgabenregeln in aktuellen Tests; vollständiger Fenster- und Medienablauf heute offen. |
| E4 (Quelle Z. 59) | Ereignis erfassen → Übernehmen: Befund im Player-Entwurf, Bestand noch unverändert, Player offen, Haltung und Position erhalten | Teilweise | Player-/Codier-/Aufgabenregeln in aktuellen Tests; vollständiger Fenster- und Medienablauf heute offen. |
| E5 (Quelle Z. 60) | Esc bei Entwurf fragt nach; Verwerfen schliesst den Player; Fokus kehrt zum Auslöser oder zur gewählten Zeile | Teilweise | Player-/Codier-/Aufgabenregeln in aktuellen Tests; vollständiger Fenster- und Medienablauf heute offen. |
| E6 (Quelle Z. 61) | Training Studio → Katalog → Übernehmen: Code im Studio, Studio bleibt offen | Teilweise | Player-/Codier-/Aufgabenregeln in aktuellen Tests; vollständiger Fenster- und Medienablauf heute offen. |
| E7 (Quelle Z. 62) | Studio: Vorschlag, Bestätigung und Trainingsfreigabe sind getrennte Schritte | Genehmigt angepasst / teilweise | Freigabe über das Export-Register; Akzeptieren und Exportregeln durch Bestandstests, keine neue gesamte Freigabefolge. |
| F1 (Quelle Z. 68) | „Ruhig" stoppt die Hintergrund-Engine (Canvas unverändert, Schleife aus) | Offen | Bewegungsregeln und Einstellungen im Bestand; sofortiger Stopp bereits laufender Animationen bleibt laut Abnahme eine Grenze. |
| F2 (Quelle Z. 69) | Wahl „ruhig" bleibt nach Neuladen erhalten | Offen | Bewegungsregeln und Einstellungen im Bestand; sofortiger Stopp bereits laufender Animationen bleibt laut Abnahme eine Grenze. |
| F3 (Quelle Z. 70) | Engine pausiert, solange ein Fenster die Ansicht verdeckt | Offen | Bewegungsregeln und Einstellungen im Bestand; sofortiger Stopp bereits laufender Animationen bleibt laut Abnahme eine Grenze. |
| F4 (Quelle Z. 71) | Systemeinstellung „Bewegung reduzieren" stoppt Engine und Pulsanimation auch bei „bewegt" | Offen | Bewegungsregeln und Einstellungen im Bestand; sofortiger Stopp bereits laufender Animationen bleibt laut Abnahme eine Grenze. |
| G1 (Quelle Z. 77) | Enter auf Navigationseintrag Export öffnet die Exportseite | Teilweise | Aktuelle Tastatur-/Bindungstests und sichtbare Bedienelemente; keine vollständige manuelle Fokus- und Screenreader-Prüfung. |
| G2 (Quelle Z. 78) | Ctrl+K, Suchtext, Enter: springt zur gefundenen Haltung | Fehler | R1: Suche wählt nach Projektwechsel ein projektfremdes Objekt; R5: Trefferarten werden verdrängt. |
| G3 (Quelle Z. 79) | F3 fokussiert die Haltungssuche | Teilweise | Aktuelle Tastatur-/Bindungstests und sichtbare Bedienelemente; keine vollständige manuelle Fokus- und Screenreader-Prüfung. |
| G4 (Quelle Z. 80) | Pfeiltasten und Enter wählen Zeilen in der Liste | Teilweise | Aktuelle Tastatur-/Bindungstests und sichtbare Bedienelemente; keine vollständige manuelle Fokus- und Screenreader-Prüfung. |
| G5 (Quelle Z. 81) | F1 öffnet Tastenkürzel, Fokus bleibt im Dialog, Esc schliesst | Teilweise | Aktuelle Tastatur-/Bindungstests und sichtbare Bedienelemente; keine vollständige manuelle Fokus- und Screenreader-Prüfung. |
| G6 (Quelle Z. 82) | Jede Schaltfläche hat Text, aria-label oder Titel | Teilweise | Aktuelle Tastatur-/Bindungstests und sichtbare Bedienelemente; keine vollständige manuelle Fokus- und Screenreader-Prüfung. |
| G7 (Quelle Z. 83) | Sichtbarer Tastaturfokus definiert | Teilweise | Aktuelle Tastatur-/Bindungstests und sichtbare Bedienelemente; keine vollständige manuelle Fokus- und Screenreader-Prüfung. |
| H-1366 (Quelle Z. 89) | 1366×768: mindestens sechs Haltungen sichtbar, Eingabefelder und Übersicht im Bild | Zusatzprüfung offen | Verbindliche Mindestauflösung ist Full HD. Kleinere Fensterprobe 1280×720 ist kein Nachweis für diese Grösse oder echte DPI-Skalierung. |
| HP-1366 (Quelle Z. 90) | 1366×768: Player zeigt Wiedergabe, Zeit, Übernehmen und Seitenspalte ohne Scrollen | Offen | Aktueller Player nicht für diese gesamte Kombination von Grösse und Bedienzuständen vermessen. |
| HS-1366 (Quelle Z. 91) | 1366×768: Training Studio: Codier- und Freigabespalte vollständig erreichbar | Offen | Diese Zusatzgrösse nicht erneut gemessen; bereits bei Full HD besteht R3. |
| H-1440 (Quelle Z. 92) | 1440×900: mindestens sechs Haltungen sichtbar, Eingabefelder und Übersicht im Bild | Zusatzprüfung offen | Verbindliche Mindestauflösung ist Full HD. Kleinere Fensterprobe 1280×720 ist kein Nachweis für diese Grösse oder echte DPI-Skalierung. |
| HP-1440 (Quelle Z. 93) | 1440×900: Player zeigt Wiedergabe, Zeit, Übernehmen und Seitenspalte ohne Scrollen | Offen | Aktueller Player nicht für diese gesamte Kombination von Grösse und Bedienzuständen vermessen. |
| HS-1440 (Quelle Z. 94) | 1440×900: Training Studio: Codier- und Freigabespalte vollständig erreichbar | Offen | Diese Zusatzgrösse nicht erneut gemessen; bereits bei Full HD besteht R3. |
| H-1920 (Quelle Z. 95) | 1920×1080: mindestens sechs Haltungen sichtbar, Eingabefelder und Übersicht im Bild | Bestanden | Neue Tabellenmessung: Full HD, DPI 96, offene Feldschublade, zwölf ganze Haltungszeilen (hell). |
| HP-1920 (Quelle Z. 96) | 1920×1080: Player zeigt Wiedergabe, Zeit, Übernehmen und Seitenspalte ohne Scrollen | Offen | Aktueller Player nicht für diese gesamte Kombination von Grösse und Bedienzuständen vermessen. |
| HS-1920 (Quelle Z. 97) | 1920×1080: Training Studio: Codier- und Freigabespalte vollständig erreichbar | Fehler | R3: Stufen 4/5 im Training Studio bei Full HD nicht vollständig sichtbar. |
| I1-hell (Quelle Z. 103) | Schriftgrösse hell: kleinster sichtbarer Text ≥ 11 px auf 15 Seiten und 3 Fenstern | Teilweise | DesignAudit-Prüfungen und ausgewählte aktuelle Bilder; kein lückenloser Messlauf über alle sichtbaren WPF-Texte und Zustände. |
| I2-hell (Quelle Z. 104) | Kontrast hell: normaler Text ≥ 4,5:1 auf 15 Seiten, 3 Fenstern, Eingaben und Platzhaltern (deckende Hintergründe) | Teilweise | DesignAudit-Prüfungen und ausgewählte aktuelle Bilder; kein lückenloser Messlauf über alle sichtbaren WPF-Texte und Zustände. |
| I3-hell (Quelle Z. 105) | Kontrast hell: Projektziel 4,5:1 auch für grossen Text (WCAG erlaubt dort 3:1) | Teilweise | DesignAudit-Prüfungen und ausgewählte aktuelle Bilder; kein lückenloser Messlauf über alle sichtbaren WPF-Texte und Zustände. |
| I4-hell (Quelle Z. 106) | Kontrast hell: Texte auf Verläufen gesondert gelistet (nicht als Verstoss gewertet) | Offen | WPF-Bilder ersetzen transparente Mica-Flächen für die Aufnahme; kein kompletter Kontrastnachweis des Windows-Compositors. |
| I1-dunkel (Quelle Z. 107) | Schriftgrösse dunkel: kleinster sichtbarer Text ≥ 11 px auf 15 Seiten und 3 Fenstern | Teilweise | DesignAudit-Prüfungen und ausgewählte aktuelle Bilder; kein lückenloser Messlauf über alle sichtbaren WPF-Texte und Zustände. |
| I2-dunkel (Quelle Z. 108) | Kontrast dunkel: normaler Text ≥ 4,5:1 auf 15 Seiten, 3 Fenstern, Eingaben und Platzhaltern (deckende Hintergründe) | Teilweise | DesignAudit-Prüfungen und ausgewählte aktuelle Bilder; kein lückenloser Messlauf über alle sichtbaren WPF-Texte und Zustände. |
| I3-dunkel (Quelle Z. 109) | Kontrast dunkel: Projektziel 4,5:1 auch für grossen Text (WCAG erlaubt dort 3:1) | Teilweise | DesignAudit-Prüfungen und ausgewählte aktuelle Bilder; kein lückenloser Messlauf über alle sichtbaren WPF-Texte und Zustände. |
| I4-dunkel (Quelle Z. 110) | Kontrast dunkel: Texte auf Verläufen gesondert gelistet (nicht als Verstoss gewertet) | Offen | WPF-Bilder ersetzen transparente Mica-Flächen für die Aufnahme; kein kompletter Kontrastnachweis des Windows-Compositors. |
| J1 (Quelle Z. 116) | Export nennt Ziel und Ergebnis und sagt, dass keine Datei geschrieben wurde | Prototyp-Regel anpassen | Demo-Kennzeichnung, keine Datei schreiben und JS-Fehlerfreiheit beschreiben die Vorschau. Echte App-Aktionen brauchen eigene Erfolgs-/Fehlernachweise. |
| J2 (Quelle Z. 117) | Revidierte XTF wird vom GEONIS-Rückabgleich unterschieden | Teilweise | XTF-Auswahl/Export durch aktuelle Tests; laufender XTF-Arbeitsstand, keine externe GEONIS-Gesamtprüfung. |
| J3 (Quelle Z. 118) | Nicht umgesetzte Aktionen sind gekennzeichnet und erklären sich | Prototyp-Regel anpassen | Demo-Kennzeichnung, keine Datei schreiben und JS-Fehlerfreiheit beschreiben die Vorschau. Echte App-Aktionen brauchen eigene Erfolgs-/Fehlernachweise. |
| J4 (Quelle Z. 119) | Leerzustand erscheint, wenn ein Filter keine Fälle hat | Teilweise | Seiten/Leerzustände und vorhandene Regeln geprüft; vollständiger jeweiliger Bedienlauf nicht protokolliert. |
| J5 (Quelle Z. 120) | Lade- und Erfolgszustand (Schattenlauf) sichtbar und als Simulation benannt | Teilweise | Seiten/Leerzustände und vorhandene Regeln geprüft; vollständiger jeweiliger Bedienlauf nicht protokolliert. |
| J6 (Quelle Z. 121) | KI-Prozentwerte werden als Modellsicherheit erklärt; Bestätigung getrennt | Teilweise | Erklärungen im aktuellen Training-Studio-Bild sichtbar; keine neue fachliche Modellabnahme. |
| J7 (Quelle Z. 122) | Skriptfehler im gesamten Lauf | Prototyp-Regel anpassen | Demo-Kennzeichnung, keine Datei schreiben und JS-Fehlerfreiheit beschreiben die Vorschau. Echte App-Aktionen brauchen eigene Erfolgs-/Fehlernachweise. |
| K1 (Quelle Z. 128) | R01 Abbrechen: Rückfrage ohne Player, Auswahl und Entwurf bleiben auf 07.6588-6587 | Teilweise | Speichern, Codierung und Dialogregeln durch aktuelle Bestandstests; diese konkrete zusammenhängende Bedienfolge nicht vollständig nachgewiesen. |
| K2 (Quelle Z. 129) | R01 Verwerfen und wechseln: Auswahl, Formular und Player gemeinsam auf 78998-79002, Ursprung unverändert | Teilweise | Speichern, Codierung und Dialogregeln durch aktuelle Bestandstests; diese konkrete zusammenhängende Bedienfolge nicht vollständig nachgewiesen. |
| K3 (Quelle Z. 130) | R01 KI-Aufgabenliste: Rückfrage vor dem Player; Speichern und wechseln übernimmt Wert und öffnet den richtigen Player | Teilweise | Speichern, Codierung und Dialogregeln durch aktuelle Bestandstests; diese konkrete zusammenhängende Bedienfolge nicht vollständig nachgewiesen. |
| K4 (Quelle Z. 131) | R06 Beenden ohne Übernahme verwirft neuen, bestätigten, gelöschten Befund und Prüfstatus; Bestand unverändert; beim Wiederöffnen weg | Teilweise | Speichern, Codierung und Dialogregeln durch aktuelle Bestandstests; diese konkrete zusammenhängende Bedienfolge nicht vollständig nachgewiesen. |
| K5 (Quelle Z. 132) | R06 Codierung übernehmen schreibt den Entwurf in den Bestand und die Übersicht | Teilweise | Speichern, Codierung und Dialogregeln durch aktuelle Bestandstests; diese konkrete zusammenhängende Bedienfolge nicht vollständig nachgewiesen. |
| K6 (Quelle Z. 133) | R06 Esc oder Schliessen bei Entwurf fragt nach; Zurück zum Player behält Entwurf und Fenster | Teilweise | Speichern, Codierung und Dialogregeln durch aktuelle Bestandstests; diese konkrete zusammenhängende Bedienfolge nicht vollständig nachgewiesen. |
| K7 (Quelle Z. 134) | R02 Akzeptieren, dann Felder leeren: Freigabe gesperrt; erzwungener Klick wird abgelehnt | Genehmigt angepasst / teilweise | WPF nutzt das bestehende Export-Register. Änderungs-/Freigaberegeln in Bestandstests, komplette neue Bedienprobe offen. |
| K8 (Quelle Z. 135) | R02 Jede Änderung (Stufe, Bildauswahl) hebt die Bestätigung auf; nach erneutem Akzeptieren ist die Freigabe möglich und wird danach wieder gesperrt | Genehmigt angepasst / teilweise | WPF nutzt das bestehende Export-Register. Änderungs-/Freigaberegeln in Bestandstests, komplette neue Bedienprobe offen. |
| K9 (Quelle Z. 136) | R03 Speicherfehler: Fehlermeldung statt Erfolg, Entwurf und Marke bleiben, Bestand unverändert | Teilweise | Speichern, Codierung und Dialogregeln durch aktuelle Bestandstests; diese konkrete zusammenhängende Bedienfolge nicht vollständig nachgewiesen. |
| K10 (Quelle Z. 137) | R03 „Speichern und wechseln" wechselt bei Speicherfehler nicht; Hinweis im Dialog | Teilweise | Speichern, Codierung und Dialogregeln durch aktuelle Bestandstests; diese konkrete zusammenhängende Bedienfolge nicht vollständig nachgewiesen. |
| K11 (Quelle Z. 138) | R03 Wiederholung nach Fehler speichert erfolgreich und überlebt das Neuladen | Teilweise | Speichern, Codierung und Dialogregeln durch aktuelle Bestandstests; diese konkrete zusammenhängende Bedienfolge nicht vollständig nachgewiesen. |
| K12 (Quelle Z. 139) | R04 Ctrl+K und F3 verlassen weder Player noch Codierfenster; Shift+Tab und Tab bleiben im obersten Dialog | Teilweise | Speichern, Codierung und Dialogregeln durch aktuelle Bestandstests; diese konkrete zusammenhängende Bedienfolge nicht vollständig nachgewiesen. |
| K13 (Quelle Z. 140) | R04 Drei Dialogebenen: Esc schliesst je die oberste, Fokus kehrt Ebene für Ebene zurück | Teilweise | Speichern, Codierung und Dialogregeln durch aktuelle Bestandstests; diese konkrete zusammenhängende Bedienfolge nicht vollständig nachgewiesen. |
| K14-1366 (Quelle Z. 141) | 1366×768: weiterhin 7 vollständige Zeilen sichtbar | Zusatzprüfung offen | Verbindliche Mindestauflösung ist Full HD. Kleinere Fensterprobe 1280×720 ist kein Nachweis für diese Grösse oder echte DPI-Skalierung. |
| K14-1440 (Quelle Z. 142) | 1440×900: weiterhin 9 vollständige Zeilen sichtbar | Zusatzprüfung offen | Verbindliche Mindestauflösung ist Full HD. Kleinere Fensterprobe 1280×720 ist kein Nachweis für diese Grösse oder echte DPI-Skalierung. |
| K14-1920 (Quelle Z. 143) | 1920×1080: weiterhin 12 vollständige Zeilen sichtbar | Bestanden | Neue Tabellenmessung: Full HD, DPI 96, offene Feldschublade, zwölf ganze Haltungszeilen (hell). |

## Gliederung des vollständigen Prototyp-Inventars

Alle 73 nummerierten Haupt- und Unterabschnitte aus [PROTOTYP-INVENTAR.md](../2026-09-06-nova/wpf-etappe-2/PROTOTYP-INVENTAR.md) sind zugeordnet. Die Zuordnung ersetzt keine Prüfung jeder darin beschriebenen Einzelaktion.

| Abschnitt | Bereich | Stand | Zuordnung |
|---|---|---|---|
| 1 (Quelle Z. 10) | Design-Tokens | Teilweise | Dem Bereich über Code, aktuelle Tests und/oder Seitenaufbau zugeordnet. Details und offene Gesamtprüfungen im Bericht. Keine pauschale Abnahme. |
| 1.1 (Quelle Z. 12) | Farbtokens je Stimmung | Teilweise | Dem Bereich über Code, aktuelle Tests und/oder Seitenaufbau zugeordnet. Details und offene Gesamtprüfungen im Bericht. Keine pauschale Abnahme. |
| 1.2 (Quelle Z. 66) | Feste Farbwerte ausserhalb der Tokens (in beiden Stimmungen gleich) | Teilweise | Dem Bereich über Code, aktuelle Tests und/oder Seitenaufbau zugeordnet. Details und offene Gesamtprüfungen im Bericht. Keine pauschale Abnahme. |
| 1.3 (Quelle Z. 75) | Schriftgrössen (px), die nicht über die Skala laufen | Teilweise | Dem Bereich über Code, aktuelle Tests und/oder Seitenaufbau zugeordnet. Details und offene Gesamtprüfungen im Bericht. Keine pauschale Abnahme. |
| 1.4 (Quelle Z. 82) | Rundungen | Teilweise | Dem Bereich über Code, aktuelle Tests und/oder Seitenaufbau zugeordnet. Details und offene Gesamtprüfungen im Bericht. Keine pauschale Abnahme. |
| 1.5 (Quelle Z. 89) | Rahmen, Schatten, Glas | Teilweise | Dem Bereich über Code, aktuelle Tests und/oder Seitenaufbau zugeordnet. Details und offene Gesamtprüfungen im Bericht. Keine pauschale Abnahme. |
| 1.6 (Quelle Z. 97) | Abstände (Auswahl der massgebenden Werte) | Teilweise | Dem Bereich über Code, aktuelle Tests und/oder Seitenaufbau zugeordnet. Details und offene Gesamtprüfungen im Bericht. Keine pauschale Abnahme. |
| 1.7 (Quelle Z. 110) | Was Glas konkret von Cockpit unterscheidet | Teilweise | Dem Bereich über Code, aktuelle Tests und/oder Seitenaufbau zugeordnet. Details und offene Gesamtprüfungen im Bericht. Keine pauschale Abnahme. |
| 2 (Quelle Z. 121) | Bewegung („bewegt" / „ruhig") | Teilweise / offen | Vorhandene Regeln und aktuelle Tests; vollständige aktuelle Bedien-/Fokus-/Bewegungsprüfung offen. |
| 3 (Quelle Z. 139) | Rahmen (Shell) | Teilweise | Dem Bereich über Code, aktuelle Tests und/oder Seitenaufbau zugeordnet. Details und offene Gesamtprüfungen im Bericht. Keine pauschale Abnahme. |
| 3.1 (Quelle Z. 141) | Vorschau-Leiste (nur Prototyp, Z. 363–384) | Nur Prototyp / Referenz | Vorschau-Steuerung, Beispieldaten oder Referenzdateien sind keine nachzubauenden Produktdaten. |
| 3.2 (Quelle Z. 144) | Linke Leiste `.rail` (Z. 387–419), 220px, Glas | Teilweise | Dem Bereich über Code, aktuelle Tests und/oder Seitenaufbau zugeordnet. Details und offene Gesamtprüfungen im Bericht. Keine pauschale Abnahme. |
| 3.3 (Quelle Z. 155) | Menüleiste (Z. 422–426) | Teilweise | Dem Bereich über Code, aktuelle Tests und/oder Seitenaufbau zugeordnet. Details und offene Gesamtprüfungen im Bericht. Keine pauschale Abnahme. |
| 3.4 (Quelle Z. 158) | Kopfzeile `.topbar` (Z. 427–438) | Teilweise / Fehler | R1: falsche Auswahl nach Projektwechsel. R5: Suchbegrenzung aus dem Prototyp verdrängt Trefferarten. |
| 3.5 (Quelle Z. 164) | Tastenkürzel (Handler Z. 1339–1357, Hilfsfenster Z. 1043–1052) | Teilweise | Dem Bereich über Code, aktuelle Tests und/oder Seitenaufbau zugeordnet. Details und offene Gesamtprüfungen im Bericht. Keine pauschale Abnahme. |
| 3.6 (Quelle Z. 188) | Fenster (Dialoge) | Teilweise | Dem Bereich über Code, aktuelle Tests und/oder Seitenaufbau zugeordnet. Details und offene Gesamtprüfungen im Bericht. Keine pauschale Abnahme. |
| 4 (Quelle Z. 199) | Seiten | Teilweise | Dem Bereich über Code, aktuelle Tests und/oder Seitenaufbau zugeordnet. Details und offene Gesamtprüfungen im Bericht. Keine pauschale Abnahme. |
| 4.1 (Quelle Z. 203) | page-overview „Übersicht" (Z. 443–487, JS Z. 1407–1438) | Fehler | R2/R4: Projektübersicht und KI-Läufe nicht zuverlässig auf das aktive Projekt begrenzt. |
| 4.2 (Quelle Z. 221) | page-projekt „Projekt" (Z. 490–523) | Teilweise | Dem Bereich über Code, aktuelle Tests und/oder Seitenaufbau zugeordnet. Details und offene Gesamtprüfungen im Bericht. Keine pauschale Abnahme. |
| 4.3 (Quelle Z. 227) | page-haltungen „Haltungen" (Z. 526–578, `.fill`) | Teilweise / Fehler R8 | Kompakt und Zeilenhöhe nachgewiesen; Inspektionsrichtung fehlt in der Stammdaten-Spaltenansicht. |
| 4.4 (Quelle Z. 255) | page-schaechte „Schächte" (Z. 581–634, `.fill`) | Fehler | R7: mindestens acht Vorgabefelder falsch gruppiert; neun Kompaktspalten im aktuellen Schachtbild vorhanden. |
| 4.5 (Quelle Z. 262) | page-import „Import" (Z. 637–685) | Teilweise | Dem Bereich über Code, aktuelle Tests und/oder Seitenaufbau zugeordnet. Details und offene Gesamtprüfungen im Bericht. Keine pauschale Abnahme. |
| 4.6 (Quelle Z. 270) | page-export „Export" (Z. 688–719, JS Z. 1880–1886) | Teilweise | Dem Bereich über Code, aktuelle Tests und/oder Seitenaufbau zugeordnet. Details und offene Gesamtprüfungen im Bericht. Keine pauschale Abnahme. |
| 4.7 (Quelle Z. 284) | page-medien „Medienkonflikte" (Z. 722–732, JS Z. 1783–1798) | Teilweise | Dem Bereich über Code, aktuelle Tests und/oder Seitenaufbau zugeordnet. Details und offene Gesamtprüfungen im Bericht. Keine pauschale Abnahme. |
| 4.8 (Quelle Z. 290) | page-druck „Druckcenter" (Z. 735–769, JS Z. 1801–1811) | Teilweise | Dem Bereich über Code, aktuelle Tests und/oder Seitenaufbau zugeordnet. Details und offene Gesamtprüfungen im Bericht. Keine pauschale Abnahme. |
| 4.9 (Quelle Z. 295) | page-dossiers „Dossiers" (Z. 772–780, JS Z. 1814–1828) | Teilweise | Dem Bereich über Code, aktuelle Tests und/oder Seitenaufbau zugeordnet. Details und offene Gesamtprüfungen im Bericht. Keine pauschale Abnahme. |
| 4.10 (Quelle Z. 300) | page-matrix „Sanierungs-Matrix" (Z. 783–790, JS Z. 1831–1846) | Teilweise | Dem Bereich über Code, aktuelle Tests und/oder Seitenaufbau zugeordnet. Details und offene Gesamtprüfungen im Bericht. Keine pauschale Abnahme. |
| 4.11 (Quelle Z. 305) | page-smatrix „Schacht-Matrix" (Z. 793–796, JS Z. 1847–1851) | Teilweise | Dem Bereich über Code, aktuelle Tests und/oder Seitenaufbau zugeordnet. Details und offene Gesamtprüfungen im Bericht. Keine pauschale Abnahme. |
| 4.12 (Quelle Z. 309) | page-schatten „Schattenauswertung" (Z. 799–807, JS Z. 1854–1868) | Teilweise | Dem Bereich über Code, aktuelle Tests und/oder Seitenaufbau zugeordnet. Details und offene Gesamtprüfungen im Bericht. Keine pauschale Abnahme. |
| 4.13 (Quelle Z. 315) | page-vsa „VSA" (Z. 810–821, JS Z. 1871–1877) | Teilweise | Dem Bereich über Code, aktuelle Tests und/oder Seitenaufbau zugeordnet. Details und offene Gesamtprüfungen im Bericht. Keine pauschale Abnahme. |
| 4.14 (Quelle Z. 321) | page-diagnose „Diagnose" (Z. 824–841) | Teilweise | Dem Bereich über Code, aktuelle Tests und/oder Seitenaufbau zugeordnet. Details und offene Gesamtprüfungen im Bericht. Keine pauschale Abnahme. |
| 4.15 (Quelle Z. 326) | page-einstellungen „Einstellungen" (Z. 844–896, JS Z. 1889–1893) | Teilweise | Dem Bereich über Code, aktuelle Tests und/oder Seitenaufbau zugeordnet. Details und offene Gesamtprüfungen im Bericht. Keine pauschale Abnahme. |
| 5 (Quelle Z. 341) | Übersicht-Panel Haltung (`renderSide('h')`, Z. 1522–1531) | Teilweise | Dem Bereich über Code, aktuelle Tests und/oder Seitenaufbau zugeordnet. Details und offene Gesamtprüfungen im Bericht. Keine pauschale Abnahme. |
| 5.1 (Quelle Z. 345) | Rohrring mit Uhrlage (Z. 1526–1527) | Teilweise | Dem Bereich über Code, aktuelle Tests und/oder Seitenaufbau zugeordnet. Details und offene Gesamtprüfungen im Bericht. Keine pauschale Abnahme. |
| 5.2 (Quelle Z. 351) | Fakten (`.facts`, Raster 2 Spalten, Z. 1528) | Teilweise | Dem Bereich über Code, aktuelle Tests und/oder Seitenaufbau zugeordnet. Details und offene Gesamtprüfungen im Bericht. Keine pauschale Abnahme. |
| 5.3 (Quelle Z. 354) | Schadenliste (`.findings`, Z. 1529) | Teilweise | Dem Bereich über Code, aktuelle Tests und/oder Seitenaufbau zugeordnet. Details und offene Gesamtprüfungen im Bericht. Keine pauschale Abnahme. |
| 5.4 (Quelle Z. 362) | KI-Hinweis (Z. 1530), nur wenn offene Befunde | Teilweise | Dem Bereich über Code, aktuelle Tests und/oder Seitenaufbau zugeordnet. Details und offene Gesamtprüfungen im Bericht. Keine pauschale Abnahme. |
| 5.5 (Quelle Z. 365) | Aktualisierung | Teilweise | Dem Bereich über Code, aktuelle Tests und/oder Seitenaufbau zugeordnet. Details und offene Gesamtprüfungen im Bericht. Keine pauschale Abnahme. |
| 5.6 (Quelle Z. 368) | Schachtansicht (`renderSide('s')`, Z. 1533–1537) | Vorgabeschwäche | R6: Rechteckig wird bereits im Prototyp als Oval beschrieben und so umgesetzt. |
| 6 (Quelle Z. 373) | Fenster „Player" (`#winPlayer`, Z. 903–948; JS Z. 1627–1703) | Teilweise / offen | Vorhandene Regeln und aktuelle Tests; vollständige aktuelle Bedien-/Fokus-/Bewegungsprüfung offen. |
| 6.1 (Quelle Z. 375) | Titelzeile (Z. 904–905) | Teilweise / offen | Vorhandene Regeln und aktuelle Tests; vollständige aktuelle Bedien-/Fokus-/Bewegungsprüfung offen. |
| 6.2 (Quelle Z. 378) | Aufbau (Z. 308–309) | Teilweise / offen | Vorhandene Regeln und aktuelle Tests; vollständige aktuelle Bedien-/Fokus-/Bewegungsprüfung offen. |
| 6.3 (Quelle Z. 381) | Video (Z. 908–913) | Teilweise / offen | Vorhandene Regeln und aktuelle Tests; vollständige aktuelle Bedien-/Fokus-/Bewegungsprüfung offen. |
| 6.4 (Quelle Z. 384) | Regler/Zeitleiste (Z. 915, 1659–1660) | Teilweise / offen | Vorhandene Regeln und aktuelle Tests; vollständige aktuelle Bedien-/Fokus-/Bewegungsprüfung offen. |
| 6.5 (Quelle Z. 387) | Bedienleiste (Z. 916–932) | Teilweise / offen | Vorhandene Regeln und aktuelle Tests; vollständige aktuelle Bedien-/Fokus-/Bewegungsprüfung offen. |
| 6.6 (Quelle Z. 391) | Eingabemarker (Z. 934–937) | Teilweise / offen | Vorhandene Regeln und aktuelle Tests; vollständige aktuelle Bedien-/Fokus-/Bewegungsprüfung offen. |
| 6.7 (Quelle Z. 394) | KI-Zeile und Abschluss (Z. 938–944) | Teilweise / offen | Vorhandene Regeln und aktuelle Tests; vollständige aktuelle Bedien-/Fokus-/Bewegungsprüfung offen. |
| 6.8 (Quelle Z. 397) | Seitenpanel `#plSide` (340px, Z. 946, 1665–1669) | Teilweise / offen | Vorhandene Regeln und aktuelle Tests; vollständige aktuelle Bedien-/Fokus-/Bewegungsprüfung offen. |
| 6.9 (Quelle Z. 405) | Entwurf / Übernehmen / Beenden (Z. 1627–1649, 1677) | Teilweise / offen | Vorhandene Regeln und aktuelle Tests; vollständige aktuelle Bedien-/Fokus-/Bewegungsprüfung offen. |
| 6.10 (Quelle Z. 413) | Tastenkürzel im Player (Z. 1696–1703) | Teilweise / offen | Vorhandene Regeln und aktuelle Tests; vollständige aktuelle Bedien-/Fokus-/Bewegungsprüfung offen. |
| 6.11 (Quelle Z. 416) | Codierfenster „VSA-Codierung" (`#winVsa`, Z. 951–980; JS Z. 1706–1761) | Teilweise / offen | Vorhandene Regeln und aktuelle Tests; vollständige aktuelle Bedien-/Fokus-/Bewegungsprüfung offen. |
| 7 (Quelle Z. 428) | Fenster „Training Studio" (`#winStudio`, Z. 983–1027; JS Z. 1764–1780) | Teilweise | Dem Bereich über Code, aktuelle Tests und/oder Seitenaufbau zugeordnet. Details und offene Gesamtprüfungen im Bericht. Keine pauschale Abnahme. |
| 7.1 (Quelle Z. 430) | Titel und Aufbau | Fehler | R3: Schadensstufen passen nicht in die rechte Spalte. |
| 7.2 (Quelle Z. 433) | Linke Spalte (Z. 986–999) | Teilweise | Dem Bereich über Code, aktuelle Tests und/oder Seitenaufbau zugeordnet. Details und offene Gesamtprüfungen im Bericht. Keine pauschale Abnahme. |
| 7.3 (Quelle Z. 436) | Prüfplatz, Mitte (Z. 1000–1008) | Teilweise | Dem Bereich über Code, aktuelle Tests und/oder Seitenaufbau zugeordnet. Details und offene Gesamtprüfungen im Bericht. Keine pauschale Abnahme. |
| 7.4 (Quelle Z. 444) | Rechte Spalte, drei Schritte (Z. 1010–1024) | Fehler | R3: Schadensstufen passen nicht in die rechte Spalte. |
| 7.5 (Quelle Z. 450) | Sperrregel Akzeptieren → Freigabe (Z. 1768–1780) | Teilweise | Dem Bereich über Code, aktuelle Tests und/oder Seitenaufbau zugeordnet. Details und offene Gesamtprüfungen im Bericht. Keine pauschale Abnahme. |
| 8 (Quelle Z. 458) | JS-Regeln, die Verhalten beschreiben | Teilweise | Dem Bereich über Code, aktuelle Tests und/oder Seitenaufbau zugeordnet. Details und offene Gesamtprüfungen im Bericht. Keine pauschale Abnahme. |
| 8.1 (Quelle Z. 460) | „Nächste Haltung prüfen" (Z. 1439–1444, 1650) | Teilweise | Dem Bereich über Code, aktuelle Tests und/oder Seitenaufbau zugeordnet. Details und offene Gesamtprüfungen im Bericht. Keine pauschale Abnahme. |
| 8.2 (Quelle Z. 465) | Auswahlwechsel mit Rückfrage (Z. 1454–1466, 1331–1338) | Teilweise | Dem Bereich über Code, aktuelle Tests und/oder Seitenaufbau zugeordnet. Details und offene Gesamtprüfungen im Bericht. Keine pauschale Abnahme. |
| 8.3 (Quelle Z. 470) | Speichern / Verwerfen / Speicherfehler (Z. 1467–1483, 1281–1286) | Teilweise | Dem Bereich über Code, aktuelle Tests und/oder Seitenaufbau zugeordnet. Details und offene Gesamtprüfungen im Bericht. Keine pauschale Abnahme. |
| 8.4 (Quelle Z. 477) | Spaltenansichten und Feldlisten | Teilweise | Dem Bereich über Code, aktuelle Tests und/oder Seitenaufbau zugeordnet. Details und offene Gesamtprüfungen im Bericht. Keine pauschale Abnahme. |
| 8.5 (Quelle Z. 480) | Filter und Suche | Teilweise / Fehler | R1: falsche Auswahl nach Projektwechsel. R5: Suchbegrenzung aus dem Prototyp verdrängt Trefferarten. |
| 8.6 (Quelle Z. 489) | Schublade und Splitter (Z. 1575–1601) | Teilweise | Dem Bereich über Code, aktuelle Tests und/oder Seitenaufbau zugeordnet. Details und offene Gesamtprüfungen im Bericht. Keine pauschale Abnahme. |
| 8.7 (Quelle Z. 495) | Tabellenzustandsregeln | Teilweise | Dem Bereich über Code, aktuelle Tests und/oder Seitenaufbau zugeordnet. Details und offene Gesamtprüfungen im Bericht. Keine pauschale Abnahme. |
| 8.8 (Quelle Z. 500) | Dialog-Stapel und Fokus | Teilweise / offen | Vorhandene Regeln und aktuelle Tests; vollständige aktuelle Bedien-/Fokus-/Bewegungsprüfung offen. |
| 8.9 (Quelle Z. 503) | Browserspeicher-Schlüssel (`nova.opt.` + …) | Teilweise | Dem Bereich über Code, aktuelle Tests und/oder Seitenaufbau zugeordnet. Details und offene Gesamtprüfungen im Bericht. Keine pauschale Abnahme. |
| 8.10 (Quelle Z. 506) | Simulationen | Teilweise | Dem Bereich über Code, aktuelle Tests und/oder Seitenaufbau zugeordnet. Details und offene Gesamtprüfungen im Bericht. Keine pauschale Abnahme. |
| 9 (Quelle Z. 512) | Beispieldaten-Felder je Thema der Eingabefelder | Teilweise | Dem Bereich über Code, aktuelle Tests und/oder Seitenaufbau zugeordnet. Details und offene Gesamtprüfungen im Bericht. Keine pauschale Abnahme. |
| 9.1 (Quelle Z. 516) | Haltung (`FIELDS_H`, Z. 1230–1235) — 36 Felder | Teilweise | Haltungsfelder 17/9/11/3 gemäss genehmigter Erweiterung in aktuellem Bild und Builder; keine vollständige manuelle Editierfolge jedes Felds. |
| 9.2 (Quelle Z. 572) | Schacht (`FIELDS_S`, Z. 1236–1241) — 30 Felder | Fehler | R7: mindestens acht Vorgabefelder falsch gruppiert; neun Kompaktspalten im aktuellen Schachtbild vorhanden. |
| 9.3 (Quelle Z. 582) | Beispielhaltungen (Z. 1100–1170), Kurzliste | Nur Prototyp / Referenz | Vorschau-Steuerung, Beispieldaten oder Referenzdateien sind keine nachzubauenden Produktdaten. |
| 10 (Quelle Z. 604) | Begleitdateien (Kurzfassung) | Nur Prototyp / Referenz | Vorschau-Steuerung, Beispieldaten oder Referenzdateien sind keine nachzubauenden Produktdaten. |

## Zusätzliche aktuelle Sperren

- R1/R2/R4: Projektwechsel und projektfremde Zustände.
- R3: Stufenauswahl im Training Studio.
- R5/R6: zwei fachlich/praktisch unzureichende Regeln bereits im Prototyp.
- R7/R8: unvollständige Umsetzung der Feldvorgaben.
- Nachschlag-Kontextmenütest: zweimaliges Zeitlimit, Ursache offen.
- Normaler Gesamt-Build: Dateisperre des laufenden MCP-Diensts; isolierter MCP-Build erfolgreich.
