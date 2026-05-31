# Arbeitsstand 2026-05-31 — Klartext (für Inspekteur, nicht Programmierer)

Branch `feature/gis-karte` · 8 gesicherte Schritte · **alles nur lokal, nichts nach außen gegeben (kein Push)** · Programm baut sauber, 768 Prüfungen laufen durch.

---

## Zuerst: die große Prüfung der KI-Verarbeitungskette

Wir haben den **echten Zustand** der KI durchleuchtet (großer Mehr-Agenten-Prüflauf, nur lesend). Wichtigste Erkenntnisse — mehrere Befürchtungen waren falsch:

- Die „überflutete Wissensdatenbank mit 96 % schlechten Beispielen" gibt es **nicht** — die Datenbank war schlicht **leer**, und eine „Rot/Schlecht"-Markierung existiert dort gar nicht.
- Viele Bausteine aus der Entwickler-Doku (automatische Schadens-Zusammenführung „DetectionAggregator", Objektverfolgung „ByteTrack", ein „Steuer-Dienst" für die Grafikkarte, eine Wissens-Dublettenprüfung) **gibt es im laufenden Programm nicht** — das waren Doku-Phantome.
- Vollbericht: `docs/audits/2026-05-31-pipeline-rootcause-workflow.md`.

---

## Was repariert wurde (8 Schritte)

### Für die tägliche Arbeit
- **Import-Vorschau ist jetzt wirklich nur Vorschau.** *Vorher:* die Vorschau konnte das echte Projekt verändern. *Nachher:* sie rechnet auf einer Kopie, das Projekt bleibt unberührt. *(0bd6667d)*
- **Projekt öffnen/neu fragt nach** — „Speichern / Verwerfen / Abbrechen", damit nichts verloren geht. *(0bd6667d)*
- **Löschen ist abgesichert** — einzelne Haltung löschen und „Spalte leeren" mit klarer Rückfrage und Anzahl betroffener Haltungen. *(0bd6667d)*
- **Codiermodus-Fenster stürzt nicht mehr ab,** wenn man es direkt nach dem Öffnen schnell wieder schließt. *(0bd6667d)*
- **Karte:** Zustandsklasse 2 (mittel) wird jetzt **orange** statt fälschlich **rot**. *(0bd6667d)*

### Für die Qualität der KI-Beurteilung
- **Die KI übernimmt nichts mehr selbst.** *Vorher:* „grüne" Befunde wurden automatisch als akzeptiert eingetragen. *Nachher:* die KI **schlägt vor**, der Mensch bestätigt. Kritische Schäden (Stufe 4/5) **pausieren das Video** und fragen nach. Gilt jetzt für **beide** Analyse-Wege. *(8fa4ca58 — ergänzt einen früheren Schritt, der nur einen Weg abdeckte)*
- **Ehrliche Ampel.** *Vorher:* ein erfundener fixer Sicherheitswert (0,8) schob die Ampel künstlich auf „grün". *Nachher:* Grenzfälle landen ehrlich auf **„gelb = bitte prüfen"**. *(984fff23 + b021b0ce)*
- **Punktschaden bleibt Punktschaden** — wird in keinem Analyse-Weg mehr fälschlich als Streckenschaden gespeichert. *(b8fdf133)*
- **KI-Einträge sind als „KI" gekennzeichnet,** nicht mehr als „manuell" getarnt. *(0bd6667d)*
- **Import warnt bei Unsinn** — z. B. Meterstand größer als Haltungslänge, unrealistische Nennweite. *(0bd6667d)*

### Im Hintergrund
- **KI-Rechenzeit pro Videobild vereinheitlicht** auf 120 Sekunden (vorher widersprüchlich 60 vs. 300, wobei die 300 wirkungslos waren). *(2707d31e + e6fde0af)*
- **Fernsteuerung übers Netz** lässt sich jetzt mit einem Schlüssel absichern. *(0bd6667d)*
- **Entwickler-Doku auf den echten Stand gebracht** — Phantome klar als „geplant, noch nicht gebaut" markiert. *(40c02598)*

---

## Die 8 Schritte (für die Ablage)

| Schritt | Inhalt |
|---|---|
| `0bd6667d` | Audit-Sammelfix: Vorschau read-only, Speichern-Abfrage, Lösch-Rückfragen, Fenster-Absturz, Karte-Farbe, Import-Warnungen, Fernsteuerung-Schlüssel, KI-Kennzeichnung |
| `8fa4ca58` | KI akzeptiert auch im zweiten Analyse-Weg nicht mehr selbst |
| `40c02598` | Entwickler-Doku entphantomisiert |
| `2707d31e` | KI-Rechenzeit pro Bild auf 120 s |
| `e6fde0af` | restliche Rechenzeit-Werte nachgezogen |
| `b8fdf133` | Punkt/Strecken-Unterscheidung im zweiten Weg |
| `984fff23` | Prüf-Test zur „ehrlichen Ampel" |
| `b021b0ce` | erfundener Sicherheitswert entfernt |

---

## Was noch offen ist

- **Deine Sicht-Abnahme** am laufenden Programm.
- **Entscheidung zur KI-Ampel (D3)** — Vorlage liegt vor, du wählst die Richtung (ehrlich-statisch dokumentieren ist empfohlen; echtes „selbstlernendes" Gate nur mit Messung).
- Weitere besprechbare Punkte: D5 (räumliche Trennung beim Zusammenführen), D6 (doppelte Logik zusammenführen — größere Umbauarbeit), D7 (fachliche Plausibilitätsprüfung vor dem KI-Lernen).
- **Nichts ist gepusht** — alles wartet auf deine Freigabe.
