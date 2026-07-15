# Verteilung Normal/Sanierung — Umsetzungsplan

> **Für die Umsetzung:** TDD, ein Test zuerst, kleine Commits. Schritte als Checkboxen. Design-Grundlage: [VERTEILUNG-NORMAL-SANIERUNG-DESIGN-2026-07-15.md](VERTEILUNG-NORMAL-SANIERUNG-DESIGN-2026-07-15.md).

**Ziel:** Für Haltungen und Schächte je eine Normal- und eine Sanierungs-Verteilung; Sanierung schiebt eine feste Ordner-Ebene `{Datum}_{Objekt}_Saniert {Jahr}` ein (Video-Zuordnung bleibt). DP nur Normal. Verteil-Seite übersichtlicher (grafischer Ordnerbaum + Umschalter, Baukasten in „Erweitert").

**Architektur:** Sanierung als **Modus** (Enum `DistributionVariant { Normal, Sanierung }`), der durch den Verteil-Aufruf bis in `DistributionDirectoryTreeController.ResolveObjectFolder` gereicht wird und dort die eine Zwischen-Ebene anhängt. Kein zweiter Konfig-Baum. UI: neues `DistributionTreePreviewControl` + Umschalter/Erweitert in der bestehenden Karte.

**Tech-Stack:** WPF/.NET 10, MVVM (CommunityToolkit), xUnit. Schichten: Application (Resolver/Verträge), Infrastructure (Distributor), UI (VM/XAML/Control).

## Global Constraints (aus Spec + CLAUDE.md)

- Video-Zuordnung: fester Objektordner + Dateiname unverändert — Sanierung **rahmt nur ein** (eine Ebene tiefer).
- DP hat **keine** Sanierungs-Variante. Excel-Export unverändert.
- `{Jahr}` = Jahr des Inspektionsdatums; existiert bereits im `DistributionPatternResolver` (Zeile 51), Fallback bei fehlendem Datum: leer → Ordner ohne Jahr.
- Sanierungs-Ordnername exakt: `{Datum}_{Objekt}_Saniert {Jahr}` (Leerzeichen vor Jahr), z. B. `20260715_80454_Saniert 2026`.
- Neue Logik als Service/Enum mit Test; DI im `ServiceProvider`; keine hartkodierten Farben; UI-Text/Kommentare Deutsch.
- Nach jeder XAML/VM-Änderung Bindings prüfen. Build + fokussierte Tests grün.

---

### Task 1: Sanierungs-Variante + Ordner-Einschub im Baum-Resolver

**Files:**
- Create: `src/AuswertungPro.Next.Application/Export/DistributionVariant.cs`
- Modify: `src/AuswertungPro.Next.Application/Export/DistributionDirectoryTreeResolver.cs`
- Test: `tests/AuswertungPro.Next.Infrastructure.Tests/DistributionSanierungPathTests.cs`

**Interfaces:**
- Produces: `enum DistributionVariant { Normal, Sanierung }`; neue Überladung `ResolveObjectDirectory(root, ordnerPattern, unterordnerPattern, objektordnerPattern, context, variant, sanierungDateiPattern)` — im Sanierungs-Modus wird `ResolveSegment(sanierungDateiPattern + "_Saniert {Jahr}", context)` als zusätzliche letzte Ebene angehängt.

- [ ] **Step 1: Enum anlegen**
```csharp
namespace AuswertungPro.Next.Application.Export;

/// <summary>Verteil-Variante: Normal (PDF direkt) oder Sanierung (eine Ebene tiefer).</summary>
public enum DistributionVariant
{
    Normal,
    Sanierung
}
```

- [ ] **Step 2: Failing test schreiben** (`DistributionSanierungPathTests.cs`)
```csharp
using AuswertungPro.Next.Application.Export;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class DistributionSanierungPathTests
{
    private static readonly DistributionPatternContext Ctx = new(
        Datum: new System.DateTime(2026, 7, 15),
        Schachtnummer: "80454");

    [Fact]
    public void Normal_endet_am_Objektordner()
    {
        var r = new DistributionDirectoryTreeResolver();
        var pfad = r.ResolveObjectDirectory(@"C:\Ziel", null, null, "{Schachtnummer}", Ctx,
            DistributionVariant.Normal, "{Datum}_{Schachtnummer}");
        Assert.EndsWith(@"80454", pfad);
    }

    [Fact]
    public void Sanierung_haengt_Saniert_Jahr_Ebene_an()
    {
        var r = new DistributionDirectoryTreeResolver();
        var pfad = r.ResolveObjectDirectory(@"C:\Ziel", null, null, "{Schachtnummer}", Ctx,
            DistributionVariant.Sanierung, "{Datum}_{Schachtnummer}");
        Assert.EndsWith(@"80454\20260715_80454_Saniert 2026", pfad);
    }
}
```

- [ ] **Step 3: Test rot** — `dotnet test tests/AuswertungPro.Next.Infrastructure.Tests --filter DistributionSanierungPathTests` → FAIL (Überladung fehlt).

- [ ] **Step 4: Resolver erweitern** — neue Überladung in `DistributionDirectoryTreeResolver`; alte delegiert mit `Normal`:
```csharp
public string ResolveObjectDirectory(
    string root, string? ordnerPattern, string? unterordnerPattern,
    string objektordnerPattern, DistributionPatternContext context)
    => ResolveObjectDirectory(root, ordnerPattern, unterordnerPattern,
        objektordnerPattern, context, DistributionVariant.Normal, null);

public string ResolveObjectDirectory(
    string root, string? ordnerPattern, string? unterordnerPattern,
    string objektordnerPattern, DistributionPatternContext context,
    DistributionVariant variant, string? sanierungDateiPattern)
{
    // ... bestehender Aufbau bis inkl. Objektordner ...
    if (variant == DistributionVariant.Sanierung)
    {
        var basis = _patternResolver.ResolveSegment(sanierungDateiPattern, context);
        var sanierungOrdner = string.IsNullOrWhiteSpace(basis)
            ? "Saniert"
            : $"{basis}_Saniert {_patternResolver.ResolveSegment("{Jahr}", context)}".TrimEnd();
        segmente.Add(ProjectPathResolver.SanitizePathSegment(sanierungOrdner));
    }
    return Path.Combine(segmente.ToArray());
}
```
Interface `IDistributionDirectoryTreeResolver` um die neue Überladung ergänzen.

- [ ] **Step 5: Test grün** — Filter-Lauf → PASS.
- [ ] **Step 6: Commit** — `git add` die drei Dateien; `git commit -m "feat(export): Sanierungs-Ordner-Ebene im Verteil-Baum-Resolver"`.

---

### Task 2: Modus durch die Verteil-Controller reichen

**Files:**
- Modify: `src/AuswertungPro.Next.Infrastructure/HoldingDistribution/DistributionDirectoryTreeController.cs` (Signatur `ResolveObjectFolder` um `variant`, `sanierungDateiPattern`)
- Modify: `src/AuswertungPro.Next.Infrastructure/HoldingDistribution/ParsedShaftDistributionController.cs` und `ParsedHoldingDistributionController.cs` (Modus durchreichen)
- Modify: `src/AuswertungPro.Next.Infrastructure/HoldingFolderDistributor*.cs` (öffentliche Verteil-Methoden um `DistributionVariant variant = Normal`)
- Test: erweitern `DistributionSanierungPathTests` um einen Controller-Pfadtest (falls `ResolveObjectFolder` intern testbar), sonst über den bestehenden Distributor-Test.

**Interfaces:**
- Consumes: `DistributionVariant`, neue Resolver-Überladung aus Task 1.
- Produces: `DistributionDirectoryTreeController.ResolveObjectFolder(root, config, context, objektPattern, variant, sanierungDateiPattern)`.

- [ ] **Step 1** `ResolveObjectFolder` in `DistributionDirectoryTreeController` um `DistributionVariant variant = Normal` + `string? sanierungDateiPattern = null` erweitern und an die neue Resolver-Überladung durchreichen (Default Normal → bestehendes Verhalten unverändert).
- [ ] **Step 2** In `ParsedShaftDistributionController.Distribute` und `ParsedHoldingDistributionController.Distribute` einen `DistributionVariant variant`-Parameter ergänzen; beim `ResolveObjectFolder`-Aufruf durchreichen mit `sanierungDateiPattern: "{Datum}_{Schachtnummer}"` bzw. `"{Datum}_{Haltung}"`. **Alles danach unverändert** (PDF-Name, Video-Zuordnung nutzen weiter `shaftFolder`/`holdingFolder`, jetzt eine Ebene tiefer).
- [ ] **Step 3** Öffentliche Verteil-Einstiegspunkte im `HoldingFolderDistributor` um `DistributionVariant variant = DistributionVariant.Normal` erweitern und bis zu den Controllern durchreichen.
- [ ] **Step 4** Build grün: `dotnet build AuswertungPro.sln`. Bestehende Distributor-Tests grün (Normal-Verhalten unverändert).
- [ ] **Step 5** Neuer Verhaltenstest: eine Schacht-PDF im Sanierungs-Modus verteilen → PDF liegt unter `…/80454/20260715_80454_Saniert 2026/20260715_80454.pdf`. (Muster analog eines bestehenden Distributor-Tests; Ort beim Umsetzen aus `tests/AuswertungPro.Next.Infrastructure.Tests` übernehmen.)
- [ ] **Step 6: Commit** — `git commit -m "feat(export): Verteil-Controller reichen Normal/Sanierung-Modus durch"`.

---

### Task 3: ViewModel — 5 Commands, Vorschau je Variante, Umschalter, Erweitert

**Files:**
- Modify: `src/AuswertungPro.Next.UI/ViewModels/Pages/DistributionTargetConfigViewModel.cs`
- Modify: `src/AuswertungPro.Next.UI/ViewModels/Pages/ExportPageViewModel.cs`
- Test: `tests/AuswertungPro.Next.UI.Tests/DistributionTargetVariantPreviewTests.cs`

**Interfaces:**
- Consumes: `DistributionVariant`, Resolver-Überladung.
- Produces (auf `DistributionTargetConfigViewModel`): `bool SupportsSanierung`, `[ObservableProperty] DistributionVariant _previewVariant`, `[ObservableProperty] bool _isAdvancedExpanded`, `IReadOnlyList<DistributionTreeNode> TreeNodes` (aktualisiert bei Umschalten); (auf `ExportPageViewModel`): `DistributeHoldingsNormalCommand`, `DistributeHoldingsSanierungCommand`, `DistributeShaftsNormalCommand`, `DistributeShaftsSanierungCommand`, `DistributeDichtheitCommand`.

- [ ] **Step 1** `DistributionTreeNode` (Record) anlegen: `record DistributionTreeNode(string Label, DistributionTreeNodeKind Kind, int Depth)` mit `enum DistributionTreeNodeKind { Ordner, Pdf, Video }` (in UI/ViewModels/Pages).
- [ ] **Step 2: Failing test** — `DistributionTargetVariantPreviewTests`: VM mit `SupportsSanierung=true`, `PreviewVariant=Sanierung` → `Vorschau` enthält `_Saniert 2026`; `TreeNodes` enthält einen `Ordner`-Knoten mit `_Saniert`. Bei `Normal` nicht.
- [ ] **Step 3: rot** — Filter-Lauf FAIL.
- [ ] **Step 4** VM erweitern: Konstruktor-Flag `supportsSanierung`; `OnPreviewVariantChanged` → `UpdateVorschau()` + `UpdateTreeNodes()`. `UpdateVorschau` im Verteil-Zweig bei `PreviewVariant==Sanierung` die Sanierungs-Ebene via Resolver-Überladung einschieben. `UpdateTreeNodes` baut die Icon-Baum-Knoten (freie Ebenen → Objektordner → [Sanierung-Ordner] → PDF, Video). DP-Karte: `supportsSanierung=false`.
- [ ] **Step 5** `ExportPageViewModel.BuildDistributionTargets`: Haltung/Schacht mit `supportsSanierung: true`, DP `false`. Die drei alten Commands zu fünf: je Haltung/Schacht ein Normal- und ein Sanierung-Command, die den bestehenden Verteil-Aufruf mit `DistributionVariant` aufrufen; DP unverändert.
- [ ] **Step 6: grün** + XAML-Binding-Check gegen die neuen Properties.
- [ ] **Step 7: Commit** — `git commit -m "feat(export): ViewModel mit Normal/Sanierung-Vorschau, Baum-Knoten und 5 Commands"`.

---

### Task 4: Grafisches Ordnerbaum-Control

**Files:**
- Create: `src/AuswertungPro.Next.UI/Controls/DistributionTreePreviewControl.xaml` (+ `.xaml.cs`)
- Test: `tests/AuswertungPro.Next.UI.Tests/DistributionTreePreviewControlBindingTests.cs` (Quelltext-Guard: DependencyProperty `Nodes` vorhanden, FluentIcon-Glyphen genutzt)

**Interfaces:**
- Consumes: `IReadOnlyList<DistributionTreeNode>` aus Task 3.
- Produces: `DistributionTreePreviewControl.NodesProperty` (DependencyProperty), Darstellung als eingerückte Icon-Zeilen (📁/📄/🎬 = FluentIcon `&#xE8B7;`/`&#xE8A5;`/`&#xE714;`).

- [ ] **Step 1** UserControl mit `ItemsControl` über `Nodes`; je Knoten `Margin.Left = Depth*16`, `ui:FluentIcon` nach `Kind`, Label. Theme-Brushes (`CardBrush`, `MutedBrush`, `AccentBrush`). Keine hartkodierten Farben.
- [ ] **Step 2** `NodesProperty` als `DependencyProperty` (Typ `IReadOnlyList<DistributionTreeNode>`).
- [ ] **Step 3** Guard-Test: Control-XAML enthält `FluentIcon` und `Nodes`-Binding (`ArchitectureSourceGuard`-Stil).
- [ ] **Step 4** Build grün.
- [ ] **Step 5: Commit** — `git commit -m "feat(ui): grafisches Ordnerbaum-Control fuer die Verteil-Vorschau"`.

---

### Task 5: ExportPage-Redesign (Umschalter, Baum, Erweitert, Menü)

**Files:**
- Modify: `src/AuswertungPro.Next.UI/Views/Pages/ExportPage.xaml`

- [ ] **Step 1** „Verteilen"-ContextMenu auf 5 Einträge umbauen (Haltungen Normal/Sanierung, Trenner, Schächte Normal/Sanierung, Trenner, Dichtheitsprüfung) mit den neuen Commands + FluentIcon-Glyphen.
- [ ] **Step 2** In der Verteil-Karte (`DistributionTargets`-ItemTemplate): oben ein Normal|Sanierung-Umschalter (zwei ToggleButtons/RadioButtons an `PreviewVariant`), nur sichtbar wenn `SupportsSanierung`.
- [ ] **Step 3** Den grafischen Baum einfügen: `<ctrl:DistributionTreePreviewControl Nodes="{Binding TreeNodes}"/>` als primäre Anzeige unter dem Umschalter.
- [ ] **Step 4** Den bestehenden Baustein-Baukasten (die zwei freien Ebenen + Zurück/Leeren + Feinbearbeitung + Dateiname-Chips) in einen `Expander Header="Erweitert"` mit `IsExpanded="{Binding IsAdvancedExpanded}"` verschieben (Default zu). Ziel-Wurzel-Feld und Live-Vorschau bleiben außerhalb sichtbar.
- [ ] **Step 5** Sicht-Check (durch Pascal): hell + dunkel, Umschalten aktualisiert Baum + Vorschau, DP ohne Umschalter, Menü löst je Variante korrekt aus.
- [ ] **Step 6: Commit** — `git commit -m "feat(ui): Verteil-Seite mit Ordnerbaum, Normal/Sanierung-Umschalter und Erweitert-Bereich"`.

---

## Self-Review

- **Spec-Deckung:** Sanierungs-Ebene (T1/T2), Jahr aus Datum (bereits im Resolver, T1 nutzt {Jahr}), Video bleibt (T2, Ordner nur tiefer), Layout Umschalter+Baum+Erweitert (T3–T5), 5 Menüeinträge (T5), DP nur Normal (T3 `supportsSanierung=false`, T5 kein DP-Sanierung-Eintrag). ✓
- **Platzhalter:** keine offenen TBD; die „Ort beim Umsetzen übernehmen"-Hinweise in T2/T3 sind bewusste Verweise auf vorhandene Testmuster, kein fehlender Inhalt.
- **Typkonsistenz:** `DistributionVariant`, `ResolveObjectDirectory(..., variant, sanierungDateiPattern)`, `ResolveObjectFolder(..., variant, sanierungDateiPattern)`, `DistributionTreeNode`/`DistributionTreeNodeKind`, `PreviewVariant`/`SupportsSanierung`/`IsAdvancedExpanded`/`TreeNodes` — durchgängig gleich benannt. ✓
