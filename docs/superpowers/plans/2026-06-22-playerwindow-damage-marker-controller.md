# PlayerWindow Damage Marker Controller Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Move the timeline damage-marker responsibility out of `PlayerWindow` into a focused controller without changing visible playback behavior.

**Architecture:** `PlayerWindow` stays owner of the WPF controls and teardown order, but delegates damage-marker build and resize work to `DamageMarkerController`. The marker data records move from the window file to the existing `AuswertungPro.Next.UI.Player` area, because `DataPageVideoOverlayBuilder` already uses them outside the window.

**Tech Stack:** C#/.NET WPF, LibVLCSharp, xUnit architecture guard tests.

---

## File Structure

- Create: `src/AuswertungPro.Next.UI/Player/PlayerDamageOverlayData.cs`
  - Owns `DamageMarkerInfo` and `PlayerDamageOverlayData`.
- Create: `src/AuswertungPro.Next.UI/Player/DamageMarkerController.cs`
  - Owns marker list state, marker UI construction, marker repositioning, and marker-click seek behavior.
- Modify: `src/AuswertungPro.Next.UI/Views/Windows/PlayerWindow.xaml.cs`
  - Remove damage-marker records and `_damageMarkers` field.
  - Add `_damageMarkerController`.
  - Delegate loaded/resize calls to the controller.
- Delete: `src/AuswertungPro.Next.UI/Views/Windows/PlayerWindow.Playback.DamageMarkers.cs`
  - Its methods move into `DamageMarkerController`.
- Modify: `src/AuswertungPro.Next.UI/DataPage/DataPageVideoOverlayBuilder.cs`
  - Import marker records from `AuswertungPro.Next.UI.Player`.
- Modify: `tests/AuswertungPro.Next.UI.Tests/UiArchitectureGuardTests.cs`
  - Add a guard that marker state no longer lives in `PlayerWindow`.

## Task 1: Move Damage Overlay Data Records

**Files:**
- Create: `src/AuswertungPro.Next.UI/Player/PlayerDamageOverlayData.cs`
- Modify: `src/AuswertungPro.Next.UI/Views/Windows/PlayerWindow.xaml.cs`
- Modify: `src/AuswertungPro.Next.UI/DataPage/DataPageVideoOverlayBuilder.cs`

- [ ] **Step 1: Create the data-record file**

Create `src/AuswertungPro.Next.UI/Player/PlayerDamageOverlayData.cs`:

```csharp
using System.Collections.Generic;

namespace AuswertungPro.Next.UI.Player;

public sealed record DamageMarkerInfo(
    string Code,
    string? Description,
    double MeterStart,
    double? MeterEnd,
    bool IsStreckenschaden);

public sealed record PlayerDamageOverlayData(
    double PipeLengthMeters,
    IReadOnlyList<DamageMarkerInfo> Markers);
```

- [ ] **Step 2: Remove the records from the window file**

In `src/AuswertungPro.Next.UI/Views/Windows/PlayerWindow.xaml.cs`, delete this block:

```csharp
public sealed record DamageMarkerInfo(
    string Code,
    string? Description,
    double MeterStart,
    double? MeterEnd,
    bool IsStreckenschaden);

public sealed record PlayerDamageOverlayData(
    double PipeLengthMeters,
    IReadOnlyList<DamageMarkerInfo> Markers);
```

Add this using near the other UI usings:

```csharp
using AuswertungPro.Next.UI.Player;
```

- [ ] **Step 3: Update the DataPage overlay builder namespace**

In `src/AuswertungPro.Next.UI/DataPage/DataPageVideoOverlayBuilder.cs`, replace:

```csharp
using AuswertungPro.Next.UI.Views.Windows;
```

with:

```csharp
using AuswertungPro.Next.UI.Player;
```

- [ ] **Step 4: Build the UI project**

Run:

```powershell
dotnet build src/AuswertungPro.Next.UI/AuswertungPro.Next.UI.csproj -v minimal -p:UseAppHost=false
```

Expected: build passes with 0 errors.

- [ ] **Step 5: Commit**

```powershell
git add src/AuswertungPro.Next.UI/Player/PlayerDamageOverlayData.cs src/AuswertungPro.Next.UI/Views/Windows/PlayerWindow.xaml.cs src/AuswertungPro.Next.UI/DataPage/DataPageVideoOverlayBuilder.cs
git commit -m "refactor: move player damage overlay data"
```

## Task 2: Extract DamageMarkerController

**Files:**
- Create: `src/AuswertungPro.Next.UI/Player/DamageMarkerController.cs`
- Modify: `src/AuswertungPro.Next.UI/Views/Windows/PlayerWindow.xaml.cs`
- Delete: `src/AuswertungPro.Next.UI/Views/Windows/PlayerWindow.Playback.DamageMarkers.cs`

- [ ] **Step 1: Add the controller class**

Create `src/AuswertungPro.Next.UI/Player/DamageMarkerController.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using LibVLCSharp.Shared;
using Rectangle = System.Windows.Shapes.Rectangle;

namespace AuswertungPro.Next.UI.Player;

public sealed class DamageMarkerController
{
    private readonly Canvas _markerCanvas;
    private readonly Slider _positionSlider;
    private readonly PlayerDamageOverlayData? _damageOverlay;
    private readonly MediaPlayer _player;
    private readonly Action _ensurePlaying;
    private readonly Action _updateUi;
    private readonly List<(DamageMarkerInfo Info, FrameworkElement Container, FrameworkElement TickOrRange, TextBlock Label)> _damageMarkers = new();

    public DamageMarkerController(
        Canvas markerCanvas,
        Slider positionSlider,
        PlayerDamageOverlayData? damageOverlay,
        MediaPlayer player,
        Action ensurePlaying,
        Action updateUi)
    {
        _markerCanvas = markerCanvas ?? throw new ArgumentNullException(nameof(markerCanvas));
        _positionSlider = positionSlider ?? throw new ArgumentNullException(nameof(positionSlider));
        _damageOverlay = damageOverlay;
        _player = player ?? throw new ArgumentNullException(nameof(player));
        _ensurePlaying = ensurePlaying ?? throw new ArgumentNullException(nameof(ensurePlaying));
        _updateUi = updateUi ?? throw new ArgumentNullException(nameof(updateUi));
    }

    public void Build()
    {
        if (_damageOverlay is null || _damageOverlay.PipeLengthMeters <= 0)
            return;

        _markerCanvas.Children.Clear();
        _damageMarkers.Clear();

        var accentBrush = (Brush)_markerCanvas.FindResource("AccentBrush");
        var accentColor = (Color)_markerCanvas.FindResource("ColorAccent");

        foreach (var info in _damageOverlay.Markers)
        {
            if (info.MeterStart < 0 || info.MeterStart > _damageOverlay.PipeLengthMeters)
                continue;

            if (info.IsStreckenschaden && info.MeterEnd.HasValue && info.MeterEnd.Value > info.MeterStart)
                CreateRangeMarker(info, accentBrush, accentColor);
            else
                CreatePointMarker(info, accentBrush, accentColor);
        }

        Reposition();
    }

    public void Reposition()
    {
        if (_damageOverlay is null || _damageMarkers.Count == 0)
            return;

        var (offsetX, trackWidth) = GetSliderTrackBounds();
        if (trackWidth <= 0)
            return;

        var pipeLength = _damageOverlay.PipeLengthMeters;

        foreach (var (info, container, tickOrRange, label) in _damageMarkers)
        {
            var x = PlayerTimelineLayoutCalculator.CalculatePointX(
                info.MeterStart,
                pipeLength,
                offsetX,
                trackWidth);

            if (info.IsStreckenschaden && info.MeterEnd.HasValue && info.MeterEnd.Value > info.MeterStart)
            {
                var range = PlayerTimelineLayoutCalculator.CalculateRangeX(
                    info.MeterStart,
                    info.MeterEnd.Value,
                    pipeLength,
                    offsetX,
                    trackWidth);
                Canvas.SetLeft(container, range.StartX);
                var barWidth = Math.Max(range.Width, 3);
                ((Rectangle)tickOrRange).Width = barWidth;

                label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                var labelWidth = label.DesiredSize.Width;
                Canvas.SetLeft(label, (barWidth - labelWidth) / 2);
            }
            else
            {
                Canvas.SetLeft(container, x - 1);
                label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                var labelWidth = label.DesiredSize.Width;
                Canvas.SetLeft(label, -(labelWidth / 2) + 1);
            }
        }
    }

    private void CreatePointMarker(DamageMarkerInfo info, Brush accentBrush, Color accentColor)
    {
        var container = new Canvas { Cursor = Cursors.Hand };

        var tick = new Rectangle
        {
            Width = 2,
            Height = 14,
            Fill = accentBrush,
            Opacity = 0.85,
            IsHitTestVisible = false,
            Effect = new System.Windows.Media.Effects.DropShadowEffect { Color = accentColor, BlurRadius = 6, ShadowDepth = 0, Opacity = 0.5 }
        };
        Canvas.SetTop(tick, -5);
        container.Children.Add(tick);

        var label = new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(info.Code) ? "?" : info.Code.Trim(),
            FontSize = 9,
            FontWeight = FontWeights.SemiBold,
            FontFamily = new FontFamily("Consolas"),
            Foreground = accentBrush,
            IsHitTestVisible = false
        };
        Canvas.SetTop(label, -19);
        container.Children.Add(label);

        container.ToolTip = $"{info.Code} @ {info.MeterStart:0.0}m"
            + (string.IsNullOrWhiteSpace(info.Description) ? "" : $"\n{info.Description}");

        container.MouseLeftButtonDown += (_, _) => SeekToMeter(info.MeterStart);

        _markerCanvas.Children.Add(container);
        _damageMarkers.Add((info, container, tick, label));
    }

    private void CreateRangeMarker(DamageMarkerInfo info, Brush accentBrush, Color accentColor)
    {
        var container = new Canvas { Cursor = Cursors.Hand };

        var bar = new Rectangle
        {
            Height = 5,
            Fill = accentBrush,
            Opacity = 0.35,
            RadiusX = 2,
            RadiusY = 2,
            IsHitTestVisible = false
        };
        Canvas.SetTop(bar, -2);
        container.Children.Add(bar);

        var startTick = new Rectangle
        {
            Width = 1.5,
            Height = 10,
            Fill = accentBrush,
            Opacity = 0.7,
            IsHitTestVisible = false
        };
        Canvas.SetTop(startTick, -4);
        container.Children.Add(startTick);

        var label = new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(info.Code) ? "?" : info.Code.Trim(),
            FontSize = 9,
            FontWeight = FontWeights.SemiBold,
            FontFamily = new FontFamily("Consolas"),
            Foreground = accentBrush,
            IsHitTestVisible = false
        };
        Canvas.SetTop(label, -19);
        container.Children.Add(label);

        var endM = Math.Min(info.MeterEnd ?? info.MeterStart, _damageOverlay!.PipeLengthMeters);
        container.ToolTip = $"{info.Code} Strecke {info.MeterStart:0.0}m - {endM:0.0}m"
            + (string.IsNullOrWhiteSpace(info.Description) ? "" : $"\n{info.Description}");

        container.MouseLeftButtonDown += (_, _) => SeekToMeter(info.MeterStart);

        _markerCanvas.Children.Add(container);
        _damageMarkers.Add((info, container, bar, label));
    }

    private (double offsetX, double trackWidth) GetSliderTrackBounds()
    {
        if (_positionSlider.Template?.FindName("PART_Track", _positionSlider) is Track track
            && track.IsVisible
            && track.ActualWidth > 0)
        {
            var thumbHalf = (track.Thumb?.ActualWidth ?? 18) / 2.0;
            var ptStart = track.TranslatePoint(new Point(thumbHalf, 0), _markerCanvas);
            var ptEnd = track.TranslatePoint(new Point(track.ActualWidth - thumbHalf, 0), _markerCanvas);
            return (ptStart.X, ptEnd.X - ptStart.X);
        }

        return (9, Math.Max(_markerCanvas.ActualWidth - 18, 1));
    }

    private void SeekToMeter(double meter)
    {
        if (_damageOverlay is null || _damageOverlay.PipeLengthMeters <= 0)
            return;

        _ensurePlaying();
        _player.SetPause(true);

        var ratio = Math.Clamp(meter / _damageOverlay.PipeLengthMeters, 0.0, 1.0);
        _positionSlider.Value = ratio * _positionSlider.Maximum;

        var length = _player.Length;
        if (length > 0)
            _player.Time = (long)(ratio * length);
        else
            _player.Position = (float)ratio;

        _updateUi();
    }
}
```

- [ ] **Step 2: Wire the controller into the window**

In `src/AuswertungPro.Next.UI/Views/Windows/PlayerWindow.xaml.cs`, replace:

```csharp
private readonly List<(DamageMarkerInfo Info, FrameworkElement Container, FrameworkElement TickOrRange, TextBlock Label)> _damageMarkers = new();
```

with:

```csharp
private readonly DamageMarkerController _damageMarkerController;
```

After this existing block:

```csharp
_player = new MediaPlayer(_libVlc)
{
    EnableHardwareDecoding = _options.EnableHardwareDecoding
};
VideoView.MediaPlayer = _player;
```

add:

```csharp
_damageMarkerController = new DamageMarkerController(
    DamageMarkerCanvas,
    PositionSlider,
    _damageOverlay,
    _player,
    EnsurePlaying,
    UpdateUi);
```

Replace:

```csharp
BuildDamageMarkers();
```

with:

```csharp
_damageMarkerController.Build();
```

Replace:

```csharp
DamageMarkerCanvas.SizeChanged += (_, __) => RepositionDamageMarkers();
```

with:

```csharp
DamageMarkerCanvas.SizeChanged += (_, __) => _damageMarkerController.Reposition();
```

- [ ] **Step 3: Delete the old partial file**

Delete `src/AuswertungPro.Next.UI/Views/Windows/PlayerWindow.Playback.DamageMarkers.cs`.

- [ ] **Step 4: Build the UI project**

Run:

```powershell
dotnet build src/AuswertungPro.Next.UI/AuswertungPro.Next.UI.csproj -v minimal -p:UseAppHost=false
```

Expected: build passes with 0 errors.

- [ ] **Step 5: Commit**

```powershell
git add src/AuswertungPro.Next.UI/Player/DamageMarkerController.cs src/AuswertungPro.Next.UI/Views/Windows/PlayerWindow.xaml.cs src/AuswertungPro.Next.UI/Views/Windows/PlayerWindow.Playback.DamageMarkers.cs
git commit -m "refactor: extract damage marker controller"
```

## Task 3: Add Architecture Guard

**Files:**
- Modify: `tests/AuswertungPro.Next.UI.Tests/UiArchitectureGuardTests.cs`

- [ ] **Step 1: Add the guard test**

In `tests/AuswertungPro.Next.UI.Tests/UiArchitectureGuardTests.cs`, add this test after `Ui_code_accesses_App_Services_only_at_composition_root`:

```csharp
[Fact]
public void PlayerWindow_damage_markers_live_in_controller()
{
    var root = FindRepositoryRoot();
    var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
    var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
    var controllerPath = Path.Combine(uiRoot, "Player", "DamageMarkerController.cs");

    Assert.True(File.Exists(controllerPath), "DamageMarkerController muss ausserhalb der PlayerWindow-Partials liegen.");

    var windowText = string.Join(
        Environment.NewLine,
        Directory.EnumerateFiles(windowsRoot, "PlayerWindow*.cs")
            .Where(path => !path.EndsWith("PlayerWindow.Playback.DamageMarkers.cs", StringComparison.OrdinalIgnoreCase))
            .Select(File.ReadAllText));
    var windowRoot = File.ReadAllText(Path.Combine(windowsRoot, "PlayerWindow.xaml.cs"));
    var controller = File.ReadAllText(controllerPath);

    Assert.DoesNotContain("_damageMarkers", windowText);
    Assert.DoesNotContain("BuildDamageMarkers", windowText);
    Assert.DoesNotContain("RepositionDamageMarkers", windowText);
    Assert.Contains("new DamageMarkerController", windowRoot);
    Assert.Contains("_damageMarkerController.Build()", windowRoot);
    Assert.Contains("_damageMarkerController.Reposition()", windowRoot);
    Assert.Contains("private readonly List<(DamageMarkerInfo Info", controller);
    Assert.Contains("PlayerTimelineLayoutCalculator.CalculatePointX", controller);
}
```

- [ ] **Step 2: Run the new test**

Run:

```powershell
dotnet test tests/AuswertungPro.Next.UI.Tests/AuswertungPro.Next.UI.Tests.csproj -v minimal --no-restore --filter PlayerWindow_damage_markers_live_in_controller
```

Expected: the single guard test passes.

- [ ] **Step 3: Run the full UI test project**

Run:

```powershell
dotnet test tests/AuswertungPro.Next.UI.Tests/AuswertungPro.Next.UI.Tests.csproj -v minimal --no-restore
```

Expected: all UI tests pass.

- [ ] **Step 4: Commit**

```powershell
git add tests/AuswertungPro.Next.UI.Tests/UiArchitectureGuardTests.cs
git commit -m "test: guard damage marker controller split"
```

## Task 4: Final Verification

**Files:**
- Verify all files touched by Tasks 1-3.

- [ ] **Step 1: Check only intended files are changed**

Run:

```powershell
git status -sb
```

Expected: no modified tracked files remain. Known unrelated untracked files may still be present:

```text
docs/VSA-Regelwerk-KI-Pipeline.md
docs/superpowers/plans/2026-06-21-pipeline-kb-fixes.md
docs/superpowers/specs/2026-06-21-pipeline-kb-fixes-design.md
sidecar/models/grounding_dino_swinb/
training/vsa_classifier/docs/
```

- [ ] **Step 2: Build**

Run:

```powershell
dotnet build src/AuswertungPro.Next.UI/AuswertungPro.Next.UI.csproj -v minimal -p:UseAppHost=false
```

Expected: build passes with 0 errors.

- [ ] **Step 3: Test**

Run:

```powershell
dotnet test tests/AuswertungPro.Next.UI.Tests/AuswertungPro.Next.UI.Tests.csproj -v minimal --no-restore
```

Expected: all UI tests pass.

- [ ] **Step 4: Manual player check**

Open one video with protocol/VSA findings and verify:

```text
1. Timeline markers are visible.
2. Point markers and range markers keep their previous positions.
3. Tooltips and labels still show the same code/description.
4. Clicking a marker seeks to the matching meter position.
5. Resizing the player repositions markers correctly.
```

This is required because the visual WPF canvas behavior is not covered by stable unit tests in the current repo.

## Self-Review

- Spec coverage: The plan implements the pilot only: data extraction, controller extraction, window wiring, build/test/manual verification. Later controllers from the spec remain deliberately out of scope.
- Placeholder scan: No red-flag placeholder patterns remain.
- Type consistency: `DamageMarkerController`, `DamageMarkerInfo`, and `PlayerDamageOverlayData` live in `AuswertungPro.Next.UI.Player`; `PlayerWindow` and `DataPageVideoOverlayBuilder` import that namespace.
