# KI-Live-Codiermodus — Bedienungsanleitung

## Voraussetzung

1. Video ueber die **Datenseite** mit einer Haltung oeffnen (PlayerWindow)
2. **Codier-Modus** Button klicken (Toolbar oben)

## Toolbar im Codiermodus (von links nach rechts)

| Button | Funktion |
|---|---|
| **◀ / ▶** | Meter-Navigation (0.5m Schritte) |
| **⊕ Kalib.** | Kalibrierung: Linie ueber Rohrdurchmesser zeichnen (DN → mm) |
| **━ Linie** | Riss messen (Laenge in mm, Uhrposition) |
| **⌒ Bogen** | Umfangsschaden messen (Uhr-Von bis Uhr-Bis, Bogenwinkel) |
| **▢ Flaeche** | Flaechenschaden markieren (Hoehe x Breite in mm) |
| **● Punkt** | Einzelschaden markieren (Uhrposition) |
| **↔ Strecke** | Streckenschaden (Meter-Bereich) |
| **⚡ Live-KI** | Automatische KI-Analyse an/aus |
| **⚡ Analysieren** | Einzelnen Frame manuell analysieren |

---

## Workflow 1: KI als Assistent (Live)

1. **"⚡ Live-KI"** aktivieren (Toggle)
2. Video abspielen
3. KI analysiert automatisch alle 5 Sekunden den aktuellen Frame
4. **Ampelsystem:**
   - **Gruen** (Sicherheit >= 75%): Befund wird automatisch eingetragen
   - **Gelb** (45–74%): Video haelt an → Bestaetigungs-Panel erscheint
   - **Rot** (< 45%): Video haelt an → User muss entscheiden
5. Bei **Gelb/Rot** erscheint ein Panel mit 3 Optionen:
   - **✓ Uebernehmen** — KI-Vorschlag akzeptieren
   - **✎ Code aendern** — Korrekten VSA-Code waehlen
   - **✗ Verwerfen** — Befund loeschen
6. Nach Entscheidung laeuft das Video automatisch weiter

## Workflow 2: Manuell zeichnen → KI analysiert

1. **"⚡ Live-KI"** aktivieren
2. Video pausieren an verdaechtiger Stelle
3. Werkzeug waehlen (z.B. **━ Linie** fuer Riss)
4. Auf dem Video zeichnen wo der Schaden ist
5. KI analysiert automatisch die markierte Stelle
6. Ergebnis erscheint in der Ereignisliste

## Workflow 3: Kalibrierung (fuer mm-Messungen)

1. **"⊕ Kalib."** aktivieren
2. Linie ueber den sichtbaren **Rohrdurchmesser** zeichnen
3. Danach messen alle Werkzeuge in Millimeter (statt Pixel)
4. Anzeige: "Kalibriert: X.X mm/norm"

## Workflow 4: Manuell analysieren (ohne Live)

1. Video an gewuenschter Stelle pausieren
2. **"⚡ Analysieren"** klicken
3. KI analysiert den einzelnen Frame
4. Befunde erscheinen in der Toolbar + Ereignisliste

---

## Overlay-Werkzeuge im Detail

### ⊕ Kalibrierung
- Zeichne eine Linie ueber den sichtbaren Rohrdurchmesser
- DN (Nennweite) wird aus den Haltungs-Stammdaten geladen
- Nach Kalibrierung: alle Messungen in mm statt relativen Werten
- **Tipp:** Moeglichst gerade, von Rohrkante zu Rohrkante

### ━ Linie
- Fuer Risse (Laengsriss, Querriss)
- Ergebnis: Q1 = Laenge in mm, Uhrposition Von/Bis
- VSA-Codes: BAA (Laengsriss), BAB (Querriss), BAC (Scherbenbildung)

### ⌒ Bogen
- Fuer Umfangsschaeden (Umfangsriss, Deformation)
- Zeichne von Startpunkt zum Endpunkt am Rohrumfang
- Bogen wird um die Rohrmitte berechnet (kalibriert oder Bildmitte)
- Ergebnis: Uhr-Von, Uhr-Bis, Bogenwinkel in Grad
- VSA-Codes: BAF (Umfangsriss), BAG (Verformung)

### ▢ Flaeche
- Fuer Flaechenschaeden (Korrosion, Abblätterung)
- Rechteck aufziehen ueber den Schadensbereich
- Ergebnis: Q1 = Hoehe mm, Q2 = Breite mm, Uhrposition Mitte
- VSA-Codes: BBB (Oberflaechenschaden), BBA (fehlendes Wandungsteil)

### ● Punkt
- Fuer Einzelschaeden (Loch, Anschluss)
- Klick auf die Schadenstelle
- Ergebnis: Uhrposition
- VSA-Codes: BAJ (Loch), BDA (Einragender Anschluss)

### ↔ Strecke
- Fuer Streckenschaeden (ueber mehrere Meter)
- Linie horizontal zeichnen
- Ergebnis: Uhr-Von, Uhr-Bis (fuer Strecken-Beobachtungen)
- VSA-Codes: BCB (Ablagerung), BBA (fehlende Auskleidung)

---

## Farbcode der Overlays auf dem Video

| Farbe | Bedeutung |
|---|---|
| **Orange (gestrichelt)** | KI-Vorschlag (noch nicht bestaetigt) |
| **Gruen** | Akzeptierter Befund |
| **Rot** | Verworfener Befund |
| **Lime** | Manuelle Linie/Strecke (User) |
| **Cyan** | Manuelles Rechteck (User) |
| **Magenta (gestrichelt)** | Kalibrierungs-Linie |
| **Orange** | Bogen-Vorschau (User) |

## Ampelsystem (QualityGate)

| Farbe | Konfidenz | Verhalten |
|---|---|---|
| **Gruen** | >= 75% | Automatisch eingetragen |
| **Gelb** | 45–74% | Video pausiert, Bestaetigungs-Panel |
| **Rot** | < 45% | Video pausiert, User muss entscheiden |

Die Konfidenz wird aus mehreren Signalen berechnet:
- KI-Vision-Analyse (Qwen2.5-VL)
- Plausibilitaets-Score (VSA-Code erkannt?)
- Optional: YOLO, DINO, SAM Pipeline-Signale
