# SewerStudio 3.1 — Vollstaendige Bedienungsanleitung

**KI-gestuetzte Kanalinspektion und Zustandsbewertung**
Version 3.1 | Stand: April 2026

---

## Inhaltsverzeichnis

1. [Ueberblick](#1-ueberblick)
2. [Systemvoraussetzungen](#2-systemvoraussetzungen)
3. [Programmstart und Projektverwaltung](#3-programmstart-und-projektverwaltung)
4. [Datenimport](#4-datenimport)
5. [Haltungen (Kanalauschnitte)](#5-haltungen)
6. [Schaechte](#6-schaechte)
7. [Inspektionsprotokolle](#7-inspektionsprotokolle)
8. [KI-gestuetzte Videoanalyse](#8-ki-gestuetzte-videoanalyse)
9. [Codiermodus (Interaktive Inspektion)](#9-codiermodus)
10. [VSA-KEK Zustandsbewertung](#10-vsa-kek-zustandsbewertung)
11. [Kostenberechnung und Massnahmenplanung](#11-kostenberechnung)
12. [Eigendevis (Kostenvoranschlag)](#12-eigendevis)
13. [Hydraulikberechnung](#13-hydraulikberechnung)
14. [Datenexport und Druckcenter](#14-datenexport-und-druckcenter)
15. [Medienverwaltung und Konflikte](#15-medienverwaltung)
16. [Trainingscenter (Selbstlernendes System)](#16-trainingscenter)
17. [Einstellungen](#17-einstellungen)
18. [Technische Spezifikationen](#18-technische-spezifikationen)
19. [Tastenkuerzel und Tipps](#19-tastenkuerzel)
20. [Fehlerbehebung](#20-fehlerbehebung)

---

## 1. Ueberblick

SewerStudio ist eine Windows-Desktop-Anwendung fuer die professionelle Kanalinspektion.
Sie verbindet klassische Auswertung mit KI-gestuetzter Videoanalyse und automatischer
Schadenscodierung nach VSA-KEK (Schweizer Standard) und EN 13508-2 (europaeischer Standard).

### Was SewerStudio kann

| Funktion | Beschreibung |
|----------|-------------|
| **Datenimport** | PDF-Protokolle, XTF/INTERLIS, WinCan, IBAK, KINS einlesen |
| **Videoanalyse** | KI erkennt Schaeden automatisch im Inspektionsvideo |
| **Codierung** | Interaktiver Codiermodus mit KI-Vorschlaegen |
| **VSA-Bewertung** | Automatische Zustandsklassen (0-4) nach VSA-KEK |
| **Kosten** | Kostenberechnung und Eigendevis-Erstellung |
| **Hydraulik** | Hydraulische Berechnungen nach DWA-A 110 |
| **Export** | Excel, PDF-Berichte, Dossiers |
| **Selbstlernen** | KI verbessert sich durch Korrekturen des Inspekteurs |

### Unterstuetzte Standards

- **VSA-KEK 2023** — Schweizer Kanalzustandsbewertung
- **EN 13508-2** — Europaeische Schadenscodierung
- **DWA-A 110** — Hydraulische Berechnungen fuer Kreisprofile
- **INTERLIS 2 / XTF** — Schweizer Infrastruktur-Datenaustausch

---

## 2. Systemvoraussetzungen

### Mindestanforderungen

| Komponente | Minimum | Empfohlen |
|-----------|---------|-----------|
| Betriebssystem | Windows 10 (64-bit) | Windows 11 Pro |
| Prozessor | Intel Core i7 (8. Gen) | Intel Core Ultra 9 285K |
| Arbeitsspeicher | 16 GB DDR4 | 64 GB DDR5 |
| Grafikkarte | NVIDIA GTX 1660 (6 GB) | NVIDIA RTX 5090 (32 GB) |
| Festplatte | 50 GB SSD | 500 GB NVMe SSD |
| .NET Runtime | .NET 10 | .NET 10 |
| Bildschirm | 1920×1080 | 2560×1440 oder groesser |

### KI-Anforderungen (GPU-Speicher)

| Modus | VRAM benoetigt | Was geht |
|-------|---------------|----------|
| **Laptop-Modus** | 8-12 GB | Nur Qwen 8B, kein Sidecar |
| **Standard-Modus** | 12-24 GB | YOLO + Qwen 8B + Sidecar |
| **Workstation-Modus** | 24-32 GB | Volle Pipeline mit Eskalation auf 32B |

### Zusaetzliche Software

| Software | Zweck | Hinweis |
|----------|-------|---------|
| **Ollama** | Lokale KI-Modelle (Qwen) | Muss laufen auf Port 11434 |
| **Python Sidecar** | YOLO/DINO/SAM Modelle | Muss laufen auf Port 8100 |
| **FFmpeg** | Video-Frame-Extraktion | Muss im PATH sein |
| **VLC** | Video-Wiedergabe | Wird mitgeliefert (libvlc) |

---

## 3. Programmstart und Projektverwaltung

### Erster Start

Nach dem Start erscheint die **Uebersichtsseite** mit:
- Liste der letzten Projekte (bis zu 20)
- Projektstatistiken (Anzahl Haltungen, letzte Aenderung)
- App-Version und KI-Status

### Neues Projekt erstellen

1. Klicke auf **"Neues Projekt"**
2. Gib den Projektnamen ein
3. Fuelle die Projektdaten aus:
   - **Auftraggeber** — Wer hat den Auftrag erteilt
   - **Auftrag Nr.** — Auftragsnummer
   - **Gemeinde** — Betroffene Gemeinde
   - **Zone** — Inspektionszone
   - **Strasse** — Strassenname
   - **Bearbeiter** — Wer fuehrt die Inspektion durch
   - **Inspektionsdatum** — Datum der Inspektion
   - **Firma** — Ausfuehrendes Unternehmen
4. Klicke auf **"Speichern"**

### Bestehendes Projekt oeffnen

- Klicke auf ein Projekt in der Liste der letzten Projekte
- Oder nutze **"Projekt oeffnen"** fuer Dateien von der Festplatte
- Drag-and-drop von Projektdateien wird unterstuetzt

### Automatisches Speichern

SewerStudio bietet drei Speichermodi:

| Modus | Wann wird gespeichert |
|-------|----------------------|
| **Bei jeder Aenderung** | Sofort nach jeder Eingabe |
| **Beim Schliessen** | Nur wenn du das Projekt schliesst |
| **Manuell** | Nur wenn du "Speichern" klickst |

Zusaetzlich werden **Wiederherstellungspunkte** angelegt, damit du bei Problemen
zu einem frueheren Stand zurueckkehren kannst.

---

## 4. Datenimport

### Unterstuetzte Import-Formate

| Format | Quelle | Was wird importiert |
|--------|--------|-------------------|
| **PDF-Protokolle** | Inspektionsberichte | Haltungsdaten, Schaeden, Messwerte |
| **XTF / INTERLIS** | Schweizer GIS-Systeme | Vollstaendige Kanaldaten mit Geometrie |
| **M150 / MDB** | Datenbankexporte | Strukturierte Kanaldaten |
| **WinCan** | WinCan Software | Projekte, Inspektionen, Medien |
| **IBAK** | IBAK-Kamerasysteme | Inspektionsdaten und Videos |
| **KINS** | KINS Software | Projektdaten |

### So importierst du Daten

1. Gehe zur Seite **"Import"**
2. Waehle das **Import-Format** (PDF, XTF, WinCan etc.)
3. Waehle die **Quelldatei(en)** oder den Quellordner
4. **Vorschau**: Klicke auf "Vorschau" um zu sehen was importiert wird, bevor es passiert
5. **Optionen**:
   - **"Nur fehlende Felder fuellen"** — Bestehende Daten werden nicht ueberschrieben
   - **"Alles ueberschreiben"** — Alle Felder werden aktualisiert
6. Klicke auf **"Import starten"**
7. Der Fortschritt wird angezeigt
8. Nach dem Import: **Zusammenfassung** mit Anzahl gefundener/erstellter/aktualisierter Datensaetze

### Import-Bericht

Nach jedem Import wird ein Bericht erstellt:
- Anzahl gefundene / erstellte / aktualisierte Datensaetze
- Fehler und Warnungen
- Unsichere Zuordnungen
- Der Bericht kann jederzeit nochmals aufgerufen werden

### Feldprioriaet bei Konflikten

Wenn dasselbe Feld aus verschiedenen Quellen importiert wird, gilt diese Reihenfolge
(hoechste Prioritaet zuerst):

1. **Manuell** — Was du selbst eingetragen hast
2. **XTF 405** — Neuester Schweizer Standard
3. **XTF** — INTERLIS-Import
4. **ILI** — Aelterer INTERLIS-Import
5. **PDF** — Aus PDF-Protokollen
6. **Legacy** — Aeltere Datenquellen

---

## 5. Haltungen

### Was ist eine Haltung?

Eine **Haltung** ist ein Kanalabschnitt zwischen zwei Schaechten. Typisch 30-80 Meter lang.
Jede Haltung hat einen Anfangsschacht und einen Endschacht.

### Die Haltungs-Tabelle

Die Hauptansicht zeigt alle Haltungen in einer sortierbaren Tabelle:

| Feld | Beschreibung | Beispiel |
|------|-------------|---------|
| NR | Laufende Nummer | 1, 2, 3... |
| Haltungsname | Eindeutige Bezeichnung | "07_1028024-10417" |
| Strasse | Strassenname | "Hauptstrasse" |
| Rohrmaterial | Material des Rohrs | Beton, PVC, Steinzeug |
| DN_mm | Nennweite in Millimeter | 300, 400, 600 |
| Nutzungsart | Art des Abwassers | Schmutzwasser, Regenwasser, Mischwasser |
| Haltungslaenge_m | Laenge in Metern | 45.30 |
| Zustandsklasse | Gesamtzustand (0-5) | 0=neuwertig, 5=sofort sanieren |
| Primaere_Schaeden | Hauptschaeden als Codes | "BAB, BCA" |
| Sanieren_JaNein | Sanierungsempfehlung | Ja / Nein |
| Kosten | Geschaetzte Kosten | CHF 12'500 |
| Eigentuemer | Wem gehoert die Leitung | "Gemeinde Altdorf" |

### Bedienung der Tabelle

- **Sortieren**: Klicke auf die Spaltenueberschrift
- **Suchen**: Nutze das Suchfeld oben — sucht in allen Spalten
- **Spalten ein/ausblenden**: Rechtsklick auf die Spaltenueberschrift
- **Zeilenhoehe anpassen**: Zoom-Regler (0.5× bis 2.0×)
- **Detail-Ansicht**: Doppelklick auf eine Zeile oeffnet die Detailansicht

### Video abspielen

Jede Haltung kann ein zugeordnetes Inspektionsvideo haben:
- Klicke auf das **Video-Symbol** in der Zeile
- Der integrierte Player oeffnet sich
- Im Player siehst du den Meterstand und das OSD (On-Screen Display)

### Detailansicht

Die Detailansicht zeigt alle Felder einer Haltung:
- Stammdaten (Name, Material, Durchmesser, Laenge)
- Inspektionsdaten (Datum, Richtung, Zustand)
- Schaeden und Protokolleintraege
- VSA-Bewertung (Zustandsnoten D/S/B)
- Kosten und Sanierungsempfehlungen
- Zugeordnete Medien (Videos, Fotos, PDFs)

---

## 6. Schaechte

### Was ist ein Schacht?

Ein **Schacht** ist der Zugang zum Kanal — der Anfangs- oder Endpunkt einer Haltung.
Schaechte werden separat inspiziert und bewertet.

### Die Schaechte-Tabelle

Aehnlich wie die Haltungs-Tabelle, aber mit schacht-spezifischen Feldern:
- Schachtbezeichnung
- Schachttyp
- Schachttiefe
- Zustand
- Zugeordnete Haltungen

### Bedienung

- Sortieren, Suchen, Filtern wie bei Haltungen
- **Neuen Schacht hinzufuegen**: Button oben
- **Schacht loeschen**: Zeile auswaehlen, Loeschen-Button
- **Reihenfolge aendern**: Per Drag-and-drop

---

## 7. Inspektionsprotokolle

### Was ist ein Protokoll?

Ein **Inspektionsprotokoll** dokumentiert alle Beobachtungen die bei einer Kamerafahrt
gemacht wurden. Jeder Eintrag hat:

| Feld | Beschreibung | Beispiel |
|------|-------------|---------|
| **Code** | VSA/EN-Schadenscode | BAB (Riss) |
| **Beschreibung** | Was wurde gesehen | "Laengsriss 12-3 Uhr" |
| **Meterstart** | Position im Rohr | 12.50 m |
| **Meterende** | Ende (bei Streckenschaeden) | 14.80 m |
| **Video-Zeitstempel** | Stelle im Video | 00:01:25 |
| **Fotos** | Zugeordnete Bilder | schaden_1.jpg |

### Schadenscodierung (VSA-KEK / EN 13508-2)

Die Codes sind hierarchisch aufgebaut:

**Bestandsaufnahme (BC-Gruppe):**
| Code | Bedeutung |
|------|-----------|
| BCD | Rohranfang — Kamera faehrt ins Rohr ein |
| BCE | Rohrende — Endschacht erreicht |
| BCA | Seitlicher Anschluss — Oeffnung in der Rohrwand |
| BCC | Bogen — Richtungsaenderung |

**Strukturelle Schaeden (BA-Gruppe):**
| Code | Bedeutung | Untertypen |
|------|-----------|-----------|
| BAB | Riss | A=laengs, B=quer, C=diagonal, D=ringfoermig |
| BAC | Bruch | A=partiell, B=total |
| BAF | Deformation | A=vertikal, B=horizontal |
| BAH | Versatz | A=vertikal, B=horizontal |
| BAI | Einragender Stutzen | — |

**Betriebliche Stoerungen (BB-Gruppe):**
| Code | Bedeutung |
|------|-----------|
| BBA | Inkrustation / Kalkablagerung |
| BBB | Wurzeleinwuchs |
| BBC | Ablagerung (Sand, Kies, verfestigt) |
| BBD | Eindringender Boden |

### Uhrlage-System

Die Position im Rohr wird als Uhr angegeben:
- **12 Uhr** = Scheitel (oben)
- **3 Uhr** = rechts
- **6 Uhr** = Sohle (unten)
- **9 Uhr** = links

### Schweregrad (Severity)

| Stufe | Bedeutung | Massnahme |
|-------|-----------|-----------|
| 1 | Optischer Mangel | Keine Massnahme noetig |
| 2 | Leichter Schaden | Beobachten |
| 3 | Mittlerer Schaden | Sanierung mittelfristig |
| 4 | Schwerer Schaden | Sanierung kurzfristig |
| 5 | Kritischer Schaden | Sofortmassnahme |

### Protokoll bearbeiten

1. Oeffne die Haltung in der Detailansicht
2. Klicke auf **"Protokoll bearbeiten"**
3. Du kannst:
   - Eintraege **hinzufuegen** (mit Code-Picker)
   - Eintraege **aendern** (Code, Beschreibung, Meter)
   - Eintraege **loeschen**
   - Fotos **zuordnen** oder **entfernen**
4. Aenderungen werden in der **Protokoll-Historie** gespeichert

### Nachprotokoll und Neuprotokoll

| Modus | Wann verwenden | Was passiert |
|-------|---------------|-------------|
| **Nachprotokoll** | Erneute Inspektion derselben Haltung | Neue Revision basierend auf der letzten |
| **Neuprotokoll** | Komplette Neuinspektion | Leere Revision, altes Protokoll bleibt als Referenz |
| **Original wiederherstellen** | Fehlerhafte Aenderungen rueckgaengig | Zurueck zum Import-Stand |

---

## 8. KI-gestuetzte Videoanalyse

### Ueberblick

SewerStudio analysiert Inspektionsvideos automatisch mit mehreren KI-Modellen:

```
Video-Frame (Bild aus dem Video)
     |
     v
YOLO (Vorfilter) ──> Ist der Frame relevant?
     |                     |
    JA                   NEIN → Frame ueberspringen
     |
     v
Grounding DINO ──> Was ist auf dem Bild zu sehen?
     |
     v
SAM ──> Wo genau ist der Schaden? (pixelgenaue Maske)
     |
     v
Qwen Vision (8B) ──> Welcher VSA-Code? Wie schwer?
     |
     v
Qualitaetsprüfung ──> Gruen / Gelb / Rot?
     |                              |
   GRUEN                         GELB
   (sicher)                   (unsicher)
     |                              |
     v                              v
  Akzeptiert                 Eskalation auf Qwen 32B
                              (zweite Meinung)
```

### KI-Modelle im Detail

| Modell | Aufgabe | GPU-Speicher | Geschwindigkeit |
|--------|---------|-------------|----------------|
| **YOLO11M** | Vorfilter: relevant oder nicht? | 2.5 GB | 100-200 ms/Frame |
| **Grounding DINO 1.5** | Semantische Erkennung | 8 GB | 200-400 ms/Frame |
| **SAM 3** | Pixelgenaue Segmentierung | 6 GB | 150-300 ms/Frame |
| **Qwen3-VL 8B** | VSA-Code + Schweregrad | 6-8 GB | 800-1200 ms/Frame |
| **Qwen3-VL 32B** | Zweite Meinung bei Unsicherheit | 22 GB | Nur bei Eskalation |

### Videoanalyse starten

1. Oeffne die **Video-Analyse-Pipeline** (Menue oder Tastenkuerzel)
2. Waehle die Haltungen die analysiert werden sollen
3. Klicke auf **"Analyse starten"**
4. Die KI verarbeitet jeden Frame des Videos (alle 1.5 Sekunden ein Frame)
5. Erkannte Schaeden werden automatisch als Protokolleintraege vorgeschlagen
6. Du kannst die Vorschlaege annehmen, korrigieren oder ablehnen

### Qualitaetsprüfung (Quality Gate)

Jeder KI-Vorschlag bekommt eine Vertrauensnote:

| Zone | Vertrauen | Was passiert |
|------|-----------|-------------|
| **Gruen** | ueber 75% | Wird automatisch akzeptiert |
| **Gelb** | 45-75% | Wird zur manuellen Pruefung markiert + Eskalation auf groesseres Modell |
| **Rot** | unter 45% | Wird abgelehnt, Frame wird nochmals analysiert |

Die Vertrauensnote wird aus mehreren Signalen berechnet:
- YOLO-Konfidenz
- DINO-Konfidenz
- SAM-Maskenstabilitaet
- Qwen-Textanalyse
- Aehnlichkeit zur Knowledge-Base
- Plausibilitaet (passt der Schaden zum Kontext?)

### Typische Analyse-Zeiten

| Video-Laenge | Anzahl Frames | Dauer (Workstation) |
|-------------|--------------|-------------------|
| 100 m Haltung (~3 min) | ~67 Frames | 1.5 - 2.5 Minuten |
| 300 m Haltung (~10 min) | ~200 Frames | 4 - 7 Minuten |
| Ganzes Projekt (50 Haltungen) | ~3'000 Frames | 1 - 3 Stunden |

---

## 9. Codiermodus

### Was ist der Codiermodus?

Der **Codiermodus** ist eine interaktive Arbeitsweise: Du faehrst Meter fuer Meter durch
das Video, und die KI schlaegt in Echtzeit Schadenscodes vor.

### So funktioniert es

1. Oeffne eine Haltung und klicke auf **"Codiermodus starten"**
2. Das Video startet am Rohranfang (Meter 0.00)
3. Du navigierst durch das Video — die KI analysiert jeden Frame
4. Bei erkannten Schaeden erscheint ein **Vorschlag**:
   - VSA-Code (z.B. "BAB" = Riss)
   - Uhrlage (z.B. "12-3 Uhr")
   - Schweregrad (1-5)
   - Vertrauenswert (0-100%)
5. Du kannst den Vorschlag:
   - **Annehmen** — Wird ins Protokoll uebernommen
   - **Korrigieren** — Code oder Beschreibung aendern, dann uebernehmen
   - **Ablehnen** — Wird verworfen
6. Korrekturen fliessen zurueck ins **Trainingssystem** (die KI lernt daraus)

### Messwerkzeuge im Codiermodus

Im Video kannst du Schaeden direkt vermessen:

| Werkzeug | Zweck |
|----------|-------|
| **Linie** | Abstand messen (z.B. Risslaenge) |
| **Bogen** | Kreisabschnitt messen (z.B. Rohrumfang) |
| **Rechteck** | Flaeche markieren |
| **Ellipse** | Ovale Bereiche markieren (z.B. Anschluesse) |
| **Kreuzprofil** | Rohrquerschnitt einzeichnen |
| **Lineal** | Praezise Laengenmessung |
| **Winkelmesser** | Winkel bestimmen |
| **Wasserstand** | Fuellhoehe markieren |
| **Freihand** | Beliebige Form zeichnen |

### Rohrkalibrierung

Damit die Messungen in Millimeter stimmen, muss das Rohr kalibriert werden:
1. Die KI versucht den Rohrdurchmesser automatisch zu erkennen
2. Falls noetig: Manuell den Rohrdurchmesser (DN) eingeben
3. Danach rechnet SewerStudio Pixel in Millimeter um

---

## 10. VSA-KEK Zustandsbewertung

### Was ist VSA-KEK?

VSA-KEK ist der Schweizer Standard fuer die Zustandsbewertung von Kanalisationen.
Jede Haltung wird nach drei Kriterien bewertet:

| Kriterium | Abkuerzung | Was wird geprueft |
|-----------|-----------|-------------------|
| **Dichtheit** | D | Ist das Rohr dicht? (Risse, Brueche, undichte Stellen) |
| **Standsicherheit** | S | Ist das Rohr stabil? (Deformation, Einsturz) |
| **Betriebssicherheit** | B | Kann das Wasser fliessen? (Ablagerungen, Wurzeln) |

### Zustandsklassen

| Klasse | Bedeutung | Farbe | Massnahme |
|--------|-----------|-------|-----------|
| **0** | Neuwertig / kein Schaden | Gruen | Keine |
| **1** | Leichte Maengel | Hellgruen | Beobachten |
| **2** | Mittlere Schaeden | Gelb | Mittelfristig sanieren |
| **3** | Erhebliche Schaeden | Orange | Kurzfristig sanieren |
| **4** | Schwere Schaeden | Rot | Sofort sanieren |

### Automatische Bewertung

1. Gehe zur Seite **"VSA"**
2. SewerStudio berechnet automatisch die Zustandsklassen D, S und B
3. Jeder Schadenscode wird einer oder mehreren Zustandsklassen zugeordnet
4. Die schlechteste Note bestimmt die Gesamtbewertung
5. Ergebnis: **Dringlichkeitszahl** (DZ) die angibt wie dringend saniert werden muss

### Sanierungsempfehlungen

Basierend auf der VSA-Bewertung schlaegt SewerStudio Massnahmen vor:

| Schadenstyp | Moegliche Massnahme |
|------------|-------------------|
| Riss / Bruch | Kurzliner, Inliner, Robotersanierung |
| Deformation | Erneuerung / Neubau |
| Versatz | Reparatur-Manschette |
| Wurzeleinwuchs | Fraesarbeiten, Robotersanierung |
| Ablagerung | Spuelung, Reinigung |
| Undichter Anschluss | Anschluss verpressen |

---

## 11. Kostenberechnung

### Kosten berechnen

1. Oeffne die **Kostenberechnung** (Menueknopf oder ueber Haltungs-Detail)
2. Waehle die Haltung oder mehrere Haltungen
3. SewerStudio berechnet automatisch basierend auf:
   - Art der Massnahme (Inliner, Kurzliner, Neubau etc.)
   - Rohrdurchmesser (DN)
   - Haltungslaenge
   - Anzahl Anschluesse
   - Weitere Parameter

### Preiskatalog

Die Preise kommen aus dem konfigurierbaren **Preiskatalog**:
- Standardpreise sind vorgeladen (Schweizer Marktpreise in CHF)
- Du kannst eigene Preise eintragen und pflegen
- Preise koennen nach DN-Bereich gefiltert werden (z.B. DN 150-300)

### Massnahmen-Vorlagen

Massnahmen bestehen aus mehreren Positionen:

**Beispiel: Inliner-Sanierung DN 300**
| Position | Einheit | Menge | Einheitspreis | Total |
|----------|---------|-------|--------------|-------|
| Spuelung | lfm | 45.0 | CHF 8.50 | CHF 382.50 |
| Kamera-Inspektion | Stk | 1 | CHF 350.00 | CHF 350.00 |
| GFK-Inliner DN 300 | lfm | 45.0 | CHF 185.00 | CHF 8'325.00 |
| Anschluesse oeffnen | Stk | 3 | CHF 450.00 | CHF 1'350.00 |
| Nachinspektion | Stk | 1 | CHF 250.00 | CHF 250.00 |
| **Zwischentotal** | | | | **CHF 10'657.50** |
| MwSt (8.1%) | | | | CHF 863.26 |
| **Total inkl. MwSt** | | | | **CHF 11'520.76** |

### Kombinierte Offerte

Fuer mehrere Haltungen gleichzeitig kann eine **kombinierte Offerte** erstellt werden:
- Alle Massnahmen zusammengefasst
- Rabatt-Optionen (Mengenrabatt)
- Skonto
- Gesamttotal mit MwSt

---

## 12. Eigendevis

### Was ist ein Eigendevis?

Ein **Eigendevis** (Kostenvoranschlag) ist eine detaillierte Kostenschaetzung die nach
Gewerken aufgeteilt ist:

| Gewerk | Was ist enthalten |
|--------|------------------|
| **Baumeister** | Grabarbeiten, Schachtarbeiten, Belagsarbeiten, Abbruch |
| **Rohrleitungsbau** | Inliner, Kurzliner, Robotersanierung, Rohrersatz |

### Eigendevis erstellen

1. Gehe zur Seite **"Eigendevis"**
2. SewerStudio erstellt automatisch einen Devis basierend auf:
   - Den Schaeden aller Haltungen
   - Den empfohlenen Massnahmen
   - Den Preisen aus dem Katalog
3. Das Ergebnis ist eine hierarchische Kostenaufstellung
4. Export als **Excel** (mit oder ohne Preise)

### Automatische Zuordnung

SewerStudio ordnet Schaeden automatisch den richtigen Massnahmen zu:

| Schadenscode | Zustandsklasse | DN-Bereich | Empfohlene Massnahme |
|-------------|---------------|-----------|---------------------|
| BAB (Riss) | 3-4 | DN 150-400 | Kurzliner |
| BAB (Riss) | 3-4 | DN 400+ | Robotersanierung |
| BAC (Bruch) | 4-5 | alle | Leitungsersatz |
| BAF (Deformation) | 3-4 | alle | Erneuerung |
| BBB (Wurzeln) | 2-3 | alle | Robotersanierung |

---

## 13. Hydraulikberechnung

### Was wird berechnet?

SewerStudio berechnet die hydraulische Leistungsfaehigkeit nach **DWA-A 110**
(fuer Kreisprofile):

| Berechnung | Was es bedeutet |
|-----------|----------------|
| **Teilfuellung** | Wie viel Wasser fliesst bei bestimmtem Wasserstand |
| **Vollfuellung** | Maximale Kapazitaet des Rohrs |
| **Fliessgeschwindigkeit** | Wie schnell das Wasser fliesst (m/s) |
| **Reynolds-Zahl** | Laminare oder turbulente Stroemung |
| **Froude-Zahl** | Stroemungsart (schiessendes oder stroemendes Wasser) |
| **Schubspannung** | Kraft auf die Rohrsohle (wichtig fuer Ablagerungen) |
| **Ablagerungsgefahr** | Risiko dass sich Sediment ablagert |

### Parameter eingeben

| Parameter | Beschreibung | Beispiel |
|-----------|-------------|---------|
| **DN** | Rohrdurchmesser in mm | 300 |
| **Material** | Rohrmaterial (beeinflusst Rauheit) | Beton, PVC, Steinzeug |
| **Gefaelle** | Rohrneigung in Promille | 5.0 ‰ |
| **Wasserstand** | Fuellhoehe in mm oder % | 150 mm oder 50% |
| **Temperatur** | Wassertemperatur | 10°C |
| **Regenart** | Misch- oder Trennsystem | Mischwasser |

### Ergebnis drucken

Die Hydraulik-Ergebnisse koennen als **PDF** gedruckt werden mit:
- Teilfuellungskurve
- Vollfuellungswerte
- Kennzahlen
- Ablagerungsgefahr-Bewertung
- Auslastungsgrad

---

## 14. Datenexport und Druckcenter

### Excel-Export

1. Gehe zur Seite **"Export"**
2. Waehle was du exportieren willst:
   - **Haltungen** → Excel-Datei mit allen Haltungsdaten
   - **Schaechte** → Excel-Datei mit allen Schachtdaten
3. Eine Vorlage (Template) bestimmt das Format
4. Die exportierte Datei wird im Projektordner gespeichert

### Druckcenter (Builder-Seite)

Das Druckcenter ist fuer umfangreiche PDF-Berichte:

1. **Filter setzen**: Nach Eigentuemer, Ausfuehrendem, Material, Jahr, Status etc.
2. **Daten pruefen**: Tabelle zeigt alle gefilterten Haltungen
3. **Statistiken ansehen**: Kosten, Sanierungsrate, Verteilung nach Ausfuehrendem
4. **PDF erstellen**: Klicke auf "PDF exportieren"

### Haltungsdossier (Detaillierter Bericht)

Fuer einzelne Haltungen kann ein **Dossier** erstellt werden mit:
- Deckblatt mit Projektdaten
- Haltungsprotokoll mit allen Schaeden
- Schachtprotokolle
- Hydraulikberechnung
- Fotogalerie

### Medienverteilung

SewerStudio kann Dateien automatisch in Gemeindeordner verteilen:
- **PDFs**: Inspektionsprotokolle pro Haltung
- **Videos**: Inspektionsvideos
- **TXT-Dateien**: Textprotokolle
- Fortschrittsanzeige und Ergebnis-Zusammenfassung

---

## 15. Medienverwaltung

### Video-Zuordnung

Jede Haltung kann ein Video, Fotos und PDFs zugeordnet haben.
SewerStudio versucht die Zuordnung automatisch anhand des Dateinamens.

### Medienkonflikte loesen

Wenn die automatische Zuordnung nicht klappt, hilft die Seite **"Medienkonflikte"**:

| Konflikttyp | Bedeutung | Loesung |
|------------|-----------|---------|
| **Fehlend** | Kein Video gefunden | Manuell zuordnen oder suchen |
| **Mehrdeutig** | Mehrere Videos passen | Das richtige auswaehlen |
| **Falsch** | Video wurde falsch zugeordnet | Korrigieren |

Die **Mediensuche** durchsucht alle verfuegbaren Dateien und schlaegt Kandidaten vor.
Die KI lernt aus manuellen Zuordnungen und kann aehnliche Konflikte kuenftig automatisch loesen.

---

## 16. Trainingscenter

### Wie lernt die KI?

SewerStudio hat ein eingebautes **Selbstlernsystem**:

1. Die KI analysiert ein Video und macht Vorschlaege
2. Du korrigierst die Vorschlaege (Code aendern, Schaden bestätigen/ablehnen)
3. Jede Korrektur wird als **Trainingsbeispiel** gespeichert
4. Die gespeicherten Beispiele verbessern zukuenftige Vorhersagen

### Trainingscenter oeffnen

1. Menue → **"Trainingscenter"**
2. Du siehst alle gesammelten Trainingsbeispiele
3. Filter nach Code, Kategorie, Qualitaet
4. Beispiele pruefen und genehmigen oder ablehnen
5. **Knowledge-Base neu aufbauen** um die Aenderungen zu aktivieren

### Knowledge-Base

Die **Knowledge-Base** ist die Wissensdatenbank der KI:

| Inhalt | Beschreibung |
|--------|-------------|
| **Trainingsbeispiele** | Bilder mit zugeordneten VSA-Codes |
| **Embeddings** | Mathematische Darstellung jedes Beispiels (fuer Aehnlichkeitssuche) |
| **Few-Shot-Beispiele** | Handverlesene Musterbeispiele fuer besonders gute Ergebnisse |

### Statistiken

| Kennzahl | Aktuell |
|----------|---------|
| Trainingsbeispiele | ~21'948 |
| Abgedeckte VSA-Codes | 289 |
| Extrahierte Video-Frames | ~24'500 |

---

## 17. Einstellungen

### Darstellung

| Einstellung | Optionen |
|------------|---------|
| **Design** | Dunkel / Hell |
| **Schriftgroesse** | Standard, Gross |

### Video-Player

| Einstellung | Standard | Beschreibung |
|------------|---------|-------------|
| Hardware-Dekodierung | Ein | Nutzt GPU fuer Videowiedergabe |
| Frame-Dropping | Ein | Ueberspringt Frames bei Uebelastung |
| Datei-Cache | 3000 ms | Wie viel Video vorgeladen wird |
| Netzwerk-Cache | 3000 ms | Fuer Netzwerk-Videos |
| Codec-Threads | 2 | Anzahl CPU-Kerne fuer Video |
| Ausgabemethode | Direct3D 11 | Video-Rendering-Methode |

### KI-Einstellungen

| Einstellung | Standard | Beschreibung |
|------------|---------|-------------|
| Ollama-URL | http://localhost:11434 | Adresse des KI-Servers |
| Vision-Modell | Auto | Automatische Modellerkennung |
| Text-Modell | qwen2.5:3b | Fuer Code-Mapping |
| Embedding-Modell | nomic-embed-text | Fuer Knowledge-Base |
| Sidecar-URL | http://localhost:8100 | Python-Sidecar-Adresse |
| Sidecar Auto-Start | Aus | Sidecar automatisch starten |
| YOLO-Konfidenz | 0.25 | Mindest-Vertrauen fuer Erkennung |
| DINO-Box-Schwelle | 0.30 | Mindest-Vertrauen fuer Boxen |
| DINO-Text-Schwelle | 0.25 | Mindest-Vertrauen fuer Text |
| Standard-DN | 300 mm | Angenommener Rohrdurchmesser |
| Timeout | 5 Minuten | Maximale Wartezeit fuer KI-Antwort |

### Speichern und Sicherung

| Einstellung | Standard | Beschreibung |
|------------|---------|-------------|
| Auto-Save-Modus | Bei jeder Aenderung | Wann automatisch gespeichert wird |
| Wiederherstellungspunkte | Ein | Sicherungskopien erstellen |
| FFmpeg-Pfad | Im PATH | Pfad zu FFmpeg (fuer Video-Extraktion) |
| pdftotext-Pfad | tools/ | Pfad zu pdftotext (fuer PDF-Import) |

---

## 18. Technische Spezifikationen

### Architektur

| Komponente | Technologie |
|-----------|-------------|
| **UI-Framework** | WPF (.NET 10, Windows) |
| **Architektur** | MVVM (Model-View-ViewModel) |
| **Datenformat** | JSON (Projektdateien) |
| **KI-Backend** | Ollama (lokal), Python FastAPI Sidecar |
| **Datenbank** | SQLite (Knowledge-Base) |
| **Video** | libVLC (Wiedergabe), FFmpeg (Extraktion) |
| **PDF** | Playwright + Scriban (Generierung), pdftotext (Import) |
| **Excel** | ClosedXML |

### KI-Pipeline Architektur

```
┌──────────────────────────────────────────────────┐
│                 C# WPF Anwendung                  │
│  ┌────────────┐  ┌──────────┐  ┌──────────────┐ │
│  │ VideoAnalyse│  │ Codier-  │  │ Knowledge-   │ │
│  │ Pipeline    │  │ modus    │  │ Base Manager │ │
│  └──────┬─────┘  └────┬─────┘  └──────┬───────┘ │
│         │              │               │          │
│         v              v               v          │
│  ┌─────────────────────────────────────────────┐ │
│  │          QualityGateService                  │ │
│  │    (Gruen/Gelb/Rot Entscheidung)            │ │
│  └──────────────┬──────────────────────────────┘ │
└─────────────────┼────────────────────────────────┘
                  │
    ┌─────────────┼────────────────┐
    │             │                │
    v             v                v
┌────────┐  ┌─────────┐  ┌──────────────┐
│ Ollama │  │ Python  │  │   SQLite     │
│ Server │  │ Sidecar │  │ Knowledge-DB │
│        │  │         │  │              │
│ Qwen   │  │ YOLO    │  │ Embeddings   │
│ 8B/32B │  │ DINO    │  │ Samples      │
│        │  │ SAM     │  │ Versionen    │
└────────┘  └─────────┘  └──────────────┘
Port 11434   Port 8100    Lokale Datei
```

### GPU-Speicher Verteilung (Workstation-Modus)

| Modell | VRAM | Status |
|--------|------|--------|
| YOLO11M (TensorRT) | 1.5 GB | Permanent geladen |
| Grounding DINO 1.5 | 3 GB | Permanent geladen (pre-warmed) |
| SAM 3 | 3 GB | Permanent geladen (pre-warmed) |
| Qwen3-VL 8B | 10 GB | Permanent geladen (keep_alive=-1) |
| **Normal-Total** | **17.5 GB** | |
| Qwen3-VL 32B (Eskalation) | 22 GB | Nur bei Bedarf geladen |
| **Eskalation-Total** | **29.5 GB** | Kurzzeitig, max 32 GB |

### Dateistruktur eines Projekts

```
MeinProjekt/
├── projekt.json              # Projektdaten
├── haltungen/                # Haltungsdaten
│   ├── haltung_001.json
│   └── ...
├── protokolle/               # Inspektionsprotokolle
├── medien/                   # Videos, Fotos
│   ├── videos/
│   └── fotos/
└── export/                   # Exportierte Berichte
```

### Umgebungsvariablen

| Variable | Beschreibung |
|----------|-------------|
| `SEWERSTUDIO_AI_ENABLED` | KI ein/aus (true/false) |
| `SEWERSTUDIO_AI_VISION_MODEL` | Vision-Modell (auto oder Modellname) |
| `SEWERSTUDIO_AI_TEXT_MODEL` | Text-Modell (Standard: qwen2.5:3b) |
| `SEWERSTUDIO_AI_EMBED_MODEL` | Embedding-Modell |
| `SEWERSTUDIO_OLLAMA_URL` | Ollama-Server URL |
| `SEWERSTUDIO_SIDECAR_URL` | Sidecar-Server URL |
| `SEWERSTUDIO_PIPELINE_MODE` | auto / multimodel / ollamaonly |
| `SEWERSTUDIO_YOLO_CONFIDENCE` | YOLO-Schwellenwert (0.0-1.0) |
| `SEWERSTUDIO_PIPE_DIAMETER_MM` | Standard-Rohrdurchmesser |
| `SEWERSTUDIO_AI_REFERENCE_MODEL` | Eskalations-Modell |
| `SEWERSTUDIO_AI_TIMEOUT_MIN` | Timeout in Minuten |
| `SEWERSTUDIO_FFMPEG` | FFmpeg-Pfad |

### Sidecar API-Endpunkte

| Endpunkt | Methode | Beschreibung |
|----------|---------|-------------|
| `/health` | GET | Statusabfrage |
| `/api/yolo` | POST | YOLO-Objekterkennung |
| `/api/dino` | POST | DINO-Semantik-Erkennung |
| `/api/sam` | POST | SAM-Segmentierung |
| `/api/enhance` | POST | Bildverbesserung (Super-Resolution) |
| `/api/video` | POST | Video-Verarbeitung |
| `/api/training` | POST | Trainingsexport |

---

## 19. Tastenkuerzel und Tipps

### Allgemeine Tastenkuerzel

| Taste | Funktion |
|-------|----------|
| **Strg+S** | Projekt speichern |
| **Strg+O** | Projekt oeffnen |
| **Strg+F** | Suchen in der aktuellen Tabelle |
| **F5** | Daten aktualisieren |

### Tipps fuer effizientes Arbeiten

1. **Vorschau nutzen**: Vor jedem Import die Vorschau pruefen — spart Aerger
2. **KI korrigieren**: Je mehr du die KI korrigierst, desto besser wird sie
3. **Codiermodus nutzen**: Schneller als manuelles Eintippen
4. **Mehrere Monitore**: Das Floating Grid erlaubt Tabellen auf zweitem Bildschirm
5. **Wiederherstellungspunkte**: Halte sie eingeschaltet — du weisst nie wann du sie brauchst
6. **Knowledge-Base pflegen**: Regelmässig das Trainingscenter pruefen und Beispiele genehmigen

---

## 20. Fehlerbehebung

### Haeufige Probleme

| Problem | Ursache | Loesung |
|---------|---------|---------|
| KI antwortet nicht | Ollama laeuft nicht | Ollama starten: `ollama serve` |
| YOLO/DINO/SAM fehlt | Sidecar laeuft nicht | Sidecar starten: `start_sidecar.ps1` |
| Video zeigt schwarzes Bild | Codec fehlt | VLC/libVLC pruefen |
| Import schlaegt fehl | Falsches Format | Dateiformat pruefen, Vorschau nutzen |
| GPU-Speicher voll | Zu viele Modelle geladen | Modelle entladen ueber Ollama-Manager |
| Langsame Analyse | Kein TensorRT | YOLO-Engine wird beim ersten Mal kompiliert |
| Excel-Export leer | Keine Daten geladen | Zuerst Daten importieren |
| PDF-Import unvollstaendig | pdftotext fehlt | pdftotext installieren und Pfad konfigurieren |

### Diagnose-Seite

Unter **"Diagnose"** findest du:
- Echtzeit-Log der Anwendung
- Fehler- und Warnmeldungen
- KI-Aktivitaeten (welches Modell gerade arbeitet)
- System-Monitor (CPU, RAM, GPU-Auslastung)

### KI-Status pruefen

In der Statusleiste unten siehst du:
- **Gruener Punkt**: KI einsatzbereit
- **Gelber Punkt**: KI laed gerade
- **Roter Punkt**: KI nicht verfuegbar
- Anzahl Samples und Codes in der Knowledge-Base

---

*SewerStudio 3.1 — Entwickelt fuer die Schweizer Kanalinspektion*
*VSA-KEK 2023 | EN 13508-2 | DWA-A 110*
