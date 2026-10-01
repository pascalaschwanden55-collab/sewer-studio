# Dossier-Stapelanlage aus Grundbuch- und Netzdaten (Umsetzungsplan)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ein Knopf im Dossier-Bereich ermittelt die Parzellen eines Projekts, holt Eigentümer und betroffene private Leitungen und legt nach Bestätigung die Eigentümerdossiers an.

**Architecture:** Drei kleine Leser in Infrastructure (Parzellen-WFS, Grundbuchauskunft, Abwassernetz-WFS) hinter je einem Vertrag in Application. Jeder Leser ist in einen **puren Parser** (Text rein, Datensätze raus) und einen dünnen HTTP-Teil zerlegt — dadurch laufen alle Regeltests ohne Internet. Die Regeln liegen in zwei Anwendungsfällen ohne Netzzugriff, das Fenster enthält keine Regel.

**Tech Stack:** C# / .NET 10, WPF, `System.Net.Http`, `System.Xml.Linq`, xUnit. **Keine neuen NuGet-Pakete.**

**Spec:** `docs/superpowers/specs/2026-08-23-dossier-stapelanlage-grundbuch-design.md`

## Global Constraints

- **Keine neuen NuGet-Pakete.** Alles mit der Standardbibliothek.
- **Kommentare und sichtbare Texte auf Deutsch.**
- **Keine echten Personendaten** in Tests, Fixtures, Kommentaren oder Commit-Nachrichten. Nur erfundene Namen wie `Martin Muster`, `Kurt Beispiel`, `Rita Beispiel`.
- **Keine Telefonsuche.** Die Nutzungsbedingungen des Verzeichnisses untersagen maschinelle Massenabfragen.
- **Application darf kein Netz kennen.** Kein `HttpClient` in `AuswertungPro.Next.Application`.
- **Bei Unsicherheit nichts eintragen.** Ein Parser, der die Seite nicht sicher versteht, liefert `null` — nie einen geratenen Wert.
- **Nichts wird geschrieben, bevor der Benutzer bestätigt.**
- **Alle Regeltests laufen ohne Internet.** Nur ein einziger, ausdrücklich benannter Abnahmetest spricht mit den echten Diensten.
- **Kein neuer übersprungener Test** — `UebersprungeneTestsWaechterTests` hält genau sieben zulässige Skip-Stellen fest und wird sonst rot.
- Bauen: `dotnet build AuswertungPro.sln` · Testen: `dotnet test AuswertungPro.sln`
- Gemessene Dienstdetails, Fehlerpfade und die Testtabelle stehen in der Spec.

---

## Dateiübersicht

| Datei | Verantwortung |
|---|---|
| `src/AuswertungPro.Next.Application/Dossiers/Lookup/LookupResults.cs` (neu) | die schlichten Ergebnis-Datensätze |
| `.../Lookup/IParcelLookup.cs` (neu) | Vertrag Parzellendienst |
| `.../Lookup/ILandRegistryLookup.cs` (neu) | Vertrag Grundbuchauskunft |
| `.../Lookup/ISewerNetworkLookup.cs` (neu) | Vertrag Abwassernetz |
| `.../Lookup/ParcelNumberFromHoldingName.cs` (neu) | reine Regel: `439.01-36051` → `439` |
| `.../Lookup/DossierNameBuilder.cs` (neu) | reine Regel: Dossiername |
| `.../Lookup/DossierBatchProposal.cs` (neu) | Vorschlagsmodell |
| `.../Lookup/DossierBatchProposalUseCase.cs` (neu) | führt beide Wege zusammen, kein Netz |
| `.../Lookup/DossierBatchCreationUseCase.cs` (neu) | Vorschläge → `DossierDefinition` |
| `src/AuswertungPro.Next.Infrastructure/Dossiers/Lookup/LandRegistryHtmlParser.cs` (neu) | **pur**: HTML → `LandRegistryEntry` |
| `.../Lookup/ParcelWfsXmlParser.cs` (neu) | **pur**: WFS-XML → `ParcelInfo` |
| `.../Lookup/SewerNetworkWfsXmlParser.cs` (neu) | **pur**: WFS-XML → `NetworkHolding` |
| `.../Lookup/GeoUrHttpGateway.cs` (neu) | HTTP: Zeitlimit, Abbruch, Aufrufe der Reihe nach |
| `.../Lookup/UriParcelWfsClient.cs` (neu) | HTTP + Parser für Parzellen |
| `.../Lookup/UriLandRegistryClient.cs` (neu) | HTTP + Parser für die Grundbuchauskunft |
| `.../Lookup/UriSewerNetworkWfsClient.cs` (neu) | HTTP + Parser für das Abwassernetz |
| `src/AuswertungPro.Next.Domain/Models/Dossiers/DossierModels.cs` (ändern) | `Municipality`, `MunicipalityBfsNr`, Formatversion 3 |
| `src/AuswertungPro.Next.Infrastructure/Dossiers/DossierComposition.cs` (ändern) | Leser zusammenbauen |
| `src/AuswertungPro.Next.UI/ServiceProvider.Dossiers.cs` (ändern) | Leser durchreichen |
| `src/AuswertungPro.Next.UI/Views/Windows/DossierBatchWindow.xaml(.cs)` (neu) | Fenster ohne Regel |
| `src/AuswertungPro.Next.UI/ViewModels/Windows/DossierBatchViewModel.cs` (neu) | Fortschritt, Auswahl |
| `src/AuswertungPro.Next.UI/Views/Pages/DossiersPage.xaml` (ändern) | Knopf |
| `src/AuswertungPro.Next.UI/ViewModels/Pages/DossiersPageViewModel.Actions.cs` (ändern) | Befehl |
| `tests/Fixtures/DossierLookup/*` (neu) | erfundene Antwortbeispiele |
| `tests/AuswertungPro.Next.Infrastructure.Tests/Dossiers/Lookup/*` (neu) | alle Tests |

---

### Task 1: Parzellennummer aus dem Haltungsnamen

Die Knotenform `<Parzelle>.<lfd>` nennt die Parzelle. Reine Regel, kein Netz.

**Files:**
- Create: `src/AuswertungPro.Next.Application/Dossiers/Lookup/ParcelNumberFromHoldingName.cs`
- Test: `tests/AuswertungPro.Next.Infrastructure.Tests/Dossiers/Lookup/ParcelNumberFromHoldingNameTests.cs`

**Interfaces:**
- Consumes: nichts
- Produces: `public static class ParcelNumberFromHoldingName` mit
  `public static IReadOnlyList<string> Extract(string? holdingName)` und
  `public static IReadOnlyList<string> ExtractAll(IEnumerable<string?> holdingNames)`

- [ ] **Step 1: Den fehlschlagenden Test schreiben**

```csharp
using System;
using System.Collections.Generic;
using System.Linq;

using AuswertungPro.Next.Application.Dossiers.Lookup;

using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.Dossiers.Lookup;

public sealed class ParcelNumberFromHoldingNameTests
{
    [Theory]
    // Ein Knoten der Form <Parzelle>.<lfd> nennt seine Parzelle.
    [InlineData("439.01-36051", "439")]
    [InlineData("36051-439.02", "439")]
    [InlineData("438.03-438.04", "438")]
    [InlineData("1273.01-7.34854", "1273")]
    public void Erkennt_die_Parzelle_aus_der_Knotenform(string name, string erwartet)
    {
        Assert.Equal(new[] { erwartet }, ParcelNumberFromHoldingName.Extract(name));
    }

    [Fact]
    public void Zwei_verschiedene_Parzellen_ergeben_zwei_Nummern()
    {
        var treffer = ParcelNumberFromHoldingName.Extract("952.02-982.03");

        Assert.Equal(new[] { "952", "982" }, treffer);
    }

    [Theory]
    // Reine Schachtnummern nennen keine Parzelle.
    [InlineData("36262-36275")]
    [InlineData("33850-7.25390")]
    [InlineData("")]
    [InlineData(null)]
    public void Ohne_Knotenform_gibt_es_keine_Nummer(string? name)
    {
        Assert.Empty(ParcelNumberFromHoldingName.Extract(name));
    }

    [Fact]
    public void ExtractAll_fasst_zusammen_und_entdoppelt()
    {
        var treffer = ParcelNumberFromHoldingName.ExtractAll(new[]
        {
            "439.01-36051", "439.02-36051", "952.02-952.03", "36262-36275", null
        });

        Assert.Equal(new[] { "439", "952" }, treffer.OrderBy(t => t.Length).ThenBy(t => t).ToArray());
    }
}
```

**Warum `7.34854` keine Parzelle ist:** Die Regel verlangt mindestens zwei Ziffern vor dem
Punkt. `7.34854` ist ein Schachtname aus dem Bestand, keine Parzelle — das war in den echten
Daten so.

- [ ] **Step 2: Test laufen lassen und Fehlschlag prüfen**

```bash
dotnet test tests/AuswertungPro.Next.Infrastructure.Tests/AuswertungPro.Next.Infrastructure.Tests.csproj --filter "FullyQualifiedName~ParcelNumberFromHoldingNameTests"
```

Erwartet: Übersetzungsfehler, `ParcelNumberFromHoldingName` unbekannt.

- [ ] **Step 3: Die Umsetzung schreiben**

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace AuswertungPro.Next.Application.Dossiers.Lookup;

/// <summary>
/// Liest die Parzellennummer aus einem Haltungsnamen.
///
/// Im Bestand heissen die Knoten privater Hausanschluesse nach ihrer Parzelle:
/// "439.01-36051" laeuft auf Parzelle 439. Der Kanton fuehrt diese Leitungen in
/// seiner oeffentlichen Netzebene groesstenteils NICHT — diese Regel ist deshalb
/// der einzige Weg, sie einer Parzelle zuzuordnen, und kostet keine Abfrage.
///
/// Die Regel liefert nur einen KANDIDATEN. Ob es die Parzelle wirklich gibt,
/// muss der Parzellendienst bestaetigen.
/// </summary>
public static class ParcelNumberFromHoldingName
{
    // Mindestens zwei Ziffern vor dem Punkt: "7.34854" ist ein Schachtname aus
    // dem Bestand, keine Parzelle.
    private static readonly Regex KnotenMitParzelle = new(
        @"^(\d{2,5})\.\d{1,3}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static IReadOnlyList<string> Extract(string? holdingName)
    {
        if (string.IsNullOrWhiteSpace(holdingName))
            return Array.Empty<string>();

        var treffer = new List<string>();

        foreach (var teil in holdingName.Split('-', StringSplitOptions.RemoveEmptyEntries))
        {
            var match = KnotenMitParzelle.Match(teil.Trim());
            if (!match.Success)
                continue;

            var nummer = match.Groups[1].Value;
            if (!treffer.Contains(nummer, StringComparer.Ordinal))
                treffer.Add(nummer);
        }

        return treffer;
    }

    public static IReadOnlyList<string> ExtractAll(IEnumerable<string?> holdingNames)
    {
        ArgumentNullException.ThrowIfNull(holdingNames);

        var alle = new List<string>();
        foreach (var name in holdingNames)
        {
            foreach (var nummer in Extract(name))
            {
                if (!alle.Contains(nummer, StringComparer.Ordinal))
                    alle.Add(nummer);
            }
        }

        return alle;
    }
}
```

- [ ] **Step 4: Test laufen lassen und Erfolg prüfen**

```bash
dotnet test tests/AuswertungPro.Next.Infrastructure.Tests/AuswertungPro.Next.Infrastructure.Tests.csproj --filter "FullyQualifiedName~ParcelNumberFromHoldingNameTests"
```

Erwartet: 10 Tests grün.

- [ ] **Step 5: Committen**

```bash
git add src/AuswertungPro.Next.Application/Dossiers/Lookup/ParcelNumberFromHoldingName.cs tests/AuswertungPro.Next.Infrastructure.Tests/Dossiers/Lookup/ParcelNumberFromHoldingNameTests.cs
git commit -m "feat(dossier): Parzellennummer aus dem Haltungsnamen lesen"
```

---

### Task 2: Ergebnistypen, Verträge und der Dossiername

Die Datensätze und Verträge, auf die alles Weitere aufbaut, plus die erste Regel, die sie
benutzt.

**Files:**
- Create: `src/AuswertungPro.Next.Application/Dossiers/Lookup/LookupResults.cs`
- Create: `src/AuswertungPro.Next.Application/Dossiers/Lookup/IParcelLookup.cs`
- Create: `src/AuswertungPro.Next.Application/Dossiers/Lookup/ILandRegistryLookup.cs`
- Create: `src/AuswertungPro.Next.Application/Dossiers/Lookup/ISewerNetworkLookup.cs`
- Create: `src/AuswertungPro.Next.Application/Dossiers/Lookup/DossierNameBuilder.cs`
- Test: `tests/AuswertungPro.Next.Infrastructure.Tests/Dossiers/Lookup/DossierNameBuilderTests.cs`

**Interfaces:**
- Consumes: nichts
- Produces: die unten stehenden Datensätze und Verträge **wörtlich**; alle späteren Aufgaben
  verwenden genau diese Namen und Signaturen.

- [ ] **Step 1: Den fehlschlagenden Test schreiben**

```csharp
using AuswertungPro.Next.Application.Dossiers.Lookup;

using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.Dossiers.Lookup;

public sealed class DossierNameBuilderTests
{
    [Fact]
    public void Nummer_und_Nachname_ergeben_den_Namen()
    {
        Assert.Equal(
            "Liegenschaft Nr. 439 Beispiel",
            DossierNameBuilder.Build("439", "Kurt Beispiel"));
    }

    [Fact]
    public void Mehrteilige_Namen_liefern_das_letzte_Wort()
    {
        Assert.Equal(
            "Liegenschaft Nr. 439 Muster",
            DossierNameBuilder.Build("439", "Martin Peter Muster"));
    }

    [Fact]
    public void Eine_Firma_wird_ganz_uebernommen_wenn_sie_ein_Wort_ist()
    {
        Assert.Equal(
            "Liegenschaft Nr. 12 Musterbau",
            DossierNameBuilder.Build("12", "Musterbau"));
    }

    [Fact]
    public void Ohne_Eigentuemer_bleibt_nur_die_Nummer()
    {
        Assert.Equal("Liegenschaft Nr. 439", DossierNameBuilder.Build("439", null));
        Assert.Equal("Liegenschaft Nr. 439", DossierNameBuilder.Build("439", "   "));
    }

    [Fact]
    public void Zeichen_die_in_keinen_Ordnernamen_gehoeren_werden_ersetzt()
    {
        // Der Name wird auch zum Ordnernamen — ein Schraegstrich waere dort fatal.
        Assert.Equal(
            "Liegenschaft Nr. 439 Muster-Beispiel",
            DossierNameBuilder.Build("439", "Muster/Beispiel"));
    }
}
```

- [ ] **Step 2: Test laufen lassen und Fehlschlag prüfen**

```bash
dotnet test tests/AuswertungPro.Next.Infrastructure.Tests/AuswertungPro.Next.Infrastructure.Tests.csproj --filter "FullyQualifiedName~DossierNameBuilderTests"
```

Erwartet: `DossierNameBuilder` unbekannt.

- [ ] **Step 3: Die Ergebnistypen schreiben**

`src/AuswertungPro.Next.Application/Dossiers/Lookup/LookupResults.cs`:

```csharp
using System.Collections.Generic;

namespace AuswertungPro.Next.Application.Dossiers.Lookup;

/// <summary>Eine Gemeinde mit ihrer BFS-Nummer.</summary>
public sealed record Municipality(int BfsNr, string Name);

/// <summary>
/// Eine Liegenschaft aus dem Parzellendienst. <paramref name="OutlineWkt"/> ist
/// der Umriss als WKT-Polygon in EPSG:2056 und wird fuer die raeumliche Suche
/// nach Leitungen gebraucht.
/// </summary>
public sealed record ParcelInfo(
    string Number,
    int BfsNr,
    string Municipality,
    int? AreaSqm,
    string Egrid,
    string OutlineWkt,
    string LandRegistryUrl);

/// <summary>
/// Ein Eigentuemer laut Grundbuchauskunft. <paramref name="Designation"/> ist
/// die Kennzeichnung bei Miteigentum ("Lit.A"), sonst leer.
/// </summary>
public sealed record LandRegistryOwner(
    string Designation,
    string Name,
    string AddressLine,
    string Share);

/// <summary>
/// Der Auszug einer Liegenschaft. <paramref name="NoOwnerRegistered"/> ist wahr,
/// wenn die Auskunft ausdruecklich "Keine" meldet — das gibt es wirklich und
/// darf nie als Name durchgehen.
/// </summary>
public sealed record LandRegistryEntry(
    string BuildingStreet,
    string BuildingHouseNumber,
    string PostalCode,
    string Town,
    IReadOnlyList<LandRegistryOwner> Owners,
    bool NoOwnerRegistered);

/// <summary>
/// Eine Haltung aus dem Abwassernetz des Kantons. <paramref name="Owner"/> ist
/// die Eigentuemerangabe des Dienstes, zum Beispiel "Privat".
/// </summary>
public sealed record NetworkHolding(
    string Designation,
    string Owner,
    double? LengthMeters,
    string GeometryWkt)
{
    /// <summary>Nur private Leitungen gehoeren in ein Eigentuemerdossier.</summary>
    public bool IsPrivate
        => Owner.Contains("Privat", System.StringComparison.OrdinalIgnoreCase);
}
```

- [ ] **Step 4: Die drei Verträge schreiben**

`IParcelLookup.cs`:

```csharp
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace AuswertungPro.Next.Application.Dossiers.Lookup;

/// <summary>
/// Liest Liegenschaften aus dem Parzellendienst. Kennt kein Dossier.
/// </summary>
public interface IParcelLookup
{
    /// <summary>Eine Parzelle ueber Gemeindenummer und Parzellennummer. Null, wenn es sie nicht gibt.</summary>
    Task<ParcelInfo?> FindAsync(int bfsNr, string parcelNumber, CancellationToken ct = default);

    /// <summary>Alle Parzellen, die von den uebergebenen WKT-Linien beruehrt werden.</summary>
    Task<IReadOnlyList<ParcelInfo>> FindTouchedAsync(
        IReadOnlyList<string> wktLines, CancellationToken ct = default);

    /// <summary>Die Gemeinden des Kantons mit ihrer BFS-Nummer.</summary>
    Task<IReadOnlyList<Municipality>> ListMunicipalitiesAsync(CancellationToken ct = default);
}
```

`ILandRegistryLookup.cs`:

```csharp
using System.Threading;
using System.Threading.Tasks;

namespace AuswertungPro.Next.Application.Dossiers.Lookup;

/// <summary>
/// Liest den oeffentlichen Grundbuchauszug einer Liegenschaft. Kennt kein Dossier.
/// </summary>
public interface ILandRegistryLookup
{
    /// <summary>Null, wenn die Auskunft nicht sicher gelesen werden konnte.</summary>
    Task<LandRegistryEntry?> ReadAsync(ParcelInfo parcel, CancellationToken ct = default);
}
```

`ISewerNetworkLookup.cs`:

```csharp
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace AuswertungPro.Next.Application.Dossiers.Lookup;

/// <summary>
/// Liest das Abwassernetz des Kantons. Kennt kein Dossier.
/// </summary>
public interface ISewerNetworkLookup
{
    /// <summary>Lage der genannten Haltungen. Nicht gefundene fehlen im Ergebnis.</summary>
    Task<IReadOnlyList<NetworkHolding>> FindByNamesAsync(
        IReadOnlyList<string> names, CancellationToken ct = default);

    /// <summary>Alle Haltungen, die auf der Parzelle liegen.</summary>
    Task<IReadOnlyList<NetworkHolding>> FindOnParcelAsync(
        ParcelInfo parcel, CancellationToken ct = default);
}
```

- [ ] **Step 5: Den Dossiernamen schreiben**

`DossierNameBuilder.cs`:

```csharp
using System;
using System.Linq;

namespace AuswertungPro.Next.Application.Dossiers.Lookup;

/// <summary>
/// Baut den Dossiernamen in der bisher von Hand verwendeten Schreibweise:
/// "Liegenschaft Nr. 439 Beispiel". Der Name wird auch zum Ordnernamen, deshalb
/// werden Zeichen ersetzt, die in keinen Ordnernamen gehoeren.
/// </summary>
public static class DossierNameBuilder
{
    private static readonly char[] VerboteneZeichen =
        { '/', '\\', ':', '*', '?', '"', '<', '>', '|' };

    public static string Build(string parcelNumber, string? ownerName)
    {
        var nummer = (parcelNumber ?? string.Empty).Trim();
        var basis = $"Liegenschaft Nr. {nummer}";

        if (string.IsNullOrWhiteSpace(ownerName))
            return Saeubern(basis);

        // Der letzte Wortteil ist der Nachname; eine einteilige Firma bleibt ganz.
        var teile = ownerName.Trim()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var kurz = teile.Length == 0 ? string.Empty : teile[^1];

        return Saeubern(kurz.Length == 0 ? basis : basis + " " + kurz);
    }

    private static string Saeubern(string wert)
    {
        var sauber = wert;
        foreach (var zeichen in VerboteneZeichen)
            sauber = sauber.Replace(zeichen, '-');

        return sauber.Trim();
    }
}
```

- [ ] **Step 6: Test laufen lassen und Erfolg prüfen**

```bash
dotnet test tests/AuswertungPro.Next.Infrastructure.Tests/AuswertungPro.Next.Infrastructure.Tests.csproj --filter "FullyQualifiedName~DossierNameBuilderTests"
```

Erwartet: 5 Tests grün.

**Hinweis:** `Muster/Beispiel` hat kein Leerzeichen, also ist der ganze Ausdruck der „Nachname"
— nach dem Ersetzen wird daraus `Muster-Beispiel`. Genau das prüft der letzte Test.

- [ ] **Step 7: Committen**

```bash
git add src/AuswertungPro.Next.Application/Dossiers/Lookup tests/AuswertungPro.Next.Infrastructure.Tests/Dossiers/Lookup/DossierNameBuilderTests.cs
git commit -m "feat(dossier): Vertraege, Ergebnistypen und Dossiername fuer die Auskunft"
```

---

### Task 3: Grundbuchauskunft lesen (purer Parser)

Der heikelste Teil: eine fremde HTML-Seite. Der Parser ist rein und wird gegen erfundene
Beispieldateien geprüft — **ohne Internet**.

**Files:**
- Create: `src/AuswertungPro.Next.Infrastructure/Dossiers/Lookup/LandRegistryHtmlParser.cs`
- Create: `tests/Fixtures/DossierLookup/grundbuch_einzeleigentuemer.html`
- Create: `tests/Fixtures/DossierLookup/grundbuch_miteigentum.html`
- Create: `tests/Fixtures/DossierLookup/grundbuch_ohne_eigentuemer.html`
- Test: `tests/AuswertungPro.Next.Infrastructure.Tests/Dossiers/Lookup/LandRegistryHtmlParserTests.cs`

**Interfaces:**
- Consumes: `LandRegistryEntry`, `LandRegistryOwner` aus Task 2
- Produces: `public static class LandRegistryHtmlParser` mit
  `public static LandRegistryEntry? Parse(string? html)`

- [ ] **Step 1: Die drei Beispieldateien anlegen**

Alle drei bilden den Aufbau der echten Seite nach, mit **erfundenen** Namen.

`tests/Fixtures/DossierLookup/grundbuch_einzeleigentuemer.html`:

```html
<html><head><title>Grundbuch</title></head><body>
<table>
<tr><td class="t">Grundbuch Musterdorf</td></tr>
<tr><td class="t">Liegenschaft Nr. 170</td></tr>
<tr><td class="t">(Hauptbuchblatt 631), Plan Nr. 40, Musterfeld</td></tr>
<tr><td class="t">294 m&#178;</td><td class="t">Geb&#228;ude, Musterweg 3 (126 m&#178;)</td></tr>
<tr><td class="t">Gartenanlage (140 m&#178;)</td></tr>
<tr><td class="t">Eigent&#252;mer</td></tr>
<tr><td class="t">Martin Muster</td></tr>
<tr><td class="t">Musterweg 3, 6472 Musterdorf</td></tr>
<tr><td class="t">Anmerkungen(nur &#246;ffentlich einsehbare)</td></tr>
<tr><td class="t">laut Grundbuch</td></tr>
</table>
</body></html>
```

`tests/Fixtures/DossierLookup/grundbuch_miteigentum.html`:

```html
<html><head><title>Grundbuch</title></head><body>
<table>
<tr><td class="t">Grundbuch Musterdorf</td></tr>
<tr><td class="t">Liegenschaft Nr. 439</td></tr>
<tr><td class="t">(Hauptbuchblatt 739), Plan Nr. 11, Musterfeld</td></tr>
<tr><td class="t">1'139 m&#178;</td><td class="t">Geb&#228;ude, Musterstrasse 30 (148 m&#178;)</td></tr>
<tr><td class="t">Eigent&#252;mer</td></tr>
<tr><td class="t">Lit.A:</td></tr>
<tr><td class="t">Kurt Beispiel</td></tr>
<tr><td class="t">Musterstrasse 30, 6472 Musterdorf</td></tr>
<tr><td class="t">1/2 Miteigentum</td></tr>
<tr><td class="t">Lit.B:</td></tr>
<tr><td class="t">Rita Beispiel</td></tr>
<tr><td class="t">Musterstrasse 30, 6472 Musterdorf</td></tr>
<tr><td class="t">1/2 Miteigentum</td></tr>
<tr><td class="t">Anmerkungen(nur &#246;ffentlich einsehbare)</td></tr>
</table>
</body></html>
```

`tests/Fixtures/DossierLookup/grundbuch_ohne_eigentuemer.html`:

```html
<html><head><title>Grundbuch</title></head><body>
<table>
<tr><td class="t">Grundbuch Musterdorf</td></tr>
<tr><td class="t">Liegenschaft Nr. 13</td></tr>
<tr><td class="t">Eigent&#252;mer</td></tr>
<tr><td class="t">Keine</td></tr>
<tr><td class="t">Anmerkungen(nur &#246;ffentlich einsehbare)</td></tr>
</table>
</body></html>
```

Die Dateien müssen im Testprojekt im Ausgabeverzeichnis landen. In
`tests/AuswertungPro.Next.Infrastructure.Tests/AuswertungPro.Next.Infrastructure.Tests.csproj`
im `<ItemGroup>` mit den `PackageReference`-Einträgen **davor** eine neue Gruppe ergänzen:

```xml
  <ItemGroup>
    <None Include="..\Fixtures\DossierLookup\**\*" Link="Fixtures\DossierLookup\%(RecursiveDir)%(Filename)%(Extension)">
      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    </None>
  </ItemGroup>
```

- [ ] **Step 2: Den fehlschlagenden Test schreiben**

```csharp
using System;
using System.IO;
using System.Linq;

using AuswertungPro.Next.Infrastructure.Dossiers.Lookup;

using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.Dossiers.Lookup;

public sealed class LandRegistryHtmlParserTests
{
    private static string Lade(string dateiname)
        => File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory, "Fixtures", "DossierLookup", dateiname));

    [Fact]
    public void Liest_einen_einzelnen_Eigentuemer_mit_Adresse()
    {
        var eintrag = LandRegistryHtmlParser.Parse(Lade("grundbuch_einzeleigentuemer.html"));

        Assert.NotNull(eintrag);
        Assert.False(eintrag!.NoOwnerRegistered);
        Assert.Equal("Musterweg", eintrag.BuildingStreet);
        Assert.Equal("3", eintrag.BuildingHouseNumber);
        Assert.Equal("6472", eintrag.PostalCode);
        Assert.Equal("Musterdorf", eintrag.Town);

        var eigentuemer = Assert.Single(eintrag.Owners);
        Assert.Equal("Martin Muster", eigentuemer.Name);
        Assert.Equal("Musterweg 3, 6472 Musterdorf", eigentuemer.AddressLine);
        Assert.Equal("", eigentuemer.Designation);
    }

    [Fact]
    public void Liest_beide_Miteigentuemer_mit_ihrer_Kennzeichnung()
    {
        var eintrag = LandRegistryHtmlParser.Parse(Lade("grundbuch_miteigentum.html"));

        Assert.NotNull(eintrag);
        Assert.Equal(2, eintrag!.Owners.Count);

        Assert.Equal("Lit.A", eintrag.Owners[0].Designation);
        Assert.Equal("Kurt Beispiel", eintrag.Owners[0].Name);
        Assert.Equal("1/2 Miteigentum", eintrag.Owners[0].Share);

        Assert.Equal("Lit.B", eintrag.Owners[1].Designation);
        Assert.Equal("Rita Beispiel", eintrag.Owners[1].Name);

        Assert.Equal("Musterstrasse", eintrag.BuildingStreet);
        Assert.Equal("30", eintrag.BuildingHouseNumber);
    }

    [Fact]
    public void Keine_wird_nie_zu_einem_Namen()
    {
        var eintrag = LandRegistryHtmlParser.Parse(Lade("grundbuch_ohne_eigentuemer.html"));

        Assert.NotNull(eintrag);
        Assert.True(eintrag!.NoOwnerRegistered);
        Assert.Empty(eintrag.Owners);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("<html><body>Seite nicht gefunden</body></html>")]
    public void Was_nicht_sicher_gelesen_werden_kann_ergibt_null(string? html)
    {
        // Lieber nichts als ein geratener Name.
        Assert.Null(LandRegistryHtmlParser.Parse(html));
    }
}
```

- [ ] **Step 3: Test laufen lassen und Fehlschlag prüfen**

```bash
dotnet test tests/AuswertungPro.Next.Infrastructure.Tests/AuswertungPro.Next.Infrastructure.Tests.csproj --filter "FullyQualifiedName~LandRegistryHtmlParserTests"
```

Erwartet: `LandRegistryHtmlParser` unbekannt.

- [ ] **Step 4: Den Parser schreiben**

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;

using AuswertungPro.Next.Application.Dossiers.Lookup;

namespace AuswertungPro.Next.Infrastructure.Dossiers.Lookup;

/// <summary>
/// Liest den oeffentlichen Grundbuchauszug des Kantons Uri.
///
/// Die Quelle ist eine Webseite, keine Schnittstelle. Der Aufbau kann sich
/// jederzeit aendern. Deshalb gilt durchgehend: was nicht sicher erkannt wird,
/// ergibt null oder bleibt leer — nie ein geratener Wert. Ein falscher Name in
/// einem Brief an den Eigentuemer waere schlimmer als eine leere Stelle.
///
/// Aufbau der Seite, an dem sich der Parser orientiert:
///   Grundbuch &lt;Gemeinde&gt;
///   Liegenschaft Nr. &lt;Nummer&gt;
///   ... Gebaeude, &lt;Strasse&gt; &lt;Haus-Nr.&gt; (&lt;Flaeche&gt;)
///   Eigentuemer
///   [Lit.A:]  &lt;Name&gt;  &lt;Adresse&gt;  [&lt;Anteil&gt;]
///   Anmerkungen...
/// </summary>
public static class LandRegistryHtmlParser
{
    private static readonly Regex GebaeudeZeile = new(
        @"Gebäude,\s*(?<strasse>[^,(]+?)\s+(?<nr>\d+[a-zA-Z]?)\s*\(",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex PlzOrt = new(
        @"\b(?<plz>\d{4})\s+(?<ort>[^,]+)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex LitZeile = new(
        @"^Lit\.\s*([A-Z])\s*:$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex AnteilZeile = new(
        @"^\d+/\d+\s", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static LandRegistryEntry? Parse(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return null;

        var zeilen = ZeilenAusHtml(html);

        var eigentuemerIndex = zeilen.FindIndex(
            z => z.StartsWith("Eigentümer", StringComparison.OrdinalIgnoreCase));
        if (eigentuemerIndex < 0)
            return null;

        var ende = zeilen.FindIndex(
            eigentuemerIndex + 1,
            z => z.StartsWith("Anmerkungen", StringComparison.OrdinalIgnoreCase));
        if (ende < 0)
            ende = zeilen.Count;

        var block = zeilen.GetRange(eigentuemerIndex + 1, ende - eigentuemerIndex - 1);

        var ohneEigentuemer = block.Count == 1
            && string.Equals(block[0], "Keine", StringComparison.OrdinalIgnoreCase);

        var eigentuemer = ohneEigentuemer
            ? new List<LandRegistryOwner>()
            : LiesEigentuemer(block);

        var (strasse, hausNr) = LiesGebaeudeadresse(zeilen);
        var (plz, ort) = LiesPlzOrt(eigentuemer, zeilen);

        return new LandRegistryEntry(strasse, hausNr, plz, ort, eigentuemer, ohneEigentuemer);
    }

    /// <summary>
    /// Wandelt das HTML in Textzeilen. Bewusst ohne HTML-Bibliothek: die Seite
    /// besteht aus Tabellenzellen, deren Text zeilenweise gelesen werden kann.
    /// </summary>
    private static List<string> ZeilenAusHtml(string html)
    {
        var ohneSkript = Regex.Replace(
            html, "<script.*?</script>|<style.*?</style>", " ",
            RegexOptions.Singleline | RegexOptions.IgnoreCase);

        var text = Regex.Replace(ohneSkript, "<[^>]+>", "\n");
        text = WebUtility.HtmlDecode(text);

        return text
            .Split('\n')
            .Select(z => Regex.Replace(z, @"[\s ]+", " ").Trim())
            .Where(z => z.Length > 0)
            .ToList();
    }

    private static List<LandRegistryOwner> LiesEigentuemer(List<string> block)
    {
        var ergebnis = new List<LandRegistryOwner>();

        var kennzeichnung = string.Empty;
        string? name = null;
        var adresse = string.Empty;
        var anteil = string.Empty;

        void Abschliessen()
        {
            if (!string.IsNullOrWhiteSpace(name))
                ergebnis.Add(new LandRegistryOwner(kennzeichnung, name!, adresse, anteil));

            name = null;
            adresse = string.Empty;
            anteil = string.Empty;
        }

        foreach (var zeile in block)
        {
            var lit = LitZeile.Match(zeile);
            if (lit.Success)
            {
                Abschliessen();
                kennzeichnung = "Lit." + lit.Groups[1].Value;
                continue;
            }

            if (AnteilZeile.IsMatch(zeile))
            {
                anteil = zeile;
                Abschliessen();
                continue;
            }

            if (name is null)
            {
                name = zeile;
                continue;
            }

            if (adresse.Length == 0)
            {
                adresse = zeile;
                continue;
            }

            // Eine dritte Zeile ohne Anteil beginnt einen neuen Eigentuemer.
            Abschliessen();
            name = zeile;
        }

        Abschliessen();
        return ergebnis;
    }

    private static (string Strasse, string HausNr) LiesGebaeudeadresse(List<string> zeilen)
    {
        foreach (var zeile in zeilen)
        {
            var treffer = GebaeudeZeile.Match(zeile);
            if (treffer.Success)
            {
                return (treffer.Groups["strasse"].Value.Trim(),
                        treffer.Groups["nr"].Value.Trim());
            }
        }

        return (string.Empty, string.Empty);
    }

    /// <summary>
    /// PLZ und Ort der Liegenschaft. Sie stehen nur in den Eigentuemeradressen —
    /// und der Eigentuemer kann auswaerts wohnen. Deshalb zaehlt nur eine
    /// Adresse, deren Ort auch im Kopf ("Grundbuch &lt;Gemeinde&gt;") steht.
    /// </summary>
    private static (string Plz, string Ort) LiesPlzOrt(
        List<LandRegistryOwner> eigentuemer, List<string> zeilen)
    {
        var kopf = zeilen.FirstOrDefault(
            z => z.StartsWith("Grundbuch ", StringComparison.OrdinalIgnoreCase));
        var gemeinde = kopf is null ? string.Empty : kopf["Grundbuch ".Length..].Trim();

        foreach (var besitzer in eigentuemer)
        {
            var treffer = PlzOrt.Match(besitzer.AddressLine);
            if (!treffer.Success)
                continue;

            var ort = treffer.Groups["ort"].Value.Trim();
            if (gemeinde.Length > 0
                && !ort.Equals(gemeinde, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return (treffer.Groups["plz"].Value, ort);
        }

        return (string.Empty, gemeinde);
    }
}
```

- [ ] **Step 5: Test laufen lassen und Erfolg prüfen**

```bash
dotnet test tests/AuswertungPro.Next.Infrastructure.Tests/AuswertungPro.Next.Infrastructure.Tests.csproj --filter "FullyQualifiedName~LandRegistryHtmlParserTests"
```

Erwartet: 6 Tests grün.

- [ ] **Step 6: Committen**

```bash
git add src/AuswertungPro.Next.Infrastructure/Dossiers/Lookup/LandRegistryHtmlParser.cs tests/Fixtures/DossierLookup tests/AuswertungPro.Next.Infrastructure.Tests/Dossiers/Lookup/LandRegistryHtmlParserTests.cs tests/AuswertungPro.Next.Infrastructure.Tests/AuswertungPro.Next.Infrastructure.Tests.csproj
git commit -m "feat(dossier): Grundbuchauskunft lesen"
```

---

### Task 4: Die beiden WFS-Antworten lesen (pure Parser)

**Files:**
- Create: `src/AuswertungPro.Next.Infrastructure/Dossiers/Lookup/ParcelWfsXmlParser.cs`
- Create: `src/AuswertungPro.Next.Infrastructure/Dossiers/Lookup/SewerNetworkWfsXmlParser.cs`
- Create: `tests/Fixtures/DossierLookup/wfs_parzelle.xml`
- Create: `tests/Fixtures/DossierLookup/wfs_haltungen.xml`
- Test: `tests/AuswertungPro.Next.Infrastructure.Tests/Dossiers/Lookup/WfsXmlParserTests.cs`

**Interfaces:**
- Consumes: `ParcelInfo`, `NetworkHolding`, `Municipality` aus Task 2
- Produces:
  - `public static class ParcelWfsXmlParser` mit `public static IReadOnlyList<ParcelInfo> Parse(string? xml)`
    und `public static IReadOnlyList<Municipality> ParseMunicipalities(string? xml)`
  - `public static class SewerNetworkWfsXmlParser` mit `public static IReadOnlyList<NetworkHolding> Parse(string? xml)`

- [ ] **Step 1: Die zwei Beispieldateien anlegen**

`tests/Fixtures/DossierLookup/wfs_parzelle.xml` — Aufbau wie die echte Antwort, gekürzt:

```xml
<?xml version="1.0" encoding="UTF-8"?>
<wfs:FeatureCollection xmlns:wfs="http://www.opengis.net/wfs/2.0"
                       xmlns:gml="http://www.opengis.net/gml/3.2"
                       xmlns:av="http://geo.ur.ch/av"
                       numberMatched="1" numberReturned="1">
  <wfs:member>
    <av:ch059_liegenschaften_flaechen gml:id="x.1">
      <av:grundstueckart>Liegenschaft</av:grundstueckart>
      <av:nummer>439</av:nummer>
      <av:egris_egrid>CH114627077847</av:egris_egrid>
      <av:flaechenmass>1139</av:flaechenmass>
      <av:url_grundbuch>https://geo.ur.ch/grundbuchauskunft?gem=1206&amp;nr=439</av:url_grundbuch>
      <av:bfsnr>1206</av:bfsnr>
      <av:gemeinde>Musterdorf</av:gemeinde>
      <av:wkb_geometry>
        <gml:MultiSurface srsName="urn:ogc:def:crs:EPSG::2056">
          <gml:surfaceMember>
            <gml:Polygon>
              <gml:exterior>
                <gml:LinearRing>
                  <gml:posList>2692400.5 1185800.25 2692430.5 1185800.25 2692430.5 1185830.75 2692400.5 1185830.75 2692400.5 1185800.25</gml:posList>
                </gml:LinearRing>
              </gml:exterior>
            </gml:Polygon>
          </gml:surfaceMember>
        </gml:MultiSurface>
      </av:wkb_geometry>
    </av:ch059_liegenschaften_flaechen>
  </wfs:member>
</wfs:FeatureCollection>
```

`tests/Fixtures/DossierLookup/wfs_haltungen.xml`:

```xml
<?xml version="1.0" encoding="UTF-8"?>
<wfs:FeatureCollection xmlns:wfs="http://www.opengis.net/wfs/2.0"
                       xmlns:gml="http://www.opengis.net/gml/3.2"
                       xmlns:leitungen="http://geo.ur.ch/leitungen"
                       numberMatched="2" numberReturned="2">
  <wfs:member>
    <leitungen:abw_haltungen gml:id="h.1">
      <leitungen:ne_bezeichnung>36051-36329</leitungen:ne_bezeichnung>
      <leitungen:ha_laengeeffektiv>11.46</leitungen:ha_laengeeffektiv>
      <leitungen:org_eigentuemer>Privat</leitungen:org_eigentuemer>
      <leitungen:wkb_geometry>
        <gml:MultiCurve srsName="urn:ogc:def:crs:EPSG::2056">
          <gml:curveMember>
            <gml:LineString>
              <gml:posList>2692462.471 1185860.503 2692458.291 1185862.403</gml:posList>
            </gml:LineString>
          </gml:curveMember>
        </gml:MultiCurve>
      </leitungen:wkb_geometry>
    </leitungen:abw_haltungen>
  </wfs:member>
  <wfs:member>
    <leitungen:abw_haltungen gml:id="h.2">
      <leitungen:ne_bezeichnung>36329-35558</leitungen:ne_bezeichnung>
      <leitungen:ha_laengeeffektiv>18.24</leitungen:ha_laengeeffektiv>
      <leitungen:org_eigentuemer>Abwasser Uri</leitungen:org_eigentuemer>
      <leitungen:wkb_geometry>
        <gml:MultiCurve srsName="urn:ogc:def:crs:EPSG::2056">
          <gml:curveMember>
            <gml:LineString>
              <gml:posList>2692470.0 1185870.0 2692480.0 1185875.0</gml:posList>
            </gml:LineString>
          </gml:curveMember>
        </gml:MultiCurve>
      </leitungen:wkb_geometry>
    </leitungen:abw_haltungen>
  </wfs:member>
</wfs:FeatureCollection>
```

- [ ] **Step 2: Den fehlschlagenden Test schreiben**

```csharp
using System;
using System.IO;
using System.Linq;

using AuswertungPro.Next.Infrastructure.Dossiers.Lookup;

using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.Dossiers.Lookup;

public sealed class WfsXmlParserTests
{
    private static string Lade(string dateiname)
        => File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory, "Fixtures", "DossierLookup", dateiname));

    [Fact]
    public void Liest_die_Parzelle_mit_Umriss()
    {
        var parzellen = ParcelWfsXmlParser.Parse(Lade("wfs_parzelle.xml"));

        var parzelle = Assert.Single(parzellen);
        Assert.Equal("439", parzelle.Number);
        Assert.Equal(1206, parzelle.BfsNr);
        Assert.Equal("Musterdorf", parzelle.Municipality);
        Assert.Equal(1139, parzelle.AreaSqm);
        Assert.Equal("CH114627077847", parzelle.Egrid);
        Assert.Contains("grundbuchauskunft", parzelle.LandRegistryUrl, StringComparison.Ordinal);

        // Der Umriss wird als WKT gebraucht, weil die raeumliche Suche ihn so erwartet.
        Assert.StartsWith("POLYGON((2692400.5 1185800.25,", parzelle.OutlineWkt, StringComparison.Ordinal);
        Assert.EndsWith("))", parzelle.OutlineWkt, StringComparison.Ordinal);
    }

    [Fact]
    public void Liest_die_Haltungen_mit_Eigentuemer_und_Linie()
    {
        var haltungen = SewerNetworkWfsXmlParser.Parse(Lade("wfs_haltungen.xml"));

        Assert.Equal(2, haltungen.Count);

        Assert.Equal("36051-36329", haltungen[0].Designation);
        Assert.Equal(11.46, haltungen[0].LengthMeters);
        Assert.True(haltungen[0].IsPrivate);
        Assert.Equal("LINESTRING(2692462.471 1185860.503,2692458.291 1185862.403)", haltungen[0].GeometryWkt);

        Assert.Equal("Abwasser Uri", haltungen[1].Owner);
        Assert.False(haltungen[1].IsPrivate);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("kein XML")]
    [InlineData("<html><body>Fehler</body></html>")]
    public void Unlesbares_ergibt_eine_leere_Liste_statt_eines_Absturzes(string? xml)
    {
        Assert.Empty(ParcelWfsXmlParser.Parse(xml));
        Assert.Empty(SewerNetworkWfsXmlParser.Parse(xml));
    }
}
```

- [ ] **Step 3: Test laufen lassen und Fehlschlag prüfen**

```bash
dotnet test tests/AuswertungPro.Next.Infrastructure.Tests/AuswertungPro.Next.Infrastructure.Tests.csproj --filter "FullyQualifiedName~WfsXmlParserTests"
```

Erwartet: die zwei Parser sind unbekannt.

- [ ] **Step 4: Den Parzellen-Parser schreiben**

```csharp
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;

using AuswertungPro.Next.Application.Dossiers.Lookup;

namespace AuswertungPro.Next.Infrastructure.Dossiers.Lookup;

/// <summary>
/// Liest die Antwort des Parzellendienstes. Rein: Text rein, Datensaetze raus.
/// Unlesbares ergibt eine leere Liste — der Aufrufer meldet dann "nicht gefunden",
/// statt mit geratenen Werten weiterzumachen.
/// </summary>
public static class ParcelWfsXmlParser
{
    public static IReadOnlyList<ParcelInfo> Parse(string? xml)
    {
        var wurzel = WfsGml.TryParse(xml);
        if (wurzel is null)
            return Array.Empty<ParcelInfo>();

        var ergebnis = new List<ParcelInfo>();

        foreach (var element in wurzel.Descendants()
                     .Where(e => e.Name.LocalName == "ch059_liegenschaften_flaechen"))
        {
            var nummer = WfsGml.Text(element, "nummer");
            if (nummer.Length == 0)
                continue;

            ergebnis.Add(new ParcelInfo(
                nummer,
                WfsGml.Int(element, "bfsnr") ?? 0,
                WfsGml.Text(element, "gemeinde"),
                WfsGml.Int(element, "flaechenmass"),
                WfsGml.Text(element, "egris_egrid"),
                WfsGml.PolygonWkt(element),
                WfsGml.Text(element, "url_grundbuch")));
        }

        return ergebnis;
    }

    /// <summary>Die Gemeindeliste kommt aus einer eigenen Ebene mit denselben Feldnamen.</summary>
    public static IReadOnlyList<Municipality> ParseMunicipalities(string? xml)
    {
        var wurzel = WfsGml.TryParse(xml);
        if (wurzel is null)
            return Array.Empty<Municipality>();

        var ergebnis = new List<Municipality>();

        foreach (var element in wurzel.Descendants()
                     .Where(e => e.Name.LocalName == "ch062_hoheitsgrenzen_gemeindegrenzen"))
        {
            var bfs = WfsGml.Int(element, "bfsnr");
            var name = WfsGml.Text(element, "gemeinde");
            if (bfs is null || name.Length == 0)
                continue;

            if (!ergebnis.Any(g => g.BfsNr == bfs.Value))
                ergebnis.Add(new Municipality(bfs.Value, name));
        }

        return ergebnis.OrderBy(g => g.Name, StringComparer.CurrentCulture).ToList();
    }
}
```

- [ ] **Step 5: Den Haltungs-Parser und die gemeinsamen Helfer schreiben**

`SewerNetworkWfsXmlParser.cs` — enthält am Ende auch die interne Helferklasse `WfsGml`,
weil beide Parser dieselben drei Handgriffe brauchen und eine eigene Datei dafür zu wenig wäre:

```csharp
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;

using AuswertungPro.Next.Application.Dossiers.Lookup;

namespace AuswertungPro.Next.Infrastructure.Dossiers.Lookup;

/// <summary>
/// Liest die Antwort der Abwasser-Netzebene. Rein: Text rein, Datensaetze raus.
/// </summary>
public static class SewerNetworkWfsXmlParser
{
    public static IReadOnlyList<NetworkHolding> Parse(string? xml)
    {
        var wurzel = WfsGml.TryParse(xml);
        if (wurzel is null)
            return Array.Empty<NetworkHolding>();

        var ergebnis = new List<NetworkHolding>();

        foreach (var element in wurzel.Descendants()
                     .Where(e => e.Name.LocalName == "abw_haltungen"))
        {
            var bezeichnung = WfsGml.Text(element, "ne_bezeichnung");
            if (bezeichnung.Length == 0)
                continue;

            ergebnis.Add(new NetworkHolding(
                bezeichnung,
                WfsGml.Text(element, "org_eigentuemer"),
                WfsGml.Double(element, "ha_laengeeffektiv"),
                WfsGml.LineStringWkt(element)));
        }

        return ergebnis;
    }
}

/// <summary>
/// Die drei Handgriffe, die beide WFS-Parser brauchen. Bewusst intern und klein:
/// Feldnamen werden ohne Namensraum gesucht, weil der Dienst seine Praefixe
/// aendern kann, ohne dass sich die Feldnamen aendern.
/// </summary>
internal static class WfsGml
{
    public static XElement? TryParse(string? xml)
    {
        if (string.IsNullOrWhiteSpace(xml))
            return null;

        try
        {
            return XDocument.Parse(xml).Root;
        }
        catch (System.Xml.XmlException)
        {
            // Der Dienst antwortet im Fehlerfall auch mal mit HTML.
            return null;
        }
    }

    public static string Text(XElement element, string feldname)
        => element.Descendants()
               .FirstOrDefault(e => e.Name.LocalName == feldname)?.Value.Trim()
           ?? string.Empty;

    public static int? Int(XElement element, string feldname)
        => int.TryParse(Text(element, feldname), NumberStyles.Integer,
            CultureInfo.InvariantCulture, out var wert)
            ? wert
            : null;

    public static double? Double(XElement element, string feldname)
        => double.TryParse(Text(element, feldname), NumberStyles.Float,
            CultureInfo.InvariantCulture, out var wert)
            ? wert
            : null;

    public static string PolygonWkt(XElement element)
    {
        var punkte = Punkte(element);
        return punkte.Count == 0 ? string.Empty : "POLYGON((" + string.Join(",", punkte) + "))";
    }

    public static string LineStringWkt(XElement element)
    {
        var punkte = Punkte(element);
        return punkte.Count == 0 ? string.Empty : "LINESTRING(" + string.Join(",", punkte) + ")";
    }

    /// <summary>
    /// GML gibt die Koordinaten als flache Zahlenfolge "x y x y ...". Eine
    /// ungerade Anzahl waere unvollstaendig und ergibt gar nichts.
    /// </summary>
    private static List<string> Punkte(XElement element)
    {
        var posList = element.Descendants()
            .FirstOrDefault(e => e.Name.LocalName == "posList")?.Value;

        if (string.IsNullOrWhiteSpace(posList))
            return new List<string>();

        var zahlen = posList.Split(
            new[] { ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries);

        if (zahlen.Length < 4 || zahlen.Length % 2 != 0)
            return new List<string>();

        var punkte = new List<string>(zahlen.Length / 2);
        for (var i = 0; i < zahlen.Length; i += 2)
            punkte.Add(zahlen[i] + " " + zahlen[i + 1]);

        return punkte;
    }
}
```

- [ ] **Step 6: Test laufen lassen und Erfolg prüfen**

```bash
dotnet test tests/AuswertungPro.Next.Infrastructure.Tests/AuswertungPro.Next.Infrastructure.Tests.csproj --filter "FullyQualifiedName~WfsXmlParserTests"
```

Erwartet: 6 Tests grün.

- [ ] **Step 7: Committen**

```bash
git add src/AuswertungPro.Next.Infrastructure/Dossiers/Lookup tests/Fixtures/DossierLookup tests/AuswertungPro.Next.Infrastructure.Tests/Dossiers/Lookup/WfsXmlParserTests.cs
git commit -m "feat(dossier): WFS-Antworten fuer Parzellen und Haltungen lesen"
```

---

### Task 5: Die Vorschläge zusammenstellen

Der Kern: beide Wege zusammenführen, ohne Netz, vollständig prüfbar mit erfundenen Lesern.

**Files:**
- Create: `src/AuswertungPro.Next.Application/Dossiers/Lookup/DossierBatchProposal.cs`
- Create: `src/AuswertungPro.Next.Application/Dossiers/Lookup/DossierBatchProposalUseCase.cs`
- Test: `tests/AuswertungPro.Next.Infrastructure.Tests/Dossiers/Lookup/DossierBatchProposalUseCaseTests.cs`

**Interfaces:**
- Consumes: `IParcelLookup`, `ILandRegistryLookup`, `ISewerNetworkLookup`,
  `ParcelInfo`, `LandRegistryEntry`, `NetworkHolding`, `ParcelNumberFromHoldingName`,
  `DossierNameBuilder` aus den Tasks 1 und 2
- Produces:
  - `public sealed record ProposedHolding(string Designation, bool IsPrivate, bool InProject, bool Preselected, string Origin)`
  - `public sealed record DossierProposal(ParcelInfo Parcel, LandRegistryEntry? Registry, IReadOnlyList<ProposedHolding> Holdings, string SuggestedName, bool Selectable, string SkipReason)`
  - `public sealed record DossierBatchProposalResult(IReadOnlyList<DossierProposal> Proposals, IReadOnlyList<string> Warnings)`
  - `public sealed class DossierBatchProposalUseCase` mit Konstruktor
    `(IParcelLookup, ILandRegistryLookup, ISewerNetworkLookup)` und
    `Task<DossierBatchProposalResult> RunAsync(DossierBatchProposalRequest request, IProgress<string>? progress, CancellationToken ct)`
  - `public sealed record DossierBatchProposalRequest(int BfsNr, IReadOnlyList<string> ProjectHoldingNames, IReadOnlyList<string> ParcelNumbersWithExistingDossier)`

- [ ] **Step 1: Den fehlschlagenden Test schreiben**

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using AuswertungPro.Next.Application.Dossiers.Lookup;

using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.Dossiers.Lookup;

public sealed class DossierBatchProposalUseCaseTests
{
    // Die gemessene Lage auf Parzelle 439: sechs Haltungen, davon eine dem
    // Kanton und eine private, die das Projekt nicht kennt.
    private static readonly NetworkHolding[] AufParzelle439 =
    {
        new("33429-7.26990", "Privat", 10.96, "LINESTRING(1 1,2 2)"),
        new("36329-35558", "Abwasser Uri", 18.24, "LINESTRING(1 1,2 2)"),
        new("36275-35558", "Privat", 15.63, "LINESTRING(1 1,2 2)"),
        new("36051-36329", "Privat", 11.46, "LINESTRING(1 1,2 2)"),
        new("36052-36329", "Privat", 12.9, "LINESTRING(1 1,2 2)"),
        new("33458-36051", "Privat", 3.32, "LINESTRING(1 1,2 2)")
    };

    private static readonly string[] ImProjekt =
    {
        "36329-35558", "36275-35558", "36051-36329", "36052-36329", "33458-36051"
    };

    private static ParcelInfo Parzelle(string nummer)
        => new(nummer, 1206, "Musterdorf", 1139, "CH1", "POLYGON((0 0,1 0,1 1,0 0))",
            "https://example.invalid/gb");

    private static LandRegistryEntry Miteigentum()
        => new("Musterstrasse", "30", "6472", "Musterdorf", new[]
        {
            new LandRegistryOwner("Lit.A", "Kurt Beispiel", "Musterstrasse 30, 6472 Musterdorf", "1/2 Miteigentum"),
            new LandRegistryOwner("Lit.B", "Rita Beispiel", "Musterstrasse 30, 6472 Musterdorf", "1/2 Miteigentum")
        }, NoOwnerRegistered: false);

    [Fact]
    public async Task Waehlt_genau_die_vier_privaten_Leitungen_aus_dem_Projekt_vor()
    {
        var use = Baue(
            parzellen: new[] { Parzelle("439") },
            aufParzelle: AufParzelle439,
            registry: Miteigentum());

        var ergebnis = await use.RunAsync(
            new DossierBatchProposalRequest(1206, ImProjekt, Array.Empty<string>()),
            progress: null, ct: CancellationToken.None);

        var vorschlag = Assert.Single(ergebnis.Proposals);
        Assert.Equal(4, vorschlag.Holdings.Count(h => h.Preselected));

        Assert.All(
            vorschlag.Holdings.Where(h => h.Preselected),
            h => Assert.True(h.IsPrivate && h.InProject));

        // Die Leitung des Kantons und die projektfremde erscheinen, aber unangehakt.
        Assert.Contains(vorschlag.Holdings, h => h.Designation == "36329-35558" && !h.Preselected);
        Assert.Contains(vorschlag.Holdings, h => h.Designation == "33429-7.26990" && !h.Preselected);
    }

    [Fact]
    public async Task Beide_Miteigentuemer_erscheinen_und_der_Name_kommt_vom_ersten()
    {
        var use = Baue(new[] { Parzelle("439") }, AufParzelle439, Miteigentum());

        var ergebnis = await use.RunAsync(
            new DossierBatchProposalRequest(1206, ImProjekt, Array.Empty<string>()),
            null, CancellationToken.None);

        var vorschlag = Assert.Single(ergebnis.Proposals);
        Assert.Equal(2, vorschlag.Registry!.Owners.Count);
        Assert.Equal("Liegenschaft Nr. 439 Beispiel", vorschlag.SuggestedName);
        Assert.True(vorschlag.Selectable);
    }

    [Fact]
    public async Task Ohne_eingetragenen_Eigentuemer_ist_der_Vorschlag_nicht_waehlbar()
    {
        var ohne = new LandRegistryEntry("", "", "", "Musterdorf",
            Array.Empty<LandRegistryOwner>(), NoOwnerRegistered: true);

        var use = Baue(new[] { Parzelle("13") }, Array.Empty<NetworkHolding>(), ohne);

        var ergebnis = await use.RunAsync(
            new DossierBatchProposalRequest(1206, ImProjekt, Array.Empty<string>()),
            null, CancellationToken.None);

        var vorschlag = Assert.Single(ergebnis.Proposals);
        Assert.False(vorschlag.Selectable);
        Assert.Contains("kein Eigent", vorschlag.SkipReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Eine_Parzelle_mit_bestehendem_Dossier_wird_nicht_erneut_angeboten()
    {
        var use = Baue(new[] { Parzelle("439") }, AufParzelle439, Miteigentum());

        var ergebnis = await use.RunAsync(
            new DossierBatchProposalRequest(1206, ImProjekt, new[] { "439" }),
            null, CancellationToken.None);

        var vorschlag = Assert.Single(ergebnis.Proposals);
        Assert.False(vorschlag.Selectable);
        Assert.Contains("Dossier", vorschlag.SkipReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Eine_aus_dem_Namen_abgeleitete_Nummer_ohne_Bestaetigung_wird_verworfen()
    {
        // Der Parzellendienst kennt 439 nicht: dann darf sie nicht erscheinen.
        var use = Baue(
            parzellen: Array.Empty<ParcelInfo>(),
            aufParzelle: Array.Empty<NetworkHolding>(),
            registry: null);

        var ergebnis = await use.RunAsync(
            new DossierBatchProposalRequest(1206, new[] { "439.01-36051" }, Array.Empty<string>()),
            null, CancellationToken.None);

        Assert.Empty(ergebnis.Proposals);
    }

    [Fact]
    public async Task Ein_Abbruch_bricht_wirklich_ab()
    {
        using var quelle = new CancellationTokenSource();
        quelle.Cancel();

        var use = Baue(new[] { Parzelle("439") }, AufParzelle439, Miteigentum());

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => use.RunAsync(
                new DossierBatchProposalRequest(1206, ImProjekt, Array.Empty<string>()),
                null, quelle.Token));
    }

    [Fact]
    public async Task Ein_Dienstfehler_wird_als_Warnung_gemeldet_und_stoppt_den_Lauf_nicht()
    {
        var use = new DossierBatchProposalUseCase(
            new FakeParcels(new[] { Parzelle("439") }),
            new FehlerhafteRegistry(),
            new FakeNetwork(AufParzelle439));

        var ergebnis = await use.RunAsync(
            new DossierBatchProposalRequest(1206, ImProjekt, Array.Empty<string>()),
            null, CancellationToken.None);

        Assert.NotEmpty(ergebnis.Warnings);
        var vorschlag = Assert.Single(ergebnis.Proposals);
        Assert.False(vorschlag.Selectable);
    }

    private static DossierBatchProposalUseCase Baue(
        IReadOnlyList<ParcelInfo> parzellen,
        IReadOnlyList<NetworkHolding> aufParzelle,
        LandRegistryEntry? registry)
        => new(new FakeParcels(parzellen), new FakeRegistry(registry), new FakeNetwork(aufParzelle));

    private sealed class FakeParcels : IParcelLookup
    {
        private readonly IReadOnlyList<ParcelInfo> _parzellen;
        public FakeParcels(IReadOnlyList<ParcelInfo> parzellen) => _parzellen = parzellen;

        public Task<ParcelInfo?> FindAsync(int bfsNr, string parcelNumber, CancellationToken ct = default)
            => Task.FromResult(_parzellen.FirstOrDefault(p => p.Number == parcelNumber));

        public Task<IReadOnlyList<ParcelInfo>> FindTouchedAsync(
            IReadOnlyList<string> wktLines, CancellationToken ct = default)
            => Task.FromResult(_parzellen);

        public Task<IReadOnlyList<Municipality>> ListMunicipalitiesAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<Municipality>>(new[] { new Municipality(1206, "Musterdorf") });
    }

    private sealed class FakeRegistry : ILandRegistryLookup
    {
        private readonly LandRegistryEntry? _eintrag;
        public FakeRegistry(LandRegistryEntry? eintrag) => _eintrag = eintrag;

        public Task<LandRegistryEntry?> ReadAsync(ParcelInfo parcel, CancellationToken ct = default)
            => Task.FromResult(_eintrag);
    }

    private sealed class FehlerhafteRegistry : ILandRegistryLookup
    {
        public Task<LandRegistryEntry?> ReadAsync(ParcelInfo parcel, CancellationToken ct = default)
            => throw new InvalidOperationException("Dienst nicht erreichbar");
    }

    private sealed class FakeNetwork : ISewerNetworkLookup
    {
        private readonly IReadOnlyList<NetworkHolding> _aufParzelle;
        public FakeNetwork(IReadOnlyList<NetworkHolding> aufParzelle) => _aufParzelle = aufParzelle;

        public Task<IReadOnlyList<NetworkHolding>> FindByNamesAsync(
            IReadOnlyList<string> names, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<NetworkHolding>>(
                _aufParzelle.Where(h => names.Contains(h.Designation)).ToList());

        public Task<IReadOnlyList<NetworkHolding>> FindOnParcelAsync(
            ParcelInfo parcel, CancellationToken ct = default)
            => Task.FromResult(_aufParzelle);
    }
}
```

- [ ] **Step 2: Test laufen lassen und Fehlschlag prüfen**

```bash
dotnet test tests/AuswertungPro.Next.Infrastructure.Tests/AuswertungPro.Next.Infrastructure.Tests.csproj --filter "FullyQualifiedName~DossierBatchProposalUseCaseTests"
```

Erwartet: `DossierBatchProposalUseCase` unbekannt.

- [ ] **Step 3: Das Vorschlagsmodell schreiben**

`DossierBatchProposal.cs`:

```csharp
using System.Collections.Generic;

namespace AuswertungPro.Next.Application.Dossiers.Lookup;

/// <summary>
/// Eine vorgeschlagene Leitung. <paramref name="Origin"/> sagt, welcher Weg sie
/// gefunden hat — "Lage" oder "Name" — damit in der Liste sichtbar ist, worauf
/// der Vorschlag beruht.
/// </summary>
public sealed record ProposedHolding(
    string Designation,
    bool IsPrivate,
    bool InProject,
    bool Preselected,
    string Origin);

/// <summary>
/// Der Vorschlag fuer ein Dossier. <paramref name="Selectable"/> ist falsch,
/// wenn daraus kein Dossier entstehen darf; <paramref name="SkipReason"/> sagt
/// dann warum.
/// </summary>
public sealed record DossierProposal(
    ParcelInfo Parcel,
    LandRegistryEntry? Registry,
    IReadOnlyList<ProposedHolding> Holdings,
    string SuggestedName,
    bool Selectable,
    string SkipReason);

/// <summary>Das Ergebnis eines Durchlaufs samt sichtbarer Warnungen.</summary>
public sealed record DossierBatchProposalResult(
    IReadOnlyList<DossierProposal> Proposals,
    IReadOnlyList<string> Warnings);

/// <summary>Was der Durchlauf braucht. Reine Eingabe, kein Projektobjekt.</summary>
public sealed record DossierBatchProposalRequest(
    int BfsNr,
    IReadOnlyList<string> ProjectHoldingNames,
    IReadOnlyList<string> ParcelNumbersWithExistingDossier);
```

- [ ] **Step 4: Den Anwendungsfall schreiben**

`DossierBatchProposalUseCase.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AuswertungPro.Next.Application.Dossiers.Lookup;

/// <summary>
/// Stellt die Dossier-Vorschlaege eines Projekts zusammen.
///
/// Zwei Wege fuehren zu den Parzellen, und beide werden gebraucht:
///   Name  — Knoten der Form "&lt;Parzelle&gt;.&lt;lfd&gt;" nennen ihre Parzelle. Kostet
///           nichts und findet die privaten Hausanschluesse, die der Kanton in
///           seiner oeffentlichen Netzebene groesstenteils nicht fuehrt.
///   Lage  — die Linien der beim Kanton bekannten Haltungen gegen die
///           Parzellenumrisse. Findet zusaetzlich Parzellen ohne solche Knoten.
///
/// Diese Klasse rechnet nur. Sie kennt kein Dateisystem und kein HTTP; die drei
/// Leser sind Abhaengigkeiten und im Test erfunden.
/// </summary>
public sealed class DossierBatchProposalUseCase
{
    private readonly IParcelLookup _parcels;
    private readonly ILandRegistryLookup _registry;
    private readonly ISewerNetworkLookup _network;

    public DossierBatchProposalUseCase(
        IParcelLookup parcels,
        ILandRegistryLookup registry,
        ISewerNetworkLookup network)
    {
        _parcels = parcels ?? throw new ArgumentNullException(nameof(parcels));
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _network = network ?? throw new ArgumentNullException(nameof(network));
    }

    public async Task<DossierBatchProposalResult> RunAsync(
        DossierBatchProposalRequest request,
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ct.ThrowIfCancellationRequested();

        var warnungen = new List<string>();
        var imProjekt = new HashSet<string>(
            request.ProjectHoldingNames.Where(n => !string.IsNullOrWhiteSpace(n)),
            StringComparer.OrdinalIgnoreCase);

        var parzellen = await SammleParzellen(request, warnungen, progress, ct)
            .ConfigureAwait(false);

        var mitDossier = new HashSet<string>(
            request.ParcelNumbersWithExistingDossier, StringComparer.OrdinalIgnoreCase);

        var vorschlaege = new List<DossierProposal>();

        foreach (var parzelle in parzellen)
        {
            ct.ThrowIfCancellationRequested();
            progress?.Report($"Parzelle {parzelle.Number}: Eigentümer und Leitungen");

            var aufParzelle = await SicherLesen(
                () => _network.FindOnParcelAsync(parzelle, ct),
                $"Leitungen auf Parzelle {parzelle.Number}", warnungen).ConfigureAwait(false)
                ?? Array.Empty<NetworkHolding>();

            var eintrag = await SicherLesen(
                () => _registry.ReadAsync(parzelle, ct),
                $"Grundbuchauskunft zu Parzelle {parzelle.Number}", warnungen)
                .ConfigureAwait(false);

            var leitungen = BaueLeitungen(parzelle, aufParzelle, imProjekt, request);
            var (waehlbar, grund) = Beurteile(parzelle, eintrag, mitDossier);

            vorschlaege.Add(new DossierProposal(
                parzelle,
                eintrag,
                leitungen,
                DossierNameBuilder.Build(parzelle.Number, eintrag?.Owners.FirstOrDefault()?.Name),
                waehlbar,
                grund));
        }

        return new DossierBatchProposalResult(vorschlaege, warnungen);
    }

    /// <summary>
    /// Beide Wege, zusammengefuehrt und entdoppelt. Eine aus einem Namen
    /// abgeleitete Nummer zaehlt erst, wenn der Parzellendienst sie bestaetigt.
    /// </summary>
    private async Task<List<ParcelInfo>> SammleParzellen(
        DossierBatchProposalRequest request,
        List<string> warnungen,
        IProgress<string>? progress,
        CancellationToken ct)
    {
        var gefunden = new List<ParcelInfo>();

        progress?.Report("Lage der Haltungen beim Kanton abfragen");
        var haltungen = await SicherLesen(
            () => _network.FindByNamesAsync(request.ProjectHoldingNames, ct),
            "Lage der Haltungen", warnungen).ConfigureAwait(false)
            ?? Array.Empty<NetworkHolding>();

        var linien = haltungen
            .Select(h => h.GeometryWkt)
            .Where(w => !string.IsNullOrWhiteSpace(w))
            .ToList();

        if (linien.Count > 0)
        {
            progress?.Report("Parzellen unter den Leitungen suchen");
            var beruehrt = await SicherLesen(
                () => _parcels.FindTouchedAsync(linien, ct),
                "Parzellensuche", warnungen).ConfigureAwait(false)
                ?? Array.Empty<ParcelInfo>();

            gefunden.AddRange(beruehrt);
        }

        foreach (var nummer in ParcelNumberFromHoldingName.ExtractAll(request.ProjectHoldingNames))
        {
            ct.ThrowIfCancellationRequested();

            if (gefunden.Any(p => p.Number.Equals(nummer, StringComparison.OrdinalIgnoreCase)))
                continue;

            var bestaetigt = await SicherLesen(
                () => _parcels.FindAsync(request.BfsNr, nummer, ct),
                $"Parzelle {nummer}", warnungen).ConfigureAwait(false);

            // Nicht bestaetigt heisst: verwerfen, nicht zeigen.
            if (bestaetigt is not null)
                gefunden.Add(bestaetigt);
        }

        return gefunden
            .GroupBy(p => p.Number, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .OrderBy(p => p.Number.Length)
            .ThenBy(p => p.Number, StringComparer.Ordinal)
            .ToList();
    }

    private static IReadOnlyList<ProposedHolding> BaueLeitungen(
        ParcelInfo parzelle,
        IReadOnlyList<NetworkHolding> aufParzelle,
        HashSet<string> imProjekt,
        DossierBatchProposalRequest request)
    {
        var ergebnis = new List<ProposedHolding>();

        foreach (var haltung in aufParzelle)
        {
            var inProjekt = imProjekt.Contains(haltung.Designation);
            ergebnis.Add(new ProposedHolding(
                haltung.Designation,
                haltung.IsPrivate,
                inProjekt,
                Preselected: haltung.IsPrivate && inProjekt,
                Origin: "Lage"));
        }

        // Was der Kanton nicht fuehrt, verraet der Knotenname: diese Haltungen
        // sind Hausanschluesse der Parzelle und liegen im Projekt.
        foreach (var name in request.ProjectHoldingNames)
        {
            if (string.IsNullOrWhiteSpace(name))
                continue;

            if (!ParcelNumberFromHoldingName.Extract(name)
                    .Any(n => n.Equals(parzelle.Number, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            if (ergebnis.Any(h => h.Designation.Equals(name, StringComparison.OrdinalIgnoreCase)))
                continue;

            ergebnis.Add(new ProposedHolding(
                name, IsPrivate: true, InProject: true, Preselected: true, Origin: "Name"));
        }

        return ergebnis;
    }

    private static (bool Waehlbar, string Grund) Beurteile(
        ParcelInfo parzelle, LandRegistryEntry? eintrag, HashSet<string> mitDossier)
    {
        if (mitDossier.Contains(parzelle.Number))
            return (false, "Für diese Parzelle gibt es bereits ein Dossier.");

        if (eintrag is null)
            return (false, "Die Grundbuchauskunft konnte nicht gelesen werden.");

        if (eintrag.NoOwnerRegistered || eintrag.Owners.Count == 0)
            return (false, "Im Grundbuch ist kein Eigentümer eingetragen.");

        return (true, string.Empty);
    }

    /// <summary>
    /// Ein Dienstfehler bei einer Parzelle darf den ganzen Lauf nicht beenden.
    /// Ein Abbruch durch den Benutzer dagegen schon — der wird durchgereicht.
    /// </summary>
    private static async Task<T?> SicherLesen<T>(
        Func<Task<T?>> leser, string was, List<string> warnungen) where T : class
    {
        try
        {
            return await leser().ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            warnungen.Add($"{was}: {ex.Message}");
            return null;
        }
    }
}
```

- [ ] **Step 5: Test laufen lassen und Erfolg prüfen**

```bash
dotnet test tests/AuswertungPro.Next.Infrastructure.Tests/AuswertungPro.Next.Infrastructure.Tests.csproj --filter "FullyQualifiedName~DossierBatchProposalUseCaseTests"
```

Erwartet: 7 Tests grün.

- [ ] **Step 6: Committen**

```bash
git add src/AuswertungPro.Next.Application/Dossiers/Lookup tests/AuswertungPro.Next.Infrastructure.Tests/Dossiers/Lookup/DossierBatchProposalUseCaseTests.cs
git commit -m "feat(dossier): Vorschlaege fuer die Stapelanlage zusammenstellen"
```

---

### Task 6: Aus Vorschlägen Dossiers machen

**Files:**
- Create: `src/AuswertungPro.Next.Application/Dossiers/Lookup/DossierBatchCreationUseCase.cs`
- Modify: `src/AuswertungPro.Next.Domain/Models/Dossiers/DossierModels.cs`
- Test: `tests/AuswertungPro.Next.Infrastructure.Tests/Dossiers/Lookup/DossierBatchCreationUseCaseTests.cs`
- Test: `tests/AuswertungPro.Next.Infrastructure.Tests/Dossiers/DossierMigrationTests.cs` (ergänzen)

**Interfaces:**
- Consumes: `DossierProposal`, `ProposedHolding` aus Task 5; `DossierDefinition`, `DossierOwnerRow`
- Produces:
  - `DossierDefinition.Municipality` (`string`), `DossierDefinition.MunicipalityBfsNr` (`int?`)
  - `DossierDocument.CurrentSchemaVersion` = 3
  - `public sealed record DossierCreationSelection(DossierProposal Proposal, IReadOnlyList<string> SelectedHoldingDesignations)`
  - `public static class DossierBatchCreationUseCase` mit
    `public static IReadOnlyList<DossierDefinition> Build(IReadOnlyList<DossierCreationSelection> selections, IReadOnlyDictionary<string, Guid> holdingIdsByName)`

- [ ] **Step 1: Den fehlschlagenden Test schreiben**

```csharp
using System;
using System.Collections.Generic;
using System.Linq;

using AuswertungPro.Next.Application.Dossiers.Lookup;
using AuswertungPro.Next.Domain.Models.Dossiers;

using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.Dossiers.Lookup;

public sealed class DossierBatchCreationUseCaseTests
{
    private static DossierProposal Vorschlag()
    {
        var parzelle = new ParcelInfo("439", 1206, "Musterdorf", 1139, "CH1",
            "POLYGON((0 0,1 0,1 1,0 0))", "https://example.invalid/gb");

        var eintrag = new LandRegistryEntry("Musterstrasse", "30", "6472", "Musterdorf", new[]
        {
            new LandRegistryOwner("Lit.A", "Kurt Beispiel", "Musterstrasse 30, 6472 Musterdorf", "1/2 Miteigentum"),
            new LandRegistryOwner("Lit.B", "Rita Beispiel", "Musterstrasse 30, 6472 Musterdorf", "1/2 Miteigentum")
        }, NoOwnerRegistered: false);

        var leitungen = new[]
        {
            new ProposedHolding("36051-36329", true, true, true, "Lage"),
            new ProposedHolding("36329-35558", false, true, false, "Lage")
        };

        return new DossierProposal(parzelle, eintrag, leitungen,
            "Liegenschaft Nr. 439 Beispiel", Selectable: true, SkipReason: "");
    }

    [Fact]
    public void Erzeugt_ein_Dossier_mit_beiden_Eigentuemerzeilen()
    {
        var ids = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase)
        {
            ["36051-36329"] = Guid.NewGuid()
        };

        var dossiers = DossierBatchCreationUseCase.Build(
            new[] { new DossierCreationSelection(Vorschlag(), new[] { "36051-36329" }) }, ids);

        var dossier = Assert.Single(dossiers);
        Assert.Equal("Liegenschaft Nr. 439 Beispiel", dossier.Name);
        Assert.Equal("439", dossier.ParcelNumbers);
        Assert.Equal("Musterdorf", dossier.Municipality);
        Assert.Equal(1206, dossier.MunicipalityBfsNr);
        Assert.Equal("Musterstrasse", dossier.Address);
        Assert.Equal("30", dossier.HouseNumbers);
        Assert.Equal("6472", dossier.PostalCode);
        Assert.Equal("Musterdorf", dossier.Town);

        Assert.Equal(2, dossier.Owners.Count);
        Assert.Equal("Kurt Beispiel", dossier.Owners[0].Name);
        Assert.Equal("Rita Beispiel", dossier.Owners[1].Name);
        Assert.All(dossier.Owners, o => Assert.Equal("", o.Phone));

        Assert.Equal(new[] { ids["36051-36329"] }, dossier.HoldingIds);
    }

    [Fact]
    public void Nur_angehakte_Leitungen_kommen_hinein()
    {
        var ids = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase)
        {
            ["36051-36329"] = Guid.NewGuid(),
            ["36329-35558"] = Guid.NewGuid()
        };

        var dossiers = DossierBatchCreationUseCase.Build(
            new[] { new DossierCreationSelection(Vorschlag(), Array.Empty<string>()) }, ids);

        Assert.Empty(Assert.Single(dossiers).HoldingIds);
    }

    [Fact]
    public void Eine_Leitung_ohne_bekannte_Kennung_wird_uebersprungen()
    {
        var dossiers = DossierBatchCreationUseCase.Build(
            new[] { new DossierCreationSelection(Vorschlag(), new[] { "36051-36329" }) },
            new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase));

        Assert.Empty(Assert.Single(dossiers).HoldingIds);
    }

    [Fact]
    public void Ein_nicht_waehlbarer_Vorschlag_erzeugt_kein_Dossier()
    {
        var gesperrt = Vorschlag() with { Selectable = false, SkipReason = "kein Eigentümer" };

        var dossiers = DossierBatchCreationUseCase.Build(
            new[] { new DossierCreationSelection(gesperrt, new[] { "36051-36329" }) },
            new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase));

        Assert.Empty(dossiers);
    }
}
```

- [ ] **Step 2: Den Migrationstest für Version 3 ergänzen**

In `tests/AuswertungPro.Next.Infrastructure.Tests/Dossiers/DossierMigrationTests.cs` anhängen:

```csharp
    [Fact]
    public void Version_2_bleibt_bei_der_Erhoehung_auf_3_ohne_neue_Eigentuemerzeile()
    {
        // Die Falle aus der Pruefung: mit "kleiner als die aktuelle Version"
        // waere eine Version-2-Datei wieder Altbestand und die geloeschte Zeile
        // kaeme zurueck.
        var document = new DossierDocument { SchemaVersion = 2 };
        document.Dossiers.Add(new DossierDefinition { OwnerName = "Martin Muster" });

        var result = DossierDocumentMigration.MigrateToCurrent(document);

        Assert.Empty(result.Dossiers[0].Owners);
        Assert.Equal(3, result.SchemaVersion);
    }
```

- [ ] **Step 3: Tests laufen lassen und Fehlschlag prüfen**

```bash
dotnet test tests/AuswertungPro.Next.Infrastructure.Tests/AuswertungPro.Next.Infrastructure.Tests.csproj --filter "FullyQualifiedName~DossierBatchCreationUseCaseTests|FullyQualifiedName~DossierDocumentMigrationTests"
```

Erwartet: `DossierBatchCreationUseCase` unbekannt; der Versionstest erwartet 3, bekommt 2.

- [ ] **Step 4: Das Datenmodell erweitern**

In `src/AuswertungPro.Next.Domain/Models/Dossiers/DossierModels.cs`, in `DossierDefinition`
direkt nach `public string Town { get; set; } = "";` einfügen:

```csharp
    /// <summary>Politische Gemeinde. Nicht dasselbe wie der Ort der Adresse.</summary>
    public string Municipality { get; set; } = "";

    /// <summary>BFS-Nummer der Gemeinde. Ueber sie laeuft die Parzellensuche.</summary>
    public int? MunicipalityBfsNr { get; set; }
```

und in `DossierDocument` die Zeile

```csharp
    public const int CurrentSchemaVersion = 2;
```

ersetzen durch

```csharp
    public const int CurrentSchemaVersion = 3;
```

**Die Ableitungsgrenze in `DossierDocumentMigration` bleibt unangetastet** — sie hängt an
`OwnersStoredFromVersion = 2` und genau deshalb kippt sie hier nicht.

- [ ] **Step 5: Den Anwendungsfall schreiben**

`src/AuswertungPro.Next.Application/Dossiers/Lookup/DossierBatchCreationUseCase.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;

using AuswertungPro.Next.Domain.Models.Dossiers;

namespace AuswertungPro.Next.Application.Dossiers.Lookup;

/// <summary>Ein bestaetigter Vorschlag samt der angehakten Leitungen.</summary>
public sealed record DossierCreationSelection(
    DossierProposal Proposal,
    IReadOnlyList<string> SelectedHoldingDesignations);

/// <summary>
/// Macht aus bestaetigten Vorschlaegen Dossiers. Reine Umwandlung: kein
/// Dateizugriff, kein Netz. Der Aufrufer speichert.
/// </summary>
public static class DossierBatchCreationUseCase
{
    public static IReadOnlyList<DossierDefinition> Build(
        IReadOnlyList<DossierCreationSelection> selections,
        IReadOnlyDictionary<string, Guid> holdingIdsByName)
    {
        ArgumentNullException.ThrowIfNull(selections);
        ArgumentNullException.ThrowIfNull(holdingIdsByName);

        var ergebnis = new List<DossierDefinition>();

        foreach (var auswahl in selections)
        {
            var vorschlag = auswahl.Proposal;

            // Ein gesperrter Vorschlag darf nie ein Dossier werden.
            if (!vorschlag.Selectable || vorschlag.Registry is null)
                continue;

            var dossier = new DossierDefinition
            {
                Name = vorschlag.SuggestedName,
                ParcelNumbers = vorschlag.Parcel.Number,
                Municipality = vorschlag.Parcel.Municipality,
                MunicipalityBfsNr = vorschlag.Parcel.BfsNr,
                Address = vorschlag.Registry.BuildingStreet,
                HouseNumbers = vorschlag.Registry.BuildingHouseNumber,
                PostalCode = vorschlag.Registry.PostalCode,
                Town = vorschlag.Registry.Town
            };

            // Das Deckblatt speist sich weiterhin aus diesen Feldern.
            var erster = vorschlag.Registry.Owners.FirstOrDefault();
            if (erster is not null)
            {
                dossier.OwnerName = erster.Name;
                dossier.OwnerAddress = erster.AddressLine;
            }

            foreach (var eigentuemer in vorschlag.Registry.Owners)
            {
                dossier.Owners.Add(new DossierOwnerRow
                {
                    HouseNumber = vorschlag.Registry.BuildingHouseNumber,
                    ParcelNumber = vorschlag.Parcel.Number,
                    Name = eigentuemer.Name,
                    // Telefonnummern werden bewusst nicht ermittelt.
                    Phone = "",
                    Mail = "",
                    Occupancy = ""
                });
            }

            foreach (var bezeichnung in auswahl.SelectedHoldingDesignations)
            {
                if (holdingIdsByName.TryGetValue(bezeichnung, out var id)
                    && !dossier.HoldingIds.Contains(id))
                {
                    dossier.HoldingIds.Add(id);
                }
            }

            ergebnis.Add(dossier);
        }

        return ergebnis;
    }
}
```

- [ ] **Step 6: Tests laufen lassen und Erfolg prüfen**

```bash
dotnet test tests/AuswertungPro.Next.Infrastructure.Tests/AuswertungPro.Next.Infrastructure.Tests.csproj --filter "FullyQualifiedName~Dossier"
```

Erwartet: alle Dossier-Tests grün, einschliesslich der fünf neuen.

- [ ] **Step 7: Committen**

```bash
git add src/AuswertungPro.Next.Application/Dossiers/Lookup/DossierBatchCreationUseCase.cs src/AuswertungPro.Next.Domain/Models/Dossiers/DossierModels.cs tests/AuswertungPro.Next.Infrastructure.Tests/Dossiers
git commit -m "feat(dossier): aus bestaetigten Vorschlaegen Dossiers bauen, Formatversion 3"
```

---

### Task 7: Die drei Leser ans Netz hängen

Dünne Hüllen: HTTP holen, an den jeweiligen Parser geben. Keine Regel.

**Files:**
- Create: `src/AuswertungPro.Next.Infrastructure/Dossiers/Lookup/GeoUrHttpGateway.cs`
- Create: `src/AuswertungPro.Next.Infrastructure/Dossiers/Lookup/UriParcelWfsClient.cs`
- Create: `src/AuswertungPro.Next.Infrastructure/Dossiers/Lookup/UriLandRegistryClient.cs`
- Create: `src/AuswertungPro.Next.Infrastructure/Dossiers/Lookup/UriSewerNetworkWfsClient.cs`
- Test: `tests/AuswertungPro.Next.Infrastructure.Tests/Dossiers/Lookup/UriLookupClientTests.cs`

**Interfaces:**
- Consumes: die drei Verträge aus Task 2, die drei Parser aus Task 3 und 4
- Produces: `GeoUrHttpGateway` mit
  `public GeoUrHttpGateway(HttpMessageHandler? handler = null, TimeSpan? timeout = null)`,
  `public Task<string?> GetStringAsync(Uri uri, CancellationToken ct)`,
  `public Task<string?> PostFormAsync(Uri uri, IReadOnlyDictionary<string,string> form, CancellationToken ct)`;
  die drei Client-Klassen mit Konstruktor `(GeoUrHttpGateway gateway)`

- [ ] **Step 1: Den fehlschlagenden Test schreiben**

Der Test hängt einen erfundenen HTTP-Beantworter ein — **kein echter Netzzugriff**.

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

using AuswertungPro.Next.Application.Dossiers.Lookup;
using AuswertungPro.Next.Infrastructure.Dossiers.Lookup;

using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.Dossiers.Lookup;

public sealed class UriLookupClientTests
{
    private static string Lade(string dateiname)
        => File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory, "Fixtures", "DossierLookup", dateiname));

    [Fact]
    public async Task Parzellensuche_gibt_die_gelesene_Parzelle_zurueck()
    {
        var handler = new FesteAntwort(Lade("wfs_parzelle.xml"));
        var client = new UriParcelWfsClient(new GeoUrHttpGateway(handler));

        var parzelle = await client.FindAsync(1206, "439");

        Assert.NotNull(parzelle);
        Assert.Equal("439", parzelle!.Number);
        Assert.Contains("nummer", handler.LetzteAnfrage, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("1206", handler.LetzteAnfrage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Haltungssuche_liest_die_Antwort()
    {
        var handler = new FesteAntwort(Lade("wfs_haltungen.xml"));
        var client = new UriSewerNetworkWfsClient(new GeoUrHttpGateway(handler));

        var haltungen = await client.FindByNamesAsync(new[] { "36051-36329" });

        Assert.Equal(2, haltungen.Count);
    }

    [Fact]
    public async Task Grundbuchauskunft_liest_die_Seite()
    {
        var handler = new FesteAntwort(Lade("grundbuch_miteigentum.html"));
        var client = new UriLandRegistryClient(new GeoUrHttpGateway(handler));

        var parzelle = new ParcelInfo("439", 1206, "Musterdorf", 1139, "CH1", "POLYGON((0 0,1 0,1 1,0 0))",
            "https://geo.ur.ch/grundbuchauskunft?gem=1206&nr=439");

        var eintrag = await client.ReadAsync(parzelle);

        Assert.NotNull(eintrag);
        Assert.Equal(2, eintrag!.Owners.Count);
    }

    [Fact]
    public async Task Ein_Serverfehler_ergibt_null_statt_einer_Ausnahme()
    {
        var handler = new FesteAntwort("", HttpStatusCode.InternalServerError);
        var client = new UriParcelWfsClient(new GeoUrHttpGateway(handler));

        Assert.Null(await client.FindAsync(1206, "439"));
    }

    [Fact]
    public async Task Ohne_Adresse_der_Grundbuchauskunft_wird_nicht_geraten()
    {
        var handler = new FesteAntwort(Lade("grundbuch_miteigentum.html"));
        var client = new UriLandRegistryClient(new GeoUrHttpGateway(handler));

        var ohneUrl = new ParcelInfo("439", 1206, "Musterdorf", 1139, "CH1",
            "POLYGON((0 0,1 0,1 1,0 0))", "");

        Assert.Null(await client.ReadAsync(ohneUrl));
    }

    private sealed class FesteAntwort : HttpMessageHandler
    {
        private readonly string _inhalt;
        private readonly HttpStatusCode _status;

        public FesteAntwort(string inhalt, HttpStatusCode status = HttpStatusCode.OK)
        {
            _inhalt = inhalt;
            _status = status;
        }

        public string LetzteAnfrage { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LetzteAnfrage = request.RequestUri?.ToString() ?? string.Empty;
            if (request.Content is not null)
            {
                LetzteAnfrage += " " + await request.Content
                    .ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            }

            return new HttpResponseMessage(_status)
            {
                Content = new StringContent(_inhalt)
            };
        }
    }
}
```

- [ ] **Step 2: Test laufen lassen und Fehlschlag prüfen**

```bash
dotnet test tests/AuswertungPro.Next.Infrastructure.Tests/AuswertungPro.Next.Infrastructure.Tests.csproj --filter "FullyQualifiedName~UriLookupClientTests"
```

Erwartet: die Klassen sind unbekannt.

- [ ] **Step 3: Das HTTP-Tor schreiben**

```csharp
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using AuswertungPro.Next.Application.Common;

namespace AuswertungPro.Next.Infrastructure.Dossiers.Lookup;

/// <summary>
/// Der gemeinsame Weg nach draussen fuer die drei Auskunftsleser: Zeitlimit,
/// Abbruch und Aufrufe der Reihe nach.
///
/// Die Abfragen gehen an einen oeffentlichen Dienst des Kantons. Sie laufen
/// deshalb bewusst NACHEINANDER — ein Schwall gleichzeitiger Anfragen waere
/// unhoeflich und kann gesperrt werden.
///
/// Ein Fehler ergibt null. Der Aufrufer meldet das als Warnung; nichts wird
/// geraten.
/// </summary>
public sealed class GeoUrHttpGateway : IDisposable
{
    private static readonly TimeSpan Standardzeit = TimeSpan.FromSeconds(45);

    private readonly HttpClient _http;
    private readonly SemaphoreSlim _einerNachDemAnderen = new(1, 1);
    private readonly bool _eigenerClient;

    public GeoUrHttpGateway(HttpMessageHandler? handler = null, TimeSpan? timeout = null)
    {
        _http = handler is null ? new HttpClient() : new HttpClient(handler, disposeHandler: false);
        _http.Timeout = timeout ?? Standardzeit;
        _eigenerClient = true;

        // Ein sprechender Absender ist bei einem fremden Dienst Anstand.
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("SewerStudio/1.0");
    }

    public async Task<string?> GetStringAsync(Uri uri, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(uri);
        return await SendeAsync(() => new HttpRequestMessage(HttpMethod.Get, uri), ct)
            .ConfigureAwait(false);
    }

    public async Task<string?> PostFormAsync(
        Uri uri, IReadOnlyDictionary<string, string> form, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(uri);
        ArgumentNullException.ThrowIfNull(form);

        return await SendeAsync(
            () => new HttpRequestMessage(HttpMethod.Post, uri)
            {
                Content = new FormUrlEncodedContent(form)
            },
            ct).ConfigureAwait(false);
    }

    private async Task<string?> SendeAsync(
        Func<HttpRequestMessage> baueAnfrage, CancellationToken ct)
    {
        await _einerNachDemAnderen.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            using var anfrage = baueAnfrage();
            using var antwort = await _http.SendAsync(anfrage, ct).ConfigureAwait(false);

            if (!antwort.IsSuccessStatusCode)
                return null;

            var bytes = await antwort.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
            return LiesText(bytes, antwort.Content.Headers.ContentType?.CharSet);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            BestEffort.ReportWarning($"[Dossier-Auskunft] Abfrage fehlgeschlagen: {ex.Message}");
            return null;
        }
        finally
        {
            _einerNachDemAnderen.Release();
        }
    }

    /// <summary>
    /// Die Grundbuchauskunft ist ISO-8859-1. Wird sie als UTF-8 gelesen, wird
    /// aus einem Umlaut ein Fragezeichen — und damit steht ein verstuemmelter
    /// Name im Brief an den Eigentuemer.
    /// </summary>
    private static string LiesText(byte[] bytes, string? charSet)
    {
        var kodierung = Encoding.UTF8;

        if (!string.IsNullOrWhiteSpace(charSet))
        {
            try
            {
                kodierung = Encoding.GetEncoding(charSet.Trim('"'));
            }
            catch (ArgumentException)
            {
                kodierung = Encoding.UTF8;
            }
        }

        return kodierung.GetString(bytes);
    }

    public void Dispose()
    {
        if (_eigenerClient)
            _http.Dispose();

        _einerNachDemAnderen.Dispose();
    }
}
```

**Hinweis für den Umsetzer:** `Encoding.GetEncoding("ISO-8859-1")` braucht unter .NET die
Registrierung der Zusatzkodierungen. Falls der Aufruf mit einer `ArgumentException` fehlschlägt,
im statischen Konstruktor von `GeoUrHttpGateway`
`Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);` ergänzen — das steht in
`System.Text.Encoding.CodePages`, das über die Standardbibliothek verfügbar ist. Ist das Paket
nicht vorhanden, stattdessen `Encoding.Latin1` verwenden (in .NET 5+ eingebaut) und im Bericht
vermerken.

- [ ] **Step 4: Die drei Leser schreiben**

`UriParcelWfsClient.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

using AuswertungPro.Next.Application.Dossiers.Lookup;

namespace AuswertungPro.Next.Infrastructure.Dossiers.Lookup;

/// <summary>
/// Liest Liegenschaften aus dem WFS des Kantons Uri. Nur Netz plus Parser,
/// keine Regel.
/// </summary>
public sealed class UriParcelWfsClient : IParcelLookup
{
    private const string Dienst = "https://geo.ur.ch/wfs";
    private const string EbeneParzellen = "av:ch059_liegenschaften_flaechen";
    private const string EbeneGemeinden = "av:ch062_hoheitsgrenzen_gemeindegrenzen";

    private readonly GeoUrHttpGateway _gateway;

    public UriParcelWfsClient(GeoUrHttpGateway gateway)
        => _gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));

    public async Task<ParcelInfo?> FindAsync(
        int bfsNr, string parcelNumber, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(parcelNumber))
            return null;

        // Ueber die BFS-Nummer suchen, nicht ueber den Gemeindenamen: Schreibweisen
        // wie "Altdorf (UR)" sind damit kein Thema.
        var filter = $"nummer='{Maskiere(parcelNumber)}' AND bfsnr={bfsNr.ToString(CultureInfo.InvariantCulture)}";
        var xml = await _gateway.GetStringAsync(BaueAbfrage(EbeneParzellen, filter), ct)
            .ConfigureAwait(false);

        var parzellen = ParcelWfsXmlParser.Parse(xml);
        return parzellen.Count == 1 ? parzellen[0] : null;
    }

    public async Task<IReadOnlyList<ParcelInfo>> FindTouchedAsync(
        IReadOnlyList<string> wktLines, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(wktLines);
        if (wktLines.Count == 0)
            return Array.Empty<ParcelInfo>();

        var linien = string.Join(",", ExtrahiereLinienkoerper(wktLines));
        var filter = $"INTERSECTS(wkb_geometry,MULTILINESTRING({linien}))";

        // Per POST, weil der Filter fuer ein ganzes Projekt mehrere tausend
        // Zeichen lang wird und nicht in eine Adresszeile gehoert.
        var xml = await _gateway.PostFormAsync(
            new Uri(Dienst),
            new Dictionary<string, string>
            {
                ["service"] = "WFS",
                ["version"] = "2.0.0",
                ["request"] = "GetFeature",
                ["typeNames"] = EbeneParzellen,
                ["srsName"] = "EPSG:2056",
                ["CQL_FILTER"] = filter
            },
            ct).ConfigureAwait(false);

        return ParcelWfsXmlParser.Parse(xml);
    }

    public async Task<IReadOnlyList<Municipality>> ListMunicipalitiesAsync(
        CancellationToken ct = default)
    {
        var xml = await _gateway
            .GetStringAsync(BaueAbfrage(EbeneGemeinden, filter: null), ct)
            .ConfigureAwait(false);

        return ParcelWfsXmlParser.ParseMunicipalities(xml);
    }

    private static Uri BaueAbfrage(string ebene, string? filter)
    {
        var abfrage = HttpUtility.ParseQueryString(string.Empty);
        abfrage["service"] = "WFS";
        abfrage["version"] = "2.0.0";
        abfrage["request"] = "GetFeature";
        abfrage["typeNames"] = ebene;
        abfrage["srsName"] = "EPSG:2056";
        if (!string.IsNullOrWhiteSpace(filter))
            abfrage["CQL_FILTER"] = filter;

        return new Uri(Dienst + "?" + abfrage);
    }

    /// <summary>Aus "LINESTRING(a b,c d)" wird "(a b,c d)" fuer die Sammelgeometrie.</summary>
    private static IEnumerable<string> ExtrahiereLinienkoerper(IReadOnlyList<string> wktLines)
    {
        foreach (var linie in wktLines)
        {
            if (string.IsNullOrWhiteSpace(linie))
                continue;

            var start = linie.IndexOf('(');
            if (start < 0 || !linie.EndsWith(")", StringComparison.Ordinal))
                continue;

            yield return linie[start..];
        }
    }

    private static string Maskiere(string wert) => wert.Replace("'", "''");
}
```

`UriSewerNetworkWfsClient.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

using AuswertungPro.Next.Application.Dossiers.Lookup;

namespace AuswertungPro.Next.Infrastructure.Dossiers.Lookup;

/// <summary>
/// Liest das Abwassernetz des Kantons Uri. Nur Netz plus Parser, keine Regel.
/// </summary>
public sealed class UriSewerNetworkWfsClient : ISewerNetworkLookup
{
    private const string Dienst = "https://geo.ur.ch/wfs";
    private const string Ebene = "leitungen:abw_haltungen";

    /// <summary>Mehr Namen je Anfrage machen die Adresszeile zu lang.</summary>
    private const int NamenJeAnfrage = 25;

    private readonly GeoUrHttpGateway _gateway;

    public UriSewerNetworkWfsClient(GeoUrHttpGateway gateway)
        => _gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));

    public async Task<IReadOnlyList<NetworkHolding>> FindByNamesAsync(
        IReadOnlyList<string> names, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(names);

        var sauber = names
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Select(n => n.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var ergebnis = new List<NetworkHolding>();

        for (var i = 0; i < sauber.Count; i += NamenJeAnfrage)
        {
            ct.ThrowIfCancellationRequested();

            var teil = sauber.Skip(i).Take(NamenJeAnfrage)
                .Select(n => "'" + n.Replace("'", "''") + "'");
            var filter = "ne_bezeichnung IN (" + string.Join(",", teil) + ")";

            var xml = await _gateway.GetStringAsync(BaueAbfrage(filter), ct).ConfigureAwait(false);
            ergebnis.AddRange(SewerNetworkWfsXmlParser.Parse(xml));
        }

        return ergebnis;
    }

    public async Task<IReadOnlyList<NetworkHolding>> FindOnParcelAsync(
        ParcelInfo parcel, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(parcel);
        if (string.IsNullOrWhiteSpace(parcel.OutlineWkt))
            return Array.Empty<NetworkHolding>();

        var xml = await _gateway.PostFormAsync(
            new Uri(Dienst),
            new Dictionary<string, string>
            {
                ["service"] = "WFS",
                ["version"] = "2.0.0",
                ["request"] = "GetFeature",
                ["typeNames"] = Ebene,
                ["srsName"] = "EPSG:2056",
                ["CQL_FILTER"] = $"INTERSECTS(wkb_geometry,{parcel.OutlineWkt})"
            },
            ct).ConfigureAwait(false);

        return SewerNetworkWfsXmlParser.Parse(xml);
    }

    private static Uri BaueAbfrage(string filter)
    {
        var abfrage = HttpUtility.ParseQueryString(string.Empty);
        abfrage["service"] = "WFS";
        abfrage["version"] = "2.0.0";
        abfrage["request"] = "GetFeature";
        abfrage["typeNames"] = Ebene;
        abfrage["srsName"] = "EPSG:2056";
        abfrage["CQL_FILTER"] = filter;

        return new Uri(Dienst + "?" + abfrage);
    }
}
```

`UriLandRegistryClient.cs`:

```csharp
using System;
using System.Threading;
using System.Threading.Tasks;

using AuswertungPro.Next.Application.Dossiers.Lookup;

namespace AuswertungPro.Next.Infrastructure.Dossiers.Lookup;

/// <summary>
/// Liest die oeffentliche Grundbuchauskunft des Kantons Uri. Nur Netz plus
/// Parser, keine Regel.
///
/// Die Adresse kommt aus dem Parzellendienst (Feld url_grundbuch) und wird
/// nicht selbst zusammengebaut: aendert der Kanton sie, folgt der Leser von
/// selbst. Ohne Adresse wird nichts geraten.
/// </summary>
public sealed class UriLandRegistryClient : ILandRegistryLookup
{
    private readonly GeoUrHttpGateway _gateway;

    public UriLandRegistryClient(GeoUrHttpGateway gateway)
        => _gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));

    public async Task<LandRegistryEntry?> ReadAsync(
        ParcelInfo parcel, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(parcel);

        if (string.IsNullOrWhiteSpace(parcel.LandRegistryUrl))
            return null;

        if (!Uri.TryCreate(parcel.LandRegistryUrl, UriKind.Absolute, out var adresse))
            return null;

        var html = await _gateway.GetStringAsync(adresse, ct).ConfigureAwait(false);
        return LandRegistryHtmlParser.Parse(html);
    }
}
```

- [ ] **Step 5: Tests laufen lassen und Erfolg prüfen**

```bash
dotnet test tests/AuswertungPro.Next.Infrastructure.Tests/AuswertungPro.Next.Infrastructure.Tests.csproj --filter "FullyQualifiedName~UriLookupClientTests"
```

Erwartet: 5 Tests grün.

- [ ] **Step 6: Committen**

```bash
git add src/AuswertungPro.Next.Infrastructure/Dossiers/Lookup tests/AuswertungPro.Next.Infrastructure.Tests/Dossiers/Lookup/UriLookupClientTests.cs
git commit -m "feat(dossier): Auskunftsleser ans Netz haengen"
```

---

### Task 8: Zusammenbau und Durchreichen

**Files:**
- Modify: `src/AuswertungPro.Next.Infrastructure/Dossiers/DossierComposition.cs`
- Modify: `src/AuswertungPro.Next.UI/ServiceProvider.Dossiers.cs`
- Test: `tests/AuswertungPro.Next.Infrastructure.Tests/Dossiers/Lookup/DossierLookupCompositionTests.cs`

**Interfaces:**
- Consumes: alle Leser aus Task 7, `DossierBatchProposalUseCase` aus Task 5
- Produces: `DossierComposition.BatchProposal` (`DossierBatchProposalUseCase`) und
  `DossierComposition.Municipalities` (`IParcelLookup`);
  `ServiceProvider.DossierBatchProposal`, `ServiceProvider.DossierParcels`

- [ ] **Step 1: Den fehlschlagenden Test schreiben**

```csharp
using AuswertungPro.Next.Application.Dossiers.Lookup;
using AuswertungPro.Next.Infrastructure.Dossiers.Lookup;

using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.Dossiers.Lookup;

public sealed class DossierLookupCompositionTests
{
    [Fact]
    public void Die_Leser_erfuellen_ihre_Vertraege_und_passen_in_den_Anwendungsfall()
    {
        // Kein Netzzugriff: die Konstruktoren rufen nichts ab. Geprueft wird
        // allein, dass die Teile zusammenpassen — genau das, was beim
        // Zusammenbau schiefgehen kann.
        using var gateway = new GeoUrHttpGateway();

        IParcelLookup parzellen = new UriParcelWfsClient(gateway);
        ILandRegistryLookup grundbuch = new UriLandRegistryClient(gateway);
        ISewerNetworkLookup netz = new UriSewerNetworkWfsClient(gateway);

        var anwendungsfall = new DossierBatchProposalUseCase(parzellen, grundbuch, netz);

        Assert.NotNull(anwendungsfall);
    }

    [Fact]
    public void Ohne_Leser_gibt_es_keinen_Anwendungsfall()
    {
        using var gateway = new GeoUrHttpGateway();

        Assert.Throws<System.ArgumentNullException>(() => new DossierBatchProposalUseCase(
            null!, new UriLandRegistryClient(gateway), new UriSewerNetworkWfsClient(gateway)));
    }
}
```

- [ ] **Step 2: Test laufen lassen und Fehlschlag prüfen**

```bash
dotnet test tests/AuswertungPro.Next.Infrastructure.Tests/AuswertungPro.Next.Infrastructure.Tests.csproj --filter "FullyQualifiedName~DossierLookupCompositionTests"
```

Erwartet: `BatchProposal` und `Parcels` gibt es nicht.

- [ ] **Step 3: Die Komposition erweitern**

In `DossierComposition.cs` im Konstruktor nach `PdfAssembly = new DossierPdfAssemblyService(pdfMerge);`
ergänzen:

```csharp
        // Die Auskunftsleser teilen sich ein Tor nach draussen: ein Zeitlimit,
        // ein Abbruch, Aufrufe der Reihe nach.
        var gateway = new Lookup.GeoUrHttpGateway();
        Parcels = new Lookup.UriParcelWfsClient(gateway);
        BatchProposal = new DossierBatchProposalUseCase(
            Parcels,
            new Lookup.UriLandRegistryClient(gateway),
            new Lookup.UriSewerNetworkWfsClient(gateway));
```

und als Eigenschaften ergänzen:

```csharp
    /// <summary>Liest Liegenschaften aus dem Parzellendienst des Kantons.</summary>
    public IParcelLookup Parcels { get; }

    /// <summary>Stellt die Dossier-Vorschlaege eines Projekts zusammen.</summary>
    public DossierBatchProposalUseCase BatchProposal { get; }
```

Am Dateikopf `using AuswertungPro.Next.Application.Dossiers.Lookup;` ergänzen.

- [ ] **Step 4: Im ServiceProvider durchreichen**

In `src/AuswertungPro.Next.UI/ServiceProvider.Dossiers.cs` ergänzen:

```csharp
    /// <summary>Liest Liegenschaften aus dem Parzellendienst des Kantons.</summary>
    public IParcelLookup DossierParcels => _dossierComposition.Parcels;

    /// <summary>Stellt die Dossier-Vorschlaege eines Projekts zusammen.</summary>
    public DossierBatchProposalUseCase DossierBatchProposal => _dossierComposition.BatchProposal;
```

Am Dateikopf `using AuswertungPro.Next.Application.Dossiers.Lookup;` ergänzen.

- [ ] **Step 5: Tests laufen lassen und Erfolg prüfen**

```bash
dotnet build AuswertungPro.sln
dotnet test tests/AuswertungPro.Next.Infrastructure.Tests/AuswertungPro.Next.Infrastructure.Tests.csproj --filter "FullyQualifiedName~DossierLookupCompositionTests"
```

Erwartet: Build ohne Fehler, 1 Test grün.

- [ ] **Step 6: Committen**

```bash
git add src/AuswertungPro.Next.Infrastructure/Dossiers/DossierComposition.cs src/AuswertungPro.Next.UI/ServiceProvider.Dossiers.cs tests/AuswertungPro.Next.Infrastructure.Tests/Dossiers/Lookup/DossierLookupCompositionTests.cs
git commit -m "feat(dossier): Auskunft im Dossier-Subsystem verdrahten"
```

---

### Task 9: Das Fenster

**Files:**
- Create: `src/AuswertungPro.Next.UI/ViewModels/Windows/DossierBatchViewModel.cs`
- Create: `src/AuswertungPro.Next.UI/Views/Windows/DossierBatchWindow.xaml`
- Create: `src/AuswertungPro.Next.UI/Views/Windows/DossierBatchWindow.xaml.cs`
- Modify: `src/AuswertungPro.Next.UI/Views/Pages/DossiersPage.xaml:60-66`
- Modify: `src/AuswertungPro.Next.UI/ViewModels/Pages/DossiersPageViewModel.Actions.cs`
- Test: `tests/AuswertungPro.Next.UI.Tests/DossierBatchViewModelTests.cs`

**Interfaces:**
- Consumes: `DossierBatchProposalUseCase`, `IParcelLookup`, `DossierBatchCreationUseCase`,
  `DossierCreationSelection`, `DossierProposal`, `ProposedHolding`, `Municipality`
- Produces: nichts für spätere Aufgaben

- [ ] **Step 1: Den fehlschlagenden Test schreiben**

Geprüft wird nur die Auswahl-Logik des ViewModels — ohne Fenster, ohne Netz.

```csharp
using System;
using System.Collections.Generic;
using System.Linq;

using AuswertungPro.Next.Application.Dossiers.Lookup;
using AuswertungPro.Next.UI.ViewModels.Windows;

using Xunit;

namespace AuswertungPro.Next.UI.Tests;

public sealed class DossierBatchViewModelTests
{
    private static DossierProposal Vorschlag(string nummer, bool waehlbar, string grund = "")
    {
        var parzelle = new ParcelInfo(nummer, 1206, "Musterdorf", 500, "CH1",
            "POLYGON((0 0,1 0,1 1,0 0))", "https://example.invalid/gb");

        var eintrag = new LandRegistryEntry("Musterstrasse", "30", "6472", "Musterdorf",
            new[] { new LandRegistryOwner("", "Martin Muster", "Musterstrasse 30, 6472 Musterdorf", "") },
            NoOwnerRegistered: false);

        var leitungen = new[]
        {
            new ProposedHolding("36051-36329", true, true, true, "Lage"),
            new ProposedHolding("36329-35558", false, true, false, "Lage")
        };

        return new DossierProposal(parzelle, eintrag, leitungen,
            "Liegenschaft Nr. " + nummer + " Muster", waehlbar, grund);
    }

    [Fact]
    public void Waehlbare_Vorschlaege_sind_angehakt_gesperrte_nicht()
    {
        var vm = new DossierBatchViewModel();
        vm.Uebernehmen(new DossierBatchProposalResult(
            new[] { Vorschlag("439", true), Vorschlag("13", false, "kein Eigentümer") },
            Array.Empty<string>()));

        Assert.True(vm.Rows[0].IsSelected);
        Assert.False(vm.Rows[1].IsSelected);
        Assert.False(vm.Rows[1].CanSelect);
        Assert.Equal(1, vm.SelectedCount);
    }

    [Fact]
    public void Ein_gesperrter_Vorschlag_laesst_sich_nicht_anhaken()
    {
        var vm = new DossierBatchViewModel();
        vm.Uebernehmen(new DossierBatchProposalResult(
            new[] { Vorschlag("13", false, "kein Eigentümer") }, Array.Empty<string>()));

        vm.Rows[0].IsSelected = true;

        Assert.False(vm.Rows[0].IsSelected);
        Assert.Equal(0, vm.SelectedCount);
    }

    [Fact]
    public void Die_Auswahl_reicht_nur_angehakte_Leitungen_weiter()
    {
        var vm = new DossierBatchViewModel();
        vm.Uebernehmen(new DossierBatchProposalResult(
            new[] { Vorschlag("439", true) }, Array.Empty<string>()));

        var auswahl = vm.BaueAuswahl();

        var eintrag = Assert.Single(auswahl);
        Assert.Equal(new[] { "36051-36329" }, eintrag.SelectedHoldingDesignations);
    }

    [Fact]
    public void Warnungen_werden_sichtbar_gemacht()
    {
        var vm = new DossierBatchViewModel();
        vm.Uebernehmen(new DossierBatchProposalResult(
            Array.Empty<DossierProposal>(), new[] { "Dienst nicht erreichbar" }));

        Assert.Contains("Dienst nicht erreichbar", vm.WarningText, StringComparison.Ordinal);
    }
}
```

- [ ] **Step 2: Test laufen lassen und Fehlschlag prüfen**

```bash
dotnet test tests/AuswertungPro.Next.UI.Tests/AuswertungPro.Next.UI.Tests.csproj --filter "FullyQualifiedName~DossierBatchViewModelTests"
```

Erwartet: `DossierBatchViewModel` unbekannt.

- [ ] **Step 3: Das ViewModel schreiben**

```csharp
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

using AuswertungPro.Next.Application.Dossiers.Lookup;

namespace AuswertungPro.Next.UI.ViewModels.Windows;

/// <summary>Eine Zeile der Vorschlagsliste.</summary>
public sealed class DossierBatchRow : INotifyPropertyChanged
{
    private bool _isSelected;

    public DossierBatchRow(DossierProposal proposal)
    {
        Proposal = proposal ?? throw new ArgumentNullException(nameof(proposal));
        Holdings = proposal.Holdings.Select(h => new DossierBatchHoldingRow(h)).ToList();
        _isSelected = proposal.Selectable;
    }

    public DossierProposal Proposal { get; }

    public IReadOnlyList<DossierBatchHoldingRow> Holdings { get; }

    public bool CanSelect => Proposal.Selectable;

    public string ParcelNumber => Proposal.Parcel.Number;

    public string Name => Proposal.SuggestedName;

    public string OwnerSummary => Proposal.Registry is null
        ? Proposal.SkipReason
        : string.Join(" / ", Proposal.Registry.Owners.Select(o => o.Name));

    /// <summary>Zaehlt nur die angehakten Leitungen; der Rest ist Hinweis.</summary>
    public string HoldingSummary
        => $"{Holdings.Count(h => h.IsSelected)} von {Holdings.Count} Leitungen";

    public string SkipReason => Proposal.SkipReason;

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            // Ein gesperrter Vorschlag bleibt gesperrt, auch wenn jemand klickt.
            var neu = value && CanSelect;
            if (neu == _isSelected)
                return;

            _isSelected = neu;
            Melde();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Melde([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>Eine Leitung innerhalb einer Zeile.</summary>
public sealed class DossierBatchHoldingRow
{
    public DossierBatchHoldingRow(ProposedHolding holding)
    {
        Holding = holding ?? throw new ArgumentNullException(nameof(holding));
        IsSelected = holding.Preselected;
    }

    public ProposedHolding Holding { get; }

    public bool IsSelected { get; set; }

    public string Designation => Holding.Designation;

    public string Note => Holding switch
    {
        { IsPrivate: false } => "gehört dem Werk",
        { InProject: false } => "nicht im Projekt",
        { Origin: "Name" } => "aus dem Leitungsnamen",
        _ => ""
    };
}

/// <summary>
/// Der Zustand des Stapelanlage-Fensters. Enthaelt keine Regel und kein Netz —
/// er nimmt ein fertiges Ergebnis entgegen und gibt die Auswahl zurueck.
/// </summary>
public sealed class DossierBatchViewModel : INotifyPropertyChanged
{
    private string _warningText = string.Empty;

    public ObservableCollection<DossierBatchRow> Rows { get; } = new();

    public string WarningText
    {
        get => _warningText;
        private set
        {
            _warningText = value;
            Melde();
        }
    }

    public int SelectedCount => Rows.Count(r => r.IsSelected);

    public void Uebernehmen(DossierBatchProposalResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        Rows.Clear();
        foreach (var vorschlag in result.Proposals)
            Rows.Add(new DossierBatchRow(vorschlag));

        WarningText = result.Warnings.Count == 0
            ? string.Empty
            : string.Join(Environment.NewLine, result.Warnings);

        Melde(nameof(SelectedCount));
    }

    public IReadOnlyList<DossierCreationSelection> BaueAuswahl()
        => Rows
            .Where(r => r.IsSelected)
            .Select(r => new DossierCreationSelection(
                r.Proposal,
                r.Holdings.Where(h => h.IsSelected).Select(h => h.Designation).ToList()))
            .ToList();

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Melde([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
```

- [ ] **Step 4: Test laufen lassen und Erfolg prüfen**

```bash
dotnet test tests/AuswertungPro.Next.UI.Tests/AuswertungPro.Next.UI.Tests.csproj --filter "FullyQualifiedName~DossierBatchViewModelTests"
```

Erwartet: 4 Tests grün.

- [ ] **Step 5: Das Fenster schreiben**

`DossierBatchWindow.xaml`:

```xml
<Window x:Class="AuswertungPro.Next.UI.Views.Windows.DossierBatchWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:ui="clr-namespace:AuswertungPro.Next.UI"
        ui:WindowFx.Entrance="True"
        Title="Dossiers aus dem Projekt erzeugen"
        Width="900" Height="640"
        WindowStartupLocation="CenterOwner"
        Background="{DynamicResource BgBrush}">
    <Grid Margin="18">
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="*"/>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="Auto"/>
        </Grid.RowDefinitions>

        <TextBlock Grid.Row="0" TextWrapping="Wrap" Margin="0,0,0,10"
                   Foreground="{DynamicResource MutedBrush}"
                   Text="Das Programm ermittelt die Parzellen aus den Leitungen des Projekts, holt die Eigentümer aus dem öffentlichen Grundbuch und schlägt die betroffenen privaten Leitungen vor. Telefonnummern werden nicht ermittelt."/>

        <StackPanel Grid.Row="1" Orientation="Horizontal" Margin="0,0,0,10">
            <TextBlock Text="Gemeinde:" VerticalAlignment="Center" Margin="0,0,8,0"/>
            <ComboBox x:Name="MunicipalityBox" Width="240" DisplayMemberPath="Name"/>
            <Button x:Name="StartButton" Content="Suchen" Padding="14,6" Margin="12,0,0,0"
                    Click="OnStart"/>
            <Button x:Name="CancelSearchButton" Content="Abbrechen" Padding="14,6" Margin="8,0,0,0"
                    IsEnabled="False" Click="OnCancelSearch"/>
        </StackPanel>

        <DataGrid Grid.Row="2" x:Name="ProposalGrid" AutoGenerateColumns="False"
                  CanUserAddRows="False" HeadersVisibility="Column"
                  GridLinesVisibility="Horizontal" IsReadOnly="False">
            <DataGrid.Columns>
                <DataGridCheckBoxColumn Header="" Width="40"
                                        Binding="{Binding IsSelected, UpdateSourceTrigger=PropertyChanged}"/>
                <DataGridTextColumn Header="Parzelle" Width="80" IsReadOnly="True"
                                    Binding="{Binding ParcelNumber}"/>
                <DataGridTextColumn Header="Dossier" Width="230" IsReadOnly="True"
                                    Binding="{Binding Name}"/>
                <DataGridTextColumn Header="Eigentümer" Width="*" IsReadOnly="True"
                                    Binding="{Binding OwnerSummary}"/>
                <DataGridTextColumn Header="Leitungen" Width="130" IsReadOnly="True"
                                    Binding="{Binding HoldingSummary}"/>
                <DataGridTextColumn Header="Hinweis" Width="180" IsReadOnly="True"
                                    Binding="{Binding SkipReason}"/>
            </DataGrid.Columns>
        </DataGrid>

        <TextBlock Grid.Row="3" x:Name="StatusText" TextWrapping="Wrap" Margin="0,10,0,0"/>

        <StackPanel Grid.Row="4" Orientation="Horizontal" HorizontalAlignment="Right"
                    Margin="0,12,0,0">
            <Button x:Name="CreateButton" Content="Dossiers erzeugen" Padding="16,7"
                    IsEnabled="False" Click="OnCreate"/>
            <Button Content="Schliessen" Padding="16,7" Margin="8,0,0,0" Click="OnClose"/>
        </StackPanel>
    </Grid>
</Window>
```

`DossierBatchWindow.xaml.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

using AuswertungPro.Next.Application.Dossiers.Lookup;
using AuswertungPro.Next.Domain.Models.Dossiers;
using AuswertungPro.Next.UI.ViewModels.Windows;

namespace AuswertungPro.Next.UI.Views.Windows;

/// <summary>
/// Erzeugt mehrere Eigentuemerdossiers auf einmal. Das Fenster zeigt nur an und
/// haekelt ab; die Regeln liegen in den Anwendungsfaellen.
/// </summary>
public partial class DossierBatchWindow : Window
{
    private readonly DossierBatchViewModel _viewModel = new();
    private readonly IParcelLookup _parcels;
    private readonly DossierBatchProposalUseCase _proposal;
    private readonly IReadOnlyList<string> _projectHoldingNames;
    private readonly IReadOnlyDictionary<string, Guid> _holdingIdsByName;
    private readonly IReadOnlyList<string> _parcelsWithDossier;

    private CancellationTokenSource? _laufendeSuche;

    private DossierBatchWindow(
        IParcelLookup parcels,
        DossierBatchProposalUseCase proposal,
        IReadOnlyList<string> projectHoldingNames,
        IReadOnlyDictionary<string, Guid> holdingIdsByName,
        IReadOnlyList<string> parcelsWithDossier)
    {
        InitializeComponent();

        _parcels = parcels;
        _proposal = proposal;
        _projectHoldingNames = projectHoldingNames;
        _holdingIdsByName = holdingIdsByName;
        _parcelsWithDossier = parcelsWithDossier;

        ProposalGrid.ItemsSource = _viewModel.Rows;
        Loaded += async (_, _) => await LadeGemeinden().ConfigureAwait(true);
    }

    /// <summary>Die erzeugten Dossiers. Leer, wenn abgebrochen wurde.</summary>
    public IReadOnlyList<DossierDefinition> Created { get; private set; } = Array.Empty<DossierDefinition>();

    public static IReadOnlyList<DossierDefinition> ShowFor(
        IParcelLookup parcels,
        DossierBatchProposalUseCase proposal,
        IReadOnlyList<string> projectHoldingNames,
        IReadOnlyDictionary<string, Guid> holdingIdsByName,
        IReadOnlyList<string> parcelsWithDossier)
    {
        ArgumentNullException.ThrowIfNull(parcels);
        ArgumentNullException.ThrowIfNull(proposal);

        var fenster = new DossierBatchWindow(
            parcels, proposal, projectHoldingNames, holdingIdsByName, parcelsWithDossier)
        {
            Owner = System.Windows.Application.Current?.MainWindow
        };

        return fenster.ShowDialog() == true ? fenster.Created : Array.Empty<DossierDefinition>();
    }

    private async Task LadeGemeinden()
    {
        try
        {
            StatusText.Text = "Gemeindeliste wird geladen…";
            var gemeinden = await _parcels.ListMunicipalitiesAsync().ConfigureAwait(true);
            MunicipalityBox.ItemsSource = gemeinden;
            StatusText.Text = gemeinden.Count == 0
                ? "Die Gemeindeliste konnte nicht geladen werden. Ohne Netzverbindung geht diese Funktion nicht."
                : string.Empty;
        }
        catch (Exception ex)
        {
            StatusText.Text = "Die Gemeindeliste konnte nicht geladen werden: " + ex.Message;
        }
    }

    private async void OnStart(object sender, RoutedEventArgs e)
    {
        if (MunicipalityBox.SelectedItem is not Municipality gemeinde)
        {
            StatusText.Text = "Bitte zuerst die Gemeinde wählen.";
            return;
        }

        _laufendeSuche?.Cancel();
        _laufendeSuche = new CancellationTokenSource();

        StartButton.IsEnabled = false;
        CancelSearchButton.IsEnabled = true;
        CreateButton.IsEnabled = false;

        var fortschritt = new Progress<string>(text => StatusText.Text = text);

        try
        {
            var ergebnis = await _proposal.RunAsync(
                new DossierBatchProposalRequest(
                    gemeinde.BfsNr, _projectHoldingNames, _parcelsWithDossier),
                fortschritt,
                _laufendeSuche.Token).ConfigureAwait(true);

            _viewModel.Uebernehmen(ergebnis);
            CreateButton.IsEnabled = _viewModel.SelectedCount > 0;

            StatusText.Text = _viewModel.Rows.Count == 0
                ? "Keine Parzellen gefunden."
                : $"{_viewModel.Rows.Count} Parzellen gefunden. {_viewModel.WarningText}".Trim();
        }
        catch (OperationCanceledException)
        {
            StatusText.Text = "Abgebrochen. Es wurde nichts erzeugt.";
        }
        catch (Exception ex)
        {
            StatusText.Text = "Die Suche ist fehlgeschlagen: " + ex.Message;
        }
        finally
        {
            StartButton.IsEnabled = true;
            CancelSearchButton.IsEnabled = false;
        }
    }

    private void OnCancelSearch(object sender, RoutedEventArgs e) => _laufendeSuche?.Cancel();

    private void OnCreate(object sender, RoutedEventArgs e)
    {
        Created = DossierBatchCreationUseCase.Build(_viewModel.BaueAuswahl(), _holdingIdsByName);
        DialogResult = true;
    }

    private void OnClose(object sender, RoutedEventArgs e) => DialogResult = false;
}
```

- [ ] **Step 6: Knopf und Befehl ergänzen**

In `src/AuswertungPro.Next.UI/Views/Pages/DossiersPage.xaml` nach dem Knopf „Aktualisieren"
(Zeile 65-66) ergänzen:

```xml
                    <Button Content="Aus Projekt erzeugen" Padding="12,7" Margin="8,0,0,0"
                            Command="{Binding CreateFromProjectCommand}"/>
```

In `src/AuswertungPro.Next.UI/ViewModels/Pages/DossiersPageViewModel.cs`:

Zwei Felder ergänzen (neben `_wordExport`):

```csharp
    private readonly IParcelLookup _parcels;
    private readonly DossierBatchProposalUseCase _batchProposal;
```

Zwei Konstruktorparameter **nach** `pdfAssembly` ergänzen und zuweisen:

```csharp
        IParcelLookup parcels,
        DossierBatchProposalUseCase batchProposal,
```

```csharp
        _parcels = parcels ?? throw new ArgumentNullException(nameof(parcels));
        _batchProposal = batchProposal ?? throw new ArgumentNullException(nameof(batchProposal));
```

Befehl anlegen (bei den anderen `AsyncRelayCommand`-Zeilen) und als Eigenschaft
veröffentlichen:

```csharp
        CreateFromProjectCommand = new AsyncRelayCommand(CreateFromProjectAsync);
```

```csharp
    public IAsyncRelayCommand CreateFromProjectCommand { get; }
```

Am Dateikopf `using AuswertungPro.Next.Application.Dossiers.Lookup;` ergänzen.

In `src/AuswertungPro.Next.UI/ViewModels/ShellViewModel.cs:169-181` die zwei neuen Argumente
an dieselbe Stelle setzen:

```csharp
                pdfAssembly: _sp.DossierPdfAssembly,
                parcels: _sp.DossierParcels,
                batchProposal: _sp.DossierBatchProposal,
                costStores: _sp.CostStores,
```

In `src/AuswertungPro.Next.UI/ViewModels/Pages/DossiersPageViewModel.Actions.cs` den Ablauf
ergänzen — er folgt dem Muster von `EditAreaAsync`:

```csharp
    /// <summary>
    /// Legt fuer die Parzellen des Projekts auf einmal Dossiers an. Die Regeln
    /// liegen in den Anwendungsfaellen; hier wird nur eingesammelt, das Fenster
    /// gezeigt und einmal gespeichert.
    /// </summary>
    private async Task CreateFromProjectAsync()
    {
        if (!EnsureProject(out var root))
            return;

        var project = _getProject();

        // Haltungsname -> Kennung. Ohne Namen laesst sich nichts zuordnen.
        var idsByName = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        foreach (var record in project.Data)
        {
            var name = (record.GetFieldValue(FieldKeys.HoldingName) ?? string.Empty).Trim();
            if (name.Length > 0)
                idsByName[name] = record.Id;
        }

        if (idsByName.Count == 0)
        {
            StatusMessage = "Das Projekt enthält keine Leitungen — es gibt nichts zu suchen.";
            return;
        }

        // Parzellen, fuer die es schon ein Dossier gibt, werden nicht erneut angeboten.
        var mitDossier = _document.Dossiers
            .Select(d => (d.ParcelNumbers ?? string.Empty).Trim())
            .Where(p => p.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var erzeugte = DossierBatchWindow.ShowFor(
            _parcels,
            _batchProposal,
            idsByName.Keys.ToList(),
            idsByName,
            mitDossier);

        if (erzeugte.Count == 0)
        {
            StatusMessage = "Es wurden keine Dossiers erzeugt.";
            return;
        }

        foreach (var dossier in erzeugte)
        {
            dossier.FolderName = DossierFolderPlanner.PlanFolderName(
                dossier.Name,
                candidate => _document.Dossiers.Any(d =>
                    string.Equals(d.FolderName, candidate, StringComparison.OrdinalIgnoreCase))
                    || Directory.Exists(Path.Combine(
                        DossierFolderPlanner.ResolveRoot(root), candidate)));

            _document.Dossiers.Add(dossier);
        }

        // Alle auf einmal: ein Speichervorgang, nicht einer je Dossier.
        if (!await SaveDocumentAsync(root))
            return;

        await ReloadAsync();
        StatusMessage = erzeugte.Count == 1
            ? "1 Dossier erzeugt."
            : $"{erzeugte.Count} Dossiers erzeugt.";
    }
```

Prüfe die `using`-Zeilen am Dateikopf: `System.Collections.Generic`, `System.IO`, `System.Linq`,
`AuswertungPro.Next.Application.Dossiers.Lookup`, `AuswertungPro.Next.Domain.Models` (für
`FieldKeys`) und `AuswertungPro.Next.UI.Views.Windows` müssen vorhanden sein.

**Warum `FolderName` erst hier gesetzt wird:** Der Ordnername muss gegen die schon vorhandenen
Dossiers und den Inhalt des Dossierordners eindeutig sein. Beides kennt nur die Seite, nicht
der Anwendungsfall — deshalb bleibt diese Regel dort, wo sie schon für „Neue Liegenschaft" steht.

- [ ] **Step 7: Bauen und alle Tests laufen lassen**

```bash
dotnet build AuswertungPro.sln
dotnet test AuswertungPro.sln
```

Erwartet: 0 Fehler, alle Tests grün. **Die Anwendung muss geschlossen sein**, sonst sperrt sie
die Ausgabedateien.

- [ ] **Step 8: Committen**

```bash
git add src/AuswertungPro.Next.UI tests/AuswertungPro.Next.UI.Tests/DossierBatchViewModelTests.cs
git commit -m "feat(dossier): Fenster fuer die Stapelanlage"
```

---

### Task 10: Abnahmetest gegen die echten Dienste

Ein einziger Test, der wirklich mit `geo.ur.ch` spricht — der Beweis, dass die Leser die echte
Antwort verstehen. Er ist ausdrücklich als solcher benannt.

**Files:**
- Test: `tests/AuswertungPro.Next.Infrastructure.Tests/Dossiers/Lookup/GeoUrLiveAcceptanceTests.cs`

**Interfaces:**
- Consumes: `UriParcelWfsClient`, `UriLandRegistryClient`, `UriSewerNetworkWfsClient`, `GeoUrHttpGateway`
- Produces: nichts

- [ ] **Step 1: Den Test schreiben**

```csharp
using System;
using System.Linq;
using System.Threading.Tasks;

using AuswertungPro.Next.Infrastructure.Dossiers.Lookup;

using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.Dossiers.Lookup;

/// <summary>
/// Der einzige Test, der mit den echten Diensten spricht. Er beweist, dass die
/// Leser die tatsaechliche Antwort verstehen — eine Fixture kann das nicht,
/// weil sie den Aufbau nur nachbaut.
///
/// Geprueft wird die Parzelle 439 in Erstfeld (BFS 1206) mit den am 2026-08-23
/// gemessenen Werten. Aendert der Kanton den Aufbau seiner Seiten, wird dieser
/// Test rot — genau dort, wo es auffallen muss.
///
/// Es werden bewusst KEINE Personennamen geprueft: die Zusicherungen kommen
/// ohne sie aus.
/// </summary>
public sealed class GeoUrLiveAcceptanceTests
{
    [Fact]
    public async Task Die_echten_Dienste_liefern_die_gemessenen_Werte_fuer_Parzelle_439()
    {
        using var gateway = new GeoUrHttpGateway();

        var parzellen = new UriParcelWfsClient(gateway);
        var parzelle = await parzellen.FindAsync(1206, "439");

        Assert.NotNull(parzelle);
        Assert.Equal("439", parzelle!.Number);
        Assert.Equal(1206, parzelle.BfsNr);
        Assert.Equal(1139, parzelle.AreaSqm);
        Assert.Equal("CH114627077847", parzelle.Egrid);
        Assert.StartsWith("POLYGON((", parzelle.OutlineWkt, StringComparison.Ordinal);
        Assert.Contains("grundbuchauskunft", parzelle.LandRegistryUrl, StringComparison.OrdinalIgnoreCase);

        // Zwei Miteigentuemer mit Kennzeichnung — ohne die Namen zu pruefen.
        var grundbuch = new UriLandRegistryClient(gateway);
        var eintrag = await grundbuch.ReadAsync(parzelle);

        Assert.NotNull(eintrag);
        Assert.False(eintrag!.NoOwnerRegistered);
        Assert.Equal(2, eintrag.Owners.Count);
        Assert.All(eintrag.Owners, o => Assert.False(string.IsNullOrWhiteSpace(o.Name)));
        Assert.Equal("Lit.A", eintrag.Owners[0].Designation);
        Assert.Equal("Lit.B", eintrag.Owners[1].Designation);
        Assert.Equal("6472", eintrag.PostalCode);
        Assert.Equal("Erstfeld", eintrag.Town);

        // Sechs Haltungen auf der Parzelle, davon fuenf privat.
        var netz = new UriSewerNetworkWfsClient(gateway);
        var haltungen = await netz.FindOnParcelAsync(parzelle);

        Assert.Equal(6, haltungen.Count);
        Assert.Equal(5, haltungen.Count(h => h.IsPrivate));
        Assert.Contains(haltungen, h => h.Designation == "36051-36329");

        // Und die Sammelabfrage findet dieselbe Haltung ueber ihren Namen.
        var nachName = await netz.FindByNamesAsync(new[] { "36051-36329" });
        Assert.Single(nachName);
    }

    [Fact]
    public async Task Die_Gemeindeliste_enthaelt_die_19_Urner_Gemeinden()
    {
        using var gateway = new GeoUrHttpGateway();

        var gemeinden = await new UriParcelWfsClient(gateway).ListMunicipalitiesAsync();

        Assert.Equal(19, gemeinden.Count);
        Assert.Contains(gemeinden, g => g.BfsNr == 1206 && g.Name == "Erstfeld");
    }
}
```

- [ ] **Step 2: Test laufen lassen**

```bash
dotnet test tests/AuswertungPro.Next.Infrastructure.Tests/AuswertungPro.Next.Infrastructure.Tests.csproj --filter "FullyQualifiedName~GeoUrLiveAcceptanceTests"
```

Erwartet: 2 Tests grün. **Braucht eine Netzverbindung.**

Schlägt einer fehl, ist das ein echtes Ergebnis und kein Testfehler: entweder hat der Kanton
etwas geändert, oder ein Leser versteht die Antwort nicht. Beides gehört in den Bericht,
zusammen mit der tatsächlichen Antwort des Dienstes.

- [ ] **Step 3: Committen**

```bash
git add tests/AuswertungPro.Next.Infrastructure.Tests/Dossiers/Lookup/GeoUrLiveAcceptanceTests.cs
git commit -m "test(dossier): Abnahme gegen die echten Auskunftsdienste"
```

---

### Task 11: Sichtprüfung durch Pascal

Kein Test ersetzt den Blick auf das Ergebnis.

**Files:** keine Änderung; nur Ausführung und Bericht.

- [ ] **Step 1: Programm starten**

```bash
dotnet build AuswertungPro.sln
```

Danach `SewerStudio.exe` starten und ein Urner Projekt öffnen.

- [ ] **Step 2: Den Ablauf durchgehen**

Dossier-Bereich → **Aus Projekt erzeugen** → Gemeinde wählen → **Suchen**.

| Prüfpunkt | Erwartung |
|---|---|
| Fortschritt | Es ist zu sehen, was gerade abgefragt wird |
| Abbrechen | Bricht wirklich ab, es entsteht nichts |
| Liste | Parzellen mit Eigentümern; ohne Eigentümer nicht anhakbar |
| Bestehende Dossiers | erscheinen mit Hinweis, nicht anhakbar |
| Leitungen | Zahl der angehakten stimmt mit der Erwartung überein |
| **Dossiers erzeugen** | Die Dossiers erscheinen in der Liste, einmal gespeichert |
| Word erzeugen | Ein erzeugtes Dossier lässt sich sofort als Word ausgeben |

- [ ] **Step 3: Ergebnis melden**

Abweichungen sammeln und melden, statt sie stillschweigend zu beheben — bei Fachfragen
entscheidet Pascal.
