# Kostenberechnung / Offertenerstellung

## Übersicht
Das Kostenberechnungs-Modul ermöglicht die schnelle Erstellung von Kostenvoranschlägen und Offerten für Kanalsanierungsmaßnahmen.

## Komponenten

### 1. Domain Models (AuswertungPro.Next.Domain/Models/Costs/)
- **PriceItem.cs**: Einzelner Preiseintrag (Artikel, Einheit, Preis, DN-Bereich)
- **PriceCatalog.cs**: Kompletter Preiskatalog mit Liste von PriceItems
- **MeasureTemplate.cs**: Vorlage für Sanierungsmaßnahme (z.B. Nadelfilzliner, Manschette)
- **TemplateLine.cs**: Einzelposition in Template (qty-Variable, Einheit, PriceId)
- **MeasureInputs.cs**: Eingabewerte für Berechnung (DN, Länge, Anschlüsse, etc.)
- **OfferLine.cs**: Berechnete Offerten-Zeile (Position, Menge, EP, Betrag)
- **OfferTotals.cs**: Summen (Zwischensumme, Rabatt, Skonto, MWST, Total)
- **CalculatedOffer.cs**: Komplette berechnete Offerte

### 2. Infrastructure Service (AuswertungPro.Next.Infrastructure/Costs/)
- **CostCalculationService.cs**:
  - LoadCatalog() / SaveCatalog()
  - LoadTemplates()
  - CalculateOffer() - einzelne Maßnahme
  - CalculateCombinedOffer() - mehrere Maßnahmen kombiniert
  - SelectPriceItemForDn() - automatische DN-basierte Preisauswahl
  - ResolveQty() - qty-Variablen-Resolution (length, connections, end_cuffs, etc.)

### 3. ViewModels / Views (AuswertungPro.Next.UI/)
#### a) Preiskatalog-Editor
- **PriceCatalogEditorViewModel** / **PriceCatalogEditorWindow**
- Funktion: Bearbeitung des Preiskatalogs (user_catalog.json)
- Features: Hinzufügen/Löschen von Positionen, Suchen/Filtern, Speichern

#### b) Maßnahmen-Auswahl
- **MeasureSelectionViewModel** / **MeasureSelectionWindow**
- Funktion: Auswahl einer oder mehrerer Maßnahmen aus Templates
- Features: Multi-Select mit Checkboxen

#### c) Kostenberechnung (einzelne Maßnahme)
- **CostCalculationViewModel** / **CostCalculationWindow**
- Funktion: Berechnung für eine einzelne Sanierungsmaßnahme
- Features: 
  - DN, Länge, Anschlüsse, Endmanschetten, Wasserhaltung eingeben
  - Berechnung auf Knopfdruck
  - Anzeige der berechneten Positionen gruppiert
  - Summen (Zwischensumme, MWST, Total)
  - Offerte in Zwischenablage kopieren
  - Zugriff auf Preiskatalog-Editor

#### d) Kombinierte Offerte
- **CombinedOfferViewModel** / **CombinedOfferWindow**
- Funktion: Berechnung mehrerer Maßnahmen in einer Offerte
- Features:
  - Maßnahmen-Auswahl per Dialog
  - Gemeinsame Inputs (DN, Länge, etc.) für alle Maßnahmen
  - Rabatt- und Skonto-Berechnung
  - Kombinierte Positionen mit Gruppierung
  - Summen mit Rabatt/Skonto/MWST
  - Offerte in Zwischenablage kopieren

### 4. Datendateien (Data/)
#### seed_price_catalog.json
Beispiel-Preiskatalog mit 19 Standard-Positionen:
- Installation (Kran, Reinigung, TV-Inspektion, etc.)
- Vorarbeiten (Anschlusssperren, Wasserhaltung, Bohrkern)
- Renovierung (Nadelfilzliner DN150-500, Manschetten, Punktreparaturen)
- Qualität (Endabnahme, Dichtigkeitsprüfung)

#### measure_templates.json
6 vordefinierte Maßnahmen-Templates:
1. Nadelfilzliner (Installation + Liner + Anschlüsse + Endmanschetten + Qualität)
2. GFK-Schlauchliner
3. Manschette (Reparatur einzelner Stellen)
4. Kurzliner (0,5-1,5m Länge)
5. Pointliner (Punktreparatur)
6. Anschluss verpressen

## Menü-Integration
Im Hauptfenster (MainWindow) unter Menü "Kosten":
- Preiskatalog bearbeiten...
- Kostenberechnung (einzelne Maßnahme)...
- Kombinierte Offerte...

## Workflow

### Einzelne Maßnahme berechnen
1. Menü → Kosten → Kostenberechnung (einzelne Maßnahme)
2. Maßnahme aus Liste auswählen (z.B. "Nadelfilzliner")
3. DN, Länge, Anschlüsse etc. eingeben
4. "Berechnen" klicken
5. Positionen prüfen, ggf. Preise anpassen (Button "Preise...")
6. "Offerte kopieren" → Text in Zwischenablage

### Kombinierte Offerte erstellen
1. Menü → Kosten → Kombinierte Offerte
2. "Maßnahmen auswählen..." → mehrere Maßnahmen markieren
3. DN, Länge, Anschlüsse, Rabatt %, Skonto % eingeben
4. "Berechnen" klicken
5. Gesamtofferte prüfen
6. "Offerte kopieren" → Text in Zwischenablage

### Preiskatalog bearbeiten
1. Menü → Kosten → Preiskatalog bearbeiten
2. Neue Position hinzufügen: "Neu", Felder ausfüllen, Enter
3. Position löschen: markieren, "Löschen"
4. Suchen: Suchfeld oben nutzen
5. "Speichern" → user_catalog.json wird aktualisiert

## Technische Details

### qty-Variablen (TemplateLine)
- `length` → LengthM
- `connections` → Connections
- `end_cuffs` → EndCuffs
- `1` → Konstante 1
- Formel-Support: `connections * 2`, `length / 10`, etc.

### DN-basierte Preisauswahl
Jeder PriceItem hat dn_min/dn_max. Service wählt automatisch passenden Preis:
- DN150 → dn_min=150, dn_max=199
- DN200 → dn_min=200, dn_max=249
- etc.

### Seed → User Catalog
Beim ersten Start wird `seed_price_catalog.json` nach `user_catalog.json` kopiert.
Änderungen erfolgen nur in user_catalog.json (seed bleibt unverändert).

## Erweiterungsmöglichkeiten
1. Excel-Export der Offerte (zusätzlich zu Zwischenablage)
2. Pro-Maßnahme-Inputs in kombinierter Offerte (derzeit alle gleich)
3. Projekt-spezifische Preiskataloge (derzeit global)
4. Historische Offerten speichern/laden
5. PDF-Export
6. Anbindung an Haltungs-Daten (automatisches Befüllen von DN/Länge)

## Dateistruktur
```
<AppContext.BaseDirectory>/Data/
  seed_price_catalog.json    (Vorlage, nicht editieren)
  user_catalog.json           (aktiver Katalog, wird editiert)
  measure_templates.json      (Maßnahmen-Templates)
```

## Build & Run
```powershell
dotnet build
dotnet run --project src/AuswertungPro.Next.UI/AuswertungPro.Next.UI.csproj
```

Menü → Kosten → ...
