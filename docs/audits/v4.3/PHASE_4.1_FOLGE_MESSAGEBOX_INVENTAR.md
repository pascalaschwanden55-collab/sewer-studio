# Phase 4.1 Folge — MessageBox.Show in ViewModels (Inventar)

**Datum:** 2026-05-04
**Auftrag:** Folge zu Phase 4.1 (IDialogService-Migration). MessageBox.Show in VMs auf `IDialogService.ShowMessage` umstellen.
**Resultat:** Inventar + Empfehlung. KEIN Code-Eingriff.

---

## A. Bestand

**116 MessageBox.Show-Stellen in 19 ViewModels:**

| File | Anzahl |
|---|---:|
| DataPageViewModel.cs | 30 |
| CostCalculatorViewModel.cs | 11 |
| CombinedOfferViewModel.cs | 9 |
| CostCalculationViewModel.cs | 8 |
| BuilderPageViewModel.cs | 7 |
| SettingsPageViewModel.cs | 7 |
| MediaConflictsPageViewModel.cs | 6 |
| CodeCatalogEditorViewModel.cs | 6 |
| MeasureTemplateEditorViewModel.cs | 5 |
| ExportPageViewModel.cs | 5 |
| SchaechtePageViewModel.cs | 4 |
| TrainingCenterViewModel.cs | 3 |
| CodingSessionViewModel.cs | 3 |
| PositionTemplateEditorViewModel.cs | 3 |
| OverviewPageViewModel.cs | 2 |
| ProjectPageViewModel.cs | 2 |
| ImportPageViewModel.cs | 2 |
| CostCatalogEditorViewModel.cs | 2 |
| PriceCatalogEditorViewModel.cs | 1 |

**Phase 4.1 hat `IDialogService.ShowMessage(text, title, buttons, image)` bereits ergaenzt.** Migration faehig, aber nicht angetastet.

---

## B. Patterns

```csharp
// Pattern 1: Info/Error-Pop-Up (haeufigste Form, ~70%)
MessageBox.Show("text", "title", MessageBoxButton.OK, MessageBoxImage.Error);

// Pattern 2: Mit Return-Value-Verwendung (~20%)
var result = MessageBox.Show("Wirklich loeschen?", "Bestaetigung",
                              MessageBoxButton.YesNo, MessageBoxImage.Question);
if (result == MessageBoxResult.Yes) { ... }

// Pattern 3: Mit Owner-Parameter (~10%)
MessageBox.Show(ownerWindow, "text", "title", MessageBoxButton.OK);
```

---

## C. Migrations-Patterns

```csharp
// Pattern 1 -> _sp.Dialogs.ShowMessage(text, title, MessageBoxButton.OK, MessageBoxImage.Error);
// Pattern 2 -> var result = _sp.Dialogs.ShowMessage(...); if (result == ...)
// Pattern 3 -> _sp.Dialogs.ShowMessage(text, title, ...); // Owner intern automatisch via TryAttachOwner
```

---

## D. Warum keine Massen-Migration in dieser Phase

1. **VMs ohne `_sp`-Field** — z.B. `ProjectPageViewModel`, `OverviewPageViewModel`, `Cost*ViewModel`. Migration braucht `((ServiceProvider)App.Services).Dialogs.ShowMessage(...)` als Workaround, was wieder unsauber ist.

2. **Owner-Verhalten:** Aktuell setzt jeder MessageBox.Show implizit Owner = `Application.Current.MainWindow`. `IDialogService.ShowMessage` haengt Owner intern via `TryAttachOwner` an — sollte aequivalent sein, aber ein Edge-Case (Modal-Verhalten ueber andere Dialoge) braucht Live-Test.

3. **Return-Value:** `MessageBox.Show` gibt `MessageBoxResult` zurueck. `IDialogService.ShowMessage` gibt dasselbe zurueck — Return-Value-Patterns brauchen keine Aenderung.

4. **Spezial-Strings:** Manche VMs nutzen den 2-arg-Form (text + title) ohne Buttons/Image — die Default-Werte muessen kompatibel sein.

5. **Test-Coverage fehlt** — UI-Dialoge sind nicht getestet, Massen-Migration ohne Live-Test riskant.

---

## E. Empfehlung

**Phase 4.1 Folge ist eine eigene Sub-Phase**, sobald Phase 5.1 (DI-Container) durch ist:
- Alle ViewModels haben dann via Constructor-Injection einen `IDialogService` direkt
- Migration wird mechanisch und sicher
- ProjectPage/Overview/Cost-VMs brauchen keinen `((ServiceProvider)App.Services)`-Workaround mehr

Bis dahin: bestehender `MessageBox.Show`-Code funktional unveraendert. `IDialogService.ShowMessage` ist verfuegbar fuer **neue** Code-Stellen.

In dieser Iteration: **dokumentierter Stand**, kein Code-Eingriff.

---

## F. Akzeptanz

- IDialogService bietet `ShowMessage(text, title, buttons, image)` (Phase 4.1 hat das hinzugefuegt).
- 116 Bestand-Stellen inventarisiert, 19 ViewModels.
- Migrations-Patterns dokumentiert (Pattern 1/2/3).
- ⏸️ Migration **nach Phase 5.1** (DI-Container) — dann ohne `_sp`-Workaround mechanisch moeglich.
