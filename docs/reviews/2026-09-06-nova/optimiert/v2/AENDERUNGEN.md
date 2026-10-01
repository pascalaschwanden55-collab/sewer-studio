# Änderungsliste v2: SewerStudio-Nova-Optimiert-v2.html

Stand 06.09.2026. Neue Datei auf dem Desktop und unter
`docs/reviews/2026-09-06-nova/optimiert/v2/`. Die Originale, die v1 und die Nachweise von
Codex sind unverändert (SHA-256 in `PRUEFTABELLE.md`). Gestaltung, 15 Seiten und drei
Arbeitsfenster bleiben wie in v1. Der WPF-Einbau ist weiterhin ein eigener Schritt; an der
echten Anwendung und an Kundenoriginalen wurde nichts geändert.

## Sechs Restpunkte, umgesetzt

| Punkt | Was jetzt gilt |
|---|---|
| R01 Wechsel vor Player | Jede Aktion, die eine andere Haltung braucht („Nächste Haltung prüfen", KI-Aufgabenliste, Play-Knopf in der Liste, globale Suche, Dossier-Sprung), läuft über eine Auswahl mit Fortsetzung. Bei ungespeicherten Änderungen erscheint zuerst nur die Rückfrage. „Abbrechen, hier bleiben" beendet die ganze Aktion: Auswahl, Formular und Entwurf bleiben, kein Player öffnet sich. „Speichern und wechseln" und „Verwerfen und wechseln" stellen Auswahl, Formular und Player gemeinsam auf die Zielhaltung um. |
| R02 Trainingsfreigabe | „Akzeptieren" bindet die Bestätigung an den genauen Stand von VSA-Code, Uhrlage, Stufe, Beschreibung und Bildauswahl. Jede Änderung hebt die Bestätigung auf und sperrt die Freigabe mit Hinweis. Die Freigabe prüft beim Klick nochmals Pflichtfelder und Stand; ein erzwungener Klick wird abgelehnt. Nach einer Freigabe ist sie wieder gesperrt. Es werden weiterhin keine Trainingsdaten geschrieben. |
| R03 Speicherfehler | Speichern meldet Erfolg oder Fehler. Schlägt der Browserspeicher fehl (zum Beispiel QuotaExceededError), bleiben Eingabe, Änderungsmarke und Speichern-Knopf erhalten, der Bestand wird zurückgesetzt, und die Meldung sagt, dass nicht gespeichert wurde. „Speichern und wechseln" wechselt erst nach erfolgreichem Speichern; bei Fehler bleibt der Dialog mit Hinweis offen. Ein späterer Versuch speichert normal. |
| R04 Kürzel in Dialogen | Strg+K und F3 verschieben den Fokus nicht mehr in die verdeckte Hauptseite; bei offenem Fenster erscheint ein kurzer Hinweis. Tab und Umschalt+Tab bleiben im obersten Dialog, auch bei drei Ebenen (Player, Codierfenster, Tastenkürzel). Esc schliesst je die oberste Ebene, der Fokus kehrt Ebene für Ebene zurück. |
| R05 Kontrast | Log-Farben INFO/WARN/ERROR, die Beschriftungen Hand-Box und SAM-Maske, farbige Kennzahlen und Platzhalter haben je Thema eigene Textfarben (`--ok-text`, `--warn-text`, `--bad-text`, `--ok-ink`, `--z0-ink`). Bildkacheln sind deckend statt mit Verlauf. Der Kontrasttest läuft jetzt über alle 15 Seiten, drei Fenster, Eingabewerte und Platzhalter in beiden Themen und unterscheidet: normaler Text unter 4,5:1 (Fehler), grosser Text unter 4,5:1 (Projektziel verletzt, WCAG erlaubt 3:1), Text auf Verläufen (gesondert gelistet). Ergebnis: null Stellen in allen drei Gruppen. |
| R06 Player-Entwurf | Der Player arbeitet auf einem Entwurf der Haltung (Befunde, KI-Vorschläge, Prüfstatus). Neu erfasste, bearbeitete, bestätigte und abgelehnte Befunde stehen nur im Entwurf; das Abzeichen „● Entwurf: n Änderungen" zeigt das. „Codierung übernehmen" schreibt den Entwurf in den Beispielbestand. „Beenden ohne Übernahme" verwirft ihn. Esc oder „Schliessen" bei Entwurf fragt: Übernehmen und schliessen, Beenden ohne Übernahme, Zurück zum Player. |

## Weitere Korrekturen

- Die Angabe „16 Bildschirmbilder" aus der Erstlieferung war falsch. Es waren und sind 14.
- Die Gegenproben von Codex wurden gegen v2 ausgeführt. `kontrast.cjs` und
  `player_abbruch.cjs` unverändert; `gegenproben.cjs` mit drei gekennzeichneten Zeilen,
  weil v2 an diesen Stellen absichtlich nachfragt oder sperrt. Ergebnisse stehen in der
  Prüftabelle.

## Unverändert gegenüber v1

Zentraler Beispielbestand, getrennte Suchen, aus Daten gerechnete Kennzahlen, Rückkehr aus
dem Codierfenster, ruhiger Modus, Systemschriften, Offline-Aufruf, 7 / 9 / 12 sichtbare
Zeilen, Funktionsliste.
