# Nachbesserung der optimierten Nova-Vorschau

Überarbeite SewerStudio-Nova-Optimiert.html und liefere eine neue SewerStudio-Nova-Optimiert-v2.html. Bewahre die bisherigen Dateien und Nachweise. Behalte die verbesserte Gestaltung, die 15 Seiten und die drei Arbeitsfenster bei.

Der bisherige Prüflauf besteht erneut alle 62 Punkte. Ergänzende Gegenproben in nachpruefung-codex/nachweise/gegenpruefung.json zeigen folgende Lücken:

1. R01: Ein Haltungswechsel muss vollständig abgeschlossen sein, bevor ein Player startet. Gegenprobe: h02 ändern → Übersicht → Nächste Haltung prüfen → Abbrechen. Der Player für h01 darf nicht erscheinen. Speichern/Verwerfen müssen dagegen Auswahl, Formular und Player gemeinsam auf h01 umstellen. Dies gilt auch für die KI-Aufgabenliste.
2. R02: Akzeptierte Trainingsangaben dürfen nach einer Änderung nicht weiterhin freigegeben werden. Akzeptieren → Code und Beschreibung leeren muss die Freigabe sperren. Jede fachliche Änderung hebt die Bestätigung auf. Prüfe den aktuellen Stand nochmals direkt bei der Freigabe. Die Vorschau schreibt weiterhin keine echten Trainingsdaten.
3. R03: Fehler des Browserspeichers dürfen keine Erfolgsmeldung erzeugen. Bei QuotaExceededError Eingabe und Änderungsmarke behalten. „Speichern und wechseln“ darf erst nach erfolgreichem Speichern wechseln. Ergänze einen gezielten Test mit erzwungenem Speicherfehler und anschliessendem erfolgreichen Wiederholungsversuch.
4. R04: Strg+K darf bei offenem Player oder Codierfenster den Fokus nicht in die verdeckte Hauptseite verschieben. Prüfe globale Kürzel, Tab, Umschalt+Tab und Esc auch bei mehreren Dialogebenen.
5. R05: Textkontrast korrigieren und vollständiger testen. Aktuell: INFO hell 3,31:1; WARN hell 4,05:1; SAM-Maske hell 3,52:1 und dunkel 1,81:1; Hand-Box dunkel 3,05:1; Platzhalter „nur Streckenschaden“ dunkel 3,44:1. Normale Texte brauchen mindestens 4,5:1. Alle 15 Seiten, drei Arbeitsfenster, Eingabewerte und Platzhalter in beiden Themen prüfen. Echte Hintergrundfarben berücksichtigen, Verläufe separat beurteilen. Das strengere Projektziel für alle Texte von 4,5:1 von der WCAG-Ausnahme für grossen Text unterscheiden.
6. R06: Player-Abbruch muss seinem Namen entsprechen. Neuer Befund → im Codierfenster übernehmen → im Player „Beenden ohne Übernahme“: Der ursprüngliche gemeinsame Datenbestand muss erhalten bleiben. Auch Bearbeiten, Löschen und Prüfstatus abdecken. Nutze einen Player-Entwurf; erst „Codierung übernehmen“ überträgt ihn.

Die Gegenproben stehen auch als ausführbarer Browsercode im Nachweisordner. Führe sowohl den bisherigen Prüflauf als auch die ergänzten Prüfungen aus. Prüfe, dass weiterhin 7/9/12 vollständige Zeilen bei 1366×768, 1440×900 und 1920×1080 sichtbar sind. Aktualisiere Änderungsliste, Prüftabelle, JSON und Bildschirmbilder anhand tatsächlicher Läufe. Der bisherige Ordner enthält 14 Bildschirmbilder; korrigiere die Angabe 16.

Liefere eine kurze Erklärung für den Anwender und einen aktualisierten HTML-Überblick. Kennzeichne nicht geprüfte Bereiche. Der WPF-Einbau bleibt ein eigener Schritt; keine Änderung der echten Anwendung oder von Kundenoriginalen im Zuge dieser HTML-Nachbesserung.
