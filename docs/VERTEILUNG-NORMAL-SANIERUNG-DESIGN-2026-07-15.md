# Design: Verteilung Normal/Sanierung + übersichtlichere Verteil-Seite

**Erstellt:** 2026-07-15 (Brainstorming mit Pascal, freigegeben)
**Branch/Stand:** `feature/gis-karte`
**Scope:** Nur die Export-/Verteil-Seite (`ExportPage`) und die Verteil-Logik. Keine anderen Seiten, keine KI-Pipeline.

## Ziel

Bei der Verteilung von **Schächten** und **Leitungen (Haltungen)** je zwei Varianten anbieten — **Normal** und **Sanierung** — und die Seite übersichtlicher/grafischer machen. **Dichtheitsprüfung immer nur Normal.**

## Getroffene Entscheidungen (Brainstorming)

1. **Sanierungs-Inhalt:** Dieselben Dateien wie Normal (PDF-Protokoll + Video), nur eine Ordner-Ebene tiefer. Die **Video-Zuordnung bleibt erhalten**.
2. **Sanierungs-Jahr:** Aus dem **Inspektionsdatum** des Objekts (Jahr des Datums-Teils) — kein Eingabefeld.
3. **Layout:** Grafischer **Ordnerbaum** (Icons) + **Normal|Sanierung-Umschalter** je Karte; der volle Baustein-Baukasten wandert in einen einklappbaren **„Erweitert"**-Bereich.
4. **Auslösung:** **Getrennte Menüeinträge** im „Verteilen"-Menü je Variante. Der Umschalter auf der Karte dient nur der **Vorschau** beider Strukturen.

## Verhalten & Struktur

Die letzte Objekt-Ebene und der Dateiname bleiben **fest** (Video-Zuordnung). Sanierung fügt genau **eine** feste Zwischen-Ebene ein.

| Menüeintrag | Zielstruktur (unter den freien Ebenen) |
|---|---|
| Haltungen – Normal | `{Haltung}/{Datum}_{Haltung}.pdf` (+ Video) |
| Haltungen – Sanierung | `{Haltung}/{Datum}_{Haltung}_Saniert {Jahr}/{Datum}_{Haltung}.pdf` (+ Video) |
| Schächte – Normal | `{Schachtnummer}/{Datum}_{Schachtnummer}.pdf` |
| Schächte – Sanierung | `{Schachtnummer}/{Datum}_{Schachtnummer}_Saniert {Jahr}/{Datum}_{Schachtnummer}.pdf` |
| Dichtheitsprüfung | `{Haltung}/{Datum}_{Haltung}_DP.pdf` — **nur Normal** |

- `{Jahr}` = Jahr des Inspektionsdatums (identisch zum Jahr in `{Datum}`).
- Ordnername der Sanierungs-Ebene: **`{fixedPattern}_Saniert {Jahr}`**, z. B. `20260715_80454_Saniert 2026` (Leerzeichen vor Jahr, wie im PDF).
- Die frei baubaren Präfix-Ebenen (Gemeinde/Datum …) sind für beide Varianten **identisch** — Sanierung hängt nur die eine Ebene an. Kein zweiter Konfig-Baum.

## Seiten-Layout

Aktuell: 3 gestapelte Karten (Haltungen, Schächte, DP), jede mit dem vollen Text-Baukasten (2 freie Ebenen + feste Objekt-Ebene + Dateiname + Vorschau) — dicht und schwer lesbar.

Neu je Karte (Haltungen, Schächte):
- Oben ein **Normal|Sanierung-Umschalter** (segmentiert) → schaltet nur die **Vorschau** um.
- Ein **grafischer Ordnerbaum** mit Icons (📁 Ordner, 📄 PDF, 🎬 Video), der die reale Zielstruktur der gewählten Variante zeigt (wie das PDF von Pascal). Ersetzt die Text-Wüste als primäre Anzeige.
- **Ziel-Wurzel**-Feld bleibt.
- Ein einklappbarer **„Erweitert"**-Bereich (Default zu) enthält den bestehenden Baustein-Baukasten (freie Ebenen, Zurück/Leeren, Feinbearbeitung, Dateiname-Chips).
- **Live-Vorschau**-Zeile (Consolas-Pfad) bleibt, folgt dem Umschalter.

**DP-Karte:** kein Umschalter (immer Normal), sonst gleich.
**Excel-Karte:** unverändert.

**„Verteilen"-Menü** (oben, Dropdown) — neue Struktur:
```
Haltungen – Normal
Haltungen – Sanierung
─────────
Schächte – Normal
Schächte – Sanierung
─────────
Dichtheitsprüfung verteilen
```

## Technik

### Modell (Approach: Modus-Flag, nicht zweiter Baum)
Sanierung ist ein **Modus** am Verteil-Aufruf, kein eigener `DistributionTargetConfig`. Die vorhandene Baum-Config gilt für beide Varianten; der Sanierungs-Modus schiebt die feste Zwischen-Ebene ein.
*Verworfene Alternative:* eigener Sanierungs-Baum je Typ — mehr Persistenz/Komplexität ohne Mehrwert, da nur die eine Ebene abweicht.

### Bausteine
- **Platzhalter `{Jahr}`** neu im `IDistributionPatternResolver`: Jahr des Inspektionsdatums des Objekts. Test: `{Jahr}` löst korrekt auf; bei fehlendem Datum definierter Fallback (heutiges Jahr).
- **Sanierungs-Ordnermuster** als fester Bestandteil des Ziel-Typs: `{fixedPattern}_Saniert {Jahr}` (nur Haltung/Schacht).
- **Distributor** (`HoldingFolderDistributor` bzw. eigener kleiner Service `DistributionSanierungPathDecorator` mit Interface): erhält den Modus (Normal/Sanierung). Im Sanierungs-Modus wird die Zwischen-Ebene zwischen Objektordner und Datei eingeschoben. Die bestehende Video-Zuordnungs-Logik (fester Objektordner + Dateiname) bleibt **unverändert** — sie operiert weiter auf demselben Dateinamen, nur im tieferen Ordner. Fokussierter Test: Pfadaufbau Normal vs. Sanierung, Video landet im selben (tieferen) Ordner.

### UI
- Neues **Ordnerbaum-Control** (`DistributionTreePreviewControl` o. ä.) als eigenes UserControl/Control im `sewer-wpf-ui`-Theme (DynamicResource-Brushes, FluentIcon-Glyphen für 📁/📄/🎬). Bindet an eine Struktur-Beschreibung aus dem ViewModel.
- `DistributionTargetConfigViewModel`: neue Properties `SupportsSanierung` (Haltung/Schacht = true, DP = false), `PreviewVariant` (Normal/Sanierung, Umschalter), abgeleitete `TreeNodes`-Vorschau je Variante; `IsAdvancedExpanded`.
- `ExportPageViewModel`: die bisherigen 3 Verteil-Commands werden zu 5 (Haltung/Schacht je Normal+Sanierung, DP normal). Bestehende Commands intern auf den Modus gehoben.

### Architektur-Regeln (Checkliste)
- Neue Logik (Sanierungs-Pfad) als Service **mit Interface**, im `ServiceProvider` registriert.
- Kein Umbau am Bestand ohne Not; additive Modus-Erweiterung.
- **Fokussierte Tests:** Pattern-Resolver `{Jahr}`, Distributor Normal/Sanierung-Pfad inkl. Video, ViewModel-Vorschau je Variante.
- XAML-Bindings gegen ViewModel prüfen (stille Fehler vermeiden). Deutsch für UI-Text/Kommentare. Keine hartkodierten Farben.

## Randbedingungen (nicht brechen)

- Video-Zuordnung: fester Objektordner + Dateiname bleiben — Sanierung rahmt nur ein.
- DP hat **keine** Sanierungs-Variante.
- Excel-Export unverändert (ein gemeinsamer Zielordner, feste Dateinamen).

## Betroffene Dateien (Erstumsetzung)

| Datei | Änderung |
|---|---|
| `src/AuswertungPro.Next.UI/Views/Pages/ExportPage.xaml` | Karten-Redesign: Umschalter, Ordnerbaum, „Erweitert"-Expander; Menü auf 5 Einträge |
| `src/AuswertungPro.Next.UI/ViewModels/Pages/ExportPageViewModel.cs` | 5 Verteil-Commands, Modus-Weitergabe |
| `…/ViewModels/…/DistributionTargetConfigViewModel` (Ort prüfen) | `SupportsSanierung`, `PreviewVariant`, `TreeNodes`, `IsAdvancedExpanded` |
| `src/AuswertungPro.Next.UI/Controls/` (neu) | `DistributionTreePreviewControl` |
| `src/AuswertungPro.Next.Infrastructure/HoldingFolderDistributor*.cs` | Sanierungs-Modus (Zwischen-Ebene einschieben) |
| Pattern-Resolver (`IDistributionPatternResolver`) | `{Jahr}`-Platzhalter |
| Settings/`DistributionTargetConfig` | ggf. `SupportsSanierung`-Kennzeichen (nur Metadaten) |
| `tests/…` | Resolver-`{Jahr}`, Distributor Normal/Sanierung, ViewModel-Vorschau |

## Offen für die Planungsphase

- Genauer Ort/Signatur von `DistributionTargetConfigViewModel` und des Distributors (beim Planen lesen).
- Ob die Sanierungs-Ebene als eigener `IDistributionSanierungPath`-Service oder als Erweiterung im bestehenden Distributor am saubersten sitzt.
