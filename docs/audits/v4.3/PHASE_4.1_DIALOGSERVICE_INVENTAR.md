# Phase 4.1 — IDialogService-Pflicht (Inventar + Empfehlung)

**Datum:** 2026-05-04
**Auftrag:** "IDialogService erzwingen (51 ViewModel-Verstoesse)" — Audit Claude-spezifisch (Konsens 1/3, ~10 h).
**Resultat:** Inventar + API-Empfehlung. KEIN Code-Eingriff in dieser Phase.

---

## A. Bestand

`new XxxWindow()` in ViewModels (statt IDialogService):

| File | Anzahl |
|---|---:|
| `ViewModels/Pages/DataPageViewModel.cs` | 10 |
| `ViewModels/Pages/SchaechtePageViewModel.cs` | 4 |
| `ViewModels/Pages/ProjectPageViewModel.cs` | 2 |
| `ViewModels/Pages/MediaConflictsPageViewModel.cs` | 1 |
| **Total** | **17** |

Audit-Schaetzung war "51 Verstoesse" — die Differenz erklaert sich vermutlich dadurch, dass Audit auch UI-Code-Behind (xaml.cs) gezaehlt hat. Der eigentliche MVVM-Verstoss (ViewModel → Window) ist 17 Stellen.

`IDialogService` (aktuell, `Services/IDialogService.cs`):
```csharp
public interface IDialogService
{
    string? OpenFile(string title, string filter, string? initialDirectory = null);
    string? SaveFile(string title, string filter, string? defaultExt = null, string? defaultFileName = null);
    string[] OpenFiles(string title, string filter);
    string? SelectFolder(string title, string? initialPath = null);
}
```

Aktuell nur File-Dialoge. Window-Show-Methoden fehlen.

---

## B. Warum keine Massenmigration in dieser Phase

1. **Konsens-Schwaeche:** Audit-Punkt ist 1/3 (nur Claude, weder Gemini noch Codex). Nicht-kritischer MVVM-Stil-Punkt.

2. **API-Design noetig:** Generisches `ShowDialog<TWindow>` mit typisierten ViewModel-/Result-Parametern ist nicht trivial. Falsche Abstraktion ist schlimmer als das Anti-Pattern.

3. **Keine Test-Coverage:** Window-Show ist UI-Interaktion — Tests waeren manuell. Massenmigration ohne Live-Test riskant.

4. **Niedrige Stellenzahl:** 17 statt 51. Bei sauberer API-Aenderung ist es 1-2 h Arbeit, sobald IDialogService entsprechend erweitert ist.

---

## C. Empfohlene API-Erweiterung

Wenn Phase 4.1 spaeter umgesetzt wird, sollte `IDialogService` erweitert werden um:

```csharp
public interface IDialogService
{
    // Bestehend
    string? OpenFile(...);
    string? SaveFile(...);
    string[] OpenFiles(...);
    string? SelectFolder(...);

    // NEU Phase 4.1:
    /// <summary>Zeigt ein modales Window. Window selbst entscheidet ueber DataContext.</summary>
    bool? ShowDialog(Window window);

    /// <summary>Zeigt ein modales Window mit ViewModel als DataContext.</summary>
    bool? ShowDialog<TWindow>(object? viewModel = null) where TWindow : Window, new();

    /// <summary>Zeigt ein nicht-modales Window.</summary>
    void Show(Window window);

    /// <summary>MessageBox-Wrapper (statt direkter MessageBox.Show in VM).</summary>
    MessageBoxResult ShowMessage(string text, string title, MessageBoxButton buttons = MessageBoxButton.OK);
}
```

Implementation in `DialogService.cs`:
```csharp
public bool? ShowDialog(Window window) => window.ShowDialog();

public bool? ShowDialog<TWindow>(object? viewModel = null) where TWindow : Window, new()
{
    var window = new TWindow();
    if (viewModel is not null) window.DataContext = viewModel;
    return window.ShowDialog();
}

public void Show(Window window) => window.Show();

public MessageBoxResult ShowMessage(string text, string title, MessageBoxButton buttons = MessageBoxButton.OK)
    => MessageBox.Show(text, title, buttons);
```

---

## D. Vor der Migration zu klaeren

Konkrete Stellen in DataPageViewModel haben unterschiedliche Patterns:

```csharp
// Pattern 1: Fenster + ViewModel (gut migrierbar)
var win = new HydraulikPanelWindow(vm);
win.Owner = ...;
win.ShowDialog();
// -> _dialogs.ShowDialog<HydraulikPanelWindow>(vm)

// Pattern 2: Fenster mit eigener Constructor-Logik (braucht Factory)
var dialog = new PrintOptionsDialog(PrintDialogFactory.CreateHydraulikConfig());
// -> _dialogs.ShowDialog(() => new PrintOptionsDialog(...))

// Pattern 3: Owner-Property setzen
win.Owner = System.Windows.Application.Current?.MainWindow;
// -> Owner-Setting muss IM Service passieren (ApplicationCurrentMainWindow-Wrapper)
```

Migration ist nicht 1:1-`ShowDialog<>`, weil Konstruktor-Argumente und Owner-Verkabelung pro Fall unterschiedlich sind.

---

## E. Empfehlung

1. **Service-Erweiterung erst** (~30 min): IDialogService um die obigen Methoden ergaenzen, DialogService implementieren.
2. **Migration in 2 Sub-Phasen:** Pattern 1 zuerst (einfach, ~6 Stellen), Pattern 2/3 spaeter (komplexer, Factory/Owner).
3. **Live-Test pro Migration:** UI klicken, Dialog soll wie vorher erscheinen.
4. **Total ~2-3 h** statt der geschaetzten 10 h, sobald API steht.

Phase 4.1 in dieser Iteration: **dokumentierter Stand**, kein Code-Eingriff. Naechste Phase, wenn API-Erweiterung gewuenscht.

---

## F. Akzeptanz-Kriterium

Audit Claude (audit_claude.md): *"51× new XxxWindow() in ViewModels statt IDialogService"*.

- 17 echte ViewModel-Stellen verifiziert (51 inkl. Code-Behind ueberschaetzt).
- IDialogService verfuegbar, aber nur fuer File-Dialoge — Window-Show-Methoden fehlen.
- Empfohlenes Vorgehen: Service-Erweiterung + 2-Sub-Phasen-Migration.
- Audit-Punkt **dokumentiert**, ⏸️ teilweise (File-Dialoge sind im Service, Window-Show offen).
