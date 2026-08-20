# Lernende Kostenanalyse — Etappe 1 bis 3

> **Für agentische Bearbeiter:** ERFORDERLICHE UNTER-FÄHIGKEIT: `superpowers:subagent-driven-development`
> (empfohlen) oder `superpowers:executing-plans`, um diesen Plan Aufgabe für Aufgabe umzusetzen.
> Schritte nutzen Checkbox-Syntax (`- [ ]`) zur Nachverfolgung.

**Ziel:** Aus persönlich beurteilten Haltungen Fälle sammeln, daraus Massnahmen mit Mengen
vorschlagen, und die Vorhersagegüte rückblickend messen.

**Architektur:** Fallbasiert. Reine Rechenklassen in `Application/Kostenanalyse`, Dateizugriff
allein in `Infrastructure/Kostenanalyse`. Kein ML-Framework, kein LLM. Der bestehende
`MeasureRecommendationService` wird nicht angefasst.

**Technik:** C# / .NET 10, xUnit, System.Text.Json. Keine neuen NuGet-Pakete.

**Konzept:** `docs/superpowers/specs/2026-08-20-lernende-kostenanalyse-design.md`

## Übergreifende Vorgaben

Diese gelten für **jede** Aufgabe:

- Kommentare und Testnamen auf **Deutsch**. Klassen- und Methodennamen englisch/deutsch gemischt
  wie im Bestand üblich (`KostenfallExtraktor`, `TryErstellen`).
- **Keine neuen NuGet-Pakete.**
- `Application` darf **nicht** auf `Infrastructure` zugreifen. Reine Rechenlogik ohne Dateizugriff.
- Tests liegen in `tests/AuswertungPro.Next.Infrastructure.Tests/Kostenanalyse/` (dort werden
  Application-Klassen im Bestand getestet, siehe `Common/FachzahlParserTests.cs`).
- **Nicht-Schäden**, die nie als Schadensart zählen: `BCD`, `BCE`, `BDA`, `000M`.
  `BCA` (seitlicher Anschluss) zählt ebenfalls nicht als Schaden, wird aber als **eigenes
  Merkmal** `AnschlussAnzahl` geführt — Anschlüsse treiben die Menge „Anschluss einbinden".
- **Bögen** = Protokolleinträge mit Code beginnend `BCC`.
- Nur **ausgewählte** Kostenzeilen (`CostLine.Selected == true`) zählen — dieselbe Regel wie in
  der Kostenzusammenstellung.
- **Preise werden nie gelernt.** Ein Fall enthält nur Mengen.
- Zahlen-Formatierung fest auf `de-CH`, nie `CurrentCulture`.
- Nach jeder Aufgabe: `dotnet build AuswertungPro.sln` (0 Fehler) und die Tests der Aufgabe.

**Schwellen** (Startwerte, in Etappe 3 zu prüfen):

| Name | Wert |
|---|---|
| `MindestNachbarn` | 3 |
| `MaximalNachbarn` | 7 |
| `MindestBogenFaelle` | 10 |
| `PositionsMehrheit` | > 0.5 |

---

## Dateiübersicht

```
src/AuswertungPro.Next.Application/Kostenanalyse/
  KostenanalyseDtos.cs          Aufgabe 1   Merkmale, Fall, Vorschlag, Enthaltungsgrund
  KostenfallMerkmalLeser.cs     Aufgabe 2   HaltungRecord -> Merkmale
  MassnahmePaketLeser.cs        Aufgabe 3   HoldingCost  -> Positionen
  KostenfallExtraktor.cs        Aufgabe 4   beides + Wahrheitsregel -> Kostenfall
  KostenfallAehnlichkeit.cs     Aufgabe 6   Rangfolge der Nachbarn
  KostenVorschlagRechner.cs     Aufgabe 7   Nachbarn -> Positionen mit Mengen
  KostenVorschlagPolicy.cs      Aufgabe 8   Enthaltungsregeln an einer Stelle
  IKostenfallStore.cs           Aufgabe 5   Vertrag
  KostenanalyseMessung.cs       Aufgabe 9   Leave-one-out
  KostenfallAufbauLauf.cs       Aufgabe 10  Projekt -> Faelle

src/AuswertungPro.Next.Infrastructure/Kostenanalyse/
  KostenfallFileStore.cs        Aufgabe 5   JSON unter <KnowledgeRoot>\kostenanalyse\
  KostenanalyseBerichtSchreiber.cs  Aufgabe 11  Bericht + SHA-256

tools/KostenfallAufbau/
  Program.cs                    Aufgabe 11  Fälle aus einem Projekt aufbauen
```

---

## Aufgabe 1: Datenmodell

**Dateien:**
- Anlegen: `src/AuswertungPro.Next.Application/Kostenanalyse/KostenanalyseDtos.cs`
- Test: `tests/AuswertungPro.Next.Infrastructure.Tests/Kostenanalyse/KostenanalyseDtosTests.cs`

**Schnittstellen:**
- Verbraucht: nichts
- Liefert: `SchadensMerkmal`, `KostenfallMerkmale`, `MassnahmePosition`, `Kostenfall`,
  `KostenfallHerkunft`, `KostenVorschlag`, `EnthaltungsGrund`

- [ ] **Schritt 1: Fehlschlagenden Test schreiben**

```csharp
using System.Collections.Generic;
using AuswertungPro.Next.Application.Kostenanalyse;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.Kostenanalyse;

public sealed class KostenanalyseDtosTests
{
    [Fact]
    public void Merkmale_kennen_ihre_Schadensarten_als_Menge()
    {
        var merkmale = new KostenfallMerkmale
        {
            DnMm = 300,
            LaengeM = 42.5,
            BogenAnzahl = 1,
            AnschlussAnzahl = 3,
            Schaeden =
            [
                new SchadensMerkmal("BAF", 2, HatStrecke: true),
                new SchadensMerkmal("BAJ", 1, HatStrecke: false)
            ]
        };

        Assert.Equal(new[] { "BAF", "BAJ" }, merkmale.Schadensarten);
        Assert.True(merkmale.HatBogen);
    }

    [Fact]
    public void Ein_Vorschlag_ohne_Positionen_ist_eine_Enthaltung()
    {
        var enthaltung = KostenVorschlag.Enthaltung(EnthaltungsGrund.ZuWenigeFaelle, "nur 1 ähnlicher Fall");

        Assert.True(enthaltung.IstEnthaltung);
        Assert.Empty(enthaltung.Positionen);
        Assert.Equal("nur 1 ähnlicher Fall", enthaltung.GrundText);
    }
}
```

- [ ] **Schritt 2: Test laufen lassen — er muss scheitern**

```bash
dotnet test tests/AuswertungPro.Next.Infrastructure.Tests/AuswertungPro.Next.Infrastructure.Tests.csproj --filter "FullyQualifiedName~KostenanalyseDtosTests"
```

Erwartet: Übersetzungsfehler `CS0246` — die Typen gibt es noch nicht.

- [ ] **Schritt 3: Datenmodell anlegen**

```csharp
using System;
using System.Collections.Generic;
using System.Linq;

namespace AuswertungPro.Next.Application.Kostenanalyse;

/// <summary>Eine Schadensart der Haltung mit Anzahl und Streckenkennzeichen.</summary>
public sealed record SchadensMerkmal(string Hauptcode, int Anzahl, bool HatStrecke);

/// <summary>
/// Die Frage eines Falls: Was zeichnet diese Haltung aus?
/// Bewusst schmal — jedes weitere Merkmal muss sich in einer Messung beweisen.
/// </summary>
public sealed record KostenfallMerkmale
{
    public int? DnMm { get; init; }
    public double LaengeM { get; init; }
    public int BogenAnzahl { get; init; }

    /// <summary>Seitliche Anschluesse (BCA). Kein Schaden, aber Mengentreiber.</summary>
    public int AnschlussAnzahl { get; init; }

    public IReadOnlyList<SchadensMerkmal> Schaeden { get; init; } = [];

    public IReadOnlyList<string> Schadensarten =>
        Schaeden.Select(s => s.Hauptcode).OrderBy(c => c, StringComparer.Ordinal).ToList();

    public bool HatBogen => BogenAnzahl > 0;
}

/// <summary>Eine Position des Massnahmenpakets — Menge ohne Preis.</summary>
public sealed record MassnahmePosition(string ItemKey, decimal Menge, string Einheit);

/// <summary>Woher ein Fall stammt — entscheidet, ob er gemessen werden darf.</summary>
public enum KostenfallHerkunft
{
    /// <summary>Der Vorschlag war verdeckt. Zaehlt zum Lernen UND zur Messung.</summary>
    Unbeeinflusst = 0,

    /// <summary>Der Vorschlag war vorher sichtbar. Zaehlt nur zum Lernen.</summary>
    VorschlagGesehen = 1
}

/// <summary>Ein gelernter Fall: Merkmale und das vom Menschen bestaetigte Paket.</summary>
public sealed record Kostenfall
{
    public string Haltung { get; init; } = "";
    public string Projekt { get; init; } = "";
    public DateTime ErfasstUtc { get; init; }
    public KostenfallHerkunft Herkunft { get; init; }
    public KostenfallMerkmale Merkmale { get; init; } = new();
    public IReadOnlyList<MassnahmePosition> Positionen { get; init; } = [];
}

/// <summary>Warum kein Vorschlag moeglich war.</summary>
public enum EnthaltungsGrund
{
    Kein = 0,
    ZuWenigeFaelle,
    DurchmesserUnbekannt,
    BogenNichtGelernt,
    NachbarnUneinig
}

/// <summary>Das Ergebnis fuer eine Haltung — entweder Positionen oder ein Grund.</summary>
public sealed record KostenVorschlag
{
    public IReadOnlyList<MassnahmePosition> Positionen { get; init; } = [];
    public int HerangezogeneFaelle { get; init; }
    public EnthaltungsGrund Grund { get; init; }
    public string GrundText { get; init; } = "";

    public bool IstEnthaltung => Grund != EnthaltungsGrund.Kein;

    public static KostenVorschlag Enthaltung(EnthaltungsGrund grund, string text)
        => new() { Grund = grund, GrundText = text };
}
```

- [ ] **Schritt 4: Test laufen lassen — er muss bestehen**

```bash
dotnet test tests/AuswertungPro.Next.Infrastructure.Tests/AuswertungPro.Next.Infrastructure.Tests.csproj --filter "FullyQualifiedName~KostenanalyseDtosTests"
```

Erwartet: 2 bestanden.

- [ ] **Schritt 5: Einchecken**

```bash
git add src/AuswertungPro.Next.Application/Kostenanalyse/KostenanalyseDtos.cs tests/AuswertungPro.Next.Infrastructure.Tests/Kostenanalyse/KostenanalyseDtosTests.cs
git commit -m "feat(kostenanalyse): Datenmodell fuer Faelle und Vorschlaege"
```

---

## Aufgabe 2: Merkmale aus der Haltung lesen

**Dateien:**
- Anlegen: `src/AuswertungPro.Next.Application/Kostenanalyse/KostenfallMerkmalLeser.cs`
- Test: `tests/AuswertungPro.Next.Infrastructure.Tests/Kostenanalyse/KostenfallMerkmalLeserTests.cs`

**Schnittstellen:**
- Verbraucht: `KostenfallMerkmale`, `SchadensMerkmal` (Aufgabe 1)
- Liefert: `KostenfallMerkmalLeser.Lies(HaltungRecord record)` → `KostenfallMerkmale`

- [ ] **Schritt 1: Fehlschlagenden Test schreiben**

```csharp
using AuswertungPro.Next.Application.Kostenanalyse;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.Kostenanalyse;

public sealed class KostenfallMerkmalLeserTests
{
    private static HaltungRecord Haltung(string dn, string laenge, params ProtocolEntry[] eintraege)
    {
        var record = new HaltungRecord();
        record.SetFieldValue("DN_mm", dn, FieldSource.Manual, userEdited: false);
        record.SetFieldValue("Haltungslaenge_m", laenge, FieldSource.Manual, userEdited: false);
        record.Protocol = new ProtocolDocument
        {
            Current = new ProtocolRevision { Entries = [.. eintraege] }
        };
        return record;
    }

    private static ProtocolEntry E(string code, bool strecke = false, bool geloescht = false)
        => new() { Code = code, IsStreckenschaden = strecke, IsDeleted = geloescht };

    [Fact]
    public void Liest_Durchmesser_und_Laenge()
    {
        var merkmale = KostenfallMerkmalLeser.Lies(Haltung("300", "42.5", E("BAF01")));

        Assert.Equal(300, merkmale.DnMm);
        Assert.Equal(42.5, merkmale.LaengeM);
    }

    [Fact]
    public void Fasst_Schaeden_auf_den_Hauptcode_zusammen_und_zaehlt_sie()
    {
        var merkmale = KostenfallMerkmalLeser.Lies(
            Haltung("300", "40", E("BAF01"), E("BAFCE"), E("BAJ02")));

        Assert.Equal(new[] { "BAF", "BAJ" }, merkmale.Schadensarten);
        Assert.Equal(2, Assert.Single(merkmale.Schaeden, s => s.Hauptcode == "BAF").Anzahl);
    }

    [Fact]
    public void Bauteile_sind_keine_Schaeden()
    {
        var merkmale = KostenfallMerkmalLeser.Lies(
            Haltung("300", "40", E("BCD"), E("BCE"), E("BDA"), E("000M"), E("BAF01")));

        Assert.Equal(new[] { "BAF" }, merkmale.Schadensarten);
    }

    [Fact]
    public void Anschluesse_sind_ein_eigenes_Merkmal_kein_Schaden()
    {
        var merkmale = KostenfallMerkmalLeser.Lies(
            Haltung("300", "40", E("BCAEA"), E("BCAAB"), E("BAF01")));

        Assert.Equal(2, merkmale.AnschlussAnzahl);
        Assert.DoesNotContain("BCA", merkmale.Schadensarten);
    }

    [Fact]
    public void Boegen_werden_gezaehlt()
    {
        var merkmale = KostenfallMerkmalLeser.Lies(
            Haltung("300", "40", E("BCCAA"), E("BCCBB"), E("BAF01")));

        Assert.Equal(2, merkmale.BogenAnzahl);
        Assert.True(merkmale.HatBogen);
    }

    [Fact]
    public void Streckenschaeden_werden_gekennzeichnet()
    {
        var merkmale = KostenfallMerkmalLeser.Lies(
            Haltung("300", "40", E("BAF01", strecke: true), E("BAJ02")));

        Assert.True(Assert.Single(merkmale.Schaeden, s => s.Hauptcode == "BAF").HatStrecke);
        Assert.False(Assert.Single(merkmale.Schaeden, s => s.Hauptcode == "BAJ").HatStrecke);
    }

    [Fact]
    public void Geloeschte_Eintraege_zaehlen_nicht()
    {
        var merkmale = KostenfallMerkmalLeser.Lies(
            Haltung("300", "40", E("BAF01"), E("BAB01", geloescht: true)));

        Assert.Equal(new[] { "BAF" }, merkmale.Schadensarten);
    }

    [Fact]
    public void Komma_als_Dezimaltrenner_wird_gelesen()
    {
        // Auf de-DE wuerde "42,5" sonst still zu 425 werden.
        Assert.Equal(42.5, KostenfallMerkmalLeser.Lies(Haltung("300", "42,5", E("BAF01"))).LaengeM);
    }
}
```

- [ ] **Schritt 2: Test laufen lassen — er muss scheitern**

```bash
dotnet test tests/AuswertungPro.Next.Infrastructure.Tests/AuswertungPro.Next.Infrastructure.Tests.csproj --filter "FullyQualifiedName~KostenfallMerkmalLeserTests"
```

Erwartet: `CS0103` — `KostenfallMerkmalLeser` existiert nicht.

- [ ] **Schritt 3: Leser umsetzen**

```csharp
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.Kostenanalyse;

/// <summary>
/// Liest die Merkmale einer Haltung: Schadensarten mit Anzahl, Durchmesser, Laenge,
/// Boegen und seitliche Anschluesse.
///
/// Bauteile sind keine Schaeden: BCD (Rohranfang), BCE (Rohrende), BDA und 000M kommen
/// in praktisch jeder Haltung vor und wuerden jede Aehnlichkeit verwaessern. BCA
/// (seitlicher Anschluss) ist ebenfalls kein Schaden, aber ein Mengentreiber und wird
/// darum getrennt gezaehlt.
/// </summary>
public static class KostenfallMerkmalLeser
{
    private static readonly HashSet<string> Bauteile =
        new(StringComparer.OrdinalIgnoreCase) { "BCD", "BCE", "BDA", "000M" };

    private const string AnschlussCode = "BCA";
    private const string BogenCode = "BCC";

    public static KostenfallMerkmale Lies(HaltungRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        var schaeden = new Dictionary<string, (int Anzahl, bool Strecke)>(StringComparer.OrdinalIgnoreCase);
        var anschluesse = 0;
        var boegen = 0;

        foreach (var eintrag in record.Protocol?.Current?.Entries ?? [])
        {
            if (eintrag.IsDeleted)
                continue;

            var code = (eintrag.Code ?? string.Empty).Trim().ToUpperInvariant();
            if (code.Length < 3)
                continue;

            var hauptcode = code[..3];

            if (code.StartsWith(BogenCode, StringComparison.Ordinal))
            {
                boegen++;
                continue;
            }

            if (hauptcode == AnschlussCode)
            {
                anschluesse++;
                continue;
            }

            if (Bauteile.Contains(hauptcode) || Bauteile.Contains(code))
                continue;

            var vorher = schaeden.TryGetValue(hauptcode, out var wert) ? wert : (0, false);
            schaeden[hauptcode] = (vorher.Item1 + 1, vorher.Item2 || eintrag.IsStreckenschaden);
        }

        return new KostenfallMerkmale
        {
            DnMm = LiesGanzzahl(record.GetFieldValue(FieldKeys.NominalDiameterMm)),
            LaengeM = LiesZahl(record.GetFieldValue(FieldKeys.HoldingLengthMeters)) ?? 0d,
            BogenAnzahl = boegen,
            AnschlussAnzahl = anschluesse,
            Schaeden = schaeden
                .OrderBy(kv => kv.Key, StringComparer.Ordinal)
                .Select(kv => new SchadensMerkmal(kv.Key, kv.Value.Anzahl, kv.Value.Strecke))
                .ToList()
        };
    }

    private static int? LiesGanzzahl(string? text)
        => int.TryParse((text ?? "").Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var wert)
            ? wert
            : null;

    private static double? LiesZahl(string? text)
    {
        // Punkt und Komma gleich behandeln — nie ueber CurrentCulture.
        var roh = (text ?? "").Trim().Replace(',', '.');
        return double.TryParse(roh, NumberStyles.Float, CultureInfo.InvariantCulture, out var wert)
            ? wert
            : null;
    }
}
```

- [ ] **Schritt 4: Test laufen lassen — er muss bestehen**

```bash
dotnet test tests/AuswertungPro.Next.Infrastructure.Tests/AuswertungPro.Next.Infrastructure.Tests.csproj --filter "FullyQualifiedName~KostenfallMerkmalLeserTests"
```

Erwartet: 8 bestanden.

- [ ] **Schritt 5: Einchecken**

```bash
git add src/AuswertungPro.Next.Application/Kostenanalyse/KostenfallMerkmalLeser.cs tests/AuswertungPro.Next.Infrastructure.Tests/Kostenanalyse/KostenfallMerkmalLeserTests.cs
git commit -m "feat(kostenanalyse): Merkmale einer Haltung lesen"
```

---

## Aufgabe 3: Massnahmenpaket aus den Kostenzeilen

**Dateien:**
- Anlegen: `src/AuswertungPro.Next.Application/Kostenanalyse/MassnahmePaketLeser.cs`
- Test: `tests/AuswertungPro.Next.Infrastructure.Tests/Kostenanalyse/MassnahmePaketLeserTests.cs`

**Schnittstellen:**
- Verbraucht: `MassnahmePosition` (Aufgabe 1)
- Liefert: `MassnahmePaketLeser.Lies(HoldingCost cost)` → `IReadOnlyList<MassnahmePosition>`

- [ ] **Schritt 1: Fehlschlagenden Test schreiben**

```csharp
using System.Collections.Generic;
using System.Linq;
using AuswertungPro.Next.Application.Kostenanalyse;
using AuswertungPro.Next.Domain.Models;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.Kostenanalyse;

public sealed class MassnahmePaketLeserTests
{
    private static HoldingCost Kosten(params CostLine[] zeilen) => new()
    {
        Holding = "H-1",
        Measures = [new MeasureCost { MeasureId = "M", MeasureName = "Massnahme", Lines = [.. zeilen] }]
    };

    private static CostLine Z(string key, decimal menge, string einheit, bool gewaehlt = true)
        => new() { ItemKey = key, Text = key, Qty = menge, Unit = einheit, UnitPrice = 100m, Selected = gewaehlt };

    [Fact]
    public void Uebernimmt_ItemKey_Menge_und_Einheit()
    {
        var paket = MassnahmePaketLeser.Lies(Kosten(Z("SCHLAUCHLINER_GFK", 42.5m, "m")));

        var position = Assert.Single(paket);
        Assert.Equal("SCHLAUCHLINER_GFK", position.ItemKey);
        Assert.Equal(42.5m, position.Menge);
        Assert.Equal("m", position.Einheit);
    }

    [Fact]
    public void Nicht_gewaehlte_Zeilen_bleiben_draussen()
    {
        var paket = MassnahmePaketLeser.Lies(Kosten(
            Z("SCHLAUCHLINER_GFK", 40m, "m"),
            Z("SPUELEN", 40m, "m", gewaehlt: false)));

        Assert.Equal("SCHLAUCHLINER_GFK", Assert.Single(paket).ItemKey);
    }

    [Fact]
    public void Gleiche_Position_mehrfach_wird_zusammengezaehlt()
    {
        var paket = MassnahmePaketLeser.Lies(Kosten(
            Z("MANSCHETTE_EDELSTAHL", 2m, "Stk"),
            Z("MANSCHETTE_EDELSTAHL", 3m, "Stk")));

        Assert.Equal(5m, Assert.Single(paket).Menge);
    }

    [Fact]
    public void Positionen_ohne_Menge_zaehlen_nicht()
    {
        Assert.Empty(MassnahmePaketLeser.Lies(Kosten(Z("SCHLAUCHLINER_GFK", 0m, "m"))));
    }

    [Fact]
    public void Ohne_ItemKey_dient_der_Text_als_Schluessel()
    {
        var zeile = new CostLine { ItemKey = "", Text = "Sonderposition", Qty = 1m, Unit = "pl", Selected = true };

        Assert.Equal("Sonderposition", Assert.Single(MassnahmePaketLeser.Lies(Kosten(zeile))).ItemKey);
    }

    [Fact]
    public void Die_Reihenfolge_ist_stabil()
    {
        var paket = MassnahmePaketLeser.Lies(Kosten(
            Z("MANSCHETTE_EDELSTAHL", 1m, "Stk"),
            Z("SCHLAUCHLINER_GFK", 1m, "m")));

        Assert.Equal(["MANSCHETTE_EDELSTAHL", "SCHLAUCHLINER_GFK"], paket.Select(p => p.ItemKey));
    }
}
```

- [ ] **Schritt 2: Test laufen lassen — er muss scheitern**

```bash
dotnet test tests/AuswertungPro.Next.Infrastructure.Tests/AuswertungPro.Next.Infrastructure.Tests.csproj --filter "FullyQualifiedName~MassnahmePaketLeserTests"
```

Erwartet: `CS0103` — `MassnahmePaketLeser` existiert nicht.

- [ ] **Schritt 3: Leser umsetzen**

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.Kostenanalyse;

/// <summary>
/// Liest das Massnahmenpaket einer Haltung aus ihren Kostenzeilen — nur Mengen, nie Preise.
/// Dadurch bleibt ein Fall von heute auch nach einer Preisrunde gueltig.
/// Es zaehlen nur ausgewaehlte Zeilen, dieselbe Regel wie in der Kostenzusammenstellung.
/// </summary>
public static class MassnahmePaketLeser
{
    public static IReadOnlyList<MassnahmePosition> Lies(HoldingCost? cost)
    {
        if (cost is null)
            return [];

        var eimer = new Dictionary<string, (decimal Menge, string Einheit, int Reihenfolge)>(
            StringComparer.OrdinalIgnoreCase);
        var lauf = 0;

        foreach (var zeile in cost.Measures.SelectMany(m => m.Lines).Where(l => l.Selected))
        {
            if (zeile.Qty <= 0m)
                continue;

            var key = (zeile.ItemKey ?? "").Trim();
            if (key.Length == 0)
                key = (zeile.Text ?? "").Trim();
            if (key.Length == 0)
                continue;

            var einheit = (zeile.Unit ?? "").Trim();

            if (eimer.TryGetValue(key, out var vorher))
                eimer[key] = (vorher.Menge + zeile.Qty, vorher.Einheit, vorher.Reihenfolge);
            else
                eimer[key] = (zeile.Qty, einheit, lauf++);
        }

        return eimer
            .OrderBy(kv => kv.Value.Reihenfolge)
            .Select(kv => new MassnahmePosition(kv.Key, kv.Value.Menge, kv.Value.Einheit))
            .ToList();
    }
}
```

- [ ] **Schritt 4: Test laufen lassen — er muss bestehen**

```bash
dotnet test tests/AuswertungPro.Next.Infrastructure.Tests/AuswertungPro.Next.Infrastructure.Tests.csproj --filter "FullyQualifiedName~MassnahmePaketLeserTests"
```

Erwartet: 6 bestanden.

- [ ] **Schritt 5: Einchecken**

```bash
git add src/AuswertungPro.Next.Application/Kostenanalyse/MassnahmePaketLeser.cs tests/AuswertungPro.Next.Infrastructure.Tests/Kostenanalyse/MassnahmePaketLeserTests.cs
git commit -m "feat(kostenanalyse): Massnahmenpaket aus Kostenzeilen lesen"
```

---

## Aufgabe 4: Fall zusammensetzen mit Wahrheitsregel

**Dateien:**
- Anlegen: `src/AuswertungPro.Next.Application/Kostenanalyse/KostenfallExtraktor.cs`
- Test: `tests/AuswertungPro.Next.Infrastructure.Tests/Kostenanalyse/KostenfallExtraktorTests.cs`

**Schnittstellen:**
- Verbraucht: `KostenfallMerkmalLeser.Lies`, `MassnahmePaketLeser.Lies`
- Liefert: `KostenfallExtraktor.TryErstellen(record, cost, projekt, herkunft, erfasstUtc, out Kostenfall fall, out string grund)` → `bool`

- [ ] **Schritt 1: Fehlschlagenden Test schreiben**

```csharp
using System;
using AuswertungPro.Next.Application.Kostenanalyse;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.Kostenanalyse;

public sealed class KostenfallExtraktorTests
{
    private static readonly DateTime Zeitpunkt = new(2026, 8, 20, 10, 0, 0, DateTimeKind.Utc);

    private static HaltungRecord Haltung(string name, string dn, string laenge, params string[] codes)
    {
        var record = new HaltungRecord();
        record.SetFieldValue("Haltungsname", name, FieldSource.Manual, userEdited: false);
        record.SetFieldValue("DN_mm", dn, FieldSource.Manual, userEdited: false);
        record.SetFieldValue("Haltungslaenge_m", laenge, FieldSource.Manual, userEdited: false);
        record.Protocol = new ProtocolDocument
        {
            Current = new ProtocolRevision
            {
                Entries = [.. System.Linq.Enumerable.Select(codes, c => new ProtocolEntry { Code = c })]
            }
        };
        return record;
    }

    private static HoldingCost Kosten(decimal menge = 40m) => new()
    {
        Holding = "H-1",
        Measures =
        [
            new MeasureCost
            {
                MeasureId = "M", MeasureName = "Renovierung",
                Lines = [new CostLine { ItemKey = "SCHLAUCHLINER_GFK", Qty = menge, Unit = "m", UnitPrice = 200m, Selected = true }]
            }
        ]
    };

    [Fact]
    public void Erstellt_einen_Fall_aus_Haltung_und_Kosten()
    {
        var ok = KostenfallExtraktor.TryErstellen(
            Haltung("H-1", "300", "40", "BAF01"), Kosten(), "Zone 1.15",
            KostenfallHerkunft.Unbeeinflusst, Zeitpunkt, out var fall, out var grund);

        Assert.True(ok, grund);
        Assert.Equal("H-1", fall!.Haltung);
        Assert.Equal("Zone 1.15", fall.Projekt);
        Assert.Equal(Zeitpunkt, fall.ErfasstUtc);
        Assert.Equal(300, fall.Merkmale.DnMm);
        Assert.Equal("SCHLAUCHLINER_GFK", Assert.Single(fall.Positionen).ItemKey);
    }

    [Fact]
    public void Ohne_Durchmesser_kein_Fall()
    {
        var ok = KostenfallExtraktor.TryErstellen(
            Haltung("H-1", "", "40", "BAF01"), Kosten(), "P",
            KostenfallHerkunft.Unbeeinflusst, Zeitpunkt, out _, out var grund);

        Assert.False(ok);
        Assert.Contains("Durchmesser", grund);
    }

    [Fact]
    public void Ohne_Laenge_kein_Fall()
    {
        var ok = KostenfallExtraktor.TryErstellen(
            Haltung("H-1", "300", "0", "BAF01"), Kosten(), "P",
            KostenfallHerkunft.Unbeeinflusst, Zeitpunkt, out _, out var grund);

        Assert.False(ok);
        Assert.Contains("Laenge", grund);
    }

    [Fact]
    public void Ohne_Schaeden_kein_Fall()
    {
        var ok = KostenfallExtraktor.TryErstellen(
            Haltung("H-1", "300", "40", "BCD", "BCE"), Kosten(), "P",
            KostenfallHerkunft.Unbeeinflusst, Zeitpunkt, out _, out var grund);

        Assert.False(ok);
        Assert.Contains("Schaden", grund);
    }

    [Fact]
    public void Ohne_Massnahmen_kein_Fall()
    {
        var ok = KostenfallExtraktor.TryErstellen(
            Haltung("H-1", "300", "40", "BAF01"), new HoldingCost { Holding = "H-1" }, "P",
            KostenfallHerkunft.Unbeeinflusst, Zeitpunkt, out _, out var grund);

        Assert.False(ok);
        Assert.Contains("Massnahme", grund);
    }

    [Fact]
    public void Ohne_Haltungsnamen_kein_Fall()
    {
        var ok = KostenfallExtraktor.TryErstellen(
            Haltung("", "300", "40", "BAF01"), Kosten(), "P",
            KostenfallHerkunft.Unbeeinflusst, Zeitpunkt, out _, out var grund);

        Assert.False(ok);
        Assert.Contains("Haltungsname", grund);
    }

    [Fact]
    public void Die_Herkunft_wird_uebernommen()
    {
        KostenfallExtraktor.TryErstellen(
            Haltung("H-1", "300", "40", "BAF01"), Kosten(), "P",
            KostenfallHerkunft.VorschlagGesehen, Zeitpunkt, out var fall, out _);

        Assert.Equal(KostenfallHerkunft.VorschlagGesehen, fall!.Herkunft);
    }
}
```

- [ ] **Schritt 2: Test laufen lassen — er muss scheitern**

```bash
dotnet test tests/AuswertungPro.Next.Infrastructure.Tests/AuswertungPro.Next.Infrastructure.Tests.csproj --filter "FullyQualifiedName~KostenfallExtraktorTests"
```

Erwartet: `CS0103` — `KostenfallExtraktor` existiert nicht.

- [ ] **Schritt 3: Extraktor umsetzen**

```csharp
using System;
using System.Diagnostics.CodeAnalysis;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.Kostenanalyse;

/// <summary>
/// Setzt aus einer Haltung und ihrer Kostenzusammenstellung einen Lernfall zusammen.
///
/// Die Wahrheitsregel ist bewusst streng: Fehlt Durchmesser, Laenge, ein echter Schaden
/// oder das Massnahmenpaket, entsteht KEIN Fall. Ein halber Fall wuerde spaeter als
/// vollwertiges Vorbild herangezogen und still falsche Mengen erzeugen.
/// </summary>
public static class KostenfallExtraktor
{
    public static bool TryErstellen(
        HaltungRecord record,
        HoldingCost? cost,
        string projekt,
        KostenfallHerkunft herkunft,
        DateTime erfasstUtc,
        [NotNullWhen(true)] out Kostenfall? fall,
        out string grund)
    {
        ArgumentNullException.ThrowIfNull(record);
        fall = null;

        var name = (record.GetFieldValue(FieldKeys.HoldingName) ?? "").Trim();
        if (name.Length == 0)
        {
            grund = "Kein Haltungsname hinterlegt.";
            return false;
        }

        var merkmale = KostenfallMerkmalLeser.Lies(record);

        if (merkmale.DnMm is not > 0)
        {
            grund = "Kein gueltiger Durchmesser (DN_mm).";
            return false;
        }

        if (merkmale.LaengeM <= 0d)
        {
            grund = "Keine gueltige Laenge (Haltungslaenge_m).";
            return false;
        }

        if (merkmale.Schaeden.Count == 0)
        {
            grund = "Kein einziger Schaden im Protokoll (Bauteile zaehlen nicht).";
            return false;
        }

        var positionen = MassnahmePaketLeser.Lies(cost);
        if (positionen.Count == 0)
        {
            grund = "Keine ausgewaehlte Massnahme in der Kostenzusammenstellung.";
            return false;
        }

        fall = new Kostenfall
        {
            Haltung = name,
            Projekt = projekt ?? "",
            ErfasstUtc = erfasstUtc,
            Herkunft = herkunft,
            Merkmale = merkmale,
            Positionen = positionen
        };
        grund = "";
        return true;
    }
}
```

- [ ] **Schritt 4: Test laufen lassen — er muss bestehen**

```bash
dotnet test tests/AuswertungPro.Next.Infrastructure.Tests/AuswertungPro.Next.Infrastructure.Tests.csproj --filter "FullyQualifiedName~KostenfallExtraktorTests"
```

Erwartet: 7 bestanden.

- [ ] **Schritt 5: Einchecken**

```bash
git add src/AuswertungPro.Next.Application/Kostenanalyse/KostenfallExtraktor.cs tests/AuswertungPro.Next.Infrastructure.Tests/Kostenanalyse/KostenfallExtraktorTests.cs
git commit -m "feat(kostenanalyse): Fall mit strenger Wahrheitsregel zusammensetzen"
```

---

## Aufgabe 5: Fallspeicher

**Dateien:**
- Anlegen: `src/AuswertungPro.Next.Application/Kostenanalyse/IKostenfallStore.cs`
- Anlegen: `src/AuswertungPro.Next.Infrastructure/Kostenanalyse/KostenfallFileStore.cs`
- Test: `tests/AuswertungPro.Next.Infrastructure.Tests/Kostenanalyse/KostenfallFileStoreTests.cs`

**Schnittstellen:**
- Verbraucht: `Kostenfall` (Aufgabe 1)
- Liefert: `IKostenfallStore.Lade()` → `IReadOnlyList<Kostenfall>`, `IKostenfallStore.Speichere(faelle)`,
  `KostenfallFileStore(string wurzel)`

- [ ] **Schritt 1: Fehlschlagenden Test schreiben**

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using AuswertungPro.Next.Application.Kostenanalyse;
using AuswertungPro.Next.Infrastructure.Kostenanalyse;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.Kostenanalyse;

public sealed class KostenfallFileStoreTests : IDisposable
{
    private readonly string _wurzel = Directory.CreateTempSubdirectory().FullName;

    public void Dispose()
    {
        try { Directory.Delete(_wurzel, recursive: true); } catch { }
    }

    private static Kostenfall Fall(string haltung) => new()
    {
        Haltung = haltung,
        Projekt = "Zone 1.15",
        ErfasstUtc = new DateTime(2026, 8, 20, 10, 0, 0, DateTimeKind.Utc),
        Herkunft = KostenfallHerkunft.Unbeeinflusst,
        Merkmale = new KostenfallMerkmale
        {
            DnMm = 300,
            LaengeM = 42.5,
            BogenAnzahl = 1,
            AnschlussAnzahl = 2,
            Schaeden = [new SchadensMerkmal("BAF", 2, true)]
        },
        Positionen = [new MassnahmePosition("SCHLAUCHLINER_GFK", 42.5m, "m")]
    };

    [Fact]
    public void Ein_leerer_Ordner_liefert_keine_Faelle()
    {
        Assert.Empty(new KostenfallFileStore(_wurzel).Lade());
    }

    [Fact]
    public void Gespeicherte_Faelle_kommen_unveraendert_zurueck()
    {
        var store = new KostenfallFileStore(_wurzel);
        store.Speichere([Fall("H-1"), Fall("H-2")]);

        var geladen = new KostenfallFileStore(_wurzel).Lade();

        Assert.Equal(2, geladen.Count);
        var erster = Assert.Single(geladen, f => f.Haltung == "H-1");
        Assert.Equal(300, erster.Merkmale.DnMm);
        Assert.Equal(42.5, erster.Merkmale.LaengeM);
        Assert.Equal(1, erster.Merkmale.BogenAnzahl);
        Assert.Equal(2, erster.Merkmale.AnschlussAnzahl);
        Assert.Equal("BAF", Assert.Single(erster.Merkmale.Schaeden).Hauptcode);
        Assert.Equal(42.5m, Assert.Single(erster.Positionen).Menge);
    }

    [Fact]
    public void Eine_beschaedigte_Datei_wird_gemeldet_und_nicht_ueberschrieben()
    {
        var pfad = Path.Combine(_wurzel, "kostenanalyse", "kostenfaelle_v1.json");
        Directory.CreateDirectory(Path.GetDirectoryName(pfad)!);
        File.WriteAllText(pfad, "{ kaputt");

        var store = new KostenfallFileStore(_wurzel);

        Assert.Throws<InvalidDataException>(() => store.Lade());
        Assert.Equal("{ kaputt", File.ReadAllText(pfad));
    }
}
```

- [ ] **Schritt 2: Test laufen lassen — er muss scheitern**

```bash
dotnet test tests/AuswertungPro.Next.Infrastructure.Tests/AuswertungPro.Next.Infrastructure.Tests.csproj --filter "FullyQualifiedName~KostenfallFileStoreTests"
```

Erwartet: `CS0246` — `IKostenfallStore` und `KostenfallFileStore` existieren nicht.

- [ ] **Schritt 3: Vertrag anlegen**

```csharp
using System.Collections.Generic;

namespace AuswertungPro.Next.Application.Kostenanalyse;

/// <summary>Lesen und Schreiben der gelernten Faelle.</summary>
public interface IKostenfallStore
{
    /// <summary>Alle Faelle. Fehlende Datei = leer; beschaedigte Datei = Ausnahme.</summary>
    IReadOnlyList<Kostenfall> Lade();

    /// <summary>Ersetzt den Bestand vollstaendig und atomar.</summary>
    void Speichere(IReadOnlyList<Kostenfall> faelle);
}
```

- [ ] **Schritt 4: Dateispeicher anlegen**

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using AuswertungPro.Next.Application.Kostenanalyse;

namespace AuswertungPro.Next.Infrastructure.Kostenanalyse;

/// <summary>
/// Speichert die Faelle als eine JSON-Datei unter &lt;Wurzel&gt;\kostenanalyse\.
///
/// Unterschied zwischen "noch nie gelaufen" und "kaputt" wie bei den uebrigen
/// KI-Dateien des Projekts: Eine fehlende Datei ist leer, eine unlesbare bricht ab und
/// wird NICHT ueberschrieben.
/// </summary>
public sealed class KostenfallFileStore : IKostenfallStore
{
    private const string OrdnerName = "kostenanalyse";
    private const string DateiName = "kostenfaelle_v1.json";

    private static readonly JsonSerializerOptions Optionen = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly string _pfad;

    public KostenfallFileStore(string wurzel)
    {
        if (string.IsNullOrWhiteSpace(wurzel))
            throw new ArgumentException("Wurzel fehlt.", nameof(wurzel));

        _pfad = Path.Combine(wurzel, OrdnerName, DateiName);
    }

    public IReadOnlyList<Kostenfall> Lade()
    {
        if (!File.Exists(_pfad))
            return [];

        try
        {
            var inhalt = File.ReadAllText(_pfad);
            return JsonSerializer.Deserialize<List<Kostenfall>>(inhalt, Optionen) ?? [];
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException(
                $"Die Falldatei ist beschaedigt und wurde nicht veraendert: {_pfad}", ex);
        }
    }

    public void Speichere(IReadOnlyList<Kostenfall> faelle)
    {
        ArgumentNullException.ThrowIfNull(faelle);

        var ordner = Path.GetDirectoryName(_pfad)!;
        Directory.CreateDirectory(ordner);

        // Erst danebenschreiben, dann umlegen — ein Absturz darf den Bestand nie halbieren.
        var temp = _pfad + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(faelle, Optionen));
        File.Move(temp, _pfad, overwrite: true);
    }
}
```

- [ ] **Schritt 5: Test laufen lassen — er muss bestehen**

```bash
dotnet test tests/AuswertungPro.Next.Infrastructure.Tests/AuswertungPro.Next.Infrastructure.Tests.csproj --filter "FullyQualifiedName~KostenfallFileStoreTests"
```

Erwartet: 3 bestanden.

- [ ] **Schritt 6: Einchecken**

```bash
git add src/AuswertungPro.Next.Application/Kostenanalyse/IKostenfallStore.cs src/AuswertungPro.Next.Infrastructure/Kostenanalyse/KostenfallFileStore.cs tests/AuswertungPro.Next.Infrastructure.Tests/Kostenanalyse/KostenfallFileStoreTests.cs
git commit -m "feat(kostenanalyse): Fallspeicher mit atomarem Schreiben"
```

---

## Aufgabe 6: Ähnlichkeit und Nachbarn

**Dateien:**
- Anlegen: `src/AuswertungPro.Next.Application/Kostenanalyse/KostenfallAehnlichkeit.cs`
- Test: `tests/AuswertungPro.Next.Infrastructure.Tests/Kostenanalyse/KostenfallAehnlichkeitTests.cs`

**Schnittstellen:**
- Verbraucht: `KostenfallMerkmale`, `Kostenfall`
- Liefert:
  - `KostenfallAehnlichkeit.DnStufen` (`IReadOnlyList<int>`)
  - `KostenfallAehnlichkeit.DnStufenAbstand(int a, int b)` → `int?`
  - `KostenfallAehnlichkeit.SchadensAehnlichkeit(a, b)` → `double`
  - `KostenfallAehnlichkeit.FindeNachbarn(ziel, faelle, maximal)` → `IReadOnlyList<Kostenfall>`

- [ ] **Schritt 1: Fehlschlagenden Test schreiben**

```csharp
using System.Collections.Generic;
using System.Linq;
using AuswertungPro.Next.Application.Kostenanalyse;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.Kostenanalyse;

public sealed class KostenfallAehnlichkeitTests
{
    private static KostenfallMerkmale M(int dn, double laenge, params string[] arten) => new()
    {
        DnMm = dn,
        LaengeM = laenge,
        Schaeden = [.. arten.Select(a => new SchadensMerkmal(a, 1, false))]
    };

    private static Kostenfall F(string name, int dn, params string[] arten) => new()
    {
        Haltung = name,
        Merkmale = M(dn, 40, arten),
        Positionen = [new MassnahmePosition("SCHLAUCHLINER_GFK", 40m, "m")]
    };

    [Fact]
    public void Gleiche_Schadensarten_ergeben_volle_Aehnlichkeit()
    {
        Assert.Equal(1.0, KostenfallAehnlichkeit.SchadensAehnlichkeit(M(300, 40, "BAF", "BAJ"), M(300, 40, "BAJ", "BAF")));
    }

    [Fact]
    public void Teilweise_Ueberschneidung_wird_anteilig_bewertet()
    {
        // gemeinsam {BAF, BAJ} = 2, insgesamt {BAF, BAJ, BBC} = 3
        var wert = KostenfallAehnlichkeit.SchadensAehnlichkeit(M(300, 40, "BAF", "BAJ"), M(300, 40, "BAF", "BAJ", "BBC"));

        Assert.Equal(2d / 3d, wert, 5);
    }

    [Fact]
    public void Ohne_Ueberschneidung_null()
    {
        Assert.Equal(0d, KostenfallAehnlichkeit.SchadensAehnlichkeit(M(300, 40, "BAF"), M(300, 40, "BBC")));
    }

    [Fact]
    public void Der_Durchmesser_Abstand_zaehlt_Katalogstufen()
    {
        Assert.Equal(0, KostenfallAehnlichkeit.DnStufenAbstand(300, 300));
        Assert.Equal(1, KostenfallAehnlichkeit.DnStufenAbstand(250, 300));
        Assert.Equal(2, KostenfallAehnlichkeit.DnStufenAbstand(200, 300));
    }

    [Fact]
    public void Ein_unbekannter_Durchmesser_hat_keinen_Abstand()
    {
        Assert.Null(KostenfallAehnlichkeit.DnStufenAbstand(333, 300));
    }

    [Fact]
    public void Nachbarn_ausserhalb_einer_Durchmesserstufe_fallen_weg()
    {
        var faelle = new[] { F("nah", 250, "BAF"), F("fern", 150, "BAF") };

        var nachbarn = KostenfallAehnlichkeit.FindeNachbarn(M(300, 40, "BAF"), faelle, 7);

        Assert.Equal("nah", Assert.Single(nachbarn).Haltung);
    }

    [Fact]
    public void Die_aehnlichsten_Faelle_stehen_vorn()
    {
        var faelle = new[]
        {
            F("halb", 300, "BAF", "BBC", "BAB"),
            F("genau", 300, "BAF", "BAJ"),
            F("teil", 300, "BAF")
        };

        var nachbarn = KostenfallAehnlichkeit.FindeNachbarn(M(300, 40, "BAF", "BAJ"), faelle, 7);

        Assert.Equal("genau", nachbarn[0].Haltung);
    }

    [Fact]
    public void Mehr_als_das_Maximum_kommt_nicht_zurueck()
    {
        var faelle = Enumerable.Range(0, 10).Select(i => F($"H{i}", 300, "BAF")).ToList();

        Assert.Equal(7, KostenfallAehnlichkeit.FindeNachbarn(M(300, 40, "BAF"), faelle, 7).Count);
    }

    [Fact]
    public void Faelle_ohne_gemeinsame_Schadensart_zaehlen_nicht_als_Nachbarn()
    {
        var faelle = new[] { F("anders", 300, "BBC") };

        Assert.Empty(KostenfallAehnlichkeit.FindeNachbarn(M(300, 40, "BAF"), faelle, 7));
    }
}
```

- [ ] **Schritt 2: Test laufen lassen — er muss scheitern**

```bash
dotnet test tests/AuswertungPro.Next.Infrastructure.Tests/AuswertungPro.Next.Infrastructure.Tests.csproj --filter "FullyQualifiedName~KostenfallAehnlichkeitTests"
```

Erwartet: `CS0103` — `KostenfallAehnlichkeit` existiert nicht.

- [ ] **Schritt 3: Ähnlichkeit umsetzen**

```csharp
using System;
using System.Collections.Generic;
using System.Linq;

namespace AuswertungPro.Next.Application.Kostenanalyse;

/// <summary>
/// Findet zu einer Haltung die aehnlichsten gelernten Faelle.
///
/// Zuerst harte Grenzen (Durchmesser hoechstens eine Katalogstufe entfernt, mindestens
/// eine gemeinsame Schadensart), danach Rangfolge nach Schadensaehnlichkeit. Der
/// Durchmesser ist bewusst ein Filter und kein Gewicht: Eine DN 150 und eine DN 600
/// sind fachlich nicht vergleichbar, egal wie gut die Schaeden passen.
/// </summary>
public static class KostenfallAehnlichkeit
{
    /// <summary>Uebliche Nennweiten in aufsteigender Reihenfolge.</summary>
    public static readonly IReadOnlyList<int> DnStufen =
        [100, 125, 150, 185, 200, 250, 300, 350, 400, 500, 600, 700, 800, 900, 1000];

    /// <summary>Abstand in Katalogstufen; null, wenn eine Weite nicht im Katalog steht.</summary>
    public static int? DnStufenAbstand(int a, int b)
    {
        var indexA = DnStufen.ToList().IndexOf(a);
        var indexB = DnStufen.ToList().IndexOf(b);
        if (indexA < 0 || indexB < 0)
            return null;

        return Math.Abs(indexA - indexB);
    }

    /// <summary>Gemeinsame Schadensarten geteilt durch alle vorkommenden.</summary>
    public static double SchadensAehnlichkeit(KostenfallMerkmale a, KostenfallMerkmale b)
    {
        var mengeA = new HashSet<string>(a.Schadensarten, StringComparer.OrdinalIgnoreCase);
        var mengeB = new HashSet<string>(b.Schadensarten, StringComparer.OrdinalIgnoreCase);
        if (mengeA.Count == 0 || mengeB.Count == 0)
            return 0d;

        var gemeinsam = mengeA.Intersect(mengeB, StringComparer.OrdinalIgnoreCase).Count();
        var insgesamt = mengeA.Union(mengeB, StringComparer.OrdinalIgnoreCase).Count();
        return insgesamt == 0 ? 0d : (double)gemeinsam / insgesamt;
    }

    public static IReadOnlyList<Kostenfall> FindeNachbarn(
        KostenfallMerkmale ziel,
        IReadOnlyList<Kostenfall> faelle,
        int maximal)
    {
        ArgumentNullException.ThrowIfNull(ziel);
        ArgumentNullException.ThrowIfNull(faelle);

        if (ziel.DnMm is not > 0)
            return [];

        var kandidaten = new List<(Kostenfall Fall, double Aehnlichkeit, int DnAbstand, int AnzahlAbstand)>();

        foreach (var fall in faelle)
        {
            if (fall.Merkmale.DnMm is not > 0)
                continue;

            var abstand = DnStufenAbstand(fall.Merkmale.DnMm.Value, ziel.DnMm.Value);
            if (abstand is null || abstand > 1)
                continue;

            var aehnlich = SchadensAehnlichkeit(ziel, fall.Merkmale);
            if (aehnlich <= 0d)
                continue;

            var anzahlZiel = ziel.Schaeden.Sum(s => s.Anzahl);
            var anzahlFall = fall.Merkmale.Schaeden.Sum(s => s.Anzahl);
            kandidaten.Add((fall, aehnlich, abstand.Value, Math.Abs(anzahlZiel - anzahlFall)));
        }

        return kandidaten
            .OrderByDescending(k => k.Aehnlichkeit)
            .ThenBy(k => k.AnzahlAbstand)
            .ThenBy(k => k.DnAbstand)
            .ThenBy(k => k.Fall.Haltung, StringComparer.Ordinal) // stabile Reihenfolge
            .Take(maximal)
            .Select(k => k.Fall)
            .ToList();
    }
}
```

- [ ] **Schritt 4: Test laufen lassen — er muss bestehen**

```bash
dotnet test tests/AuswertungPro.Next.Infrastructure.Tests/AuswertungPro.Next.Infrastructure.Tests.csproj --filter "FullyQualifiedName~KostenfallAehnlichkeitTests"
```

Erwartet: 9 bestanden.

- [ ] **Schritt 5: Einchecken**

```bash
git add src/AuswertungPro.Next.Application/Kostenanalyse/KostenfallAehnlichkeit.cs tests/AuswertungPro.Next.Infrastructure.Tests/Kostenanalyse/KostenfallAehnlichkeitTests.cs
git commit -m "feat(kostenanalyse): aehnliche Faelle finden"
```

---

## Aufgabe 7: Mengen aus den Nachbarn rechnen

**Dateien:**
- Anlegen: `src/AuswertungPro.Next.Application/Kostenanalyse/KostenVorschlagRechner.cs`
- Test: `tests/AuswertungPro.Next.Infrastructure.Tests/Kostenanalyse/KostenVorschlagRechnerTests.cs`

**Schnittstellen:**
- Verbraucht: `Kostenfall`, `KostenfallMerkmale`, `MassnahmePosition`
- Liefert: `KostenVorschlagRechner.Rechne(ziel, nachbarn)` → `IReadOnlyList<MassnahmePosition>`

- [ ] **Schritt 1: Fehlschlagenden Test schreiben**

```csharp
using System.Collections.Generic;
using System.Linq;
using AuswertungPro.Next.Application.Kostenanalyse;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.Kostenanalyse;

public sealed class KostenVorschlagRechnerTests
{
    private static KostenfallMerkmale Ziel(double laenge) => new()
    {
        DnMm = 300,
        LaengeM = laenge,
        Schaeden = [new SchadensMerkmal("BAF", 1, false)]
    };

    private static Kostenfall Nachbar(string name, double laenge, params MassnahmePosition[] positionen) => new()
    {
        Haltung = name,
        Merkmale = new KostenfallMerkmale
        {
            DnMm = 300,
            LaengeM = laenge,
            Schaeden = [new SchadensMerkmal("BAF", 1, false)]
        },
        Positionen = [.. positionen]
    };

    private static MassnahmePosition P(string key, decimal menge, string einheit)
        => new(key, menge, einheit);

    [Fact]
    public void Meterpositionen_werden_auf_die_Laenge_umgerechnet()
    {
        // Alle Nachbarn linern auf voller Laenge -> das Ziel auch.
        var nachbarn = new[]
        {
            Nachbar("A", 20, P("SCHLAUCHLINER_GFK", 20m, "m")),
            Nachbar("B", 40, P("SCHLAUCHLINER_GFK", 40m, "m")),
            Nachbar("C", 60, P("SCHLAUCHLINER_GFK", 60m, "m"))
        };

        var position = Assert.Single(KostenVorschlagRechner.Rechne(Ziel(50), nachbarn));

        Assert.Equal("SCHLAUCHLINER_GFK", position.ItemKey);
        Assert.Equal(50m, position.Menge);
        Assert.Equal("m", position.Einheit);
    }

    [Fact]
    public void Stueckpositionen_nehmen_den_Median()
    {
        var nachbarn = new[]
        {
            Nachbar("A", 40, P("MANSCHETTE_EDELSTAHL", 1m, "Stk")),
            Nachbar("B", 40, P("MANSCHETTE_EDELSTAHL", 2m, "Stk")),
            Nachbar("C", 40, P("MANSCHETTE_EDELSTAHL", 9m, "Stk"))
        };

        // Der Mittelwert waere 4 — der Ausreisser darf nicht durchschlagen.
        Assert.Equal(2m, Assert.Single(KostenVorschlagRechner.Rechne(Ziel(40), nachbarn)).Menge);
    }

    [Fact]
    public void Eine_Position_ohne_Mehrheit_erscheint_nicht()
    {
        var nachbarn = new[]
        {
            Nachbar("A", 40, P("SCHLAUCHLINER_GFK", 40m, "m")),
            Nachbar("B", 40, P("SCHLAUCHLINER_GFK", 40m, "m")),
            Nachbar("C", 40, P("SCHLAUCHLINER_GFK", 40m, "m"), P("SONDERPOSITION", 1m, "pl"))
        };

        Assert.Equal("SCHLAUCHLINER_GFK", Assert.Single(KostenVorschlagRechner.Rechne(Ziel(40), nachbarn)).ItemKey);
    }

    [Fact]
    public void Genau_die_Haelfte_reicht_nicht()
    {
        var nachbarn = new[]
        {
            Nachbar("A", 40, P("MANSCHETTE_EDELSTAHL", 1m, "Stk")),
            Nachbar("B", 40, P("SCHLAUCHLINER_GFK", 40m, "m"))
        };

        Assert.Empty(KostenVorschlagRechner.Rechne(Ziel(40), nachbarn));
    }

    [Fact]
    public void Ohne_Nachbarn_kommt_nichts()
    {
        Assert.Empty(KostenVorschlagRechner.Rechne(Ziel(40), []));
    }

    [Fact]
    public void Ein_Nachbar_ohne_Laenge_verdirbt_die_Umrechnung_nicht()
    {
        var nachbarn = new[]
        {
            Nachbar("A", 0, P("SCHLAUCHLINER_GFK", 40m, "m")),
            Nachbar("B", 40, P("SCHLAUCHLINER_GFK", 40m, "m")),
            Nachbar("C", 40, P("SCHLAUCHLINER_GFK", 40m, "m"))
        };

        Assert.Equal(40m, Assert.Single(KostenVorschlagRechner.Rechne(Ziel(40), nachbarn)).Menge);
    }

    [Fact]
    public void Stueckmengen_werden_ganzzahlig_gerundet()
    {
        var nachbarn = new[]
        {
            Nachbar("A", 40, P("ANSCHLUSS_EINBINDEN", 2m, "Stk")),
            Nachbar("B", 40, P("ANSCHLUSS_EINBINDEN", 3m, "Stk"))
        };

        // Median von 2 und 3 ist 2.5 -> aufgerundet 3, weil es keine halben Anschluesse gibt.
        Assert.Equal(3m, Assert.Single(KostenVorschlagRechner.Rechne(Ziel(40), nachbarn)).Menge);
    }
}
```

- [ ] **Schritt 2: Test laufen lassen — er muss scheitern**

```bash
dotnet test tests/AuswertungPro.Next.Infrastructure.Tests/AuswertungPro.Next.Infrastructure.Tests.csproj --filter "FullyQualifiedName~KostenVorschlagRechnerTests"
```

Erwartet: `CS0103` — `KostenVorschlagRechner` existiert nicht.

- [ ] **Schritt 3: Rechner umsetzen**

```csharp
using System;
using System.Collections.Generic;
using System.Linq;

namespace AuswertungPro.Next.Application.Kostenanalyse;

/// <summary>
/// Baut aus den Nachbarn ein Massnahmenpaket mit Mengen.
///
/// Zwei bewusste Entscheidungen:
/// - Median statt Mittelwert: Ein einzelner Ausreisser (9 Manschetten in einer Haltung)
///   darf den Vorschlag nicht kippen.
/// - Nur Positionen mit Mehrheit: Aus sieben verschiedenen Paketen entstuende sonst ein
///   Sammelsurium, das so nie jemand bestellt haette.
/// </summary>
public static class KostenVorschlagRechner
{
    /// <summary>Einheiten, die auf die Haltungslaenge umgerechnet werden.</summary>
    private static readonly HashSet<string> Metereinheiten =
        new(StringComparer.OrdinalIgnoreCase) { "m", "lfm", "m1" };

    /// <summary>Einheiten, die als ganze Stuecke gelten.</summary>
    private static readonly HashSet<string> Stueckeinheiten =
        new(StringComparer.OrdinalIgnoreCase) { "stk", "st", "stck", "stueck", "stück" };

    public static IReadOnlyList<MassnahmePosition> Rechne(
        KostenfallMerkmale ziel,
        IReadOnlyList<Kostenfall> nachbarn)
    {
        ArgumentNullException.ThrowIfNull(ziel);
        ArgumentNullException.ThrowIfNull(nachbarn);

        if (nachbarn.Count == 0)
            return [];

        var ergebnis = new List<MassnahmePosition>();
        var reihenfolge = new List<string>();
        var werte = new Dictionary<string, List<decimal>>(StringComparer.OrdinalIgnoreCase);
        var einheiten = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var nachbar in nachbarn)
        {
            foreach (var position in nachbar.Positionen)
            {
                if (!werte.TryGetValue(position.ItemKey, out var liste))
                {
                    liste = [];
                    werte[position.ItemKey] = liste;
                    einheiten[position.ItemKey] = position.Einheit;
                    reihenfolge.Add(position.ItemKey);
                }

                liste.Add(NormalisiereMenge(position, nachbar.Merkmale, ziel, einheiten[position.ItemKey]));
            }
        }

        foreach (var key in reihenfolge)
        {
            var liste = werte[key];

            // Strenge Mehrheit: genau die Haelfte reicht nicht.
            if (liste.Count * 2 <= nachbarn.Count)
                continue;

            var einheit = einheiten[key];
            var menge = Median(liste);

            if (Stueckeinheiten.Contains(einheit))
                menge = Math.Ceiling(menge);
            else
                menge = Math.Round(menge, 2, MidpointRounding.AwayFromZero);

            if (menge <= 0m)
                continue;

            ergebnis.Add(new MassnahmePosition(key, menge, einheit));
        }

        return ergebnis;
    }

    /// <summary>
    /// Meterpositionen werden auf die Ziel-Laenge hochgerechnet. Fehlt beim Nachbarn die
    /// Laenge, wird seine Menge unveraendert uebernommen statt durch null zu teilen.
    /// </summary>
    private static decimal NormalisiereMenge(
        MassnahmePosition position,
        KostenfallMerkmale nachbar,
        KostenfallMerkmale ziel,
        string einheit)
    {
        if (!Metereinheiten.Contains(einheit))
            return position.Menge;

        if (nachbar.LaengeM <= 0d || ziel.LaengeM <= 0d)
            return position.Menge;

        var anteil = position.Menge / (decimal)nachbar.LaengeM;
        return anteil * (decimal)ziel.LaengeM;
    }

    private static decimal Median(List<decimal> werte)
    {
        var sortiert = werte.OrderBy(w => w).ToList();
        var mitte = sortiert.Count / 2;

        return sortiert.Count % 2 == 1
            ? sortiert[mitte]
            : (sortiert[mitte - 1] + sortiert[mitte]) / 2m;
    }
}
```

- [ ] **Schritt 4: Test laufen lassen — er muss bestehen**

```bash
dotnet test tests/AuswertungPro.Next.Infrastructure.Tests/AuswertungPro.Next.Infrastructure.Tests.csproj --filter "FullyQualifiedName~KostenVorschlagRechnerTests"
```

Erwartet: 7 bestanden.

- [ ] **Schritt 5: Einchecken**

```bash
git add src/AuswertungPro.Next.Application/Kostenanalyse/KostenVorschlagRechner.cs tests/AuswertungPro.Next.Infrastructure.Tests/Kostenanalyse/KostenVorschlagRechnerTests.cs
git commit -m "feat(kostenanalyse): Mengen aus Nachbarn ueber den Median rechnen"
```

---

## Aufgabe 8: Enthaltung — wann geschwiegen wird

**Dateien:**
- Anlegen: `src/AuswertungPro.Next.Application/Kostenanalyse/KostenVorschlagPolicy.cs`
- Test: `tests/AuswertungPro.Next.Infrastructure.Tests/Kostenanalyse/KostenVorschlagPolicyTests.cs`

**Schnittstellen:**
- Verbraucht: `KostenfallAehnlichkeit`, `KostenVorschlagRechner`, `KostenVorschlag`
- Liefert:
  - `KostenVorschlagPolicy.MindestNachbarn` = 3, `MaximalNachbarn` = 7, `MindestBogenFaelle` = 10
  - `KostenVorschlagPolicy.Schlage(ziel, faelle)` → `KostenVorschlag`

- [ ] **Schritt 1: Fehlschlagenden Test schreiben**

```csharp
using System.Collections.Generic;
using System.Linq;
using AuswertungPro.Next.Application.Kostenanalyse;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.Kostenanalyse;

public sealed class KostenVorschlagPolicyTests
{
    private static KostenfallMerkmale Ziel(int dn = 300, int boegen = 0) => new()
    {
        DnMm = dn,
        LaengeM = 40,
        BogenAnzahl = boegen,
        Schaeden = [new SchadensMerkmal("BAF", 1, false)]
    };

    private static Kostenfall Fall(string name, int dn = 300, int boegen = 0) => new()
    {
        Haltung = name,
        Merkmale = new KostenfallMerkmale
        {
            DnMm = dn,
            LaengeM = 40,
            BogenAnzahl = boegen,
            Schaeden = [new SchadensMerkmal("BAF", 1, false)]
        },
        Positionen = [new MassnahmePosition("SCHLAUCHLINER_GFK", 40m, "m")]
    };

    private static IReadOnlyList<Kostenfall> Faelle(int anzahl, int dn = 300, int boegen = 0)
        => Enumerable.Range(0, anzahl).Select(i => Fall($"H{i:D2}", dn, boegen)).ToList();

    [Fact]
    public void Genug_aehnliche_Faelle_ergeben_einen_Vorschlag()
    {
        var vorschlag = KostenVorschlagPolicy.Schlage(Ziel(), Faelle(5));

        Assert.False(vorschlag.IstEnthaltung);
        Assert.Equal(5, vorschlag.HerangezogeneFaelle);
        Assert.Equal("SCHLAUCHLINER_GFK", Assert.Single(vorschlag.Positionen).ItemKey);
    }

    [Fact]
    public void Weniger_als_drei_Faelle_ergeben_eine_Enthaltung()
    {
        var vorschlag = KostenVorschlagPolicy.Schlage(Ziel(), Faelle(2));

        Assert.True(vorschlag.IstEnthaltung);
        Assert.Equal(EnthaltungsGrund.ZuWenigeFaelle, vorschlag.Grund);
        Assert.Contains("2", vorschlag.GrundText);
    }

    [Fact]
    public void Ein_unbekannter_Durchmesser_ergibt_eine_Enthaltung()
    {
        var vorschlag = KostenVorschlagPolicy.Schlage(Ziel(dn: 333), Faelle(5));

        Assert.Equal(EnthaltungsGrund.DurchmesserUnbekannt, vorschlag.Grund);
    }

    [Fact]
    public void Ein_Bogen_ohne_gelernte_Bogenfaelle_ergibt_eine_Enthaltung()
    {
        var vorschlag = KostenVorschlagPolicy.Schlage(Ziel(boegen: 1), Faelle(5));

        Assert.Equal(EnthaltungsGrund.BogenNichtGelernt, vorschlag.Grund);
        Assert.Contains("Bogen", vorschlag.GrundText);
    }

    [Fact]
    public void Mit_genug_Bogenfaellen_wird_wieder_vorgeschlagen()
    {
        var vorschlag = KostenVorschlagPolicy.Schlage(Ziel(boegen: 1), Faelle(12, boegen: 1));

        Assert.False(vorschlag.IstEnthaltung);
    }

    [Fact]
    public void Uneinige_Nachbarn_ergeben_eine_Enthaltung()
    {
        // Drei Nachbarn, drei verschiedene Pakete -> keine Position erreicht die Mehrheit.
        var faelle = new List<Kostenfall>
        {
            Fall("A") with { Positionen = [new MassnahmePosition("A_POS", 1m, "Stk")] },
            Fall("B") with { Positionen = [new MassnahmePosition("B_POS", 1m, "Stk")] },
            Fall("C") with { Positionen = [new MassnahmePosition("C_POS", 1m, "Stk")] }
        };

        var vorschlag = KostenVorschlagPolicy.Schlage(Ziel(), faelle);

        Assert.Equal(EnthaltungsGrund.NachbarnUneinig, vorschlag.Grund);
    }

    [Fact]
    public void Ohne_gelernte_Faelle_wird_geschwiegen()
    {
        Assert.True(KostenVorschlagPolicy.Schlage(Ziel(), []).IstEnthaltung);
    }
}
```

- [ ] **Schritt 2: Test laufen lassen — er muss scheitern**

```bash
dotnet test tests/AuswertungPro.Next.Infrastructure.Tests/AuswertungPro.Next.Infrastructure.Tests.csproj --filter "FullyQualifiedName~KostenVorschlagPolicyTests"
```

Erwartet: `CS0103` — `KostenVorschlagPolicy` existiert nicht.

- [ ] **Schritt 3: Regeln umsetzen**

```csharp
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace AuswertungPro.Next.Application.Kostenanalyse;

/// <summary>
/// Entscheidet, ob ueberhaupt vorgeschlagen wird — und schweigt sonst mit Begruendung.
///
/// Das ist das wichtigste Bauteil der Kostenanalyse: Eine erfundene Zahl in einer Offerte
/// richtet mehr Schaden an als eine fehlende. Die Schwellen sind begruendete Startwerte
/// und werden mit der Rueckblick-Messung ueberprueft.
/// </summary>
public static class KostenVorschlagPolicy
{
    public const int MindestNachbarn = 3;
    public const int MaximalNachbarn = 7;
    public const int MindestBogenFaelle = 10;

    private static readonly CultureInfo Ch = CultureInfo.GetCultureInfo("de-CH");

    public static KostenVorschlag Schlage(KostenfallMerkmale ziel, IReadOnlyList<Kostenfall> faelle)
    {
        ArgumentNullException.ThrowIfNull(ziel);
        ArgumentNullException.ThrowIfNull(faelle);

        if (ziel.DnMm is not > 0 || KostenfallAehnlichkeit.DnStufenAbstand(ziel.DnMm.Value, ziel.DnMm.Value) is null)
        {
            return KostenVorschlag.Enthaltung(
                EnthaltungsGrund.DurchmesserUnbekannt,
                $"Durchmesser {ziel.DnMm?.ToString(Ch) ?? "unbekannt"} ist keine bekannte Nennweite.");
        }

        if (ziel.HatBogen)
        {
            var bogenfaelle = faelle.Count(f => f.Merkmale.HatBogen);
            if (bogenfaelle < MindestBogenFaelle)
            {
                return KostenVorschlag.Enthaltung(
                    EnthaltungsGrund.BogenNichtGelernt,
                    $"Haltung hat einen Bogen, gelernt sind erst {bogenfaelle} Bogenfaelle "
                    + $"(noetig: {MindestBogenFaelle}).");
            }
        }

        var nachbarn = KostenfallAehnlichkeit.FindeNachbarn(ziel, faelle, MaximalNachbarn);
        if (nachbarn.Count < MindestNachbarn)
        {
            return KostenVorschlag.Enthaltung(
                EnthaltungsGrund.ZuWenigeFaelle,
                $"Zu wenig Erfahrung: nur {nachbarn.Count} aehnliche Faelle "
                + $"(noetig: {MindestNachbarn}).");
        }

        var positionen = KostenVorschlagRechner.Rechne(ziel, nachbarn);
        if (positionen.Count == 0)
        {
            return KostenVorschlag.Enthaltung(
                EnthaltungsGrund.NachbarnUneinig,
                $"Die {nachbarn.Count} aehnlichen Faelle haben keine gemeinsame Massnahme.");
        }

        return new KostenVorschlag
        {
            Positionen = positionen,
            HerangezogeneFaelle = nachbarn.Count,
            Grund = EnthaltungsGrund.Kein
        };
    }
}
```

- [ ] **Schritt 4: Test laufen lassen — er muss bestehen**

```bash
dotnet test tests/AuswertungPro.Next.Infrastructure.Tests/AuswertungPro.Next.Infrastructure.Tests.csproj --filter "FullyQualifiedName~KostenVorschlagPolicyTests"
```

Erwartet: 7 bestanden.

- [ ] **Schritt 5: Einchecken**

```bash
git add src/AuswertungPro.Next.Application/Kostenanalyse/KostenVorschlagPolicy.cs tests/AuswertungPro.Next.Infrastructure.Tests/Kostenanalyse/KostenVorschlagPolicyTests.cs
git commit -m "feat(kostenanalyse): Enthaltungsregeln mit Begruendung"
```

---

## Aufgabe 9: Rückblick-Messung (Leave-one-out)

**Dateien:**
- Anlegen: `src/AuswertungPro.Next.Application/Kostenanalyse/KostenanalyseMessung.cs`
- Test: `tests/AuswertungPro.Next.Infrastructure.Tests/Kostenanalyse/KostenanalyseMessungTests.cs`

**Schnittstellen:**
- Verbraucht: `KostenVorschlagPolicy.Schlage`, `Kostenfall`, `KostenVorschlag`
- Liefert:
  - `KostenanalyseMessErgebnis` (record) mit `Gesamt`, `MitVorschlag`, `Enthalten`,
    `PositionenRichtig`, `PositionenZuviel`, `PositionenFehlend`, `Abdeckung`
  - `KostenanalyseMessung.Messe(faelle)` → `KostenanalyseMessErgebnis`

- [ ] **Schritt 1: Fehlschlagenden Test schreiben**

```csharp
using System.Collections.Generic;
using System.Linq;
using AuswertungPro.Next.Application.Kostenanalyse;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.Kostenanalyse;

public sealed class KostenanalyseMessungTests
{
    private static Kostenfall Fall(string name, params MassnahmePosition[] positionen) => new()
    {
        Haltung = name,
        Merkmale = new KostenfallMerkmale
        {
            DnMm = 300,
            LaengeM = 40,
            Schaeden = [new SchadensMerkmal("BAF", 1, false)]
        },
        Positionen = positionen.Length > 0
            ? [.. positionen]
            : [new MassnahmePosition("SCHLAUCHLINER_GFK", 40m, "m")]
    };

    [Fact]
    public void Jeder_Fall_wird_ohne_sich_selbst_vorhergesagt()
    {
        // 5 gleiche Faelle: Jeder wird aus den 4 anderen exakt getroffen.
        var faelle = Enumerable.Range(0, 5).Select(i => Fall($"H{i}")).ToList();

        var ergebnis = KostenanalyseMessung.Messe(faelle);

        Assert.Equal(5, ergebnis.Gesamt);
        Assert.Equal(5, ergebnis.MitVorschlag);
        Assert.Equal(0, ergebnis.Enthalten);
        Assert.Equal(5, ergebnis.PositionenRichtig);
        Assert.Equal(0, ergebnis.PositionenFehlend);
        Assert.Equal(0, ergebnis.PositionenZuviel);
        Assert.Equal(1.0, ergebnis.Abdeckung);
    }

    [Fact]
    public void Zu_kleine_Bestaende_ergeben_lauter_Enthaltungen()
    {
        var ergebnis = KostenanalyseMessung.Messe([Fall("H1"), Fall("H2"), Fall("H3")]);

        // Jeder Fall sieht nur 2 andere -> unter MindestNachbarn.
        Assert.Equal(3, ergebnis.Gesamt);
        Assert.Equal(0, ergebnis.MitVorschlag);
        Assert.Equal(3, ergebnis.Enthalten);
        Assert.Equal(0.0, ergebnis.Abdeckung);
    }

    [Fact]
    public void Eine_vergessene_Position_wird_gezaehlt()
    {
        // 4 Faelle nur mit Liner, der fuenfte hat zusaetzlich Manschetten.
        var faelle = new List<Kostenfall>
        {
            Fall("H1"), Fall("H2"), Fall("H3"), Fall("H4"),
            Fall("H5",
                new MassnahmePosition("SCHLAUCHLINER_GFK", 40m, "m"),
                new MassnahmePosition("MANSCHETTE_EDELSTAHL", 2m, "Stk"))
        };

        var ergebnis = KostenanalyseMessung.Messe(faelle);

        // Fuer H5 fehlt die Manschette im Vorschlag.
        Assert.True(ergebnis.PositionenFehlend >= 1);
    }

    [Fact]
    public void Ein_leerer_Bestand_ergibt_ein_leeres_Ergebnis()
    {
        var ergebnis = KostenanalyseMessung.Messe([]);

        Assert.Equal(0, ergebnis.Gesamt);
        Assert.Equal(0.0, ergebnis.Abdeckung);
    }

    [Fact]
    public void Nur_unbeeinflusste_Faelle_werden_gemessen()
    {
        var faelle = Enumerable.Range(0, 5)
            .Select(i => Fall($"H{i}") with
            {
                Herkunft = i == 0 ? KostenfallHerkunft.VorschlagGesehen : KostenfallHerkunft.Unbeeinflusst
            })
            .ToList();

        var ergebnis = KostenanalyseMessung.Messe(faelle);

        // Der beeinflusste Fall bleibt Lernmaterial, wird aber nicht bewertet.
        Assert.Equal(4, ergebnis.Gesamt);
    }
}
```

- [ ] **Schritt 2: Test laufen lassen — er muss scheitern**

```bash
dotnet test tests/AuswertungPro.Next.Infrastructure.Tests/AuswertungPro.Next.Infrastructure.Tests.csproj --filter "FullyQualifiedName~KostenanalyseMessungTests"
```

Erwartet: `CS0103` — `KostenanalyseMessung` existiert nicht.

- [ ] **Schritt 3: Messung umsetzen**

```csharp
using System;
using System.Collections.Generic;
using System.Linq;

namespace AuswertungPro.Next.Application.Kostenanalyse;

/// <summary>Ergebnis der Rueckblick-Messung. Abdeckung und Treffer stehen bewusst nebeneinander.</summary>
public sealed record KostenanalyseMessErgebnis
{
    public int Gesamt { get; init; }
    public int MitVorschlag { get; init; }
    public int Enthalten { get; init; }
    public int PositionenRichtig { get; init; }
    public int PositionenZuviel { get; init; }
    public int PositionenFehlend { get; init; }

    /// <summary>Anteil der Haltungen, die ueberhaupt einen Vorschlag bekamen.</summary>
    public double Abdeckung => Gesamt == 0 ? 0d : (double)MitVorschlag / Gesamt;
}

/// <summary>
/// Misst die Vorhersagegüte rueckblickend: Jeder Fall wird OHNE sich selbst vorhergesagt
/// und mit dem echten Paket verglichen (Leave-one-out).
///
/// Bewertet werden nur unbeeinflusste Faelle. Ein Fall, bei dem der Vorschlag vorher
/// sichtbar war, bleibt Lernmaterial — sonst misst sich das Verfahren an sich selbst.
///
/// Abdeckung wird immer mitberichtet: Ein Modell, das nur schweigt, hat null Fehler
/// und null Nutzen.
/// </summary>
public static class KostenanalyseMessung
{
    public static KostenanalyseMessErgebnis Messe(IReadOnlyList<Kostenfall> faelle)
    {
        ArgumentNullException.ThrowIfNull(faelle);

        var messbar = faelle.Where(f => f.Herkunft == KostenfallHerkunft.Unbeeinflusst).ToList();

        var mitVorschlag = 0;
        var enthalten = 0;
        var richtig = 0;
        var zuviel = 0;
        var fehlend = 0;

        foreach (var fall in messbar)
        {
            // Ohne sich selbst — sonst waere die Antwort im Bestand enthalten.
            var andere = faelle.Where(f => !ReferenceEquals(f, fall)).ToList();
            var vorschlag = KostenVorschlagPolicy.Schlage(fall.Merkmale, andere);

            if (vorschlag.IstEnthaltung)
            {
                enthalten++;
                continue;
            }

            mitVorschlag++;

            var vorhergesagt = new HashSet<string>(
                vorschlag.Positionen.Select(p => p.ItemKey), StringComparer.OrdinalIgnoreCase);
            var tatsaechlich = new HashSet<string>(
                fall.Positionen.Select(p => p.ItemKey), StringComparer.OrdinalIgnoreCase);

            richtig += vorhergesagt.Intersect(tatsaechlich, StringComparer.OrdinalIgnoreCase).Count();
            zuviel += vorhergesagt.Except(tatsaechlich, StringComparer.OrdinalIgnoreCase).Count();
            fehlend += tatsaechlich.Except(vorhergesagt, StringComparer.OrdinalIgnoreCase).Count();
        }

        return new KostenanalyseMessErgebnis
        {
            Gesamt = messbar.Count,
            MitVorschlag = mitVorschlag,
            Enthalten = enthalten,
            PositionenRichtig = richtig,
            PositionenZuviel = zuviel,
            PositionenFehlend = fehlend
        };
    }
}
```

- [ ] **Schritt 4: Test laufen lassen — er muss bestehen**

```bash
dotnet test tests/AuswertungPro.Next.Infrastructure.Tests/AuswertungPro.Next.Infrastructure.Tests.csproj --filter "FullyQualifiedName~KostenanalyseMessungTests"
```

Erwartet: 5 bestanden.

- [ ] **Schritt 5: Einchecken**

```bash
git add src/AuswertungPro.Next.Application/Kostenanalyse/KostenanalyseMessung.cs tests/AuswertungPro.Next.Infrastructure.Tests/Kostenanalyse/KostenanalyseMessungTests.cs
git commit -m "feat(kostenanalyse): Rueckblick-Messung mit Abdeckung"
```

---

## Aufgabe 10: Fälle aus einem Projekt aufbauen

**Dateien:**
- Anlegen: `src/AuswertungPro.Next.Application/Kostenanalyse/KostenfallAufbauLauf.cs`
- Anlegen: `tools/KostenfallAufbau/KostenfallAufbau.csproj`
- Anlegen: `tools/KostenfallAufbau/Program.cs`
- Ändern: `AuswertungPro.sln` (Projekt aufnehmen)
- Test: `tests/AuswertungPro.Next.Infrastructure.Tests/Kostenanalyse/KostenfallAufbauLaufTests.cs`

**Schnittstellen:**
- Verbraucht: `KostenfallExtraktor.TryErstellen`, `KostenfallFileStore`
- Liefert: `KostenfallAufbauLauf.Baue(project, costStore, projektName, jetztUtc)`
  → `(IReadOnlyList<Kostenfall> Faelle, IReadOnlyList<string> Uebersprungen)`

Die Kernlogik liegt als testbare Klasse in Application; die CLI ist nur eine Hülle.

- [ ] **Schritt 1: Fehlschlagenden Test schreiben**

```csharp
using System;
using System.Linq;
using AuswertungPro.Next.Application.Kostenanalyse;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.Kostenanalyse;

public sealed class KostenfallAufbauLaufTests
{
    private static readonly DateTime Jetzt = new(2026, 8, 20, 12, 0, 0, DateTimeKind.Utc);

    private static HaltungRecord Haltung(string name, string dn, string laenge, params string[] codes)
    {
        var record = new HaltungRecord();
        record.SetFieldValue("Haltungsname", name, FieldSource.Manual, userEdited: false);
        record.SetFieldValue("DN_mm", dn, FieldSource.Manual, userEdited: false);
        record.SetFieldValue("Haltungslaenge_m", laenge, FieldSource.Manual, userEdited: false);
        record.Protocol = new ProtocolDocument
        {
            Current = new ProtocolRevision { Entries = [.. codes.Select(c => new ProtocolEntry { Code = c })] }
        };
        return record;
    }

    private static HoldingCost Kosten(string holding) => new()
    {
        Holding = holding,
        Measures =
        [
            new MeasureCost
            {
                MeasureId = "M", MeasureName = "Renovierung",
                Lines = [new CostLine { ItemKey = "SCHLAUCHLINER_GFK", Qty = 40m, Unit = "m", UnitPrice = 200m, Selected = true }]
            }
        ]
    };

    [Fact]
    public void Baut_Faelle_nur_aus_Haltungen_mit_Kosten()
    {
        var projekt = new Project();
        projekt.Data.Add(Haltung("H-1", "300", "40", "BAF01"));
        projekt.Data.Add(Haltung("H-2", "300", "40", "BAF01"));
        var kosten = new ProjectCostStore { ByHolding = { ["H-1"] = Kosten("H-1") } };

        var (faelle, uebersprungen) = KostenfallAufbauLauf.Baue(projekt, kosten, "Zone 1.15", Jetzt);

        Assert.Equal("H-1", Assert.Single(faelle).Haltung);
        Assert.Contains(uebersprungen, u => u.Contains("H-2"));
    }

    [Fact]
    public void Der_Grund_des_Ueberspringens_steht_im_Bericht()
    {
        var projekt = new Project();
        projekt.Data.Add(Haltung("H-1", "", "40", "BAF01"));
        var kosten = new ProjectCostStore { ByHolding = { ["H-1"] = Kosten("H-1") } };

        var (faelle, uebersprungen) = KostenfallAufbauLauf.Baue(projekt, kosten, "P", Jetzt);

        Assert.Empty(faelle);
        Assert.Contains("Durchmesser", Assert.Single(uebersprungen));
    }

    [Fact]
    public void Alle_Faelle_gelten_als_unbeeinflusst()
    {
        // Der Altbestand entstand ohne jeden Vorschlag - er ist unbeeinflusst.
        var projekt = new Project();
        projekt.Data.Add(Haltung("H-1", "300", "40", "BAF01"));
        var kosten = new ProjectCostStore { ByHolding = { ["H-1"] = Kosten("H-1") } };

        var (faelle, _) = KostenfallAufbauLauf.Baue(projekt, kosten, "P", Jetzt);

        Assert.Equal(KostenfallHerkunft.Unbeeinflusst, Assert.Single(faelle).Herkunft);
    }
}
```

- [ ] **Schritt 2: Test laufen lassen — er muss scheitern**

```bash
dotnet test tests/AuswertungPro.Next.Infrastructure.Tests/AuswertungPro.Next.Infrastructure.Tests.csproj --filter "FullyQualifiedName~KostenfallAufbauLaufTests"
```

Erwartet: `CS0103` — `KostenfallAufbauLauf` existiert nicht.

- [ ] **Schritt 3: Aufbaulauf umsetzen**

Datei: `src/AuswertungPro.Next.Application/Kostenanalyse/KostenfallAufbauLauf.cs`

```csharp
using System;
using System.Collections.Generic;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.Kostenanalyse;

/// <summary>
/// Baut aus einem geoeffneten Projekt und seinem Kostenspeicher die Lernfaelle auf.
///
/// Uebersprungene Haltungen werden mit Grund gemeldet, nicht still verschluckt: Wer
/// spaeter wissen will, warum nur 58 von 96 Haltungen zaehlen, findet die Antwort hier.
///
/// Alle so gewonnenen Faelle gelten als unbeeinflusst — sie entstanden, bevor es
/// ueberhaupt einen Vorschlag gab.
/// </summary>
public static class KostenfallAufbauLauf
{
    public static (IReadOnlyList<Kostenfall> Faelle, IReadOnlyList<string> Uebersprungen) Baue(
        Project projekt,
        ProjectCostStore kosten,
        string projektName,
        DateTime jetztUtc)
    {
        ArgumentNullException.ThrowIfNull(projekt);
        ArgumentNullException.ThrowIfNull(kosten);

        var faelle = new List<Kostenfall>();
        var uebersprungen = new List<string>();

        foreach (var record in projekt.Data)
        {
            var name = (record.GetFieldValue(FieldKeys.HoldingName) ?? "").Trim();
            var anzeige = name.Length == 0 ? "(ohne Namen)" : name;

            if (name.Length == 0 || !kosten.ByHolding.TryGetValue(name, out var cost))
            {
                uebersprungen.Add($"{anzeige}: keine Kostenzusammenstellung.");
                continue;
            }

            if (KostenfallExtraktor.TryErstellen(
                    record, cost, projektName, KostenfallHerkunft.Unbeeinflusst, jetztUtc,
                    out var fall, out var grund))
            {
                faelle.Add(fall);
            }
            else
            {
                uebersprungen.Add($"{anzeige}: {grund}");
            }
        }

        return (faelle, uebersprungen);
    }
}
```

- [ ] **Schritt 4: Test laufen lassen — er muss bestehen**

```bash
dotnet test tests/AuswertungPro.Next.Infrastructure.Tests/AuswertungPro.Next.Infrastructure.Tests.csproj --filter "FullyQualifiedName~KostenfallAufbauLaufTests"
```

Erwartet: 3 bestanden.

- [ ] **Schritt 5: CLI-Hülle anlegen**

Datei: `tools/KostenfallAufbau/KostenfallAufbau.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\src\AuswertungPro.Next.Application\AuswertungPro.Next.Application.csproj" />
    <ProjectReference Include="..\..\src\AuswertungPro.Next.Infrastructure\AuswertungPro.Next.Infrastructure.csproj" />
  </ItemGroup>
</Project>
```

Datei: `tools/KostenfallAufbau/Program.cs`

```csharp
using System.Text.Json;
using AuswertungPro.Next.Application.Kostenanalyse;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Kostenanalyse;

// Baut die Lernfaelle aus einem Projekt auf. Liest nur; schreibt allein die Falldatei.
if (args.Length < 3)
{
    Console.WriteLine("Aufruf: KostenfallAufbau <projekt.json> <costs.json> <KnowledgeRoot> [--execute]");
    return 2;
}

var projektPfad = args[0];
var kostenPfad = args[1];
var wurzel = args[2];
var schreiben = args.Contains("--execute");

var optionen = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
var projekt = JsonSerializer.Deserialize<Project>(File.ReadAllText(projektPfad), optionen);
var kosten = JsonSerializer.Deserialize<ProjectCostStore>(File.ReadAllText(kostenPfad), optionen);

if (projekt is null || kosten is null)
{
    Console.Error.WriteLine("Projekt oder Kostendatei nicht lesbar.");
    return 1;
}

var projektName = string.IsNullOrWhiteSpace(projekt.Name)
    ? Path.GetFileNameWithoutExtension(projektPfad)
    : projekt.Name;

var (faelle, uebersprungen) = KostenfallAufbauLauf.Baue(projekt, kosten, projektName, DateTime.UtcNow);

Console.WriteLine($"Faelle aufgebaut : {faelle.Count}");
Console.WriteLine($"Uebersprungen    : {uebersprungen.Count}");
foreach (var zeile in uebersprungen.Take(20))
    Console.WriteLine($"  {zeile}");
if (uebersprungen.Count > 20)
    Console.WriteLine($"  ... und {uebersprungen.Count - 20} weitere");

if (!schreiben)
{
    Console.WriteLine();
    Console.WriteLine("Pruflauf - nichts geschrieben. Mit --execute schreiben.");
    return 0;
}

new KostenfallFileStore(wurzel).Speichere(faelle);
Console.WriteLine($"Geschrieben nach {wurzel}\\kostenanalyse\\kostenfaelle_v1.json");
return 0;
```

- [ ] **Schritt 6: Projekt in die Solution aufnehmen**

```bash
dotnet sln AuswertungPro.sln add tools/KostenfallAufbau/KostenfallAufbau.csproj
dotnet build AuswertungPro.sln
```

Erwartet: 0 Fehler. (CLAUDE.md verlangt, neue Werkzeugprojekte sofort aufzunehmen.)

- [ ] **Schritt 7: Prüflauf auf den echten Daten**

```bash
dotnet run --project tools/KostenfallAufbau -- "D:\Projekte\Zone 1.15\Altdorf_Zone_1.15.json" "D:\Projekte\Zone 1.15\costs\costs.json" "C:\KI_BRAIN"
```

Erwartet: rund 58 Fälle, der Rest mit Begründung übersprungen. **Ohne** `--execute` wird
nichts geschrieben. Die Zahl mit dem Konzept vergleichen — grössere Abweichung heisst,
die Merkmalsregeln stimmen nicht.

- [ ] **Schritt 8: Einchecken**

```bash
git add src/AuswertungPro.Next.Application/Kostenanalyse/KostenfallAufbauLauf.cs tools/KostenfallAufbau/ AuswertungPro.sln tests/AuswertungPro.Next.Infrastructure.Tests/Kostenanalyse/KostenfallAufbauLaufTests.cs
git commit -m "feat(kostenanalyse): Faelle aus einem Projekt aufbauen"
```

---

## Aufgabe 11: Messbericht schreiben

**Dateien:**
- Anlegen: `src/AuswertungPro.Next.Infrastructure/Kostenanalyse/KostenanalyseBerichtSchreiber.cs`
- Test: `tests/AuswertungPro.Next.Infrastructure.Tests/Kostenanalyse/KostenanalyseBerichtSchreiberTests.cs`

**Schnittstellen:**
- Verbraucht: `KostenanalyseMessErgebnis` (Aufgabe 9)
- Liefert: `KostenanalyseBerichtSchreiber.Schreibe(wurzel, ergebnis, zeitpunktUtc)` → `string` (Pfad
  der Berichtsdatei); daneben entsteht `<name>.sha256`

- [ ] **Schritt 1: Fehlschlagenden Test schreiben**

```csharp
using System;
using System.IO;
using AuswertungPro.Next.Application.Kostenanalyse;
using AuswertungPro.Next.Infrastructure.Kostenanalyse;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.Kostenanalyse;

public sealed class KostenanalyseBerichtSchreiberTests : IDisposable
{
    private readonly string _wurzel = Directory.CreateTempSubdirectory().FullName;

    public void Dispose()
    {
        try { Directory.Delete(_wurzel, recursive: true); } catch { }
    }

    private static KostenanalyseMessErgebnis Ergebnis() => new()
    {
        Gesamt = 58,
        MitVorschlag = 41,
        Enthalten = 17,
        PositionenRichtig = 96,
        PositionenZuviel = 12,
        PositionenFehlend = 23
    };

    [Fact]
    public void Schreibt_Bericht_und_Pruefsumme()
    {
        var pfad = KostenanalyseBerichtSchreiber.Schreibe(
            _wurzel, Ergebnis(), new DateTime(2026, 8, 20, 14, 30, 0, DateTimeKind.Utc));

        Assert.True(File.Exists(pfad));
        Assert.True(File.Exists(pfad + ".sha256"));
        Assert.Equal(64, File.ReadAllText(pfad + ".sha256").Trim().Length);
    }

    [Fact]
    public void Der_Bericht_enthaelt_Abdeckung_und_Treffer()
    {
        var pfad = KostenanalyseBerichtSchreiber.Schreibe(
            _wurzel, Ergebnis(), new DateTime(2026, 8, 20, 14, 30, 0, DateTimeKind.Utc));

        var inhalt = File.ReadAllText(pfad);

        Assert.Contains("\"gesamt\": 58", inhalt);
        Assert.Contains("\"mitVorschlag\": 41", inhalt);
        Assert.Contains("abdeckung", inhalt);
        Assert.Contains("positionenFehlend", inhalt);
    }

    [Fact]
    public void Ein_bestehender_Bericht_wird_nie_ueberschrieben()
    {
        var zeitpunkt = new DateTime(2026, 8, 20, 14, 30, 0, DateTimeKind.Utc);
        KostenanalyseBerichtSchreiber.Schreibe(_wurzel, Ergebnis(), zeitpunkt);

        Assert.Throws<IOException>(
            () => KostenanalyseBerichtSchreiber.Schreibe(_wurzel, Ergebnis(), zeitpunkt));
    }
}
```

- [ ] **Schritt 2: Test laufen lassen — er muss scheitern**

```bash
dotnet test tests/AuswertungPro.Next.Infrastructure.Tests/AuswertungPro.Next.Infrastructure.Tests.csproj --filter "FullyQualifiedName~KostenanalyseBerichtSchreiberTests"
```

Erwartet: `CS0103` — `KostenanalyseBerichtSchreiber` existiert nicht.

- [ ] **Schritt 3: Berichtschreiber umsetzen**

```csharp
using System;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AuswertungPro.Next.Application.Kostenanalyse;

namespace AuswertungPro.Next.Infrastructure.Kostenanalyse;

/// <summary>
/// Schreibt das Messergebnis als Bericht mit SHA-256 daneben — wie die uebrigen
/// Messberichte des Projekts. Ein bestehender Bericht wird nie ueberschrieben: Eine
/// zweite Messung desselben Zeitpunkts waere sonst still verschwunden.
/// </summary>
public static class KostenanalyseBerichtSchreiber
{
    private static readonly JsonSerializerOptions Optionen = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static string Schreibe(string wurzel, KostenanalyseMessErgebnis ergebnis, DateTime zeitpunktUtc)
    {
        ArgumentNullException.ThrowIfNull(ergebnis);
        if (string.IsNullOrWhiteSpace(wurzel))
            throw new ArgumentException("Wurzel fehlt.", nameof(wurzel));

        var ordner = Path.Combine(wurzel, "kostenanalyse", "berichte");
        Directory.CreateDirectory(ordner);

        var name = $"kostenanalyse_rueckblick_{zeitpunktUtc:yyyyMMdd_HHmmss}.json";
        var pfad = Path.Combine(ordner, name);

        if (File.Exists(pfad))
            throw new IOException($"Bericht existiert bereits und wird nicht ueberschrieben: {pfad}");

        var dokument = new
        {
            erzeugtUtc = zeitpunktUtc.ToString("O", CultureInfo.InvariantCulture),
            art = "rueckblick_leave_one_out",
            hinweis = "Standortbestimmung, keine Freigabe. Misst nur den vorhandenen Bestand.",
            gesamt = ergebnis.Gesamt,
            mitVorschlag = ergebnis.MitVorschlag,
            enthalten = ergebnis.Enthalten,
            abdeckung = Math.Round(ergebnis.Abdeckung, 4),
            positionenRichtig = ergebnis.PositionenRichtig,
            positionenZuviel = ergebnis.PositionenZuviel,
            positionenFehlend = ergebnis.PositionenFehlend,
            schwellen = new
            {
                mindestNachbarn = KostenVorschlagPolicy.MindestNachbarn,
                maximalNachbarn = KostenVorschlagPolicy.MaximalNachbarn,
                mindestBogenFaelle = KostenVorschlagPolicy.MindestBogenFaelle
            }
        };

        var inhalt = JsonSerializer.Serialize(dokument, Optionen);
        File.WriteAllText(pfad, inhalt);

        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(inhalt))).ToLowerInvariant();
        File.WriteAllText(pfad + ".sha256", hash);

        return pfad;
    }
}
```

- [ ] **Schritt 4: Test laufen lassen — er muss bestehen**

```bash
dotnet test tests/AuswertungPro.Next.Infrastructure.Tests/AuswertungPro.Next.Infrastructure.Tests.csproj --filter "FullyQualifiedName~KostenanalyseBerichtSchreiberTests"
```

Erwartet: 3 bestanden.

- [ ] **Schritt 5: Einchecken**

```bash
git add src/AuswertungPro.Next.Infrastructure/Kostenanalyse/KostenanalyseBerichtSchreiber.cs tests/AuswertungPro.Next.Infrastructure.Tests/Kostenanalyse/KostenanalyseBerichtSchreiberTests.cs
git commit -m "feat(kostenanalyse): Messbericht mit Pruefsumme"
```

---

## Aufgabe 12: Messung auf den echten Fällen

**Dateien:**
- Ändern: `tools/KostenfallAufbau/Program.cs` (Befehl `--messen`)
- Anlegen: `docs/quality/KOSTENANALYSE-RUECKBLICK-<Datum>.md`

**Schnittstellen:**
- Verbraucht: `KostenfallFileStore.Lade`, `KostenanalyseMessung.Messe`,
  `KostenanalyseBerichtSchreiber.Schreibe`
- Liefert: nichts für spätere Aufgaben

- [ ] **Schritt 1: Messbefehl ergänzen**

In `tools/KostenfallAufbau/Program.cs` **vor** der bestehenden Argumentprüfung einfügen:

```csharp
// Messbefehl: liest den Fallbestand und misst rueckblickend.
if (args.Length >= 2 && args[0] == "--messen")
{
    var messWurzel = args[1];
    var bestand = new KostenfallFileStore(messWurzel).Lade();

    var messErgebnis = KostenanalyseMessung.Messe(bestand);
    Console.WriteLine($"Faelle gemessen  : {messErgebnis.Gesamt}");
    Console.WriteLine($"mit Vorschlag    : {messErgebnis.MitVorschlag}");
    Console.WriteLine($"Enthaltungen     : {messErgebnis.Enthalten}");
    Console.WriteLine($"Abdeckung        : {messErgebnis.Abdeckung:P1}");
    Console.WriteLine($"Positionen richtig/zuviel/fehlend: "
        + $"{messErgebnis.PositionenRichtig}/{messErgebnis.PositionenZuviel}/{messErgebnis.PositionenFehlend}");

    var berichtPfad = KostenanalyseBerichtSchreiber.Schreibe(messWurzel, messErgebnis, DateTime.UtcNow);
    Console.WriteLine($"Bericht: {berichtPfad}");
    return 0;
}
```

- [ ] **Schritt 2: Bauen**

```bash
dotnet build AuswertungPro.sln
```

Erwartet: 0 Fehler.

- [ ] **Schritt 3: Fälle wirklich schreiben**

```bash
dotnet run --project tools/KostenfallAufbau -- "D:\Projekte\Zone 1.15\Altdorf_Zone_1.15.json" "D:\Projekte\Zone 1.15\costs\costs.json" "C:\KI_BRAIN" --execute
```

Erwartet: rund 58 Fälle geschrieben.

- [ ] **Schritt 4: Messen**

```bash
dotnet run --project tools/KostenfallAufbau -- --messen "C:\KI_BRAIN"
```

Notiere die vier Zahlen: Abdeckung, richtig, zuviel, fehlend.

- [ ] **Schritt 5: Ergebnis festhalten**

Lege `docs/quality/KOSTENANALYSE-RUECKBLICK-<Datum>.md` an mit:

- den gemessenen Zahlen und dem SHA-256 des Berichts
- der Feststellung, dass nur Zone 1.15 gemessen wurde (ein Gebiet, ein Bearbeiter,
  ein Preisstand) — **das ist keine Freigabe**
- der Entscheidung: weiter zu Etappe 4/5, oder zuerst ein zweites Projekt auswerten

**Entscheidungshilfe:** Liegt die Abdeckung unter 50 % oder sind mehr Positionen fehlend
als richtig, trägt die Datenlage nicht. Dann ist die Antwort „zweites Projekt auswerten"
und nicht „Schwellen senken". Schwellen zu senken, bis die Zahl schön aussieht, ist genau
der Fehler, den die Enthaltung verhindern soll.

- [ ] **Schritt 6: Einchecken**

```bash
git add tools/KostenfallAufbau/Program.cs docs/quality/
git commit -m "feat(kostenanalyse): Rueckblick-Messung auf den echten Faellen"
```

---

## Nach Etappe 3

Halt. Die Messung entscheidet, ob es weitergeht:

| Ergebnis | nächster Schritt |
|---|---|
| Abdeckung brauchbar, mehr richtig als fehlend | Etappe 4 (Anzeige in der Schattenauswertung) planen |
| viele Enthaltungen | zweites Projekt auswerten — nicht die Schwellen senken |
| viele falsche Positionen | Merkmale prüfen (Ausmass der Schäden?), erneut messen |

Etappe 4 und 5 werden erst nach dieser Entscheidung geplant.
