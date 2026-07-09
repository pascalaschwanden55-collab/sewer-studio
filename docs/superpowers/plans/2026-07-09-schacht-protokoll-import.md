# Schacht-Protokoll: Aktualisieren + Einzel-Import — Implementation Plan

> **For agentic workers (auch Codex):** Diesen Plan Task für Task abarbeiten. Jeder Task ist ein eigener TDD-Zyklus (Test schreiben → rot → implementieren → grün → committen). Schritte nutzen Checkbox-Syntax (`- [ ]`). Empfohlene Sub-Skills für Claude-Worker: superpowers:subagent-driven-development oder superpowers:executing-plans. Codex arbeitet die Tasks sequenziell ab.

**Goal:** Zwei neue Toolbar-Knöpfe auf der Schachtseite — „Aktualisieren" (verknüpftes Protokoll neu einlesen, Schacht komplett neu aufbauen, mit Warnung) und „Protokoll importieren" (einzelnes PDF wählen, bei doppelter Schachtnummer nachfragen, Datei nach `Schächte_Verteilt\<Nr>\` verteilen).

**Architecture:** Neuer, UI-freier Dienst `SchachtProtocolImportService` (Interface in Application, Impl in Infrastructure). Die vorhandene Parse-/Anwende-Logik aus `LegacyPdfImportService.ImportSchachtPdf` wird verhaltensgleich in eine gemeinsame internal-Klasse `SchachtProtocolApplier` herausgelöst und von beiden Seiten genutzt (kein Duplikat). Das ViewModel orchestriert nur die Dialoge.

**Tech Stack:** WPF/.NET 10, C# (Nullable/ImplicitUsings enable), CommunityToolkit.Mvvm, xUnit 2.7 (klassische Asserts). PDF-Text via `PdfTextExtractor` (pdftotext + PdfPig-Fallback).

## Global Constraints

- **Kommentare auf Deutsch** (CLAUDE.md).
- **Keine neuen NuGet-Pakete** ohne Rückfrage.
- **Neue Logik als eigener Service mit Interface**, DI-Registrierung im handgeschriebenen Service-Locator `ServiceProvider.cs` (kein `new` verstreut).
- **Test-Framework: xUnit 2.7.0**, klassische Asserts (`Assert.Equal(erwartet, ist)`, `Assert.True/Null/Same/Single/Contains`). **Kein FluentAssertions, kein Moq.**
- **Ziel-Test-Projekt** für Infrastructure-Logik: `tests/AuswertungPro.Next.Infrastructure.Tests/`. Testdaten inline als Rohtext oder Laufzeit-Dateien in `Path.GetTempPath()` mit Cleanup im `finally`.
- **Ordnernamen nie hartcodieren** — Zielordner nur über `ProjectStructure.SchachtVerteiltDir(...)`.
- **VRAM-Budget (max 29GB) und QualityGate** sind von diesem rein CPU-/UI-nahen Feature nicht betroffen und bleiben unverändert.
- Build: `dotnet build AuswertungPro.sln`. Tests: `dotnet test tests/AuswertungPro.Next.Infrastructure.Tests/AuswertungPro.Next.Infrastructure.Tests.csproj`.

---

### Task 1: Vertrag — DTO + Interface in Application

**Files:**
- Create: `src/AuswertungPro.Next.Application/Import/ISchachtProtocolImportService.cs`

**Interfaces:**
- Produces:
  - `record SchachtProtocolParseResult(bool IstSchachtprotokoll, string? Schachtnummer, string? Datum, string? Funktion, string? PrimaereSchaeden, string? Bemerkungen, string? Status, string? Link, IReadOnlyList<(string Bauteil, string Schaden)> Schaeden)`
  - `interface ISchachtProtocolImportService` mit `SchachtProtocolParseResult Parse(string pdfPfad)`, `SchachtRecord? FindSchacht(Project project, string? schachtnummer)`, `void Apply(SchachtRecord ziel, SchachtProtocolParseResult ergebnis, string pdfPfadFuerFeld)`, `string DistributePdf(string projektOrdner, string schachtnummer, string pdfQuelle)`.

Reiner Vertrag ohne Verhalten → kein Unit-Test, nur Compile-Gate.

- [ ] **Step 1: Datei mit DTO + Interface anlegen**

Create `src/AuswertungPro.Next.Application/Import/ISchachtProtocolImportService.cs`:

```csharp
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.Import;

/// <summary>
/// Ergebnis des Parsens EINES Schacht-Protokoll-PDFs. UI-frei, damit es im
/// ViewModel (Kollisionspruefung) und beim Anwenden wiederverwendet werden kann.
/// </summary>
public sealed record SchachtProtocolParseResult(
    bool IstSchachtprotokoll,
    string? Schachtnummer,
    string? Datum,
    string? Funktion,
    string? PrimaereSchaeden,
    string? Bemerkungen,
    string? Status,
    string? Link,
    IReadOnlyList<(string Bauteil, string Schaden)> Schaeden);

/// <summary>
/// Liest ein einzelnes Schacht-Protokoll-PDF und wendet es auf einen Schacht an
/// (Felder + Schaeden). Verteilt die PDF-Datei in die kanonische Projektstruktur.
/// Bewusst schlank und ohne UI, damit die Kernlogik testbar bleibt; Dialoge
/// (Warnung, Kollisions-Nachfrage) orchestriert das ViewModel.
/// </summary>
public interface ISchachtProtocolImportService
{
    /// <summary>Liest die PDF, prueft ob Schachtprotokoll, liefert Felder + Schaeden. Ohne Seiteneffekt.</summary>
    SchachtProtocolParseResult Parse(string pdfPfad);

    /// <summary>Findet einen Schacht per Schachtnummer (Aliase Schachtnummer/Nr./NR.). Null wenn keiner passt.</summary>
    SchachtRecord? FindSchacht(Project project, string? schachtnummer);

    /// <summary>Schreibt Felder + Schaeden + PDF_Path auf den gegebenen Record (baut ihn komplett neu auf).</summary>
    void Apply(SchachtRecord ziel, SchachtProtocolParseResult ergebnis, string pdfPfadFuerFeld);

    /// <summary>Kopiert die PDF nach Schaechte_Verteilt\&lt;Nr&gt;\ und gibt den relativen Projektpfad zurueck.</summary>
    string DistributePdf(string projektOrdner, string schachtnummer, string pdfQuelle);
}
```

- [ ] **Step 2: Build prüfen**

Run: `dotnet build src/AuswertungPro.Next.Application/AuswertungPro.Next.Application.csproj`
Expected: Build erfolgreich (0 Fehler).

- [ ] **Step 3: Commit**

```bash
git add src/AuswertungPro.Next.Application/Import/ISchachtProtocolImportService.cs
git commit -m "feat(schacht): Vertrag fuer Einzel-Protokoll-Import (DTO + Interface)"
```

---

### Task 2: SchachtProtocolApplier herauslösen + Legacy umstellen

**Files:**
- Create: `src/AuswertungPro.Next.Infrastructure/Import/Pdf/SchachtProtocolApplier.cs`
- Modify: `src/AuswertungPro.Next.Infrastructure/Import/Pdf/LegacyPdfImportService.cs` (Methode `ImportSchachtPdf`, Z.517-597; Helfer `SetSchachtField`/`GetSchachtFieldAliases`, Z.680-704)
- Test: `tests/AuswertungPro.Next.Infrastructure.Tests/SchachtProtocolApplierTests.cs`

**Interfaces:**
- Produces: `internal static class SchachtProtocolApplier` mit `IReadOnlyList<string> Apply(SchachtRecord target, string key, LegacyPdfImportService.ParsedSchachtFields parsed, IReadOnlyList<(string Component, string Damage)> damageEntries, string pdfPath)`.
- Consumes: `LegacyPdfImportService.ParsedSchachtFields` (public record, 7 Felder), `ProtocolEntry`/`ProtocolRevision`/`ProtocolDocument`, `SchachtRecord.SetFieldValue`.

- [ ] **Step 1: Failing test schreiben**

Create `tests/AuswertungPro.Next.Infrastructure.Tests/SchachtProtocolApplierTests.cs`:

```csharp
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Import.Pdf;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class SchachtProtocolApplierTests
{
    [Fact]
    public void Apply_SetztFelderPdfPfadUndProtokoll()
    {
        var parsed = new LegacyPdfImportService.ParsedSchachtFields(
            "74467", "02.10.2025", "Kontrollschacht", "BAC", null, "offen", null);
        var damages = new[] { ("Schachtdeckel", "gerissen"), ("Konus", "Riss") };
        var record = new SchachtRecord();

        var imported = SchachtProtocolApplier.Apply(record, "74467", parsed, damages, "C:/x/quelle.pdf");

        Assert.Equal("74467", record.GetFieldValue("Schachtnummer"));
        Assert.Equal("Kontrollschacht", record.GetFieldValue("Funktion"));
        Assert.Equal("C:/x/quelle.pdf", record.GetFieldValue("PDF_Path"));
        Assert.NotNull(record.Protocol);
        Assert.Equal(2, record.Protocol!.Original.Entries.Count);
        Assert.Equal("Schachtdeckel", record.Protocol!.Original.Entries[0].Code);
        Assert.Contains("Schachtnummer", imported);
        Assert.Contains("Protokoll (2 Beobachtungen)", imported);
    }
}
```

- [ ] **Step 2: Test ausführen, Fehlschlag bestätigen**

Run: `dotnet test tests/AuswertungPro.Next.Infrastructure.Tests/AuswertungPro.Next.Infrastructure.Tests.csproj --filter "FullyQualifiedName~SchachtProtocolApplierTests"`
Expected: FAIL — `SchachtProtocolApplier` existiert nicht (Compile-Fehler).

- [ ] **Step 3: SchachtProtocolApplier implementieren**

Create `src/AuswertungPro.Next.Infrastructure/Import/Pdf/SchachtProtocolApplier.cs` (Code 1:1 aus `ImportSchachtPdf` übernommen, inkl. Umlaut-Aliase):

```csharp
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.Infrastructure.Import.Pdf;

/// <summary>
/// Wendet ein geparstes Schachtprotokoll auf EINEN bestehenden SchachtRecord an
/// (Felder + PDF-Pfad + strukturiertes Protokoll). Herausgeloest aus
/// LegacyPdfImportService.ImportSchachtPdf, damit der Einzel-Import-Dienst dieselbe
/// Logik nutzt (kein Duplikat). Sucht/legt KEINEN Record an — das bleibt beim Aufrufer.
/// </summary>
internal static class SchachtProtocolApplier
{
    /// <summary>
    /// Schreibt die geparsten Felder + Schaeden auf <paramref name="target"/>.
    /// Gibt die Liste der (fuer die Import-Meldung relevanten) gesetzten Felder zurueck.
    /// </summary>
    public static IReadOnlyList<string> Apply(
        SchachtRecord target,
        string key,
        LegacyPdfImportService.ParsedSchachtFields parsed,
        IReadOnlyList<(string Component, string Damage)> damageEntries,
        string pdfPath)
    {
        SetSchachtField(target, "Schachtnummer", key);
        SetSchachtField(target, "NR.", key);
        SetSchachtField(target, "Nr.", key);

        if (!string.IsNullOrWhiteSpace(parsed.Datum))
            SetSchachtField(target, "Ausfuehrung Datum/Jahr", parsed.Datum);

        if (!string.IsNullOrWhiteSpace(parsed.Funktion))
            SetSchachtField(target, "Funktion", parsed.Funktion);

        if (!string.IsNullOrWhiteSpace(parsed.PrimaereSchaeden))
            SetSchachtField(target, "Primaere Schaeden", parsed.PrimaereSchaeden);

        if (!string.IsNullOrWhiteSpace(parsed.Bemerkungen))
            SetSchachtField(target, "Bemerkungen", parsed.Bemerkungen);

        if (!string.IsNullOrWhiteSpace(parsed.Link))
            SetSchachtField(target, "Link", parsed.Link);

        if (!string.IsNullOrWhiteSpace(parsed.Status))
            SetSchachtField(target, "Status offen/abgeschlossen", parsed.Status);

        // PDF-Pfad speichern fuer spaeteres Oeffnen per Rechtsklick
        target.SetFieldValue("PDF_Path", pdfPath);

        // Strukturiertes Protokoll aus Bauteil-Schaeden erstellen
        if (damageEntries.Count > 0)
        {
            var protocolEntries = damageEntries.Select(d => new ProtocolEntry
            {
                Code = d.Component,
                Beschreibung = d.Damage,
                Source = ProtocolEntrySource.Imported
            }).ToList();

            var originalRevision = new ProtocolRevision
            {
                Comment = $"Import aus PDF: {Path.GetFileName(pdfPath)}",
                Entries = protocolEntries
            };
            var currentRevision = new ProtocolRevision
            {
                Comment = "Arbeitskopie",
                Entries = protocolEntries.Select(e => new ProtocolEntry
                {
                    Code = e.Code,
                    Beschreibung = e.Beschreibung,
                    Source = e.Source
                }).ToList()
            };

            target.Protocol = new ProtocolDocument
            {
                HaltungId = key,
                Original = originalRevision,
                Current = currentRevision
            };
        }

        var imported = new List<string>();
        if (!string.IsNullOrWhiteSpace(parsed.SchachtNummer)) imported.Add("Schachtnummer");
        if (!string.IsNullOrWhiteSpace(parsed.Datum)) imported.Add("Ausfuehrung Datum/Jahr");
        if (!string.IsNullOrWhiteSpace(parsed.Funktion)) imported.Add("Funktion");
        if (!string.IsNullOrWhiteSpace(parsed.PrimaereSchaeden)) imported.Add("Primaere Schaeden");
        if (!string.IsNullOrWhiteSpace(parsed.Bemerkungen)) imported.Add("Bemerkungen");
        if (damageEntries.Count > 0) imported.Add($"Protokoll ({damageEntries.Count} Beobachtungen)");
        return imported;
    }

    private static void SetSchachtField(SchachtRecord record, string logicalField, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;

        foreach (var candidate in GetSchachtFieldAliases(logicalField))
            record.SetFieldValue(candidate, value);
    }

    private static IReadOnlyList<string> GetSchachtFieldAliases(string logicalField)
    {
        return logicalField switch
        {
            "Schachtnummer" => new[] { "Schachtnummer" },
            "Funktion" => new[] { "Funktion" },
            "Primaere Schaeden" => new[] { "Primäre Schäden", "Primaere Schaeden", "PrimÃ¤re SchÃ¤den" },
            "Bemerkungen" => new[] { "Bemerkungen" },
            "Link" => new[] { "Link" },
            "NR." => new[] { "NR.", "Nr." },
            "Nr." => new[] { "Nr.", "NR." },
            "Ausfuehrung Datum/Jahr" => new[] { "Ausführung Datum/Jahr", "Ausführung\nDatum/Jahr", "Ausfuehrung Datum/Jahr", "Ausfuehrung\nDatum/Jahr", "AusfÃ¼hrung Datum/Jahr" },
            "Status offen/abgeschlossen" => new[] { "Status offen/abgeschlossen", "Status\noffen/abgeschlossen" },
            _ => new[] { logicalField }
        };
    }
}
```

- [ ] **Step 4: Test ausführen, grün bestätigen**

Run: `dotnet test tests/AuswertungPro.Next.Infrastructure.Tests/AuswertungPro.Next.Infrastructure.Tests.csproj --filter "FullyQualifiedName~SchachtProtocolApplierTests"`
Expected: PASS.

- [ ] **Step 5: `ImportSchachtPdf` in LegacyPdfImportService auf den Applier umstellen**

In `src/AuswertungPro.Next.Infrastructure/Import/Pdf/LegacyPdfImportService.cs` den Block **Z.517 bis Z.596** (von `SetSchachtField(target, "Schachtnummer", key);` bis zur schließenden `stats.Messages.Add(...)`-Anweisung, also Feld-Anwendung + Protokoll-Aufbau + imported-Liste + Info-Message) ersetzen durch:

```csharp
        var damageEntries = ParseSchachtDamageEntries(fullText);
        var imported = SchachtProtocolApplier.Apply(target, key, parsed, damageEntries, pdfPath);

        project.ModifiedAtUtc = DateTime.UtcNow;
        project.Dirty = true;

        if (!created)
            stats.UpdatedRecords++;

        stats.Messages.Add(new ImportMessage
        {
            Level = "Info",
            Context = "PDF-SCHACHT",
            Message = $"Schacht importiert: {Path.GetFileName(pdfPath)} | Schacht={key} | Felder={string.Join(", ", imported)}"
        });
```

Wichtig: Zeilen 502-515 (Schlüssel bilden, Record suchen, ggf. `new SchachtRecord()` + `AddSchachtRecord`) bleiben **unverändert** davor stehen. Die Zeilen 577-596 des Originals (project.Dirty, UpdatedRecords, Info-Message) sind in obigem Block bereits enthalten — nicht doppelt stehen lassen.

- [ ] **Step 6: Verwaiste private Helfer in LegacyPdfImportService prüfen und entfernen**

Prüfen, ob `SetSchachtField` / `GetSchachtFieldAliases` sonst noch irgendwo in dieser Datei referenziert werden:

Run: `grep -nE "SetSchachtField|GetSchachtFieldAliases" src/AuswertungPro.Next.Infrastructure/Import/Pdf/LegacyPdfImportService.cs`
Expected: Nur noch die beiden Definitionen (Z.680-704), keine Aufrufe mehr.

Wenn keine Aufrufe mehr: die beiden Methoden `SetSchachtField` (Z.680-687) und `GetSchachtFieldAliases` (Z.689-704) aus `LegacyPdfImportService.cs` **löschen** (sie leben jetzt im Applier). Falls doch noch Aufrufe existieren: Methoden belassen.

- [ ] **Step 7: Voller Build + bestehende Schacht-Tests (Charakterisierung) grün**

Run: `dotnet build AuswertungPro.sln`
Expected: 0 Fehler.

Run: `dotnet test tests/AuswertungPro.Next.Infrastructure.Tests/AuswertungPro.Next.Infrastructure.Tests.csproj --filter "FullyQualifiedName~SchachtPdfImportMappingTests|FullyQualifiedName~SchachtProtocolParserTests|FullyQualifiedName~SchachtProtocolApplierTests"`
Expected: PASS — der End-to-End-Import (`SchachtPdfImportMappingTests`) verhält sich nach der Umstellung identisch.

- [ ] **Step 8: Commit**

```bash
git add src/AuswertungPro.Next.Infrastructure/Import/Pdf/SchachtProtocolApplier.cs src/AuswertungPro.Next.Infrastructure/Import/Pdf/LegacyPdfImportService.cs tests/AuswertungPro.Next.Infrastructure.Tests/SchachtProtocolApplierTests.cs
git commit -m "refactor(schacht): Anwende-Logik in SchachtProtocolApplier herausloesen (verhaltensgleich)"
```

---

### Task 3: SchachtProtocolImportService implementieren

**Files:**
- Create: `src/AuswertungPro.Next.Infrastructure/Import/Protocols/SchachtProtocolImportService.cs`
- Test: `tests/AuswertungPro.Next.Infrastructure.Tests/SchachtProtocolImportServiceTests.cs`

**Interfaces:**
- Consumes: `ISchachtProtocolImportService` + `SchachtProtocolParseResult` (Task 1); `SchachtProtocolApplier.Apply` (Task 2); `PdfTextExtractor.ExtractPages(string, string?) -> PdfTextExtraction(Pages, FullText)`; `LegacyPdfImportService.ParseSchachtFields(string) -> ParsedSchachtFields`; `SchachtProtocolParser.ParseSchachtDamageEntries(string) -> IReadOnlyList<(string Component, string Damage)>` (internal, gleiches Assembly); `ProjectStructure.SchachtVerteiltDir(string, string)`; `ProjectPathResolver.MakeRelative(string, string)`.
- Produces: `public sealed class SchachtProtocolImportService : ISchachtProtocolImportService` (parameterloser Konstruktor) und `internal static SchachtProtocolParseResult ParseFromText(string fullText)`.

- [ ] **Step 1: Failing tests schreiben**

Create `tests/AuswertungPro.Next.Infrastructure.Tests/SchachtProtocolImportServiceTests.cs`:

```csharp
using AuswertungPro.Next.Application.Import;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Import.Protocols;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class SchachtProtocolImportServiceTests
{
    [Fact]
    public void ParseFromText_MitSchachtprotokoll_LiefertFelder()
    {
        var text = string.Join("\n", new[]
        {
            "Schachtprotokoll   Nr. 74467",
            "Schachttyp Kontrollschacht",
            "Datum 02/10/2025"
        });

        var result = SchachtProtocolImportService.ParseFromText(text);

        Assert.True(result.IstSchachtprotokoll);
        Assert.Equal("74467", result.Schachtnummer);
        Assert.Equal("Kontrollschacht", result.Funktion);
    }

    [Fact]
    public void ParseFromText_OhneSchachtprotokoll_IstFalse()
    {
        var result = SchachtProtocolImportService.ParseFromText("Irgendein Haltungsprotokoll Text");

        Assert.False(result.IstSchachtprotokoll);
        Assert.Null(result.Schachtnummer);
        Assert.Empty(result.Schaeden);
    }

    [Fact]
    public void FindSchacht_FindetPerSchachtnummer()
    {
        var project = new Project();
        var schacht = new SchachtRecord();
        schacht.SetFieldValue("Schachtnummer", "74467");
        project.SchaechteData.Add(schacht);
        var svc = new SchachtProtocolImportService();

        var found = svc.FindSchacht(project, "74467");

        Assert.Same(schacht, found);
    }

    [Fact]
    public void FindSchacht_NullWennNichtVorhanden()
    {
        var svc = new SchachtProtocolImportService();

        Assert.Null(svc.FindSchacht(new Project(), "99999"));
    }

    [Fact]
    public void Apply_BautRecordNeuAuf()
    {
        var ergebnis = new SchachtProtocolParseResult(
            true, "74467", "02.10.2025", "Kontrollschacht", null, null, "offen", null,
            new[] { ("Schachtdeckel", "gerissen") });
        var schacht = new SchachtRecord();
        var svc = new SchachtProtocolImportService();

        svc.Apply(schacht, ergebnis, "Schächte_Verteilt/74467/quelle.pdf");

        Assert.Equal("74467", schacht.GetFieldValue("Schachtnummer"));
        Assert.Equal("Kontrollschacht", schacht.GetFieldValue("Funktion"));
        Assert.Equal("Schächte_Verteilt/74467/quelle.pdf", schacht.GetFieldValue("PDF_Path"));
        Assert.NotNull(schacht.Protocol);
        Assert.Single(schacht.Protocol!.Original.Entries);
        Assert.Equal("Schachtdeckel", schacht.Protocol!.Original.Entries[0].Code);
    }

    [Fact]
    public void DistributePdf_KopiertUndGibtRelativenPfad()
    {
        var root = Path.Combine(Path.GetTempPath(), "sst_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var src = Path.Combine(root, "quelle.pdf");
            File.WriteAllText(src, "%PDF-1.4 dummy");
            var svc = new SchachtProtocolImportService();

            var rel = svc.DistributePdf(root, "74467", src);

            var expected = Path.Combine(root, "Schächte_Verteilt", "74467", "quelle.pdf");
            Assert.True(File.Exists(expected));
            Assert.Contains("74467", rel);
            Assert.Contains("quelle.pdf", rel);
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch { /* Best effort */ }
        }
    }
}
```

- [ ] **Step 2: Tests ausführen, Fehlschlag bestätigen**

Run: `dotnet test tests/AuswertungPro.Next.Infrastructure.Tests/AuswertungPro.Next.Infrastructure.Tests.csproj --filter "FullyQualifiedName~SchachtProtocolImportServiceTests"`
Expected: FAIL — `SchachtProtocolImportService` existiert nicht.

- [ ] **Step 3: Service implementieren**

Create `src/AuswertungPro.Next.Infrastructure/Import/Protocols/SchachtProtocolImportService.cs`:

```csharp
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.Import;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Import.Pdf;

namespace AuswertungPro.Next.Infrastructure.Import.Protocols;

/// <summary>
/// Liest ein einzelnes Schacht-Protokoll-PDF und wendet es auf einen Schacht an.
/// Nutzt die bestehende Lese-/Schaden-Parser-Technik (PdfTextExtractor,
/// SchachtProtocolParser) und die gemeinsame Anwende-Logik (SchachtProtocolApplier).
/// </summary>
public sealed class SchachtProtocolImportService : ISchachtProtocolImportService
{
    public SchachtProtocolParseResult Parse(string pdfPfad)
    {
        var extraction = PdfTextExtractor.ExtractPages(pdfPfad);
        return ParseFromText(extraction.FullText);
    }

    /// <summary>Reine Text-&gt;Ergebnis-Logik, damit sie ohne echtes PDF testbar ist.</summary>
    internal static SchachtProtocolParseResult ParseFromText(string fullText)
    {
        var istSchacht = !string.IsNullOrWhiteSpace(fullText)
            && fullText.Contains("Schachtprotokoll", StringComparison.OrdinalIgnoreCase);
        if (!istSchacht)
            return new SchachtProtocolParseResult(
                false, null, null, null, null, null, null, null,
                Array.Empty<(string, string)>());

        var pf = LegacyPdfImportService.ParseSchachtFields(fullText);
        var damages = SchachtProtocolParser.ParseSchachtDamageEntries(fullText);
        return new SchachtProtocolParseResult(
            true, pf.SchachtNummer, pf.Datum, pf.Funktion, pf.PrimaereSchaeden,
            pf.Bemerkungen, pf.Status, pf.Link, damages);
    }

    public SchachtRecord? FindSchacht(Project project, string? schachtnummer)
    {
        if (string.IsNullOrWhiteSpace(schachtnummer)) return null;
        var key = schachtnummer.Trim();
        return project.SchaechteData.FirstOrDefault(r =>
            string.Equals((r.GetFieldValue("Schachtnummer") ?? "").Trim(), key, StringComparison.OrdinalIgnoreCase) ||
            string.Equals((r.GetFieldValue("Nr.") ?? "").Trim(), key, StringComparison.OrdinalIgnoreCase) ||
            string.Equals((r.GetFieldValue("NR.") ?? "").Trim(), key, StringComparison.OrdinalIgnoreCase));
    }

    public void Apply(SchachtRecord ziel, SchachtProtocolParseResult ergebnis, string pdfPfadFuerFeld)
    {
        var pf = new LegacyPdfImportService.ParsedSchachtFields(
            ergebnis.Schachtnummer, ergebnis.Datum, ergebnis.Funktion,
            ergebnis.PrimaereSchaeden, ergebnis.Bemerkungen, ergebnis.Status, ergebnis.Link);
        var key = (ergebnis.Schachtnummer ?? "").Trim();
        SchachtProtocolApplier.Apply(ziel, key, pf, ergebnis.Schaeden, pdfPfadFuerFeld);
    }

    public string DistributePdf(string projektOrdner, string schachtnummer, string pdfQuelle)
    {
        var destDir = ProjectStructure.SchachtVerteiltDir(projektOrdner, schachtnummer);
        Directory.CreateDirectory(destDir);
        var dest = Path.Combine(destDir, Path.GetFileName(pdfQuelle));
        if (!File.Exists(dest))
            File.Copy(pdfQuelle, dest, overwrite: false);
        return ProjectPathResolver.MakeRelative(dest, projektOrdner);
    }
}
```

Hinweis: `ProjectStructure` liegt im Namespace `AuswertungPro.Next.Infrastructure.Import` — falls der Compiler ihn nicht findet, `using AuswertungPro.Next.Infrastructure.Import;` ergänzen.

- [ ] **Step 4: Tests ausführen, grün bestätigen**

Run: `dotnet test tests/AuswertungPro.Next.Infrastructure.Tests/AuswertungPro.Next.Infrastructure.Tests.csproj --filter "FullyQualifiedName~SchachtProtocolImportServiceTests"`
Expected: PASS (alle 6 Tests).

- [ ] **Step 5: Commit**

```bash
git add src/AuswertungPro.Next.Infrastructure/Import/Protocols/SchachtProtocolImportService.cs tests/AuswertungPro.Next.Infrastructure.Tests/SchachtProtocolImportServiceTests.cs
git commit -m "feat(schacht): SchachtProtocolImportService (Parse/Find/Apply/DistributePdf)"
```

---

### Task 4: DI-Registrierung im ServiceProvider

**Files:**
- Modify: `src/AuswertungPro.Next.UI/ServiceProvider.cs` (Import-Region ~Z.80; Konstruktor ~Z.129; `GetService` ~Z.288)

**Interfaces:**
- Consumes: `SchachtProtocolImportService` (Task 3), `ISchachtProtocolImportService` (Task 1, Namespace bereits via `using AuswertungPro.Next.Application.Import;` importiert).
- Produces: `ServiceProvider.SchachtProtocolImport` (Property vom Typ `ISchachtProtocolImportService`) — das ViewModel greift in Task 5 darauf zu.

Reine Verdrahtung → verifiziert über Build (Task 5 nutzt die Property).

- [ ] **Step 1: Property in der Import-Region ergänzen**

In `src/AuswertungPro.Next.UI/ServiceProvider.cs`, in der `#region Import` (nach `PhotoImport`, vor `#endregion`):

```csharp
    // Einzel-Import eines Schacht-Protokolls (Aktualisieren + Protokoll importieren, Schachtseite).
    public ISchachtProtocolImportService SchachtProtocolImport { get; }
```

- [ ] **Step 2: Instanziierung im Konstruktor ergänzen**

Direkt nach `PhotoImport = new PhotoImportService();`:

```csharp
        SchachtProtocolImport = new AuswertungPro.Next.Infrastructure.Import.Protocols.SchachtProtocolImportService();
```

- [ ] **Step 3: Auflösung in `GetService` ergänzen**

Im `GetService(Type serviceType)`-Block, bei den anderen Import-Services:

```csharp
        if (serviceType == typeof(ISchachtProtocolImportService)) return SchachtProtocolImport;
```

- [ ] **Step 4: Build prüfen**

Run: `dotnet build src/AuswertungPro.Next.UI/AuswertungPro.Next.UI.csproj`
Expected: 0 Fehler.

- [ ] **Step 5: Commit**

```bash
git add src/AuswertungPro.Next.UI/ServiceProvider.cs
git commit -m "feat(schacht): SchachtProtocolImportService im ServiceProvider registrieren"
```

---

### Task 5: ViewModel — zwei Commands verdrahten

**Files:**
- Modify: `src/AuswertungPro.Next.UI/ViewModels/Pages/SchaechtePageViewModel.cs` (Command-Deklarationen ~Z.47-52; Ctor-Zuweisung ~Z.123-128; `OnSelectedChanged` ~Z.161-168; neue Handler-Methoden)

**Interfaces:**
- Consumes: `_sp.SchachtProtocolImport` (Task 4); `_sp.Dialogs` (`Warn`, `Info`, `ConfirmWarn(string,string,bool)`, `ConfirmCancel(string,string) -> DialogConfirm`, `OpenFile(string,string,string?) -> string?`); `_shell.GetProjectFolder() -> string?`; `_shell.TrySaveProject()`; `ProjectPathResolver.ResolveFilePathFromProjectFolder(string?, string?) -> string?`; `Records` (= `_shell.Project.SchaechteData`), `Selected`, `_shell.CollectionLock`, `LastResult`.
- Produces: `RefreshProtocolCommand`, `ImportProtocolCommand` (`IRelayCommand`) — verbraucht in Task 6 (XAML-Bindings).

- [ ] **Step 1: Benötigte usings sicherstellen**

Run: `grep -nE "using AuswertungPro.Next.Application.Common;|using AuswertungPro.Next.UI.Services;|using AuswertungPro.Next.Domain.Models;" src/AuswertungPro.Next.UI/ViewModels/Pages/SchaechtePageViewModel.cs`

Fehlende dieser drei `using`-Zeilen oben in der Datei ergänzen: `using AuswertungPro.Next.Application.Common;` (für `ProjectPathResolver`), `using AuswertungPro.Next.UI.Services;` (für `DialogConfirm`), `using AuswertungPro.Next.Domain.Models;` (für `SchachtRecord`).

- [ ] **Step 2: Command-Properties deklarieren**

Bei den bestehenden Command-Deklarationen (nach `public IRelayCommand SaveCommand { get; }`):

```csharp
    public IRelayCommand RefreshProtocolCommand { get; }
    public IRelayCommand ImportProtocolCommand { get; }
```

- [ ] **Step 3: Commands im Konstruktor verdrahten**

Bei den bestehenden Ctor-Zuweisungen (nach `SaveCommand = new RelayCommand(Save);`):

```csharp
        RefreshProtocolCommand = new RelayCommand(RefreshProtocol, CanRefreshProtocol);
        ImportProtocolCommand = new RelayCommand(ImportProtocol);
```

- [ ] **Step 4: `OnSelectedChanged` um Notify ergänzen**

In der bestehenden Methode `partial void OnSelectedChanged(SchachtRecord? value)` (bei den anderen `NotifyCanExecuteChanged`-Zeilen) ergänzen:

```csharp
        (RefreshProtocolCommand as RelayCommand)?.NotifyCanExecuteChanged();
```

- [ ] **Step 5: Handler-Methoden hinzufügen**

Bei den anderen privaten Handler-Methoden (z.B. nach `Save()`):

```csharp
    // "Aktualisieren": verknuepftes Protokoll neu einlesen -> Schacht komplett neu aufbauen (mit Warnung).
    private bool CanRefreshProtocol()
        => Selected is not null && !string.IsNullOrWhiteSpace(Selected.GetFieldValue("PDF_Path"));

    private void RefreshProtocol()
    {
        var schacht = Selected;
        if (schacht is null) return;

        var relPath = schacht.GetFieldValue("PDF_Path");
        if (string.IsNullOrWhiteSpace(relPath)) return;

        var projektOrdner = _shell.GetProjectFolder();
        if (string.IsNullOrWhiteSpace(projektOrdner))
        {
            _sp.Dialogs.Info("Kein Projekt geöffnet.", "Aktualisieren");
            return;
        }

        if (!_sp.Dialogs.ConfirmWarn(
                "Der Schacht wird komplett aus dem Protokoll neu aufgebaut. Von Hand erfasste Werte gehen dabei verloren. Fortfahren?",
                "Aktualisieren"))
            return;

        var absPath = ProjectPathResolver.ResolveFilePathFromProjectFolder(relPath, projektOrdner);
        if (absPath is null)
        {
            _sp.Dialogs.Warn("Die verknüpfte Protokoll-Datei wurde nicht gefunden.", "Aktualisieren");
            return;
        }

        var ergebnis = _sp.SchachtProtocolImport.Parse(absPath);
        if (!ergebnis.IstSchachtprotokoll || string.IsNullOrWhiteSpace(ergebnis.Schachtnummer))
        {
            _sp.Dialogs.Warn("Das verknüpfte PDF ist kein lesbares Schachtprotokoll.", "Aktualisieren");
            return;
        }

        // Relativen Pfad behalten (Datei liegt bereits im Projekt).
        _sp.SchachtProtocolImport.Apply(schacht, ergebnis, relPath);

        _shell.Project.ModifiedAtUtc = DateTime.UtcNow;
        _shell.Project.Dirty = true;
        _shell.TrySaveProject();
        LastResult = $"Schacht {ergebnis.Schachtnummer} aktualisiert ({ergebnis.Schaeden.Count} Beobachtungen).";
    }

    // "Protokoll importieren": einzelnes PDF waehlen -> bei Kollision nachfragen -> verteilen + anwenden.
    private void ImportProtocol()
    {
        var projektOrdner = _shell.GetProjectFolder();
        if (string.IsNullOrWhiteSpace(projektOrdner))
        {
            _sp.Dialogs.Info("Kein Projekt geöffnet.", "Protokoll importieren");
            return;
        }

        var pdfPfad = _sp.Dialogs.OpenFile("Protokoll importieren", "PDF (*.pdf)|*.pdf");
        if (string.IsNullOrWhiteSpace(pdfPfad)) return;

        var ergebnis = _sp.SchachtProtocolImport.Parse(pdfPfad);
        if (!ergebnis.IstSchachtprotokoll)
        {
            _sp.Dialogs.Warn("Das gewählte PDF ist kein Schachtprotokoll.", "Protokoll importieren");
            return;
        }
        if (string.IsNullOrWhiteSpace(ergebnis.Schachtnummer))
        {
            _sp.Dialogs.Warn("Im Protokoll wurde keine Schachtnummer gefunden.", "Protokoll importieren");
            return;
        }

        var vorhanden = _sp.SchachtProtocolImport.FindSchacht(_shell.Project, ergebnis.Schachtnummer);
        SchachtRecord ziel;
        if (vorhanden is not null)
        {
            var wahl = _sp.Dialogs.ConfirmCancel(
                $"Schacht {ergebnis.Schachtnummer} ist bereits vorhanden.\n\n" +
                "Ja = Überschreiben\nNein = Als neuen Schacht anlegen\nAbbrechen = Nichts tun",
                "Protokoll importieren");

            if (wahl == DialogConfirm.Cancel) return;
            if (wahl == DialogConfirm.Yes)
            {
                ziel = vorhanden;
            }
            else
            {
                ziel = new SchachtRecord();
                lock (_shell.CollectionLock) { Records.Add(ziel); }
            }
        }
        else
        {
            ziel = new SchachtRecord();
            lock (_shell.CollectionLock) { Records.Add(ziel); }
        }

        var relPath = _sp.SchachtProtocolImport.DistributePdf(projektOrdner, ergebnis.Schachtnummer, pdfPfad);
        _sp.SchachtProtocolImport.Apply(ziel, ergebnis, relPath);
        Selected = ziel;

        _shell.Project.ModifiedAtUtc = DateTime.UtcNow;
        _shell.Project.Dirty = true;
        _shell.TrySaveProject();
        LastResult = $"Protokoll importiert: Schacht {ergebnis.Schachtnummer} ({ergebnis.Schaeden.Count} Beobachtungen).";
    }
```

Hinweis: Falls `Records` in dieser Klasse anders heißt (Property, die `_shell.Project.SchaechteData` liefert), den bestehenden Namen verwenden — `grep -n "SchaechteData\|Records" src/AuswertungPro.Next.UI/ViewModels/Pages/SchaechtePageViewModel.cs` zeigt den Collection-Zugriff, den `Add`/`Remove` bereits nutzen; denselben verwenden.

- [ ] **Step 6: Build prüfen**

Run: `dotnet build src/AuswertungPro.Next.UI/AuswertungPro.Next.UI.csproj`
Expected: 0 Fehler.

- [ ] **Step 7: Commit**

```bash
git add src/AuswertungPro.Next.UI/ViewModels/Pages/SchaechtePageViewModel.cs
git commit -m "feat(schacht): Commands Aktualisieren + Protokoll importieren im ViewModel"
```

---

### Task 6: XAML — zwei Toolbar-Buttons

**Files:**
- Modify: `src/AuswertungPro.Next.UI/Views/Pages/SchaechtePage.xaml` (erste Toolbar-Leiste, nach dem „Runter"-Button ~Z.64)

**Interfaces:**
- Consumes: `RefreshProtocolCommand`, `ImportProtocolCommand` (Task 5).

- [ ] **Step 1: Buttons einfügen**

In `src/AuswertungPro.Next.UI/Views/Pages/SchaechtePage.xaml`, direkt **nach** dem „Runter"-Button (`Command="{Binding MoveDownCommand}"`, endet ~Z.64) und noch **innerhalb** des Toolbar-`StackPanel`, einfügen:

```xml
                <Border Width="1" Background="{DynamicResource BorderBrush}" Margin="6,4"/>

                <Button Command="{Binding RefreshProtocolCommand}" Style="{StaticResource ToolbarButton}" Margin="0,0,2,0"
                        ToolTip="Verknüpftes Protokoll neu einlesen (Schacht wird neu aufgebaut)">
                    <StackPanel Orientation="Horizontal">
                        <TextBlock Text="&#xE72C;" FontFamily="Segoe MDL2 Assets" FontSize="13" VerticalAlignment="Center" Margin="0,0,6,0"/>
                        <TextBlock Text="Aktualisieren" VerticalAlignment="Center"/>
                    </StackPanel>
                </Button>
                <Button Command="{Binding ImportProtocolCommand}" Style="{StaticResource ToolbarButton}" Margin="0,0,2,0"
                        ToolTip="Einzelnes Schacht-Protokoll (PDF) importieren und ins Projekt verteilen">
                    <StackPanel Orientation="Horizontal">
                        <TextBlock Text="&#xE8E5;" FontFamily="Segoe MDL2 Assets" FontSize="13" Foreground="{DynamicResource AccentBrush}" VerticalAlignment="Center" Margin="0,0,6,0"/>
                        <TextBlock Text="Protokoll importieren" VerticalAlignment="Center"/>
                    </StackPanel>
                </Button>
```

- [ ] **Step 2: Build (XAML-Kompilierung) prüfen**

Run: `dotnet build src/AuswertungPro.Next.UI/AuswertungPro.Next.UI.csproj`
Expected: 0 Fehler (XAML kompiliert, Bindings auflösbar).

- [ ] **Step 3: Manuelle Verifikation (End-to-End)**

App starten, Projekt öffnen, Schachtseite öffnen. Prüfen:
1. Beide neue Buttons erscheinen in der Toolbar.
2. „Aktualisieren" ist ausgegraut, solange kein Schacht ausgewählt ist bzw. der Schacht kein verknüpftes `PDF_Path` hat.
3. Bei einem Schacht mit verknüpftem PDF: „Aktualisieren" zeigt die Warnung, baut nach „Ja" Felder + Schäden neu auf.
4. „Protokoll importieren" öffnet den Dateidialog; nach Wahl eines Schachtprotokoll-PDFs wird der Schacht angelegt/aktualisiert, das PDF liegt unter `Schächte_Verteilt\<Nr>\`, und bei bereits vorhandener Nummer erscheint die Ja/Nein/Abbrechen-Nachfrage.

- [ ] **Step 4: Commit**

```bash
git add src/AuswertungPro.Next.UI/Views/Pages/SchaechtePage.xaml
git commit -m "feat(schacht): Toolbar-Buttons Aktualisieren + Protokoll importieren"
```

---

## Self-Review (durchgeführt)

**Spec-Abdeckung:** Zwei Buttons (Task 6) ✓; Aktualisieren mit Warnung + komplett neu aufbauen (Task 5, `RefreshProtocol` + `ConfirmWarn`) ✓; Aktualisieren nur bei verknüpftem PDF aktiv (Task 5, `CanRefreshProtocol`) ✓; Import mit Kollisions-Nachfrage Ja/Nein/Abbrechen (Task 5, `ConfirmCancel`) ✓; Datei-Verteilung nach `Schächte_Verteilt\<Nr>\` (Task 3, `DistributePdf`) ✓; eigener Dienst + Interface + DI (Tasks 1/3/4) ✓; verhaltensgleicher Bestandseingriff mit Charakterisierung (Task 2, Step 7) ✓; Nur-Schachtprotokoll-Ablehnung (Task 5, `IstSchachtprotokoll`-Prüfungen) ✓; Tests für Parse/Apply/Find/Distribute (Tasks 2/3) ✓.

**Typ-Konsistenz:** `SchachtProtocolParseResult` (9 Felder) einheitlich in Tasks 1/3/5; `Apply(...)`-Signaturen zwischen Applier (Task 2) und Service (Task 3) passen; `ConfirmCancel -> DialogConfirm` (Yes/No/Cancel) existiert bereits.

**Nicht im Scope (YAGNI):** Mehrfach-Auswahl, Merge statt Überschreiben, eigener OCR-Fallback, Haltungsprotokolle über diese Buttons, Änderungen an der Detailansicht.
