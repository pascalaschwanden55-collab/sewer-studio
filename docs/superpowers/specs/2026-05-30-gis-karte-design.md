# GIS-Karte für SewerStudio — Design

- **Datum:** 2026-05-30
- **Status:** freigegeben (Brainstorming abgeschlossen)
- **Branch:** `feature/gis-karte`

## Ziel
Eine **eingebaute Karte** in SewerStudio: das Kanalnetz lagetreu auf der offiziellen
Uri-Hintergrundkarte (WMS), die Haltungen **nach Zustandsklasse eingefärbt**, und
**Klick auf eine Haltung → deren Inspektion/Video öffnen**. So sieht der Inspekteur
auf einen Blick, wo die schlechten Haltungen liegen, und springt direkt zur Auswertung.

## Nicht-Ziele (YAGNI)
- Kein Editieren der Geometrie in SewerStudio (das bleibt in QGIS).
- Keine eigene Geo-Datenhaltung/Datenbank — die Geometrie kommt fertig aus QGIS.
- Kein Routing, keine Hydraulik-Visualisierung, keine 3D-Ansicht.
- Keine Schacht-Symbole in v1 (nur Haltungs-Linien). Später optional.
- Kein Direktzugriff auf den WFS in v1 (die XTF reicht). Später optional.

## Datenquellen
1. **Geometrie:** `D:\QGIS_V4\Export_Sewer_Studio\Abwasserkataster_Uri.xtf`
   (fester Pfad, vom QGIS-Plugin „AWU XTF-Exporter" wöchentlich neu geschrieben;
   SIA405_ABWASSER_2020_LV95, INTERLIS 2, **~604 MB, ca. 94'109 Haltungen**,
   enthält LV95-Koordinaten als echte Linien). Pfad in den App-Einstellungen
   hinterlegbar (Default = obiger Pfad).
2. **Zustand je Haltung:** aus dem aktuellen SewerStudio-Projekt
   (`HaltungRecord`, Feld `Zustandsklasse` 0–4; Schlüssel = `Haltungsname`).
3. **Hintergrund:** WMS des Kantons Uri `https://geo.ur.ch/wms` (EPSG:2056 / LV95).
   Standard-Layer: **`basemaps:basemap_av_farbe`** (Farbe), Alternative
   **`basemaps:basemap_av_sw`** (s/w) — in den Einstellungen wählbar.
   Layer-Liste via `https://geo.ur.ch/wms?request=getCapabilities`.

## Koordinatensystem
Alles in **EPSG:2056 (CH1903+/LV95)**. WMS liefert in 2056, die XTF-Koordinaten sind
2056 → fachlich keine Reprojektion nötig.
**Technisches Risiko (im Plan zu klären):** Mapsui arbeitet standardmäßig in
SphericalMercator (EPSG:3857). Im Plan ist zu entscheiden, ob die Karte direkt in
2056 betrieben wird (WMS muss 2056 liefern) oder ob WMS-Bild und Netz nach 3857
umgerechnet werden. Beides geht; die Wahl gehört in den Implementierungsplan.

## Architektur — isoliert, App bleibt stabil
Komplett **neues Modul**, eigene Dateien, **kein Eingriff** in Pipeline/Kosten/bestehende
Seiten. Anbindung nur über einen neuen Menü-Eintrag „Karte".

Vier klar abgegrenzte Bausteine:

### 1. `XtfNetworkExtractor` (Infrastructure, rein/testbar)
- **Zweck:** liest die Kataster-XTF und liefert je Haltung eine Linie.
- **Eingang:** XTF-Pfad.
- **Ausgang:** `IReadOnlyList<HaltungGeometry>` mit `Haltungsname` + **Polylinie**
  (Liste von `(X,Y)`-Punkten in LV95).
- **Logik:** Pro Haltung wird die **echte Linie aus `Verlauf → POLYLINE → COORD`
  gelesen** (mehrere Stützpunkte, auch gekrümmte Haltungen korrekt). Nur falls eine
  Haltung kein `Verlauf` hat: **Fallback** = gerade Linie zwischen
  `vonHaltungspunktRef`/`nachHaltungspunktRef` (deshalb im 1. Durchlauf trotzdem die
  Haltungspunkte `TID → COORD C1/C2` sammeln). Streaming-XML-Reader (`XmlReader`),
  damit die ~604 MB nicht komplett in den RAM müssen.
- **Abhängigkeiten:** nur `System.Xml`. Keine UI, keine DB.

### 2. `NetworkGeometryCache` (Infrastructure)
- **Zweck:** hält eine **schlanke Kopie** der Geometrie, damit die 700-MB-XTF nicht bei
  jedem Kartenöffnen geparst wird.
- **Eingang:** XTF-Pfad + Extractor.
- **Ausgang:** geladene `HaltungGeometry`-Liste.
- **Logik:** Cache-Datei (`%LOCALAPPDATA%\SewerStudio\map\network_cache.json`).
  Beim Laden: ist die XTF **neuer** als der Cache → Extractor laufen lassen + Cache
  neu schreiben; sonst Cache direkt laden. Speichert XTF-Pfad+mtime im Cache-Kopf.
- **Abhängigkeiten:** `XtfNetworkExtractor`, Dateisystem, `System.Text.Json`.

### 3. `HaltungConditionProvider` (Infrastructure/Application)
- **Zweck:** liefert je Haltungsname den Zustand-Rohwert + Feldquelle.
- **Eingang:** aktuelles Projekt (vorhandene `HaltungRecord`s).
- **Ausgang:** `Dictionary<Haltungsname, ZustandRoh?>` (null = nicht inspiziert);
  `ZustandRoh` = Wert + welches Feld (damit `ZustandColorMapper` die richtige Skala kennt).
- **Abhängigkeiten:** Projekt-Repository / Shell-Projekt. Reine Lese-Operation.

### 5. `ZustandColorMapper` (Infrastructure, rein/testbar)
- **Zweck:** Zustand-Rohwert → feste Farbstufe (grün/orange/rot/grau), **eindeutig**
  und unabhängig von der invertierten Skala (siehe Einfärbung).
- **Eingang:** `ZustandRoh?`. **Ausgang:** Farb-Enum (`Gut/Mittel/Schlecht/Unbekannt`).
- **Abhängigkeiten:** keine. Kleiner Unit-Test (jede Skala-Richtung).

### 4. `KartePage` + `KarteViewModel` (UI)
- **Zweck:** zeigt die Mapsui-Karte, verknüpft Geometrie+Zustand, behandelt Klick.
- **Aufbau der Karte (Layer von unten):**
  1. **WMS-Layer** `https://geo.ur.ch/wms`, Layer `basemaps:basemap_av_farbe`
     (Hintergrund).
  2. **Netz-Layer:** alle Haltungen als Polylinien; Farbe über den
     `ZustandColorMapper` (siehe Einfärbung); Haltungen ohne Projekt-Zustand = grau.
- **Klick:** Treffer-Haltung markieren → ViewModel meldet `Haltungsname` →
  Knopf „Inspektion/Video öffnen" startet die bestehende Codier-/Player-Ansicht
  für diese Haltung (gleicher Weg wie Rechtsklick → öffnen in der Tabelle).
- **Legende:** Farbskala 0–4 + „nicht inspiziert".

## Datenfluss
```
XTF (D:\QGIS_V4\...\Abwasserkataster_Uri.xtf)
   └─ XtfNetworkExtractor ─→ NetworkGeometryCache (schlank, lokal)
                                      │
Projekt-Haltungen ─ HaltungConditionProvider ─┐
                                      ▼        ▼
                              KarteViewModel (Join über Haltungsname)
                                      ▼
                Mapsui: [WMS geo.ur.ch] + [Netz-Linien, eingefärbt]
                                      ▼
                Klick → Haltungsname → bestehende Inspektion öffnen
```

## Einfärbung
**⚠️ Wichtig — Skala ist im Code nicht einheitlich.** An manchen Stellen entsteht
`4` als *guter* Zustand (invertierte EZ-Skala 0=schlecht/4=gut), an anderen ist
`4` der *schlechteste* (VSA-Zustandsklasse). Wenn die Karte den Rohwert direkt
einfärbt, malt sie eventuell **falsch herum**. Deshalb:

- Eine kleine, **eindeutige Normalisierungsfunktion `ZustandColorMapper`**:
  - Eingang: der Zustand-Rohwert je Haltung (+ Quelle/Feldname, damit klar ist,
    welche Skala gilt).
  - Ausgang: feste Farbstufe → **`0/1 = grün`, `2 = orange`, `3/4/5 = rot`,
    `n/a = grau`**.
- **Im Plan zu klären (Pflicht):** welches Feld die Karte als Zustand nimmt und in
  welcher Richtung dessen Skala läuft — an einer bekannten Haltung gegenprüfen
  (eine real schlechte Haltung MUSS rot werden), bevor die Farbe fix verdrahtet wird.
- **Keine Inspektion / nicht im Projekt = grau** (ganzes Kanton-Netz sichtbar, nur
  bearbeitete Haltungen farbig).
- Konkrete Farbwerte aus den Theme-Brushes wiederverwenden, falls vorhanden.

## Fehlerbehandlung
- **XTF fehlt/Pfad falsch:** Karte zeigt nur WMS-Hintergrund + Hinweis „Netz-Datei nicht
  gefunden, Pfad in Einstellungen prüfen". Kein Absturz.
- **XTF beschädigt / Koordinate ungültig:** betroffene Haltung wird übersprungen +
  gezählt; Rest wird gezeichnet; Anzahl übersprungener im Log.
- **WMS offline:** Netz wird trotzdem auf neutralem Hintergrund gezeichnet; Hinweis.
- **Name-Join-Miss:** Haltung ohne passenden Projekt-Eintrag → grau (kein Fehler).
- Alle Geo-/Netzwerk-Fehler dürfen die App **nie** abstürzen lassen (try/catch in
  Lade-Pfaden, wie bei den bestehenden Telemetry-Writern).

## Abhängigkeit
- **NuGet `Mapsui.Wpf`** (freigegeben). Einzige neue Abhängigkeit. WMS- und
  Vektor-Layer-Unterstützung sind enthalten.

## Testbarkeit
- `XtfNetworkExtractor` und der Name-Join sind **reine Funktionen** → kleine Unit-Tests
  mit einer Mini-XTF (2–3 Haltungen) möglich. (CLAUDE.md begrenzt Tests sonst auf
  Recommendation/QualityGate; hier ist ein schmaler Parser-Test sinnvoll und billig.)
- UI/Mapsui wird nicht automatisiert getestet (manuelle Sichtprüfung).

## Isolation / Dateien (neu)
- `src/AuswertungPro.Next.Infrastructure/Map/XtfNetworkExtractor.cs`
- `src/AuswertungPro.Next.Infrastructure/Map/NetworkGeometryCache.cs`
- `src/AuswertungPro.Next.Infrastructure/Map/HaltungGeometry.cs` (Record, Polylinie)
- `src/AuswertungPro.Next.Infrastructure/Map/ZustandColorMapper.cs`
- `src/AuswertungPro.Next.UI/ViewModels/Pages/KarteViewModel.cs`
- `src/AuswertungPro.Next.UI/Views/Pages/KartePage.xaml` (+ `.xaml.cs`)
- Menü-Eintrag in `ShellViewModel.cs` (1 Zeile), DataTemplate in `App.xaml` (analog
  zu den bestehenden Seiten), Mapsui-Paketverweis in `AuswertungPro.Next.UI.csproj`.
- `HaltungConditionProvider` an passender Stelle (Application/Infrastructure).
- **Kein** Eingriff in KI-Pipeline, Kosten oder andere bestehende Logik.

## Spätere Erweiterungen (nicht jetzt)
- Schacht-Symbole (Normschacht/Abwasserknoten als Punkte).
- Alternativ swisstopo-WMS als Hintergrund.
- Netz direkt aus dem WFS statt aus der XTF.
- Filter/Suche (nach Strasse, Zustand, „nur zu sanierende").
