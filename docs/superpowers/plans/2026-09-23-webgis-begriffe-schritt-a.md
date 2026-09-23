# WebGIS-Begriffe, Schritt A — Umsetzungsplan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Die einfachen Auswahlfelder (Status, Lagebestimmung, Funktion hydraulisch, Verbindungsart, Bettung, Sanierungsbedarf, Nutzungsart, Profiltyp, Normschacht-Funktion) speichern, zeigen und holen nur noch WebGIS-Begriffe, zeichengenau.

**Architecture:** Neu ist eine Domain-Klasse `WebGisWerteliste` je Feld und die statische Sammlung `WebGisBegriffe`. Die Listen kommen aus dem eingebetteten `Objektakten.Katalog.json`, die Erkennung von Alt- und Importschreibweisen über Faltung, wenige Aliase und die bestehenden Vokabulare als Vorstufe. Die Normseite (`SiaWerteliste.NachNorm`, `NutzungsartVokabular`, `ProfiltypVokabular`) lernt die WebGIS-Beschriftungen als Eingabe, damit XTF, DSS, VSA und Farben weiterlaufen. Den Wechsel des gespeicherten Werts machen nur `ProjectVocabularyNormalizer` (Laden/Speichern), Holen, Objektakte, `QgisFeldKarte` (QGIS/GeoShop) und die Auswahllisten.

**Tech Stack:** C# / .NET 10, WPF, xUnit.

**Spec:** `docs/superpowers/specs/2026-09-23-webgis-begriffe-design.md`

## Global Constraints

- Gespeicherter Wert in den Feldern von Schritt A = Beschriftung aus der WebGIS-Katalogliste, zeichengenau («In Betrieb», «Kreisprofil (K)», «Tot/Aufgehoben, verfüllt»).
- Zustandsklasse bleibt Ziffer 0–4 (nicht anfassen). Material (Haltung/Schacht) und FunktionHierarchisch sind NICHT Schritt A.
- Bestandswerte ohne WebGIS-Gegenstück bleiben unverändert stehen, nie löschen, nie raten.
- `FieldMeta` wird beim Anheben nie verändert (keine neue Handmarkierung, sonst würde gesendet).
- DSS-Export bleibt fail-closed (unbekannter Wert sperrt). SIA405-Export: ohne Norm Feld weg + Hinweis (wie heute).
- «Unbekannt» ist gültig, das Holen füllt damit aber kein leeres Feld.
- Senderegeln unverändert: Sanierungsbedarf nur «Saniert» mit Akte; Bettung wird nicht gesendet.
- Schachtfunktion: nur Normschacht wird umgestellt. «Pumpwerk» → «Pumpenschacht» (das Vokabular führt beide schon als dasselbe). «Absturzbauwerk», «Trennbauwerk», «Be-/Entlüftung», «Sickerschacht», «Spezialbauwerk» bleiben unverändert und werden markiert. Spezialbauwerk/Versickerungsanlage: Funktion unverändert.
- Profiltyp: WebGIS «Andere (A)» ist ein eigener Wert; eine gelesene Schreibweise «andere»/«Anderes (A)» wird zu «Andere (A)». Gespeichertes «Spezialprofil» wird «Spezialprofil (S)». Norm für «Andere (A)» bleibt «Spezialprofil» (bisheriger Entscheid).
- Masse (Entscheid A): Zahl bleibt, Projektprüfung markiert Zahlen, die nicht in der WebGIS-Liste stehen.
- Kein NuGet, keine neue DI-Registrierung, keine Änderung am Projektformat.
- Laufendes SewerStudio sperrt `bin\Debug`: bauen/testen mit `-o .tmp/<ordner>`.
- Kommentare Deutsch (ae/oe/ue im Code), sichtbare Texte mit Umlauten.

## Review Focus

1. Ein Altwert ohne WebGIS-Begriff in einem Auswahlfeld der Tabelle (Status «weitere», Schachtfunktion «Absturzbauwerk») muss sichtbar bleiben und darf nicht beim ersten Klick überschrieben werden → Task 4 (Test «Altwert bleibt in der Liste»).
2. Schacht der Bauwerksart Spezialbauwerk mit Funktion «Pumpwerk» darf NICHT zu «Pumpenschacht» werden → Task 3.
3. Ein importierter Wert (Quelle Xtf/Kataster) bleibt nach dem Anheben ohne Handmarkierung und wird darum nicht gesendet → Task 3.
4. Das WebGIS liefert beim Holen einen Text, der nicht in der Katalogliste steht (Liste im WebGIS erweitert) → Hinweis, nichts eingetragen → Task 5.
5. Zwei WebGIS-Beschriftungen einer Liste falten auf denselben Text → Erkennung wäre mehrdeutig → Task 1 (Eindeutigkeits-Wächter).

---

### Task 1: WebGIS-Wertelisten im Domain

**Files:**
- Create: `src/AuswertungPro.Next.Domain/Models/WebGisWerteliste.cs`
- Create: `src/AuswertungPro.Next.Domain/Models/WebGisBegriffe.cs`
- Modify: `src/AuswertungPro.Next.Application/WebGis/WebGisHandwertKarte.cs` (Falte delegiert)
- Test: `tests/AuswertungPro.Next.Infrastructure.Tests/WebGis/WebGisBegriffeTests.cs`

**Interfaces:**
- Produces:
  - `WebGisWerteliste`: `IReadOnlyList<string> Werte`, `IReadOnlyList<string> Auswahl` ("" + Werte), `bool Kennt(string? wert)` (leer oder exakt in Werte), `string Normalisieren(string? wert)` (Label oder unveränderter Text).
  - `WebGisBegriffe`: `string Falte(string?)`, `WebGisWerteliste? Fuer(bool schacht, string feld)`, `string Normalisieren(bool schacht, string feld, string? wert)`, `IReadOnlyList<string> HaltungFelder`, `IReadOnlyList<string> SchachtFelder` (ohne "Funktion"), Konstante `SchachtFunktion = "Funktion"`.
  - Feldschlüssel: Haltung `FieldKeys.OperatingStatus, PositionAccuracy, HydraulicFunction, ConnectionType, BeddingEncasement, RehabilitationNeed, UsageType, ProfileType`; Schacht `OperatingStatus, PositionAccuracy, RehabilitationNeed, UsageType` + `"Funktion"`.

- [ ] **Step 1: Failing tests schreiben**

```csharp
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Infrastructure.Tests.WebGis;

/// <summary>WebGIS-Begriffe, Schritt A (Entscheid Pascal 23.09.2026): gespeichert wird die WebGIS-Beschriftung.</summary>
public sealed class WebGisBegriffeTests
{
    [Theory]
    [InlineData(false, FieldKeys.OperatingStatus, "in_Betrieb", "In Betrieb")]
    [InlineData(false, FieldKeys.OperatingStatus, "ausser_Betrieb", "Ausser Betrieb")]
    [InlineData(false, FieldKeys.OperatingStatus, "tot", "Tot/Aufgehoben, verfüllt")]
    [InlineData(false, FieldKeys.OperatingStatus, "unbekannt", "Unbekannt")]
    [InlineData(false, FieldKeys.PositionAccuracy, "genau", "Genau")]
    [InlineData(false, FieldKeys.RehabilitationNeed, "mittelfristig", "Mittelfristig")]
    [InlineData(false, FieldKeys.RehabilitationNeed, "Saniert", "Saniert")]
    [InlineData(false, FieldKeys.UsageType, "Niederschlagsabwasser", "Regenabwasser")]
    [InlineData(false, FieldKeys.UsageType, "Regenwasser", "Regenabwasser")]
    [InlineData(false, FieldKeys.UsageType, "Bachwasser", "Bachabwasser")]
    [InlineData(false, FieldKeys.UsageType, "entlastetes Mischabwasser", "Entlastetes Mischabwasser")]
    [InlineData(false, FieldKeys.UsageType, "Schmutzwasser", "Schmutzabwasser")]
    [InlineData(false, FieldKeys.ProfileType, "Kreisprofil", "Kreisprofil (K)")]
    [InlineData(false, FieldKeys.ProfileType, "offenes_Profil", "Offenes Profil (OP)")]
    [InlineData(false, FieldKeys.ProfileType, "Spezialprofil", "Spezialprofil (S)")]
    [InlineData(false, FieldKeys.ProfileType, "Maulprofil", "Maulprofil (E)")]
    [InlineData(false, FieldKeys.ProfileType, "K", "Kreisprofil (K)")]
    [InlineData(false, FieldKeys.ProfileType, "Anderes (A)", "Andere (A)")]
    [InlineData(false, FieldKeys.HydraulicFunction, "Duekerleitung", "Dükerleitung")]
    [InlineData(false, FieldKeys.HydraulicFunction, "Spuelleitung", "Spülleitung")]
    [InlineData(false, FieldKeys.ConnectionType, "spiegelgeschweisst", "Spiegelgeschweisst")]
    [InlineData(false, FieldKeys.ConnectionType, "Ueberschiebmuffen", "Überschiebmuffen")]
    [InlineData(false, FieldKeys.BeddingEncasement, "in_Kanal_aufgehaengt", "In Kanal aufgehängt")]
    [InlineData(false, FieldKeys.BeddingEncasement, "SIA_Typ1", "SIA Typ1")]
    [InlineData(true, "Funktion", "Kontroll_Einsteigschacht", "Kontrollschacht")]
    [InlineData(true, "Funktion", "Pumpwerk", "Pumpenschacht")]
    [InlineData(true, "Funktion", "Oelabscheider", "Ölabscheider")]
    [InlineData(true, FieldKeys.PositionAccuracy, "ungenau", "Ungenau")]
    public void Bisherige_schreibweise_wird_zum_webgis_begriff(bool schacht, string feld, string bisher, string webgis)
        => Assert.Equal(webgis, WebGisBegriffe.Normalisieren(schacht, feld, bisher));

    [Theory]
    [InlineData(false, FieldKeys.OperatingStatus, "weitere")]
    [InlineData(false, FieldKeys.HydraulicFunction, "Versickerungsleitung")]
    [InlineData(true, "Funktion", "Absturzbauwerk")]
    [InlineData(true, "Funktion", "Trennbauwerk")]
    [InlineData(true, "Funktion", "Be-/Entlüftung")]
    [InlineData(true, "Funktion", "Sickerschacht")]
    [InlineData(true, "Funktion", "Spezialbauwerk")]
    public void Wert_ohne_webgis_gegenstueck_bleibt_unveraendert(bool schacht, string feld, string wert)
    {
        Assert.Equal(wert, WebGisBegriffe.Normalisieren(schacht, feld, wert));
        Assert.False(WebGisBegriffe.Fuer(schacht, feld)!.Kennt(wert));
    }

    [Fact]
    public void Webgis_begriff_bleibt_zeichengenau_und_leer_bleibt_leer()
    {
        var status = WebGisBegriffe.Fuer(false, FieldKeys.OperatingStatus)!;
        Assert.Equal("Tot/Aufgehoben, verfüllt", status.Normalisieren("Tot/Aufgehoben, verfüllt"));
        Assert.Equal("", status.Normalisieren("   "));
        Assert.True(status.Kennt(""));
        Assert.True(status.Kennt("In Betrieb"));
        Assert.False(status.Kennt("in_Betrieb"));
    }

    [Fact]
    public void Listen_kommen_aus_dem_webgis_katalog_ohne_leereintrag()
    {
        Assert.Equal(["Unbekannt", "In Betrieb", "Ausser Betrieb", "Tot/Aufgehoben, verfüllt"],
            WebGisBegriffe.Fuer(false, FieldKeys.OperatingStatus)!.Werte);
        Assert.Equal(["", "Unbekannt", "Ungenau", "Genau"], WebGisBegriffe.Fuer(true, FieldKeys.PositionAccuracy)!.Auswahl);
        Assert.Contains("Pumpenschacht", WebGisBegriffe.Fuer(true, "Funktion")!.Werte);
        Assert.DoesNotContain("", WebGisBegriffe.Fuer(false, FieldKeys.ConnectionType)!.Werte);
        Assert.Null(WebGisBegriffe.Fuer(false, FieldKeys.PipeMaterial)); // Material: Schritt B
    }

    [Fact]
    public void Keine_zwei_webgis_begriffe_einer_liste_falten_gleich()
    {
        foreach (var schacht in new[] { false, true })
        foreach (var feld in schacht ? WebGisBegriffe.SchachtFelder.Append(WebGisBegriffe.SchachtFunktion) : WebGisBegriffe.HaltungFelder)
        {
            var werte = WebGisBegriffe.Fuer(schacht, feld)!.Werte;
            var doppelt = werte.GroupBy(WebGisBegriffe.Falte).Where(g => g.Count() > 1).Select(g => string.Join("/", g)).ToList();
            Assert.True(doppelt.Count == 0, $"{feld}: {string.Join(", ", doppelt)}");
        }
    }
}
```

- [ ] **Step 2: Test laufen lassen → rot (Typ fehlt)**

Run: `dotnet test tests/AuswertungPro.Next.Infrastructure.Tests --filter "FullyQualifiedName~WebGisBegriffeTests" -o .tmp/testout-begriffe`
Expected: Build-Fehler «WebGisBegriffe does not exist».

- [ ] **Step 3: `WebGisWerteliste.cs` schreiben**

```csharp
using System.Collections.ObjectModel;

namespace AuswertungPro.Next.Domain.Models;

/// <summary>
/// Eine Auswahlliste des WebGIS (GEONIS Uri), wie sie die Maske zeigt. Gespeichert wird in
/// SewerStudio genau eine dieser Beschriftungen (Entscheid Pascal 23.09.2026): Was hier steht,
/// geht so in die Trigonet-Datenbank.
///
/// <see cref="Normalisieren"/> erkennt Alt- und Importschreibweisen (gefaltet, ueber wenige
/// belegte Aliase oder ueber das bestehende Vokabular als Vorstufe). Was keiner Beschriftung
/// eindeutig entspricht, bleibt UNVERAENDERT stehen — nie geraten, nie geloescht.
/// </summary>
public sealed class WebGisWerteliste
{
    private readonly IReadOnlyDictionary<string, string> _aliase;
    private readonly Func<string?, string>? _vorstufe;

    public WebGisWerteliste(IEnumerable<string> beschriftungen,
        IReadOnlyDictionary<string, string>? aliase = null, Func<string?, string>? vorstufe = null)
    {
        ArgumentNullException.ThrowIfNull(beschriftungen);
        Werte = new ReadOnlyCollection<string>(beschriftungen
            .Where(b => !string.IsNullOrWhiteSpace(b)).Distinct(StringComparer.Ordinal).ToList());
        Auswahl = new ReadOnlyCollection<string>(new[] { "" }.Concat(Werte).ToList());
        var gefaltet = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (von, nach) in aliase ?? new Dictionary<string, string>())
        {
            if (!Werte.Contains(nach, StringComparer.Ordinal))
                throw new InvalidOperationException($"Alias «{von}» zeigt auf «{nach}», das nicht in der WebGIS-Liste steht.");
            gefaltet[WebGisBegriffe.Falte(von)] = nach;
        }
        _aliase = gefaltet;
        _vorstufe = vorstufe;
    }

    /// <summary>Die Beschriftungen des WebGIS, in der Reihenfolge der Maske, ohne Leereintrag.</summary>
    public IReadOnlyList<string> Werte { get; }

    /// <summary>Leer plus <see cref="Werte"/> — fuer die Auswahl im Programm.</summary>
    public IReadOnlyList<string> Auswahl { get; }

    /// <summary>True fuer leer oder eine Beschriftung der Liste (zeichengenau).</summary>
    public bool Kennt(string? wert)
    {
        var text = (wert ?? "").Trim();
        return text.Length == 0 || Werte.Contains(text, StringComparer.Ordinal);
    }

    /// <summary>Die WebGIS-Beschriftung zum Wert, "" fuer leer, sonst der Text unveraendert.</summary>
    public string Normalisieren(string? wert)
    {
        var text = (wert ?? "").Trim();
        if (text.Length == 0) return "";
        if (Finde(text) is { } direkt) return direkt;
        if (_vorstufe is not null)
        {
            var vor = (_vorstufe(text) ?? "").Trim();
            if (vor.Length > 0 && !string.Equals(vor, text, StringComparison.Ordinal) && Finde(vor) is { } ueber)
                return ueber;
        }
        return text;
    }

    private string? Finde(string text)
    {
        if (Werte.Contains(text, StringComparer.Ordinal)) return text;
        var gefaltet = WebGisBegriffe.Falte(text);
        var treffer = Werte.Where(w => WebGisBegriffe.Falte(w) == gefaltet).Take(2).ToList();
        if (treffer.Count == 1) return treffer[0];
        return _aliase.TryGetValue(gefaltet, out var alias) ? alias : null;
    }
}
```

- [ ] **Step 4: `WebGisBegriffe.cs` schreiben**

```csharp
using System.Text.RegularExpressions;

namespace AuswertungPro.Next.Domain.Models;

/// <summary>
/// Die WebGIS-Auswahllisten der Felder, die Schritt A auf WebGIS-Begriffe umstellt
/// (Plan docs/superpowers/plans/2026-09-23-webgis-begriffe-schritt-a.md). Quelle ist der
/// eingebettete Objektaktenkatalog, aus den WebGIS-Masken erhoben — keine zweite Liste.
/// Material und FunktionHierarchisch folgen in Schritt B und C.
/// </summary>
public static class WebGisBegriffe
{
    // Vor den Listen: statische Felder werden in Quelltext-Reihenfolge initialisiert.
    private static readonly Regex KuerzelAmEnde = new(@"\s*\([A-Za-z]{1,4}\)\s*$", RegexOptions.Compiled);

    public const string SchachtFunktion = "Funktion";

    public static IReadOnlyList<string> HaltungFelder { get; } =
    [
        FieldKeys.OperatingStatus, FieldKeys.PositionAccuracy, FieldKeys.HydraulicFunction, FieldKeys.ConnectionType,
        FieldKeys.BeddingEncasement, FieldKeys.RehabilitationNeed, FieldKeys.UsageType, FieldKeys.ProfileType,
    ];

    /// <summary>Schachtfelder ohne die Funktion (die nur beim Normschacht umgestellt wird).</summary>
    public static IReadOnlyList<string> SchachtFelder { get; } =
    [
        FieldKeys.OperatingStatus, FieldKeys.PositionAccuracy, FieldKeys.RehabilitationNeed, FieldKeys.UsageType,
    ];

    private static readonly Lazy<IReadOnlyDictionary<string, WebGisWerteliste>> Haltung = new(() =>
        new Dictionary<string, WebGisWerteliste>(StringComparer.Ordinal)
        {
            [FieldKeys.OperatingStatus] = Liste("haltung-C04", StatusAliase),
            [FieldKeys.PositionAccuracy] = Liste("haltung-C13"),
            [FieldKeys.HydraulicFunction] = Liste("haltung-C03"),
            [FieldKeys.ConnectionType] = Liste("haltung-C18"),
            [FieldKeys.BeddingEncasement] = Liste("haltung-C17"),
            [FieldKeys.RehabilitationNeed] = Liste("haltung-C28"),
            [FieldKeys.UsageType] = Liste("haltung-C01", NutzungsartAliase, NutzungsartVokabular.Normalisieren),
            [FieldKeys.ProfileType] = Liste("haltung-C07", ProfiltypAliase, ProfiltypVokabular.Normalisieren),
        });

    private static readonly Lazy<IReadOnlyDictionary<string, WebGisWerteliste>> Schacht = new(() =>
        new Dictionary<string, WebGisWerteliste>(StringComparer.Ordinal)
        {
            [FieldKeys.OperatingStatus] = Liste("schacht-C05", StatusAliase),
            [FieldKeys.PositionAccuracy] = Liste("schacht-C11"),
            [FieldKeys.RehabilitationNeed] = Liste("schacht-C23"),
            [FieldKeys.UsageType] = Liste("schacht-C02", NutzungsartAliase, NutzungsartVokabular.Normalisieren),
            [SchachtFunktion] = Liste("schacht-C00", FunktionAliase, SchachtFunktionVokabular.Normalisieren),
        });

    // Nur belegte Paare mit gleicher Bedeutung. Alles andere bleibt stehen und wird markiert.
    private static IReadOnlyDictionary<string, string> StatusAliase => new Dictionary<string, string>
        { ["tot"] = "Tot/Aufgehoben, verfüllt" };
    // Das Vokabular fuehrt Regenabwasser schon als alte Schreibweise von Niederschlagsabwasser.
    private static IReadOnlyDictionary<string, string> NutzungsartAliase => new Dictionary<string, string>
        { ["Niederschlagsabwasser"] = "Regenabwasser", ["Bachwasser"] = "Bachabwasser" };
    private static IReadOnlyDictionary<string, string> ProfiltypAliase => new Dictionary<string, string>
        { ["Anderes"] = "Andere (A)", ["Anderes (A)"] = "Andere (A)" };
    // Das Vokabular fuehrt pumpenschacht schon als Schreibweise von Pumpwerk.
    private static IReadOnlyDictionary<string, string> FunktionAliase => new Dictionary<string, string>
        { ["Pumpwerk"] = "Pumpenschacht" };

    private static WebGisWerteliste Liste(string katalogId,
        IReadOnlyDictionary<string, string>? aliase = null, Func<string?, string>? vorstufe = null)
    {
        var katalog = FieldCatalog.Objektfelder.Auswahl(katalogId)
            ?? throw new InvalidOperationException($"WebGIS-Liste {katalogId} fehlt im Objektaktenkatalog.");
        return new WebGisWerteliste(katalog.Eintraege.Where(e => !e.Eigen).Select(e => e.Label), aliase, vorstufe);
    }

    /// <summary>Die WebGIS-Liste des Felds, oder null, wenn das Feld (noch) nicht umgestellt ist.</summary>
    public static WebGisWerteliste? Fuer(bool schacht, string feld)
        => (schacht ? Schacht.Value : Haltung.Value).GetValueOrDefault(feld);

    /// <summary>WebGIS-Beschriftung zum Wert; fuer nicht umgestellte Felder der Wert unveraendert.</summary>
    public static string Normalisieren(bool schacht, string feld, string? wert)
        => Fuer(schacht, feld)?.Normalisieren(wert) ?? (wert ?? "");

    /// <summary>
    /// Vergleichsform: Kuerzel in Klammern am Ende weg, Unterstrich zu Leerzeichen, ae/oe/ue zu
    /// Umlaut, Leerraum zusammengezogen, klein. Dieselbe Regel wie beim Senden (WebGisHandwertKarte).
    /// </summary>
    public static string Falte(string? s)
    {
        var t = (s ?? string.Empty).Trim();
        t = KuerzelAmEnde.Replace(t, "");
        t = t.Replace('_', ' ');
        t = t.Replace("ae", "ä").Replace("oe", "ö").Replace("ue", "ü");
        t = Regex.Replace(t, @"\s+", " ");
        return t.Trim().ToLowerInvariant();
    }
}
```

- [ ] **Step 5: `WebGisHandwertKarte.Falte` delegiert** — in `src/AuswertungPro.Next.Application/WebGis/WebGisHandwertKarte.cs` den Rumpf von `Falte` ersetzen durch `=> WebGisBegriffe.Falte(s);` und das Feld `KuerzelAmEnde` entfernen, falls es sonst nicht mehr gebraucht wird. Achtung: die Domain-Regex erlaubt 1–4 Buchstaben (vorher 1–3). Bestehende WebGIS-Tests müssen grün bleiben.

- [ ] **Step 6: Tests grün**

Run: `dotnet test tests/AuswertungPro.Next.Infrastructure.Tests --filter "FullyQualifiedName~WebGis" -o .tmp/testout-begriffe`
Expected: alle grün. Scheitert `Keine_zwei_webgis_begriffe_einer_liste_falten_gleich`, NICHT die Faltung lockern: den Befund melden (zwei WebGIS-Werte wären sonst nicht unterscheidbar).

- [ ] **Step 7: Commit** (nur wenn Pascal Commits freigegeben hat; sonst Schritt überspringen und am Ende gesammelt melden)

```bash
git add src/AuswertungPro.Next.Domain/Models/WebGisWerteliste.cs src/AuswertungPro.Next.Domain/Models/WebGisBegriffe.cs src/AuswertungPro.Next.Application/WebGis/WebGisHandwertKarte.cs tests/AuswertungPro.Next.Infrastructure.Tests/WebGis/WebGisBegriffeTests.cs
git commit -m "WebGIS-Begriffe: Wertelisten aus dem WebGIS-Katalog (Schritt A)"
```

---

### Task 2: Normseite versteht WebGIS-Beschriftungen

**Files:**
- Modify: `src/AuswertungPro.Next.Domain/Models/SiaKanalVokabular.cs` (`SiaWerteliste.NachNorm`, `Aliase`, Status-Alias)
- Modify: `src/AuswertungPro.Next.Domain/Models/NutzungsartVokabular.cs` (Gelesen «bachabwasser»)
- Modify: `src/AuswertungPro.Next.Domain/Models/ProfiltypVokabular.cs` (Zuordnungen «Maulprofil (E)», «Andere (A)»)
- Test: `tests/AuswertungPro.Next.Infrastructure.Tests/WebGis/WebGisBegriffeNormTests.cs`

**Interfaces:**
- Consumes: `WebGisBegriffe.Fuer`, `HaltungFelder` (Task 1).
- Produces: `SiaWerteliste.Aliase` (init, `IReadOnlyDictionary<string,string>`, OrdinalIgnoreCase); `NachNorm` erkennt Umlaute (ü→ue …) und Kürzel in Klammern.

- [ ] **Step 1: Failing test**

```csharp
using AuswertungPro.Next.Application.Xtf;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Infrastructure.Tests.WebGis;

/// <summary>
/// Der SIA405-Export (und ueber ihn DSS, VSA, Farben) muss die neu gespeicherten WebGIS-Begriffe
/// verstehen. Ohne Norm sind nur die hier namentlich genannten, reinen WebGIS-Werte.
/// </summary>
public sealed class WebGisBegriffeNormTests
{
    private const string Modell = "SIA405_ABWASSER_2020_LV95";

    public static TheoryData<string, string, string[]> Felder() => new()
    {
        { FieldKeys.OperatingStatus, "Status", [] },
        { FieldKeys.PositionAccuracy, "Lagebestimmung", [] },
        { FieldKeys.HydraulicFunction, "FunktionHydraulisch",
            ["Belagsrinne Wasserschale", "Entwaesserungsgraben befestigt", "Entwaesserungsgraben unbefestigt", "Schlitzrinne", "Wasserrinne mit Rost"] },
        { FieldKeys.ConnectionType, "Verbindungsart",
            ["Stumpfschweissmuffe", "Führungsbolzen", "Manschette einbetoniert", "Schweissmuffen", "Stahlmuffen"] },
        { FieldKeys.BeddingEncasement, "Bettung_Umhuellung", ["Kies", "In Kulisse", "Pressvortrieb"] },
        { FieldKeys.RehabilitationNeed, "Sanierungsbedarf", ["Saniert"] },
        { FieldKeys.UsageType, "Nutzungsart_Ist", ["Bergwasser", "Strassenabwasser"] },
        { FieldKeys.ProfileType, "Profiltyp", [] },
    };

    [Theory]
    [MemberData(nameof(Felder))]
    public void Jeder_webgis_begriff_mit_norm_wird_exportiert(string feld, string xtfName, string[] ohneNorm)
    {
        var fehlend = WebGisBegriffe.Fuer(false, feld)!.Werte
            .Where(w => string.IsNullOrEmpty(XtfStammdatenPlanBuilder.NachXtfWert(xtfName, w, Modell)))
            .ToList();
        Assert.Equal(ohneNorm.OrderBy(x => x, StringComparer.Ordinal), fehlend.OrderBy(x => x, StringComparer.Ordinal));
    }

    [Theory]
    [InlineData("Tot/Aufgehoben, verfüllt", "tot")]
    [InlineData("In Betrieb", "in_Betrieb")]
    public void Status_aus_dem_webgis_wird_zum_normwert(string webgis, string norm)
        => Assert.Equal(norm, SiaKanalVokabular.Status.NachNorm(webgis));

    [Theory]
    [InlineData("Dükerleitung", "Duekerleitung")]
    [InlineData("Überschiebmuffen", "Ueberschiebmuffen")]
    [InlineData("In Kanal aufgehängt", "in_Kanal_aufgehaengt")]
    public void Umlaute_der_webgis_begriffe_werden_fuer_die_norm_umgeschrieben(string webgis, string norm)
    {
        var treffer = new[] { SiaKanalVokabular.FunktionHydraulisch, SiaKanalVokabular.Verbindungsart, SiaKanalVokabular.BettungUmhuellung }
            .Select(l => l.NachNorm(webgis)).FirstOrDefault(n => n is not null);
        Assert.Equal(norm, treffer);
    }

    [Theory]
    [InlineData("Maulprofil (E)", "Maulprofil")]
    [InlineData("Andere (A)", "Spezialprofil")]
    public void Profil_aus_dem_webgis_hat_eine_norm(string webgis, string norm)
        => Assert.Equal(norm, ProfiltypVokabular.NachNorm(webgis));

    [Fact]
    public void Bachabwasser_zaehlt_wie_bachwasser()
        => Assert.Equal("Bachwasser", NutzungsartVokabular.Normalisieren("Bachabwasser"));
}
```

- [ ] **Step 2: rot laufen lassen**

Run: `dotnet test tests/AuswertungPro.Next.Infrastructure.Tests --filter "FullyQualifiedName~WebGisBegriffeNormTests" -o .tmp/testout-begriffe`
Expected: FAIL (u. a. Status «Tot/…» → null, «Dükerleitung» → null).

- [ ] **Step 3: `SiaWerteliste` erweitern** — in `SiaKanalVokabular.cs`:

```csharp
    private static readonly System.Text.RegularExpressions.Regex KuerzelAmEnde =
        new(@"\s*\([A-Za-z]{1,4}\)\s*$", System.Text.RegularExpressions.RegexOptions.Compiled);

    /// <summary>
    /// Belegte Beschriftungen des WebGIS, die ohne Regel keinem Normwert entsprechen
    /// (Status «Tot/Aufgehoben, verfüllt» -> tot). Schluessel ohne Gross/Klein.
    /// </summary>
    public IReadOnlyDictionary<string, string> Aliase { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    public string? NachNorm(string? wert)
    {
        var text = (wert ?? "").Trim();
        if (text.Length == 0)
            return null;
        if (Aliase.TryGetValue(text, out var alias))
            return Werte.FirstOrDefault(w => string.Equals(w, alias, StringComparison.Ordinal));

        // Seit 23.09.2026 speichert SewerStudio die WebGIS-Beschriftung («Dükerleitung»,
        // «Kreisprofil (K)»). Fuer die Norm Kuerzel weg und Umlaute ausschreiben.
        var ohneKuerzel = KuerzelAmEnde.Replace(text, "").Trim();
        foreach (var kandidat in new[] { text, ohneKuerzel, OhneUmlaute(ohneKuerzel) })
            if (Treffer(kandidat) is { } norm)
                return norm;
        return null;
    }

    private string? Treffer(string text)
    {
        var mitUnterstrich = text.Replace(' ', '_');
        var mitPunkt = text.Replace(' ', '.');
        return Werte.FirstOrDefault(w =>
            string.Equals(w, text, StringComparison.OrdinalIgnoreCase)
            || string.Equals(w, mitUnterstrich, StringComparison.OrdinalIgnoreCase)
            || string.Equals(w, mitPunkt, StringComparison.OrdinalIgnoreCase));
    }

    private static string OhneUmlaute(string s) => s
        .Replace("ä", "ae").Replace("ö", "oe").Replace("ü", "ue")
        .Replace("Ä", "Ae").Replace("Ö", "Oe").Replace("Ü", "Ue");
```

und die Statusliste:

```csharp
    public static readonly SiaWerteliste Status = new(
        "ausser_Betrieb", "in_Betrieb", "tot", "unbekannt", "weitere")
    {
        Aliase = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Tot/Aufgehoben, verfüllt"] = "tot",
        },
    };
```

- [ ] **Step 4: Vokabulare** — `NutzungsartVokabular.cs`: `new(["bachwasser", "bachabwasser"], "Bachwasser", "Bachwasser", "Bachwasser"),`. `ProfiltypVokabular.cs` in `Zuordnungen` ergänzen:

```csharp
            ["Maulprofil (E)"] = ("Maulprofil", "Maulprofil"), // so steht es im WebGIS (Kuerzel E)
            ["Andere (A)"] = ("Spezialprofil", "Spezialprofil"),
```

- [ ] **Step 5: grün** — gleicher Befehl wie Step 2, dazu `--filter "FullyQualifiedName~Vokabular|FullyQualifiedName~Xtf|FullyQualifiedName~Dss|FullyQualifiedName~WebGis"`. Weicht in `Jeder_webgis_begriff_mit_norm_wird_exportiert` die Menge ohne Norm ab: NICHT die erwartete Liste anpassen, sondern den Unterschied melden (neue Normzuordnung wäre ein fachlicher Entscheid).

- [ ] **Step 6: Commit** (wie Task 1): `WebGIS-Begriffe: Norm-Export versteht WebGIS-Beschriftungen`

---

### Task 3: Bestandsprojekte beim Laden und Speichern anheben

**Files:**
- Modify: `src/AuswertungPro.Next.Infrastructure/Projects/ProjectVocabularyNormalizer.cs`
- Test: `tests/AuswertungPro.Next.Infrastructure.Tests/Projects/ProjectVocabularyNormalizerTests.cs`

**Interfaces:**
- Consumes: `WebGisBegriffe.Normalisieren/HaltungFelder/SchachtFelder/SchachtFunktion` (Task 1), `AbwasserbauwerkVokabular.Klasse`, `SchachtFeldnamen.Feld`.

- [ ] **Step 1: Failing tests anfügen**

```csharp
    [Fact]
    public void Haltungsfelder_werden_auf_webgis_begriffe_gehoben_ohne_herkunft_zu_aendern()
    {
        var project = new Project();
        var h = new HaltungRecord();
        h.SetFieldValue(FieldKeys.OperatingStatus, "in_Betrieb", FieldSource.Xtf, false);
        h.Fields[FieldKeys.UsageType] = "Niederschlagsabwasser";
        h.Fields[FieldKeys.ProfileType] = "Kreisprofil";
        h.Fields[FieldKeys.PositionAccuracy] = "genau";
        project.Data.Add(h);
        var metaVorher = h.FieldMeta[FieldKeys.OperatingStatus];

        ProjectVocabularyNormalizer.Normalize(project);

        Assert.Equal("In Betrieb", h.GetFieldValue(FieldKeys.OperatingStatus));
        Assert.Equal("Regenabwasser", h.GetFieldValue(FieldKeys.UsageType));
        Assert.Equal("Kreisprofil (K)", h.GetFieldValue(FieldKeys.ProfileType));
        Assert.Equal("Genau", h.GetFieldValue(FieldKeys.PositionAccuracy));
        Assert.Same(metaVorher, h.FieldMeta[FieldKeys.OperatingStatus]);
        Assert.False(h.FieldMeta[FieldKeys.OperatingStatus].UserEdited);
    }

    [Fact]
    public void Wert_ohne_webgis_begriff_bleibt_stehen()
    {
        var project = new Project();
        var h = new HaltungRecord();
        h.Fields[FieldKeys.OperatingStatus] = "weitere";
        project.Data.Add(h);

        Assert.Equal(0, ProjectVocabularyNormalizer.Normalize(project));
        Assert.Equal("weitere", h.GetFieldValue(FieldKeys.OperatingStatus));
    }

    [Fact]
    public void Normschacht_funktion_wird_webgis_begriff_spezialbauwerk_nicht()
    {
        var project = new Project();
        var norm = new SchachtRecord();
        norm.Fields["Funktion"] = "Pumpwerk";
        norm.Fields["Status"] = "in_Betrieb";
        var spezial = new SchachtRecord();
        spezial.Fields[FieldKeys.ShaftStructureType] = "Spezialbauwerk";
        spezial.Fields["Funktion"] = "Pumpwerk";
        project.SchaechteData.Add(norm);
        project.SchaechteData.Add(spezial);

        ProjectVocabularyNormalizer.Normalize(project);

        Assert.Equal("Pumpenschacht", norm.GetFieldValue("Funktion"));
        Assert.Equal("In Betrieb", norm.GetFieldValue("Status"));
        Assert.Equal("Pumpwerk", spezial.GetFieldValue("Funktion"));
    }
```

Bestehende Tests der Datei, die nach dem Laden «Kontrollschacht»/«Kreisprofil»/«Niederschlagsabwasser» erwarten: «Kontrollschacht» bleibt (WebGIS-Begriff), «Kreisprofil» → «Kreisprofil (K)», «Niederschlagsabwasser» → «Regenabwasser» — Erwartung dort anpassen und im Testnamen/Kommentar den Grund (Entscheid 23.09.2026) nennen.

- [ ] **Step 2: rot**

Run: `dotnet test tests/AuswertungPro.Next.Infrastructure.Tests --filter "FullyQualifiedName~ProjectVocabularyNormalizerTests" -o .tmp/testout-begriffe`

- [ ] **Step 3: Umsetzung** — Schleifen in `Normalize` ersetzen:

```csharp
        foreach (var record in project.Data ?? [])
        {
            if (record is null) continue;
            // Material folgt in Schritt B; bis dahin die bisherige Normschreibweise.
            geaendert += Hebe(record.Fields, FieldKeys.PipeMaterial, MaterialVokabular.Normalisieren);
            // Seit 23.09.2026 (Entscheid Pascal): die WebGIS-Beschriftung, zeichengenau.
            foreach (var feld in WebGisBegriffe.HaltungFelder)
                geaendert += Hebe(record.Fields, feld, w => WebGisBegriffe.Normalisieren(false, feld, w));
        }

        foreach (var record in project.SchaechteData ?? [])
        {
            if (record is null) continue;
            geaendert += Hebe(record.Fields, "Material", SchachtMaterialVokabular.Normalisieren);
            geaendert += Hebe(record.Fields, FieldKeys.ShaftShape, SchachtformVokabular.Normalisieren);
            foreach (var feld in WebGisBegriffe.SchachtFelder)
                geaendert += Hebe(record.Fields, SchachtFeldnamen.Feld(record, feld),
                    w => WebGisBegriffe.Normalisieren(true, feld, w));

            // Nur der Normschacht hat die WebGIS-Funktionsliste; Spezialbauwerk und
            // Versickerungsanlage behalten ihre Normfunktion, bis deren Listen erhoben sind.
            var funktionsfeld = SchachtFeldnamen.Feld(record, WebGisBegriffe.SchachtFunktion);
            var art = record.GetFieldValue(SchachtFeldnamen.Feld(record, FieldKeys.ShaftStructureType));
            var normschacht = AbwasserbauwerkVokabular.Klasse(art, record.GetFieldValue(funktionsfeld)) == "Normschacht";
            geaendert += normschacht
                ? Hebe(record.Fields, funktionsfeld, w => WebGisBegriffe.Normalisieren(true, WebGisBegriffe.SchachtFunktion, w))
                : Hebe(record.Fields, funktionsfeld, SchachtFunktionVokabular.Normalisieren);
        }
```

Den Klassenkommentar um den Absatz ergänzen: «Seit 2026-09-23 hebt er die Felder aus WebGisBegriffe auf die WebGIS-Beschriftung (Schritt A).»

- [ ] **Step 4: grün**, danach Projekt-Tests breit: `--filter "FullyQualifiedName~Project"`.
- [ ] **Step 5: Commit**: `WebGIS-Begriffe: Bestandsprojekte beim Laden anheben`

---

### Task 4: Auswahllisten zeigen nur WebGIS-Begriffe, Altwerte bleiben sichtbar

**Files:**
- Modify: `src/AuswertungPro.Next.Domain/Models/FieldCatalog.cs` (`GetComboItems`, Einträge der Schritt-A-Felder aus `ComboItems` entfernen)
- Modify: `src/AuswertungPro.Next.UI/ViewModels/Pages/DataPageViewModel.Normoptionen.cs`
- Modify: `src/AuswertungPro.Next.UI/DataPage/SanierungsbedarfOptionen.cs`
- Modify: `src/AuswertungPro.Next.UI/ViewModels/Pages/SchaechtePageViewModel.cs:316` (`SchachtFunktionOptions`)
- Modify: `src/AuswertungPro.Next.UI/DataPage/SchachtNormoptionen.cs`
- Modify: `src/AuswertungPro.Next.UI/DataPage/SchaechteColumnPolicy.cs` (`ResolveOptionField`: Nutzungsart, Lagebestimmung)
- Modify: `src/AuswertungPro.Next.UI/DataPage/DataPageDropdownOptionSynchronizer.cs` + `ViewModels/Pages/DataPageViewModel.DropdownOptions.cs:203`
- Modify: `tests/AuswertungPro.Next.Infrastructure.Tests/DropdownExportierbarkeitTests.cs`
- Test: `tests/AuswertungPro.Next.UI.Tests/WebGisAuswahlTests.cs`

**Interfaces:**
- Consumes: `WebGisBegriffe.Fuer(...).Auswahl/Werte` (Task 1).
- Produces: `FieldCatalog.GetComboItems(feld)` liefert für Schritt-A-Haltungsfelder die WebGIS-`Auswahl`. `DataPageDropdownOptionSets` hat zusätzlich `IReadOnlyDictionary<string, ObservableCollection<string>> WebGisListen` (Default leer).

- [ ] **Step 1: Failing tests (UI.Tests)**

```csharp
using System.Collections.ObjectModel;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.DataPage;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>Auswahllisten zeigen die WebGIS-Begriffe; ein Altwert verschwindet nie aus der Anzeige.</summary>
public sealed class WebGisAuswahlTests
{
    [Fact]
    public void Haltungsauswahl_ist_die_webgis_liste()
    {
        Assert.Equal(["", "Unbekannt", "In Betrieb", "Ausser Betrieb", "Tot/Aufgehoben, verfüllt"],
            FieldCatalog.GetComboItems(FieldKeys.OperatingStatus));
        Assert.Contains("Kreisprofil (K)", FieldCatalog.GetComboItems(FieldKeys.ProfileType));
        Assert.Contains("Regenabwasser", FieldCatalog.GetComboItems(FieldKeys.UsageType));
        Assert.DoesNotContain("Niederschlagsabwasser", FieldCatalog.GetComboItems(FieldKeys.UsageType));
        Assert.Equal(WebGisBegriffe.Fuer(false, FieldKeys.RehabilitationNeed)!.Auswahl, SanierungsbedarfOptionen.Alle);
    }

    [Fact]
    public void Normschacht_funktion_ist_die_webgis_liste_und_altwert_bleibt_sichtbar()
    {
        var liste = SchachtNormoptionen.Funktion("Normschacht", "Absturzbauwerk");
        Assert.Contains("Pumpenschacht", liste);
        Assert.Contains("Absturzbauwerk", liste); // Altwert: sonst waere das Feld leer und beim ersten Klick weg
        Assert.DoesNotContain("Pumpwerk", SchachtNormoptionen.Funktion("Normschacht", "Pumpenschacht"));
    }

    [Fact]
    public void Altwert_einer_haltung_wird_der_auswahl_angehaengt()
    {
        var status = new ObservableCollection<string>(FieldCatalog.GetComboItems(FieldKeys.OperatingStatus));
        var h = new HaltungRecord();
        h.Fields[FieldKeys.OperatingStatus] = "weitere";
        var sets = new DataPageDropdownOptionSets(new(), new(), new(), new(), new())
        {
            WebGisListen = new Dictionary<string, ObservableCollection<string>> { [FieldKeys.OperatingStatus] = status },
        };

        DataPageDropdownOptionSynchronizer.SyncFromRecords([h], sets);

        Assert.Equal("weitere", status[^1]);
    }

    [Fact]
    public void Schachtspalten_nutzungsart_und_lagebestimmung_sind_auswahlfelder()
    {
        Assert.Equal(FieldKeys.UsageType, SchaechteColumnPolicy.ResolveOptionField("Nutzungsart"));
        Assert.Equal(FieldKeys.PositionAccuracy, SchaechteColumnPolicy.ResolveOptionField("Lagebestimmung"));
    }
}
```

- [ ] **Step 2: rot** — `dotnet test tests/AuswertungPro.Next.UI.Tests --filter "FullyQualifiedName~WebGisAuswahlTests" -o .tmp/testout-begriffe-ui`

- [ ] **Step 3: `FieldCatalog.GetComboItems`**

```csharp
    // Seit 23.09.2026 (Entscheid Pascal): Felder aus WebGisBegriffe bieten die WebGIS-Liste an.
    // Bewusst hier und nicht in ComboItems: WebGisBegriffe liest den Objektaktenkatalog, und der
    // statische Aufbau von FieldCatalog darf ihn nicht vorzeitig anfassen.
    public static IReadOnlyList<string> GetComboItems(string fieldName)
        => WebGisBegriffe.Fuer(false, fieldName)?.Auswahl
           ?? (ComboItems.TryGetValue(fieldName, out var items) ? items : Array.Empty<string>());
```

Aus `ComboItems` die Einträge `UsageType, ConnectionType, BeddingEncasement, ProfileType, HydraulicFunction, OperatingStatus, RehabilitationNeed, PositionAccuracy` entfernen (FunktionHierarchisch und PipeMaterial bleiben).

- [ ] **Step 4: UI-Listen**
  - `DataPageViewModel.Normoptionen.cs`: `public IReadOnlyList<string> NutzungsartOptions => FieldCatalog.GetComboItems(FieldKeys.UsageType);`
  - `SanierungsbedarfOptionen.cs`: `public static IReadOnlyList<string> Alle { get; } = FieldCatalog.GetComboItems(FieldKeys.RehabilitationNeed);` (Kommentar: die WebGIS-Liste enthält «Saniert»).
  - `SchaechtePageViewModel.cs:316`: `SchachtFunktionOptions = new ObservableCollection<string>(WebGisBegriffe.Fuer(true, WebGisBegriffe.SchachtFunktion)!.Auswahl);`
  - `SchachtNormoptionen.Funktion`:

```csharp
    internal static IReadOnlyList<string> Funktion(string? art, string? funktion)
    {
        IReadOnlyList<string> liste = AbwasserbauwerkVokabular.Klasse(art, funktion) switch
        {
            "Normschacht" => WebGisBegriffe.Fuer(true, WebGisBegriffe.SchachtFunktion)!.Auswahl,
            "Spezialbauwerk" => new[] { "" }.Concat(AbwasserbauwerkVokabular.Spezialfunktionen).ToArray(),
            _ => [""]
        };
        // Ein Altwert ausserhalb der Liste bleibt sichtbar: sonst zeigt das Feld leer an und der
        // erste Klick ersetzt ihn. Die Projektpruefung meldet ihn zum Korrigieren.
        var aktuell = (funktion ?? "").Trim();
        return aktuell.Length == 0 || liste.Contains(aktuell, StringComparer.Ordinal)
            ? liste : liste.Append(aktuell).ToArray();
    }
```

  - `SchaechteColumnPolicy.ResolveOptionField` nach der Status-Zeile: `if (normalized == "nutzungsart") return FieldKeys.UsageType;` und `if (normalized == "lagebestimmung") return FieldKeys.PositionAccuracy;`. Prüfen, dass `SchaechtePageViewModel` die Eigenschaften `NutzungsartOptions` und `LagebestimmungOptions` hat; sonst in `SchaechtePageViewModel.Normoptionen.cs` ergänzen: `public IReadOnlyList<string> NutzungsartOptions => WebGisBegriffe.Fuer(true, FieldKeys.UsageType)!.Auswahl;` und `public IReadOnlyList<string> LagebestimmungOptions => WebGisBegriffe.Fuer(true, FieldKeys.PositionAccuracy)!.Auswahl;`.

- [ ] **Step 5: Altwerte der Haltung** — `DataPageDropdownOptionSets` um `public IReadOnlyDictionary<string, ObservableCollection<string>> WebGisListen { get; init; } = new Dictionary<string, ObservableCollection<string>>();` ergänzen; in `SyncFromRecords` je Datensatz:

```csharp
            // WebGIS-Felder: ein Altwert ohne WebGIS-Begriff bleibt sichtbar statt leer.
            foreach (var (feld, liste) in options.WebGisListen)
                DropdownOptionList.AddIfMissing(liste, record.GetFieldValue(feld));
```

  In `DataPageViewModel.DropdownOptions.cs:203` die Sets mit `WebGisListen = new Dictionary<string, ObservableCollection<string>> { [FieldKeys.OperatingStatus] = StatusOptions, [FieldKeys.PositionAccuracy] = LagebestimmungOptions, [FieldKeys.HydraulicFunction] = FunktionHydraulischOptions, [FieldKeys.ConnectionType] = VerbindungsartOptions, [FieldKeys.BeddingEncasement] = BettungUmhuellungOptions, [FieldKeys.ProfileType] = ProfiltypOptions, [FieldKeys.RehabilitationNeed] = SanierungsbedarfOptions }` aufrufen. (`SanierungsbedarfOptions` ist eine `ObservableCollection<string>`, im Konstruktor aus `SanierungsbedarfOptionen.Alle` gebaut.)

- [ ] **Step 6: Wächter anpassen** — `DropdownExportierbarkeitTests`:
  - In `Jeder_waehlbare_Wert_findet_ein_Ziel_in_der_Datei` die reinen WebGIS-Werte ohne Norm namentlich ausnehmen, mit Beleg im Kommentar (WebGIS-Liste, SIA405 kennt sie nicht; Entscheid 23.09.2026):

```csharp
    private static readonly HashSet<string> BewussteAusnahmen = new(StringComparer.Ordinal)
    {
        "GFK", "Guss",
        // Reine WebGIS-Werte (Uri) ohne SIA405-Gegenstueck. Seit 23.09.2026 fuehrt SewerStudio
        // die WebGIS-Listen; der Export laesst sie weg und nennt sie im Bericht.
        "Belagsrinne Wasserschale", "Entwaesserungsgraben befestigt", "Entwaesserungsgraben unbefestigt",
        "Schlitzrinne", "Wasserrinne mit Rost",
        "Stumpfschweissmuffe", "Führungsbolzen", "Manschette einbetoniert", "Schweissmuffen", "Stahlmuffen",
        "Kies", "In Kulisse", "Pressvortrieb", "Saniert", "Bergwasser", "Strassenabwasser",
    };
```

  - `Schachtfelder()`: `SchachtFunktionVokabular.Auswahl` durch `WebGisBegriffe.Fuer(true, WebGisBegriffe.SchachtFunktion)!.Auswahl` ersetzen und die zwei reinen WebGIS-Funktionen `Absturzschacht`, `Trennschacht` im Test ausnehmen (gleicher Kommentar).
  - `Die_Urner_Profilformen_stehen_in_der_Haltungs_Auswahl`: erwartete Liste auf `["", "Unbekannt (U)", "Kreisprofil (K)", "Eiprofil (E)", "Maulprofil (E)", "Offenes Profil (OP)", "Rechteckprofil (R)", "Spezialprofil (S)", "Andere (A)"]`.

- [ ] **Step 7: grün** — UI-Tests `--filter "FullyQualifiedName~WebGisAuswahl|FullyQualifiedName~Schaechte|FullyQualifiedName~DataPage|FullyQualifiedName~Kurzansicht|FullyQualifiedName~RedesignSia|FullyQualifiedName~SchachtNorm"` und Infrastructure `--filter "FullyQualifiedName~Dropdown"`. Tests, die alte Listeninhalte festschreiben (z. B. «in_Betrieb» als Auswahl), auf die WebGIS-Liste umstellen und den Grund nennen.
- [ ] **Step 8: Commit**: `WebGIS-Begriffe: Auswahllisten aus dem WebGIS, Altwerte bleiben sichtbar`

---

### Task 5: Holen speichert den WebGIS-Text wörtlich

**Files:**
- Modify: `src/AuswertungPro.Next.Application/WebGis/WebGisImportWert.cs`
- Test: `tests/AuswertungPro.Next.Infrastructure.Tests/WebGis/WebGisImportWertTests.cs`

- [ ] **Step 1: Failing tests anfügen**

```csharp
    [Theory]
    [InlineData(WebGisObjektart.Haltung, "Status", "In Betrieb")]
    [InlineData(WebGisObjektart.Haltung, "Profiltyp", "Kreisprofil (K)")]
    [InlineData(WebGisObjektart.Haltung, "Nutzungsart", "Regenabwasser")]
    [InlineData(WebGisObjektart.Haltung, "FunktionHydraulisch", "Dükerleitung")]
    [InlineData(WebGisObjektart.Schacht, "Status", "Tot/Aufgehoben, verfüllt")]
    [InlineData(WebGisObjektart.Schacht, "Funktion", "Absturzschacht")]
    public void Webgis_begriff_wird_woertlich_uebernommen(WebGisObjektart art, string feld, string text)
    {
        Assert.Equal(text, WebGisImportWert.Zuordne(art, feld, text, out var hinweis));
        Assert.Null(hinweis);
    }

    [Fact]
    public void Text_ausserhalb_der_bekannten_webgis_liste_wird_gemeldet_nicht_eingetragen()
    {
        Assert.Null(WebGisImportWert.Zuordne(WebGisObjektart.Haltung, "Status", "Im Bau", out var hinweis));
        Assert.Contains("nicht in der bekannten WebGIS-Liste", hinweis);
    }

    [Fact]
    public void Unbekannt_fuellt_weiterhin_nichts()
    {
        Assert.Null(WebGisImportWert.Zuordne(WebGisObjektart.Haltung, "Status", "Unbekannt", out var hinweis));
        Assert.Null(hinweis);
    }
```

Bestehende Tests der Datei, die «in_Betrieb», «Kreisprofil», «PAA.Sammelkanal» (bleibt: Schritt C), «Niederschlagsabwasser» erwarten: für Schritt-A-Felder auf die WebGIS-Beschriftung umstellen.

- [ ] **Step 2: rot** — `--filter "FullyQualifiedName~WebGisImportWertTests"`
- [ ] **Step 3: Umsetzung** — in `Zuordne` direkt nach dem Zahlenblock (vor `var optionen = ...`):

```csharp
        // Schritt A (23.09.2026): Felder mit WebGIS-Liste speichern den WebGIS-Text woertlich.
        if (WebGisBegriffe.Fuer(art == WebGisObjektart.Schacht, feld) is { } liste)
        {
            if (liste.Kennt(text)) return text;
            hinweis = $"{feld}: «{text}» steht nicht in der bekannten WebGIS-Liste — nicht übernommen (Katalog prüfen).";
            return null;
        }
```

- [ ] **Step 4: grün** — `--filter "FullyQualifiedName~WebGis"` (alle WebGIS-Tests, inkl. `WebGisImportPlanBuilderTests`, `WebGisImportUseCaseTests`; deren Erwartung «in_Betrieb» → «In Betrieb»).
- [ ] **Step 5: Commit**: `WebGIS-Begriffe: Holen übernimmt den WebGIS-Text wörtlich`

---

### Task 6: Objektakte schreibt die Beschriftung unverändert

**Files:**
- Modify: `src/AuswertungPro.Next.Application/UseCases/Objektakten/ObjektaktenBearbeitung.cs:212-229`
- Test: `tests/AuswertungPro.Next.Infrastructure.Tests/WebGis/WebGisBegriffeObjektakteTests.cs`

- [ ] **Step 1: Failing test**

```csharp
using AuswertungPro.Next.Application.UseCases.Objektakten;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Infrastructure.Tests.WebGis;

public sealed class WebGisBegriffeObjektakteTests
{
    [Theory]
    [InlineData("haltung.usage", "Regenabwasser")]
    [InlineData("haltung.profile", "Kreisprofil (K)")]
    [InlineData("schacht.funktion", "Pumpenschacht")]
    [InlineData("haltung.status", "Tot/Aufgehoben, verfüllt")]
    public void Objektakte_schreibt_die_webgis_beschriftung_ins_speicherfeld(string feldId, string text)
        => Assert.Equal(text, ObjektaktenBearbeitung.Normalisiere(FieldCatalog.Objektfelder.Feld(feldId), text));

    [Fact]
    public void Zustandsklasse_bleibt_ziffer()
        => Assert.Equal("2", ObjektaktenBearbeitung.Normalisiere(FieldCatalog.Objektfelder.Feld("haltung.condition"), "Mittlere Mängel (Z2)"));
}
```

(`Normalisiere` ist `internal`; die Infrastructure-Tests sehen Application-Interna — sonst den Test in das Projekt legen, das `ObjektaktenBearbeitung`-Interna bereits testet.)

- [ ] **Step 2: rot**
- [ ] **Step 3: Umsetzung** — im `switch` die Zeilen `"haltung.usage"`, `"haltung.profile"`, `"schacht.funktion"` entfernen; Kommentar: «Seit 23.09.2026 ist die WebGIS-Beschriftung der gespeicherte Wert (Schritt A). Material folgt in Schritt B.»
- [ ] **Step 4: grün** — `--filter "FullyQualifiedName~Objektakt"`; Tests, die nach Akten-Eingabe «Kreisprofil»/«Niederschlagsabwasser»/«Pumpwerk» im Feld erwarten, umstellen.
- [ ] **Step 5: Commit**: `WebGIS-Begriffe: Objektakte speichert die WebGIS-Beschriftung`

---

### Task 7: QGIS und GeoShop liefern WebGIS-Begriffe

**Files:**
- Modify: `src/AuswertungPro.Next.Application/Lookup/QgisFeldKarte.cs` (`Wert`)
- Modify: `src/AuswertungPro.Next.Infrastructure/Lookup/GeoShopXtfZuordnung.cs` (Profiltyp-Zeile)
- Test: `tests/AuswertungPro.Next.Infrastructure.Tests/WebGis/WebGisBegriffeKatasterTests.cs`

- [ ] **Step 1: Failing test**

```csharp
using AuswertungPro.Next.Application.Lookup;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Infrastructure.Tests.WebGis;

/// <summary>GeoShop/QGIS liefern Normwerte; im Projekt steht danach der WebGIS-Begriff (sonst Scheinabweichungen im Vergleich).</summary>
public sealed class WebGisBegriffeKatasterTests
{
    [Fact]
    public void Kataster_normwerte_werden_webgis_begriffe()
    {
        var h = new QgisBauteil("H1", new Dictionary<string, string>
        {
            ["bw_status"] = "in_Betrieb", ["ha_lagebestimmung"] = "genau", ["ka_nutzungsart_ist"] = "Niederschlagsabwasser",
            ["ka_funktionhydraulisch"] = "Freispiegelleitung",
        });
        Assert.Equal("In Betrieb", QgisFeldKarte.Wert(h, FieldKeys.OperatingStatus, BauteilArt.Haltung));
        Assert.Equal("Genau", QgisFeldKarte.Wert(h, FieldKeys.PositionAccuracy, BauteilArt.Haltung));
        Assert.Equal("Regenabwasser", QgisFeldKarte.Wert(h, FieldKeys.UsageType, BauteilArt.Haltung));

        var s = new QgisBauteil("80461", new Dictionary<string, string> { ["ns_funktion"] = "Kontroll_Einsteigschacht", ["bw_status"] = "tot" });
        Assert.Equal("Kontrollschacht", QgisFeldKarte.Wert(s, "Funktion", BauteilArt.Schacht));
        Assert.Equal("Tot/Aufgehoben, verfüllt", QgisFeldKarte.Wert(s, FieldKeys.OperatingStatus, BauteilArt.Schacht));
    }
}
```

(Konstruktor von `QgisBauteil` vor dem Schreiben nachsehen: `new QgisBauteil(name, roh)` wie in `GeoShopXtfZuordnung.cs:86`.)

- [ ] **Step 2: rot**
- [ ] **Step 3: Umsetzung** — in `QgisFeldKarte.Wert` direkt nach `var wert = zuordnung.Umsetzung(text);`:

```csharp
            // Seit 23.09.2026: im Projekt steht der WebGIS-Begriff (Schritt A). Die Sperren gegen
            // «unbekannt» darunter greifen auch fuer «Unbekannt».
            wert = WebGisBegriffe.Normalisieren(art == BauteilArt.Schacht, zuordnung.Feld, wert);
```

  In `GeoShopXtfZuordnung.cs` die Profilzeile: `if (!string.IsNullOrEmpty(typ) && typ != "unbekannt") felder[FieldKeys.ProfileType] = WebGisBegriffe.Normalisieren(false, FieldKeys.ProfileType, typ);`
  Achtung Schachtfunktion Spezialbauwerk: `GeoShopXtfZuordnung` setzt dort `felder["Funktion"]` NACH der Karte aus `AbwasserbauwerkVokabular.Spezialfunktion` — das bleibt unverändert (Normfunktion). Für den Normschacht liefert die Karte bereits den WebGIS-Begriff.
- [ ] **Step 4: grün** — `--filter "FullyQualifiedName~Qgis|FullyQualifiedName~GeoShop|FullyQualifiedName~LeereFelder"`; Erwartungen «in_Betrieb»/«genau»/«Niederschlagsabwasser» in diesen Tests umstellen.
- [ ] **Step 5: Commit**: `WebGIS-Begriffe: QGIS und GeoShop liefern WebGIS-Begriffe`

---

### Task 8: Projektprüfung meldet Werte ohne WebGIS-Begriff und nicht sendbare Masse

**Files:**
- Modify: `src/AuswertungPro.Next.Application/UseCases/ProjektPruefung/ProjektPruefregeln.cs`
- Test: `tests/AuswertungPro.Next.Infrastructure.Tests/ProjektPruefungTests.cs`

- [ ] **Step 1: Failing test**

```csharp
    [Fact]
    public void Werte_ohne_webgis_begriff_und_nicht_sendbare_masse_werden_gemeldet()
    {
        var projekt = new Project();
        var h = new HaltungRecord();
        h.SetFieldValue(FieldKeys.HoldingName, "H1", FieldSource.Manual, false);
        h.Fields[FieldKeys.OperatingStatus] = "weitere";
        h.Fields[FieldKeys.NominalDiameterMm] = "148";
        projekt.Data.Add(h);
        var s = new SchachtRecord();
        s.Fields["Schachtnummer"] = "80409";
        s.Fields["Funktion"] = "Absturzbauwerk";
        projekt.SchaechteData.Add(s);

        var punkte = ProjektPruefregeln.Pruefe(projekt, _ => null).Punkte;

        Assert.Contains(punkte, p => p.Objektname == "H1" && p.Meldung.Contains("«weitere»") && p.Meldung.Contains("kein WebGIS-Begriff"));
        Assert.Contains(punkte, p => p.Objektname == "H1" && p.Meldung.Contains("«148»") && p.Meldung.Contains("nicht in der WebGIS-Liste"));
        Assert.Contains(punkte, p => p.Objektname == "80409" && p.Meldung.Contains("«Absturzbauwerk»"));
    }
```

- [ ] **Step 2: rot**
- [ ] **Step 3: Umsetzung** — in `Pruefe` innerhalb der Haltungsschleife nach `PruefeObjekt(...)`: `PruefeWebGis(b, name, false, h.GetFieldValue, "Normschacht");` und in der Schachtschleife `PruefeWebGis(b, name, true, Wert, AbwasserbauwerkVokabular.Klasse(Wert(FieldKeys.ShaftStructureType), Wert("Funktion")));`. Lokale Funktion neben `Add`:

```csharp
        // Seit 23.09.2026: Was ins WebGIS geht, muss ein WebGIS-Begriff sein (Schritt A). Altwerte
        // bleiben stehen; hier werden sie zum Korrigieren gezeigt.
        void PruefeWebGis(ObjektaktenBearbeitung b, string name, bool schacht, Func<string, string> wert, string? klasse)
        {
            IEnumerable<string> felder = schacht
                ? WebGisBegriffe.SchachtFelder.Concat(klasse == "Normschacht"
                    ? new[] { WebGisBegriffe.SchachtFunktion } : Array.Empty<string>())
                : WebGisBegriffe.HaltungFelder;
            foreach (var feld in felder)
            {
                var text = wert(feld).Trim();
                if (text.Length > 0 && !WebGisBegriffe.Fuer(schacht, feld)!.Kennt(text))
                    Add(b, name, ProjektPruefbereich.Eingabefelder,
                        $"{feld}: «{text}» ist kein WebGIS-Begriff und wird nicht ins WebGIS übertragen.", speicher: feld);
            }
            // Entscheid A (23.09.2026): Masse bleiben wie gemessen; nur Zahlen der WebGIS-Liste sind sendbar.
            var masse = schacht
                ? new[] { (Feld: FieldKeys.ShaftDimension1Mm, Katalog: "schacht-C09"), (FieldKeys.ShaftDimension2Mm, "schacht-C09") }
                : new[] { (Feld: FieldKeys.NominalDiameterMm, Katalog: "haltung-C08"), (FieldKeys.ClearWidthMm, "haltung-C08") };
            foreach (var (feld, katalog) in masse)
            {
                var text = wert(feld).Trim();
                var liste = FieldCatalog.Objektfelder.Auswahl(katalog)?.Eintraege.Select(e => e.Label).ToHashSet(StringComparer.Ordinal);
                if (text.Length > 0 && liste is not null && !liste.Contains(text))
                    Add(b, name, ProjektPruefbereich.Eingabefelder,
                        $"{feld}: «{text}» steht nicht in der WebGIS-Liste und kann nicht übertragen werden.", speicher: feld);
            }
        }
```

  (`FieldKeys.ShaftDimension1Mm`/`ShaftDimension2Mm`/`ClearWidthMm`/`NominalDiameterMm` existieren; Schachtwerte laufen über `Wert(key)` = `SchachtFeldnamen.Feld`.)
- [ ] **Step 4: grün** — `--filter "FullyQualifiedName~ProjektPruefung"` (Infrastructure und UI).
- [ ] **Step 5: Commit**: `WebGIS-Begriffe: Projektprüfung meldet Werte ohne WebGIS-Begriff`

---

### Task 9: Gesamtlauf, Nachzug der Erwartungen, Doku

**Files:**
- Modify: Tests, die alte Schreibweisen der Schritt-A-Felder festschreiben (nur Erwartungen, keine Produktlogik)
- Modify: `CLAUDE.md` (Abschnitt WebGIS-Export, Stufe 5)

- [ ] **Step 1: Alles bauen** — `dotnet build AuswertungPro.sln -o .tmp/buildout-begriffe` → 0 Fehler.
- [ ] **Step 2: Alle Tests** — `dotnet test tests/AuswertungPro.Next.Infrastructure.Tests -o .tmp/testout-begriffe-alle` und `dotnet test tests/AuswertungPro.Next.UI.Tests -o .tmp/testout-begriffe-ui-alle` und `dotnet test tests/AuswertungPro.Next.Pipeline.Tests -o .tmp/testout-begriffe-pipe`. Vorbestehend rot (nicht Teil dieses Plans, siehe CLAUDE.md): `ExportPageViewModelDependencyTests.ViewModel_speichert_keinen_ServiceProvider_als_Feld` und die Player-Architekturtests aus der parallelen Arbeit. Jeden NEUEN roten Test einzeln beurteilen: Erwartet er eine alte Schreibweise eines Schritt-A-Felds → Erwartung auf den WebGIS-Begriff umstellen (Grund im Kommentar). Prüft er eine Norm-Ausgabe (XTF/DSS/VSA) → Produktfehler, zurück zu Task 2.
- [ ] **Step 3: CLAUDE.md** — unter «Stufe 5» einen Punkt «WEBGIS-BEGRIFFE, SCHRITT A (23.09.2026)» mit: Regel (gespeichert = WebGIS-Beschriftung), Felder, Quelle `WebGisBegriffe` (Katalog), Anheben beim Laden, Altwerte bleiben + Projektprüfung, Schachtfunktion nur Normschacht, DSS fail-closed, Unbekannt-Regel, Masse Entscheid A, Tests.
- [ ] **Step 4: Abnahme mit Pascal** — SewerStudio schliessen, normal bauen, Zone 1.15 öffnen (hebt an), «Vom WebGIS holen»: für die Schritt-A-Felder keine Übersetzungen mehr, keine Hinweise «passt zu keinem SewerStudio-Wert»; Projektprüfung zeigt die Altwerte.
- [ ] **Step 5: Commit**: `WebGIS-Begriffe Schritt A: Doku und Testerwartungen`
