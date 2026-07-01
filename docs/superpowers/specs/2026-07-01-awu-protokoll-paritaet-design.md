# AWU-Haltungsprotokoll — Parität zum Original (Design)

> Datum: 2026-07-01 · Lane: Claude (Backend: Application/Infrastructure + Tests) · Branch-Ziel: `feature/gis-karte`

## Ziel
Das app-generierte AWU-Haltungsprotokoll (`ProtocolPdfExporter.BuildHaltungsprotokollPdf`) soll
das Original (Abwasser-Uri „Haltungsgrafik") in vier Punkten einholen, die der Anwender im
Direktvergleich bemängelt hat:

- **A** — Das App-Protokoll zeigt **viel mehr Codes** als das Original (Fortsetzungs-/Quantifizierungszeilen doppeln jede Beobachtung).
- **B** — Es fehlt die **Trennlinie** zwischen Haupt- und Gegeninspektion, die das Original hat.
- **C** — Quantifizierung wird **roh** angezeigt (`Q1=45, Q2=20`) statt als Klartext (`Winkel = 45°`, `Querschnitt = 20 %`).
- **D** — **Foto- und MPEG-Spalten sind leer**, obwohl das Original Fotonummer + Timecode zeigt.

## Verifizierte Ist-Analyse (Code-Ebene)

| Beobachtung | Wurzel im Code |
|---|---|
| Zu viele Codes (A) | `ProtocolPdfEntryResolver.ResolveEntriesForExport` erzeugt **eine Zeile pro `VsaFinding`**; Dedup-Key `Code\|Meter\|Meter\|Beschreibung` lässt Text- und Quantifizierungs-/„–"-Zeile beide überleben. |
| Keine Trennlinie (B) | Die einzige Code-Liste ist die **ins SVG geklebte** Tabelle (`HaltungsgrafikSvgBuilder`); sie kennt keinen Segmenttrenner. `IsAbortCode` wird nur fürs Grafiksymbol genutzt. |
| Roh-Quantifizierung (C) | Bei leerer Beschreibung fällt der Zustandstext auf `BuildParameterShortText` → wörtlich `Q1=45`. Der fertige Formatierer `ProtocolDescriptionBuilder.Build` (macht `{Wert}{Einheit}` + Uhrlage) wird **nie** genutzt, weil der Export nie die `CodeDefinition` auflöst. |
| Foto/MPEG leer (D) | Spalten werden gerendert (`ComposeObservationListTable`), aber diese Tabelle wird **nie aufgerufen**. Zusätzlich setzt der **XTF-Import nur `FotoPath`, keinen MPEG-Timecode** aufs Finding (WinCan-Factory setzt beide). |

## Architektur-Entscheidung
Nicht gegen das höhenbegrenzte SVG kämpfen. Stattdessen die **bereits vorhandene, ungenutzte**
`ProtocolPdfExporter.ComposeObservationListTable` (Spalten m+ | m- | OP Kürzel | Zustand | Foto |
MPEG | Zeit | Bemerkung, fließend + paginierend) **unter die Haltungsgrafik** setzen. Die Grafik
bleibt als Übersicht. Steuerbar per neuer Option `HaltungsprotokollPdfOptions.IncludeObservationTable`
(Default `true`).

## Komponenten & Datenfluss

### A — Weniger Codes: MERGE statt DROP
Neuer, konservativer Nachlauf in `ProtocolPdfEntryResolver` (oder ein eigener `ObservationCollapser`),
der Einträge mit **gleichem Code und gleichem Meter(start/end)** zusammenführt, wenn genau einer die
inhaltliche Beschreibung trägt und der/die andere(n) nur Quantifizierung/„–"/leer.
- Ergebnis: **ein** Eintrag, dessen Beschreibung + Quantifizierung + Fotos vereint sind.
- **Invariante:** Eine codierte Beobachtung mit eigener, nicht-leerer, nicht-quantifizierungs-Information
  wird **nie** verworfen. Im Zweifel bleiben beide Zeilen stehen (kein Datenverlust).
- Rein additiver Post-Pass; bei uneindeutigen Fällen No-Op.

### B — Trennlinie Haupt-/Gegeninspektion
In der neuen Tabellensektion beim **ersten** Eintrag mit `IsAbortCode(entry)` eine Trenn-/Titelzeile
„Gegeninspektion" einziehen (voll-breite Zelle mit Linie). Reihenfolge bleibt nach Meter sortiert;
der Abbruchcode markiert den Segmentwechsel.

### C — Quantifizierung als Klartext
- `HaltungsprotokollPdfOptions` bekommt `ICodeCatalogProvider? CodeCatalog` (Default `null`).
- Beim Rendern der Zustandsspalte: `CodeCatalog.TryGet(entry.Code, out def)` →
  `ProtocolDescriptionBuilder.Build(def, entry.CodeMeta?.Parameters, MeterStart, MeterEnd)`.
- **Fallback** (kein Katalog / Code unbekannt / kein Ergebnis): heutiges `BuildObservationZustandTextLong`.
- Umsetzung als vorab berechnete `IReadOnlyDictionary<ProtocolEntry,string>` (analog `photoNumbers`),
  damit weder `ProtocolEntry` mutiert noch die Signaturen tief durchgereicht werden müssen.
- Keine Signaturänderung an `BuildHaltungsprotokollPdf` (5 Aufrufer) — Injektion nur über Options.

### D — Foto/MPEG füllen
- Die neue Tabellensektion zeigt Foto-Nr. (`photoNumbers`) + `entry.Mpeg`/`entry.Zeit`.
- WinCan liefert beides bereits (`WinCanFindingFactory`).
- **XTF-Pfad:** MPEG-Timecode pro Beobachtung aufs `VsaFinding` setzen, **sofern** die VSA_KEK-Quelle
  einen pro-Beobachtung-Timecode führt. Erst Datencheck am echten Projekt; wenn die Quelle keinen
  Timecode hat, ehrlich dokumentieren statt erfinden.

## Teststrategie
- **A:** Resolver-Tests — (1) Text+Quantifizierungszeile gleicher Code/Meter → 1 Eintrag mit gefaltetem
  Text; (2) zwei echte verschiedene Beobachtungen gleichen Codes an unterschiedlichem Meter → bleiben 2;
  (3) zwei codierte Beobachtungen mit je eigener Info → bleiben 2 (kein Verlust).
- **B:** Tabellen-/Sektionstest — Eintragsfolge mit Abbruchcode erzeugt genau eine „Gegeninspektion"-Trennzeile an der richtigen Position.
- **C:** Zustandstext-Resolver — mit Katalog → Klartext inkl. Einheit; ohne Katalog → altes Verhalten (verhaltensneutral).
- **D:** XTF-Import-Test — Finding trägt MPEG, wenn Quelle Timecode liefert; WinCan-Regressionstest bleibt grün.
- Volle Suite grün vor Merge; Kommentare deutsch; keine neuen NuGets.

## Nicht in Scope
- Kein Umbau des SVG-Grafik-Renderings (bleibt als Übersicht).
- Keine Gegeninspektions-Zweitprotokoll-Datei (`PDF_G`) — separat.
- Keine Änderung der 5 `BuildHaltungsprotokollPdf`-Aufrufsignaturen.
