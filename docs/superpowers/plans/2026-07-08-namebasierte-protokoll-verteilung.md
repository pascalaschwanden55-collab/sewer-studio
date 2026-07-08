# Name-basierte Protokoll-Verteilung Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Protokoll-PDFs narrensicher über den Namen (Datei-/Ordnername) auf Haltungen und Schächte verteilen — fehlende Schächte aus Protokollen anlegen, jede Quell-Variante trifft oder wird sichtbar gemeldet.

**Architecture:** Reiner `ProtocolNameResolver` (Pfad → Art+Name) + Service `NameBasedProtocolDistributor` (verteilt + legt Schächte an + Report), im `ServiceProvider` registriert. Angedockt an den Ein-Knopf-Import (name-basiert zuerst, Content-Split nur Fallback via vorhandenem `splitPdf`-Flag) und an einen „Verteil-Ordner wählen"-Befehl.

**Tech Stack:** .NET 10, C#, xUnit. Infrastructure-Layer.

## Global Constraints

- Thin-AI/Schichten: Kernlogik in Infrastructure, UI ruft Service. Resolver rein/testbar.
- Neuer Service mit Interface, im `ServiceProvider` registriert (CLAUDE.md-Checkliste).
- Additiv: bestehender Content-Split bleibt Fallback (`KanalImportDistributor` unverändert, nur anders aufgerufen).
- Deutsche Kommentare. Fokussierte Tests für Resolver + Distributor.
- Commits: ~68 unzusammenhängende uncommittete Dateien + teils bereits „dirty" Dateien → jede Task staged NUR eigene Dateien/Hunks (kein `git add -A`). `ImportPageViewModel.cs` und `ServiceProvider.cs` VOR dem Commit mit `git diff --stat -- <datei>` prüfen; bei Fremd-Hunks nur eigene Hunks per `git apply --cached` stagen.

## Domänen-Fakten (verifiziert)
- `FieldKeys.HoldingName = "Haltungsname"`, `FieldKeys.PdfPath = "PDF_Path"`. Schacht-Nummer-Feld: `"Schachtnummer"`.
- `HaltungRecord.SetFieldValue(string fieldName, string? value, FieldSource source, bool userEdited)`; `HaltungRecord.GetFieldValue(string) → string?`.
- `SchachtRecord.SetFieldValue(string fieldName, string? value)` (2 Argumente!); `SchachtRecord.GetFieldValue(string) → string?`. Anlage: `new SchachtRecord()` → `project.SchaechteData.Add(record)`.
- `ProjectStructure.HaltungVerteiltDir(projectFolder, san)` / `SchachtVerteiltDir(projectFolder, san)`; Konstanten `HaltungenVerteilt`/`SchaechteVerteilt`.
- `ProjectPathResolver.SanitizePathSegment(string?)`, `MakeRelative(absolute, projectFolder)`, `IsRelative(string?)`.
- `HoldingKeyNormalizer.Normalize(string?) → string`.
- `ProjectImportOrchestrator` (sealed, instanz, ctor-injektion), erzeugt in `ImportPageViewModel.cs:673`; ruft `KanalImportDistributor.Distribute(project, projectFolder, archivedPdfDir, sourceFolder, splitPdf: …, primaryProtocolPdf: …)` bei `:354`.
- `ServiceProvider`: Services als Properties + Zuweisung im ctor + `GetService`-switch.

---

### Task 1: ProtocolNameResolver (rein) + Tests

**Files:**
- Create: `src/AuswertungPro.Next.Infrastructure/Import/Protocols/ProtocolNameResolver.cs`
- Test: `tests/AuswertungPro.Next.Infrastructure.Tests/Import/ProtocolNameResolverTests.cs`

**Interfaces:**
- Produces: `enum ProtocolKind { Haltung, Schacht }`; `readonly record struct ProtocolTarget(ProtocolKind Kind, string Name)`; `static ProtocolTarget? ProtocolNameResolver.Resolve(string pdfPath)`.

- [ ] **Step 1: Failing test**

`tests/AuswertungPro.Next.Infrastructure.Tests/Import/ProtocolNameResolverTests.cs`:
```csharp
using AuswertungPro.Next.Infrastructure.Import.Protocols;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.Import;

public sealed class ProtocolNameResolverTests
{
    [Theory]
    [InlineData(@"D:\P\Importdateien\PDF\H_33390-36268.pdf", ProtocolKind.Haltung, "33390-36268")]
    [InlineData(@"D:\P\Importdateien\PDF\L_1273.01-7.34854.pdf", ProtocolKind.Haltung, "1273.01-7.34854")]
    [InlineData(@"D:\X\Schächte\27581\20260427_27581.pdf", ProtocolKind.Schacht, "27581")]
    [InlineData(@"D:\X\Haltungen\33390-36268\20260424_33390-36268.pdf", ProtocolKind.Haltung, "33390-36268")]
    [InlineData(@"D:\X\S_952.06.pdf", ProtocolKind.Schacht, "952.06")]
    [InlineData(@"D:\X\36051.pdf", ProtocolKind.Schacht, "36051")]
    public void Resolve_erkennt_art_und_name(string path, ProtocolKind kind, string name)
    {
        var t = ProtocolNameResolver.Resolve(path);
        Assert.NotNull(t);
        Assert.Equal(kind, t!.Value.Kind);
        Assert.Equal(name, t.Value.Name);
    }

    [Theory]
    [InlineData(@"D:\X\A3_Übersichtsplan.pdf")]
    [InlineData(@"D:\X\Haltungsliste.pdf")]
    [InlineData(@"D:\X\Haltungs-Statistik.pdf")]
    [InlineData(@"D:\X\30x105_Jagdmatt_200_orto.pdf")]
    [InlineData(@"D:\X\30x105_Jagdmatt_200_AV.pdf")]
    public void Resolve_ueberspringt_nicht_protokolle(string path)
        => Assert.Null(ProtocolNameResolver.Resolve(path));
}
```

- [ ] **Step 2: Test rot**

Run: `dotnet build tests/AuswertungPro.Next.Infrastructure.Tests/AuswertungPro.Next.Infrastructure.Tests.csproj --no-restore -v q`
Expected: FEHLER (CS0246 `ProtocolNameResolver`/`ProtocolKind` fehlen).

- [ ] **Step 3: Resolver anlegen**

`src/AuswertungPro.Next.Infrastructure/Import/Protocols/ProtocolNameResolver.cs`:
```csharp
using System;
using System.IO;
using System.Linq;

namespace AuswertungPro.Next.Infrastructure.Import.Protocols;

/// <summary>Art eines Protokolls: Haltung oder Schacht.</summary>
public enum ProtocolKind { Haltung, Schacht }

/// <summary>Zielangabe eines Protokoll-PDFs: Art + (unnormalisierter) Name.</summary>
public readonly record struct ProtocolTarget(ProtocolKind Kind, string Name);

/// <summary>
/// Ermittelt aus einem PDF-Pfad narrensicher (nur über Datei-/Ordnername, ohne PDF-Inhalt) die Art
/// (Haltung/Schacht) und den Namen. Reiner Helfer → unit-testbar. Nicht-Protokolle (Pläne, Listen,
/// Statistiken, orto/AV) werden übersprungen (null).
/// </summary>
public static class ProtocolNameResolver
{
    // Nicht-Protokolle: an diesen Namensbestandteilen erkennbar (klein geschrieben).
    private static readonly string[] NichtProtokoll =
        { "übersichtsplan", "uebersichtsplan", "ubersichtsplan", "haltungsliste",
          "statistik", "_orto", "_av", "uebersicht", "übersicht" };

    public static ProtocolTarget? Resolve(string pdfPath)
    {
        if (string.IsNullOrWhiteSpace(pdfPath))
            return null;

        var file = Path.GetFileNameWithoutExtension(pdfPath);
        var lowerFull = Path.GetFileName(pdfPath).ToLowerInvariant();
        if (NichtProtokoll.Any(p => lowerFull.Contains(p)))
            return null;

        var parent = new DirectoryInfo(Path.GetDirectoryName(pdfPath) ?? "").Name;

        // Name bereinigen: führendes YYYYMMDD_, Präfixe H_/L_/S_, Duplikat-Suffix _<ziffern>.
        var name = StripDatePrefix(file);
        var prefix = DetectPrefix(name);
        name = StripPrefix(name, prefix);
        name = StripDupSuffix(name).Trim();

        if (name.Length == 0 || !name.Any(char.IsDigit))
            return null; // sieht nicht nach Haltungs-/Schacht-Id aus

        // Art bestimmen: 1) Elternordner, 2) Präfix, 3) '-'-Heuristik.
        ProtocolKind kind;
        if (parent.Equals("Haltungen", StringComparison.OrdinalIgnoreCase))
            kind = ProtocolKind.Haltung;
        else if (parent.Equals("Schächte", StringComparison.OrdinalIgnoreCase) ||
                 parent.Equals("Schaechte", StringComparison.OrdinalIgnoreCase))
            kind = ProtocolKind.Schacht;
        else if (prefix is "H_" or "L_")
            kind = ProtocolKind.Haltung;
        else if (prefix is "S_")
            kind = ProtocolKind.Schacht;
        else
            kind = name.Contains('-') ? ProtocolKind.Haltung : ProtocolKind.Schacht;

        return new ProtocolTarget(kind, name);
    }

    private static string StripDatePrefix(string s)
    {
        // "20260427_27581" -> "27581"
        var us = s.IndexOf('_');
        if (us == 8 && s[..8].All(char.IsDigit))
            return s[(us + 1)..];
        return s;
    }

    private static string? DetectPrefix(string s)
    {
        if (s.StartsWith("H_", StringComparison.OrdinalIgnoreCase)) return "H_";
        if (s.StartsWith("L_", StringComparison.OrdinalIgnoreCase)) return "L_";
        if (s.StartsWith("S_", StringComparison.OrdinalIgnoreCase)) return "S_";
        return null;
    }

    private static string StripPrefix(string s, string? prefix)
        => prefix is null ? s : s[prefix.Length..];

    private static string StripDupSuffix(string s)
    {
        // "<basis>_1" -> "<basis>" (nur wenn Suffix rein numerisch)
        var us = s.LastIndexOf('_');
        if (us > 0 && us < s.Length - 1 && s[(us + 1)..].All(char.IsDigit))
            return s[..us];
        return s;
    }
}
```

- [ ] **Step 4: Tests grün**

Run: `dotnet build tests/AuswertungPro.Next.Infrastructure.Tests/AuswertungPro.Next.Infrastructure.Tests.csproj --no-restore -v q` (0 Fehler), dann
`dotnet test tests/AuswertungPro.Next.Infrastructure.Tests/AuswertungPro.Next.Infrastructure.Tests.csproj --no-build --filter "FullyQualifiedName~ProtocolNameResolver"`
Expected: `Bestanden! … erfolgreich: 11`.

- [ ] **Step 5: Commit**

```bash
git add src/AuswertungPro.Next.Infrastructure/Import/Protocols/ProtocolNameResolver.cs \
        tests/AuswertungPro.Next.Infrastructure.Tests/Import/ProtocolNameResolverTests.cs
git commit -m "feat(import): ProtocolNameResolver (Pfad -> Haltung/Schacht + Name)"
```

**Hinweis (Spec-Abweichung, bewusst):** Die Spec nannte zusätzlich `PdfDokumentTypErkennung` zur Nicht-Protokoll-Erkennung. Um den Resolver rein/testbar zu halten und gescannte Protokolle (leerer Text) nicht fälschlich zu verwerfen, erfolgt die Erkennung hier per Namensmuster — das deckt alle beobachteten Nicht-Protokolle ab. Für den Reviewer: dies ist eine absichtliche Vereinfachung, keine Lücke.

---

### Task 2: NameBasedProtocolDistributor + Interface + Report + Tests

**Files:**
- Create: `src/AuswertungPro.Next.Infrastructure/Import/Protocols/INameBasedProtocolDistributor.cs`
- Create: `src/AuswertungPro.Next.Infrastructure/Import/Protocols/NameBasedProtocolDistributor.cs`
- Test: `tests/AuswertungPro.Next.Infrastructure.Tests/Import/NameBasedProtocolDistributorTests.cs`

**Interfaces:**
- Consumes: `ProtocolNameResolver.Resolve` (Task 1); Domänen-Fakten oben.
- Produces: `sealed record ProtocolDistributionReport(int HaltungProtokolle, int SchachtProtokolle, int SchaechteAngelegt, IReadOnlyList<string> NichtZugeordnet, IReadOnlyList<string> Meldungen)`; `interface INameBasedProtocolDistributor { ProtocolDistributionReport Distribute(Project project, string projectFolder, string sourceFolder); }`; `class NameBasedProtocolDistributor : INameBasedProtocolDistributor`.

- [ ] **Step 1: Failing test**

`tests/AuswertungPro.Next.Infrastructure.Tests/Import/NameBasedProtocolDistributorTests.cs`:
```csharp
using System.IO;
using System.Linq;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Import.Protocols;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.Import;

public sealed class NameBasedProtocolDistributorTests
{
    private static string NewTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "nbpd_" + System.Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static HaltungRecord Haltung(string name)
    {
        var r = new HaltungRecord();
        r.SetFieldValue("Haltungsname", name, FieldSource.Manual, false);
        return r;
    }

    [Fact]
    public void Distribute_verteilt_haltung_und_legt_schacht_an()
    {
        var projectFolder = NewTempDir();
        var source = NewTempDir();
        try
        {
            // Quelle: 1 Haltungs-PDF (vertauschte Reihenfolge!) + 1 Schacht-PDF + 1 Nicht-Protokoll.
            File.WriteAllText(Path.Combine(source, "H_36268-33390.pdf"), "x"); // Projekt hat 33390-36268
            File.WriteAllText(Path.Combine(source, "S_27581.pdf"), "x");
            File.WriteAllText(Path.Combine(source, "Haltungsliste.pdf"), "x");

            var project = new Project();
            project.Data.Add(Haltung("33390-36268"));

            var report = new NameBasedProtocolDistributor().Distribute(project, projectFolder, source);

            // Haltung: PDF verteilt (trotz vertauschter Reihenfolge) + PDF_Path gesetzt.
            Assert.Equal(1, report.HaltungProtokolle);
            Assert.False(string.IsNullOrWhiteSpace(project.Data[0].GetFieldValue("PDF_Path")));
            Assert.True(Directory.EnumerateFiles(
                Path.Combine(projectFolder, "Haltungen_Verteilt"), "*.pdf", SearchOption.AllDirectories).Any());

            // Schacht: neu angelegt + verteilt.
            Assert.Equal(1, report.SchachtProtokolle);
            Assert.Equal(1, report.SchaechteAngelegt);
            var schacht = project.SchaechteData.Single();
            Assert.Equal("27581", schacht.GetFieldValue("Schachtnummer"));
            Assert.False(string.IsNullOrWhiteSpace(schacht.GetFieldValue("PDF_Path")));

            // Nicht-Protokoll ignoriert, keine „nicht zugeordnet".
            Assert.Empty(report.NichtZugeordnet);

            // Idempotent: zweiter Lauf legt keinen zweiten Schacht an.
            var report2 = new NameBasedProtocolDistributor().Distribute(project, projectFolder, source);
            Assert.Equal(0, report2.SchaechteAngelegt);
            Assert.Single(project.SchaechteData);
        }
        finally
        {
            Directory.Delete(projectFolder, true);
            Directory.Delete(source, true);
        }
    }

    [Fact]
    public void Distribute_meldet_nicht_zuordenbare_haltung()
    {
        var projectFolder = NewTempDir();
        var source = NewTempDir();
        try
        {
            File.WriteAllText(Path.Combine(source, "H_99999-88888.pdf"), "x"); // kein passender Record
            var project = new Project();
            project.Data.Add(Haltung("33390-36268"));

            var report = new NameBasedProtocolDistributor().Distribute(project, projectFolder, source);

            Assert.Equal(0, report.HaltungProtokolle);
            Assert.Contains(report.NichtZugeordnet, s => s.Contains("H_99999-88888"));
        }
        finally
        {
            Directory.Delete(projectFolder, true);
            Directory.Delete(source, true);
        }
    }
}
```

- [ ] **Step 2: Test rot**

Run: `dotnet build tests/AuswertungPro.Next.Infrastructure.Tests/AuswertungPro.Next.Infrastructure.Tests.csproj --no-restore -v q`
Expected: FEHLER (CS0246 `NameBasedProtocolDistributor`/`ProtocolDistributionReport`).

- [ ] **Step 3: Interface + Report**

`src/AuswertungPro.Next.Infrastructure/Import/Protocols/INameBasedProtocolDistributor.cs`:
```csharp
using System.Collections.Generic;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Infrastructure.Import.Protocols;

/// <summary>Ergebnis einer name-basierten Protokoll-Verteilung.</summary>
public sealed record ProtocolDistributionReport(
    int HaltungProtokolle,
    int SchachtProtokolle,
    int SchaechteAngelegt,
    IReadOnlyList<string> NichtZugeordnet,
    IReadOnlyList<string> Meldungen);

/// <summary>
/// Verteilt Protokoll-PDFs aus einem Quellordner name-basiert auf Haltungen und Schächte des Projekts.
/// </summary>
public interface INameBasedProtocolDistributor
{
    ProtocolDistributionReport Distribute(Project project, string projectFolder, string sourceFolder);
}
```

- [ ] **Step 4: Distributor**

`src/AuswertungPro.Next.Infrastructure/Import/Protocols/NameBasedProtocolDistributor.cs`:
```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Import.Common;

namespace AuswertungPro.Next.Infrastructure.Import.Protocols;

/// <summary>
/// Verteilt Protokoll-PDFs name-basiert (siehe <see cref="ProtocolNameResolver"/>): Haltungen werden
/// per (normalisiertem) Haltungsnamen gematcht — auch bei vertauschter Schacht-Reihenfolge —, Schächte
/// per Schachtnummer; fehlt der Schacht, wird er angelegt (Protokoll ist maßgebend). Nicht zuordenbare
/// PDFs landen im Report unter „nicht zugeordnet". Idempotent: gleiche Zieldatei wird nicht dupliziert.
/// </summary>
public sealed class NameBasedProtocolDistributor : INameBasedProtocolDistributor
{
    public ProtocolDistributionReport Distribute(Project project, string projectFolder, string sourceFolder)
    {
        int haltung = 0, schacht = 0, angelegt = 0;
        var nichtZugeordnet = new List<string>();
        var meldungen = new List<string>();

        if (!Directory.Exists(sourceFolder))
            return new ProtocolDistributionReport(0, 0, 0, nichtZugeordnet, new[] { $"Quellordner fehlt: {sourceFolder}" });

        var pdfs = Directory.EnumerateFiles(sourceFolder, "*.pdf", SearchOption.AllDirectories)
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase);

        foreach (var pdf in pdfs)
        {
            var target = ProtocolNameResolver.Resolve(pdf);
            if (target is null)
                continue; // Nicht-Protokoll -> stillschweigend überspringen

            try
            {
                if (target.Value.Kind == ProtocolKind.Haltung)
                {
                    var rec = FindHaltung(project, target.Value.Name);
                    if (rec is null) { nichtZugeordnet.Add(Path.GetFileName(pdf)); continue; }
                    var name = rec.GetFieldValue(FieldKeys.HoldingName) ?? target.Value.Name;
                    var dest = CopyInto(ProjectStructure.HaltungVerteiltDir(projectFolder, ProjectPathResolver.SanitizePathSegment(name)), pdf);
                    rec.SetFieldValue(FieldKeys.PdfPath, ProjectPathResolver.MakeRelative(dest, projectFolder), FieldSource.Legacy, userEdited: false);
                    haltung++;
                }
                else
                {
                    var rec = FindSchacht(project, target.Value.Name);
                    if (rec is null)
                    {
                        rec = new SchachtRecord();
                        rec.SetFieldValue("Schachtnummer", target.Value.Name);
                        project.SchaechteData.Add(rec);
                        angelegt++;
                    }
                    var nr = rec.GetFieldValue("Schachtnummer") ?? target.Value.Name;
                    var dest = CopyInto(ProjectStructure.SchachtVerteiltDir(projectFolder, ProjectPathResolver.SanitizePathSegment(nr)), pdf);
                    rec.SetFieldValue(FieldKeys.PdfPath, ProjectPathResolver.MakeRelative(dest, projectFolder));
                    schacht++;
                }
            }
            catch (Exception ex)
            {
                meldungen.Add($"{Path.GetFileName(pdf)}: {ex.Message}");
            }
        }

        return new ProtocolDistributionReport(haltung, schacht, angelegt, nichtZugeordnet, meldungen);
    }

    private static HaltungRecord? FindHaltung(Project project, string name)
    {
        var norm = HoldingKeyNormalizer.Normalize(name);
        var rec = project.Data.FirstOrDefault(r =>
            HoldingKeyNormalizer.Normalize(r.GetFieldValue(FieldKeys.HoldingName)) == norm);
        if (rec is not null) return rec;

        // Vertauschte Schacht-Reihenfolge A-B <-> B-A (nur bei genau einem '-').
        var parts = name.Split('-');
        if (parts.Length == 2)
        {
            var reversed = HoldingKeyNormalizer.Normalize(parts[1] + "-" + parts[0]);
            rec = project.Data.FirstOrDefault(r =>
                HoldingKeyNormalizer.Normalize(r.GetFieldValue(FieldKeys.HoldingName)) == reversed);
        }
        return rec;
    }

    private static SchachtRecord? FindSchacht(Project project, string nr)
    {
        var norm = HoldingKeyNormalizer.Normalize(nr);
        return project.SchaechteData.FirstOrDefault(r =>
            HoldingKeyNormalizer.Normalize(r.GetFieldValue("Schachtnummer")) == norm);
    }

    private static string CopyInto(string destDir, string sourcePdf)
    {
        Directory.CreateDirectory(destDir);
        var dest = Path.Combine(destDir, Path.GetFileName(sourcePdf));
        if (!File.Exists(dest))
            File.Copy(sourcePdf, dest, overwrite: false);
        return dest;
    }
}
```

- [ ] **Step 5: Tests grün**

Run: `dotnet build tests/AuswertungPro.Next.Infrastructure.Tests/AuswertungPro.Next.Infrastructure.Tests.csproj --no-restore -v q` (0 Fehler), dann
`dotnet test tests/AuswertungPro.Next.Infrastructure.Tests/AuswertungPro.Next.Infrastructure.Tests.csproj --no-build --filter "FullyQualifiedName~NameBasedProtocolDistributor"`
Expected: `Bestanden! … erfolgreich: 2`.

- [ ] **Step 6: Commit**

```bash
git add src/AuswertungPro.Next.Infrastructure/Import/Protocols/INameBasedProtocolDistributor.cs \
        src/AuswertungPro.Next.Infrastructure/Import/Protocols/NameBasedProtocolDistributor.cs \
        tests/AuswertungPro.Next.Infrastructure.Tests/Import/NameBasedProtocolDistributorTests.cs
git commit -m "feat(import): NameBasedProtocolDistributor (Haltung/Schacht by name + Report)"
```

---

### Task 3: DI-Registrierung im ServiceProvider

**Files:**
- Modify: `src/AuswertungPro.Next.UI/ServiceProvider.cs`

**Interfaces:**
- Consumes: `INameBasedProtocolDistributor`, `NameBasedProtocolDistributor` (Task 2).
- Produces: `ServiceProvider.NameBasedProtocolDistributor` (Property vom Typ `INameBasedProtocolDistributor`).

- [ ] **Step 1: Using + Property + Zuweisung + GetService**

In `src/AuswertungPro.Next.UI/ServiceProvider.cs`:
- `using AuswertungPro.Next.Infrastructure.Import.Protocols;` ergänzen (falls nicht vorhanden).
- Bei den Service-Properties (neben `public IPdfImportService PdfImport { get; }`, ~Z. 72) ergänzen:
```csharp
        public INameBasedProtocolDistributor NameBasedProtocolDistributor { get; }
```
- Im ctor bei den Zuweisungen (neben `PdfImport = new PdfImportServiceAdapter();`, ~Z. 125) ergänzen:
```csharp
            NameBasedProtocolDistributor = new NameBasedProtocolDistributor();
```
- Im `GetService`-Switch (neben `if (serviceType == typeof(IPdfImportService)) return PdfImport;`, ~Z. 283) ergänzen:
```csharp
            if (serviceType == typeof(INameBasedProtocolDistributor)) return NameBasedProtocolDistributor;
```

- [ ] **Step 2: Build**

Run: `dotnet build src/AuswertungPro.Next.UI/AuswertungPro.Next.UI.csproj --no-restore -t:Compile -v q 2>&1 | grep -iE "error CS|erfolgreich"`
Expected: `0 Fehler` / „erfolgreich" (Compile-Ziel wegen evtl. laufender App).

- [ ] **Step 3: Commit (nur eigenen Hunk)**

```bash
git diff --stat -- src/AuswertungPro.Next.UI/ServiceProvider.cs   # eigene Änderung prüfen
git add src/AuswertungPro.Next.UI/ServiceProvider.cs
git commit -m "feat(import): NameBasedProtocolDistributor im ServiceProvider registriert"
```
Falls `ServiceProvider.cs` Fremd-Hunks enthält: nur die eigenen 3 Hunks per `git apply --cached` stagen.

---

### Task 4: Ein-Knopf-Import andocken (name-basiert zuerst, Content-Split Fallback)

**Files:**
- Modify: `src/AuswertungPro.Next.Infrastructure/Import/ProjectImportOrchestrator.cs` (ctor + Aufruf bei ~Z. 354)
- Modify: `src/AuswertungPro.Next.UI/ViewModels/Pages/ImportPageViewModel.cs` (ctor-Aufruf bei ~Z. 673)

**Interfaces:**
- Consumes: `INameBasedProtocolDistributor` (Task 2), DI-Property (Task 3).

- [ ] **Step 1: Ctor-Feld im Orchestrator**

In `ProjectImportOrchestrator.cs` neben den anderen `private readonly`-Feldern:
```csharp
    private readonly AuswertungPro.Next.Infrastructure.Import.Protocols.INameBasedProtocolDistributor? _protocolDistributor;
```
Im ctor einen optionalen Parameter am Ende ergänzen (bestehende Aufrufer bleiben kompatibel):
```csharp
        AuswertungPro.Next.Infrastructure.Import.Protocols.INameBasedProtocolDistributor? protocolDistributor = null,
```
und zuweisen: `_protocolDistributor = protocolDistributor;`

- [ ] **Step 2: Name-basiert zuerst, dann Content-Split als Fallback**

Am Aufruf bei ~Z. 354. Direkt VOR `var distResult = KanalImportDistributor.Distribute(…)`:
```csharp
            // Name-basierte Protokoll-Verteilung zuerst (narrensicher, Dateiname-basiert).
            var nameBased = _protocolDistributor?.Distribute(project, projectFolder, archivedPdfDir);
            var nameBasedHits = (nameBased?.HaltungProtokolle ?? 0) + (nameBased?.SchachtProtokolle ?? 0);
            if (nameBased is not null)
            {
                messages.Add($"Protokolle name-basiert verteilt: {nameBased.HaltungProtokolle} Haltungen, {nameBased.SchachtProtokolle} Schächte, {nameBased.SchaechteAngelegt} Schächte angelegt.");
                foreach (var nz in nameBased.NichtZugeordnet)
                    messages.Add($"Protokoll nicht zugeordnet: {nz}");
            }
```
Und den bestehenden `splitPdf:`-Ausdruck so ergänzen, dass bei Namens-Treffern NICHT nochmal inhaltlich gesplittet wird:
```csharp
                splitPdf: nameBasedHits == 0 && (det.Format != KanalExportFormat.Kins || kinsGesamtprotokoll is not null),
```
(Vorher: `splitPdf: det.Format != KanalExportFormat.Kins || kinsGesamtprotokoll is not null,`.)

- [ ] **Step 3: VM übergibt den Distributor an den Orchestrator**

In `ImportPageViewModel.cs` beim `new ProjectImportOrchestrator(` (~Z. 673) das neue Argument ergänzen:
```csharp
            protocolDistributor: _sp.NameBasedProtocolDistributor);
```
(als letztes Argument; die anderen Argumente unverändert lassen).

- [ ] **Step 4: Build**

Run: `dotnet build src/AuswertungPro.Next.UI/AuswertungPro.Next.UI.csproj --no-restore -t:Compile -v q 2>&1 | grep -iE "error CS|erfolgreich"`
Expected: `0 Fehler`.

- [ ] **Step 5: Commit (nur eigene Hunks)**

```bash
git diff --stat -- src/AuswertungPro.Next.Infrastructure/Import/ProjectImportOrchestrator.cs \
                   src/AuswertungPro.Next.UI/ViewModels/Pages/ImportPageViewModel.cs
git add src/AuswertungPro.Next.Infrastructure/Import/ProjectImportOrchestrator.cs
# ImportPageViewModel.cs: nur den eigenen ctor-Argument-Hunk stagen (Datei kann fremde Hunks haben)
git add -p src/AuswertungPro.Next.UI/ViewModels/Pages/ImportPageViewModel.cs   # falls interaktiv nicht möglich: git apply --cached mit isoliertem Hunk
git commit -m "feat(import): Ein-Knopf-Import nutzt name-basierte Protokoll-Verteilung zuerst"
```

---

### Task 5: „Verteil-Ordner wählen" (Haltungen + Schächte in einem Rutsch)

**Files:**
- Modify: `src/AuswertungPro.Next.UI/ViewModels/Pages/ImportPageViewModel.cs` (`ImportSchachtPdfsFolderAsync`, ~Z. 227–261)

**Interfaces:**
- Consumes: `_sp.NameBasedProtocolDistributor` (Task 3), `_shell.GetProjectFolder()`.

- [ ] **Step 1: Methode auf name-basierten Distributor umstellen**

`ImportSchachtPdfsFolderAsync` (~Z. 227) so ersetzen, dass ein beliebiger Verteil-Ordner gewählt und darüber Haltungen + Schächte verteilt werden:
```csharp
    private async Task ImportSchachtPdfsFolderAsync()
    {
        var projectFolder = _shell.GetProjectFolder();
        if (string.IsNullOrWhiteSpace(projectFolder))
        {
            _sp.Dialogs.Info("Kein Projekt geöffnet.", "Protokolle verteilen");
            return;
        }

        var folder = _sp.Dialogs.SelectFolder("Verteil-Ordner mit Protokollen wählen", projectFolder);
        if (string.IsNullOrWhiteSpace(folder))
            return;

        var report = await Task.Run(() =>
            _sp.NameBasedProtocolDistributor.Distribute(_shell.Project, projectFolder, folder));

        _shell.Project.Dirty = true;
        _shell.SaveCommand.Execute(null);

        var text = $"Verteilt: {report.HaltungProtokolle} Haltungs-Protokolle, {report.SchachtProtokolle} Schacht-Protokolle" +
                   $" ({report.SchaechteAngelegt} Schächte neu angelegt).";
        if (report.NichtZugeordnet.Count > 0)
            text += $"\n\nNicht zugeordnet ({report.NichtZugeordnet.Count}):\n" + string.Join("\n", report.NichtZugeordnet.Take(30));
        _sp.Dialogs.Info(text, "Protokolle verteilen");
    }
```
(Der Button-Text/Command-Name `ImportSchachtPdfsFolderCommand` bleibt; nur das Verhalten wird name-basiert. Falls es einen sichtbaren Button-Label im XAML gibt, im selben Commit auf „Protokolle verteilen (Ordner)" anpassen — sonst unverändert lassen.)

- [ ] **Step 2: Build**

Run: `dotnet build src/AuswertungPro.Next.UI/AuswertungPro.Next.UI.csproj --no-restore -t:Compile -v q 2>&1 | grep -iE "error CS|erfolgreich"`
Expected: `0 Fehler`.

- [ ] **Step 3: Commit (nur eigenen Hunk)**

```bash
git diff --stat -- src/AuswertungPro.Next.UI/ViewModels/Pages/ImportPageViewModel.cs
git add src/AuswertungPro.Next.UI/ViewModels/Pages/ImportPageViewModel.cs   # nur eigener Hunk; sonst git apply --cached
git commit -m "feat(import): 'Verteil-Ordner waehlen' verteilt Haltungen + Schaechte name-basiert"
```

---

## Self-Review

**Spec-Abdeckung:** Name-basiert alle Layouts → T1 Resolver + T2 Distributor (Fallback-Kette Elternordner/Präfix/`-`; Content-Split-Fallback via T4 `splitPdf`). Haltung beide Reihenfolgen → T2 `FindHaltung`. Schacht anlegen (Protokoll maßgebend) → T2. Nicht-Protokolle raus → T1. Sichtbarer Report/kein stilles Verschlucken → `ProtocolDistributionReport` + T4/T5-Meldungen. DI-Service → T3. Andockpunkte Ein-Knopf + Verteil-Ordner → T4/T5. Idempotenz → T2 `CopyInto` + Schacht-Match vor Anlage.

**Placeholder-Scan:** kein TBD/TODO; alle Code-/Testblöcke vollständig.

**Typ-Konsistenz:** `ProtocolTarget`/`ProtocolKind` (T1) = Nutzung in T2. `ProtocolDistributionReport`-Felder (T2) = Nutzung in T4/T5. `INameBasedProtocolDistributor.Distribute(Project, string, string)` konsistent T2/T3/T4/T5. Feldkeys/`SetFieldValue`-Signaturen (Haltung 4-arg, Schacht 2-arg) korrekt verwendet.

**Bekannte Abweichung (für Reviewer):** Nicht-Protokoll-Erkennung per Namensmuster statt `PdfDokumentTypErkennung` (Begründung in Task 1).
