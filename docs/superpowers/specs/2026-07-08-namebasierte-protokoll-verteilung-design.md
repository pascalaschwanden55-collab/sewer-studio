# Name-basierte Protokoll-Verteilung (Haltungen + Schächte) — Design

**Datum:** 2026-07-08
**Status:** Freigegeben (Design), bereit für Umsetzungsplan

## Ziel
Protokoll-PDFs **narrensicher** auf Haltungen **und** Schächte verteilen — über den **Namen** im Datei-/Ordnernamen statt über PDF-Inhalts-Parsing. Jede Quell-Variante führt ans Ziel oder wird sichtbar gemeldet; nichts wird still verschluckt. Fehlende Schächte werden aus den Protokollen angelegt (Protokoll ist maßgebend).

## Problem (Ist-Zustand)
Beim IKAS/XTF-Import wurden **0 Original-Protokolle** verteilt (Report `kanalimport_20260708_182426.txt`, Zeile „Verteilung: … 0 Original-Protokolle"), obwohl ~30 per-Haltung-PDFs (`H_<H>.pdf`/`L_<H>.pdf`) vorliegen. Ursachen im bestehenden `KanalImportDistributor`/`HoldingFolderDistributor`:
1. Es wird nur **ein** „maßgebliches" Gesamt-PDF gewählt (`SelectPrimaryProtocolPdf`) und gesplittet; per-Haltung-Dateien werden ignoriert.
2. Zuordnung über **PDF-Inhalt**; Dateiname-Zuordnung nur bei WinCan (`isWinCan`). Bei IKAS greift sie nicht.
3. Gescannte Bild-PDFs liefern keinen Text → 0 Treffer.
Schächte fehlen im Projekt komplett (`SchaechteData` leer), obwohl Schacht-Protokolle vorliegen.

## Global Constraints (verbindlich, je Task)
- **Thin-AI/Schichten:** Geschäftslogik in C# (Infrastructure/Application), UI ruft Service/ViewModel. Kern rein & testbar.
- **Neuer Service mit Interface**, im `ServiceProvider` (DI) registriert; kein verstreutes `new`.
- **Additiv:** bestehender Inhalts-Parse-Pfad bleibt als Fallback; kein großes Refactoring am Bestand ohne Not.
- **Deutsche Kommentare.** JSON/Records unverändert.
- **Fokussierte Tests** für Resolver + Distributor (Kernlogik).
- **Commits:** ~68 unzusammenhängende uncommittete Dateien + teils bereits „dirty" Dateien im Tree → jede Task staged NUR ihre eigenen Dateien/Hunks (kein `git add -A`).

## Bestätigte Entscheidungen
- **Name-basiert für ALLE Layouts** (roh flach, aufgeteilter Baum, flach per Schacht, gemischt); Gesamtprotokoll-Inhalts-Split nur als Fallback.
- **Schächte:** aus Protokollen anlegen; falls die XTF Schächte enthält, sind die **Protokolle maßgebend** (Protokoll gewinnt). Kein XTF-Schacht-Parsing in diesem Umbau.
- **Narrensicher:** Fallback-Kette + sichtbarer Report, kein stilles Verschlucken.

## Domänen-Fakten (verifiziert)
- Haltungsname-Feld: `FieldKeys.HoldingName = "Haltungsname"`. Protokoll-PDF-Feld (beide Arten): `FieldKeys.PdfPath = "PDF_Path"`.
- Schacht-Nummer-Feld: `"Schachtnummer"` (String auf `SchachtRecord`). Anlage: `new SchachtRecord()` → `project.SchaechteData.Add(record)` → `SetFieldValue("Schachtnummer", nr, …)`.
- Ziel-Ordner: `ProjectStructure.HaltungVerteiltDir(projectFolder, san)` / `SchachtVerteiltDir(projectFolder, san)`; Konstanten `Haltungen_Verteilt` / `Schächte_Verteilt`.
- Normalisierung/Matching: `HoldingKeyNormalizer.Normalize(value)`. Pfade: `ProjectPathResolver.SanitizePathSegment/MakeRelative/IsRelative`.
- Vorhandenes Vorbild: `KanalImportDistributor.FindRecordBySanitizedHaltung(project, folderName)` (Haltung-Match), `MediaDistributionService` (Schacht via `GetFieldValue("Schachtnummer")`).

## Architektur

### 1. `ProtocolNameResolver` (neu, rein, testbar)
`src/AuswertungPro.Next.Infrastructure/Import/Protocols/ProtocolNameResolver.cs`
- `public static ProtocolTarget? Resolve(string pdfPath)` → `ProtocolTarget(ProtocolKind Kind, string Name)` oder `null` (nicht zuordenbar/kein Protokoll).
- `enum ProtocolKind { Haltung, Schacht }`.
- **Regel-Reihenfolge (erste greift):**
  1. Elternordner heißt `Haltungen` → Haltung; `Schächte`/`Schaechte` → Schacht.
  2. Dateiname-Präfix `H_`/`L_` → Haltung; `S_` → Schacht.
  3. `-` im (präfix-/datumsbereinigten) Namen → Haltung, sonst Schacht.
- **Namens-Extraktion:** führendes `YYYYMMDD_`, Präfixe `H_`/`L_`/`S_`, Duplikat-Suffix `_<ziffern>` und Extension entfernen; Punkte bleiben; getrimmt.
- **Nicht-Protokolle überspringen:** Namensmuster (`übersichtsplan`, `haltungsliste`, `haltungs-statistik`, `_orto`, `_av`, `plan`) und die bestehende `PdfDokumentTypErkennung` (Nicht-`TvProtokoll` → null).

### 2. `INameBasedProtocolDistributor` / `NameBasedProtocolDistributor` (neuer Service)
`src/AuswertungPro.Next.Infrastructure/Import/Protocols/NameBasedProtocolDistributor.cs` (+ Interface)
- `ProtocolDistributionReport Distribute(Project project, string projectFolder, string sourceFolder)`.
- Ablauf:
  1. PDFs unter `sourceFolder` **rekursiv** sammeln.
  2. Je PDF `ProtocolNameResolver.Resolve` → (Art, Name) oder überspringen (Nicht-Protokoll) bzw. „nicht zugeordnet".
  3. **Haltung:** Match in `project.Data` per normalisiertem `Haltungsname`, **beide Schacht-Reihenfolgen** `A-B`/`B-A` probieren. Treffer → PDF nach `HaltungVerteiltDir(...)` kopieren (Datei existiert → kein Duplikat), `record[PDF_Path]` = relativer Pfad.
  4. **Schacht:** Match in `project.SchaechteData` per normalisierter `Schachtnummer`; **fehlt → SchachtRecord anlegen** (Nummer setzen). PDF nach `SchachtVerteiltDir(...)` kopieren, `record[PDF_Path]` = relativer Pfad.
  5. Kein Match → im Report als „nicht zugeordnet: <Dateiname>".
- **Idempotenz:** Zielpfad deterministisch (`<stamp>_<san>.pdf` bzw. Originalname); vorhandene identische Datei nicht doppelt kopieren; `PDF_Path` überschreiben statt duplizieren.
- **Report** `ProtocolDistributionReport(int HaltungProtokolle, int SchachtProtokolle, int SchaechteAngelegt, IReadOnlyList<string> NichtZugeordnet, IReadOnlyList<string> Meldungen)`.

### 3. Narrensicher — Varianten & Fallback
| Variante | Beispiel | Zuordnung |
|---|---|---|
| Roh flach, Haltung | `Importdateien\PDF\H_33390-36268.pdf`, `L_…` | Präfix → Haltung |
| Aufgeteilter Baum | `…\Haltungen\<H>\YYYYMMDD_<H>.pdf`, `…\Schächte\<Nr>\…` | Elternordner → Art |
| Roh flach, Schacht | `<Nr>.pdf`, `YYYYMMDD_<Nr>.pdf`, `S_<Nr>.pdf` | Nummer → Schacht |
| Ein Gesamtprotokoll | `*_Protokoll.pdf` (KINS) | bestehender Inhalts-Split = Fallback |
| Gemischt | Haltung- + Schacht-PDFs zusammen | pro Datei einzeln |

**Fallback-Kette je PDF:** (1) Elternordner → (2) Präfix → (3) `-`-Heuristik → (4) bestehender Inhalts-Parse (nur wenn Name nichts ergab) → (5) „nicht zugeordnet" im Report. Kein stilles Verschlucken.

### 4. Andockpunkte (Integration)
- **Ein-Knopf-Import:** Der Protokoll-Verteil-Schritt ruft zuerst den `NameBasedProtocolDistributor` auf `Importdateien\PDF` auf; nur wenn dort keine per-Name-Treffer entstehen, greift der bisherige Gesamtprotokoll-Split (Fallback). Report-Zahlen fließen in den Import-Report.
- **„Verteil-Ordner wählen"** (bestehender Schacht-Import-Button `ImportSchachtPdfsFolderCommand` wird darauf umgestellt/erweitert): Nutzer wählt einen Ordner (z.B. `…\Jagdmatt_Verteilung`); der Distributor verteilt in einem Rutsch **Haltungen UND Schächte** by name, legt fehlende Schächte an, zeigt den Report.
- DI-Registrierung im `ServiceProvider`.

## Tests
- `ProtocolNameResolverTests`: `H_33390-36268.pdf`→(Haltung,"33390-36268"); `L_1273.01-7.34854.pdf`→(Haltung,"1273.01-7.34854"); `Schächte\27581\20260427_27581.pdf`→(Schacht,"27581"); `Haltungen\<H>\20260424_<H>.pdf`→Haltung; `A3_Übersichtsplan.pdf`/`Haltungsliste.pdf`→null.
- `NameBasedProtocolDistributorTests` (temp-Projektstruktur, Dummy-PDFs): verteilt Haltung-PDF in `Haltungen_Verteilt\<H>` + setzt `PDF_Path`; legt fehlenden Schacht an + verteilt in `Schächte_Verteilt\<Nr>`; Haltung mit vertauschter Schacht-Reihenfolge matcht; Nicht-Protokoll wird übersprungen; unbekannter Name landet in `NichtZugeordnet`; zweiter Lauf erzeugt keine Duplikate.

## Nicht im Scope
- Kein XTF/SIA405-Schacht-Import (Protokolle maßgebend).
- Kein OCR/Inhalts-Parse-Umbau (bestehender Inhalts-Split bleibt nur Fallback).
- Keine UI-Neugestaltung über den bestehenden Button + Report hinaus.
