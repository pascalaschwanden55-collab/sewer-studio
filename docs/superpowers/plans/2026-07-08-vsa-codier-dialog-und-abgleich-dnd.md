# VSA-Codier-Dialog + Abgleich-Panel (DnD & symmetrische Aktionen) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Den „+"/Codier-Weg im Protokoll-Editor auf den modernen VSA-KEK-2020-Dialog umstellen und das Abgleich-Panel um Drag&Drop zwischen KI/Import sowie um symmetrische rechte Aktionen (Foto/BBox/Bestätigen→KI-Brain) erweitern.

**Architecture:** WPF/MVVM. F1 tauscht in `ProtocolObservationsWindow` einen Dialog aus (beide arbeiten auf `ProtocolEntry`). F2 ergänzt das bestehende `PlayerCodingSidePanel` (Code-behind-getrieben) um ein Attached-Behavior für Drag&Drop, einen reinen Transfer-Helfer und drei rechte Kontextmenü-Aktionen, die vorhandene Workflows (Foto-Viewer, Code-Explorer-Edit, Training-Persistenz→KnowledgeBase) wiederverwenden.

**Tech Stack:** .NET 10, WPF, CommunityToolkit.Mvvm, xUnit. Kein neues NuGet-Paket.

## Global Constraints
- Kommentare auf Deutsch (Projektregel).
- Keine neuen NuGet-Pakete ohne Rückfrage.
- Bestehenden Code nur an den benannten Stellen ändern; kein Löschen von `ObservationCatalogWindow`.
- Deep-Copy beim Kopieren: **neue** `CodingEvent.EventId` UND **neue** `ProtocolEntry.EntryId` (sonst kollidieren IDs → Abgleich/Highlighting kaputt).
- Build bei laufender App: `dotnet build tests\<projekt>.csproj --no-dependencies` (die laufende `SewerStudio.exe` ist sonst gesperrt).

---

### Task 1: F1 — Protokoll-Editor öffnet den modernen VSA-KEK-2020-Dialog

**Files:**
- Modify: `src/AuswertungPro.Next.UI/Views/ProtocolObservationsWindow.xaml.cs:213-241` (`OpenObservationDialog`)

**Interfaces:**
- Consumes: `_sp.CodeSelectionCatalog` (`IVsaCodeSelectionCatalog`), `VsaCodeExplorerViewModel(ProtocolEntry? existingEntry, double? presetMeter, TimeSpan? presetZeit, IVsaCodeSelectionCatalog? catalog)`, `VsaCodeExplorerWindow(VsaCodeExplorerViewModel vm, string? videoPath, TimeSpan? currentVideoTime)` mit `ProtocolEntry? SelectedEntry`, `CodingProtocolEntryCopier.CopyEditableValues(source, target)`.
- Produces: (keine — interne View-Methode, Rückgabe bleibt `bool`).

- [ ] **Step 1: `OpenObservationDialog` austauschen**

Ersetze den Methodenrumpf (Z. 213–241) durch:

```csharp
private bool OpenObservationDialog(ProtocolEntry entry)
{
    if (_sp.CodeSelectionCatalog is null)
    {
        _sp.Dialogs.Info("Code-Katalog ist nicht verfuegbar.", "Protokoll");
        return false;
    }

    _isOpeningDialog = true;
    try
    {
        // Moderner VSA-KEK-2020-Dialog (wie im Player/Live-Codieren) statt des alten
        // Beobachtungskatalogs. Beide arbeiten auf ProtocolEntry; der moderne liefert
        // einen NEUEN Entry (SelectedEntry), dessen Werte wir in den bestehenden
        // Eintrag zurueckspiegeln.
        var vm = new AuswertungPro.Next.UI.ViewModels.Windows.VsaCodeExplorerViewModel(
            entry, entry.MeterStart, entry.Zeit, _sp.CodeSelectionCatalog);
        var dlg = new AuswertungPro.Next.UI.Views.Windows.VsaCodeExplorerWindow(vm, _videoPath, entry.Zeit)
        {
            Owner = this,
            Width = 1420,
            Height = 850
        };
        if (dlg.ShowDialog() == true && dlg.SelectedEntry is not null)
        {
            AuswertungPro.Next.UI.Ai.CodingProtocolEntryCopier.CopyEditableValues(dlg.SelectedEntry, entry);
            return true;
        }
        return false;
    }
    finally
    {
        _isOpeningDialog = false;
    }
}
```

Hinweis: `entry` ist der Bestands-Entry (Neu: frischer Entry aus `AddEntry`; Bearbeiten: der selektierte). `CopyEditableValues(dlg.SelectedEntry, entry)` schreibt Code/Beschreibung/Meter/IsStreckenschaden/Zeit/CodeMeta/FotoPaths in `entry` — genau die Felder, die die Beobachtungsliste und „Primäre Schäden" anzeigen.

- [ ] **Step 2: Build (Testprojekt gegen laufende App entkoppelt)**

Run: `dotnet build src/AuswertungPro.Next.UI/AuswertungPro.Next.UI.csproj --nologo -v q`
Expected: `0 Fehler` (nur CS-Fehler zählen; MSB3021/exe-Sperre ignorieren, falls App läuft).

- [ ] **Step 3: Manuelle Verifikation**

App starten → Haltung → „Primäre Schäden" → „+" → es öffnet der moderne Dialog „VSA Schadencodierung - VSA-KEK 2020" (nicht der gelbe Katalog). Code wählen → Übernehmen → neuer Eintrag erscheint korrekt. Ebenso „Bearbeiten" (Zeile anklicken) und „Kopieren".

- [ ] **Step 4: Commit**

```bash
git add src/AuswertungPro.Next.UI/Views/ProtocolObservationsWindow.xaml.cs
git commit -m "feat(protokoll): + oeffnet modernen VSA-KEK-2020-Dialog statt altem Katalog"
```

---

### Task 2: F2a — Transfer-Helfer `CodingEventColumnTransfer` (Move/Copy, testbar)

**Files:**
- Create: `src/AuswertungPro.Next.UI/Ai/CodingEventColumnTransfer.cs`
- Test: `tests/AuswertungPro.Next.UI.Tests/CodingEventColumnTransferTests.cs`

**Interfaces:**
- Consumes: `CodingEvent { Guid EventId; ProtocolEntry Entry; OverlayGeometry? Overlay; CodingEventAiContext? AiContext; double MeterAtCapture; TimeSpan VideoTimestamp }`, `ProtocolEntry { Guid EntryId; string Code; string Beschreibung; double? MeterStart/End; bool IsStreckenschaden; string? Mpeg; TimeSpan? Zeit; List<string> FotoPaths; ProtocolEntrySource Source; ProtocolEntryCodeMeta? CodeMeta }`.
- Produces:
  - `static CodingEvent Move(CodingEvent ev, ObservableCollection<CodingEvent> source, ObservableCollection<CodingEvent> target)`
  - `static CodingEvent Copy(CodingEvent ev, ObservableCollection<CodingEvent> target)`
  - `static CodingEvent CloneWithNewIds(CodingEvent ev)`

- [ ] **Step 1: Failing test schreiben**

`tests/AuswertungPro.Next.UI.Tests/CodingEventColumnTransferTests.cs`:

```csharp
using System.Collections.ObjectModel;
using System.Linq;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai;
using Xunit;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingEventColumnTransferTests
{
    private static CodingEvent Ev(string code, double meter)
        => new()
        {
            MeterAtCapture = meter,
            Entry = new ProtocolEntry { Code = code, Beschreibung = code + " Text", FotoPaths = { "a.png" } }
        };

    [Fact]
    public void Move_entfernt_aus_quelle_und_fuegt_sortiert_in_ziel()
    {
        var src = new ObservableCollection<CodingEvent> { Ev("BCD", 5) };
        var target = new ObservableCollection<CodingEvent> { Ev("BAB", 2), Ev("BBA", 9) };
        var moved = src[0];

        var result = CodingEventColumnTransfer.Move(moved, src, target);

        Assert.Same(moved, result);
        Assert.Empty(src);
        Assert.Equal(3, target.Count);
        Assert.Equal(new[] { 2.0, 5.0, 9.0 }, target.Select(e => e.MeterAtCapture)); // nach Meter sortiert
    }

    [Fact]
    public void Copy_laesst_original_und_dupliziert_mit_neuen_ids()
    {
        var src = new ObservableCollection<CodingEvent> { Ev("BCD", 5) };
        var target = new ObservableCollection<CodingEvent>();
        var original = src[0];

        var copy = CodingEventColumnTransfer.Copy(original, target);

        Assert.Single(src);                                   // Original bleibt
        Assert.Single(target);
        Assert.NotEqual(original.EventId, copy.EventId);       // neue EventId
        Assert.NotEqual(original.Entry.EntryId, copy.Entry.EntryId); // neue EntryId
        Assert.Equal("BCD", copy.Entry.Code);
        Assert.Equal(original.Entry.FotoPaths, copy.Entry.FotoPaths);
        Assert.NotSame(original.Entry.FotoPaths, copy.Entry.FotoPaths); // eigene Liste
    }
}
```

- [ ] **Step 2: Test läuft rot**

Run: `dotnet test tests/AuswertungPro.Next.UI.Tests/AuswertungPro.Next.UI.Tests.csproj --filter "FullyQualifiedName~CodingEventColumnTransfer"`
Expected: FAIL („CodingEventColumnTransfer" existiert nicht).

- [ ] **Step 3: Implementierung**

`src/AuswertungPro.Next.UI/Ai/CodingEventColumnTransfer.cs`:

```csharp
using System;
using System.Collections.ObjectModel;
using System.Linq;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.UI.Ai;

/// <summary>
/// Verschiebt bzw. kopiert eine Befund-Kachel (<see cref="CodingEvent"/>) zwischen den beiden
/// Spalten des Abgleich-Panels (KI-Befunde ↔ Import). Reine Datenoperation, unit-testbar.
/// Kopieren erzeugt einen Deep-Clone mit NEUER EventId/EntryId, damit sich Original und Kopie
/// nicht ueber gleiche IDs im Abgleich/Highlighting stoeren.
/// </summary>
public static class CodingEventColumnTransfer
{
    /// <summary>Verschiebt <paramref name="ev"/> aus <paramref name="source"/> nach
    /// <paramref name="target"/> (nach Meter einsortiert). Gibt das verschobene Event zurueck.</summary>
    public static CodingEvent Move(
        CodingEvent ev,
        ObservableCollection<CodingEvent> source,
        ObservableCollection<CodingEvent> target)
    {
        source.Remove(ev);
        InsertSorted(target, ev);
        return ev;
    }

    /// <summary>Fuegt einen Deep-Clone von <paramref name="ev"/> in <paramref name="target"/> ein
    /// (Original bleibt). Gibt den Clone zurueck.</summary>
    public static CodingEvent Copy(CodingEvent ev, ObservableCollection<CodingEvent> target)
    {
        var clone = CloneWithNewIds(ev);
        InsertSorted(target, clone);
        return clone;
    }

    /// <summary>Deep-Clone mit neuen IDs (Entry inkl. Fotos/CodeMeta, Overlay, AiContext).</summary>
    public static CodingEvent CloneWithNewIds(CodingEvent ev)
    {
        return new CodingEvent
        {
            EventId = Guid.NewGuid(),
            MeterAtCapture = ev.MeterAtCapture,
            VideoTimestamp = ev.VideoTimestamp,
            Entry = CloneEntry(ev.Entry),
            Overlay = CloneOverlay(ev.Overlay),
            AiContext = CloneAiContext(ev.AiContext),
        };
    }

    private static void InsertSorted(ObservableCollection<CodingEvent> target, CodingEvent ev)
    {
        var index = target.Count;
        for (var i = 0; i < target.Count; i++)
        {
            if (ev.MeterAtCapture < target[i].MeterAtCapture)
            {
                index = i;
                break;
            }
        }
        target.Insert(index, ev);
    }

    private static ProtocolEntry CloneEntry(ProtocolEntry e)
        => new()
        {
            EntryId = Guid.NewGuid(),
            Code = e.Code,
            Beschreibung = e.Beschreibung,
            MeterStart = e.MeterStart,
            MeterEnd = e.MeterEnd,
            IsStreckenschaden = e.IsStreckenschaden,
            Mpeg = e.Mpeg,
            Zeit = e.Zeit,
            FotoPaths = e.FotoPaths.ToList(),
            Source = e.Source,
            CodeMeta = CloneCodeMeta(e.CodeMeta),
        };

    private static ProtocolEntryCodeMeta? CloneCodeMeta(ProtocolEntryCodeMeta? m)
        => m is null ? null : new ProtocolEntryCodeMeta
        {
            Code = m.Code,
            Parameters = new System.Collections.Generic.Dictionary<string, string>(m.Parameters, StringComparer.OrdinalIgnoreCase),
            Severity = m.Severity,
            Count = m.Count,
            Notes = m.Notes,
            UpdatedAt = m.UpdatedAt,
        };

    private static OverlayGeometry? CloneOverlay(OverlayGeometry? o)
        => o is null ? null : new OverlayGeometry
        {
            GeometryId = Guid.NewGuid(),
            ToolType = o.ToolType,
            Points = o.Points.Select(p => new NormalizedPoint { X = p.X, Y = p.Y }).ToList(),
            Q1Mm = o.Q1Mm,
            Q2Mm = o.Q2Mm,
            ClockFrom = o.ClockFrom,
            ClockTo = o.ClockTo,
            ArcDegrees = o.ArcDegrees,
            DnRatioPercent = o.DnRatioPercent,
            FillPercent = o.FillPercent,
            LevelSubMode = o.LevelSubMode,
            EllipseRadiusXMm = o.EllipseRadiusXMm,
            EllipseRadiusYMm = o.EllipseRadiusYMm,
            SnapshotPath = o.SnapshotPath,
        };

    private static CodingEventAiContext? CloneAiContext(CodingEventAiContext? a)
        => a is null ? null : new CodingEventAiContext
        {
            SuggestedCode = a.SuggestedCode,
            Confidence = a.Confidence,
            Reason = a.Reason,
            Decision = a.Decision,
            QualityGateLevel = a.QualityGateLevel,
            SamMaskRle = a.SamMaskRle,
            SamMaskImageWidth = a.SamMaskImageWidth,
            SamMaskImageHeight = a.SamMaskImageHeight,
        };
}
```

> **Achtung `OverlayGeometry`-Felder:** Vor dem Schreiben von `CloneOverlay` die tatsächlichen Property-Namen in `src/AuswertungPro.Next.Domain/Models/CodingSession.cs` (Z. 57–92) prüfen und 1:1 übernehmen; nur existierende Properties setzen (Rest weglassen). Der Test deckt `CloneOverlay` nicht ab — Compilerfehler = fehlende/falsche Property.

- [ ] **Step 4: Test läuft grün**

Run: `dotnet test tests/AuswertungPro.Next.UI.Tests/AuswertungPro.Next.UI.Tests.csproj --filter "FullyQualifiedName~CodingEventColumnTransfer"`
Expected: PASS (2 Tests).

- [ ] **Step 5: Commit**

```bash
git add src/AuswertungPro.Next.UI/Ai/CodingEventColumnTransfer.cs tests/AuswertungPro.Next.UI.Tests/CodingEventColumnTransferTests.cs
git commit -m "feat(codieren): CodingEventColumnTransfer (Move/Copy) fuer Abgleich-Spalten"
```

---

### Task 3: F2a — Drag&Drop-Behavior + Verdrahtung im Codier-Panel

**Files:**
- Create: `src/AuswertungPro.Next.UI/Behaviors/CodingEventDragDropBehavior.cs`
- Modify: `src/AuswertungPro.Next.UI/Views/Windows/PlayerCodingSidePanel.xaml` (Behavior auf `LstCodingEvents` + `LstImportEvents` aktivieren)
- Modify: `src/AuswertungPro.Next.UI/Views/Windows/PlayerWindow.Coding.Lifecycle.ImportReference.cs` (Behavior mit den beiden Ziel-Collections + Nach-Transfer-Callback verdrahten — die Datei, die `InitializeCodingImportReferences()` hält; falls dort kein passender Init-Punkt, im Konstruktor-Wiring von `PlayerWindow.xaml.cs`)

**Interfaces:**
- Consumes: `CodingEventColumnTransfer.Move/Copy` (Task 2); `_codingSessionHost.EventCollection` (KI, `ObservableCollection<CodingEvent>`), `_codingImportReferenceEvents.Events` (Import); Removal-aus-Session-Pfad (siehe Step 3-Hinweis).
- Produces: Attached-Behavior mit `SetOtherColumn(ListBox, Func<ObservableCollection<CodingEvent>>)`, `SetOnAfterTransfer(ListBox, Action)`, `SetOnRemovedFromKi(ListBox, Action<CodingEvent>)`, `SetIsKiColumn(ListBox, bool)` und `SetEnabled(ListBox, bool)`.

- [ ] **Step 1: Behavior anlegen**

`src/AuswertungPro.Next.UI/Behaviors/CodingEventDragDropBehavior.cs`:

```csharp
using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Behaviors;

/// <summary>
/// Drag&Drop von Befund-Kacheln (<see cref="CodingEvent"/>) zwischen den zwei Spalten des
/// Abgleich-Panels. Ziehen = Verschieben, Strg+Ziehen = Kopieren (Windows-Standard).
/// Beide ListBoxen (KI links, Import rechts) erhalten dieses Behavior; jede kennt ihre
/// „andere" Spalte als Ziel. Reines UI — die eigentliche Datenoperation macht
/// <see cref="CodingEventColumnTransfer"/>.
/// </summary>
public static class CodingEventDragDropBehavior
{
    private const string Format = "SewerStudio.CodingEvent";
    private static Point _dragStart;

    public static readonly DependencyProperty EnabledProperty = DependencyProperty.RegisterAttached(
        "Enabled", typeof(bool), typeof(CodingEventDragDropBehavior),
        new PropertyMetadata(false, OnEnabledChanged));
    public static void SetEnabled(DependencyObject d, bool v) => d.SetValue(EnabledProperty, v);
    public static bool GetEnabled(DependencyObject d) => (bool)d.GetValue(EnabledProperty);

    // Quell-Collection dieser Liste (zum Entfernen beim Move).
    public static readonly DependencyProperty SourceColumnProperty = DependencyProperty.RegisterAttached(
        "SourceColumn", typeof(Func<ObservableCollection<CodingEvent>>), typeof(CodingEventDragDropBehavior),
        new PropertyMetadata(null));
    public static void SetSourceColumn(DependencyObject d, Func<ObservableCollection<CodingEvent>>? v) => d.SetValue(SourceColumnProperty, v);
    public static Func<ObservableCollection<CodingEvent>>? GetSourceColumn(DependencyObject d) => (Func<ObservableCollection<CodingEvent>>?)d.GetValue(SourceColumnProperty);

    // Ist diese Liste die KI-Spalte? (fuer Session-Entfernung/-Zugabe)
    public static readonly DependencyProperty IsKiColumnProperty = DependencyProperty.RegisterAttached(
        "IsKiColumn", typeof(bool), typeof(CodingEventDragDropBehavior), new PropertyMetadata(false));
    public static void SetIsKiColumn(DependencyObject d, bool v) => d.SetValue(IsKiColumnProperty, v);
    public static bool GetIsKiColumn(DependencyObject d) => (bool)d.GetValue(IsKiColumnProperty);

    // Callbacks (vom Code-behind gesetzt).
    public static readonly DependencyProperty OnAfterTransferProperty = DependencyProperty.RegisterAttached(
        "OnAfterTransfer", typeof(Action), typeof(CodingEventDragDropBehavior), new PropertyMetadata(null));
    public static void SetOnAfterTransfer(DependencyObject d, Action? v) => d.SetValue(OnAfterTransferProperty, v);
    public static Action? GetOnAfterTransfer(DependencyObject d) => (Action?)d.GetValue(OnAfterTransferProperty);

    public static readonly DependencyProperty OnRemovedFromKiProperty = DependencyProperty.RegisterAttached(
        "OnRemovedFromKi", typeof(Action<CodingEvent>), typeof(CodingEventDragDropBehavior), new PropertyMetadata(null));
    public static void SetOnRemovedFromKi(DependencyObject d, Action<CodingEvent>? v) => d.SetValue(OnRemovedFromKiProperty, v);
    public static Action<CodingEvent>? GetOnRemovedFromKi(DependencyObject d) => (Action<CodingEvent>?)d.GetValue(OnRemovedFromKiProperty);

    public static readonly DependencyProperty OnAddedToKiProperty = DependencyProperty.RegisterAttached(
        "OnAddedToKi", typeof(Action<CodingEvent>), typeof(CodingEventDragDropBehavior), new PropertyMetadata(null));
    public static void SetOnAddedToKi(DependencyObject d, Action<CodingEvent>? v) => d.SetValue(OnAddedToKiProperty, v);
    public static Action<CodingEvent>? GetOnAddedToKi(DependencyObject d) => (Action<CodingEvent>?)d.GetValue(OnAddedToKiProperty);

    private static void OnEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ListBox list) return;
        if ((bool)e.NewValue)
        {
            list.PreviewMouseLeftButtonDown += OnMouseDown;
            list.PreviewMouseMove += OnMouseMove;
            list.AllowDrop = true;
            list.DragOver += OnDragOver;
            list.Drop += OnDrop;
        }
        else
        {
            list.PreviewMouseLeftButtonDown -= OnMouseDown;
            list.PreviewMouseMove -= OnMouseMove;
            list.DragOver -= OnDragOver;
            list.Drop -= OnDrop;
        }
    }

    private static void OnMouseDown(object sender, MouseButtonEventArgs e) => _dragStart = e.GetPosition(null);

    private static void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed) return;
        var pos = e.GetPosition(null);
        if (Math.Abs(pos.X - _dragStart.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(pos.Y - _dragStart.Y) < SystemParameters.MinimumVerticalDragDistance) return;

        if (sender is not ListBox list) return;
        if (ItemFromPoint(list, e.GetPosition(list)) is not CodingEvent ev) return;

        var data = new DataObject(Format, ev);
        DragDrop.DoDragDrop(list, data, DragDropEffects.Move | DragDropEffects.Copy);
    }

    private static void OnDragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(Format)
            ? ((e.KeyStates & DragDropKeyStates.ControlKey) != 0 ? DragDropEffects.Copy : DragDropEffects.Move)
            : DragDropEffects.None;
        e.Handled = true;
    }

    private static void OnDrop(object sender, DragEventArgs e)
    {
        if (sender is not ListBox targetList) return;
        if (e.Data.GetData(Format) is not CodingEvent ev) return;

        var targetCol = GetSourceColumn(targetList)?.Invoke();
        if (targetCol is null || targetCol.Contains(ev)) return; // gleiche Spalte -> nichts

        var isCopy = (e.KeyStates & DragDropKeyStates.ControlKey) != 0;
        var targetIsKi = GetIsKiColumn(targetList);

        if (isCopy)
        {
            var clone = CodingEventColumnTransfer.Copy(ev, targetCol);
            if (targetIsKi) GetOnAddedToKi(targetList)?.Invoke(clone);
        }
        else
        {
            // Quell-Collection = die „andere" Spalte. Wir kennen sie ueber das Ziel nicht direkt;
            // darum die Quelle aus dem DragSource ermitteln: sie ist die Liste, in der ev noch liegt.
            var sourceList = e.OriginalSource as DependencyObject;
            var sourceCol = FindSourceColumnContaining(ev, targetList);
            if (sourceCol is null) return;
            var wasKi = !targetIsKi; // genau zwei Spalten
            CodingEventColumnTransfer.Move(ev, sourceCol, targetCol);
            if (wasKi) GetOnRemovedFromKi(targetList)?.Invoke(ev);
            if (targetIsKi) GetOnAddedToKi(targetList)?.Invoke(ev);
        }

        GetOnAfterTransfer(targetList)?.Invoke();
        e.Handled = true;
    }

    // Da es genau zwei Spalten gibt, ist die Quelle die „andere" — wird ueber den vom
    // Code-behind gesetzten OtherColumn-Resolver am Ziel gefunden.
    public static readonly DependencyProperty OtherColumnProperty = DependencyProperty.RegisterAttached(
        "OtherColumn", typeof(Func<ObservableCollection<CodingEvent>>), typeof(CodingEventDragDropBehavior),
        new PropertyMetadata(null));
    public static void SetOtherColumn(DependencyObject d, Func<ObservableCollection<CodingEvent>>? v) => d.SetValue(OtherColumnProperty, v);
    public static Func<ObservableCollection<CodingEvent>>? GetOtherColumn(DependencyObject d) => (Func<ObservableCollection<CodingEvent>>?)d.GetValue(OtherColumnProperty);

    private static ObservableCollection<CodingEvent>? FindSourceColumnContaining(CodingEvent ev, ListBox targetList)
    {
        var other = GetOtherColumn(targetList)?.Invoke();
        return other is not null && other.Contains(ev) ? other : null;
    }

    private static object? ItemFromPoint(ListBox list, Point p)
    {
        if (list.InputHitTest(p) is not DependencyObject d) return null;
        while (d is not null && d is not ListBoxItem) d = VisualTreeHelper.GetParent(d);
        return (d as ListBoxItem)?.DataContext;
    }
}
```

> Hinweis: `SourceColumn` am Ziel = die **eigene** Collection des Ziels; `OtherColumn` am Ziel = die Quelle. Beim Verdrahten (Step 3) für jede Liste beides setzen.

- [ ] **Step 2: XAML aktivieren**

In `PlayerCodingSidePanel.xaml` am `LstCodingEvents` (bei Z. ~109–120) und `LstImportEvents` (Z. ~332–342) ergänzen (Namespace `behaviors:` ist bereits importiert, da `PhotoHoverPreviewBehavior` genutzt wird):

```xml
behaviors:CodingEventDragDropBehavior.Enabled="True"
```

- [ ] **Step 3: Ziel-Collections + Callbacks im Code-behind setzen**

In der Init-Methode, die die Import-Referenz aufbaut (`InitializeCodingImportReferences()` in `PlayerWindow.Coding.Lifecycle.ImportReference.cs`), nach dem die beiden Collections stehen, ergänzen:

```csharp
// KI-Spalte (links)
Behaviors.CodingEventDragDropBehavior.SetIsKiColumn(LstCodingEvents, true);
Behaviors.CodingEventDragDropBehavior.SetSourceColumn(LstCodingEvents, () => _codingSessionHost.EventCollection);
Behaviors.CodingEventDragDropBehavior.SetOtherColumn(LstCodingEvents, () => _codingImportReferenceEvents.Events);
// Import-Spalte (rechts)
Behaviors.CodingEventDragDropBehavior.SetIsKiColumn(LstImportEvents, false);
Behaviors.CodingEventDragDropBehavior.SetSourceColumn(LstImportEvents, () => _codingImportReferenceEvents.Events);
Behaviors.CodingEventDragDropBehavior.SetOtherColumn(LstImportEvents, () => _codingSessionHost.EventCollection);

Action afterTransfer = () =>
{
    RefreshCodingEventsList();
    RunCodingProtocolMatch();
};
Behaviors.CodingEventDragDropBehavior.SetOnAfterTransfer(LstCodingEvents, afterTransfer);
Behaviors.CodingEventDragDropBehavior.SetOnAfterTransfer(LstImportEvents, afterTransfer);

// Aus KI heraus verschoben: sauber aus der Session entfernen (kein Rueckbluten ins Protokoll).
Action<CodingEvent> removedFromKi = ev => _codingSessionRuntimeOwner.Service?.RemoveEvent(ev);
Behaviors.CodingEventDragDropBehavior.SetOnRemovedFromKi(LstImportEvents, removedFromKi);
// In KI hinein: als offener KI-Befund registrieren (noch unbestaetigt).
Action<CodingEvent> addedToKi = ev => _codingSessionRuntimeOwner.Service?.AddExistingEvent(ev);
Behaviors.CodingEventDragDropBehavior.SetOnAddedToKi(LstCodingEvents, addedToKi);
```

> **Verifizieren vor Umsetzung:** exakte Methodennamen der Session prüfen — `_codingSessionRuntimeOwner.Service` ist der `ICodingSessionService`. Für das Entfernen aus der Session den Pfad aus `RejectDefect`/`CodingInlineDefectDecisionWorkflow.Reject` verwenden (dort wird aus VM **und** Session entfernt), aber **ohne** Reject-Markierung — d.h. eine reine „Remove"-Methode. Existiert keine, im gleichen Muster wie „Löschen" (Kontextmenü der linken Liste) entfernen. Für „In KI hinein" analog den Pfad nutzen, über den `CodingSessionService.EventAdded` die VM-Collection füllt (VM Z. 74–79) — die Kachel muss in **beiden** landen (VM.Events sichtbar + Session), sonst ist sie beim Speichern weg. Falls keine passende Add-API existiert, `addedToKi` weglassen und die Kachel liegt nur in `EventCollection` (Anzeige) — dann in der manuellen Verifikation prüfen, ob „Bestätigen" trotzdem greift, sonst Add-API ergänzen (separate Rückfrage).

- [ ] **Step 4: Build**

Run: `dotnet build src/AuswertungPro.Next.UI/AuswertungPro.Next.UI.csproj --nologo -v q`
Expected: `0 Fehler`.

- [ ] **Step 5: Manuelle Verifikation**

Player → Codier-Modus → Abgleich. Kachel von links nach rechts ziehen → wandert; von rechts nach links → wandert und wird links „offen"; Strg+Ziehen → Duplikat, Original bleibt. Badges/Zähler nach dem Ziehen aktuell.

- [ ] **Step 6: Commit**

```bash
git add src/AuswertungPro.Next.UI/Behaviors/CodingEventDragDropBehavior.cs src/AuswertungPro.Next.UI/Views/Windows/PlayerCodingSidePanel.xaml src/AuswertungPro.Next.UI/Views/Windows/PlayerWindow.Coding.Lifecycle.ImportReference.cs
git commit -m "feat(abgleich): Drag&Drop von Kacheln zwischen KI und Import (Move/Strg=Copy)"
```

---

### Task 4: F2b — Rechte Spalte: „Fotos anzeigen"

**Files:**
- Modify: `src/AuswertungPro.Next.UI/Views/Windows/PlayerCodingSidePanel.xaml` (Kontextmenü `LstImportEvents`, Z. ~343–348: MenuItem)
- Modify: `src/AuswertungPro.Next.UI/Views/Windows/PlayerCodingSidePanel.xaml.cs` (Relay-Event `ImportShowPhotosRequested` + `*_Click`)
- Modify: `src/AuswertungPro.Next.UI/Views/Windows/PlayerCodingSidePanelEventBinder.cs` (Handler-Record-Feld + Bind)
- Modify: `src/AuswertungPro.Next.UI/Views/Windows/PlayerWindow.CodingSidePanelAccessors.cs` (Zuweisung)
- Modify: `src/AuswertungPro.Next.UI/Views/Windows/PlayerWindow.Coding.Photos.Viewer.cs` (Host-Handler)

**Interfaces:**
- Consumes: `CodingPhotoViewerCommandWorkflow.Execute(new CodingPhotoViewerCommandRequest(item), actions)`, `CodingPhotoViewerDisplayWorkflow.Show(this, ev, _protocolContext.LastProjectPath)`.
- Produces: `PlayerCodingSidePanel.ImportShowPhotosRequested` (Event), Host-Methode `ImportShowPhotos_Click(object,RoutedEventArgs)`.

Muster: 1:1 wie die bestehende linke „Fotos anzeigen"-Kette (`CodingEventShowPhotos_Click`) und der bestehende rechte „Zum Zeitpunkt springen"-Eintrag (`ImportSeek`). Für alle vier Wiring-Stellen gilt: dem `ImportSeek`-Vorbild folgen und `LstImportEvents.SelectedItem` verwenden.

- [ ] **Step 1: XAML — MenuItem im rechten Kontextmenü ergänzen**

Im `ContextMenu` von `LstImportEvents` (bei „Zum Zeitpunkt springen"):
```xml
<MenuItem Header="Fotos anzeigen" Click="ImportShowPhotos_Click"/>
```

- [ ] **Step 2: UserControl-Relay** — in `PlayerCodingSidePanel.xaml.cs` analog zu den vorhandenen Import-Events (`ImportSeekRequested` etc.):
```csharp
public event System.EventHandler? ImportShowPhotosRequested;
private void ImportShowPhotos_Click(object sender, System.Windows.RoutedEventArgs e)
    => ImportShowPhotosRequested?.Invoke(this, System.EventArgs.Empty);
```

- [ ] **Step 3: Binder** — in `PlayerCodingSidePanelEventBinder.cs` das Handler-Record um ein Feld `EventHandler? ImportShowPhotos` erweitern und in `Bind(...)` ergänzen:
```csharp
panel.ImportShowPhotosRequested += handlers.ImportShowPhotos;
```

- [ ] **Step 4: Host-Handler** — in `PlayerWindow.Coding.Photos.Viewer.cs` ergänzen:
```csharp
private void ImportShowPhotos_Click(object sender, RoutedEventArgs e)
{
    CodingPhotoViewerCommandWorkflow.Execute(
        new CodingPhotoViewerCommandRequest(LstImportEvents.SelectedItem),
        new CodingPhotoViewerCommandActions(
            ShowNoPhotosOverlay: () => ShowOverlay("Keine Fotos vorhanden.", TimeSpan.FromSeconds(3)),
            ShowViewer: codingEvent => CodingPhotoViewerDisplayWorkflow.Show(
                this, codingEvent, _protocolContext.LastProjectPath)));
}
```

- [ ] **Step 5: Zuweisung** — in `PlayerWindow.CodingSidePanelAccessors.cs` `WireCodingSidePanelEvents()` bei den Import-Zuweisungen ergänzen: `ImportShowPhotos = ImportShowPhotos_Click`.

- [ ] **Step 6: Build + manuelle Verifikation + Commit**

Run: `dotnet build src/AuswertungPro.Next.UI/AuswertungPro.Next.UI.csproj --nologo -v q` → `0 Fehler`.
Verifikation: Rechtsklick auf Import-Kachel → „Fotos anzeigen" öffnet den Foto-Viewer.
```bash
git add -A && git commit -m "feat(abgleich): rechte Spalte 'Fotos anzeigen'"
```

---

### Task 5: F2b — Rechte Spalte: „Bearbeiten / BBox ziehen"

**Files:** dieselben vier Wiring-Dateien wie Task 4 (Event `ImportEditRequested`, Handler `ImportEdit_Click`) + Host-Handler im passenden Partial (`PlayerWindow.Coding.Events.Actions.cs`, wo `TryEditCodingEvent` liegt).

**Interfaces:**
- Consumes: die bestehende Edit-Kette der linken Liste — `TryEditCodingEvent(CodingEvent)` bzw. `CodingCodeExplorerEditWorkflow.Execute(...)`, die den modernen `VsaCodeExplorerWindow` mit PhotoAssistant/BBox öffnet.

- [ ] **Step 1: XAML** — MenuItem „Bearbeiten" ins rechte Kontextmenü:
```xml
<MenuItem Header="Bearbeiten (Code / BBox ziehen)" Click="ImportEdit_Click"/>
```

- [ ] **Step 2–5: Relay/Binder/Zuweisung** analog Task 4 (Event `ImportEditRequested`, `ImportEdit_Click`), Host-Handler:
```csharp
private void ImportEdit_Click(object sender, RoutedEventArgs e)
{
    if (LstImportEvents.SelectedItem is AuswertungPro.Next.Domain.Models.CodingEvent ev)
        TryEditCodingEvent(ev);   // exakte Signatur in PlayerWindow.Coding.Events.Actions.cs pruefen
}
```
> Prüfen: heißt die Methode `TryEditCodingEvent(CodingEvent)` und arbeitet sie auf dem übergebenen Event (nicht fest auf `LstCodingEvents.SelectedItem`)? Falls sie fest die linke Selektion nimmt, stattdessen direkt `CodingCodeExplorerEditWorkflow.Execute(...)` mit `ev` aufrufen (Signatur aus `CodingCodeExplorerEditWorkflow.cs`). Nach dem Edit `RunCodingProtocolMatch()` aufrufen.

- [ ] **Step 6: Build + Verifikation (Rechtsklick → Bearbeiten → moderner Dialog → PhotoAssistant → Rechteck ziehen → Übernehmen) + Commit**
```bash
git add -A && git commit -m "feat(abgleich): rechte Spalte 'Bearbeiten' (Code + BBox ziehen)"
```

---

### Task 6: F2b — Rechte Spalte: „Bestätigen → ins KI-Brain"

**Files:** dieselben vier Wiring-Dateien (Event `ImportConfirmToBrainRequested`, Handler `ImportConfirmToBrain_Click`) + Host-Handler in `PlayerWindow.Coding.ProtocolMatch.Training.cs` (neben `ImportConfirm_Click`).

**Interfaces:**
- Consumes: `CodingEventDecisionPolicy.ApplyManualReviewDecision(CodingEvent, CodingUserDecision, string)`, `PersistSingleEventAsTrainingSample(CodingEvent)` (existiert auf `PlayerWindow`, indexiert bei Status=Approved in `knowledge_base.db`).

- [ ] **Step 1: XAML** — MenuItem ins rechte Kontextmenü:
```xml
<MenuItem Header="✓ Bestätigen → ins KI-Brain" Click="ImportConfirmToBrain_Click"/>
```

- [ ] **Step 2–5: Relay/Binder/Zuweisung** analog Task 4 (Event `ImportConfirmToBrainRequested`), Host-Handler in `PlayerWindow.Coding.ProtocolMatch.Training.cs`:
```csharp
private void ImportConfirmToBrain_Click(object sender, RoutedEventArgs e)
    => HandleImportConfirmToBrainAsync().SafeFireAndForget("ImportConfirmToBrain");

private async System.Threading.Tasks.Task HandleImportConfirmToBrainAsync()
{
    if (LstImportEvents.SelectedItem is not CodingEvent importEvent) return;
    if (string.IsNullOrWhiteSpace(importEvent.Entry.Code))
    {
        ShowOverlay("Kein VSA-Code — bitte zuerst 'Bearbeiten'.", TimeSpan.FromSeconds(3));
        return;
    }
    // Entscheidung setzen -> Mapper vergibt Status=Approved -> KB-Index feuert.
    CodingEventDecisionPolicy.ApplyManualReviewDecision(
        importEvent, CodingUserDecision.Accepted, "Import bestaetigt (ins Brain)");
    await PersistSingleEventAsTrainingSample(importEvent);
    ShowOverlay("Ins KI-Brain uebernommen.", TimeSpan.FromSeconds(2));
    RunCodingProtocolMatch();
}
```
> `CodingUserDecision`/`CodingEventDecisionPolicy` liegen in `AuswertungPro.Next.Domain.Models` bzw. `AuswertungPro.Next.UI.Ai` — Usings ergänzen. Die bestehende rechte Aktion „Bestätigen (als Training übernehmen)" (`ImportConfirm_Click`, TeacherAnnotation) bleibt daneben bestehen.

- [ ] **Step 6: Build + Verifikation + Commit**

Verifikation: Import-Kachel mit gültigem Code + Beschreibung ≥10 Zeichen → Rechtsklick → „Bestätigen → ins KI-Brain" → Overlay „übernommen". Danach prüfen, dass in `training_samples.json` ein Approved-Sample steht und (bei erreichbarem Ollama) `knowledge_base.db` einen neuen Eintrag hat (via `sqlite-kb-inspector` / KbInspector-Tool).
```bash
git add -A && git commit -m "feat(abgleich): rechte Spalte 'Bestaetigen -> ins KI-Brain' (KnowledgeBase-Index)"
```

---

## Self-Review (durchgeführt)
- **Spec-Abdeckung:** F1 → Task 1; F2a DnD → Task 2 (Transfer) + Task 3 (Behavior); F2b Foto → Task 4, BBox/Edit → Task 5, Bestätigen→Brain → Task 6. Alle Spec-Punkte abgedeckt.
- **Platzhalter:** Keine „TBD"/„TODO". Die drei „Verifizieren-vor-Umsetzung"-Hinweise (OverlayGeometry-Properties, Session-Remove/Add-API, `TryEditCodingEvent`-Signatur) sind bewusste Präzisierungs-Checks, keine offenen Lücken — sie nennen die genaue Datei/Zeile und die Fallback-Strategie.
- **Typ-Konsistenz:** `CodingEventColumnTransfer.Move/Copy/CloneWithNewIds` (Task 2) wird in Task 3 exakt so aufgerufen. `CopyEditableValues(source, target)`-Reihenfolge in Task 1 stimmt mit der Definition (`source→target`).

## Offene Klärungen für die Umsetzung (nicht blockierend)
1. Exakte Session-API zum Entfernen/Hinzufügen von KI-Events (Task 3, Step 3) — im Code bestätigen (Muster: `RejectDefect`/„Löschen").
2. `TryEditCodingEvent`-Signatur (Task 5) — arbeitet sie auf einem übergebenen Event?
Beide sind lokal im Codier-Partial verifizierbar; bei Abweichung greift die im Task genannte Fallback-Strategie.
