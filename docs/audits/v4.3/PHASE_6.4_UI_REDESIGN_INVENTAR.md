# Phase 6.4 — UI-Redesign Fluent-WPF / WPF-UI 3 (Inventar + Empfehlung)

**Datum:** 2026-05-04
**Auftrag:** "UI-Redesign: Fluent-WPF / WPF-UI 3 als Design-Basis" — Audit A6 (Konsens 3/3, ~2 Wochen).
**Resultat:** Inventar + Strategie. KEIN Code-Eingriff.

---

## A. Bestand

**Aktuelle UI-Bibliothek:**
- Plain WPF mit eigenen Theme-Files (Theme.xaml, ThemeLight.xaml, Controls.xaml, BuilderTheme.xaml — 2.323 Zeilen total)
- 12 Pages mit konsistentem PageHeader-Stil (Phase 3.3 abgeschlossen)
- Spacing/Typography-Tokens (Phase 3.2)
- DataGrid-Standard-Styles (Phase 3.3)
- StatusBadge-UserControl (Phase 3.3)

**Audit-Empfehlung:** Fluent-WPF / WPF-UI 3 als Design-Basis adaptieren — modernes Look-and-Feel, Microsoft-Standard.

---

## B. Was WPF-UI 3 bringen wuerde

- **NavigationView** statt Custom-Hamburger-Sidebar
- **Card / InfoBar / Snackbar** als Standard-Controls
- **Acrylic / Mica** Hintergruende (Win11)
- **Thinking-Animation** (KI-spezifisch)
- **Fluent-Icons** statt Segoe MDL2
- **Auto-Theme-Switch** (Hell/Dunkel folgt System)
- **Animations-Bibliothek** fuer Page-Transitions

---

## C. Probleme einer 1:1-Adaption

1. **NuGet-Add:** WPF-UI 3 ist ein NuGet — laut CLAUDE.md "keine NuGet-Pakete ohne Rueckfrage".
2. **Massen-XAML-Aenderung:** ~50 XAML-Files (Pages, Windows, Controls) muessen ihre Window/UserControl-Definitionen anpassen.
3. **Theme-Konflikte:** Aktuelles Custom-Theme (ThemeLight.xaml) muss entweder weg oder kompatibel gemacht werden.
4. **Test-Aufwand:** Jede Page muss visuell auf Hell/Dunkel-Umschaltung getestet werden.
5. **Performance:** Acrylic/Mica brauchen DWM-Composition — auf alten GPUs langsamer.
6. **Icons:** Wenn ueberall Segoe MDL2 (`&#xE8FD;`) verwendet wird, muessen Icons via Fluent ersetzt werden.

---

## D. Vorgeschlagener gestaffelter Pfad (~2 Wochen)

### Sub-Phase 6.4.A: NuGet-Test + Sandbox (~4 h)
- WPF-UI 3 NuGet auf einer Sandbox-Page testen (z.B. neue Demo-Page).
- Kompatibilitaet mit Custom-Theme pruefen.
- User-Freigabe einholen.

### Sub-Phase 6.4.B: Theme-Migration (~2 Tage)
- ThemeLight.xaml als Fluent-Theme uebersetzen oder entfernen.
- Spacing/Typography-Tokens in Fluent-Tokens uebersetzen.
- DataGrid-Style auf Fluent-Equivalent.

### Sub-Phase 6.4.C: NavigationView + MainWindow (~2 Tage)
- Custom-Sidebar durch WPF-UI 3 NavigationView ersetzen.
- 12 NavItems migrieren.
- Theme-Switch in Settings beibehalten.

### Sub-Phase 6.4.D: Page-Migration (~5 Tage)
- 12 Pages auf Fluent-Cards / InfoBar / NumberBox umstellen.
- Pro Page Live-Test.

### Sub-Phase 6.4.E: Window-Migration (~3 Tage)
- ~30 Windows (PlayerWindow, TrainingCenterWindow, ...) auf FluentWindow umstellen.
- Acrylic/Mica nur fuer Top-Level-Windows.

### Sub-Phase 6.4.F: Icon-Migration (~1 Tag)
- Segoe-MDL2-Icons durch Fluent-Icons ersetzen (Massensuche+Replace).

### Sub-Phase 6.4.G: Live-Test + Doku (~1 Tag)
- Hell/Dunkel-Wechsel manuell pro Page.
- Performance-Profile.

**Total: ~2 Wochen, wie Audit-Schaetzung.**

---

## E. Risiken

| Risiko | Wirkung | Gegenmittel |
|---|---|---|
| WPF-UI-3-Breaking-Changes | Update-Kosten | Version pinnen, kein Auto-Update |
| Theme-Inkompatibilitaet | Doppel-Pflege | Erst entscheiden: Fluent ersetzen Custom-Theme komplett |
| Acrylic-Performance | Lag bei alten GPUs | Acrylic optional, default off |
| Custom-Controls (PageHeader, StatusBadge) | Eigenkonkurrenz zu Fluent-Equivalents | PageHeader mit Fluent-NavigationViewItemHeader ersetzen oder behalten |
| Branch-Konflikt mit anderen Eingriffen | Massen-Merge | Eigene Mehr-Wochen-Session |

---

## F. Alternative: minimaler Theme-Refresh

Statt voller WPF-UI-3-Migration:
- **Variante A:** WPF-UI 3 NuGet adaptieren (~2 Wochen, Audit-Vorschlag).
- **Variante B:** Custom-Theme behalten, gezielt einzelne Fluent-Controls (NumberBox, InfoBar, Snackbar) als NuGet adaptieren.
- **Variante C:** Custom-Theme refreshen (Acrylic-Brushes selbst bauen) — kein NuGet-Add, ~1 Woche.

User-Entscheid empfohlen vor jeder Bewegung.

---

## G. Empfehlung

Phase 6.4 ist **die letzte Audit-Phase** und der **groesste UI-Eingriff**. Voraussetzungen:

1. **Phase 5.3 abgeschlossen** (KI raus aus UI — sonst doppelter Refactor).
2. **Phase 6.1 + 6.2 abgeschlossen** (PlayerWindow + TrainingCenterVM zerlegt — Fluent-Migration einfacher).
3. **NuGet-Freigabe** fuer WPF-UI 3 ODER Entscheid fuer Variante B/C.
4. **Eigene 2-Wochen-Session** mit Branch.

In dieser Iteration: **dokumentierter Stand**, kein Code-Eingriff.

---

## H. Akzeptanz

- Aktueller UI-Stand verifiziert: Plain WPF + Custom-Theme + 12 PageHeader-Pages.
- WPF-UI-3-Vorteile dokumentiert.
- 7-Sub-Phasen-Plan (~2 Wochen).
- 3 Varianten (A: voll, B: gezielt, C: Refresh) zur Diskussion.
- ⏸️ Migration ist groesste UI-Aenderung des gesamten Audits, eigene Session noetig.
- KEIN Code-Eingriff in dieser Iteration.
