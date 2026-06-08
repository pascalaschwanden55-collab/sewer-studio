# GIS: Verlauf-Fallback (Haltungen ohne `<Verlauf>` als gerade Linie) — Implementation Plan

> **⛔ ZURÜCKGESTELLT (Task-0-Messung 2026-05-31):** `Abwasserkataster_Uri_korrigiert.xtf` (671 MB) hat **110'224/110'224 Haltungen mit `<Verlauf>` → 0 Fallback-Kandidaten**. Das Feature löst aktuell kein reales Problem und wird NICHT gebaut. Plan bleibt als Bauanleitung erhalten, falls künftig XTF ohne Verlauf auftauchen.

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans (oder subagent-driven-development). Steps mit Checkbox (`- [ ]`).

**Goal:** Haltungen ohne `<Verlauf>`-Polylinie (oder mit < 2 Punkten) verschwinden aktuell lautlos von der Karte. Sie sollen als **gerade Linie zwischen Start- und End-Schacht** gezeichnet werden (Design-Spec-Vorgabe).

**Architecture:** `XtfNetworkExtractor` liest heute nur `<Verlauf>` und hat **keine** Knoten-Koordinaten. Fallback braucht die Start/End-Schacht-Koordinaten. Verknüpfung im SIA405-XTF läuft über den **Haltungsnamen** (`vonKnoten-nachKnoten`, z. B. `1039170-85450`), die Koordinaten liegen am `.Abwasserknoten` (gleiche `Bezeichnung`). Lösung: vorab eine Knoten-Koordinaten-Tabelle bauen (aus `XtfManholeExtractor`) und dem Netz-Extractor als optionalen Lookup übergeben; bei fehlendem Verlauf gerade Linie aus den beiden Knoten.

**Tech Stack:** .NET 10, xUnit, `XmlReader`-Streaming.

---

## Verifizierte Faktenlage (Stand HEAD b53932fe)

- `XtfNetworkExtractor.Extract` liest pro `.Haltung` nur `Bezeichnung` + `Verlauf/POLYLINE/COORD/C1,C2`; Haltungen mit `< 2` Punkten werden verworfen (`XtfNetworkExtractor.cs:77`). **Keine Knoten-Refs/-Koordinaten.**
- `XtfManholeExtractor.Extract` liefert je `.Abwasserknoten` ein `ManholeGeometry(name, x, y)` (Bezeichnung + Lage C1/C2) — `XtfManholeExtractor.cs:36-65`.
- XTF-Struktur (aus `XtfNetworkExtractorTests.cs:10-19`): Haltungsname `1039170-85450`; **keine** expliziten `vonPunktRef`/`nachPunktRef` im Sample → Verknüpfung über den Namen.

---

## Task 0 (PFLICHT-Vorabmessung — go/no-go)

Bevor irgendwas gebaut wird: **Wie viele Haltungen haben überhaupt kein `<Verlauf>`?** Wenn ~0, lohnt das Feature nicht.

- [ ] **Read-only zählen** (auf der echten XTF, kein Schreibzugriff):
  - Haltungen gesamt vs. Haltungen, die der Extractor heute liefert (= mit ≥2 Verlauf-Punkten). Differenz = Fallback-Kandidaten.
  - Praktisch: kleines wegwerfbares Skript/REPL, das den XTF streamt und `.Haltung`-Starts zählt vs. die vom Extractor gelieferten.
- [ ] **Entscheidung:** Differenz nennenswert (> ~1 %)? → weiter mit Task 1. Sonst → Feature zurückstellen, Notiz im GIS-Audit.

> Ohne diese Messung nicht implementieren — sonst bauen wir evtl. für ein Nicht-Problem (gleiche Disziplin wie beim 32B-Budget).

---

## Task 1: Knoten-Koordinaten-Tabelle (aus Schächten)

**Files:**
- Create: `src/AuswertungPro.Next.Infrastructure/Map/NodeCoordinateIndex.cs`
- Create: `tests/AuswertungPro.Next.Infrastructure.Tests/Map/NodeCoordinateIndexTests.cs`

- [ ] **Step 1: Failing test**

```csharp
using AuswertungPro.Next.Infrastructure.Map;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.Map;

public class NodeCoordinateIndexTests
{
    [Fact]
    public void Lookup_FindetKnotenExakt()
    {
        var index = NodeCoordinateIndex.From(new[]
        {
            new ManholeGeometry("1039170", 2690511.0, 1194863.0),
            new ManholeGeometry("85450",   2690500.0, 1194862.0),
        });
        Assert.True(index.TryGet("1039170", out var p));
        Assert.Equal(2690511.0, p.X, 3);
        Assert.False(index.TryGet("99999", out _));
    }
}
```

- [ ] **Step 2: Test fails (Typ fehlt)** — `dotnet test ... --filter NodeCoordinateIndex`

- [ ] **Step 3: Implementierung**

```csharp
using System.Collections.Generic;

namespace AuswertungPro.Next.Infrastructure.Map;

/// <summary>
/// Schlägt Schacht-/Knoten-Koordinaten per Bezeichnung nach (für den Verlauf-Fallback).
/// </summary>
public sealed class NodeCoordinateIndex
{
    private readonly Dictionary<string, (double X, double Y)> _byName;

    private NodeCoordinateIndex(Dictionary<string, (double X, double Y)> byName) => _byName = byName;

    public static NodeCoordinateIndex From(IEnumerable<ManholeGeometry> manholes)
    {
        var dict = new Dictionary<string, (double, double)>(System.StringComparer.OrdinalIgnoreCase);
        foreach (var m in manholes)
            dict[m.Name.Trim()] = (m.X, m.Y);
        return new NodeCoordinateIndex(dict);
    }

    public bool TryGet(string? nodeName, out (double X, double Y) coord)
    {
        coord = default;
        return !string.IsNullOrWhiteSpace(nodeName) && _byName.TryGetValue(nodeName.Trim(), out coord);
    }

    public int Count => _byName.Count;
}
```

- [ ] **Step 4: Test grün. Commit** `feat(karte): NodeCoordinateIndex (Knoten-Koordinaten-Lookup) + Test`

> Hinweis: `ManholeGeometry`-Property-Namen (`Name`/`X`/`Y`) vor Step 3 in `ManholeGeometry.cs` verifizieren und ggf. anpassen.

---

## Task 2: Fallback im XtfNetworkExtractor

**Files:**
- Modify: `src/AuswertungPro.Next.Infrastructure/Map/XtfNetworkExtractor.cs`
- Modify: `tests/AuswertungPro.Next.Infrastructure.Tests/Map/XtfNetworkExtractorTests.cs`

- [ ] **Step 1: Failing tests** (Haltung ohne Verlauf → gerade Linie aus 2 Knoten; nicht auflösbar → übersprungen)

```csharp
private const string NoVerlaufXtf = @"<?xml version='1.0' encoding='UTF-8'?>
<TRANSFER><DATASECTION><SIA405_ABWASSER_2020_LV95 BID='b'>
  <SIA405_ABWASSER_2020_LV95.SIA405_Abwasser.Haltung TID='h1'>
    <Bezeichnung>1039170-85450</Bezeichnung>
  </SIA405_ABWASSER_2020_LV95.SIA405_Abwasser.Haltung>
</SIA405_ABWASSER_2020_LV95></DATASECTION></TRANSFER>";

[Fact]
public void Extract_OhneVerlauf_ZeichnetGeradeAusKnoten()
{
    var nodes = NodeCoordinateIndex.From(new[]
    {
        new ManholeGeometry("1039170", 2690511.0, 1194863.0),
        new ManholeGeometry("85450",   2690500.0, 1194862.0),
    });
    var path = Path.GetTempFileName();
    File.WriteAllText(path, NoVerlaufXtf);
    try
    {
        var result = new XtfNetworkExtractor().Extract(path, nodes).ToList();
        Assert.Single(result);
        Assert.Equal(2, result[0].Points.Count);
        Assert.Equal(2690511.0, result[0].Points[0].X, 3);
        Assert.Equal(2690500.0, result[0].Points[1].X, 3);
    }
    finally { File.Delete(path); }
}

[Fact]
public void Extract_OhneVerlauf_OhneKnoten_WirdUebersprungen()
{
    var path = Path.GetTempFileName();
    File.WriteAllText(path, NoVerlaufXtf);
    try
    {
        // Kein Lookup übergeben -> kein Fallback möglich -> übersprungen (wie bisher)
        Assert.Empty(new XtfNetworkExtractor().Extract(path).ToList());
    }
    finally { File.Delete(path); }
}
```

- [ ] **Step 2: Tests fehlschlagen** (Extract hat noch keinen 2. Parameter)

- [ ] **Step 3: Implementierung** — optionaler Lookup + Fallback im EndElement-Zweig:

`Extract`-Signatur:
```csharp
public IEnumerable<HaltungGeometry> Extract(string xtfPath, NodeCoordinateIndex? nodes = null)
```
Im `.Haltung`-EndElement (`XtfNetworkExtractor.cs:75-80`) statt nur `points is { Count: >= 2 }`:
```csharp
else if (reader.LocalName.EndsWith(".Haltung", StringComparison.Ordinal))
{
    if (!string.IsNullOrWhiteSpace(name))
    {
        if (points is { Count: >= 2 })
            yield return new HaltungGeometry(name!, points);
        else if (nodes is not null && TryStraightLine(name!, nodes, out var line))
            yield return new HaltungGeometry(name!, line);  // gerade Ersatzlinie
    }
    name = null; points = null;
}
```
Helper (gleiche Klasse):
```csharp
private static bool TryStraightLine(string haltungsname, NodeCoordinateIndex nodes,
    out List<(double X, double Y)> line)
{
    line = null!;
    var parts = haltungsname.Split('-');
    if (parts.Length != 2) return false;          // nur eindeutiges "von-nach"
    if (!nodes.TryGet(parts[0], out var a)) return false;
    if (!nodes.TryGet(parts[1], out var b)) return false;
    line = new() { (a.X, a.Y), (b.X, b.Y) };
    return true;
}
```

- [ ] **Step 4: Tests grün. Commit** `feat(karte): Verlauf-Fallback - Haltung ohne Verlauf als gerade Linie aus Schacht-Koords`

---

## Task 3: Verdrahten (Knoten-Index aufbauen + durchreichen)

**Files:**
- Modify: `src/AuswertungPro.Next.Infrastructure/Map/NetworkGeometryCache.cs` (wo `XtfNetworkExtractor.Extract` aufgerufen wird)
- ggf. `KarteViewModel.cs` (XTF-Ladepfad)

- [ ] **Step 1:** Vor dem Netz-Extract einmal die Knoten lesen:
```csharp
var nodes = NodeCoordinateIndex.From(new XtfManholeExtractor().Extract(xtfPath));
var geometrien = new XtfNetworkExtractor().Extract(xtfPath, nodes).ToList();
```
(Zwei Streaming-Durchläufe über dieselbe XTF — Knoten-Dict ist beschränkt, Netz bleibt streaming.)

- [ ] **Step 2:** Cache-Format-Version hochzählen (neue Geometrien) — `NetworkGeometryCache.CurrentFormatVersion` +1, damit alte Caches verworfen werden.

- [ ] **Step 3:** Build `dotnet build AuswertungPro.sln` → 0/0. Volle Tests grün.

- [ ] **Step 4: Commit** `feat(karte): Knoten-Index in den Netz-Ladepfad verdrahtet (Verlauf-Fallback aktiv)`

---

## OFFENE FRAGE (vor/bei Task 2 klären — braucht echte Daten)

**Knoten-Namens-Matching.** Der Test nutzt saubere Namen (`1039170-85450` ↔ Abwasserknoten `1039170`/`85450`). In Realdaten können Präfixe vorkommen (z. B. `06.24341-35625`, Knoten `10.1064892`). Dann greift `Split('-')` + exakter Lookup evtl. nicht. **Vor Task 2 an der echten XTF prüfen:** Stimmen Haltungsnamen-Teile 1:1 mit Abwasserknoten-`Bezeichnung` überein? Falls nicht → Normalisierung ergänzen (analog `StripNodePrefixes` aus dem IBAK-Import) und Tests dafür. Lieber **kein Fallback** als eine **falsche** gerade Linie zum falschen Knoten.

---

## Self-Review
1. **Spec-Abdeckung:** „Haltung ohne Verlauf → gerade Linie aus Knoten" → Task 2 (+ Task 1 Lookup, Task 3 Verdrahtung). ✓
2. **Kein Blind-Bau:** Task 0 misst erst, ob das Problem real ist; offene Frage zum Namens-Matching explizit. ✓
3. **Platzhalter:** Test-/Impl-Code vollständig; XTF-Struktur aus echtem Test übernommen; Property-Namen-Check für ManholeGeometry vermerkt. ✓
4. **Sicherheit:** Lieber überspringen als falsch verbinden (TryStraightLine gibt false statt Rätselraten). ✓

## Was dieser Plan NICHT tut
- Keine expliziten XTF-Ref-Elemente parsen (Sample hat keine; Name-basiert ist der belegte Weg).
- Keine Mapsui-/Render-Änderung.
