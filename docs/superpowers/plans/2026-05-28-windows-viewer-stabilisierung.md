# Windows Viewer Stabilisierung Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Den Windows-Viewer (`PlayerWindow`) so stabilisieren, dass Video, Codierung, Overlays und KI-Auswertung testbar bleiben, ohne dass eine einzelne 7000-Zeilen-Datei weitere Fachlogik versteckt.

**Architecture:** Der erste Umbau ist ein Sicherheitsumbau, kein UI-Redesign. Pure Fachlogik wird aus `PlayerWindow.xaml.cs` in kleine Klassen mit Unit-Tests verschoben; WPF/VLC-Code bleibt zunaechst im Window und wird erst danach ueber Adapter getrennt. Jede Aufgabe muss einen gruenen Zwischenstand erzeugen.

**Tech Stack:** .NET 10, WPF, LibVLCSharp.WPF, xUnit, CommunityToolkit.Mvvm, bestehende AuswertungPro-Schichten.

---

## Scope

Dieser Plan betrifft nur den Windows-Viewer:

- `src/AuswertungPro.Next.UI/Views/Windows/PlayerWindow.xaml.cs`
- `src/AuswertungPro.Next.UI/Views/Windows/PlayerWindow.xaml`
- neue kleine Klassen unter `src/AuswertungPro.Next.UI/Player/`
- Tests unter `tests/AuswertungPro.Next.UI.Tests/`

Nicht in diesem Paket:

- keine neue KI-Funktion
- kein neues VSA-Regelwerk
- keine Aenderung am Sidecar
- keine neue Startanimation
- kein grosser MVVM-Neubau in einem Schritt

## Zielbild Paket 1

Nach Paket 1 soll `PlayerWindow.xaml.cs` mindestens diese Logik nicht mehr selbst besitzen:

- Eingabemarker-Stichwort -> VSA-Code
- erlaubte Import-Fallback-Codes
- Import-Kontext-Code-Verfeinerung
- Timeline-/Marker-Positionsrechnung
- einfache Playback-Zustandsrechnung ohne WPF

Das Window bleibt Host fuer VLC, Popups und Canvas-Rendering. Diese Begrenzung ist Absicht, damit der Umbau nicht wieder eine Grossbaustelle wird.

---

### Task 1: Baseline sichern

**Files:**
- Read: `src/AuswertungPro.Next.UI/Views/Windows/PlayerWindow.xaml.cs`
- Read: `src/AuswertungPro.Next.UI/Views/Windows/PlayerWindow.xaml`
- Test: `tests/AuswertungPro.Next.UI.Tests/AuswertungPro.Next.UI.Tests.csproj`

- [ ] **Step 1: Git-Status pruefen**

Run:

```powershell
git status --short --branch
```

Expected:

```text
## master
```

Untracked Tool-Ordner duerfen sichtbar sein. Es duerfen keine getrackten Aenderungen vorhanden sein.

- [ ] **Step 2: UI-Testbaseline laufen lassen**

Run:

```powershell
dotnet test tests/AuswertungPro.Next.UI.Tests/AuswertungPro.Next.UI.Tests.csproj --nologo
```

Expected:

```text
Bestanden!
```

- [ ] **Step 3: UI-Buildbaseline laufen lassen**

Run:

```powershell
dotnet build src/AuswertungPro.Next.UI/AuswertungPro.Next.UI.csproj --nologo -v minimal -p:UseAppHost=false
```

Expected:

```text
Der Buildvorgang wurde erfolgreich ausgefuehrt.
```

- [ ] **Step 4: Commit ist nicht noetig**

Kein Commit, weil nur gelesen und getestet wurde.

---

### Task 2: Eingabemarker-Codeauflösung aus PlayerWindow herausziehen

**Files:**
- Create: `src/AuswertungPro.Next.UI/Player/PlayerVsaCodeHintResolver.cs`
- Modify: `src/AuswertungPro.Next.UI/Views/Windows/PlayerWindow.xaml.cs`
- Modify: `tests/AuswertungPro.Next.UI.Tests/PlayerWindowVsaMappingTests.cs`

- [ ] **Step 1: Test zuerst auf direkte Resolver-Klasse umstellen**

Modify `tests/AuswertungPro.Next.UI.Tests/PlayerWindowVsaMappingTests.cs` so that the first test no longer uses reflection for `ResolveEingabemarkerCodeHint`.

Use this test body:

```csharp
[Theory]
[InlineData("VERFORMUNG", "BAA")]
[InlineData("OBERFLAECHENSCHADEN", "BAF")]
[InlineData("VERSATZ", "BAJ")]
[InlineData("VERSCHIEBUNG", "BAJ")]
[InlineData("WURZELN", "BBA")]
[InlineData("BEWUCHS", "BBA")]
[InlineData("INKRUSTATION", "BBB")]
public void Eingabemarker_keyword_mapping_matches_vsa_kek_manifest(string keyword, string expectedCode)
{
    var code = PlayerVsaCodeHintResolver.ResolveKeyword(keyword);

    Assert.Equal(expectedCode, code);
}
```

Add:

```csharp
using AuswertungPro.Next.UI.Player;
```

- [ ] **Step 2: Test rot sehen**

Run:

```powershell
dotnet test tests/AuswertungPro.Next.UI.Tests/AuswertungPro.Next.UI.Tests.csproj --nologo --filter "FullyQualifiedName~PlayerWindowVsaMappingTests"
```

Expected:

```text
CS0103 oder CS0246 fuer PlayerVsaCodeHintResolver
```

- [ ] **Step 3: Resolver erstellen**

Create `src/AuswertungPro.Next.UI/Player/PlayerVsaCodeHintResolver.cs`:

```csharp
namespace AuswertungPro.Next.UI.Player;

public static class PlayerVsaCodeHintResolver
{
    public static string? ResolveKeyword(string? keyword)
    {
        if (string.IsNullOrWhiteSpace(keyword))
        {
            return null;
        }

        var normalized = keyword.Trim().ToUpperInvariant();

        if (normalized.Contains("VERFORM", StringComparison.Ordinal) ||
            normalized.Contains("DEFORMATION", StringComparison.Ordinal))
        {
            return "BAA";
        }

        if (normalized.Contains("RISS", StringComparison.Ordinal))
        {
            return "BAB";
        }

        if (normalized.Contains("BRUCH", StringComparison.Ordinal) ||
            normalized.Contains("EINSTURZ", StringComparison.Ordinal))
        {
            return "BAC";
        }

        if (normalized.Contains("OBERFL", StringComparison.Ordinal) ||
            normalized.Contains("KORROSION", StringComparison.Ordinal) ||
            normalized.Contains("EROSION", StringComparison.Ordinal))
        {
            return "BAF";
        }

        if (normalized.Contains("VERSATZ", StringComparison.Ordinal) ||
            normalized.Contains("VERSCHIEB", StringComparison.Ordinal) ||
            normalized.Contains("ROHRVERBIND", StringComparison.Ordinal))
        {
            return "BAJ";
        }

        if (normalized.Contains("WURZEL", StringComparison.Ordinal) ||
            normalized.Contains("BEWUCHS", StringComparison.Ordinal))
        {
            return "BBA";
        }

        if (normalized.Contains("INKRUST", StringComparison.Ordinal) ||
            normalized.Contains("ANHAFT", StringComparison.Ordinal) ||
            normalized.Contains("KALK", StringComparison.Ordinal))
        {
            return "BBB";
        }

        if (normalized.Contains("ABLAGER", StringComparison.Ordinal) ||
            normalized.Contains("SEDIMENT", StringComparison.Ordinal))
        {
            return "BBC";
        }

        return null;
    }
}
```

- [ ] **Step 4: PlayerWindow delegieren lassen**

In `PlayerWindow.xaml.cs`, replace the body of the private static method `ResolveEingabemarkerCodeHint` with:

```csharp
private static string? ResolveEingabemarkerCodeHint(string? keyword)
    => Player.PlayerVsaCodeHintResolver.ResolveKeyword(keyword);
```

- [ ] **Step 5: Tests gruen**

Run:

```powershell
dotnet test tests/AuswertungPro.Next.UI.Tests/AuswertungPro.Next.UI.Tests.csproj --nologo --filter "FullyQualifiedName~PlayerWindowVsaMappingTests"
```

Expected:

```text
Bestanden!
```

- [ ] **Step 6: Commit**

```powershell
git add src/AuswertungPro.Next.UI/Player/PlayerVsaCodeHintResolver.cs src/AuswertungPro.Next.UI/Views/Windows/PlayerWindow.xaml.cs tests/AuswertungPro.Next.UI.Tests/PlayerWindowVsaMappingTests.cs
git commit -m "refactor(viewer): extract VSA code hint resolver"
```

---

### Task 3: Import-Fallback-Codeprüfung herausziehen

**Files:**
- Create: `src/AuswertungPro.Next.UI/Player/PlayerImportFallbackCodePolicy.cs`
- Modify: `src/AuswertungPro.Next.UI/Views/Windows/PlayerWindow.xaml.cs`
- Modify: `tests/AuswertungPro.Next.UI.Tests/PlayerWindowVsaMappingTests.cs`

- [ ] **Step 1: Tests auf direkte Policy erweitern**

Append this test to `PlayerWindowVsaMappingTests.cs`:

```csharp
[Theory]
[InlineData("BAAA")]
[InlineData("BAFAA")]
[InlineData("BAJB")]
[InlineData("BBAA")]
[InlineData("BBBA")]
[InlineData("BBCA")]
public void Import_fallback_policy_allows_assessable_vsa_damage_codes(string code)
{
    Assert.True(PlayerImportFallbackCodePolicy.IsAllowed(code));
}
```

Append this test:

```csharp
[Theory]
[InlineData("")]
[InlineData("   ")]
[InlineData("BDA")]
[InlineData("BDB")]
[InlineData("BCD")]
[InlineData("BCE")]
[InlineData("BCC")]
public void Import_fallback_policy_rejects_empty_or_observation_codes(string code)
{
    Assert.False(PlayerImportFallbackCodePolicy.IsAllowed(code));
}
```

- [ ] **Step 2: Test rot sehen**

Run:

```powershell
dotnet test tests/AuswertungPro.Next.UI.Tests/AuswertungPro.Next.UI.Tests.csproj --nologo --filter "FullyQualifiedName~PlayerWindowVsaMappingTests"
```

Expected:

```text
CS0103 oder CS0246 fuer PlayerImportFallbackCodePolicy
```

- [ ] **Step 3: Policy erstellen**

Create `src/AuswertungPro.Next.UI/Player/PlayerImportFallbackCodePolicy.cs`:

```csharp
namespace AuswertungPro.Next.UI.Player;

public static class PlayerImportFallbackCodePolicy
{
    private static readonly string[] AllowedPrefixes =
    [
        "BAA", "BAB", "BAC", "BAD", "BAE", "BAF", "BAG", "BAH", "BAI", "BAJ",
        "BAK", "BAL", "BAM", "BAN", "BAO", "BAP",
        "BBA", "BBB", "BBC", "BBD", "BBE", "BBF", "BBG", "BBH",
        "BDD", "BDE"
    ];

    private static readonly string[] ObservationPrefixes =
    [
        "BCA", "BCB", "BCC", "BCD", "BCE",
        "BDA", "BDB", "BDC", "BDG"
    ];

    public static bool IsAllowed(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return false;
        }

        var normalized = code.Trim().ToUpperInvariant();

        if (ObservationPrefixes.Any(prefix => normalized.StartsWith(prefix, StringComparison.Ordinal)))
        {
            return false;
        }

        return AllowedPrefixes.Any(prefix => normalized.StartsWith(prefix, StringComparison.Ordinal));
    }
}
```

- [ ] **Step 4: PlayerWindow delegieren lassen**

In `PlayerWindow.xaml.cs`, replace the body of `IsAllowedImportFallbackCode` with:

```csharp
private static bool IsAllowedImportFallbackCode(string code)
    => Player.PlayerImportFallbackCodePolicy.IsAllowed(code);
```

- [ ] **Step 5: Tests gruen**

Run:

```powershell
dotnet test tests/AuswertungPro.Next.UI.Tests/AuswertungPro.Next.UI.Tests.csproj --nologo --filter "FullyQualifiedName~PlayerWindowVsaMappingTests"
```

Expected:

```text
Bestanden!
```

- [ ] **Step 6: Commit**

```powershell
git add src/AuswertungPro.Next.UI/Player/PlayerImportFallbackCodePolicy.cs src/AuswertungPro.Next.UI/Views/Windows/PlayerWindow.xaml.cs tests/AuswertungPro.Next.UI.Tests/PlayerWindowVsaMappingTests.cs
git commit -m "refactor(viewer): extract import fallback code policy"
```

---

### Task 4: Timeline-Positionsrechnung isolieren

**Files:**
- Create: `src/AuswertungPro.Next.UI/Player/PlayerTimelineLayoutCalculator.cs`
- Create: `tests/AuswertungPro.Next.UI.Tests/PlayerTimelineLayoutCalculatorTests.cs`
- Modify: `src/AuswertungPro.Next.UI/Views/Windows/PlayerWindow.xaml.cs`

- [ ] **Step 1: Tests fuer Markerpositionen schreiben**

Create `tests/AuswertungPro.Next.UI.Tests/PlayerTimelineLayoutCalculatorTests.cs`:

```csharp
using AuswertungPro.Next.UI.Player;
using Xunit;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerTimelineLayoutCalculatorTests
{
    [Theory]
    [InlineData(0, 100, 20, 500, 20)]
    [InlineData(50, 100, 20, 500, 260)]
    [InlineData(100, 100, 20, 500, 500)]
    [InlineData(-10, 100, 20, 500, 20)]
    [InlineData(120, 100, 20, 500, 500)]
    public void CalculatePointX_clamps_meter_to_track(double meter, double length, double offset, double width, double expected)
    {
        var x = PlayerTimelineLayoutCalculator.CalculatePointX(meter, length, offset, width);

        Assert.Equal(expected, x, precision: 3);
    }

    [Fact]
    public void CalculateRange_returns_sorted_clamped_bounds()
    {
        var range = PlayerTimelineLayoutCalculator.CalculateRangeX(90, 10, 100, 20, 500);

        Assert.Equal(68, range.StartX, precision: 3);
        Assert.Equal(452, range.EndX, precision: 3);
        Assert.Equal(384, range.Width, precision: 3);
    }
}
```

- [ ] **Step 2: Test rot sehen**

Run:

```powershell
dotnet test tests/AuswertungPro.Next.UI.Tests/AuswertungPro.Next.UI.Tests.csproj --nologo --filter "FullyQualifiedName~PlayerTimelineLayoutCalculatorTests"
```

Expected:

```text
CS0246 fuer PlayerTimelineLayoutCalculator
```

- [ ] **Step 3: Calculator erstellen**

Create `src/AuswertungPro.Next.UI/Player/PlayerTimelineLayoutCalculator.cs`:

```csharp
namespace AuswertungPro.Next.UI.Player;

public readonly record struct TimelineRangeX(double StartX, double EndX, double Width);

public static class PlayerTimelineLayoutCalculator
{
    public static double CalculatePointX(double meter, double totalLength, double trackOffsetX, double trackWidth)
    {
        if (totalLength <= 0 || trackWidth <= 0)
        {
            return trackOffsetX;
        }

        var ratio = Math.Clamp(meter / totalLength, 0.0, 1.0);
        return trackOffsetX + ratio * trackWidth;
    }

    public static TimelineRangeX CalculateRangeX(double startMeter, double endMeter, double totalLength, double trackOffsetX, double trackWidth)
    {
        var startX = CalculatePointX(startMeter, totalLength, trackOffsetX, trackWidth);
        var endX = CalculatePointX(endMeter, totalLength, trackOffsetX, trackWidth);

        if (endX < startX)
        {
            (startX, endX) = (endX, startX);
        }

        return new TimelineRangeX(startX, endX, Math.Max(0, endX - startX));
    }
}
```

- [ ] **Step 4: PlayerWindow-Markerrechnung umstellen**

In `CreatePointMarker`, replace direct ratio math with:

```csharp
var x = Player.PlayerTimelineLayoutCalculator.CalculatePointX(
    info.MeterStart,
    _damageOverlay.TotalLengthMeter,
    offsetX,
    trackWidth);
```

In `CreateRangeMarker`, replace direct start/end ratio math with:

```csharp
var range = Player.PlayerTimelineLayoutCalculator.CalculateRangeX(
    info.MeterStart,
    info.MeterEnd ?? info.MeterStart,
    _damageOverlay.TotalLengthMeter,
    offsetX,
    trackWidth);
```

Use `range.StartX`, `range.EndX`, and `range.Width` for Canvas placement.

- [ ] **Step 5: Tests gruen**

Run:

```powershell
dotnet test tests/AuswertungPro.Next.UI.Tests/AuswertungPro.Next.UI.Tests.csproj --nologo --filter "FullyQualifiedName~PlayerTimelineLayoutCalculatorTests|FullyQualifiedName~PlayerWindowVsaMappingTests"
```

Expected:

```text
Bestanden!
```

- [ ] **Step 6: Commit**

```powershell
git add src/AuswertungPro.Next.UI/Player/PlayerTimelineLayoutCalculator.cs src/AuswertungPro.Next.UI/Views/Windows/PlayerWindow.xaml.cs tests/AuswertungPro.Next.UI.Tests/PlayerTimelineLayoutCalculatorTests.cs
git commit -m "refactor(viewer): isolate timeline marker layout"
```

---

### Task 5: Playback-Zustand ohne VLC testbar machen

**Files:**
- Create: `src/AuswertungPro.Next.UI/Player/PlayerPlaybackState.cs`
- Create: `tests/AuswertungPro.Next.UI.Tests/PlayerPlaybackStateTests.cs`
- Modify: `src/AuswertungPro.Next.UI/Views/Windows/PlayerWindow.xaml.cs`

- [ ] **Step 1: Tests schreiben**

Create `tests/AuswertungPro.Next.UI.Tests/PlayerPlaybackStateTests.cs`:

```csharp
using AuswertungPro.Next.UI.Player;
using Xunit;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerPlaybackStateTests
{
    [Theory]
    [InlineData(0.1f, 0.25f)]
    [InlineData(0.25f, 0.25f)]
    [InlineData(1.0f, 1.0f)]
    [InlineData(8.0f, 8.0f)]
    [InlineData(9.0f, 8.0f)]
    public void ClampRate_keeps_supported_speed_range(float input, float expected)
    {
        Assert.Equal(expected, PlayerPlaybackState.ClampRate(input));
    }

    [Theory]
    [InlineData(1000, 5000, 6000)]
    [InlineData(1000, -5000, 0)]
    [InlineData(99000, 5000, 100000)]
    public void AddSeconds_clamps_to_video_duration(long currentMs, int deltaSeconds, long expectedMs)
    {
        var next = PlayerPlaybackState.AddSeconds(currentMs, 100000, deltaSeconds);

        Assert.Equal(expectedMs, next);
    }
}
```

- [ ] **Step 2: Test rot sehen**

Run:

```powershell
dotnet test tests/AuswertungPro.Next.UI.Tests/AuswertungPro.Next.UI.Tests.csproj --nologo --filter "FullyQualifiedName~PlayerPlaybackStateTests"
```

Expected:

```text
CS0246 fuer PlayerPlaybackState
```

- [ ] **Step 3: Playback-State-Klasse erstellen**

Create `src/AuswertungPro.Next.UI/Player/PlayerPlaybackState.cs`:

```csharp
namespace AuswertungPro.Next.UI.Player;

public static class PlayerPlaybackState
{
    public const float MinRate = 0.25f;
    public const float MaxRate = 8.0f;

    public static float ClampRate(float rate)
        => Math.Clamp(rate, MinRate, MaxRate);

    public static long AddSeconds(long currentTimeMs, long durationMs, int deltaSeconds)
    {
        var next = currentTimeMs + deltaSeconds * 1000L;
        return Math.Clamp(next, 0, Math.Max(0, durationMs));
    }
}
```

- [ ] **Step 4: PlayerWindow-Konstanten delegieren**

In `PlayerWindow.xaml.cs`, replace private constants:

```csharp
private const float MinRate = Player.PlayerPlaybackState.MinRate;
private const float MaxRate = Player.PlayerPlaybackState.MaxRate;
```

In `ChangeSpeed`, apply:

```csharp
SetSpeed(Player.PlayerPlaybackState.ClampRate(_player.Rate + delta));
```

In `JumpSeconds`, calculate target with:

```csharp
var target = Player.PlayerPlaybackState.AddSeconds(_player.Time, _player.Length, seconds);
_player.Time = target;
```

- [ ] **Step 5: Tests gruen**

Run:

```powershell
dotnet test tests/AuswertungPro.Next.UI.Tests/AuswertungPro.Next.UI.Tests.csproj --nologo --filter "FullyQualifiedName~PlayerPlaybackStateTests"
```

Expected:

```text
Bestanden!
```

- [ ] **Step 6: Commit**

```powershell
git add src/AuswertungPro.Next.UI/Player/PlayerPlaybackState.cs src/AuswertungPro.Next.UI/Views/Windows/PlayerWindow.xaml.cs tests/AuswertungPro.Next.UI.Tests/PlayerPlaybackStateTests.cs
git commit -m "refactor(viewer): isolate playback state rules"
```

---

### Task 6: PlayerWindow in partial Dateien schneiden, ohne Verhalten zu aendern

**Files:**
- Create: `src/AuswertungPro.Next.UI/Views/Windows/PlayerWindow.Playback.cs`
- Create: `src/AuswertungPro.Next.UI/Views/Windows/PlayerWindow.LiveDetection.cs`
- Create: `src/AuswertungPro.Next.UI/Views/Windows/PlayerWindow.Coding.cs`
- Create: `src/AuswertungPro.Next.UI/Views/Windows/PlayerWindow.OverlayRendering.cs`
- Modify: `src/AuswertungPro.Next.UI/Views/Windows/PlayerWindow.xaml.cs`

- [ ] **Step 1: Keine Logik aendern**

Diese Aufgabe ist mechanisch. Nur Methodenblöcke verschieben. Keine Methode umbenennen, keine Sichtbarkeit aendern, keine neuen Abhaengigkeiten einfuehren.

Methodenbereiche:

```text
Playback: lines around 310-1018
LiveDetection: lines around 1019-2534
Coding: lines around 2593-6337 and 6617-8030
OverlayRendering: lines around 3409-4890 and 7467-7638
```

- [ ] **Step 2: Jede neue Datei als partial class anlegen**

Each new file must start with:

```csharp
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
}
```

Then move the matching methods into the class body.

- [ ] **Step 3: Build nach jedem verschobenen Bereich**

After each moved block, run:

```powershell
dotnet build src/AuswertungPro.Next.UI/AuswertungPro.Next.UI.csproj --nologo -v minimal -p:UseAppHost=false
```

Expected:

```text
0 Fehler
```

- [ ] **Step 4: UI-Tests laufen lassen**

Run:

```powershell
dotnet test tests/AuswertungPro.Next.UI.Tests/AuswertungPro.Next.UI.Tests.csproj --nologo
```

Expected:

```text
Bestanden!
```

- [ ] **Step 5: Commit**

```powershell
git add src/AuswertungPro.Next.UI/Views/Windows/PlayerWindow*.cs
git commit -m "refactor(viewer): split PlayerWindow into partial files"
```

---

### Task 7: XAML-Ressourcen aus PlayerWindow herausziehen

**Files:**
- Create: `src/AuswertungPro.Next.UI/Views/Windows/PlayerWindow.Resources.xaml`
- Modify: `src/AuswertungPro.Next.UI/Views/Windows/PlayerWindow.xaml`

- [ ] **Step 1: ResourceDictionary erstellen**

Create `PlayerWindow.Resources.xaml`:

```xml
<ResourceDictionary
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
</ResourceDictionary>
```

- [ ] **Step 2: Style-Block verschieben**

Move these resources from `PlayerWindow.xaml` into `PlayerWindow.Resources.xaml` without changing keys:

```text
PlayerButton
PlayerPrimaryButton
MarkToolPopupButton
SpeedToggleButton
DefectActionBtn
```

- [ ] **Step 3: ResourceDictionary einbinden**

In `PlayerWindow.xaml`, replace the moved style definitions with:

```xml
<ResourceDictionary>
    <ResourceDictionary.MergedDictionaries>
        <ResourceDictionary Source="PlayerWindow.Resources.xaml"/>
    </ResourceDictionary.MergedDictionaries>
</ResourceDictionary>
```

If `PlayerWindow.xaml` already has other resources, keep them in the same dictionary and add the merged dictionary first.

- [ ] **Step 4: Build pruefen**

Run:

```powershell
dotnet build src/AuswertungPro.Next.UI/AuswertungPro.Next.UI.csproj --nologo -v minimal -p:UseAppHost=false
```

Expected:

```text
0 Fehler
```

- [ ] **Step 5: App-Smoke-Test**

Run:

```powershell
dotnet run --project src/AuswertungPro.Next.UI/AuswertungPro.Next.UI.csproj --no-build
```

Manual expected:

```text
PlayerWindow oeffnet, Buttons haben weiterhin Stil, Coding-Toolbar ist sichtbar, kein XAML StaticResource-Fehler.
```

- [ ] **Step 6: Commit**

```powershell
git add src/AuswertungPro.Next.UI/Views/Windows/PlayerWindow.xaml src/AuswertungPro.Next.UI/Views/Windows/PlayerWindow.Resources.xaml
git commit -m "refactor(viewer): move PlayerWindow styles to resource dictionary"
```

---

### Task 8: Abschlussverifikation

**Files:**
- Verify only

- [ ] **Step 1: UI Tests**

Run:

```powershell
dotnet test tests/AuswertungPro.Next.UI.Tests/AuswertungPro.Next.UI.Tests.csproj --nologo
```

Expected:

```text
Bestanden!
```

- [ ] **Step 2: Pipeline Tests**

Run:

```powershell
dotnet test tests/AuswertungPro.Next.Pipeline.Tests/AuswertungPro.Next.Pipeline.Tests.csproj --nologo
```

Expected:

```text
Bestanden!
```

- [ ] **Step 3: UI Build**

Run:

```powershell
dotnet build src/AuswertungPro.Next.UI/AuswertungPro.Next.UI.csproj --nologo -v minimal -p:UseAppHost=false
```

Expected:

```text
0 Fehler
```

- [ ] **Step 4: Manual Smoke-Test**

Run:

```powershell
dotnet run --project src/AuswertungPro.Next.UI/AuswertungPro.Next.UI.csproj --no-build
```

Manual expected:

```text
Video laesst sich starten, pausieren, seeken.
Speed-Buttons funktionieren.
Coding-Modus oeffnet und schliesst.
Overlay-Werkzeuge zeichnen sichtbar.
Live-KI Button laesst sich toggeln, ohne Window-Crash.
```

- [ ] **Step 5: Final Commit falls noetig**

If only verification happened, no commit. If small fixes were needed:

```powershell
git add src tests
git commit -m "fix(viewer): stabilize refactor follow-ups"
```

---

## Erfolgskriterium

Paket 1 ist fertig, wenn:

- `PlayerWindow.xaml.cs` deutlich kleiner ist oder zumindest in partial Dateien getrennt ist.
- VSA-Codehint-, Importfallback-, Timeline- und Playback-Regeln direkt getestet sind.
- UI-Build gruen ist.
- UI-Tests gruen sind.
- Der Viewer im Smoke-Test normal startet.

## Naechstes Paket nach Paket 1

Erst nach diesem Paket lohnt der tiefere Umbau:

- `CodingSidePanel` als eigenes `UserControl`
- `PlayerTransportBar` als eigenes `UserControl`
- `CodingOverlayRenderer` als WPF-Rendering-Service
- `PlayerWindowViewModel` fuer Commands und Status

Diese Schritte sind bewusst nicht Teil von Paket 1, weil erst die Logik aus der grossen Datei heraus muss.
