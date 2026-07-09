# Schacht-Protokoll: Aktualisieren + Einzel-Import — Design

**Datum:** 2026-07-09
**Branch:** feature/gis-karte
**Status:** Freigegeben (Design), bereit für Umsetzungsplan

## 1. Ziel

Auf der Schachtseite sollen zwei neue Knöpfe entstehen:

1. **„Aktualisieren"** — liest das bereits mit dem ausgewählten Schacht verknüpfte
   Protokoll-PDF erneut ein und baut den Schacht komplett aus dem Protokoll neu auf
   (Felder + Schäden). Vorher erscheint eine Warnung, weil von Hand erfasste Werte
   dabei verloren gehen.
2. **„Protokoll importieren"** — der Benutzer wählt eine einzelne PDF-Datei. Das
   Programm liest sie, erkennt die Schachtnummer, ordnet sie einem Schacht zu (bei
   Kollision mit Nachfrage), füllt Felder + Schäden und verteilt die Datei in den
   richtigen Projektordner `Schächte_Verteilt\<Nr>\`.

Beide Knöpfe sitzen in der **oberen Toolbar** der Schachtseite.

## 2. Fachliche Entscheidungen (mit dem Benutzer geklärt)

| Thema | Entscheidung |
|---|---|
| Trennung | **Zwei** getrennte Knöpfe (nicht ein kombinierter). |
| „Aktualisieren"-Verhalten | Schacht **komplett** aus dem Protokoll neu aufbauen. Von Hand erfasste Werte gehen verloren. **Vorher Warnung** (Ja/Nein). |
| „Aktualisieren"-Voraussetzung | Nur aktiv, wenn ein Schacht ausgewählt ist **und** er ein verknüpftes Protokoll (`PDF_Path`) besitzt. Sonst ausgegraut. |
| „Protokoll importieren" bei bekannter Schachtnummer | **Nachfragen**: Überschreiben / Als neuen anlegen / Abbrechen. |
| „Protokoll importieren" bei unbekannter Nummer | Neuen Schacht anlegen. |
| Ablage der Datei | Kopie nach `Schächte_Verteilt\<Schachtnummer>\`, `PDF_Path` wird auf den **relativen** Zielpfad gesetzt. |

## 3. Architektur (Ansatz A)

**Grundsatz:** Die Technik zum PDF-Lesen und Schaden-Erkennen existiert bereits und
wird wiederverwendet. Das neue Feature wird als **eigener, klar abgegrenzter Dienst mit
Interface** additiv gebaut (CLAUDE.md-Checkliste Punkt 1–3). Kein Doppel-Code.

### 3.1 Neuer Dienst

- **Interface:** `ISchachtProtocolImportService`
  (Namespace `AuswertungPro.Next.Application.Import`, ergänzt in
  `src/AuswertungPro.Next.Application/Import/IImportServices.cs`).
- **Implementierung:** `SchachtProtocolImportService`
  (`src/AuswertungPro.Next.Infrastructure/Import/Protocols/SchachtProtocolImportService.cs`).
  Liegt bewusst in *Infrastructure*, weil der Schaden-Parser
  (`SchachtProtocolParser.ParseSchachtDamageEntries`) `internal` ist und nur dort
  direkt erreichbar ist.
- **Registrierung:** als Property im handgeschriebenen Service-Locator
  `src/AuswertungPro.Next.UI/ServiceProvider.cs` (im Konstruktor `new`, Auflösung in
  `GetService`), analog zu `PdfImport`, `NameBasedProtocolDistributor`.

### 3.2 Methoden des Dienstes (schlank, UI-frei, testbar)

```
SchachtProtocolParseResult Parse(string pdfPfad)
    // Liest PDF-Text, prüft ob Schachtprotokoll, gibt Schachtnummer +
    // Felder + Schäden zurück. Verändert nichts. Kein Treffer -> IstSchachtprotokoll=false.

void Apply(SchachtRecord ziel, SchachtProtocolParseResult ergebnis, string pdfPfad)
    // Schreibt Felder + Schäden (ProtocolDocument) + PDF_Path auf den gegebenen Record.
    // Baut den Schacht komplett neu auf (last-write-wins; Protokoll wird ersetzt).

string DistributePdf(Project projekt, string projektOrdner, string schachtNr, string pdfQuelle)
    // Kopiert die PDF nach Schächte_Verteilt\<Nr>\ und gibt den relativen Zielpfad zurück
    // (via ProjectStructure.SchachtVerteiltDir + ProjectPathResolver.MakeRelative).
```

`SchachtProtocolParseResult` (neues DTO in `AuswertungPro.Next.Application.Import`):
`bool IstSchachtprotokoll`, `string? Schachtnummer`, `IReadOnlyDictionary<string,string> Felder`,
`IReadOnlyList<(string Bauteil, string Schaden)> Schaeden`.

### 3.3 Wiederverwendung bestehender Bausteine

| Baustein | Sichtbarkeit | Nutzung |
|---|---|---|
| `PdfTextExtractor.ExtractPages(pdfPath, explicitPdfToTextPath?)` | public | Volltext holen. |
| `LegacyPdfImportService.ParseSchachtFields(text)` | public | Felder (Schachtnummer, Datum, Funktion, primäre Schäden, Status). |
| `SchachtProtocolParser.ParseSchachtDamageEntries(text)` | internal (Infrastructure) | Schäden als `(Bauteil, Schaden)`-Liste. |
| `ProtocolDocument`/`ProtocolRevision`/`ProtocolEntry` (`Domain/Protocol/ProtocolModels.cs`) | public | Schäden am Schacht ablegen. |
| `ProjectStructure.SchachtVerteiltDir`, `ProjectPathResolver.SanitizePathSegment`/`MakeRelative` | public | Datei verteilen + relativen Pfad bilden. |
| `IDialogService` (`Warn`, `Confirm`, `ConfirmWarn`) | public | Warnung + Nachfrage. |

### 3.4 Der eine bewusste Eingriff am Bestand (transparent)

Die Logik „geparstes Ergebnis auf **einen** `SchachtRecord` anwenden" (inkl.
Feld-Alias-Mapping für Umlaut-/Encoding-Varianten und Protokoll-Aufbau) steckt heute
`private` in `LegacyPdfImportService.ImportSchachtPdf`. Um sie ohne Duplikat in beiden
Diensten zu nutzen, wird sie in eine neue **`internal` Helferklasse**
`SchachtProtocolApplier` (Infrastructure) herausgelöst:

- Neuer Dienst ruft `SchachtProtocolApplier.Apply(record, felder, schaeden, pdfPfad)`.
- `LegacyPdfImportService.ImportSchachtPdf` wird auf denselben Applier umgestellt —
  **verhaltensgleich** (die Such-/Anlege-Entscheidung bleibt dort, nur die
  Feld-/Protokoll-Schreibzeilen wandern in den Applier).

Damit der bestehende Ordner-Import identisch bleibt, wird ein
**Charakterisierungstest** ergänzt, der `ImportSchachtPdf` über einen Beispieltext vor
und nach dem Umbau auf gleiche Feld-/Protokoll-Ergebnisse prüft.

> Risiko-Hinweis: Dies ist der einzige Eingriff in funktionierenden Bestandscode. Wer
> das vermeiden will, kann alternativ `ImportSchachtPdf` nur duplizieren — das wird hier
> bewusst **nicht** gewählt (Doppelpflege der kniffligen Alias-Logik).

## 4. Ablauf im Detail

Beide Abläufe werden im `SchaechtePageViewModel`
(`src/AuswertungPro.Next.UI/ViewModels/Pages/SchaechtePageViewModel.cs`) als zwei neue
`IRelayCommand` verdrahtet (`RefreshProtocolCommand`, `ImportProtocolCommand`), analog zu
`SaveCommand`/`AddCommand`. Die Buttons kommen in `SchaechtePage.xaml` in die erste
Toolbar-Leiste (nach „Löschen", vor „Hoch"/„Runter").

### 4.1 „Aktualisieren" (RefreshProtocolCommand)

`CanExecute`: `Selected != null && Selected.GetFieldValue("PDF_Path")` nicht leer.

1. Warnung `ConfirmWarn("Der Schacht wird komplett aus dem Protokoll neu aufgebaut.
   Von Hand erfasste Werte gehen dabei verloren. Fortfahren?", "Aktualisieren")`.
   Bei „Nein" → Abbruch.
2. `PDF_Path` (relativ) über `ProjectPathResolver` gegen den Projektordner zu einem
   absoluten Pfad auflösen. Datei fehlt → `Warn(...)`, Abbruch.
3. `Parse(absPfad)`. Kein Schachtprotokoll / keine Nummer → `Warn(...)`, Abbruch.
4. `Apply(Selected, ergebnis, absPfad)` — überschreibt **denselben** Record.
5. Ansicht neu zeichnen + `_shell.TrySaveProject()` (bzw. `Project.Dirty = true` und
   speichern, wie die bestehenden Commands).

### 4.2 „Protokoll importieren" (ImportProtocolCommand)

1. Dateidialog (nur `*.pdf`) über den bestehenden Dialog-Dienst; nichts gewählt → Abbruch.
2. `Parse(pdfPfad)`. Kein Schachtprotokoll → `Warn("Das ist kein Schachtprotokoll …")`,
   Abbruch. Keine Schachtnummer → `Warn(...)`, Abbruch.
3. Schacht mit dieser Nummer im Projekt suchen (Vergleich wie bestehend über
   `Schachtnummer`/`Nr.`/`NR.`, normalisiert).
4. **Kollision** (gefunden): Nachfrage mit drei Optionen
   *Überschreiben / Als neuen anlegen / Abbrechen*.
   - Überschreiben → bestehenden Record als Ziel.
   - Als neuen anlegen → `new SchachtRecord()` + threadsicher zu `SchaechteData`.
   - Abbrechen → Ende.
   **Kein Treffer:** neuen `SchachtRecord` anlegen.
5. `DistributePdf(projekt, projektordner, schachtNr, pdfPfad)` → relativer Zielpfad.
6. `Apply(ziel, ergebnis, relativerZielpfad)` — Felder + Schäden + `PDF_Path` (relativ).
7. `_shell.TrySaveProject()`.

Für die 3-Wege-Nachfrage wird — falls `IDialogService` noch keine Ja/Nein/Abbrechen-
Methode bietet — `ConfirmYesNoCancel(message, title)` additiv ergänzt (kleine,
verhaltensarme Erweiterung; `DialogService` nutzt `MessageBoxButton.YesNoCancel`).

## 5. Datenmodell-Berührungspunkte

- Schrieb ausschließlich über `SchachtRecord.SetFieldValue(...)` und `SchachtRecord.Protocol`.
- Schäden: pro `(Bauteil, Schaden)` ein `ProtocolEntry { Code = Bauteil,
  Beschreibung = Schaden, Source = Imported }`; gesammelt in `Original`-Revision +
  Arbeitskopie `Current`; als `ProtocolDocument` gesetzt (ersetzt vorhandenes Protokoll).
- Kein neues Feld, kein Schema-Bruch, keine Änderung an `projekt.json`-Struktur.

## 6. Fehlerbehandlung / Randfälle

- PDF ohne Textebene: `PdfTextExtractor` fällt intern auf PdfPig zurück. Ein eigener
  OCR-Fallback ist **nicht** Teil dieses Features (siehe Scope). Bleibt der Text leer →
  „kein Schachtprotokoll erkannt".
- Kein `Schächte_Verteilt`-Ordner: `DistributePdf` legt ihn an (`Directory.CreateDirectory`).
- Zieldatei existiert schon: nicht überschreiben (idempotent, wie bestehender Distributor).
- Thread-Sicherheit: Neu-Anlegen in `SchaechteData` läuft unter dem bestehenden
  `CollectionLock`-Muster (`ImportRunContext.WithCollectionLock` bzw. direktes Lock).
- „Aktualisieren" ohne verknüpftes PDF: Button ausgegraut (kann nicht ausgelöst werden).

## 7. Tests (CLAUDE.md-Checkliste Punkt 4)

- **`SchachtProtocolImportService.Parse`**: Beispieltext → korrekte Schachtnummer,
  Felder, Schadensliste; Nicht-Schachtprotokoll → `IstSchachtprotokoll=false`.
- **`SchachtProtocolApplier.Apply`**: Record wird korrekt neu aufgebaut; vorhandenes
  Protokoll wird ersetzt; `PDF_Path` gesetzt.
- **Charakterisierungstest** für `LegacyPdfImportService.ImportSchachtPdf`
  (verhaltensgleich nach Umstellung auf den Applier).
- **Kollisions-Entscheidung** (reine Logik: gefunden/überschreiben/neu/abbrechen)
  als testbare Methode ausgelagert, damit sie ohne UI prüfbar ist.

VRAM-Budget und QualityGate sind nicht betroffen (reine CPU-/UI-Logik).

## 8. Nicht im Scope (YAGNI)

- Mehrfach-Auswahl / „alle Schächte aktualisieren".
- Zusammenführen (Merge) statt Überschreiben.
- Eigener OCR-Fallback für den Einzel-Import.
- Haltungsprotokolle über diese Knöpfe (nur Schachtprotokolle; andere werden abgelehnt).
- Änderungen an der Detailansicht (`SchachtansichtView`) — Knöpfe nur in der Toolbar.

## 9. Betroffene Dateien (Überblick)

**Neu**
- `src/AuswertungPro.Next.Infrastructure/Import/Protocols/SchachtProtocolImportService.cs`
- `src/AuswertungPro.Next.Infrastructure/Import/Protocols/SchachtProtocolApplier.cs` (internal)
- DTO `SchachtProtocolParseResult` (in `Application/Import`)
- Tests unter `tests/AuswertungPro.Next.*.Tests/`

**Geändert (klein)**
- `src/AuswertungPro.Next.Application/Import/IImportServices.cs` (Interface + DTO)
- `src/AuswertungPro.Next.Infrastructure/Import/Pdf/LegacyPdfImportService.cs`
  (nur: `ImportSchachtPdf` nutzt den Applier — verhaltensgleich)
- `src/AuswertungPro.Next.UI/ServiceProvider.cs` (Registrierung)
- `src/AuswertungPro.Next.UI/ViewModels/Pages/SchaechtePageViewModel.cs` (2 Commands)
- `src/AuswertungPro.Next.UI/Views/Pages/SchaechtePage.xaml` (2 Buttons)
- ggf. `src/AuswertungPro.Next.UI/Services/IDialogService.cs` + `DialogService.cs`
  (Ja/Nein/Abbrechen-Methode)

## 10. Risiken

- **Einziges Bestands-Risiko:** Umbau von `ImportSchachtPdf` auf den Applier. Durch
  Charakterisierungstest abgesichert; verhaltensgleich.
- Feld-Aliasse: die Umlaut-/Encoding-Varianten (`GetSchachtFieldAliases`) müssen im
  Applier vollständig erhalten bleiben — beim Herauslösen 1:1 mitnehmen.
- Relative vs. absolute `PDF_Path`: „Aktualisieren" muss relativen Pfad zuerst auflösen;
  „Importieren" schreibt relativen Pfad. Klar getrennt behandeln.
