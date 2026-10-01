# Design-Audit Pakete M2–M4: Feedback, Bedienbarkeit, Einstellungssuche — Umsetzungsplan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Die drei offenen Pakete des Design-Audits vom 2026-09-03 umsetzen: spuerbares Erfolgs-Feedback (Toast mit „Ordner oeffnen"-Link, Hover-Lift, gestaffeltes Einblenden), Bedienbarkeit (vorlesbare Namen fuer Icon-Knoepfe, Tastenkuerzel in den Player-Tooltips) und eine Suche in den Einstellungen.

**Architecture:** Alles additiv auf dem bestehenden Fundament (`IToastService`/`ToastHost`, `HoverFx`, `EntranceFx`, `SettingsPage`). Reine Logik (Toast-Warteschlange, Suchabgleich) bleibt UI-frei und testbar; XAML-Regeln werden wie bei den vorigen Paketen durch Waechtertests unter `tests/AuswertungPro.Next.UI.Tests/DesignAudit*` festgehalten. Kein neuer Dienst im `ServiceProvider` (die Registrierungszahl 155 bleibt).

**Tech Stack:** WPF / .NET 10, CommunityToolkit.Mvvm, xUnit. Build und Tests wie in CLAUDE.md.

**Spec:** `docs/DESIGN-AUDIT-2026-09-03.md`, Abschnitte 3.6 (Animationen/Feedback), 3.7 (Bedienbarkeit) und Massnahmenplan M2, M3, M4. Ist-Zahlen vom 2026-09-04: `IToastService` hat 4 Methoden ohne Aktion; 24 Icon-Knoepfe ohne `AutomationProperties.Name` (3 davon ohne Tooltip); `PlayerKeyboardShortcutPolicy` kennt 10 Tasten, keine steht im Tooltip; `SettingsPage.xaml` hat 6 Reiter, 17 `GroupBox`-Gruppen, keine Suche; `HoverFx.Lift` 1 Stelle, `EntranceFx.Stagger` 2 Stellen.

## Global Constraints

- **TDD ohne Ausnahme:** Jeder Test wird zuerst rot gesehen, dann kommt der Code. Testlauf fuer UI-Tests: `dotnet test tests/AuswertungPro.Next.UI.Tests/AuswertungPro.Next.UI.Tests.csproj -o .tmp/testout-design --nologo -v q --filter "FullyQualifiedName~<Klasse>"`. Der Schalter `-o .tmp/testout-design` umgeht die gesperrten DLLs unter `bin\Debug`, falls SewerStudio laeuft.
- **Sichtbare Texte mit echten Umlauten** (`ö`, `ä`, `ü`), Schweizer `ss` ohne `ß`; Quellcode-Kommentare mit `ae/oe/ue`. Waechter: `DesignAuditFeinschliffTests`.
- **Symbole nur als `ui:FluentIcon`**, nie als Textzeichen; Farben nur als `{DynamicResource …Brush}`; Schriftgroessen nur als `{DynamicResource TextXS|TextS|TextM|TextL|TextXL|TextTitle|TextDisplay}`; Rundungen nur als `{DynamicResource RadiusS|M|L|XL|XXL|Pill}`. Waechter: `DesignAuditFeinschliffTests`, `DesignAuditSchriftskalaTests`, `DesignAuditFensterUndRundungenTests`.
- **Keine neuen NuGet-Pakete.** Kein `MessageBox.Show`. Kein neues Fenster.
- **Wartbarkeitsgrenzen:** keine Produktionsdatei ueber 1000 Zeilen, keine Partial-Klasse ueber 2000 Zeilen (`MaintainabilityFitnessTests`). `ExportPageViewModel.cs` steht bei 866 Zeilen, `DossierPreviewFieldPanel` bei 1999.
- **`Margin`/`Padding` nicht anfassen** (Entscheid Audit: kein Blindflug ohne Sichtpruefung).
- **Commit je Task**, deutsche Commit-Botschaft, Abschluss `Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>`. Vor `git add` pruefen, dass nur eigene Dateien im Working Tree liegen (`git status --short`).
- **Kein Screenshot-Beweis moeglich:** Am Ende jedes Tasks im Bericht klar sagen, was nur per Test und was nicht am Bildschirm geprueft ist.

---

## Dateiuebersicht

| Datei | Verantwortung | Task |
|---|---|---|
| `src/AuswertungPro.Next.UI/Services/IToastService.cs` | Vertrag; neue Standardmethode `Success(message, aktionText, aktion)` | 1 |
| `src/AuswertungPro.Next.UI/Services/ToastQueueLogic.cs` | `ToastItem` mit Aktion; `Show`-Ueberladung | 1 |
| `src/AuswertungPro.Next.UI/Services/ToastService.cs` | reicht Aktion an die Senke weiter | 1 |
| `src/AuswertungPro.Next.UI/Controls/ToastHost.xaml(.cs)` | Link-Knopf im Toast, `Enqueue`-Ueberladung | 1 |
| `src/AuswertungPro.Next.UI/MainWindow.xaml.cs:30` | Senke mit vier Parametern | 1 |
| `tests/AuswertungPro.Next.UI.Tests/ToastAktionTests.cs` (neu) | Logik + XAML-Waechter fuer Toast-Aktion | 1 |
| `src/AuswertungPro.Next.UI/ViewModels/Pages/ExportPageViewModel.ExcelExport.cs:51,125` | Excel-Toasts mit „Ordner oeffnen" | 2 |
| `src/AuswertungPro.Next.UI/ViewModels/Pages/ExportPageViewModel.Xtf.cs` (`Uebernimm`) | XTF-Toast mit „Ordner oeffnen" | 2 |
| `src/AuswertungPro.Next.UI/Services/ImportReportNavigationController.cs` | Import-Toast mit „Bericht oeffnen" | 2 |
| `tests/AuswertungPro.Next.UI.Tests/ExportPageXtfAuswahlTests.cs` | Toast-Aktion nach XTF-Schreiben | 2 |
| `tests/AuswertungPro.Next.UI.Tests/ImportReportToastTests.cs` (neu) | Toast beim Berichtsablegen | 2 |
| `src/AuswertungPro.Next.UI/Controls/PhotoGalleryPanel.xaml:59` | `HoverFx.Lift` auf Fotokarten | 3 |
| `src/AuswertungPro.Next.UI/Views/Pages/DossiersPage.xaml:36` | `EntranceFx.Stagger` auf dem Cockpit | 3 |
| `tests/AuswertungPro.Next.UI.Tests/DesignAuditFeedbackTests.cs` (neu) | Waechter fuer Lift/Stagger | 3 |
| 8 XAML-Dateien mit 24 Icon-Knoepfen (Liste in Task 4) | `AutomationProperties.Name` | 4 |
| `tests/AuswertungPro.Next.UI.Tests/DesignAuditAccessibilityTests.cs` | Waechter „Icon-Knopf hat Namen" | 4 |
| `src/AuswertungPro.Next.UI/Views/Windows/PlayerWindow.xaml` | Tastenkuerzel in Tooltips | 5 |
| `tests/AuswertungPro.Next.UI.Tests/DesignAuditPlayerShortcutTests.cs` (neu) | Waechter Tooltip ↔ `PlayerKeyboardShortcutPolicy` | 5 |
| `src/AuswertungPro.Next.UI/Settings/SettingsSearchMatcher.cs` (neu) | reiner Textabgleich (Umlaut-tolerant, UND-Verknuepfung) | 6 |
| `src/AuswertungPro.Next.UI/Settings/SettingsSearchController.cs` (neu) | blendet Gruppen aus, waehlt Reiter | 6 |
| `src/AuswertungPro.Next.UI/Views/Pages/SettingsPage.xaml(.cs)` | Suchfeld im Kopf, Verdrahtung | 6 |
| `tests/AuswertungPro.Next.UI.Tests/SettingsSearchTests.cs` (neu) | Matcher, Controller (STA), XAML-Waechter | 6 |
| `CLAUDE.md`, `docs/DESIGN-AUDIT-2026-09-03.md` | Regeln + Status | 7 |

---

### Task 1: Toast mit Aktion („Ordner oeffnen"-Link)

**Files:**
- Modify: `src/AuswertungPro.Next.UI/Services/IToastService.cs`
- Modify: `src/AuswertungPro.Next.UI/Services/ToastQueueLogic.cs:17-27,68`
- Modify: `src/AuswertungPro.Next.UI/Services/ToastService.cs`
- Modify: `src/AuswertungPro.Next.UI/Controls/ToastHost.xaml:44-46` (TextBlock der Meldung), `ToastHost.xaml.cs:38-50`
- Modify: `src/AuswertungPro.Next.UI/MainWindow.xaml.cs:30`
- Test: `tests/AuswertungPro.Next.UI.Tests/ToastAktionTests.cs` (neu)

**Interfaces:**
- Consumes: `ToastQueueLogic.Show(string message, ToastSeverity severity, long nowMs)` (bestehend), `ToastItem(long Id, string Message, ToastSeverity Severity)` (bestehend, positional record).
- Produces: `IToastService.Success(string message, string aktionText, Action aktion)` (Standardmethode — bestehende Fakes in Tests kompilieren weiter), `ToastItem.AktionText`, `ToastItem.Aktion`, `ToastItem.HatAktion`, `ToastQueueLogic.Show(string, ToastSeverity, long, string? aktionText, Action? aktion)`, `ToastHost.Enqueue(string, ToastSeverity, string? aktionText, Action? aktion)`, `ToastService.AttachSink(Action<string, ToastSeverity, string?, Action?>)`.

- [ ] **Step 1: Failing test schreiben**

```csharp
// tests/AuswertungPro.Next.UI.Tests/ToastAktionTests.cs
using System.IO;
using AuswertungPro.Next.UI.Services;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Ein Erfolgs-Toast darf einen Link tragen ("Ordner öffnen"), der die Aktion ausloest.
/// Ohne Link verhaelt sich alles wie bisher.
/// </summary>
public sealed class ToastAktionTests
{
    [Fact]
    public void Show_mit_Aktion_traegt_Text_und_Aktion_am_sichtbaren_Toast()
    {
        var logik = new ToastQueueLogic();
        var ausgeloest = false;

        logik.Show("Haltungen exportiert", ToastSeverity.Success, nowMs: 0, aktionText: "Ordner öffnen", aktion: () => ausgeloest = true);

        var item = Assert.Single(logik.Visible);
        Assert.Equal("Ordner öffnen", item.AktionText);
        Assert.True(item.HatAktion);
        item.Aktion!();
        Assert.True(ausgeloest);
    }

    [Fact]
    public void Show_ohne_Aktion_bleibt_wie_bisher()
    {
        var logik = new ToastQueueLogic();
        logik.Show("Projekt gespeichert", ToastSeverity.Success, nowMs: 0);

        var item = Assert.Single(logik.Visible);
        Assert.Null(item.AktionText);
        Assert.False(item.HatAktion);
    }

    [Fact]
    public void Standardmethode_faellt_ohne_Senke_auf_die_einfache_Meldung_zurueck()
    {
        var dienst = new ToastService();
        string? gesehen = null;
        string? aktionText = null;
        dienst.AttachSink((message, _, aktion, _) => { gesehen = message; aktionText = aktion; });

        dienst.Success("Schächte exportiert", "Ordner öffnen", () => { });

        Assert.Equal("Schächte exportiert", gesehen);
        Assert.Equal("Ordner öffnen", aktionText);
    }

    [Fact]
    public void Der_Toast_zeigt_den_Link_und_verdrahtet_den_Klick()
    {
        var xaml = File.ReadAllText(RepoFile("src", "AuswertungPro.Next.UI", "Controls", "ToastHost.xaml"));

        Assert.Contains("{Binding AktionText}", xaml, StringComparison.Ordinal);
        Assert.Contains("{Binding HatAktion, Converter={StaticResource BoolToVis}}", xaml, StringComparison.Ordinal);
        Assert.Contains("Click=\"ToastAktion_Click\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Style=\"{StaticResource LinkButtonStyle}\"", xaml, StringComparison.Ordinal);
    }
}
```

- [ ] **Step 2: Rot sehen**

Run: `dotnet test tests/AuswertungPro.Next.UI.Tests/AuswertungPro.Next.UI.Tests.csproj -o .tmp/testout-design --nologo -v q --filter "FullyQualifiedName~ToastAktionTests"`
Expected: Build-Fehler `CS1739`/`CS1061` (Parameter `aktionText`, Eigenschaft `AktionText` unbekannt).

- [ ] **Step 3: `ToastItem` und `ToastQueueLogic.Show` erweitern**

In `ToastQueueLogic.cs` den Record ersetzen (Zeile 17):

```csharp
public sealed record ToastItem(
    long Id,
    string Message,
    ToastSeverity Severity,
    string? AktionText = null,
    Action? Aktion = null)
{
    /// <summary>Anzeigedauer in ms; null = bleibt bis Klick (nur Error).</summary>
    public long? DurationMs => Severity switch
    {
        ToastSeverity.Warning => 5000,
        ToastSeverity.Error => null,
        _ => 3000, // Success / Info
    };

    /// <summary>True, wenn der Toast einen Link wie "Ordner öffnen" traegt.</summary>
    public bool HatAktion => !string.IsNullOrWhiteSpace(AktionText) && Aktion is not null;
}
```

Die bestehende Methode `public long? Show(string message, ToastSeverity severity, long nowMs)` (Zeile 68) so umbauen, dass sie an eine neue Ueberladung delegiert. Dort, wo heute `new ToastItem(id, message, severity)` erzeugt wird, `new ToastItem(id, message, severity, aktionText, aktion)` erzeugen:

```csharp
public long? Show(string message, ToastSeverity severity, long nowMs)
    => Show(message, severity, nowMs, aktionText: null, aktion: null);

/// <summary>Wie <see cref="Show(string, ToastSeverity, long)"/>, zusaetzlich mit einem Link, den der Nutzer anklicken kann.</summary>
public long? Show(string message, ToastSeverity severity, long nowMs, string? aktionText, Action? aktion)
{
    // ... bisheriger Rumpf; einzige Aenderung: das ToastItem erhaelt aktionText und aktion.
}
```

- [ ] **Step 4: `IToastService` und `ToastService`**

`IToastService.cs` — Standardmethode ergaenzen (Fakes in Tests muessen sie NICHT implementieren):

```csharp
public interface IToastService
{
    void Success(string message);
    void Info(string message);
    void Warning(string message);
    void Error(string message);

    /// <summary>
    /// Erfolg mit anklickbarem Link, z. B. "Ordner öffnen". Umsetzungen ohne Link zeigen nur
    /// die Meldung — deshalb eine Standardmethode.
    /// </summary>
    void Success(string message, string aktionText, Action aktion) => Success(message);
}
```

`ToastService.cs` — Senke mit vier Parametern; die Standardmethode ueberschreiben:

```csharp
public sealed class ToastService : IToastService
{
    private Action<string, ToastSeverity, string?, Action?>? _sink;

    /// <summary>Verbindet den Service einmalig mit dem sichtbaren Host (vom MainWindow gesetzt).</summary>
    public void AttachSink(Action<string, ToastSeverity, string?, Action?> sink) => _sink = sink;

    public void Success(string message) => Post(message, ToastSeverity.Success, null, null);
    public void Success(string message, string aktionText, Action aktion) => Post(message, ToastSeverity.Success, aktionText, aktion);
    public void Info(string message) => Post(message, ToastSeverity.Info, null, null);
    public void Warning(string message) => Post(message, ToastSeverity.Warning, null, null);
    public void Error(string message) => Post(message, ToastSeverity.Error, null, null);

    private void Post(string message, ToastSeverity severity, string? aktionText, Action? aktion)
    {
        if (severity is ToastSeverity.Warning or ToastSeverity.Error)
            BestEffort.ReportWarning($"[Toast/{severity}] {message}");
        else
            Trace.WriteLine($"[Toast/{severity}] {message}");

        var sink = _sink;
        if (sink is null)
            return;

        sink(message, severity, aktionText, aktion);
    }
}
```

`MainWindow.xaml.cs:30`:

```csharp
services.Toasts.AttachSink((message, severity, aktionText, aktion) => ToastHostControl.Enqueue(message, severity, aktionText, aktion));
```

- [ ] **Step 5: `ToastHost` — Link im Toast**

`ToastHost.xaml.cs` — `Enqueue` erweitern und den Klick-Handler ergaenzen:

```csharp
/// <summary>Meldung anzeigen. Threadsicher — marshalt bei Bedarf auf den UI-Thread.</summary>
public void Enqueue(string message, ToastSeverity severity)
    => Enqueue(message, severity, null, null);

public void Enqueue(string message, ToastSeverity severity, string? aktionText, Action? aktion)
{
    if (!Dispatcher.CheckAccess())
    {
        Dispatcher.BeginInvoke(new Action(() => Enqueue(message, severity, aktionText, aktion)));
        return;
    }

    _logic.Show(message, severity, NowMs(), aktionText, aktion);
    Sync();
    if (!_timer.IsEnabled)
        _timer.Start();
}

/// <summary>Link im Toast: Aktion ausfuehren, danach den Toast schliessen wie bei einem Klick.</summary>
private void ToastAktion_Click(object sender, RoutedEventArgs e)
{
    if (sender is not FrameworkElement { DataContext: ToastItem item })
        return;

    e.Handled = true; // Der Klick darf nicht zusaetzlich Toast_Click ausloesen.
    try
    {
        item.Aktion?.Invoke();
    }
    catch (Exception ex)
    {
        BestEffort.ReportWarning($"[Toast] Aktion '{item.AktionText}' fehlgeschlagen: {ex.Message}");
    }

    _logic.Dismiss(item.Id, NowMs()); // Name der bestehenden Schliessmethode pruefen: sie wird in Toast_Click (Zeile 166) aufgerufen; denselben Aufruf verwenden.
    Sync();
}
```

`ToastHost.xaml` — Ressource und Link. In `<UserControl.Resources>` (anlegen, falls nicht vorhanden) `<BooleanToVisibilityConverter x:Key="BoolToVis"/>`. Den `TextBlock` der Meldung (Zeile 44-46) durch einen `StackPanel` ersetzen:

```xml
<StackPanel Grid.Column="1" Margin="12,10,14,10">
    <TextBlock Text="{Binding Message}" TextWrapping="Wrap"
               Foreground="{DynamicResource TextBrush}" FontSize="{DynamicResource TextM}"/>
    <!-- Link wie "Ordner öffnen": nur wenn der Aufrufer eine Aktion mitgegeben hat. -->
    <Button Style="{StaticResource LinkButtonStyle}" HorizontalAlignment="Left" Margin="0,4,0,0"
            Content="{Binding AktionText}" Click="ToastAktion_Click"
            FontSize="{DynamicResource TextS}"
            Visibility="{Binding HatAktion, Converter={StaticResource BoolToVis}}"/>
</StackPanel>
```

Hinweis: `LinkButtonStyle` ist in `Theme/Controls.xaml` definiert (Key vorhanden, kein neuer Stil noetig).

- [ ] **Step 6: Gruen sehen und Nachbarn pruefen**

Run: `dotnet test tests/AuswertungPro.Next.UI.Tests/AuswertungPro.Next.UI.Tests.csproj -o .tmp/testout-design --nologo -v q --filter "FullyQualifiedName~Toast"`
Expected: `ToastAktionTests` 4 gruen, `ToastQueueLogicTests`, `ToastHostAnimationTests`, `CodingScreenshotToastWorkflowTests` weiterhin gruen.

- [ ] **Step 7: Commit**

```bash
git add src/AuswertungPro.Next.UI/Services/IToastService.cs src/AuswertungPro.Next.UI/Services/ToastQueueLogic.cs src/AuswertungPro.Next.UI/Services/ToastService.cs src/AuswertungPro.Next.UI/Controls/ToastHost.xaml src/AuswertungPro.Next.UI/Controls/ToastHost.xaml.cs src/AuswertungPro.Next.UI/MainWindow.xaml.cs tests/AuswertungPro.Next.UI.Tests/ToastAktionTests.cs
git commit -m "Toast mit Link: Erfolgsmeldungen koennen eine Aktion wie 'Ordner oeffnen' tragen" -m "Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 2: Erfolgs-Toasts mit „Ordner oeffnen" / „Bericht oeffnen" verdrahten

**Files:**
- Modify: `src/AuswertungPro.Next.UI/ViewModels/Pages/ExportPageViewModel.ExcelExport.cs:51,125`
- Modify: `src/AuswertungPro.Next.UI/ViewModels/Pages/ExportPageViewModel.Xtf.cs` (Methode `Uebernimm`)
- Modify: `src/AuswertungPro.Next.UI/Services/ImportReportNavigationController.cs:13-48`
- Test: `tests/AuswertungPro.Next.UI.Tests/ExportPageXtfAuswahlTests.cs` (Test `Nach_dem_Schreiben_laesst_sich_der_Ausgabeordner_oeffnen` erweitern)
- Test: `tests/AuswertungPro.Next.UI.Tests/ImportReportToastTests.cs` (neu)

**Interfaces:**
- Consumes: `IToastService.Success(string, string, Action)` aus Task 1; `IExplorerRevealService.TryReveal(string?, out string?)` (bestehend, im `ExportPageViewModel` als `_explorerReveal`); `ImportReportNavigationController.SetLastReportPath(string?)` und `OpenLastReport()` (bestehend, Zeilen 34/37).
- Produces: `ImportReportNavigationController` erhaelt einen optionalen Konstruktorparameter `IToastService? toasts = null` (letzter Parameter).

- [ ] **Step 1: Failing tests**

In `ExportPageXtfAuswahlTests.cs` den `ToastFake` durch eine aufzeichnende Fassung ersetzen und den dritten Test erweitern:

```csharp
private sealed class ToastFake : IToastService
{
    public string? LetzterAktionText { get; private set; }
    public Action? LetzteAktion { get; private set; }

    public void Success(string message) { }
    public void Success(string message, string aktionText, Action aktion)
    {
        LetzterAktionText = aktionText;
        LetzteAktion = aktion;
    }
    public void Info(string message) { }
    public void Warning(string message) { }
    public void Error(string message) => Assert.Fail(message);
}
```

Die `Testwelt` merkt sich den Fake als `public ToastFake Toasts { get; } = new();` und uebergibt ihn statt `new ToastFake()`. Am Ende von `Nach_dem_Schreiben_laesst_sich_der_Ausgabeordner_oeffnen` ergaenzen:

```csharp
Assert.Equal("Ordner öffnen", welt.Toasts.LetzterAktionText);
welt.Explorer.Zuruecksetzen();
welt.Toasts.LetzteAktion!();
Assert.Equal(erwartet, welt.Explorer.Geoeffnet);
```

`ExplorerFake` erhaelt `public void Zuruecksetzen() => Geoeffnet = null;`.

Neue Datei `ImportReportToastTests.cs`:

```csharp
using System.IO;
using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Sobald der Importbericht abgelegt ist, sagt ein Toast "Import abgeschlossen" und bietet
/// "Bericht öffnen" an — bisher musste man den Bericht im Menue suchen.
/// </summary>
public sealed class ImportReportToastTests
{
    // Bestehende Signatur (ImportReportNavigationController.cs:13):
    //   (IDialogService dialogs, Func<string?> getProjectPath, Func<string, bool> tryOpen)
    // — der neue vierte Parameter `IToastService? toasts = null` kommt in Step 4 hinzu.
    private static ImportReportNavigationController Controller(ToastFake toasts, List<string> geoeffnet)
        => new(
            new DialogFake(),
            () => @"C:\Projekt\Projektdateien\projekt.json",
            pfad => { geoeffnet.Add(pfad); return true; },
            toasts: toasts);

    [Fact]
    public void Abgelegter_Bericht_erzeugt_Toast_mit_Bericht_oeffnen()
    {
        var toasts = new ToastFake();
        var geoeffnet = new List<string>();
        var controller = Controller(toasts, geoeffnet);
        var bericht = Path.Combine(Path.GetTempPath(), "import_" + Guid.NewGuid().ToString("N") + ".txt");
        File.WriteAllText(bericht, "Bericht");

        try
        {
            controller.SetLastReportPath(bericht);

            Assert.Equal("Import abgeschlossen — Bericht liegt bereit.", toasts.Meldung);
            Assert.Equal("Bericht öffnen", toasts.AktionText);
            toasts.Aktion!();
            Assert.Equal([bericht], geoeffnet); // Der Link oeffnet den Bericht ueber den sicheren Shell-Oeffner.
        }
        finally
        {
            File.Delete(bericht);
        }
    }

    [Fact]
    public void Ohne_Berichtspfad_gibt_es_keinen_Toast()
    {
        var toasts = new ToastFake();
        var controller = Controller(toasts, []);

        controller.SetLastReportPath(null);

        Assert.Null(toasts.Meldung);
    }

    private sealed class DialogFake : IDialogService
    {
        public string? OpenFile(string title, string filter, string? initialDirectory = null) => null;
        public string[] OpenFiles(string title, string filter) => [];
        public string? SaveFile(string title, string filter, string? defaultExt = null, string? defaultFileName = null) => null;
        public string? SelectFolder(string title, string? initialPath = null) => null;
        public void Info(string message, string title = "Hinweis") { }
        public void Warn(string message, string title = "Warnung") { }
        public void Error(string message, string title = "Fehler") => Assert.Fail(message);
        public bool Confirm(string message, string title = "Bestaetigung") => true;
        public bool ConfirmWarn(string message, string title = "Bestaetigung", bool defaultNo = true) => true;
        public DialogConfirm ConfirmCancel(string message, string title = "Bestaetigung") => DialogConfirm.Yes;
    }

    private sealed class ToastFake : IToastService
    {
        public string? Meldung { get; private set; }
        public string? AktionText { get; private set; }
        public Action? Aktion { get; private set; }

        public void Success(string message) => Meldung = message;
        public void Success(string message, string aktionText, Action aktion)
        {
            Meldung = message; AktionText = aktionText; Aktion = aktion;
        }
        public void Info(string message) { }
        public void Warning(string message) { }
        public void Error(string message) => Assert.Fail(message);
    }
}
```

`ImportReportNavigationController` ist `internal`; die UI-Tests sehen interne Typen bereits (`InternalsVisibleTo` — pruefen mit `grep -rn InternalsVisibleTo src/AuswertungPro.Next.UI/*.cs`; falls nicht vorhanden, den Controller `public sealed` machen).

- [ ] **Step 2: Rot sehen**

Run: `dotnet test tests/AuswertungPro.Next.UI.Tests/AuswertungPro.Next.UI.Tests.csproj -o .tmp/testout-design --nologo -v q --filter "FullyQualifiedName~ImportReportToastTests|FullyQualifiedName~ExportPageXtfAuswahlTests"`
Expected: Build-Fehler (`toasts` unbekannt) bzw. `Assert.Equal` auf `LetzterAktionText` schlaegt fehl (null).

- [ ] **Step 3: Excel und XTF verdrahten**

`ExportPageViewModel.ExcelExport.cs:51` (Haltungen) und `:125` (Schaechte) — jeweils:

```csharp
_toasts.Success($"Haltungen exportiert: {Path.GetFileName(outPath)}", "Ordner öffnen",
    () => _explorerReveal.TryReveal(outPath, out _));
```

(analog `Schächte exportiert`). `TryReveal` mit dem Dateipfad markiert die Datei im Explorer.

`ExportPageViewModel.Xtf.cs`, Methode `Uebernimm`:

```csharp
private void Uebernimm(XtfExportErgebnis ergebnis)
{
    LastResult = ergebnis.Meldung;
    if (!ergebnis.Geschrieben)
        return;

    LetzterXtfOrdner = ergebnis.Ordner;
    _toasts.Success(ergebnis.Meldung, "Ordner öffnen", OeffneXtfOrdner);
}
```

- [ ] **Step 4: Import verdrahten**

`ImportReportNavigationController.cs`: Konstruktor um `IToastService? toasts = null` (letzter Parameter) und Feld `private readonly IToastService? _toasts;` erweitern. `SetLastReportPath`:

```csharp
public void SetLastReportPath(string? path)
{
    _lastReportPath = path; // bestehende Zuweisung beibehalten (Feldname pruefen)
    if (string.IsNullOrWhiteSpace(path))
        return;

    _toasts?.Success("Import abgeschlossen — Bericht liegt bereit.", "Bericht öffnen", OpenLastReport);
}
```

Im `ImportPageViewModel.cs:117-120` wird der Controller heute so gebaut:

```csharp
_reportNavigationController = new Services.ImportReportNavigationController(
    dialogs,
    () => _settings.LastProjectPath,
    path => Services.SafeShellOpen.TryOpen(path, out _));
```

Dort als vierten Parameter `toasts: sp.Toasts` ergaenzen (`sp` ist in diesem Konstruktor vorhanden — dieselbe Variable liefert `sp.ProjectPhotoAssignment` wenige Zeilen darueber).

- [ ] **Step 5: Gruen sehen, ganze UI-Reihe**

Run: `dotnet test tests/AuswertungPro.Next.UI.Tests/AuswertungPro.Next.UI.Tests.csproj -o .tmp/testout-design --nologo -v q`
Expected: 0 Fehler (Stand vor diesem Plan: 6272 gruen, 3 uebersprungen). Faellt `NachschlagKontextmenueTests` (60-s-Kindprozess) um, allein wiederholen — er besteht allein in ~26 s.

- [ ] **Step 6: Commit**

```bash
git add src/AuswertungPro.Next.UI/ViewModels/Pages/ExportPageViewModel.ExcelExport.cs src/AuswertungPro.Next.UI/ViewModels/Pages/ExportPageViewModel.Xtf.cs src/AuswertungPro.Next.UI/Services/ImportReportNavigationController.cs src/AuswertungPro.Next.UI/ViewModels/Pages/ImportPageViewModel.cs tests/AuswertungPro.Next.UI.Tests/ExportPageXtfAuswahlTests.cs tests/AuswertungPro.Next.UI.Tests/ImportReportToastTests.cs
git commit -m "Erfolgs-Toasts fuehren zum Ergebnis: Ordner oeffnen nach Excel/XTF, Bericht oeffnen nach Import" -m "Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 3: Hover-Lift und gestaffeltes Einblenden

**Files:**
- Modify: `src/AuswertungPro.Next.UI/Controls/PhotoGalleryPanel.xaml:59` (Border der Fotokarte)
- Modify: `src/AuswertungPro.Next.UI/Views/Pages/DossiersPage.xaml:36` (Wurzel-`Grid`)
- Test: `tests/AuswertungPro.Next.UI.Tests/DesignAuditFeedbackTests.cs` (neu)

**Interfaces:**
- Consumes: angehaengte Eigenschaften `ui:HoverFx.Lift="True"` (`HoverFx.cs`, hebt ein Element beim Zeigen an; respektiert `MotionSettings.ReduceMotion`) und `ui:EntranceFx.Stagger="True"` (`EntranceFx.cs`, wirkt auf die Kinder eines `Panel`, also auch `Grid`; Vorbild `VsaPage.xaml:6`).
- Produces: nichts Neues.

- [ ] **Step 1: Failing test**

```csharp
// tests/AuswertungPro.Next.UI.Tests/DesignAuditFeedbackTests.cs
using System.IO;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>Design-Audit M2: Bewegung dort, wo sie ein Versprechen einloest (Karte ist klickbar, Seite baut sich auf).</summary>
public sealed class DesignAuditFeedbackTests
{
    [Fact]
    public void Fotokarten_heben_sich_beim_Zeigen_an()
    {
        var xaml = File.ReadAllText(RepoFile("src", "AuswertungPro.Next.UI", "Controls", "PhotoGalleryPanel.xaml"));
        Assert.Contains("xmlns:ui=\"clr-namespace:AuswertungPro.Next.UI\"", xaml, StringComparison.Ordinal);
        Assert.Contains("ui:HoverFx.Lift=\"True\"", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void Dossier_Cockpit_baut_sich_gestaffelt_auf()
    {
        var xaml = File.ReadAllText(RepoFile("src", "AuswertungPro.Next.UI", "Views", "Pages", "DossiersPage.xaml"));
        Assert.Contains("<Grid Margin=\"0\" ui:EntranceFx.Stagger=\"True\">", xaml, StringComparison.Ordinal);
    }
}
```

- [ ] **Step 2: Rot sehen**

Run: `dotnet test tests/AuswertungPro.Next.UI.Tests/AuswertungPro.Next.UI.Tests.csproj -o .tmp/testout-design --nologo -v q --filter "FullyQualifiedName~DesignAuditFeedbackTests"`
Expected: 2 FAIL („Sub-string not found").

- [ ] **Step 3: XAML aendern**

`PhotoGalleryPanel.xaml`: im Wurzelelement `xmlns:ui="clr-namespace:AuswertungPro.Next.UI"` ergaenzen (falls nicht vorhanden); Zeile 59:

```xml
<Border Margin="4" Padding="4" CornerRadius="{DynamicResource RadiusM}" Cursor="Hand"
        ui:HoverFx.Lift="True"
        Background="{DynamicResource HeaderBrush}"
        BorderBrush="{DynamicResource BorderBrush}" BorderThickness="1"
        MouseLeftButtonUp="Foto_Click">
```

`DossiersPage.xaml:36`: `<Grid Margin="0">` → `<Grid Margin="0" ui:EntranceFx.Stagger="True">` (der Namespace `ui` ist in Zeile 5 bereits deklariert).

- [ ] **Step 4: Gruen sehen**

Run: wie Step 2. Expected: 2 PASS. Zusaetzlich `--filter "FullyQualifiedName~DesignAudit"` — alle Waechter gruen.

- [ ] **Step 5: Commit**

```bash
git add src/AuswertungPro.Next.UI/Controls/PhotoGalleryPanel.xaml src/AuswertungPro.Next.UI/Views/Pages/DossiersPage.xaml tests/AuswertungPro.Next.UI.Tests/DesignAuditFeedbackTests.cs
git commit -m "Fotokarten heben sich beim Zeigen an, das Dossier-Cockpit baut sich gestaffelt auf" -m "Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 4: Vorlesbare Namen fuer Icon-Knoepfe

**Files:**
- Modify (24 Knoepfe, Zeilen Stand 2026-09-04):
  - `src/AuswertungPro.Next.UI/Dialogs/PositionTemplateEditorDialog.xaml:103,108,139`
  - `src/AuswertungPro.Next.UI/Views/Pages/ExportPage.xaml:169,286`
  - `src/AuswertungPro.Next.UI/Views/Pages/Haltungsansicht/HaltungsansichtView.xaml:190`
  - `src/AuswertungPro.Next.UI/Views/Pages/SettingsPage.xaml:436,447,458,621,640,683`
  - `src/AuswertungPro.Next.UI/Views/Windows/PlayerWindow.xaml:190,467,476`
  - `src/AuswertungPro.Next.UI/Views/Windows/SanierungsmassnahmenWindow.xaml:445,453,458,513,519,525`
  - `src/AuswertungPro.Next.UI/Views/Windows/SchachtMassnahmenWindow.xaml:68,104`
  - `src/AuswertungPro.Next.UI/Views/Windows/TrainingCenterWindow.xaml:54`
- Test: `tests/AuswertungPro.Next.UI.Tests/DesignAuditAccessibilityTests.cs` (neuer Fact)

**Interfaces:** keine.

- [ ] **Step 1: Failing test in `DesignAuditAccessibilityTests.cs` ergaenzen**

```csharp
[Fact]
public void Icon_Knoepfe_haben_einen_vorlesbaren_Namen_und_einen_Tooltip()
{
    // Ein Knopf, der nur ein Glyph zeigt, ist fuer Screenreader und Sprachsteuerung stumm.
    // Der Tooltip ist die Erklaerung fuer Sehende, AutomationProperties.Name dieselbe fuer alle anderen.
    var uiRoot = RepoFile("src", "AuswertungPro.Next.UI");
    var muster = new System.Text.RegularExpressions.Regex(
        "<Button\\b([^>]*)>\\s*<ui:FluentIcon[^>]*/>\\s*</Button>", System.Text.RegularExpressions.RegexOptions.Compiled);
    var treffer = new List<string>();

    foreach (var datei in Directory.EnumerateFiles(uiRoot, "*.xaml", SearchOption.AllDirectories))
    {
        if (datei.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}") || datei.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
            continue;

        var xaml = File.ReadAllText(datei);
        foreach (System.Text.RegularExpressions.Match m in muster.Matches(xaml))
        {
            var attribute = m.Groups[1].Value;
            if (attribute.Contains("Content=", StringComparison.Ordinal))
                continue;
            var zeile = xaml[..m.Index].Count(c => c == '\n') + 1;
            if (!attribute.Contains("AutomationProperties.Name=", StringComparison.Ordinal))
                treffer.Add($"{Path.GetRelativePath(uiRoot, datei)}:{zeile}: kein AutomationProperties.Name");
            if (!attribute.Contains("ToolTip=", StringComparison.Ordinal))
                treffer.Add($"{Path.GetRelativePath(uiRoot, datei)}:{zeile}: kein ToolTip");
        }
    }

    Assert.True(treffer.Count == 0, "Icon-Knoepfe ohne Namen/Tooltip:\n" + string.Join("\n", treffer));
}
```

(Am Dateikopf `using System.IO;` und `using System.Linq;` sicherstellen.)

- [ ] **Step 2: Rot sehen**

Run: `dotnet test tests/AuswertungPro.Next.UI.Tests/AuswertungPro.Next.UI.Tests.csproj -o .tmp/testout-design --nologo -v q --filter "FullyQualifiedName~Icon_Knoepfe_haben"`
Expected: FAIL mit 27 Zeilen (24 ohne Name, 3 davon zusaetzlich ohne Tooltip).

- [ ] **Step 3: Alle 24 Knoepfe ergaenzen**

Regel: `AutomationProperties.Name` = Tooltip-Text ohne Schlusspunkt. Beispiel `ExportPage.xaml:169`:

```xml
<Button Grid.Column="1" Command="{Binding BrowseExcelExportRootCommand}"
        Margin="4,0,0,0" Padding="10,0"
        ToolTip="Gemeinsamen Excel-Zielordner wählen"
        AutomationProperties.Name="Gemeinsamen Excel-Zielordner wählen">
    <ui:FluentIcon Glyph="&#xE838;" Foreground="{DynamicResource MutedBrush}"/>
</Button>
```

Die drei Knoepfe in `SettingsPage.xaml:621,640,683` haben keinen Tooltip (alle drei sind Ordner-Wahl-Knoepfe mit Glyph `E838` neben einem Pfadfeld): `ToolTip="Ordner wählen"` und `AutomationProperties.Name="Ordner wählen"` — den genauen Zweck aus dem benachbarten `TextBlock`/`TextBox` uebernehmen (z. B. „Video-Ordner wählen").

Die uebrigen Namen (Tooltip ohne Punkt): `Nach oben`, `Nach unten`, `Position löschen`, `Ziel-Wurzel wählen`, `Neue Beobachtung codieren`, `Projektdatei auswählen`, `Standardordner für Projekte auswählen`, `Abwasserkataster-XTF für die Kartenansicht auswählen`, `Tastenkürzel anzeigen`, `Zurück (Step)`, `Weiter (Step)`, `Maßnahme entfernen` (→ sichtbar `Massnahme entfernen`, kein `ß`), `Position entfernen`, `In Auswahl übernehmen`, `Entfernen`, `Ordnerauswahl zurücksetzen`.

- [ ] **Step 4: Gruen sehen**

Run: `--filter "FullyQualifiedName~DesignAudit"` — alle Waechter gruen (auch `DesignAuditFeinschliffTests`, weil das `ß` entfernt wurde).

- [ ] **Step 5: Commit**

```bash
git add src/AuswertungPro.Next.UI tests/AuswertungPro.Next.UI.Tests/DesignAuditAccessibilityTests.cs
git commit -m "24 Icon-Knoepfe tragen einen vorlesbaren Namen; drei Ordner-Knoepfe erhalten einen Tooltip" -m "Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 5: Tastenkuerzel in den Player-Tooltips

**Files:**
- Modify: `src/AuswertungPro.Next.UI/Views/Windows/PlayerWindow.xaml` (Steuerleiste; Knoepfe finden ueber ihre `Click="…"`-/`Command`-Namen, die dieselben Aktionen ausloesen wie `PlayerKeyboardShortcutPolicy.Resolve`)
- Test: `tests/AuswertungPro.Next.UI.Tests/DesignAuditPlayerShortcutTests.cs` (neu)

**Interfaces:**
- Consumes: `PlayerKeyboardShortcutPolicy.Resolve(Key, bool)` in `src/AuswertungPro.Next.UI/Player/PlayerKeyboardShortcutPolicy.cs:29-43` — die verbindliche Tastenliste.
- Produces: feste Tooltip-Texte (unten), auf die der Waechter prueft.

Tastenliste (aus der Policy) und der Tooltip, der dazu im XAML stehen muss:

| Taste | Aktion | Tooltip-Text |
|---|---|---|
| Leertaste | TogglePlayPause | `Abspielen / Pause — Leertaste` |
| S | Stop | `Stopp — Taste S` |
| + / − | SpeedUp / SpeedDown | `Schneller — Taste +` / `Langsamer — Taste −` |
| ← / → | JumpBackward / JumpForward | `5 Sekunden zurück — Pfeil links` / `5 Sekunden vor — Pfeil rechts` |
| D | ToggleDetection | `Erkennung ein/aus — Taste D` |
| M | ToggleMarkTool | `Bereich markieren — Taste M` |
| F1 | Kuerzel-Uebersicht | `Tastenkürzel anzeigen — F1` (Knopf `PlayerWindow.xaml:190`) |

`P` (Pause) und `R` (Resume) haben keinen eigenen Knopf — sie bleiben in der F1-Uebersicht.

- [ ] **Step 1: Failing test**

```csharp
// tests/AuswertungPro.Next.UI.Tests/DesignAuditPlayerShortcutTests.cs
using System.IO;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Wer im Player codiert, hat die Haende auf der Tastatur. Jede Taste aus
/// PlayerKeyboardShortcutPolicy, die einen Knopf hat, steht in dessen Tooltip.
/// </summary>
public sealed class DesignAuditPlayerShortcutTests
{
    [Theory]
    [InlineData("Abspielen / Pause — Leertaste")]
    [InlineData("Stopp — Taste S")]
    [InlineData("Schneller — Taste +")]
    [InlineData("Langsamer — Taste −")]
    [InlineData("5 Sekunden zurück — Pfeil links")]
    [InlineData("5 Sekunden vor — Pfeil rechts")]
    [InlineData("Erkennung ein/aus — Taste D")]
    [InlineData("Bereich markieren — Taste M")]
    [InlineData("Tastenkürzel anzeigen — F1")]
    public void Player_Knoepfe_nennen_ihre_Taste_im_Tooltip(string tooltip)
    {
        var xaml = File.ReadAllText(RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.xaml"));
        Assert.Contains($"ToolTip=\"{tooltip}\"", xaml, StringComparison.Ordinal);
    }
}
```

- [ ] **Step 2: Rot sehen**

Run: `--filter "FullyQualifiedName~DesignAuditPlayerShortcutTests"` — Expected: 9 FAIL.

- [ ] **Step 3: Knoepfe finden und Tooltips setzen**

Zuordnung der Knoepfe: `grep -n 'Click="\|Command="' src/AuswertungPro.Next.UI/Views/Windows/PlayerWindow.xaml` und in `PlayerWindow.xaml.cs` bzw. `Views/Windows/PlayerWindow.*.cs` nachlesen, welcher Handler `TogglePlayPause`, `Stop`, `SpeedUp`/`SpeedDown`, `JumpForward`/`JumpBackward`, `ToggleDetection`, `ToggleMarkTool` ausloest (dieselben Methoden, die der Tastatur-Dispatcher fuer `PlayerKeyboardAction` aufruft — `grep -rn "PlayerKeyboardAction\." src/AuswertungPro.Next.UI/Views/Windows src/AuswertungPro.Next.UI/Player`). Den jeweiligen `ToolTip` exakt auf den Text der Tabelle setzen; bestehende Zusatzinfos im Tooltip (z. B. „5 Sekunden") sind in den neuen Texten enthalten. Hat ein Knopf einen eigenen `AutomationProperties.Name`, diesen ebenfalls auf den neuen Text setzen. Der Knopf `Tastenkürzel anzeigen` (Zeile 190) erhaelt `ToolTip="Tastenkürzel anzeigen — F1"`.

Gibt es fuer eine Taste keinen Knopf (z. B. Geschwindigkeit nur als `1x 2x 4x 8x`-Auswahl), den Tooltip an das nächstliegende Bedienelement setzen (die Geschwindigkeitsleiste) und das im Commit-Text nennen.

- [ ] **Step 4: Gruen sehen**

Run: `--filter "FullyQualifiedName~DesignAuditPlayerShortcutTests|FullyQualifiedName~DesignAudit"` — Expected: alle PASS (auch die Umlaut- und Glyph-Waechter, die Tooltips lesen).

- [ ] **Step 5: Commit**

```bash
git add src/AuswertungPro.Next.UI/Views/Windows/PlayerWindow.xaml tests/AuswertungPro.Next.UI.Tests/DesignAuditPlayerShortcutTests.cs
git commit -m "Player-Knoepfe nennen ihre Taste im Tooltip" -m "Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 6: Suche in den Einstellungen

**Files:**
- Create: `src/AuswertungPro.Next.UI/Settings/SettingsSearchMatcher.cs`
- Create: `src/AuswertungPro.Next.UI/Settings/SettingsSearchController.cs`
- Modify: `src/AuswertungPro.Next.UI/Views/Pages/SettingsPage.xaml:267-275` (Kopf-`DockPanel`), `SettingsPage.xaml.cs` (11 Zeilen heute)
- Test: `tests/AuswertungPro.Next.UI.Tests/SettingsSearchTests.cs` (neu)

**Interfaces:**
- Consumes: `TabControl` mit `TabItem`s und `GroupBox`-Gruppen (`Style="{StaticResource SettingsSectionGroupBox}"`, 17 Stueck) in `SettingsPage.xaml`; `RunOnSta`-Helfer der UI-Tests (siehe `DossierTextUndoControllerTests`).
- Produces:
  - `public static class SettingsSearchMatcher { public static string Normalisiere(string text); public static bool Passt(string suche, IEnumerable<string> texte); }`
  - `public sealed class SettingsSearchController(TabControl reiter) { public int Anwenden(string suche); }` — gibt die Zahl sichtbarer Gruppen zurueck; bei leerer Suche alles sichtbar.

- [ ] **Step 1: Failing tests (Matcher)**

```csharp
// tests/AuswertungPro.Next.UI.Tests/SettingsSearchTests.cs
using System.IO;
using System.Windows.Controls;
using AuswertungPro.Next.UI.Settings;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Design-Audit M4: Die Einstellungen (6 Reiter, 17 Gruppen) bekommen ein Suchfeld. Der Abgleich
/// ist Umlaut-tolerant ("pruef" findet "Prüfen"), verknuepft mehrere Woerter mit UND und liest
/// Ueberschrift, Texte, Haekchen-Beschriftungen und Tooltips einer Gruppe.
/// </summary>
public sealed class SettingsSearchTests
{
    [Theory]
    [InlineData("Prüfen und bereinigen", "pruef", true)]
    [InlineData("Prüfen und bereinigen", "prüf", true)]
    [InlineData("Datenordner und Logs", "log ordner", true)]
    [InlineData("Datenordner und Logs", "log video", false)]
    [InlineData("KI-Schwellwerte", "schwell", true)]
    [InlineData("Video-Player", "", true)]
    public void Matcher_ist_umlaut_tolerant_und_verknuepft_Woerter_mit_UND(string text, string suche, bool erwartet)
        => Assert.Equal(erwartet, SettingsSearchMatcher.Passt(suche, [text]));

    [Fact]
    public void Matcher_liest_alle_Texte_einer_Gruppe_gemeinsam()
        => Assert.True(SettingsSearchMatcher.Passt("fotos seite", ["Haltungsprotokoll (PDF)", "Fotos je Seite", "Gilt für selbst erzeugte Protokolle"]));

    [Fact]
    public void Controller_blendet_Gruppen_ohne_Treffer_aus_und_waehlt_den_ersten_Reiter_mit_Treffer()
    {
        StaTestRunner.Run(() =>
        {
            var reiter = new TabControl();
            var allgemein = new TabItem { Header = "Allgemein", Content = new StackPanel() };
            var videoGruppe = new GroupBox { Header = "Video-Player", Content = new TextBlock { Text = "Sprungweite in Sekunden" } };
            var kiGruppe = new GroupBox { Header = "KI-Schwellwerte", Content = new CheckBox { Content = "Mindest-Konfidenz für YOLO" } };
            var video = new TabItem { Header = "Video und KI", Content = new StackPanel { Children = { videoGruppe, kiGruppe } } };
            ((StackPanel)allgemein.Content).Children.Add(new GroupBox { Header = "Speichern", Content = new TextBlock { Text = "Autosave" } });
            reiter.Items.Add(allgemein);
            reiter.Items.Add(video);
            reiter.SelectedIndex = 0;

            var controller = new SettingsSearchController(reiter);

            Assert.Equal(1, controller.Anwenden("yolo"));
            Assert.Equal(System.Windows.Visibility.Collapsed, videoGruppe.Visibility);
            Assert.Equal(System.Windows.Visibility.Visible, kiGruppe.Visibility);
            Assert.Same(video, reiter.SelectedItem);

            Assert.Equal(3, controller.Anwenden(""));
            Assert.Equal(System.Windows.Visibility.Visible, videoGruppe.Visibility);
        });
    }

    [Fact]
    public void Die_Einstellungsseite_hat_ein_Suchfeld_im_Kopf()
    {
        var xaml = File.ReadAllText(RepoFile("src", "AuswertungPro.Next.UI", "Views", "Pages", "SettingsPage.xaml"));
        Assert.Contains("x:Name=\"SucheBox\"", xaml, StringComparison.Ordinal);
        Assert.Contains("TextChanged=\"SucheBox_TextChanged\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"SucheTreffer\"", xaml, StringComparison.Ordinal);
        Assert.Contains("ToolTip=\"Einstellung suchen — zeigt nur passende Gruppen und springt zum ersten Reiter mit Treffer.\"", xaml, StringComparison.Ordinal);
    }
}
```

`StaTestRunner.Run(Action)` existiert in den UI-Tests (`tests/AuswertungPro.Next.UI.Tests/StaTestRunner.cs`).

- [ ] **Step 2: Rot sehen**

Run: `--filter "FullyQualifiedName~SettingsSearchTests"` — Expected: Build-Fehler (Typen fehlen).

- [ ] **Step 3: Matcher schreiben**

```csharp
// src/AuswertungPro.Next.UI/Settings/SettingsSearchMatcher.cs
using System;
using System.Collections.Generic;
using System.Linq;

namespace AuswertungPro.Next.UI.Settings;

/// <summary>
/// Reiner Textabgleich fuer die Einstellungssuche. Umlaute und ihre ae/oe/ue-Schreibweise
/// gelten als gleich, Gross-/Kleinschreibung ist egal, mehrere Suchwoerter muessen alle
/// vorkommen (UND). Keine WPF-Abhaengigkeit, damit die Regel allein testbar bleibt.
/// </summary>
public static class SettingsSearchMatcher
{
    public static string Normalisiere(string text)
        => (text ?? string.Empty)
            .ToLowerInvariant()
            .Replace("ä", "ae").Replace("ö", "oe").Replace("ü", "ue").Replace("ß", "ss");

    public static bool Passt(string suche, IEnumerable<string> texte)
    {
        var woerter = Normalisiere(suche).Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (woerter.Length == 0)
            return true;

        var inhalt = Normalisiere(string.Join(" ", texte.Where(t => !string.IsNullOrWhiteSpace(t))));
        return woerter.All(w => inhalt.Contains(w, StringComparison.Ordinal));
    }
}
```

- [ ] **Step 4: Controller schreiben**

```csharp
// src/AuswertungPro.Next.UI/Settings/SettingsSearchController.cs
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace AuswertungPro.Next.UI.Settings;

/// <summary>
/// Wendet die Einstellungssuche auf den Reiter-Baum an: Gruppen (GroupBox) ohne Treffer werden
/// ausgeblendet, Reiter ohne Treffer abgedunkelt, und liegt der aktuelle Reiter ohne Treffer,
/// springt die Auswahl zum ersten Reiter mit Treffer. Eine leere Suche stellt alles wieder her.
/// </summary>
public sealed class SettingsSearchController(TabControl reiter)
{
    private const double AbgedunkeltOpacity = 0.45;

    /// <summary>Liefert die Zahl der sichtbaren Gruppen.</summary>
    public int Anwenden(string suche)
    {
        var sichtbarGesamt = 0;
        TabItem? ersterMitTreffer = null;

        foreach (var tab in reiter.Items.OfType<TabItem>())
        {
            var sichtbarImReiter = 0;
            foreach (var gruppe in Gruppen(tab))
            {
                var passt = SettingsSearchMatcher.Passt(suche, Texte(gruppe));
                gruppe.Visibility = passt ? Visibility.Visible : Visibility.Collapsed;
                if (passt)
                    sichtbarImReiter++;
            }

            tab.Opacity = sichtbarImReiter == 0 && !string.IsNullOrWhiteSpace(suche) ? AbgedunkeltOpacity : 1.0;
            if (sichtbarImReiter > 0)
                ersterMitTreffer ??= tab;
            sichtbarGesamt += sichtbarImReiter;
        }

        var aktuell = reiter.SelectedItem as TabItem;
        if (!string.IsNullOrWhiteSpace(suche) && ersterMitTreffer is not null
            && (aktuell is null || aktuell.Opacity < 1.0))
        {
            reiter.SelectedItem = ersterMitTreffer;
        }

        return sichtbarGesamt;
    }

    private static IEnumerable<GroupBox> Gruppen(TabItem tab)
        => Nachfahren(tab.Content as DependencyObject).OfType<GroupBox>();

    /// <summary>Alles, was der Nutzer in der Gruppe lesen kann: Ueberschrift, Texte, Haekchen, Knoepfe, Tooltips.</summary>
    private static IEnumerable<string> Texte(GroupBox gruppe)
    {
        if (gruppe.Header is string kopf)
            yield return kopf;

        foreach (var element in Nachfahren(gruppe))
        {
            switch (element)
            {
                case TextBlock t: yield return t.Text; break;
                case ContentControl { Content: string s }: yield return s; break;
            }

            if (element is FrameworkElement { ToolTip: string tooltip })
                yield return tooltip;
        }
    }

    /// <summary>Logischer Baum reicht: XAML-Inhalte sind logische Kinder, ohne Layoutlauf verfuegbar.</summary>
    private static IEnumerable<DependencyObject> Nachfahren(DependencyObject? wurzel)
    {
        if (wurzel is null)
            yield break;

        foreach (var kind in LogicalTreeHelper.GetChildren(wurzel).OfType<DependencyObject>())
        {
            yield return kind;
            foreach (var enkel in Nachfahren(kind))
                yield return enkel;
        }
    }
}
```

- [ ] **Step 5: Suchfeld in die Seite**

`SettingsPage.xaml`, Kopf-`DockPanel` (Zeile 267-275) — zwischen Titel und Speichern-Knopf:

```xml
<DockPanel Grid.Row="0" Margin="0,0,0,12">
    <Button DockPanel.Dock="Right"
            Content="Speichern"
            Command="{Binding SaveCommand}"
            Style="{StaticResource PrimaryButton}"
            Width="140"
            ToolTip="Alle geänderten Einstellungen dauerhaft speichern."/>
    <TextBlock Text="Einstellungen" Style="{StaticResource PageTitle}" Margin="0,0,12,0"/>
    <!-- Suche: filtert die 17 Gruppen ueber alle Reiter; Umlaute und ae/oe/ue gelten gleich. -->
    <StackPanel Orientation="Horizontal" VerticalAlignment="Center" Margin="12,0,12,0">
        <ui:FluentIcon Glyph="&#xE721;" Foreground="{DynamicResource MutedBrush}" Margin="0,0,6,0"/>
        <TextBox x:Name="SucheBox" Width="260" TextChanged="SucheBox_TextChanged"
                 AutomationProperties.Name="Einstellung suchen"
                 ToolTip="Einstellung suchen — zeigt nur passende Gruppen und springt zum ersten Reiter mit Treffer."/>
        <TextBlock x:Name="SucheTreffer" Margin="8,0,0,0" VerticalAlignment="Center"
                   FontSize="{DynamicResource TextXS}" Foreground="{DynamicResource MutedBrush}"/>
    </StackPanel>
</DockPanel>
```

`xmlns:ui="clr-namespace:AuswertungPro.Next.UI"` im Wurzelelement sicherstellen. Dem `TabControl` (Zeile 277) `x:Name="EinstellungsReiter"` geben.

`SettingsPage.xaml.cs`:

```csharp
using System.Windows.Controls;
using AuswertungPro.Next.UI.Settings;

namespace AuswertungPro.Next.UI.Views.Pages;

public partial class SettingsPage : UserControl
{
    private SettingsSearchController? _suche;

    public SettingsPage()
    {
        InitializeComponent();
    }

    private void SucheBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        _suche ??= new SettingsSearchController(EinstellungsReiter);
        var suche = SucheBox.Text;
        var sichtbar = _suche.Anwenden(suche);
        SucheTreffer.Text = string.IsNullOrWhiteSpace(suche)
            ? string.Empty
            : sichtbar == 0 ? "keine Treffer" : sichtbar == 1 ? "1 Gruppe" : $"{sichtbar} Gruppen";
    }
}
```

(Den heutigen Inhalt von `SettingsPage.xaml.cs` — 11 Zeilen — vorher lesen und bestehende Handler beibehalten.)

- [ ] **Step 6: Gruen sehen, ganze UI-Reihe**

Run: `--filter "FullyQualifiedName~SettingsSearchTests|FullyQualifiedName~SettingsPage|FullyQualifiedName~DesignAudit"` — dann die ganze UI-Reihe ohne Filter. Expected: 0 Fehler. `SettingsPageLayoutTests` prueft feste Textstellen der Seite — falls es die Kopfzeile wortgenau erwartet, seine Erwartung auf das neue Suchfeld erweitern, nicht das Suchfeld entfernen.

- [ ] **Step 7: Commit**

```bash
git add src/AuswertungPro.Next.UI/Settings/SettingsSearchMatcher.cs src/AuswertungPro.Next.UI/Settings/SettingsSearchController.cs src/AuswertungPro.Next.UI/Views/Pages/SettingsPage.xaml src/AuswertungPro.Next.UI/Views/Pages/SettingsPage.xaml.cs tests/AuswertungPro.Next.UI.Tests/SettingsSearchTests.cs
git commit -m "Einstellungen bekommen ein Suchfeld: filtert die 17 Gruppen umlaut-tolerant ueber alle Reiter" -m "Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 7: Regeln festhalten und Audit-Status nachfuehren

**Files:**
- Modify: `CLAUDE.md` (Abschnitt „Design-Feinschliff 2026-09-03", hinter „Fenster und Rundungen")
- Modify: `docs/DESIGN-AUDIT-2026-09-03.md` (neuer Statusblock vor „6. Waechter")

- [ ] **Step 1: CLAUDE.md ergaenzen** (Absatz, ae/oe-Schreibweise wie der Rest der Datei):

```markdown
**Feedback, Bedienbarkeit, Suche (M2-M4, 2026-09-04):** `IToastService.Success(message,
aktionText, aktion)` zeigt einen Link im Toast (Standardmethode; Fakes brauchen sie nicht).
Excel- und XTF-Export toasten mit „Ordner oeffnen", der Importbericht mit „Bericht oeffnen"
(`ImportReportNavigationController`). Fotokarten tragen `ui:HoverFx.Lift`, das Dossier-Cockpit
`ui:EntranceFx.Stagger`. Jeder Icon-Knopf hat `AutomationProperties.Name` UND `ToolTip`
(Waechter in `DesignAuditAccessibilityTests`); Player-Knoepfe nennen ihre Taste im Tooltip
(`DesignAuditPlayerShortcutTests`, Quelle `PlayerKeyboardShortcutPolicy`). Die Einstellungen
haben ein Suchfeld: `SettingsSearchMatcher` (reiner Abgleich, Umlaut-tolerant, UND) und
`SettingsSearchController` (blendet `GroupBox`-Gruppen aus, springt zum ersten Reiter mit
Treffer). Neue Gruppen brauchen nichts weiter — der Controller liest Ueberschrift, Texte,
Haekchen und Tooltips selbst.
```

- [ ] **Step 2: Audit-Status** — vor „## 6. Waechter" einfuegen:

```markdown
### Stand 2026-09-04: M2, M3, M4 UMGESETZT

- M2: Toast mit Link („Ordner oeffnen" nach Excel/XTF, „Bericht oeffnen" nach Import);
  Hover-Lift auf Fotokarten, gestaffeltes Einblenden im Dossier-Cockpit.
- M3: 24 Icon-Knoepfe mit vorlesbarem Namen (3 davon erhielten erst einen Tooltip);
  9 Player-Tooltips nennen ihre Taste.
- M4: Suchfeld in den Einstellungen (17 Gruppen, 6 Reiter, umlaut-tolerant).
- Offen bleibt L1 (Abstands-Raster) — bewusst, siehe Abschnitt 5.
```

- [ ] **Step 3: Commit**

```bash
git add CLAUDE.md docs/DESIGN-AUDIT-2026-09-03.md
git commit -m "Doku: Feedback, Bedienbarkeit und Einstellungssuche als Regeln festgehalten" -m "Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

## Abnahme durch Pascal (nicht automatisierbar)

Nach Task 7 SewerStudio neu starten und pruefen — das kann kein Test:

1. *Export → Haltungen exportieren*: Toast unten rechts mit Link „Ordner öffnen"; Klick oeffnet den Explorer mit markierter Datei.
2. *Import*: Nach dem Lauf Toast „Import abgeschlossen — Bericht liegt bereit." mit „Bericht öffnen".
3. *Dossiers*: Seite baut sich gestaffelt auf; Fotokarten heben sich beim Zeigen leicht an (mit „Animationen reduzieren" in den Einstellungen: ruhig).
4. *Player*: Tooltip auf Abspielen zeigt „— Leertaste".
5. *Einstellungen*: „fotos" tippen → nur die Gruppe „Haltungsprotokoll (PDF)" bleibt, Reiter „Allgemein" wird gewaehlt, rechts steht „1 Gruppe".
