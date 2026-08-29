# Auftrag: SIA405-Normbegriffe in SchachtPro

## Worum es geht

SchachtPro exportiert Schachtprotokolle als `.spro`-Archiv und als QR-Code
(Format `SPQR1`). Diese Daten werden in SewerStudio eingelesen und sollen von
dort als **SIA405-XTF** an den Kanton weitergegeben werden.

Das scheitert heute an den Begriffen. SchachtPro schreibt die Wörter, die in
QGIS angezeigt werden — die XTF verlangt andere, festgelegte Werte:

| SchachtPro heute | SIA405 verlangt |
|---|---|
| `Kontrollschacht` | `Kontroll_Einsteigschacht` |
| `Fertigbetonelement` | `Beton` |
| `Rund` | (kein Formfeld — siehe unten) |
| `1.00` (Meter) | `1000` (Millimeter) |

Ein von Hand getippter oder falsch geschriebener Wert wird von einem
INTERLIS-Prüfer abgelehnt. Die Enum-Werte sind **gross-/kleinschreibungsgenau**
und enthalten teils Schreibweisen, die wie Tippfehler aussehen, aber so
vorgeschrieben sind (`Polyvinilchlorid` mit i, nicht y).

## WICHTIG: erst prüfen, nicht ändern

Ändere in diesem ersten Durchgang **keinen Code**. Liefere einen Bericht.
Der Grund: Ich weiss nicht, ob jede SchachtPro-Auswahl überhaupt ein
Norm-Gegenstück hat. Wo zwei SchachtPro-Werte auf einen Normwert fallen,
geht beim Export Information verloren — das muss ich entscheiden, nicht du.

## Was du tun sollst

### 1. Bestandsaufnahme

Finde im Code, wo die Auswahllisten dieser Felder definiert sind, und liste
**alle** möglichen Werte auf — nicht nur die, die in einem Beispiel vorkommen:

- `schachtFunktion`
- `materialSchacht`
- `schachtform`
- `medium`
- `deckelMaterial`, `deckelform`, `deckelTyp`, `belastungsklasse`
- `schachthalsForm`, Konus-Felder
- bei den Anschlüssen: `typ`, `material`, `rohrform`, `richtung`, `medium`

Nenne je Feld: Datei, Zeile, Speicherform (String? Enum? String-Resource?),
und ob der gespeicherte Wert derselbe ist wie der angezeigte.

Prüfe ausserdem: Werden diese Werte **lokalisiert** (strings.xml)? Falls ja,
hängt der exportierte Wert an der Sprache des Geräts — das wäre ein eigener
Fehler und muss in den Bericht.

### 2. Abgleich mit der Norm

Vergleiche jeden gefundenen Wert mit den Listen unten. Ordne jeden in genau
eine Kategorie ein:

- **eindeutig** — genau ein Normwert passt
- **mehrdeutig** — mehrere Normwerte kämen infrage
- **verlustig** — mehrere SchachtPro-Werte fallen auf denselben Normwert
- **ohne Gegenstück** — die Norm kennt das nicht

### 3. Einheiten

Prüfe alle Massfelder: `dimension`, `laenge`, `breite`, `tiefe`,
`deckelDurchmesser`, `rahmenDeckelHoehe`, `schachthalsDimension`,
Konus-Masse, sowie `dn`/`breite`/`hoehe` bei den Anschlüssen.

Für jedes: In welcher Einheit wird gespeichert? Meter oder Millimeter?
Mit welchem Dezimaltrenner? Die Norm verlangt **Millimeter als ganze Zahl**.
Ein Faktor-1000-Fehler schreibt 1 mm statt 1 m — das muss sicher sein.

### 4. Vorschlag

Schlage vor, wie der Normwert in den Export kommt. Meine Vorgabe:
**additiv**, ohne die bestehende Speicherung zu ändern und ohne Migration
vorhandener Protokolle. Also zum Beispiel ein zusätzliches Feld je Wert im
`.spro`-JSON (`schachtFunktionSia405` neben `schachtFunktion`), nicht ein
Ersetzen. Begründe, wenn du einen besseren Weg siehst.

Sag ausdrücklich, welche Felder du **nicht** abbilden kannst.

---

## Die verbindlichen SIA405-Werte

Quelle: `SIA405_ABWASSER_2020_LV95`, Exportdatei von Abwasser Uri
(AWU_XTF_Exporter_QGIS), vollständig ausgezählt über 1544 Objekte.
Diese Schreibweise ist verbindlich, Zeichen für Zeichen.

**Normschacht.Funktion**
```
Kontroll_Einsteigschacht
Schlammsammler
Einlaufschacht
Pumpwerk
Oelabscheider
```
Achtung: `Kontroll_Einsteigschacht` ist EIN Wert für Kontrollschacht UND
Einsteigschacht. Wenn SchachtPro beide getrennt führt, ist das ein
Verlustfall — melde ihn, wandle ihn nicht still um.

**Normschacht.Material**
```
Beton
```
Im ganzen Bestand kommt nur dieser eine Wert vor. Ob die Norm weitere
erlaubt, kann ich aus der Exportdatei nicht sagen — melde, welche Werte
SchachtPro anbietet, dann kläre ich das gegen die Modelldatei.

**Normschacht.Status**
```
in_Betrieb
ausser_Betrieb
```

**Normschacht.Sanierungsbedarf**
```
keiner
kurzfristig
mittelfristig
langfristig
dringend
unbekannt
```

**Haltung.Material** (für die Anschlussrohre relevant)
```
Kunststoff_Polyvinilchlorid      (mit i — nicht Polyvinylchlorid)
Kunststoff_Hartpolyethylen
Kunststoff_Polyethylen
Kunststoff_Polypropylen
Beton_Normalbeton
Beton_unbekannt
unbekannt                        (klein geschrieben)
```
SchachtPro schreibt hier heute `Polyethylen (PE)`, `Polyvinylchlorid (PVC)`,
`Hart-Polyethylen (HDPE)` — also mit Klammerzusatz. Keiner dieser Werte ist
normgültig.

**Kanal.Nutzungsart_Ist** (entspricht `medium`)
```
Mischabwasser
Schmutzabwasser
Niederschlagsabwasser
```
`Niederschlagsabwasser` gilt ab Modell 2020. Ältere Modelle verlangen
stattdessen `Regenabwasser`; beide schliessen sich aus. Speichere den
2020er Wert.

**Haltung.Lagebestimmung**
```
genau
ungenau
```

**Rohrprofil.Profiltyp**
```
Kreisprofil
```

**Masse**
```
Lichte_Hoehe     Millimeter, ganze Zahl     im Bestand 110 bis 315
Dimension1/2     Millimeter, ganze Zahl     im Bestand 500 bis 1900
Baujahr          Jahreszahl                 im Bestand 1960 bis 2025
Sohlenkote       Meter mit Dezimalstellen   im Bestand 1077 bis 1137
```

**Form runder Schächte**
Der `Normschacht` hat in dieser Datei **kein** Formfeld. Rund wird über die
Masse ausgedrückt: bei 79 von 80 bemassten Schächten sind `Dimension1` und
`Dimension2` gleich, bei einem verschieden. Kein einziger hat nur `Dimension1`.
Ob die Norm etwas anderes vorschreibt, ist damit nicht geklärt — behandle
`schachtform` deshalb als **ohne sicheres Gegenstück** und melde es.

---

## Randbedingungen

- Bestehende Protokolle auf den Geräten dürfen sich nicht ändern. Keine
  Datenmigration ohne Rückfrage.
- Der `.spro`-Export wird von SewerStudio gelesen. Änderungen am JSON müssen
  **additiv** sein: neue Felder ja, bestehende umbenennen oder entfernen nein.
- Dasselbe gilt für den QR-Code (`SPQR1`). Er ist heute schon dicht:
  Version 23, 109x109 Module auf 39 mm, 809 Zeichen. Zusätzliche Felder
  vergrössern ihn. Sag mir, um wie viel — falls er dadurch nicht mehr sicher
  lesbar wird, brauchen wir einen anderen Weg.
- Antworte auf Deutsch.
