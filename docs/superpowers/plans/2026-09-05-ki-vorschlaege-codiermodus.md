# KI-Vorschlaege im Codiermodus — Umsetzungsplan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Beim Oeffnen des Codiermodus laeuft im Hintergrund der Vorabdurchlauf fuer Bogen, Rohranfang und Rohrende; die Treffer erscheinen als Karte "KI-Vorschlaege" im Seitenpanel und als Marker auf der Zeitleiste, mit Springen, Bestaetigen und Ablehnen.

**Architecture:** Ein neuer Application-UseCase (`CodingSuggestionScanUseCase`) ruft die zwei bestehenden Durchlaeufe (`IBendSuggestionScanService`, `IPipeEndSuggestionScanService`) nacheinander und fasst sie zu einem `CodingSuggestionSet` zusammen. Die reinen Entscheidungen (Textzeile, Meterspur-Nachschlag, Bestaetigungsplan) liegen als statische Regeln in Application. Der Player haelt nur eine Owner-Klasse mit der sichtbaren Liste, einen Markerzeichner und eine `PlayerWindow`-Teildatei, die Springen/Bestaetigen/Ablehnen an die bestehenden Codierwege weiterreicht.

**Tech Stack:** C# / .NET 10, WPF, xUnit (`tests/AuswertungPro.Next.Pipeline.Tests` fuer Application-Regeln, `tests/AuswertungPro.Next.UI.Tests` fuer Player-Klassen), kein neues NuGet-Paket, kein neuer Sidecar-Endpunkt.

**Spec:** `docs/superpowers/specs/2026-09-05-ki-vorschlaege-codiermodus-design.md`

## Global Constraints

- Keine neue Datei unter `src/AuswertungPro.Next.UI/Ai` (Waechter `UiAiFreezeArchitectureTests`). Neue Ablaeufe nach `src/AuswertungPro.Next.Application/UseCases/CodingSuggestions/`.
- Bogen-Pin: ID `bcc_nc15_seed46_20260808`, Gewicht-SHA-256 `8ad82c1b0186ec02126a18f095d551d7a083faa90855350b22a6e893ac860114` (gleich wie `BendSuggestionListViewModel.KandidatId`/`GewichtSha256`).
- Reihenfolge im Durchlauf: zuerst Bogen, dann Rohranfang/Rohrende (Slot `YOLO_TEST` wird geteilt).
- Ein technischer Fehler ist nie "kein Vorschlag"; `OperationCanceledException` wird immer durchgereicht.
- Ein Meterstand wird nie als `0,0` angezeigt, wenn er fehlt; geschaetzte Meter heissen "ca." und werden nie als Laenge vorgeschlagen.
- Sichtbare Texte mit echten Umlauten (XAML/Anzeige), Quellcode und Kommentare mit `ae/oe/ue`. Schweizer `ss`, kein `ß`.
- XAML: `FontSize` nur als `{DynamicResource Text…}`, Rundungen nur als `{DynamicResource Radius…}`, keine festen `#RRGGBB`, Menuepunkte mit literalem Header tragen ein `MenuItem.Icon` mit `ui:FluentIcon`.
- Registrierungszaehler in `ServiceProviderRegistrationTests` von 157 auf 158.
- Build laeuft bei offenem SewerStudio nur in einen eigenen Ausgabeordner: `dotnet build AuswertungPro.sln -o .tmp/testout-vorschlaege`; Tests mit `-o .tmp/testout-vorschlaege/<proj> --no-restore`.
- Commits auf Deutsch, Abschluss `Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>`. Nicht pushen (Push-Pruefung baut in gesperrte Ordner, solange SewerStudio laeuft).

---

## Dateiuebersicht

| Aktion | Datei | Verantwortung |
|---|---|---|
| Aendern | `src/AuswertungPro.Next.Application/UseCases/BendSuggestions/BendSuggestionScanUseCase.cs` | zusaetzlich `MeterTrack` im Ergebnis |
| Neu | `src/AuswertungPro.Next.Application/UseCases/CodingSuggestions/CodingSuggestionModels.cs` | Vorschlag, Set, Teilstatus, Pin |
| Neu | `src/AuswertungPro.Next.Application/UseCases/CodingSuggestions/CodingSuggestionText.cs` | Zeilentext (rein) |
| Neu | `src/AuswertungPro.Next.Application/UseCases/CodingSuggestions/CodingSuggestionMeterLookup.cs` | Meterspur-Nachschlag (rein) |
| Neu | `src/AuswertungPro.Next.Application/UseCases/CodingSuggestions/CodingSuggestionConfirmPolicy.cs` | Bestaetigungsplan (rein) |
| Neu | `src/AuswertungPro.Next.Application/UseCases/CodingSuggestions/CodingSuggestionScanUseCase.cs` | Reihenfolge, Teilausfall, Abbruch, Gedaechtnis |
| Neu | `src/AuswertungPro.Next.Application/UseCases/CodingSuggestions/ICodingSuggestionScanService.cs` | Vertrag + Verdrahtung `CodingSuggestionScanService` |
| Aendern | `src/AuswertungPro.Next.Application/UseCases/CodingModeBackgroundServicesWorkflow.cs` | vierter Schritt |
| Neu | `src/AuswertungPro.Next.UI/ServiceProvider.CodingSuggestions.cs` | Dienst bauen |
| Aendern | `src/AuswertungPro.Next.UI/ServiceProviderRegistrationMap.cs` | registrieren |
| Aendern | `src/AuswertungPro.Next.UI/AppSettings.cs`, `Settings/SettingsSaveWorkflow.cs`, `ViewModels/Pages/SettingsPageViewModel.cs`, `Views/Pages/SettingsPage.xaml` | Schalter |
| Neu | `src/AuswertungPro.Next.UI/Player/CodingSuggestionRow.cs`, `CodingSuggestionsOwner.cs` | sichtbare Liste |
| Neu | `src/AuswertungPro.Next.UI/Player/SuggestionMarkerLayout.cs`, `SuggestionMarkerController.cs` | Zeitleistenmarker |
| Aendern | `src/AuswertungPro.Next.UI/Views/Windows/PlayerCodingSidePanel.xaml` + `.xaml.cs`, `PlayerCodingSidePanelEventBinder.cs`, `PlayerWindow.CodingSidePanelAccessors.cs` | Karte und Ereignisse |
| Aendern | `src/AuswertungPro.Next.UI/Views/Windows/PlayerWindow.xaml` | `SuggestionMarkerCanvas` |
| Neu | `src/AuswertungPro.Next.UI/Views/Windows/PlayerWindow.Coding.Suggestions.cs` | Start, Abbruch, Springen, Bestaetigen, Ablehnen |
| Aendern | `PlayerWindow.Coding.Lifecycle.Ui.cs`, `PlayerWindow.Wiring.cs`, `PlayerWindow.xaml.cs`, `PlayerWindowCodingModeExitControllerFactory.cs` | Verdrahtung, Abbruch beim Austritt |
| Aendern | `CLAUDE.md` | Regeln festhalten |

---

### Task 1: Meterspur im Bogen-Ergebnis

**Files:**
- Modify: `src/AuswertungPro.Next.Application/UseCases/BendSuggestions/BendSuggestionScanUseCase.cs`
- Test: `tests/AuswertungPro.Next.Pipeline.Tests/BendSuggestionScanUseCaseTests.cs`

**Interfaces:**
- Produces: `public sealed record MeterTrackPoint(double TimeSeconds, double Meter, bool IsEstimated);` und `BendSuggestionScanResult.MeterTrack` (`IReadOnlyList<MeterTrackPoint>`, nie null).

- [ ] **Step 1: Fehlschlagenden Test schreiben**

An das Ende der Klasse `BendSuggestionScanUseCaseTests` (vor den `private static`-Helfern) einfuegen:

```csharp
    [Fact]
    public async Task Die_Meterspur_traegt_jede_gelesene_oder_gefuellte_Sekunde()
    {
        // Der Codiermodus braucht am Rohrende den Meterstand — auch dort, wo kein
        // Bogen ist. Deshalb geht die ganze plausibilisierte, lueckengefuellte
        // Folge hinaus, nicht nur die Treffer.
        var ergebnis = await BendSuggestionScanUseCase.ExecuteAsync(
            Auftrag(),
            Kalibrierung(),
            Aktionen(
                extract: _ => Task.FromResult(Bilder(5)),
                detect: (bild, _) => Task.FromResult(bild.Index == 3
                    ? BendFrameResult.NoBend with { Meter = null }
                    : BendFrameResult.NoBend with { Meter = 0.5 * bild.Index })),
            CancellationToken.None);

        Assert.True(ergebnis.IsUsable);
        Assert.Equal(5, ergebnis.MeterTrack.Count);
        var dritte = ergebnis.MeterTrack.Single(p => p.TimeSeconds == 12.0);
        Assert.True(dritte.IsEstimated);
        Assert.Equal(1.5, dritte.Meter, 3);
        Assert.All(ergebnis.MeterTrack.Where(p => p.TimeSeconds != 12.0), p => Assert.False(p.IsEstimated));
    }

    [Fact]
    public async Task Ohne_Arbeitspunkt_ist_die_Meterspur_leer_und_nie_null()
    {
        var ergebnis = await BendSuggestionScanUseCase.ExecuteAsync(
            Auftrag(),
            calibration: null,
            Aktionen(
                extract: _ => Task.FromResult(Bilder(2)),
                detect: (_, _) => Task.FromResult(BendFrameResult.NoBend)),
            CancellationToken.None);

        Assert.False(ergebnis.IsUsable);
        Assert.Empty(ergebnis.MeterTrack);
    }
```

Hinweis: `BendFrameResult` ist ein `record` mit positionalen Parametern `(Outcome, Confidence, Reason, Meter)`; `with { Meter = … }` funktioniert. Falls der Compiler die Eigenschaft nicht als `init` kennt, stattdessen `new BendFrameResult(BendFrameOutcome.NoBend, 0.0, null, 0.5 * bild.Index)` verwenden.

- [ ] **Step 2: Test laufen lassen, Fehlschlag pruefen**

Run: `dotnet test tests/AuswertungPro.Next.Pipeline.Tests -o .tmp/testout-vorschlaege/pipe --filter "FullyQualifiedName~BendSuggestionScanUseCaseTests" -v q`
Expected: Compilerfehler `'BendSuggestionScanResult' enthaelt keine Definition fuer 'MeterTrack'`.

- [ ] **Step 3: Minimalimplementierung**

In `BendSuggestionScanUseCase.cs` vor `public sealed record BendSuggestionScanResult(` einfuegen:

```csharp
/// <summary>
/// Ein Punkt der Meterspur des ganzen Videos: pro ausgewertetem Bild der
/// plausibilisierte und lueckengefuellte Meterstand. <see cref="IsEstimated"/>
/// heisst "aus Nachbarn gefuellt" — brauchbar zum Einordnen, nie als gemessener
/// Wert (Laengenvorschlag) zu verwenden.
/// </summary>
public sealed record MeterTrackPoint(double TimeSeconds, double Meter, bool IsEstimated);
```

Den Record `BendSuggestionScanResult` um einen letzten optionalen Parameter erweitern und die Eigenschaft nie-null machen:

```csharp
public sealed record BendSuggestionScanResult(
    bool IsUsable,
    string Reason,
    IReadOnlyList<BendSuggestion> Suggestions,
    int FramesAnalyzed,
    int FramesNotAssessed,
    TimeSpan Duration,
    string CandidateId,
    string WeightSha256,
    double MinConfidence,
    double StrongConfidence,
    string WorkpointSource = "",
    IReadOnlyList<MeterTrackPoint>? MeterTrack = null)
{
    /// <summary>Meterspur des Videos; leer, wenn der Durchlauf nicht lief.</summary>
    public IReadOnlyList<MeterTrackPoint> MeterTrack { get; init; } = MeterTrack ?? Array.Empty<MeterTrackPoint>();
}
```

In `ExecuteAsync` nach `var gefuellt = …ToDictionary(...)` die Spur bauen:

```csharp
        var meterTrack = gefuellt.Values
            .Where(reading => reading.Meter.HasValue)
            .OrderBy(reading => reading.TimeSeconds)
            .Select(reading => new MeterTrackPoint(reading.TimeSeconds, reading.Meter!.Value, reading.IsEstimated))
            .ToList();
```

und im abschliessenden `return new BendSuggestionScanResult(` als letztes Argument `calibration?.Source ?? "", meterTrack);` uebergeben.

- [ ] **Step 4: Tests laufen lassen**

Run: `dotnet test tests/AuswertungPro.Next.Pipeline.Tests -o .tmp/testout-vorschlaege/pipe --filter "FullyQualifiedName~BendSuggestion" -v q`
Expected: alle gruen, auch die bestehenden Bogen-Tests (der zusaetzliche Parameter hat einen Standardwert).

- [ ] **Step 5: Commit**

```bash
git add src/AuswertungPro.Next.Application/UseCases/BendSuggestions/BendSuggestionScanUseCase.cs tests/AuswertungPro.Next.Pipeline.Tests/BendSuggestionScanUseCaseTests.cs
git commit -m "Bogen-Durchlauf gibt die Meterspur des ganzen Videos heraus" -m "Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 2: Modelle, Pin, Zeilentext und Meter-Nachschlag

**Files:**
- Create: `src/AuswertungPro.Next.Application/UseCases/CodingSuggestions/CodingSuggestionModels.cs`
- Create: `src/AuswertungPro.Next.Application/UseCases/CodingSuggestions/CodingSuggestionText.cs`
- Create: `src/AuswertungPro.Next.Application/UseCases/CodingSuggestions/CodingSuggestionMeterLookup.cs`
- Test: `tests/AuswertungPro.Next.Pipeline.Tests/CodingSuggestionModelsTests.cs`

**Interfaces:**
- Produces:
  - `enum CodingSuggestionKind { Bogen, Rohranfang, Rohrende }`
  - `enum CodingSuggestionPartStatus { Bereit, NichtVerfuegbar, Fehler }`
  - `record CodingSuggestionPartState(CodingSuggestionPartStatus Status, string Grund)` mit `static Bereit`
  - `record CodingSuggestion(CodingSuggestionKind Kind, double PeakTimeSeconds, double? Meter, bool MeterIsEstimated, double Confidence, bool IsStrong, double AcceptancePrecision)`
  - `record CodingSuggestionSet(IReadOnlyList<CodingSuggestion> Suggestions, IReadOnlyList<MeterTrackPoint> MeterTrack, CodingSuggestionPartState BogenTeil, CodingSuggestionPartState AnfangEndeTeil)` mit `static Leer(string grund)`
  - `static class CodingBendCandidatePin { const string Id; const string WeightSha256; }`
  - `static string CodingSuggestionText.Zeile(CodingSuggestion s)`
  - `static MeterTrackPoint? CodingSuggestionMeterLookup.Find(IReadOnlyList<MeterTrackPoint> track, double timeSeconds, double toleranceSeconds = 1.5)`

- [ ] **Step 1: Fehlschlagende Tests schreiben**

`tests/AuswertungPro.Next.Pipeline.Tests/CodingSuggestionModelsTests.cs`:

```csharp
using System;
using System.Collections.Generic;
using AuswertungPro.Next.Application.UseCases.BendSuggestions;
using AuswertungPro.Next.Application.UseCases.CodingSuggestions;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

/// <summary>
/// Reine Regeln der Vorschlagsliste im Codiermodus: Pin, Zeilentext,
/// Meterspur-Nachschlag. Kein WPF, kein Sidecar.
/// </summary>
public sealed class CodingSuggestionModelsTests
{
    [Fact]
    public void Der_Bogen_Pin_ist_der_gemessene_Kandidat_des_Training_Studios()
    {
        Assert.Equal("bcc_nc15_seed46_20260808", CodingBendCandidatePin.Id);
        Assert.Equal(
            "8ad82c1b0186ec02126a18f095d551d7a083faa90855350b22a6e893ac860114",
            CodingBendCandidatePin.WeightSha256);
    }

    [Fact]
    public void Bogen_mit_gelesenem_Meter_zeigt_Meter_und_Staerke()
    {
        var zeile = CodingSuggestionText.Zeile(Bogen(meter: 9.42, geschaetzt: false, stark: true));
        Assert.Equal("Bogen · Meter 9,42 · stark", zeile);
    }

    [Fact]
    public void Bogen_mit_geschaetztem_Meter_sagt_ca()
    {
        var zeile = CodingSuggestionText.Zeile(Bogen(meter: 9.42, geschaetzt: true, stark: false));
        Assert.Equal("Bogen · Meter ca. 9,4 · schwach", zeile);
    }

    [Fact]
    public void Bogen_ohne_Meter_nennt_die_Sekunde_und_nie_null_Meter()
    {
        var zeile = CodingSuggestionText.Zeile(Bogen(meter: null, geschaetzt: false, stark: true) with { PeakTimeSeconds = 87.4 });
        Assert.Equal("Bogen · Sekunde 87 (Meterstand nicht lesbar) · stark", zeile);
        Assert.DoesNotContain("0,0", zeile);
    }

    [Fact]
    public void Rohranfang_und_Rohrende_nennen_Sekunde_und_Abnahmewert()
    {
        var anfang = new CodingSuggestion(CodingSuggestionKind.Rohranfang, 4.2, null, false, 0.97, true, 0.8545);
        var ende = new CodingSuggestion(CodingSuggestionKind.Rohrende, 143.0, 42.35, false, 0.91, true, 0.8889);

        Assert.Equal("Rohranfang · Sekunde 4 · Abnahme 85 %", CodingSuggestionText.Zeile(anfang));
        Assert.Equal("Rohrende · Sekunde 143 · Abnahme 89 %", CodingSuggestionText.Zeile(ende));
    }

    [Fact]
    public void Meter_Nachschlag_nimmt_den_naechsten_Punkt_innerhalb_der_Toleranz()
    {
        var spur = new List<MeterTrackPoint>
        {
            new(10.0, 5.00, false),
            new(11.0, 5.50, false),
            new(12.0, 6.00, true),
            new(20.0, 10.0, false)
        };

        var treffer = CodingSuggestionMeterLookup.Find(spur, 11.4);
        Assert.NotNull(treffer);
        Assert.Equal(11.0, treffer!.TimeSeconds);

        var geschaetzt = CodingSuggestionMeterLookup.Find(spur, 12.2);
        Assert.NotNull(geschaetzt);
        Assert.True(geschaetzt!.IsEstimated);

        Assert.Null(CodingSuggestionMeterLookup.Find(spur, 16.0));
        Assert.Null(CodingSuggestionMeterLookup.Find(Array.Empty<MeterTrackPoint>(), 11.0));
    }

    [Fact]
    public void Ein_leeres_Set_traegt_den_Grund_in_beiden_Teilen()
    {
        var leer = CodingSuggestionSet.Leer("ausgeschaltet");
        Assert.Empty(leer.Suggestions);
        Assert.Empty(leer.MeterTrack);
        Assert.Equal(CodingSuggestionPartStatus.NichtVerfuegbar, leer.BogenTeil.Status);
        Assert.Equal("ausgeschaltet", leer.AnfangEndeTeil.Grund);
    }

    private static CodingSuggestion Bogen(double? meter, bool geschaetzt, bool stark)
        => new(CodingSuggestionKind.Bogen, 30.0, meter, geschaetzt, stark ? 0.9 : 0.6, stark, 0.0);
}
```

- [ ] **Step 2: Test laufen lassen, Fehlschlag pruefen**

Run: `dotnet test tests/AuswertungPro.Next.Pipeline.Tests -o .tmp/testout-vorschlaege/pipe --filter "FullyQualifiedName~CodingSuggestionModelsTests" -v q`
Expected: Compilerfehler, Namespace `CodingSuggestions` unbekannt.

- [ ] **Step 3: Minimalimplementierung**

`CodingSuggestionModels.cs`:

```csharp
using System;
using System.Collections.Generic;
using AuswertungPro.Next.Application.UseCases.BendSuggestions;

namespace AuswertungPro.Next.Application.UseCases.CodingSuggestions;

/// <summary>Die drei Helfer, die im Codiermodus vorschlagen duerfen.</summary>
public enum CodingSuggestionKind
{
    Bogen = 0,
    Rohranfang = 1,
    Rohrende = 2
}

/// <summary>Zustand eines Teil-Durchlaufs. Ein Fehler ist nie "kein Vorschlag".</summary>
public enum CodingSuggestionPartStatus
{
    Bereit = 0,
    NichtVerfuegbar = 1,
    Fehler = 2
}

public sealed record CodingSuggestionPartState(CodingSuggestionPartStatus Status, string Grund)
{
    public static CodingSuggestionPartState Bereit { get; } = new(CodingSuggestionPartStatus.Bereit, string.Empty);

    public static CodingSuggestionPartState NichtVerfuegbar(string grund)
        => new(CodingSuggestionPartStatus.NichtVerfuegbar, grund);

    public static CodingSuggestionPartState Fehler(string grund)
        => new(CodingSuggestionPartStatus.Fehler, grund);
}

/// <summary>Ein Vorschlag an einer Videostelle.</summary>
/// <param name="Meter">Gelesener oder gefuellter Meterstand; null = nicht lesbar.</param>
/// <param name="MeterIsEstimated">True = aus Nachbarn gefuellt, nur grobe Lage.</param>
/// <param name="IsStrong">Bogen: ueber der starken Grenze des Arbeitspunkts. Anfang/Ende: immer true.</param>
/// <param name="AcceptancePrecision">Anfang/Ende: gepinnter Abnahmewert (Precision). Bogen: 0.</param>
public sealed record CodingSuggestion(
    CodingSuggestionKind Kind,
    double PeakTimeSeconds,
    double? Meter,
    bool MeterIsEstimated,
    double Confidence,
    bool IsStrong,
    double AcceptancePrecision);

/// <summary>Ergebnis des Vorabdurchlaufs fuer den Codiermodus.</summary>
public sealed record CodingSuggestionSet(
    IReadOnlyList<CodingSuggestion> Suggestions,
    IReadOnlyList<MeterTrackPoint> MeterTrack,
    CodingSuggestionPartState BogenTeil,
    CodingSuggestionPartState AnfangEndeTeil)
{
    public static CodingSuggestionSet Leer(string grund)
        => new(
            Array.Empty<CodingSuggestion>(),
            Array.Empty<MeterTrackPoint>(),
            CodingSuggestionPartState.NichtVerfuegbar(grund),
            CodingSuggestionPartState.NichtVerfuegbar(grund));
}

/// <summary>
/// Der einzige Bogen-Kandidat mit gemessenem Arbeitspunkt (workpoint.json). Im
/// Codiermodus gibt es keine Modellwahl; dieser Pin ist dieselbe Konstante wie
/// im Training Studio (BendSuggestionListViewModel). Ein anderes Gewicht braucht
/// eine neue Messung UND einen neuen Pin.
/// </summary>
public static class CodingBendCandidatePin
{
    public const string Id = "bcc_nc15_seed46_20260808";
    public const string WeightSha256 = "8ad82c1b0186ec02126a18f095d551d7a083faa90855350b22a6e893ac860114";
}
```

`CodingSuggestionText.cs`:

```csharp
using System;
using System.Globalization;

namespace AuswertungPro.Next.Application.UseCases.CodingSuggestions;

/// <summary>
/// Der Zeilentext der Vorschlagsliste. Ein fehlender Meterstand heisst
/// "nicht lesbar", niemals 0,0; ein gefuellter Wert heisst "ca.".
/// </summary>
public static class CodingSuggestionText
{
    private static readonly CultureInfo DeCh = CultureInfo.GetCultureInfo("de-CH");

    public static string Zeile(CodingSuggestion vorschlag)
    {
        ArgumentNullException.ThrowIfNull(vorschlag);

        return vorschlag.Kind switch
        {
            CodingSuggestionKind.Bogen => $"Bogen · {Ort(vorschlag)} · {(vorschlag.IsStrong ? "stark" : "schwach")}",
            CodingSuggestionKind.Rohranfang => $"Rohranfang · Sekunde {Sekunde(vorschlag)} · Abnahme {Prozent(vorschlag.AcceptancePrecision)}",
            CodingSuggestionKind.Rohrende => $"Rohrende · Sekunde {Sekunde(vorschlag)} · Abnahme {Prozent(vorschlag.AcceptancePrecision)}",
            _ => throw new ArgumentOutOfRangeException(nameof(vorschlag), vorschlag.Kind, null)
        };
    }

    public static string Art(CodingSuggestionKind kind) => kind switch
    {
        CodingSuggestionKind.Bogen => "Bogen",
        CodingSuggestionKind.Rohranfang => "Rohranfang",
        CodingSuggestionKind.Rohrende => "Rohrende",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
    };

    private static string Ort(CodingSuggestion v)
    {
        if (v.Meter is not { } meter)
            return $"Sekunde {Sekunde(v)} (Meterstand nicht lesbar)";
        return v.MeterIsEstimated
            ? $"Meter ca. {meter.ToString("0.0", DeCh)}"
            : $"Meter {meter.ToString("0.00", DeCh)}";
    }

    private static string Sekunde(CodingSuggestion v)
        => Math.Floor(v.PeakTimeSeconds).ToString("0", CultureInfo.InvariantCulture);

    private static string Prozent(double anteil)
        => $"{Math.Round(anteil * 100.0).ToString("0", CultureInfo.InvariantCulture)} %";
}
```

`CodingSuggestionMeterLookup.cs`:

```csharp
using System;
using System.Collections.Generic;

namespace AuswertungPro.Next.Application.UseCases.CodingSuggestions;

/// <summary>
/// Sucht in der Meterspur des Bogen-Durchlaufs den Punkt, der einer Videosekunde
/// am naechsten liegt. Die Spur ist mit 1 Bild je Sekunde aufgenommen; 1,5 s
/// Toleranz erlaubt genau den Nachbarn, aber keinen Sprung ueber eine Luecke.
/// </summary>
public static class CodingSuggestionMeterLookup
{
    public static AuswertungPro.Next.Application.UseCases.BendSuggestions.MeterTrackPoint? Find(
        IReadOnlyList<AuswertungPro.Next.Application.UseCases.BendSuggestions.MeterTrackPoint> track,
        double timeSeconds,
        double toleranceSeconds = 1.5)
    {
        ArgumentNullException.ThrowIfNull(track);

        AuswertungPro.Next.Application.UseCases.BendSuggestions.MeterTrackPoint? bester = null;
        var besterAbstand = double.PositiveInfinity;
        foreach (var punkt in track)
        {
            var abstand = Math.Abs(punkt.TimeSeconds - timeSeconds);
            if (abstand <= toleranceSeconds && abstand < besterAbstand)
            {
                bester = punkt;
                besterAbstand = abstand;
            }
        }

        return bester;
    }
}
```

- [ ] **Step 4: Tests laufen lassen**

Run: `dotnet test tests/AuswertungPro.Next.Pipeline.Tests -o .tmp/testout-vorschlaege/pipe --filter "FullyQualifiedName~CodingSuggestionModelsTests" -v q`
Expected: 7 Tests gruen.

- [ ] **Step 5: Commit**

```bash
git add src/AuswertungPro.Next.Application/UseCases/CodingSuggestions tests/AuswertungPro.Next.Pipeline.Tests/CodingSuggestionModelsTests.cs
git commit -m "Codiermodus-Vorschlaege: Modelle, Bogen-Pin, Zeilentext und Meter-Nachschlag" -m "Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 3: Bestaetigungsplan als reine Regel

**Files:**
- Create: `src/AuswertungPro.Next.Application/UseCases/CodingSuggestions/CodingSuggestionConfirmPolicy.cs`
- Test: `tests/AuswertungPro.Next.Pipeline.Tests/CodingSuggestionConfirmPolicyTests.cs`

**Interfaces:**
- Consumes: `CodingSuggestion`, `CodingSuggestionKind`, `MeterTrackPoint`, `CodingSuggestionMeterLookup.Find`
- Produces:
  - `enum CodingSuggestionConfirmAction { OpenCodeWindow, CreateBoundaryEvent, AlreadyPresent }`
  - `record CodingSuggestionConfirmPlan(CodingSuggestionConfirmAction Action, string Code, double? Meter, bool ProposeLength, string Hinweis)`
  - `static CodingSuggestionConfirmPlan CodingSuggestionConfirmPolicy.Plan(CodingSuggestion s, IReadOnlyList<MeterTrackPoint> track, IReadOnlyCollection<string> activeCodes, bool hasHoldingLength)`

- [ ] **Step 1: Fehlschlagende Tests schreiben**

```csharp
using System;
using System.Collections.Generic;
using AuswertungPro.Next.Application.UseCases.BendSuggestions;
using AuswertungPro.Next.Application.UseCases.CodingSuggestions;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

/// <summary>Was ein Klick auf "Bestaetigen" ausloest — ohne WPF entschieden.</summary>
public sealed class CodingSuggestionConfirmPolicyTests
{
    private static readonly IReadOnlyList<MeterTrackPoint> Spur =
    [
        new(142.0, 42.10, false),
        new(143.0, 42.35, false),
        new(150.0, 44.00, true)
    ];

    [Fact]
    public void Bogen_oeffnet_das_Codierfenster_mit_BCC_und_dem_Vorschlagsmeter()
    {
        var plan = CodingSuggestionConfirmPolicy.Plan(
            new CodingSuggestion(CodingSuggestionKind.Bogen, 30.0, 9.42, false, 0.9, true, 0.0),
            Spur, activeCodes: [], hasHoldingLength: true);

        Assert.Equal(CodingSuggestionConfirmAction.OpenCodeWindow, plan.Action);
        Assert.Equal("BCC", plan.Code);
        Assert.Equal(9.42, plan.Meter);
        Assert.False(plan.ProposeLength);
    }

    [Fact]
    public void Bogen_mit_geschaetztem_Meter_gibt_keinen_Meter_vor()
    {
        var plan = CodingSuggestionConfirmPolicy.Plan(
            new CodingSuggestion(CodingSuggestionKind.Bogen, 30.0, 9.4, true, 0.9, true, 0.0),
            Spur, [], true);

        Assert.Null(plan.Meter);
    }

    [Fact]
    public void Rohranfang_legt_BCD_bei_null_Meter_an()
    {
        var plan = CodingSuggestionConfirmPolicy.Plan(
            new CodingSuggestion(CodingSuggestionKind.Rohranfang, 4.0, null, false, 0.97, true, 0.85),
            Spur, [], true);

        Assert.Equal(CodingSuggestionConfirmAction.CreateBoundaryEvent, plan.Action);
        Assert.Equal("BCD", plan.Code);
        Assert.Equal(0.0, plan.Meter);
    }

    [Fact]
    public void Ein_vorhandenes_BCD_wird_nicht_doppelt_angelegt()
    {
        var plan = CodingSuggestionConfirmPolicy.Plan(
            new CodingSuggestion(CodingSuggestionKind.Rohranfang, 4.0, null, false, 0.97, true, 0.85),
            Spur, ["BCD", "BAB"], true);

        Assert.Equal(CodingSuggestionConfirmAction.AlreadyPresent, plan.Action);
        Assert.Contains("bereits", plan.Hinweis);
    }

    [Fact]
    public void Rohrende_nimmt_den_Meter_aus_der_Spur_und_schlaegt_die_Laenge_vor_wenn_sie_fehlt()
    {
        var plan = CodingSuggestionConfirmPolicy.Plan(
            new CodingSuggestion(CodingSuggestionKind.Rohrende, 143.4, null, false, 0.91, true, 0.89),
            Spur, [], hasHoldingLength: false);

        Assert.Equal(CodingSuggestionConfirmAction.CreateBoundaryEvent, plan.Action);
        Assert.Equal("BCE", plan.Code);
        Assert.Equal(42.35, plan.Meter);
        Assert.True(plan.ProposeLength);
    }

    [Fact]
    public void Rohrende_mit_vorhandener_Laenge_schlaegt_nichts_vor()
    {
        var plan = CodingSuggestionConfirmPolicy.Plan(
            new CodingSuggestion(CodingSuggestionKind.Rohrende, 143.4, null, false, 0.91, true, 0.89),
            Spur, [], hasHoldingLength: true);

        Assert.Equal(42.35, plan.Meter);
        Assert.False(plan.ProposeLength);
    }

    [Fact]
    public void Rohrende_mit_geschaetztem_Spurwert_schlaegt_nie_eine_Laenge_vor()
    {
        var plan = CodingSuggestionConfirmPolicy.Plan(
            new CodingSuggestion(CodingSuggestionKind.Rohrende, 150.2, null, false, 0.91, true, 0.89),
            Spur, [], hasHoldingLength: false);

        Assert.Null(plan.Meter);
        Assert.False(plan.ProposeLength);
    }

    [Fact]
    public void Rohrende_ohne_Spur_legt_BCE_ohne_Meter_an()
    {
        var plan = CodingSuggestionConfirmPolicy.Plan(
            new CodingSuggestion(CodingSuggestionKind.Rohrende, 143.4, null, false, 0.91, true, 0.89),
            Array.Empty<MeterTrackPoint>(), [], false);

        Assert.Equal(CodingSuggestionConfirmAction.CreateBoundaryEvent, plan.Action);
        Assert.Null(plan.Meter);
        Assert.False(plan.ProposeLength);
    }
}
```

- [ ] **Step 2: Test laufen lassen, Fehlschlag pruefen**

Run: `dotnet test tests/AuswertungPro.Next.Pipeline.Tests -o .tmp/testout-vorschlaege/pipe --filter "FullyQualifiedName~CodingSuggestionConfirmPolicyTests" -v q`
Expected: Compilerfehler `CodingSuggestionConfirmPolicy` unbekannt.

- [ ] **Step 3: Minimalimplementierung**

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using AuswertungPro.Next.Application.UseCases.BendSuggestions;

namespace AuswertungPro.Next.Application.UseCases.CodingSuggestions;

public enum CodingSuggestionConfirmAction
{
    /// <summary>Codierfenster mit vorgewaehltem Hauptcode oeffnen; der Mensch waehlt die Richtung.</summary>
    OpenCodeWindow = 0,

    /// <summary>Grenzereignis (BCD/BCE) direkt anlegen.</summary>
    CreateBoundaryEvent = 1,

    /// <summary>Das Grenzereignis existiert schon — nur springen, nichts anlegen.</summary>
    AlreadyPresent = 2
}

/// <param name="Meter">Vorgabemeter; null = normale Meterermittlung des Codiermodus.</param>
/// <param name="ProposeLength">True = Haltungslaenge fehlt und ein gelesener (nicht geschaetzter) Meter liegt vor.</param>
public sealed record CodingSuggestionConfirmPlan(
    CodingSuggestionConfirmAction Action,
    string Code,
    double? Meter,
    bool ProposeLength,
    string Hinweis);

/// <summary>
/// Entscheidet ohne WPF, was "Bestaetigen" tut. Ein geschaetzter Meter wird nie
/// als Vorgabe oder Laenge verwendet; ein vorhandenes BCD/BCE wird nie verdoppelt.
/// </summary>
public static class CodingSuggestionConfirmPolicy
{
    public static CodingSuggestionConfirmPlan Plan(
        CodingSuggestion vorschlag,
        IReadOnlyList<MeterTrackPoint> meterTrack,
        IReadOnlyCollection<string> activeCodes,
        bool hasHoldingLength)
    {
        ArgumentNullException.ThrowIfNull(vorschlag);
        ArgumentNullException.ThrowIfNull(meterTrack);
        ArgumentNullException.ThrowIfNull(activeCodes);

        switch (vorschlag.Kind)
        {
            case CodingSuggestionKind.Bogen:
                return new CodingSuggestionConfirmPlan(
                    CodingSuggestionConfirmAction.OpenCodeWindow,
                    "BCC",
                    vorschlag.MeterIsEstimated ? null : vorschlag.Meter,
                    ProposeLength: false,
                    Hinweis: string.Empty);

            case CodingSuggestionKind.Rohranfang:
                if (HatCode(activeCodes, "BCD"))
                    return Vorhanden("BCD", "Rohranfang ist bereits codiert.");
                return new CodingSuggestionConfirmPlan(
                    CodingSuggestionConfirmAction.CreateBoundaryEvent, "BCD", 0.0, false, string.Empty);

            case CodingSuggestionKind.Rohrende:
            {
                if (HatCode(activeCodes, "BCE"))
                    return Vorhanden("BCE", "Rohrende ist bereits codiert.");

                var punkt = CodingSuggestionMeterLookup.Find(meterTrack, vorschlag.PeakTimeSeconds);
                var meter = punkt is { IsEstimated: false } ? punkt.Meter : (double?)null;
                return new CodingSuggestionConfirmPlan(
                    CodingSuggestionConfirmAction.CreateBoundaryEvent,
                    "BCE",
                    meter,
                    ProposeLength: meter.HasValue && !hasHoldingLength,
                    Hinweis: string.Empty);
            }

            default:
                throw new ArgumentOutOfRangeException(nameof(vorschlag), vorschlag.Kind, null);
        }
    }

    private static bool HatCode(IReadOnlyCollection<string> codes, string code)
        => codes.Any(c => string.Equals(c?.Trim(), code, StringComparison.OrdinalIgnoreCase));

    private static CodingSuggestionConfirmPlan Vorhanden(string code, string hinweis)
        => new(CodingSuggestionConfirmAction.AlreadyPresent, code, null, false, hinweis);
}
```

- [ ] **Step 4: Tests laufen lassen**

Run: `dotnet test tests/AuswertungPro.Next.Pipeline.Tests -o .tmp/testout-vorschlaege/pipe --filter "FullyQualifiedName~CodingSuggestionConfirmPolicyTests" -v q`
Expected: 8 Tests gruen.

- [ ] **Step 5: Commit**

```bash
git add src/AuswertungPro.Next.Application/UseCases/CodingSuggestions/CodingSuggestionConfirmPolicy.cs tests/AuswertungPro.Next.Pipeline.Tests/CodingSuggestionConfirmPolicyTests.cs
git commit -m "Codiermodus-Vorschlaege: Bestaetigungsplan als reine Regel" -m "Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 4: Der Durchlauf (UseCase, Vertrag, Verdrahtung)

**Files:**
- Create: `src/AuswertungPro.Next.Application/UseCases/CodingSuggestions/CodingSuggestionScanUseCase.cs`
- Create: `src/AuswertungPro.Next.Application/UseCases/CodingSuggestions/ICodingSuggestionScanService.cs`
- Test: `tests/AuswertungPro.Next.Pipeline.Tests/CodingSuggestionScanUseCaseTests.cs`

**Interfaces:**
- Consumes: `IBendSuggestionScanService.ScanAsync(BendSuggestionScanRequest, CancellationToken, IProgress<BendSuggestionScanProgress>?, …)`, `IPipeEndSuggestionScanService.ScanAsync(PipeEndScanRequest, CancellationToken, IProgress<PipeEndScanProgress>?)`, `ICodingSuggestionExposure.MarkExposed(string)`, `BendSuggestionScanResult` (Task 1), `PipeEndScanResult`, `PipeEndLernstufePins`.
- Produces:
  - `record CodingSuggestionScanRequest(string VideoPath, string Haltung, bool Enabled)`
  - `record CodingSuggestionScanActions(Func<BendSuggestionScanRequest, CancellationToken, Task<BendSuggestionScanResult>> ScanBends, Func<PipeEndScanRequest, CancellationToken, Task<PipeEndScanResult>> ScanPipeEnds, Action<string> MarkExposed) { Action<int>? ReportPercent }`
  - `static Task<CodingSuggestionSet> CodingSuggestionScanUseCase.ExecuteAsync(request, actions, ct)`
  - `static int CodingSuggestionScanUseCase.Percent(bool bogenPhase, int processed, int total)`
  - `interface ICodingSuggestionScanService { Task<CodingSuggestionSet> ScanAsync(CodingSuggestionScanRequest request, CancellationToken ct, IProgress<int>? percent = null); }`
  - `sealed class CodingSuggestionScanService : ICodingSuggestionScanService` (Konstruktor `(IBendSuggestionScanService bends, IPipeEndSuggestionScanService pipeEnds, ICodingSuggestionExposure exposure)`)

- [ ] **Step 1: Fehlschlagende Tests schreiben**

```csharp
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.UseCases.BendSuggestions;
using AuswertungPro.Next.Application.UseCases.CodingSuggestions;
using AuswertungPro.Next.Application.UseCases.PipeEndSuggestions;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

/// <summary>
/// Der Vorabdurchlauf des Codiermodus: Bogen zuerst, dann Anfang/Ende; jeder
/// Teil faellt fuer sich aus; ein Abbruch geht durch; das Sitzungsgedaechtnis
/// wird nur bei mindestens einem Vorschlag gesetzt.
/// </summary>
public sealed class CodingSuggestionScanUseCaseTests
{
    [Fact]
    public async Task Bogen_laeuft_vor_Anfang_und_Ende_und_der_Pin_ist_gesetzt()
    {
        var reihenfolge = new List<string>();
        string? kandidat = null;

        var set = await CodingSuggestionScanUseCase.ExecuteAsync(
            Auftrag(),
            Aktionen(
                bends: (r, _) => { reihenfolge.Add("bogen"); kandidat = r.CandidateId; return Task.FromResult(BogenOk()); },
                pipeEnds: (_, _) => { reihenfolge.Add("enden"); return Task.FromResult(EndenOk()); }),
            CancellationToken.None);

        Assert.Equal(new[] { "bogen", "enden" }, reihenfolge);
        Assert.Equal(CodingBendCandidatePin.Id, kandidat);
        Assert.Equal(3, set.Suggestions.Count);
        Assert.Equal(CodingSuggestionPartStatus.Bereit, set.BogenTeil.Status);
        Assert.Equal(CodingSuggestionPartStatus.Bereit, set.AnfangEndeTeil.Status);
    }

    [Fact]
    public async Task Ausgeschaltet_startet_nichts()
    {
        var aufgerufen = false;
        var set = await CodingSuggestionScanUseCase.ExecuteAsync(
            Auftrag() with { Enabled = false },
            Aktionen(
                bends: (_, _) => { aufgerufen = true; return Task.FromResult(BogenOk()); },
                pipeEnds: (_, _) => { aufgerufen = true; return Task.FromResult(EndenOk()); }),
            CancellationToken.None);

        Assert.False(aufgerufen);
        Assert.Empty(set.Suggestions);
        Assert.Equal(CodingSuggestionPartStatus.NichtVerfuegbar, set.BogenTeil.Status);
    }

    [Fact]
    public async Task Ein_Bogen_ohne_Arbeitspunkt_laesst_Anfang_und_Ende_trotzdem_laufen()
    {
        var set = await CodingSuggestionScanUseCase.ExecuteAsync(
            Auftrag(),
            Aktionen(
                bends: (_, _) => Task.FromResult(BogenNichtNutzbar("kein Arbeitspunkt")),
                pipeEnds: (_, _) => Task.FromResult(EndenOk())),
            CancellationToken.None);

        Assert.Equal(CodingSuggestionPartStatus.NichtVerfuegbar, set.BogenTeil.Status);
        Assert.Equal("kein Arbeitspunkt", set.BogenTeil.Grund);
        Assert.Equal(2, set.Suggestions.Count);
        Assert.Empty(set.MeterTrack);
    }

    [Fact]
    public async Task Ein_technischer_Fehler_wird_Fehler_und_nie_eine_leere_Liste()
    {
        var set = await CodingSuggestionScanUseCase.ExecuteAsync(
            Auftrag(),
            Aktionen(
                bends: (_, _) => Task.FromResult(BogenOk()),
                pipeEnds: (_, _) => throw new InvalidOperationException("Sidecar nicht erreichbar")),
            CancellationToken.None);

        Assert.Equal(CodingSuggestionPartStatus.Fehler, set.AnfangEndeTeil.Status);
        Assert.Contains("Sidecar nicht erreichbar", set.AnfangEndeTeil.Grund);
        Assert.Single(set.Suggestions);
        Assert.Equal(CodingSuggestionKind.Bogen, set.Suggestions[0].Kind);
    }

    [Fact]
    public async Task Ein_Abbruch_wird_durchgereicht_und_markiert_nichts()
    {
        var markiert = false;
        using var quelle = new CancellationTokenSource();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            CodingSuggestionScanUseCase.ExecuteAsync(
                Auftrag(),
                Aktionen(
                    bends: (_, ct) => { quelle.Cancel(); ct.ThrowIfCancellationRequested(); return Task.FromResult(BogenOk()); },
                    pipeEnds: (_, _) => Task.FromResult(EndenOk()),
                    markExposed: _ => markiert = true),
                quelle.Token));

        Assert.False(markiert);
    }

    [Fact]
    public async Task Das_Gedaechtnis_wird_nur_bei_mindestens_einem_Vorschlag_gesetzt()
    {
        var markierte = new List<string>();

        await CodingSuggestionScanUseCase.ExecuteAsync(
            Auftrag(),
            Aktionen(
                bends: (_, _) => Task.FromResult(BogenOk() with { Suggestions = Array.Empty<BendSuggestion>() }),
                pipeEnds: (_, _) => Task.FromResult(EndenOk() with { Suggestions = Array.Empty<PipeEndSuggestion>() }),
                markExposed: markierte.Add),
            CancellationToken.None);
        Assert.Empty(markierte);

        await CodingSuggestionScanUseCase.ExecuteAsync(
            Auftrag(),
            Aktionen(
                bends: (_, _) => Task.FromResult(BogenOk()),
                pipeEnds: (_, _) => Task.FromResult(EndenOk()),
                markExposed: markierte.Add),
            CancellationToken.None);
        Assert.Equal(new[] { "H_1-2" }, markierte);
    }

    [Fact]
    public async Task Anfang_und_Ende_tragen_den_gepinnten_Abnahmewert_und_die_Meterspur_kommt_vom_Bogen()
    {
        var set = await CodingSuggestionScanUseCase.ExecuteAsync(
            Auftrag(),
            Aktionen(
                bends: (_, _) => Task.FromResult(BogenOk()),
                pipeEnds: (_, _) => Task.FromResult(EndenOk())),
            CancellationToken.None);

        var anfang = Assert.Single(set.Suggestions, s => s.Kind == CodingSuggestionKind.Rohranfang);
        Assert.Equal(PipeEndLernstufePins.Rohranfang.Precision, anfang.AcceptancePrecision);
        Assert.Equal(2, set.MeterTrack.Count);
    }

    [Fact]
    public void Der_Fortschritt_teilt_sich_in_zwei_Haelften()
    {
        Assert.Equal(0, CodingSuggestionScanUseCase.Percent(bogenPhase: true, 0, 100));
        Assert.Equal(25, CodingSuggestionScanUseCase.Percent(bogenPhase: true, 50, 100));
        Assert.Equal(50, CodingSuggestionScanUseCase.Percent(bogenPhase: false, 0, 100));
        Assert.Equal(100, CodingSuggestionScanUseCase.Percent(bogenPhase: false, 100, 100));
        Assert.Equal(0, CodingSuggestionScanUseCase.Percent(bogenPhase: true, 5, 0));
    }

    private static CodingSuggestionScanRequest Auftrag()
        => new(@"D:\Videos\H_1-2.mpg", "H_1-2", Enabled: true);

    private static CodingSuggestionScanActions Aktionen(
        Func<BendSuggestionScanRequest, CancellationToken, Task<BendSuggestionScanResult>> bends,
        Func<PipeEndScanRequest, CancellationToken, Task<PipeEndScanResult>> pipeEnds,
        Action<string>? markExposed = null)
        => new(bends, pipeEnds, markExposed ?? (_ => { }));

    private static BendSuggestionScanResult BogenOk()
        => new(
            true, string.Empty,
            [new BendSuggestion(9.42, 9.42, 30.0, 0.9, 4, BendSuggestionStrength.Strong)],
            60, 0, TimeSpan.FromSeconds(5),
            CodingBendCandidatePin.Id, CodingBendCandidatePin.WeightSha256, 0.5, 0.8, "Test",
            [new MeterTrackPoint(29.0, 9.0, false), new MeterTrackPoint(30.0, 9.42, false)]);

    private static BendSuggestionScanResult BogenNichtNutzbar(string grund)
        => new(false, grund, Array.Empty<BendSuggestion>(), 0, 0, TimeSpan.Zero,
            CodingBendCandidatePin.Id, CodingBendCandidatePin.WeightSha256, 0.0, 0.0);

    private static PipeEndScanResult EndenOk()
        => new(
            [
                new PipeEndSuggestion(PipeEndKind.Rohranfang, 3.0, 5.0, 4.0, 0.97, 3),
                new PipeEndSuggestion(PipeEndKind.Rohrende, 141.0, 145.0, 143.0, 0.91, 5)
            ],
            60, TimeSpan.FromSeconds(6), PipeEndLernstufePins.All);
}
```

- [ ] **Step 2: Test laufen lassen, Fehlschlag pruefen**

Run: `dotnet test tests/AuswertungPro.Next.Pipeline.Tests -o .tmp/testout-vorschlaege/pipe --filter "FullyQualifiedName~CodingSuggestionScanUseCaseTests" -v q`
Expected: Compilerfehler `CodingSuggestionScanUseCase` unbekannt.

- [ ] **Step 3: Minimalimplementierung**

`CodingSuggestionScanUseCase.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.UseCases.BendSuggestions;
using AuswertungPro.Next.Application.UseCases.PipeEndSuggestions;

namespace AuswertungPro.Next.Application.UseCases.CodingSuggestions;

/// <param name="Haltung">Sichtbarer Haltungsname; Schluessel des Sitzungsgedaechtnisses.</param>
/// <param name="Enabled">Schalter aus den Einstellungen; false startet nichts.</param>
public sealed record CodingSuggestionScanRequest(string VideoPath, string Haltung, bool Enabled);

/// <summary>Aussenverbindungen — eingehaengt, damit die Regeln ohne Sidecar pruefbar sind.</summary>
public sealed record CodingSuggestionScanActions(
    Func<BendSuggestionScanRequest, CancellationToken, Task<BendSuggestionScanResult>> ScanBends,
    Func<PipeEndScanRequest, CancellationToken, Task<PipeEndScanResult>> ScanPipeEnds,
    Action<string> MarkExposed)
{
    /// <summary>Gesamtfortschritt 0..100; optional, rein nach aussen.</summary>
    public Action<int>? ReportPercent { get; init; }
}

/// <summary>
/// Vorabdurchlauf fuer den Codiermodus: zuerst Bogen, dann Rohranfang/Rohrende
/// (alle drei Gewichte teilen den Slot YOLO_TEST). Jeder Teil faellt fuer sich
/// aus; ein technischer Fehler ist nie "kein Vorschlag"; ein Abbruch geht durch.
/// </summary>
public static class CodingSuggestionScanUseCase
{
    public static async Task<CodingSuggestionSet> ExecuteAsync(
        CodingSuggestionScanRequest request,
        CodingSuggestionScanActions actions,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actions);

        if (!request.Enabled)
            return CodingSuggestionSet.Leer("In den Einstellungen ausgeschaltet.");

        var vorschlaege = new List<CodingSuggestion>();
        IReadOnlyList<MeterTrackPoint> spur = Array.Empty<MeterTrackPoint>();

        // --- Teil 1: Bogen ---
        CodingSuggestionPartState bogenTeil;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var bogen = await actions.ScanBends(
                new BendSuggestionScanRequest
                {
                    VideoPath = request.VideoPath,
                    CandidateId = CodingBendCandidatePin.Id,
                    WeightSha256 = CodingBendCandidatePin.WeightSha256
                },
                cancellationToken).ConfigureAwait(false);

            if (bogen.IsUsable)
            {
                bogenTeil = CodingSuggestionPartState.Bereit;
                spur = bogen.MeterTrack;
                vorschlaege.AddRange(bogen.Suggestions.Select(s => new CodingSuggestion(
                    CodingSuggestionKind.Bogen,
                    s.PeakTimeSeconds,
                    s.MeterStart,
                    s.MeterIsEstimated,
                    s.MaxConfidence,
                    s.Strength == BendSuggestionStrength.Strong,
                    AcceptancePrecision: 0.0)));
            }
            else
            {
                bogenTeil = CodingSuggestionPartState.NichtVerfuegbar(bogen.Reason);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            bogenTeil = CodingSuggestionPartState.Fehler(ex.Message);
        }

        actions.ReportPercent?.Invoke(50);

        // --- Teil 2: Rohranfang / Rohrende ---
        CodingSuggestionPartState endenTeil;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var enden = await actions.ScanPipeEnds(
                new PipeEndScanRequest { VideoPath = request.VideoPath },
                cancellationToken).ConfigureAwait(false);

            endenTeil = CodingSuggestionPartState.Bereit;
            vorschlaege.AddRange(enden.Suggestions.Select(s => new CodingSuggestion(
                s.Kind == PipeEndKind.Rohranfang ? CodingSuggestionKind.Rohranfang : CodingSuggestionKind.Rohrende,
                s.PeakTimeSeconds,
                Meter: null,
                MeterIsEstimated: false,
                s.MaxConfidence,
                IsStrong: true,
                AcceptancePrecision: Pin(s.Kind).Precision)));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            endenTeil = CodingSuggestionPartState.Fehler(ex.Message);
        }

        actions.ReportPercent?.Invoke(100);

        var sortiert = vorschlaege.OrderBy(v => v.PeakTimeSeconds).ToList();
        if (sortiert.Count > 0)
            actions.MarkExposed(request.Haltung);

        return new CodingSuggestionSet(sortiert, spur, bogenTeil, endenTeil);
    }

    /// <summary>Bogen belegt 0..50 %, Anfang/Ende 50..100 %.</summary>
    public static int Percent(bool bogenPhase, int processed, int total)
    {
        if (total <= 0)
            return bogenPhase ? 0 : 50;
        var anteil = Math.Clamp(processed / (double)total, 0.0, 1.0);
        var basis = bogenPhase ? 0.0 : 50.0;
        return (int)Math.Round(basis + anteil * 50.0);
    }

    private static PipeEndLernstufePin Pin(PipeEndKind kind)
        => kind == PipeEndKind.Rohranfang ? PipeEndLernstufePins.Rohranfang : PipeEndLernstufePins.Rohrende;
}
```

`ICodingSuggestionScanService.cs`:

```csharp
using System;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.UseCases.BendSuggestions;
using AuswertungPro.Next.Application.UseCases.PipeEndSuggestions;

namespace AuswertungPro.Next.Application.UseCases.CodingSuggestions;

/// <summary>Vorabdurchlauf fuer den Codiermodus; der Player kennt nur diesen Vertrag.</summary>
public interface ICodingSuggestionScanService
{
    Task<CodingSuggestionSet> ScanAsync(
        CodingSuggestionScanRequest request,
        CancellationToken cancellationToken,
        IProgress<int>? percent = null);
}

/// <summary>
/// Verdrahtet den UseCase mit den zwei bestehenden Durchlaeufen und dem
/// Sitzungsgedaechtnis. Enthaelt selbst keine Regel.
/// </summary>
public sealed class CodingSuggestionScanService : ICodingSuggestionScanService
{
    private readonly IBendSuggestionScanService _bends;
    private readonly IPipeEndSuggestionScanService _pipeEnds;
    private readonly ICodingSuggestionExposure _exposure;

    public CodingSuggestionScanService(
        IBendSuggestionScanService bends,
        IPipeEndSuggestionScanService pipeEnds,
        ICodingSuggestionExposure exposure)
    {
        _bends = bends ?? throw new ArgumentNullException(nameof(bends));
        _pipeEnds = pipeEnds ?? throw new ArgumentNullException(nameof(pipeEnds));
        _exposure = exposure ?? throw new ArgumentNullException(nameof(exposure));
    }

    public Task<CodingSuggestionSet> ScanAsync(
        CodingSuggestionScanRequest request,
        CancellationToken cancellationToken,
        IProgress<int>? percent = null)
    {
        var bogenFortschritt = percent is null
            ? null
            : new Progress<BendSuggestionScanProgress>(p =>
                percent.Report(CodingSuggestionScanUseCase.Percent(true, p.Processed, p.Total)));
        var endenFortschritt = percent is null
            ? null
            : new Progress<PipeEndScanProgress>(p =>
                percent.Report(CodingSuggestionScanUseCase.Percent(false, p.Processed, p.Total)));

        return CodingSuggestionScanUseCase.ExecuteAsync(
            request,
            new CodingSuggestionScanActions(
                ScanBends: (r, ct) => _bends.ScanAsync(r, ct, bogenFortschritt),
                ScanPipeEnds: (r, ct) => _pipeEnds.ScanAsync(r, ct, endenFortschritt),
                MarkExposed: _exposure.MarkExposed)
            {
                ReportPercent = percent is null ? null : percent.Report
            },
            cancellationToken);
    }
}
```

- [ ] **Step 4: Tests laufen lassen**

Run: `dotnet test tests/AuswertungPro.Next.Pipeline.Tests -o .tmp/testout-vorschlaege/pipe --filter "FullyQualifiedName~CodingSuggestion" -v q`
Expected: alle Tests der drei neuen Klassen gruen.

- [ ] **Step 5: Commit**

```bash
git add src/AuswertungPro.Next.Application/UseCases/CodingSuggestions tests/AuswertungPro.Next.Pipeline.Tests/CodingSuggestionScanUseCaseTests.cs
git commit -m "Codiermodus-Vorschlaege: Durchlauf Bogen vor Anfang/Ende mit Teilausfall und Gedaechtnis" -m "Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 5: Registrierung im ServiceProvider

**Files:**
- Create: `src/AuswertungPro.Next.UI/ServiceProvider.CodingSuggestions.cs`
- Modify: `src/AuswertungPro.Next.UI/ServiceProviderRegistrationMap.cs` (Zeile mit `IPipeEndSuggestionScanService`)
- Test: `tests/AuswertungPro.Next.UI.Tests/ServiceProviderRegistrationTests.cs` (Zaehler 157 -> 158)

**Interfaces:**
- Consumes: `ServiceProvider.BendSuggestionScan`, `ServiceProvider.PipeEndSuggestionScan`, `ServiceProvider.CodingSuggestionExposure`
- Produces: `ServiceProvider.CodingSuggestionScan` (`ICodingSuggestionScanService`)

- [ ] **Step 1: Test anpassen (faellt danach)**

In `ServiceProviderRegistrationTests.cs` hinter dem Kommentar `// 156 -> 157: …` einfuegen und beide `157` auf `158` setzen:

```csharp
        // 157 -> 158: ICodingSuggestionScanService fuehrt im Codiermodus den Vorabdurchlauf
        // (Bogen, dann Rohranfang/Rohrende) und setzt das Sitzungsgedaechtnis — der Player
        // kennt nur diesen Vertrag, keine Modellwahl.
        Assert.True(
            registrations.Count == 158,
            $"Erwartet 158 Registrierungen, tatsaechlich {registrations.Count}. Bei einem neuen " +
```

- [ ] **Step 2: Test laufen lassen, Fehlschlag pruefen**

Run: `dotnet test tests/AuswertungPro.Next.UI.Tests -o .tmp/testout-vorschlaege/ui --filter "FullyQualifiedName~ServiceProviderRegistrationTests" -v q`
Expected: rot, "Erwartet 158 Registrierungen, tatsaechlich 157".

- [ ] **Step 3: Implementierung**

`ServiceProvider.CodingSuggestions.cs`:

```csharp
using AuswertungPro.Next.Application.UseCases.CodingSuggestions;

namespace AuswertungPro.Next.UI;

public sealed partial class ServiceProvider
{
    private ICodingSuggestionScanService? _codingSuggestionScan;

    /// <summary>
    /// Vorabdurchlauf des Codiermodus. Baut auf den zwei Training-Studio-Diensten
    /// auf; der Bogen-Kandidat ist in Application fest gepinnt.
    /// </summary>
    public ICodingSuggestionScanService CodingSuggestionScan
        => _codingSuggestionScan ??= new CodingSuggestionScanService(
            BendSuggestionScan,
            PipeEndSuggestionScan,
            CodingSuggestionExposure);
}
```

In `ServiceProviderRegistrationMap.cs` `using AuswertungPro.Next.Application.UseCases.CodingSuggestions;` ergaenzen und direkt nach der Zeile `[typeof(IPipeEndSuggestionScanService)] = services.PipeEndSuggestionScan,` einfuegen:

```csharp
            [typeof(ICodingSuggestionScanService)] = services.CodingSuggestionScan,
```

- [ ] **Step 4: Tests laufen lassen**

Run: `dotnet test tests/AuswertungPro.Next.UI.Tests -o .tmp/testout-vorschlaege/ui --filter "FullyQualifiedName~ServiceProviderRegistrationTests" -v q`
Expected: gruen.

- [ ] **Step 5: Commit**

```bash
git add src/AuswertungPro.Next.UI/ServiceProvider.CodingSuggestions.cs src/AuswertungPro.Next.UI/ServiceProviderRegistrationMap.cs tests/AuswertungPro.Next.UI.Tests/ServiceProviderRegistrationTests.cs
git commit -m "Codiermodus-Vorschlaege: Dienst im ServiceProvider registriert (158)" -m "Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 6: Schalter in den Einstellungen

**Files:**
- Modify: `src/AuswertungPro.Next.UI/AppSettings.cs` (neben `AiStartOnProgramStart`, Zeile ~365)
- Modify: `src/AuswertungPro.Next.UI/Settings/SettingsSaveWorkflow.cs` (`SettingsSaveValues` + Zuweisung bei Zeile ~73)
- Modify: `src/AuswertungPro.Next.UI/ViewModels/Pages/SettingsPageViewModel.cs` (Laden Zeile ~319, Speichern Zeile ~501)
- Modify: `src/AuswertungPro.Next.UI/Views/Pages/SettingsPage.xaml` (Gruppe "KI-Laufzeit", nach der Autostart-Zeile)
- Test: `tests/AuswertungPro.Next.UI.Tests/SettingsSaveWorkflowTests.cs`

**Interfaces:**
- Produces: `AppSettings.CodingSuggestionsEnabled` (bool, Standard true), `SettingsSaveValues.CodingSuggestionsEnabled` (letzter Parameter, Standard true), `SettingsPageViewModel.CodingSuggestionsEnabled`.

- [ ] **Step 1: Fehlschlagenden Test schreiben**

In `SettingsSaveWorkflowTests.cs` einen Test ergaenzen. Vorhandene Tests der Datei zeigen, wie `SettingsSaveValues` und `SettingsSaveWorkflowRequest` gebaut werden; denselben Aufbau kopieren und nur den neuen Wert pruefen:

```csharp
    [Fact]
    public void Der_Schalter_fuer_KI_Vorschlaege_im_Codiermodus_wird_gespeichert()
    {
        var settings = new AppSettings();
        Assert.True(settings.CodingSuggestionsEnabled); // Standard: ein

        var values = MinimalValues with { CodingSuggestionsEnabled = false };
        SettingsSaveWorkflow.Save(new SettingsSaveWorkflowRequest(settings, new DiagnosticsOptions(), values, () => { }));

        Assert.False(settings.CodingSuggestionsEnabled);
    }
```

`MinimalValues` ist der in der Datei vorhandene statische Helfer (Zeile ~130) fuer ein vollstaendiges `SettingsSaveValues`; `SettingsSaveWorkflowRequest` hat vier Pflichtargumente `(Settings, Diagnostics, Values, SaveSettings)`.

- [ ] **Step 2: Test laufen lassen, Fehlschlag pruefen**

Run: `dotnet test tests/AuswertungPro.Next.UI.Tests -o .tmp/testout-vorschlaege/ui --filter "FullyQualifiedName~SettingsSaveWorkflowTests" -v q`
Expected: Compilerfehler `CodingSuggestionsEnabled` unbekannt.

- [ ] **Step 3: Implementierung**

`AppSettings.cs`, direkt nach `public bool AiStartOnProgramStart { get; set; }`:

```csharp
    /// <summary>
    /// Vorabdurchlauf (Bogen, Rohranfang, Rohrende) beim Oeffnen des Codiermodus.
    /// Standard ein; ein fehlender Wert in settings.json bleibt ein.
    /// </summary>
    public bool CodingSuggestionsEnabled { get; set; } = true;
```

`SettingsSaveWorkflow.cs`: `SettingsSaveValues` um einen letzten Parameter erweitern: `string? SearchChApiKey = null, bool CodingSuggestionsEnabled = true);` und bei der Zuweisung nach `settings.AiStartOnProgramStart = values.StartAiOnProgramStart;` ergaenzen: `settings.CodingSuggestionsEnabled = values.CodingSuggestionsEnabled;`

`SettingsPageViewModel.cs`: Feld `[ObservableProperty] private bool _codingSuggestionsEnabled = true;` bei den anderen `[ObservableProperty]`-Feldern; beim Laden nach `StartAiOnProgramStart = _settings.AiStartOnProgramStart;` die Zeile `CodingSuggestionsEnabled = _settings.CodingSuggestionsEnabled;`; beim Speichern im `new SettingsSaveValues(...)` nach `SearchChApiKey` das Argument `CodingSuggestionsEnabled` anhaengen.

`SettingsPage.xaml`, Gruppe `KI-Laufzeit`: eine vierte `RowDefinition` ergaenzen und nach der Zeile mit `Content="KI beim Programmstart starten"` einfuegen (Zeilenindizes der folgenden Elemente um eins erhoehen):

```xml
                                <TextBlock Grid.Row="1" Grid.Column="0" Text="Codiermodus" Style="{StaticResource SettingsFieldLabel}"/>
                                <CheckBox Grid.Row="1" Grid.Column="1" Grid.ColumnSpan="2"
                                          Content="KI-Vorschläge beim Codieren (Bogen, Rohranfang, Rohrende)"
                                          IsChecked="{Binding CodingSuggestionsEnabled}"
                                          ToolTip="Beim Öffnen des Codiermodus prüft die KI das Video im Hintergrund und schlägt Bogen, Rohranfang und Rohrende zum Bestätigen vor."
                                          Style="{StaticResource SettingsFieldCheckBox}"/>
```

- [ ] **Step 4: Tests laufen lassen**

Run: `dotnet test tests/AuswertungPro.Next.UI.Tests -o .tmp/testout-vorschlaege/ui --filter "FullyQualifiedName~SettingsSaveWorkflowTests|FullyQualifiedName~SettingsSearch|FullyQualifiedName~DesignAudit" -v q`
Expected: gruen (auch die Design-Waechter: Umlaute im sichtbaren Text, kein fester FontSize).

- [ ] **Step 5: Commit**

```bash
git add src/AuswertungPro.Next.UI/AppSettings.cs src/AuswertungPro.Next.UI/Settings/SettingsSaveWorkflow.cs src/AuswertungPro.Next.UI/ViewModels/Pages/SettingsPageViewModel.cs src/AuswertungPro.Next.UI/Views/Pages/SettingsPage.xaml tests/AuswertungPro.Next.UI.Tests/SettingsSaveWorkflowTests.cs
git commit -m "Einstellungen: Schalter fuer KI-Vorschlaege beim Codieren (Standard ein)" -m "Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 7: Vierter Schritt der Hintergrunddienste

**Files:**
- Modify: `src/AuswertungPro.Next.Application/UseCases/CodingModeBackgroundServicesWorkflow.cs`
- Modify: `src/AuswertungPro.Next.UI/Views/Windows/PlayerWindow.Coding.Lifecycle.Ui.cs` (Methode `StartCodingModeBackgroundServices`)
- Test: `tests/AuswertungPro.Next.Pipeline.Tests/CodingModeBackgroundServicesWorkflowTests.cs`

**Interfaces:**
- Produces: `CodingModeBackgroundServicesWorkflowActions(Action StartCodingAiInitialization, Action StartCodingOsdTimer, Action ShowInitialOsdMeterBadge, Action StartSuggestionScan)`
- Consumes (Task 10): `PlayerWindow.StartSuggestionScan()`

- [ ] **Step 1: Fehlschlagenden Test schreiben**

```csharp
using System.Collections.Generic;
using AuswertungPro.Next.Application.UseCases;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

public sealed class CodingModeBackgroundServicesWorkflowTests
{
    [Fact]
    public void Der_Vorabdurchlauf_startet_als_vierter_Schritt_nach_der_KI_Initialisierung()
    {
        var reihenfolge = new List<string>();

        CodingModeBackgroundServicesWorkflow.Execute(
            new CodingModeBackgroundServicesWorkflowActions(
                StartCodingAiInitialization: () => reihenfolge.Add("ki"),
                StartCodingOsdTimer: () => reihenfolge.Add("osd"),
                ShowInitialOsdMeterBadge: () => reihenfolge.Add("badge"),
                StartSuggestionScan: () => reihenfolge.Add("vorschlaege")));

        Assert.Equal(new[] { "ki", "osd", "badge", "vorschlaege" }, reihenfolge);
    }
}
```

- [ ] **Step 2: Test laufen lassen, Fehlschlag pruefen**

Run: `dotnet test tests/AuswertungPro.Next.Pipeline.Tests -o .tmp/testout-vorschlaege/pipe --filter "FullyQualifiedName~CodingModeBackgroundServicesWorkflowTests" -v q`
Expected: Compilerfehler, Parameter `StartSuggestionScan` unbekannt.

- [ ] **Step 3: Implementierung**

`CodingModeBackgroundServicesWorkflow.cs`:

```csharp
namespace AuswertungPro.Next.Application.UseCases;

public sealed record CodingModeBackgroundServicesWorkflowActions(
    Action StartCodingAiInitialization,
    Action StartCodingOsdTimer,
    Action ShowInitialOsdMeterBadge,
    Action StartSuggestionScan);

public static class CodingModeBackgroundServicesWorkflow
{
    public static void Execute(CodingModeBackgroundServicesWorkflowActions actions)
    {
        ArgumentNullException.ThrowIfNull(actions);

        actions.StartCodingAiInitialization();
        actions.StartCodingOsdTimer();
        actions.ShowInitialOsdMeterBadge();
        // Zuletzt: Der Vorabdurchlauf wartet intern die KI-Bereitschaft ab und
        // darf keinen der drei sofortigen Schritte verzoegern.
        actions.StartSuggestionScan();
    }
}
```

`PlayerWindow.Coding.Lifecycle.Ui.cs`, in `StartCodingModeBackgroundServices` das vierte Argument ergaenzen: `StartSuggestionScan: StartSuggestionScan));` — die Methode `StartSuggestionScan` entsteht in Task 10. Damit dieser Task fuer sich baut, vorerst in `PlayerWindow.Coding.Lifecycle.Ui.cs` eine leere Methode anlegen, die Task 10 in seine eigene Datei verschiebt:

```csharp
    /// <summary>Wird in PlayerWindow.Coding.Suggestions.cs ausgefuellt.</summary>
    private void StartSuggestionScan() { }
```

- [ ] **Step 4: Tests und Build**

Run: `dotnet build src/AuswertungPro.Next.UI/AuswertungPro.Next.UI.csproj -o .tmp/testout-vorschlaege/ui -v q` und `dotnet test tests/AuswertungPro.Next.Pipeline.Tests -o .tmp/testout-vorschlaege/pipe --filter "FullyQualifiedName~CodingModeBackgroundServicesWorkflowTests" -v q`
Expected: Build 0 Fehler, Test gruen.

- [ ] **Step 5: Commit**

```bash
git add src/AuswertungPro.Next.Application/UseCases/CodingModeBackgroundServicesWorkflow.cs src/AuswertungPro.Next.UI/Views/Windows/PlayerWindow.Coding.Lifecycle.Ui.cs tests/AuswertungPro.Next.Pipeline.Tests/CodingModeBackgroundServicesWorkflowTests.cs
git commit -m "Codiermodus: Vorabdurchlauf als vierter Hintergrundschritt vorgesehen" -m "Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 8: Sichtbare Liste — Zeile und Owner

**Files:**
- Create: `src/AuswertungPro.Next.UI/Player/CodingSuggestionRow.cs`
- Create: `src/AuswertungPro.Next.UI/Player/CodingSuggestionsOwner.cs`
- Test: `tests/AuswertungPro.Next.UI.Tests/CodingSuggestionsOwnerTests.cs`

**Interfaces:**
- Consumes: `CodingSuggestion`, `CodingSuggestionSet`, `CodingSuggestionText.Zeile/Art`, `CodingSuggestionPartStatus`
- Produces:
  - `CodingSuggestionRow` (INotifyPropertyChanged): `CodingSuggestion Suggestion`, `CodingSuggestionKind Kind`, `string Text`, `double TimeSeconds`, `string Glyph`, `bool IsConfirmed`
  - `CodingSuggestionsOwner` (INotifyPropertyChanged): `ObservableCollection<CodingSuggestionRow> Rows`, `string HeaderText`, `string HintText`, `bool IsScanning`, `int OpenCount`, `IReadOnlyList<MeterTrackPoint> MeterTrack`, `void BeginScan()`, `void SetPercent(int)`, `void Apply(CodingSuggestionSet)`, `void Fail(string)`, `void Confirm(CodingSuggestionRow)`, `void Reject(CodingSuggestionRow)`, `void Clear()`

- [ ] **Step 1: Fehlschlagende Tests schreiben**

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using AuswertungPro.Next.Application.UseCases.BendSuggestions;
using AuswertungPro.Next.Application.UseCases.CodingSuggestions;
using AuswertungPro.Next.UI.Player;
using Xunit;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingSuggestionsOwnerTests
{
    [Fact]
    public void Waehrend_des_Durchlaufs_zeigt_der_Kopf_den_Fortschritt()
    {
        var owner = new CodingSuggestionsOwner();
        owner.BeginScan();
        owner.SetPercent(43);

        Assert.True(owner.IsScanning);
        Assert.Equal("KI prüft Video … 43 %", owner.HeaderText);
    }

    [Fact]
    public void Nach_dem_Durchlauf_zaehlt_der_Kopf_die_offenen_Vorschlaege()
    {
        var owner = new CodingSuggestionsOwner();
        owner.BeginScan();
        owner.Apply(Set(Bogen(30), Anfang(4)));

        Assert.False(owner.IsScanning);
        Assert.Equal(2, owner.Rows.Count);
        Assert.Equal("KI-VORSCHLÄGE (2)", owner.HeaderText);
        Assert.Equal(string.Empty, owner.HintText);
        Assert.Equal(new[] { 4.0, 30.0 }, owner.Rows.Select(r => r.TimeSeconds));
    }

    [Fact]
    public void Ein_ausgefallener_Teil_steht_als_Hinweis_da()
    {
        var owner = new CodingSuggestionsOwner();
        owner.Apply(new CodingSuggestionSet(
            [Anfang(4)],
            Array.Empty<MeterTrackPoint>(),
            CodingSuggestionPartState.NichtVerfuegbar("kein Arbeitspunkt"),
            CodingSuggestionPartState.Bereit));

        Assert.Equal("Bogen: kein Arbeitspunkt", owner.HintText);
    }

    [Fact]
    public void Bestaetigen_graut_aus_und_Ablehnen_entfernt()
    {
        var owner = new CodingSuggestionsOwner();
        owner.Apply(Set(Bogen(30), Anfang(4), Ende(143)));

        owner.Confirm(owner.Rows[0]);
        Assert.True(owner.Rows[0].IsConfirmed);
        Assert.Equal(3, owner.Rows.Count);
        Assert.Equal(2, owner.OpenCount);
        Assert.Equal("KI-VORSCHLÄGE (2)", owner.HeaderText);

        owner.Reject(owner.Rows[2]);
        Assert.Equal(2, owner.Rows.Count);
        Assert.Equal(1, owner.OpenCount);
    }

    [Fact]
    public void Fehler_und_Clear_raeumen_auf()
    {
        var owner = new CodingSuggestionsOwner();
        owner.BeginScan();
        owner.Fail("Sidecar nicht erreichbar");
        Assert.False(owner.IsScanning);
        Assert.Equal("KI-Vorschläge nicht verfügbar", owner.HeaderText);
        Assert.Equal("Sidecar nicht erreichbar", owner.HintText);

        owner.Apply(Set(Bogen(30)));
        owner.Clear();
        Assert.Empty(owner.Rows);
        Assert.Empty(owner.MeterTrack);
        Assert.Equal("KI-VORSCHLÄGE", owner.HeaderText);
    }

    [Fact]
    public void Die_Zeile_traegt_Text_und_Glyph_je_Art()
    {
        var zeile = new CodingSuggestionRow(Bogen(30));
        Assert.Equal("Bogen · Meter 9,42 · stark", zeile.Text);
        Assert.False(string.IsNullOrEmpty(zeile.Glyph));
        Assert.NotEqual(new CodingSuggestionRow(Anfang(4)).Glyph, new CodingSuggestionRow(Ende(143)).Glyph);
    }

    private static CodingSuggestionSet Set(params CodingSuggestion[] v)
        => new(v.OrderBy(s => s.PeakTimeSeconds).ToList(), [new MeterTrackPoint(30, 9.42, false)],
            CodingSuggestionPartState.Bereit, CodingSuggestionPartState.Bereit);

    private static CodingSuggestion Bogen(double s) => new(CodingSuggestionKind.Bogen, s, 9.42, false, 0.9, true, 0);
    private static CodingSuggestion Anfang(double s) => new(CodingSuggestionKind.Rohranfang, s, null, false, 0.97, true, 0.8545);
    private static CodingSuggestion Ende(double s) => new(CodingSuggestionKind.Rohrende, s, null, false, 0.91, true, 0.8889);
}
```

- [ ] **Step 2: Test laufen lassen, Fehlschlag pruefen**

Run: `dotnet test tests/AuswertungPro.Next.UI.Tests -o .tmp/testout-vorschlaege/ui --filter "FullyQualifiedName~CodingSuggestionsOwnerTests" -v q`
Expected: Compilerfehler.

- [ ] **Step 3: Implementierung**

`CodingSuggestionRow.cs`:

```csharp
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using AuswertungPro.Next.Application.UseCases.CodingSuggestions;

namespace AuswertungPro.Next.UI.Player;

/// <summary>Eine Zeile der Karte "KI-Vorschlaege" — reine Anzeige, kein Ereignis.</summary>
public sealed class CodingSuggestionRow : INotifyPropertyChanged
{
    private bool _isConfirmed;

    public CodingSuggestionRow(CodingSuggestion suggestion)
    {
        Suggestion = suggestion ?? throw new ArgumentNullException(nameof(suggestion));
        Text = CodingSuggestionText.Zeile(suggestion);
        Glyph = suggestion.Kind switch
        {
            CodingSuggestionKind.Bogen => "\uE7AD",       // Bogen: gebogener Pfeil
            CodingSuggestionKind.Rohranfang => "\uE72A",  // Rohranfang: Pfeil nach rechts
            CodingSuggestionKind.Rohrende => "\uE73E",    // Rohrende: Haken
            _ => "\uE946"
        };
    }

    public CodingSuggestion Suggestion { get; }
    public CodingSuggestionKind Kind => Suggestion.Kind;
    public string Text { get; }
    public string Glyph { get; }
    public double TimeSeconds => Suggestion.PeakTimeSeconds;

    public bool IsConfirmed
    {
        get => _isConfirmed;
        set { if (_isConfirmed == value) return; _isConfirmed = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
```

`CodingSuggestionsOwner.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using AuswertungPro.Next.Application.UseCases.BendSuggestions;
using AuswertungPro.Next.Application.UseCases.CodingSuggestions;

namespace AuswertungPro.Next.UI.Player;

/// <summary>
/// Zustand der Karte "KI-Vorschlaege": Zeilen, Kopftext, Hinweis, Meterspur.
/// Bestaetigen graut aus (die Zeile bleibt als Beleg), Ablehnen entfernt.
/// </summary>
public sealed class CodingSuggestionsOwner : INotifyPropertyChanged
{
    private const string Titel = "KI-VORSCHLÄGE";

    private string _headerText = Titel;
    private string _hintText = string.Empty;
    private bool _isScanning;

    public ObservableCollection<CodingSuggestionRow> Rows { get; } = new();

    public IReadOnlyList<MeterTrackPoint> MeterTrack { get; private set; } = Array.Empty<MeterTrackPoint>();

    public string HeaderText { get => _headerText; private set => Set(ref _headerText, value); }
    public string HintText { get => _hintText; private set => Set(ref _hintText, value); }
    public bool IsScanning { get => _isScanning; private set => Set(ref _isScanning, value); }

    public int OpenCount => Rows.Count(r => !r.IsConfirmed);

    public void BeginScan()
    {
        Clear();
        IsScanning = true;
        SetPercent(0);
    }

    public void SetPercent(int percent)
    {
        if (!IsScanning) return;
        HeaderText = $"KI prüft Video … {Math.Clamp(percent, 0, 100)} %";
    }

    public void Apply(CodingSuggestionSet set)
    {
        ArgumentNullException.ThrowIfNull(set);
        IsScanning = false;
        Rows.Clear();
        foreach (var s in set.Suggestions.OrderBy(s => s.PeakTimeSeconds))
            Rows.Add(new CodingSuggestionRow(s));
        MeterTrack = set.MeterTrack;
        HintText = string.Join(" · ", new[]
        {
            Hinweis("Bogen", set.BogenTeil),
            Hinweis("Rohranfang/Rohrende", set.AnfangEndeTeil)
        }.Where(t => t.Length > 0));
        RefreshHeader();
    }

    public void Fail(string grund)
    {
        IsScanning = false;
        HeaderText = "KI-Vorschläge nicht verfügbar";
        HintText = grund ?? string.Empty;
    }

    public void Confirm(CodingSuggestionRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        row.IsConfirmed = true;
        RefreshHeader();
    }

    public void Reject(CodingSuggestionRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        Rows.Remove(row);
        RefreshHeader();
    }

    public void Clear()
    {
        Rows.Clear();
        MeterTrack = Array.Empty<MeterTrackPoint>();
        IsScanning = false;
        HintText = string.Empty;
        HeaderText = Titel;
    }

    private void RefreshHeader()
    {
        HeaderText = Rows.Count == 0 ? Titel : $"{Titel} ({OpenCount})";
        OnPropertyChanged(nameof(OpenCount));
    }

    private static string Hinweis(string teil, CodingSuggestionPartState state)
        => state.Status == CodingSuggestionPartStatus.Bereit ? string.Empty : $"{teil}: {state.Grund}";

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        OnPropertyChanged(name);
    }

    private void OnPropertyChanged(string? name)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
```

- [ ] **Step 4: Tests laufen lassen**

Run: `dotnet test tests/AuswertungPro.Next.UI.Tests -o .tmp/testout-vorschlaege/ui --filter "FullyQualifiedName~CodingSuggestionsOwnerTests" -v q`
Expected: 6 Tests gruen.

- [ ] **Step 5: Commit**

```bash
git add src/AuswertungPro.Next.UI/Player/CodingSuggestionRow.cs src/AuswertungPro.Next.UI/Player/CodingSuggestionsOwner.cs tests/AuswertungPro.Next.UI.Tests/CodingSuggestionsOwnerTests.cs
git commit -m "Codiermodus-Vorschlaege: Zeilen und Kartenzustand" -m "Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 9: Zeitleistenmarker

**Files:**
- Create: `src/AuswertungPro.Next.UI/Player/SuggestionMarkerLayout.cs`
- Create: `src/AuswertungPro.Next.UI/Player/SuggestionMarkerController.cs`
- Modify: `src/AuswertungPro.Next.UI/Views/Windows/PlayerWindow.xaml` (nach `DamageMarkerCanvas`, Zeile ~846)
- Test: `tests/AuswertungPro.Next.UI.Tests/SuggestionMarkerLayoutTests.cs`, `tests/AuswertungPro.Next.UI.Tests/SuggestionMarkerControllerTests.cs`

**Interfaces:**
- Consumes: `CodingSuggestionRow`, `PlayerSliderTrackBounds.Resolve(Slider, FrameworkElement)`
- Produces:
  - `static double? SuggestionMarkerLayout.CalculateX(double timeSeconds, double durationSeconds, double offsetX, double trackWidth)`
  - `SuggestionMarkerController(Canvas canvas, Func<(double offsetX, double trackWidth)> getBounds, Func<double?> getDurationSeconds, Action<double> seekToSeconds)` mit `Build(IReadOnlyList<CodingSuggestionRow>)`, `Reposition()`, `Clear()`

- [ ] **Step 1: Fehlschlagende Tests schreiben**

`SuggestionMarkerLayoutTests.cs`:

```csharp
using AuswertungPro.Next.UI.Player;
using Xunit;

namespace AuswertungPro.Next.UI.Tests;

public sealed class SuggestionMarkerLayoutTests
{
    [Fact]
    public void Position_folgt_der_Videozeit_auf_der_Spurbreite()
    {
        Assert.Equal(10.0, SuggestionMarkerLayout.CalculateX(0, 200, 10, 400));
        Assert.Equal(210.0, SuggestionMarkerLayout.CalculateX(100, 200, 10, 400));
        Assert.Equal(410.0, SuggestionMarkerLayout.CalculateX(200, 200, 10, 400));
    }

    [Fact]
    public void Ausserhalb_der_Dauer_oder_ohne_Dauer_gibt_es_keine_Lage()
    {
        Assert.Null(SuggestionMarkerLayout.CalculateX(201, 200, 10, 400));
        Assert.Null(SuggestionMarkerLayout.CalculateX(-1, 200, 10, 400));
        Assert.Null(SuggestionMarkerLayout.CalculateX(5, 0, 10, 400));
        Assert.Null(SuggestionMarkerLayout.CalculateX(5, 200, 10, 0));
    }
}
```

`SuggestionMarkerControllerTests.cs` (STA wie `CodingBendMarkerOverlayControllerTests`; den dortigen `RunOnSta`-Helfer hier erneut definieren):

```csharp
using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows.Controls;
using AuswertungPro.Next.Application.UseCases.CodingSuggestions;
using AuswertungPro.Next.UI.Player;
using Xunit;

namespace AuswertungPro.Next.UI.Tests;

public sealed class SuggestionMarkerControllerTests
{
    [Fact]
    public void Build_zeichnet_je_offenem_Vorschlag_einen_Marker_und_Clear_entfernt_alle()
    {
        var result = RunOnSta(() =>
        {
            var canvas = new Canvas();
            var gesprungen = new List<double>();
            var controller = new SuggestionMarkerController(
                canvas,
                () => (0.0, 400.0),
                () => 200.0,
                gesprungen.Add);

            var rows = new List<CodingSuggestionRow>
            {
                new(new CodingSuggestion(CodingSuggestionKind.Rohranfang, 4, null, false, 0.9, true, 0.85)),
                new(new CodingSuggestion(CodingSuggestionKind.Bogen, 100, 9.4, false, 0.9, true, 0)),
                new(new CodingSuggestion(CodingSuggestionKind.Rohrende, 500, null, false, 0.9, true, 0.89)) // ausserhalb
            };
            controller.Build(rows);
            var nachBuild = canvas.Children.Count;
            var links = Canvas.GetLeft(canvas.Children[1]);
            controller.Clear();
            return (nachBuild, links, canvas.Children.Count);
        });

        Assert.Equal(2, result.nachBuild);
        Assert.Equal(200.0 - 1, result.links, 3);
        Assert.Equal(0, result.Item3);
    }

    private static T RunOnSta<T>(Func<T> func)
    {
        T result = default!;
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try { result = func(); }
            catch (Exception ex) { error = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (error is not null) throw error;
        return result;
    }
}
```

- [ ] **Step 2: Test laufen lassen, Fehlschlag pruefen**

Run: `dotnet test tests/AuswertungPro.Next.UI.Tests -o .tmp/testout-vorschlaege/ui --filter "FullyQualifiedName~SuggestionMarker" -v q`
Expected: Compilerfehler.

- [ ] **Step 3: Implementierung**

`SuggestionMarkerLayout.cs`:

```csharp
namespace AuswertungPro.Next.UI.Player;

/// <summary>Lage eines Vorschlagsmarkers auf der Zeitleiste — nach Videozeit, nicht nach Meter.</summary>
public static class SuggestionMarkerLayout
{
    public static double? CalculateX(double timeSeconds, double durationSeconds, double offsetX, double trackWidth)
    {
        if (durationSeconds <= 0 || trackWidth <= 0)
            return null;
        if (timeSeconds < 0 || timeSeconds > durationSeconds)
            return null;
        return offsetX + timeSeconds / durationSeconds * trackWidth;
    }
}
```

`SuggestionMarkerController.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Rectangle = System.Windows.Shapes.Rectangle;

namespace AuswertungPro.Next.UI.Player;

/// <summary>
/// Zeichnet die KI-Vorschlaege als kleine Marker unter der Zeitleiste. Eigene
/// Flaeche und zweite Farbe (SecondaryAccentBrush), damit sie sich von den
/// Befundmarkern unterscheiden. Klick springt zur Videozeit.
/// </summary>
public sealed class SuggestionMarkerController
{
    private readonly Canvas _canvas;
    private readonly Func<(double offsetX, double trackWidth)> _getBounds;
    private readonly Func<double?> _getDurationSeconds;
    private readonly Action<double> _seekToSeconds;
    private readonly List<(CodingSuggestionRow Row, FrameworkElement Element)> _marker = new();

    public SuggestionMarkerController(
        Canvas canvas,
        Func<(double offsetX, double trackWidth)> getBounds,
        Func<double?> getDurationSeconds,
        Action<double> seekToSeconds)
    {
        _canvas = canvas ?? throw new ArgumentNullException(nameof(canvas));
        _getBounds = getBounds ?? throw new ArgumentNullException(nameof(getBounds));
        _getDurationSeconds = getDurationSeconds ?? throw new ArgumentNullException(nameof(getDurationSeconds));
        _seekToSeconds = seekToSeconds ?? throw new ArgumentNullException(nameof(seekToSeconds));
    }

    public void Build(IReadOnlyList<CodingSuggestionRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        Clear();

        var brush = _canvas.TryFindResource("SecondaryAccentBrush") as Brush ?? Brushes.Gray;
        var dauer = _getDurationSeconds() ?? 0.0;
        var (offsetX, trackWidth) = _getBounds();

        foreach (var row in rows)
        {
            if (SuggestionMarkerLayout.CalculateX(row.TimeSeconds, dauer, offsetX, trackWidth) is not { } x)
                continue;

            var tick = new Rectangle
            {
                Width = 3,
                Height = 8,
                RadiusX = 1.5,
                RadiusY = 1.5,
                Fill = brush,
                Opacity = row.IsConfirmed ? 0.35 : 0.9,
                Cursor = Cursors.Hand,
                ToolTip = row.Text
            };
            var zeit = row.TimeSeconds;
            tick.MouseLeftButtonDown += (_, _) => _seekToSeconds(zeit);
            Canvas.SetLeft(tick, x - 1);
            Canvas.SetTop(tick, 0);
            _canvas.Children.Add(tick);
            _marker.Add((row, tick));
        }
    }

    public void Reposition()
    {
        var dauer = _getDurationSeconds() ?? 0.0;
        var (offsetX, trackWidth) = _getBounds();
        foreach (var (row, element) in _marker)
        {
            if (SuggestionMarkerLayout.CalculateX(row.TimeSeconds, dauer, offsetX, trackWidth) is { } x)
                Canvas.SetLeft(element, x - 1);
        }
    }

    public void Clear()
    {
        foreach (var (_, element) in _marker)
            _canvas.Children.Remove(element);
        _marker.Clear();
    }
}
```

`PlayerWindow.xaml`: direkt nach dem `DamageMarkerCanvas`-Element (endet mit `ClipToBounds="False"/>`) einfuegen:

```xml
                    <!-- KI-Vorschläge (Bogen, Rohranfang, Rohrende): eigene Spur unter dem Regler -->
                    <Canvas x:Name="SuggestionMarkerCanvas"
                            IsHitTestVisible="True"
                            VerticalAlignment="Bottom"
                            Height="8"
                            Margin="0,0,0,-6"
                            ClipToBounds="False"/>
```

- [ ] **Step 4: Tests laufen lassen**

Run: `dotnet test tests/AuswertungPro.Next.UI.Tests -o .tmp/testout-vorschlaege/ui --filter "FullyQualifiedName~SuggestionMarker" -v q`
Expected: 3 Tests gruen.

- [ ] **Step 5: Commit**

```bash
git add src/AuswertungPro.Next.UI/Player/SuggestionMarkerLayout.cs src/AuswertungPro.Next.UI/Player/SuggestionMarkerController.cs src/AuswertungPro.Next.UI/Views/Windows/PlayerWindow.xaml tests/AuswertungPro.Next.UI.Tests/SuggestionMarkerLayoutTests.cs tests/AuswertungPro.Next.UI.Tests/SuggestionMarkerControllerTests.cs
git commit -m "Codiermodus-Vorschlaege: Marker auf der Zeitleiste nach Videozeit" -m "Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 10: Karte im Seitenpanel und Ereignisse

**Files:**
- Modify: `src/AuswertungPro.Next.UI/Views/Windows/PlayerCodingSidePanel.xaml` (rechte Spalte `Grid.Column="4"`, Zeilen 383-507)
- Modify: `src/AuswertungPro.Next.UI/Views/Windows/PlayerCodingSidePanel.xaml.cs`
- Modify: `src/AuswertungPro.Next.UI/Views/Windows/PlayerCodingSidePanelEventBinder.cs`
- Modify: `src/AuswertungPro.Next.UI/Views/Windows/PlayerWindow.CodingSidePanelAccessors.cs`
- Test: `tests/AuswertungPro.Next.UI.Tests/DesignAudit*` (bestehende Waechter) und Build

**Interfaces:**
- Produces (XAML-Namen): `LstSuggestions` (ListBox), `TxtSuggestionHeader` (TextBlock), `TxtSuggestionHint` (TextBlock); Ereignisse `SuggestionsDoubleClickRequested`, `SuggestionSeekRequested`, `SuggestionConfirmRequested`, `SuggestionRejectRequested`; Binder-Felder `SuggestionsDoubleClick`, `SuggestionSeek`, `SuggestionConfirm`, `SuggestionReject`.
- Consumes (Task 11): `PlayerWindow.SuggestionSeek_Click`, `SuggestionConfirm_Click`, `SuggestionReject_Click`.

- [ ] **Step 1: XAML der Karte**

Die rechte Spalte enthaelt heute genau ein `<Border Grid.Column="4" …>` (Import). Dieses Border in ein Grid mit zwei Zeilen packen: Zeile 0 `*` = bestehendes Import-Border (Attribut `Grid.Column="4"` dort entfernen, stattdessen `Grid.Row="0"`), Zeile 1 `Auto` = neue Karte. Neue Karte:

```xml
                            <Grid Grid.Column="4">
                                <Grid.RowDefinitions>
                                    <RowDefinition Height="*"/>
                                    <RowDefinition Height="Auto"/>
                                </Grid.RowDefinitions>

                                <!-- bestehendes Import-Border hier, mit Grid.Row="0" -->

                                <Border Grid.Row="1" Style="{DynamicResource PlayerCard}" Margin="0,4,0,0">
                                    <DockPanel>
                                        <TextBlock x:Name="TxtSuggestionHeader"
                                                   DockPanel.Dock="Top"
                                                   Style="{DynamicResource SectionLabel}"
                                                   Margin="0,0,0,4"
                                                   FontSize="{DynamicResource TextXS}"
                                                   Text="KI-VORSCHLÄGE"
                                                   ToolTip="Vorschläge der KI aus dem Vorabdurchlauf: Bogen, Rohranfang, Rohrende. Nichts wird ohne Bestätigen eingetragen."/>
                                        <TextBlock x:Name="TxtSuggestionHint"
                                                   DockPanel.Dock="Bottom"
                                                   FontSize="{DynamicResource TextXS}"
                                                   Foreground="{DynamicResource TextSecondaryBrush}"
                                                   TextWrapping="Wrap"
                                                   Visibility="Collapsed"/>
                                        <ListBox x:Name="LstSuggestions"
                                                 Background="Transparent"
                                                 BorderThickness="0"
                                                 FontSize="{DynamicResource TextXS}"
                                                 Foreground="{DynamicResource TextBrush}"
                                                 MaxHeight="180"
                                                 ScrollViewer.HorizontalScrollBarVisibility="Disabled"
                                                 MouseDoubleClick="Suggestions_DoubleClick">
                                            <ListBox.ContextMenu>
                                                <ContextMenu>
                                                    <MenuItem Header="Zum Zeitpunkt springen" Click="SuggestionSeek_Click">
                                                        <MenuItem.Icon><ui:FluentIcon Glyph="&#xE823;" Foreground="{DynamicResource MutedBrush}"/></MenuItem.Icon>
                                                    </MenuItem>
                                                    <MenuItem Header="Bestätigen" Click="SuggestionConfirm_Click">
                                                        <MenuItem.Icon><ui:FluentIcon Glyph="&#xE8FB;" Foreground="{DynamicResource SuccessBrush}"/></MenuItem.Icon>
                                                    </MenuItem>
                                                    <MenuItem Header="Ablehnen" Click="SuggestionReject_Click">
                                                        <MenuItem.Icon><ui:FluentIcon Glyph="&#xE711;" Foreground="{DynamicResource DangerBrush}"/></MenuItem.Icon>
                                                    </MenuItem>
                                                </ContextMenu>
                                            </ListBox.ContextMenu>
                                            <ListBox.ItemTemplate>
                                                <DataTemplate>
                                                    <StackPanel Orientation="Horizontal" ToolTip="{Binding Text}">
                                                        <StackPanel.Style>
                                                            <Style TargetType="StackPanel">
                                                                <Style.Triggers>
                                                                    <DataTrigger Binding="{Binding IsConfirmed}" Value="True">
                                                                        <Setter Property="Opacity" Value="0.45"/>
                                                                    </DataTrigger>
                                                                </Style.Triggers>
                                                            </Style>
                                                        </StackPanel.Style>
                                                        <ui:FluentIcon Glyph="{Binding Glyph}" FontSize="{DynamicResource TextXS}" Foreground="{DynamicResource SecondaryAccentBrush}" Margin="0,0,6,0" VerticalAlignment="Center"/>
                                                        <TextBlock Text="{Binding Text}" FontSize="{DynamicResource TextXS}" VerticalAlignment="Center"/>
                                                        <ui:FluentIcon Glyph="&#xE73E;" FontSize="{DynamicResource TextXS}" Foreground="{DynamicResource SuccessBrush}" Margin="6,0,0,0" VerticalAlignment="Center">
                                                            <ui:FluentIcon.Style>
                                                                <Style TargetType="ui:FluentIcon">
                                                                    <Setter Property="Visibility" Value="Collapsed"/>
                                                                    <Style.Triggers>
                                                                        <DataTrigger Binding="{Binding IsConfirmed}" Value="True">
                                                                            <Setter Property="Visibility" Value="Visible"/>
                                                                        </DataTrigger>
                                                                    </Style.Triggers>
                                                                </Style>
                                                            </ui:FluentIcon.Style>
                                                        </ui:FluentIcon>
                                                    </StackPanel>
                                                </DataTemplate>
                                            </ListBox.ItemTemplate>
                                        </ListBox>
                                    </DockPanel>
                                </Border>
                            </Grid>
```

Kein Bool-zu-Sichtbarkeit-Konverter noetig: Der Haken wird ueber den `DataTrigger` ein- und ausgeblendet. Es gibt in dieser Datei nur `PositiveIntToVisibility` (fuer Zahlen), der passt hier nicht.

- [ ] **Step 2: Code-behind und Binder**

`PlayerCodingSidePanel.xaml.cs`, bei den Ereignissen ergaenzen:

```csharp
    public event MouseButtonEventHandler? SuggestionsDoubleClickRequested;
    public event RoutedEventHandler? SuggestionSeekRequested;
    public event RoutedEventHandler? SuggestionConfirmRequested;
    public event RoutedEventHandler? SuggestionRejectRequested;

    private void Suggestions_DoubleClick(object sender, MouseButtonEventArgs e) => SuggestionsDoubleClickRequested?.Invoke(sender, e);
    private void SuggestionSeek_Click(object sender, RoutedEventArgs e) => SuggestionSeekRequested?.Invoke(sender, e);
    private void SuggestionConfirm_Click(object sender, RoutedEventArgs e) => SuggestionConfirmRequested?.Invoke(sender, e);
    private void SuggestionReject_Click(object sender, RoutedEventArgs e) => SuggestionRejectRequested?.Invoke(sender, e);
```

`PlayerCodingSidePanelEventBinder.cs`: an das Ende des Records `PlayerCodingSidePanelEventHandlers` vier Felder anhaengen und in `Bind` abonnieren:

```csharp
    RoutedEventHandler ImportConfirmToBrain,
    MouseButtonEventHandler SuggestionsDoubleClick,
    RoutedEventHandler SuggestionSeek,
    RoutedEventHandler SuggestionConfirm,
    RoutedEventHandler SuggestionReject);
```

```csharp
        sidePanel.SuggestionsDoubleClickRequested += handlers.SuggestionsDoubleClick;
        sidePanel.SuggestionSeekRequested += handlers.SuggestionSeek;
        sidePanel.SuggestionConfirmRequested += handlers.SuggestionConfirm;
        sidePanel.SuggestionRejectRequested += handlers.SuggestionReject;
```

`PlayerWindow.CodingSidePanelAccessors.cs`: im `new PlayerCodingSidePanelEventHandlers(...)` nach `ImportConfirmToBrain: ImportConfirmToBrain_Click` anhaengen:

```csharp
                SuggestionsDoubleClick: (_, _) => SuggestionSeek_Click(this, new RoutedEventArgs()),
                SuggestionSeek: SuggestionSeek_Click,
                SuggestionConfirm: SuggestionConfirm_Click,
                SuggestionReject: SuggestionReject_Click));
```

Diese drei `PlayerWindow`-Methoden entstehen in Task 11. Damit dieser Task baut, sie vorerst leer in `PlayerWindow.Coding.Suggestions.cs` anlegen (Task 11 fuellt sie):

```csharp
using System.Windows;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    private void SuggestionSeek_Click(object sender, RoutedEventArgs e) { }
    private void SuggestionConfirm_Click(object sender, RoutedEventArgs e) { }
    private void SuggestionReject_Click(object sender, RoutedEventArgs e) { }
}
```

`tests/AuswertungPro.Next.UI.Tests/PlayerCodingSidePanelEventBinderTests.cs` konstruiert `PlayerCodingSidePanelEventHandlers` positional: dort die vier neuen Argumente `SuggestionsDoubleClick: (_, _) => { }, SuggestionSeek: (_, _) => { }, SuggestionConfirm: (_, _) => { }, SuggestionReject: (_, _) => { }` anhaengen. Prueft der Test die Zahl der abonnierten Ereignisse, die Erwartung um vier erhoehen.

- [ ] **Step 3: Build und Waechter**

Run: `dotnet build src/AuswertungPro.Next.UI/AuswertungPro.Next.UI.csproj -o .tmp/testout-vorschlaege/ui -v q` und `dotnet test tests/AuswertungPro.Next.UI.Tests -o .tmp/testout-vorschlaege/ui --filter "FullyQualifiedName~DesignAudit|FullyQualifiedName~UiAiFreeze|FullyQualifiedName~PlayerCodingSidePanel" -v q`
Expected: 0 Fehler, Waechter gruen (Umlaute in `Header`/`Text`/`ToolTip`, Menue-Icons vorhanden, FontSize nur ueber Tokens).

- [ ] **Step 4: Commit**

```bash
git add src/AuswertungPro.Next.UI/Views/Windows/PlayerCodingSidePanel.xaml src/AuswertungPro.Next.UI/Views/Windows/PlayerCodingSidePanel.xaml.cs src/AuswertungPro.Next.UI/Views/Windows/PlayerCodingSidePanelEventBinder.cs src/AuswertungPro.Next.UI/Views/Windows/PlayerWindow.CodingSidePanelAccessors.cs src/AuswertungPro.Next.UI/Views/Windows/PlayerWindow.Coding.Suggestions.cs
git commit -m "Codiermodus: Karte KI-Vorschlaege im Seitenpanel mit Springen, Bestaetigen, Ablehnen" -m "Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 11: Der Player verbindet alles

**Files:**
- Modify: `src/AuswertungPro.Next.UI/Views/Windows/PlayerWindow.Coding.Suggestions.cs` (aus Task 10, jetzt vollstaendig)
- Modify: `src/AuswertungPro.Next.UI/Views/Windows/PlayerWindow.Coding.Lifecycle.Ui.cs` (leere `StartSuggestionScan` aus Task 7 entfernen)
- Modify: `src/AuswertungPro.Next.UI/Views/Windows/PlayerWindow.Wiring.cs` (`WireWindowSurfaceEvents`)
- Modify: `src/AuswertungPro.Next.UI/Views/Windows/PlayerWindowCodingModeExitControllerFactory.cs` (`PlayerWindowCodingModeExitActions`, `CreateTeardownActions`)
- Modify: `src/AuswertungPro.Next.UI/Views/Windows/PlayerWindow.xaml.cs` (Zeile ~477, `new PlayerWindowCodingModeExitActions(...)`)

**Interfaces:**
- Consumes: `ICodingSuggestionScanService` ueber `_protocolContext.LegacyServiceProvider!.CodingSuggestionScan`; `AppSettings.CodingSuggestionsEnabled` ueber `_protocolContext.Settings`; `_codingSessionHost.VideoPath`, `_codingSessionHost.HaltungName`; `_codingSessionRuntimeOwner.Service` (`ICodingSessionService`, `Events`, `AddEvent`); `_protocolContext.HaltungRecord` (`GetFieldValue`, `SetFieldValue`); `_protocolContext.Dialogs.Confirm`; `_playerTimelineHost.SeekMilliseconds`, `DurationSeconds`; `CodingSelectedCodeEventWorkflow.Create`; `CodingCodeExplorerServiceCreationWorkflow.Create(CreateVsaCodeExplorerViewModel, _protocolContext.CodeUsage, _protocolContext.LegacyServiceProvider)` + `TryEdit`; `CodingExplorerEntryFactory.CreateSeed`; `CodingManualEventAppender.Apply(ProtocolEntry, OverlayGeometry?, ICodingSessionService)`; `CodingEventCreationPostWorkflow.Apply(created, _codingSidePanelControllers.EventCreationPostActions, new CodingEventCreationPostOptions(SelectCreatedEvent, ClearSelectedCode))`; `CodingCaptureSnapshot`; `CodingSidePanelControl.LstSuggestions/TxtSuggestionHeader/TxtSuggestionHint`; `SuggestionMarkerCanvas`; `PlayerSliderTrackBounds.Resolve(PositionSlider, SuggestionMarkerCanvas)`; `_codingPipelineHealthController.InitializeAsync()`; `SafeFireAndForget`.
- Produces: `StartSuggestionScan()`, `CancelSuggestionScan()`, die drei Klick-Handler; `PlayerWindowCodingModeExitActions.CancelSuggestionScan`.

- [ ] **Step 1: Teildatei schreiben**

`PlayerWindow.Coding.Suggestions.cs` vollstaendig ersetzen:

```csharp
using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Data;
using AuswertungPro.Next.Application.UseCases.CodingSuggestions;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Ai.Coding;
using AuswertungPro.Next.UI.Helpers;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Views.Windows;

/// <summary>
/// KI-Vorschlaege im Codiermodus: Vorabdurchlauf im Hintergrund, Karte im
/// Seitenpanel, Marker auf der Zeitleiste. Entscheidungen (was Bestaetigen tut)
/// liegen in CodingSuggestionConfirmPolicy; hier wird nur verdrahtet.
/// </summary>
public partial class PlayerWindow
{
    private readonly CodingSuggestionsOwner _codingSuggestions = new();
    private SuggestionMarkerController? _suggestionMarkers;
    private CancellationTokenSource? _suggestionScanCts;

    private SuggestionMarkerController SuggestionMarkers
        => _suggestionMarkers ??= new SuggestionMarkerController(
            SuggestionMarkerCanvas,
            () => PlayerSliderTrackBounds.Resolve(PositionSlider, SuggestionMarkerCanvas),
            () => _playerTimelineHost.DurationSeconds,
            SeekToSuggestionSeconds);

    /// <summary>Vierter Hintergrundschritt beim Eintritt in den Codiermodus.</summary>
    private void StartSuggestionScan()
    {
        CancelSuggestionScan();
        BindSuggestionCard();

        var settings = _protocolContext.Settings;
        var provider = _protocolContext.LegacyServiceProvider;
        var videoPath = _codingSessionHost.VideoPath;
        var haltung = _codingSessionHost.HaltungName ?? _protocolContext.HaltungId ?? string.Empty;

        if (provider is null || string.IsNullOrWhiteSpace(videoPath))
        {
            _codingSuggestions.Fail("Kein Video oder keine Dienste im Codiermodus.");
            return;
        }

        var cts = new CancellationTokenSource();
        _suggestionScanCts = cts;
        _codingSuggestions.BeginScan();
        RunSuggestionScanAsync(provider.CodingSuggestionScan,
            new CodingSuggestionScanRequest(videoPath, haltung, settings?.CodingSuggestionsEnabled ?? true),
            cts).SafeFireAndForget("CodingSuggestionScan");
    }

    private async System.Threading.Tasks.Task RunSuggestionScanAsync(
        ICodingSuggestionScanService service,
        CodingSuggestionScanRequest request,
        CancellationTokenSource cts)
    {
        try
        {
            // Erst die KI-Bereitschaft (startet bei Bedarf den Sidecar), dann der Durchlauf.
            await _codingPipelineHealthController.InitializeAsync();
            cts.Token.ThrowIfCancellationRequested();

            var fortschritt = new Progress<int>(p =>
            {
                if (ReferenceEquals(_suggestionScanCts, cts))
                    _codingSuggestions.SetPercent(p);
            });
            var set = await service.ScanAsync(request, cts.Token, fortschritt);

            if (!ReferenceEquals(_suggestionScanCts, cts))
                return; // ein spaeterer Codiermodus hat uebernommen
            _codingSuggestions.Apply(set);
            SuggestionMarkers.Build(_codingSuggestions.Rows);
        }
        catch (OperationCanceledException)
        {
            // Codiermodus verlassen oder Fenster geschlossen — kein Hinweis.
        }
        catch (Exception ex)
        {
            if (ReferenceEquals(_suggestionScanCts, cts))
                _codingSuggestions.Fail(ex.Message);
        }
        finally
        {
            if (ReferenceEquals(_suggestionScanCts, cts))
                _suggestionScanCts = null;
            cts.Dispose();
        }
    }

    /// <summary>Beim Verlassen des Codiermodus und beim Schliessen: Durchlauf stoppen, Karte leeren.</summary>
    private void CancelSuggestionScan()
    {
        var cts = _suggestionScanCts;
        _suggestionScanCts = null;
        try { cts?.Cancel(); } catch (ObjectDisposedException) { }
        _codingSuggestions.Clear();
        _suggestionMarkers?.Clear();
    }

    private void BindSuggestionCard()
    {
        var panel = CodingSidePanelControl;
        panel.LstSuggestions.ItemsSource = _codingSuggestions.Rows;
        panel.TxtSuggestionHeader.SetBinding(System.Windows.Controls.TextBlock.TextProperty,
            new Binding(nameof(CodingSuggestionsOwner.HeaderText)) { Source = _codingSuggestions });
        panel.TxtSuggestionHint.SetBinding(System.Windows.Controls.TextBlock.TextProperty,
            new Binding(nameof(CodingSuggestionsOwner.HintText)) { Source = _codingSuggestions });
        panel.TxtSuggestionHint.SetBinding(VisibilityProperty,
            new Binding(nameof(CodingSuggestionsOwner.HintText))
            {
                Source = _codingSuggestions,
                Converter = new StringToVisibilityConverter()
            });
    }

    private CodingSuggestionRow? SelectedSuggestionRow
        => CodingSidePanelControl.LstSuggestions.SelectedItem as CodingSuggestionRow;

    private void SeekToSuggestionSeconds(double seconds)
    {
        _playerPlaybackControlHost.SetPause();
        _playerTimelineHost.SeekMilliseconds((long)Math.Round(seconds * 1000.0));
        _codingNavigationPendingState.Set(true);
    }

    private void SuggestionSeek_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedSuggestionRow is { } row)
            SeekToSuggestionSeconds(row.TimeSeconds);
    }

    private void SuggestionReject_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedSuggestionRow is not { } row) return;
        _codingSuggestions.Reject(row);
        SuggestionMarkers.Build(_codingSuggestions.Rows);
    }

    private void SuggestionConfirm_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedSuggestionRow is not { IsConfirmed: false } row) return;
        var session = _codingSessionRuntimeOwner.Service;
        var record = _protocolContext.HaltungRecord;
        if (session is null || record is null) return;

        SeekToSuggestionSeconds(row.TimeSeconds);
        var videoTime = TimeSpan.FromSeconds(row.TimeSeconds);
        var aktiveCodes = session.Events
            .Where(ev => !ev.Entry.IsDeleted)
            .Select(ev => ev.Entry.Code ?? string.Empty)
            .ToList();
        var laengeVorhanden = !string.IsNullOrWhiteSpace(record.GetFieldValue(FieldKeys.HoldingLengthMeters));

        var plan = CodingSuggestionConfirmPolicy.Plan(row.Suggestion, _codingSuggestions.MeterTrack, aktiveCodes, laengeVorhanden);

        switch (plan.Action)
        {
            case CodingSuggestionConfirmAction.AlreadyPresent:
                _codingSuggestions.Confirm(row);
                _protocolContext.Dialogs.Info(plan.Hinweis, "KI-Vorschlag");
                break;

            case CodingSuggestionConfirmAction.OpenCodeWindow:
                if (ConfirmBendSuggestion(plan, videoTime, session))
                    _codingSuggestions.Confirm(row);
                break;

            case CodingSuggestionConfirmAction.CreateBoundaryEvent:
                if (plan.ProposeLength && plan.Meter is { } laenge)
                {
                    var text = laenge.ToString("0.00", CultureInfo.GetCultureInfo("de-CH"));
                    if (_protocolContext.Dialogs.Confirm($"Länge {text} m aus dem Video als Haltungslänge übernehmen?", "Haltungslänge"))
                        record.SetFieldValue(FieldKeys.HoldingLengthMeters, laenge.ToString("F2", CultureInfo.InvariantCulture), FieldSource.Protocol, userEdited: false);
                }

                var meter = plan.Meter ?? _codingOsdMeterController.LastMeter ?? _codingSessionHost.CurrentMeter;
                var beschreibung = plan.Code == "BCD" ? "Rohranfang" : "Rohrende";
                var created = CodingSelectedCodeEventWorkflow.Create(
                    plan.Code, beschreibung, meter, videoTime, null, session, CodingCaptureSnapshot);
                CodingEventCreationPostWorkflow.Apply(
                    created,
                    _codingSidePanelControllers.EventCreationPostActions,
                    new CodingEventCreationPostOptions(SelectCreatedEvent: true, ClearSelectedCode: false));
                _codingSuggestions.Confirm(row);
                break;
        }

        SuggestionMarkers.Build(_codingSuggestions.Rows);
    }

    /// <summary>Bogen: Codierfenster mit BCC vorgewaehlt; der Mensch waehlt die Richtung.</summary>
    private bool ConfirmBendSuggestion(CodingSuggestionConfirmPlan plan, TimeSpan videoTime, AuswertungPro.Next.Application.Ai.ICodingSessionService session)
    {
        var entry = CodingExplorerEntryFactory.CreateSeed(null, videoTime, suggestedCode: plan.Code);
        entry.MeterStart = plan.Meter;
        entry.MeterEnd = plan.Meter;

        var service = CodingCodeExplorerServiceCreationWorkflow.Create(
            CreateVsaCodeExplorerViewModel,
            _protocolContext.CodeUsage,
            _protocolContext.LegacyServiceProvider);
        var angenommen = _codingOverlayInputVisibilityController.Run(() =>
            service.TryEdit(entry, plan.Meter, videoTime, _codingSessionHost.VideoPath, videoTime, this, CreateVsaCodeExplorerLiveSnapshotProvider()));
        if (!angenommen)
            return false;

        var photoPath = CodingCaptureSnapshot(entry);
        CodingProtocolEntryPhotoPathAppender.AddIfPresent(entry, photoPath);
        var created = CodingManualEventAppender.Apply(entry, null, session);
        CodingEventCreationPostWorkflow.Apply(
            created,
            _codingSidePanelControllers.EventCreationPostActions,
            new CodingEventCreationPostOptions(SelectCreatedEvent: true, ClearSelectedCode: false));
        return true;
    }

    /// <summary>Leerer Text = Hinweiszeile ausblenden.</summary>
    private sealed class StringToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => string.IsNullOrWhiteSpace(value as string) ? Visibility.Collapsed : Visibility.Visible;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
```

Namen pruefen, bevor gebaut wird (jeweils mit `grep -rn "<Name>" src/AuswertungPro.Next.UI/Views/Windows/PlayerWindow*.cs`):
- `_playerPlaybackControlHost.SetPause` (wird in `PrepareCodingModePlayback` verwendet), `_playerTimelineHost`, `_codingNavigationPendingState.Set`, `_codingOsdMeterController.LastMeter`, `_codingSessionHost.CurrentMeter`, `_codingOverlayInputVisibilityController.Run` (`Func<bool>` → `bool`, siehe `CreateCodingCodeExplorerEditActions`), `_protocolContext.HaltungId`, `_protocolContext.CodeUsage`, `CodingProtocolEntryPhotoPathAppender.AddIfPresent`.
- Existiert `_codingNavigationPendingState.Set` mit `bool`? Sonst den Aufruf so uebernehmen, wie ihn `EnterCodingMode` (`SetCodingNavigationPending`) verwendet.
- `IDialogService.Info(string, string)` und `Confirm(string, string)` existieren (`Services/IDialogService.cs`).

- [ ] **Step 2: Leeren Platzhalter aus Task 7 entfernen**

In `PlayerWindow.Coding.Lifecycle.Ui.cs` die Zeile `private void StartSuggestionScan() { }` samt Kommentar loeschen.

- [ ] **Step 3: Abbruch beim Austritt**

`PlayerWindowCodingModeExitControllerFactory.cs`: Record erweitern:

```csharp
internal sealed record PlayerWindowCodingModeExitActions(
    Func<double, bool> CloseOpenStreckenschaeden,
    Action HideInlineDefectDetail,
    Action ResetFrameReadiness,
    Action CancelSuggestionScan);
```

In `CreateTeardownActions` den Eintrag `ClearImportReferenceEvents` so ersetzen:

```csharp
            ClearImportReferenceEvents: () =>
            {
                // KI-Vorschlaege gehoeren wie die Import-Referenz zur Sitzung: beim Verlassen weg.
                dependencies.Actions.CancelSuggestionScan();
                CodingImportReferenceStateResetter.ClearEvents(
                    dependencies.ProtocolStates.ImportReferenceEvents.Events);
            },
```

`PlayerWindow.xaml.cs` (Zeile ~477): `CancelSuggestionScan: CancelSuggestionScan` als viertes Argument. `tests/AuswertungPro.Next.UI.Tests/PlayerWindowCodingModeExitControllerFactoryTests.cs` konstruiert `PlayerWindowCodingModeExitActions`: dort `CancelSuggestionScan: () => { }` ergaenzen. Prueft dieser Test die Reihenfolge oder Zahl der Teardown-Aktionen, bleibt sie gleich — der Abbruch ist in `ClearImportReferenceEvents` eingebettet.

- [ ] **Step 4: Marker bei Groessenaenderung nachfuehren**

`PlayerWindow.Wiring.cs`, in `WireWindowSurfaceEvents` nach dem `PlayerSurfaceEventBinder.Bind(...)`-Aufruf:

```csharp
        // Die Vorschlagsmarker haengen an derselben Spur wie die Befundmarker.
        PositionSlider.SizeChanged += (_, _) => _suggestionMarkers?.Reposition();
        SuggestionMarkerCanvas.SizeChanged += (_, _) => _suggestionMarkers?.Reposition();
```

- [ ] **Step 5: Build und Tests**

Run: `dotnet build AuswertungPro.sln -o .tmp/testout-vorschlaege -v q 2>&1 | grep -E "error|Fehler" | grep -v NETSDK1194`
Expected: keine Zeile (0 Fehler).

Run: `dotnet test tests/AuswertungPro.Next.UI.Tests -o .tmp/testout-vorschlaege/ui -v q` und `dotnet test tests/AuswertungPro.Next.Pipeline.Tests -o .tmp/testout-vorschlaege/pipe -v q`
Expected: alle gruen (bekannte Ausnahme: der isolierte Nachschlag-WPF-Test kann unter Last rot sein; einzeln gruen).

- [ ] **Step 6: Sichtprobe im Programm**

SewerStudio aus `.tmp/testout-vorschlaege/SewerStudio.exe` starten, ein Video mit Haltung oeffnen, Codiermodus betreten:
- Karte "KI-VORSCHLÄGE" zeigt "KI prüft Video … n %", danach Zeilen; Marker unter dem Regler.
- Doppelklick springt; Rechtsklick → Bestaetigen bei einem Bogen oeffnet das Codierfenster mit BCC; Rohranfang legt BCD an; Rohrende fragt bei leerer Laenge.
- Codiermodus verlassen: Karte leer, Marker weg; erneut betreten: neuer Durchlauf.
- Einstellungen → Schalter aus → Karte sagt "In den Einstellungen ausgeschaltet."

- [ ] **Step 7: Commit**

```bash
git add src/AuswertungPro.Next.UI/Views/Windows/PlayerWindow.Coding.Suggestions.cs src/AuswertungPro.Next.UI/Views/Windows/PlayerWindow.Coding.Lifecycle.Ui.cs src/AuswertungPro.Next.UI/Views/Windows/PlayerWindow.Wiring.cs src/AuswertungPro.Next.UI/Views/Windows/PlayerWindowCodingModeExitControllerFactory.cs src/AuswertungPro.Next.UI/Views/Windows/PlayerWindow.xaml.cs
git commit -m "Codiermodus: KI-Vorschlaege laufen beim Eintritt im Hintergrund und lassen sich bestaetigen" -m "Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 12: Regeln festhalten

**Files:**
- Modify: `CLAUDE.md` (nach dem Abschnitt "Rohranfang und Rohrende im Vorabdurchlauf (seit 2026-09-04)")

- [ ] **Step 1: Abschnitt ergaenzen**

```markdown
### KI-Vorschlaege im Codiermodus (seit 2026-09-05)

Beim Eintritt in den Codiermodus laeuft `CodingSuggestionScanUseCase`
(`Application/UseCases/CodingSuggestions`) im Hintergrund: zuerst der Bogen-Durchlauf
mit dem festen Pin `CodingBendCandidatePin` (`bcc_nc15_seed46_20260808`), dann
Rohranfang/Rohrende ueber die gepinnten Lernstufen. Ergebnis ist ein
`CodingSuggestionSet` mit Meterspur (`BendSuggestionScanResult.MeterTrack`). Die Karte
"KI-Vorschlaege" im Seitenpanel und die Marker unter dem Regler zeigen es; Bestaetigen
folgt `CodingSuggestionConfirmPolicy`: Bogen oeffnet das Codierfenster mit `BCC`,
Rohranfang legt BCD bei 0 m an, Rohrende legt BCE mit dem Spurmeter an und schlaegt bei
leerer `Haltungslaenge_m` diesen Wert vor (`FieldSource.Protocol`). Schalter:
`AppSettings.CodingSuggestionsEnabled` (Standard ein).

Vier Regeln nie zurueckdrehen:

- **Bogen vor Anfang/Ende, nie parallel** — alle drei Gewichte teilen `YOLO_TEST`.
- **Jeder Teil faellt fuer sich aus**; ein technischer Fehler ist `Fehler` mit Text,
  nie eine leere Liste. `OperationCanceledException` geht immer durch.
- **Ein geschaetzter Meter wird nie Vorgabe oder Laenge**; ein fehlender Meter wird nie
  als `0,0` gezeigt.
- **Mindestens ein gezeigter Vorschlag markiert die Haltung im Sitzungsgedaechtnis**
  (`ICodingSuggestionExposure`), damit Goldsamples dieser Haltung `SuggestionShown`
  tragen und den unbeeinflussten Messbestand nicht verfaelschen.
```

- [ ] **Step 2: Commit**

```bash
git add CLAUDE.md
git commit -m "Doku: KI-Vorschlaege im Codiermodus als Regeln festgehalten" -m "Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

## Selbstpruefung gegen die Spezifikation

| Spec-Abschnitt | Task |
|---|---|
| Ablauf 1-6 (Start, Schalter, Reihenfolge, Set, Gedaechtnis, Abbruch) | 4, 6, 7, 11 |
| Teilausfall je Teil, Fehler nie "kein Vorschlag" | 4 |
| Meterspur + Nachschlag 1,5 s | 1, 2 |
| Anzeige: Karte, Kopfzeile mit Prozent, Zeilentexte, Marker, Kontextmenue | 8, 9, 10 |
| Bestaetigen: Bogen/BCC, Rohranfang/BCD, Rohrende/BCE + Laengenvorschlag, Dedup | 3, 11 |
| Registrierung 158, Schalter in Einstellungen | 5, 6 |
| Tests laut Spec | 1-9 |
| Waechter (UiAiFreeze, DesignAudit) | 10, 11 |
| CLAUDE.md | 12 |
