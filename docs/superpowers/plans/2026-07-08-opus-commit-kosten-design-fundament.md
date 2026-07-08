# Commit-Sicherung, Kostenregel-Rest & Design-Fundament — Implementierungsplan

> **Für agentische Ausführung:** ERFORDERLICHES SUB-SKILL: `superpowers:executing-plans` (oder `superpowers:subagent-driven-development`), Task für Task. Schritte nutzen Checkbox-Syntax (`- [ ]`).
>
> **Modell-Hinweis:** Dieser Plan wurde von Fable geschrieben und ist für die Ausführung durch Opus gedacht. Alle Entscheidungen sind getroffen — bei Abweichungen zwischen Plan und Realität: anhalten und den Nutzer fragen statt improvisieren.

**Ziel:** (A) Den gesamten uncommitteten Arbeitsstand in 5 thematische Commits sichern, (B) die zwei offenen Tasks der Sanieren=Ja-Kostenregel abschliessen, (C) das Design-Fundament mechanisch vorbereiten (Theme-Parität, Monospace, Severity-Farben) — OHNE gestalterische Neuentwürfe.

**Architektur:** Keine neuen Subsysteme. Phase A ist reine Git-Arbeit. Phase B folgt einem existierenden Detail-Plan. Phase C ergänzt Theme-Ressourcen und ersetzt hartcodierte Farben durch vorhandene `DynamicResource`-Brushes.

**Tech-Stack:** WPF/.NET 10, xUnit, Git (PowerShell 5.1 — KEIN `&&`, stattdessen `;`).

## Globale Regeln (gelten für jeden Task)

- Sprache: Antworten, Commit-Messages und Code-Kommentare auf **Deutsch** (Umlaute in Commit-Messages als ae/oe/ue schreiben).
- **NICHT pushen.** Nur lokale Commits. Branch bleibt `feature/gis-karte`.
- Vor jedem Build: sicherstellen, dass **SewerStudio.exe nicht läuft** (`Get-Process -Name SewerStudio -ErrorAction SilentlyContinue`) — sonst schlägt der Build mit MSB3027 (gesperrte DLLs) fehl. Falls sie läuft: Nutzer bitten, sie zu schliessen. Nicht selbst killen.
- Build: `dotnet build AuswertungPro.sln` → 0 Fehler erwartet. Tests: `dotnet test AuswertungPro.sln --no-build` → 0 Fehler erwartet (Stand 2026-07-08: 8015 Tests grün, 1 übersprungen ist normal).
- Keine NuGet-Pakete hinzufügen. Kein Refactoring am Bestand über das Beschriebene hinaus.
- Commit-Fusszeile jedes Commits:
  `Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>`
- Gemischte Dateien: Wenn `git diff <datei>` zeigt, dass eine Datei zu ZWEI Themen gehört (z. B. `ServiceProvider.cs` mit Schacht-DI und etwas anderem), die Datei dem dominanten Thema zuordnen und das zweite Thema im Commit-Body erwähnen. KEIN `git add -p` (fehleranfällig, nicht nötig).
- Dateien, die im Plan nicht erwähnt sind, aber in `git status` auftauchen: per `git diff` dem thematisch passendsten Commit zuordnen. Am Ende von Phase A darf `git status --short` nichts Uncommittetes mehr zeigen.

---

## Phase A — Arbeitsstand in 5 thematische Commits sichern

### Task 1: Vorprüfung

- [ ] **Schritt 1:** Prüfen, dass die App nicht läuft: `Get-Process -Name SewerStudio -ErrorAction SilentlyContinue` → keine Ausgabe erwartet.
- [ ] **Schritt 2:** `git status --short` ausführen und mit den Dateilisten der Tasks 2–6 abgleichen. Neue, hier nicht gelistete Dateien notieren (Zuordnungsregel siehe Globale Regeln).
- [ ] **Schritt 3:** `dotnet build AuswertungPro.sln` → 0 Fehler. `dotnet test AuswertungPro.sln --no-build` → 0 Fehler.
- [ ] **Schritt 4:** Nichts committen. Weiter zu Task 2.

### Task 2: Commit 1 — Backup-Versionierung (`_Versionen`)

**Hintergrund:** Die Datensicherung löscht/überschreibt nichts mehr endgültig; ersetzte und entfallene Dateien wandern in datierte Stände unter `_Versionen\`, die letzten 10 Stände bleiben (Rotation). Umgesetzt und getestet am 2026-07-08.

- [ ] **Schritt 1: Exakt diese Dateien stagen**

```powershell
git add src/AuswertungPro.Next.Application/Backup/BackupVersionRetention.cs
git add src/AuswertungPro.Next.Application/Backup/RestoreAnleitungText.cs
git add src/AuswertungPro.Next.Infrastructure/Backup/DirectoryMirror.cs
git add src/AuswertungPro.Next.Infrastructure/Backup/FullBackupService.cs
git add src/AuswertungPro.Next.UI/Settings/SettingsFullBackupPresentationBuilder.cs
git add src/AuswertungPro.Next.UI/Settings/SettingsFullBackupWorkflow.cs
git add tests/AuswertungPro.Next.Infrastructure.Tests/Backup/BackupVersionRetentionTests.cs
git add tests/AuswertungPro.Next.Infrastructure.Tests/Backup/DirectoryMirrorTests.cs
git add tests/AuswertungPro.Next.Infrastructure.Tests/Backup/FullBackupServiceTests.cs
git add tests/AuswertungPro.Next.UI.Tests/SettingsFullBackupPresentationBuilderTests.cs
git add tests/AuswertungPro.Next.UI.Tests/SettingsFullBackupWorkflowTests.cs
```

- [ ] **Schritt 2: Committen**

```powershell
git commit -m @'
feat(backup): Datensicherung loescht nichts mehr endgueltig (_Versionen-Staende)

Ersetzte und entfallene Dateien wandern pro Lauf in _Versionen\<Datum>,
die letzten 10 Staende bleiben (Rotation). Fremde Ordnernamen dort werden
nie geloescht. Neue Regeln in BackupVersionRetention, Mirror verschiebt
statt loescht, RESTORE-ANLEITUNG/Dialogtexte angepasst.

Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>
'@
```

### Task 3: Commit 2 — Schacht-Sanierungstool + Massnahmen-Katalog

**Hintergrund:** Schacht-Sanierungsmatrix (NPK-Kapitel 700), Massnahmen-Katalog mit eigenen Preisen (Rechtsklick in Schachtansicht → Felder „Massnahmen"/„Kosten" → Excel), Kosten-Infrastruktur (Aggregator, LV-Loader, AWU-Eigentums-Filter, DN-Preis-Editor). Gebaut + getestet 2026-07-07.

- [ ] **Schritt 1: Diese Dateien/Ordner stagen** (vorher je `git diff` überfliegen; `ServiceProvider.cs`, `App.xaml`, `ShellViewModel.cs` gehören hierher, wenn ihr Diff Schacht-Registrierung/-Navigation enthält — das ist der erwartete Fall):

```powershell
git add src/AuswertungPro.Next.Application/Schacht/
git add src/AuswertungPro.Next.Application/Cost/CostCatalogDnPriceEditor.cs
git add src/AuswertungPro.Next.Domain/Models/SchachtMassnahmeKatalogEintrag.cs
git add src/AuswertungPro.Next.Infrastructure/Schacht/
git add src/AuswertungPro.Next.Infrastructure/Costs/NpkLeistungsverzeichnisExcelExporter.cs
git add src/AuswertungPro.Next.Infrastructure/Costs/NpkLeistungsverzeichnisExporter.cs
git add src/AuswertungPro.Next.Infrastructure/Costs/ProjectCostStoreRepository.cs
git add src/AuswertungPro.Next.Infrastructure/Costs/ProjectPositionAggregator.cs
git add src/AuswertungPro.Next.Infrastructure/Costs/OwnershipAwuFilter.cs
git add src/AuswertungPro.Next.Infrastructure/Costs/SchachtLvCostLoader.cs
git add src/AuswertungPro.Next.Infrastructure/Costs/SchachtMeasureFactory.cs
git add src/AuswertungPro.Next.UI/App.xaml
git add src/AuswertungPro.Next.UI/ServiceProvider.cs
git add src/AuswertungPro.Next.UI/ViewModels/ShellViewModel.cs
git add src/AuswertungPro.Next.UI/Config/cost_catalog.json
git add src/AuswertungPro.Next.UI/Config/measure_templates.json
git add src/AuswertungPro.Next.UI/Dialogs/CostCatalogEditorDialog.xaml
git add src/AuswertungPro.Next.UI/Dialogs/CostCatalogEditorDialog.xaml.cs
git add src/AuswertungPro.Next.UI/ViewModels/Windows/CostCatalogEditorViewModel.cs
git add src/AuswertungPro.Next.UI/ViewModels/Pages/BuilderPageViewModel.cs
git add src/AuswertungPro.Next.UI/ViewModels/Pages/SanierungsMatrixPageViewModel.cs
git add src/AuswertungPro.Next.UI/ViewModels/Pages/SanierungMatrixOptionDeriver.cs
git add src/AuswertungPro.Next.UI/ViewModels/Pages/SchachtSanierungsMatrixPageViewModel.cs
git add src/AuswertungPro.Next.UI/ViewModels/Windows/SchachtMassnahmenKatalogEditorViewModel.cs
git add src/AuswertungPro.Next.UI/ViewModels/Windows/SchachtMassnahmenViewModel.cs
git add src/AuswertungPro.Next.UI/Views/Pages/SchachtSanierungsMatrixPage.xaml
git add src/AuswertungPro.Next.UI/Views/Pages/SchachtSanierungsMatrixPage.xaml.cs
git add src/AuswertungPro.Next.UI/Views/Windows/SchachtMassnahmenKatalogEditorWindow.xaml
git add src/AuswertungPro.Next.UI/Views/Windows/SchachtMassnahmenKatalogEditorWindow.xaml.cs
git add src/AuswertungPro.Next.UI/Views/Windows/SchachtMassnahmenWindow.xaml
git add src/AuswertungPro.Next.UI/Views/Windows/SchachtMassnahmenWindow.xaml.cs
git add src/AuswertungPro.Next.UI/Views/Pages/Schachtansicht/SchachtansichtView.xaml
git add src/AuswertungPro.Next.UI/Views/Pages/Schachtansicht/SchachtansichtView.xaml.cs
git add tests/AuswertungPro.Next.Infrastructure.Tests/Cost/
git add tests/AuswertungPro.Next.Infrastructure.Tests/Schacht/
git add tests/AuswertungPro.Next.Pipeline.Tests/SchachtEmpfehlungRecordMapperTests.cs
git add tests/AuswertungPro.Next.Pipeline.Tests/SchachtEmpfehlungTextFormatterTests.cs
git add tests/AuswertungPro.Next.UI.Tests/SanierungMatrixOptionDeriverTests.cs
git add tests/AuswertungPro.Next.UI.Tests/SchachtMassnahmenKatalogEditorViewModelTests.cs
git add tests/AuswertungPro.Next.UI.Tests/SchachtMassnahmenViewModelTests.cs
git add tests/AuswertungPro.Next.UI.Tests/SchachtSanierungsMatrixPageViewModelTests.cs
git add docs/superpowers/specs/2026-07-07-schacht-empfohlene-massnahmen-design.md
```

- [ ] **Schritt 2: Committen**

```powershell
git commit -m @'
feat(schacht): Sanierungstool (NPK 700) + Massnahmen-Katalog mit Kosten

Schacht-Sanierungsmatrix mit NPK-Kapitel-700-Positionen, Massnahmen-Katalog
(eigene Liste Name+Preis, Rechtsklick in Schachtansicht) -> Felder
Massnahmen/Kosten -> Excel-Export. Kosten-Infrastruktur: Aggregator-Kapitel,
LV-Loader, AWU-Eigentums-Filter, DN-Preis-Editor.

Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>
'@
```

### Task 4: Commit 3 — QGIS-Bridge: Schächte, „Ausgeführt durch", gestreckte Schäden, Plugin-Port

**Hintergrund:** Bridge-Erweiterung für Schächte (AP1–AP7), neuer Endpunkt `/qgis/sanierungstyp.geojson` (Kategorien Baumeister/Sanierer/Gärtner), Schadens-Meterstände auf Geometrie gestreckt (BCE = Rohrende), Plugin-Stand aus `%APPDATA%` ins Repo portiert.

- [ ] **Schritt 1: Stagen**

```powershell
git add src/AuswertungPro.Next.UI/QgisBridge/QgisBridgeRequestProcessor.cs
git add src/AuswertungPro.Next.UI/QgisBridge/QgisBridgeSelection.cs
git add src/AuswertungPro.Next.UI/QgisBridge/QgisBridgeSnapshotBuilder.cs
git add src/AuswertungPro.Next.UI/QgisBridge/QgisProjectSnapshot.cs
git add src/AuswertungPro.Next.UI/ViewModels/Pages/KarteViewModel.cs
git add src/AuswertungPro.Next.Application/DataPage/AusgefuehrtDurchKategorie.cs
git add integrations/qgis/install-sewerstudio-bridge.ps1
git add integrations/qgis/sewerstudio_bridge/metadata.txt
git add integrations/qgis/sewerstudio_bridge/sewerstudio_bridge.py
git add tests/AuswertungPro.Next.Pipeline.Tests/AusgefuehrtDurchKategorieTests.cs
git add tests/AuswertungPro.Next.UI.Tests/QgisBridgeSelectionTests.cs
git add tests/AuswertungPro.Next.UI.Tests/QgisBridgeSnapshotBuilderTests.cs
git add tests/AuswertungPro.Next.UI.Tests/QgisPluginPackagingTests.cs
```

- [ ] **Schritt 2:** Kontrolle: `git grep -n "_zlog" -- integrations/qgis/` → MUSS leer sein (Stand 2026-07-08 ist es leer; falls nicht leer: die `_zlog`-Debug-Zeilen aus dem Plugin entfernen, das war temporäres Zoom-Debugging).
- [ ] **Schritt 3: Committen**

```powershell
git commit -m @'
feat(qgis): Bridge fuer Schaechte + Layer Ausgefuehrt-durch + Schaeden gestreckt

Bridge liefert Schacht-Snapshots (AP1-AP7); neuer Endpunkt
/qgis/sanierungstyp.geojson kategorisiert Baumeister/Sanierer/Gaertner mit
laufender Nr.; Schadens-Meterstaende werden auf die Geometrie gestreckt
(BCE = inspizierte Laenge -> End-Schacht). Installiertes Plugin (Flash,
Zoom-Fix, Dock) ins Repo uebernommen.

Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>
'@
```

### Task 5: Commit 4 — PlayerWindow / Restliche UI-Änderungen

- [ ] **Schritt 1:** `git diff src/AuswertungPro.Next.UI/Views/Windows/PlayerWindow.xaml src/AuswertungPro.Next.UI/Views/Windows/PlayerWindow.xaml.cs src/AuswertungPro.Next.UI/Views/Windows/PlayerWindow.State.cs src/AuswertungPro.Next.UI/Views/Windows/PlayerWindow.Wiring.cs` lesen und das Thema bestimmen (erwartete Kandidaten: Zwei-Video/Gegeninspektion-Feinschliff oder Schacht-Video-Anbindung).
- [ ] **Schritt 2:** Die 4 PlayerWindow-Dateien stagen und mit thematisch passender deutscher Message committen (Format wie oben, `feat(player): …` bzw. `fix(player): …`, mit Co-Authored-By-Fusszeile). Gehört das Diff eindeutig zum Schacht-Tool, stattdessen Message `feat(schacht): Player-Anbindung …` verwenden.

### Task 6: Commit 5 — Docs/Specs + Endkontrolle

- [ ] **Schritt 1:** Stagen und committen:

```powershell
git add docs/superpowers/plans/2026-07-08-vsa-codier-dialog-und-abgleich-dnd.md
git add docs/superpowers/specs/2026-07-08-vsa-codier-dialog-und-abgleich-dnd-design.md
git add docs/superpowers/plans/2026-07-08-opus-commit-kosten-design-fundament.md
git commit -m @'
docs: Plaene und Design-Specs (VSA-Codier-Dialog, Commit/Kosten/Design-Fundament)

Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>
'@
```

- [ ] **Schritt 2:** `git status --short` → einzige erlaubte verbleibende Änderung: `Export_Vorlage/Haltungen.xlsx` (wird in Phase B behandelt). Alles andere: siehe Zuordnungsregel in den Globalen Regeln.
- [ ] **Schritt 3:** `git log --oneline -8` ausgeben und dem Nutzer als Zwischenstand zeigen.

---

## Phase B — Sanieren=Ja-Kostenregel abschliessen (Task 5+6 des bestehenden Plans)

**Quelle:** `docs/superpowers/plans/2026-07-07-kosten-felder-sanieren-sync.md` — Task 5 ab Zeile 406, Task 6 ab Zeile 435. Diese Tasks sind dort vollständig mit Code und Tests beschrieben; dieser Plan hier regelt nur Reihenfolge und Abnahme.

### Task 7: Template-Header prüfen/fixen (= Task 5 des Kosten-Plans)

**Hintergrund:** `Export_Vorlage/Haltungen.xlsx` ist bereits modifiziert (Binärdiff) — möglicherweise wurde der Header `Renovierung Inliner m` schon von Hand ergänzt.

- [ ] **Schritt 1:** Task 5 im Kosten-Plan (Zeile 406 ff.) vollständig lesen.
- [ ] **Schritt 2:** Den dort beschriebenen Verifikationsschritt ausführen (prüft, ob der Header in der Vorlage vorhanden ist und das Feld exportiert wird).
- [ ] **Schritt 3a — Header ist schon da:** Nur committen:

```powershell
git add Export_Vorlage/Haltungen.xlsx
git commit -m @'
feat(export): Vorlagen-Header Renovierung Inliner m in Haltungen.xlsx

Ohne den Header wurde das Feld nie exportiert (Task 5 Kosten-Plan).

Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>
'@
```

- [ ] **Schritt 3b — Header fehlt:** Task 5 exakt nach Kosten-Plan umsetzen, dann Verifikation wiederholen und wie in 3a committen.

### Task 8: Live-Sync in der App (= Task 6 des Kosten-Plans)

- [ ] **Schritt 1:** Task 6 im Kosten-Plan (Zeile 435 ff.) vollständig lesen und exakt umsetzen: Ja→Nein-Rückfrage beim Grid-Commit von `Sanieren_JaNein` + Sync-Aufrufe (`DerivedCostFieldSynchronizer`) auf den dort genannten Speicher-Pfaden (Matrix/CostCalc/Recompute-Save). Inklusive der dort definierten Tests.
- [ ] **Schritt 2:** `dotnet build AuswertungPro.sln` → 0 Fehler; `dotnet test AuswertungPro.sln --no-build` → 0 Fehler.
- [ ] **Schritt 3:** Committen (`feat(kosten): Sanieren-JaNein live synchronisiert (Rueckfrage + Sync auf Speicher-Pfaden)`, deutsche Message, Co-Authored-By-Fusszeile).
- [ ] **Schritt 4 — NUTZER-VERIFIKATION (nicht automatisierbar):** Dem Nutzer melden: App starten, Zone 1.15 öffnen, Haltungen exportieren → Spalte „Anschlüsse verpressen" muss **52** summieren (nicht 72). Erst nach seiner Bestätigung gilt Phase B als abgenommen.

---

## Phase C — Design-Fundament (mechanisch, KEINE Neugestaltung)

**Wichtig:** Diese Phase poliert die Grundlage. Der „Leuchtturm-Screen" (Kommandozentrale/Dashboard) ist BEWUSST NICHT Teil dieses Plans — er wird später gestalterisch mit Fable entworfen.

### Task 9: Theme-Parität — Severity-Brushes & Studien-Brushes in ThemeLight

**Hintergrund:** `Theme/Theme.xaml` (Dark) definiert `Severity1Brush`–`Severity5Brush`, `SecondaryAccentBrush`, `SecondaryAccentHoverBrush`, `SecondaryAccentSubtleBrush`, `AccentBarBrush` (Zeilen 59–73). `Theme/ThemeLight.xaml` braucht dieselben Keys mit hell-tauglichen Werten, sonst fällt jede künftige Verwendung im Light-Theme auf Transparent/Nichts zurück.

- [ ] **Schritt 1:** `Theme/ThemeLight.xaml` lesen; prüfen, welche der genannten Keys fehlen (`git grep -n "Severity1Brush\|SecondaryAccentBrush\|AccentBarBrush" -- src/AuswertungPro.Next.UI/Theme/ThemeLight.xaml`).
- [ ] **Schritt 2:** Fehlende Keys in `ThemeLight.xaml` ergänzen — an strukturell gleicher Stelle wie im Dark-Theme, mit diesen Werten (auf hellem Grund WCAG-tauglich abgestimmt):

```xaml
<!-- ── DESIGNSTUDIE: Zweit-Akzent (Teal), Severity-Skala, Akzent-Verlauf — hell abgestimmt ── -->
<Color x:Key="ColorSecondaryAccent">#FF0E7490</Color>
<Color x:Key="ColorSecondaryAccentHover">#FF155E75</Color>
<SolidColorBrush x:Key="SecondaryAccentBrush" Color="{StaticResource ColorSecondaryAccent}"/>
<SolidColorBrush x:Key="SecondaryAccentHoverBrush" Color="{StaticResource ColorSecondaryAccentHover}"/>
<SolidColorBrush x:Key="SecondaryAccentSubtleBrush" Color="#FFD2ECF4"/>
<SolidColorBrush x:Key="Severity1Brush" Color="#FF16A34A"/>
<SolidColorBrush x:Key="Severity2Brush" Color="#FF65A30D"/>
<SolidColorBrush x:Key="Severity3Brush" Color="#FFD97706"/>
<SolidColorBrush x:Key="Severity4Brush" Color="#FFEA580C"/>
<SolidColorBrush x:Key="Severity5Brush" Color="#FFDC2626"/>
<LinearGradientBrush x:Key="AccentBarBrush" StartPoint="0,0" EndPoint="1,0">
    <GradientStop Color="#FF2563EB" Offset="0"/>
    <GradientStop Color="#FF0E7490" Offset="1"/>
</LinearGradientBrush>
```

- [ ] **Schritt 3:** Vollständigen Key-Abgleich beider Themes machen: jeden `x:Key` aus `Theme.xaml` in `ThemeLight.xaml` suchen und umgekehrt. Fehlende Keys auf der jeweils anderen Seite ergänzen (Farbwert sinngemäss hell/dunkel übersetzen — im Zweifel den Nutzer fragen). Ergebnis als Tabelle in den Commit-Body schreiben.
- [ ] **Schritt 4:** Build; App manuell starten, in Einstellungen zwischen Hell/Dunkel umschalten (Schalter existiert, `SettingsPageViewModel.IsDarkTheme`) → beide Themes ohne sichtbare Löcher/Transparenzfehler.
- [ ] **Schritt 5:** Committen: `feat(theme): Severity- und Studien-Brushes in beiden Themes (Paritaet hergestellt)`.

### Task 10: Monospace-Token für technische Werte

- [ ] **Schritt 1:** In `Theme/Controls.xaml` (dort liegen die geteilten Tokens, z. B. `AnimDurationFast`) ein FontFamily-Token ergänzen:

```xaml
<FontFamily x:Key="FontMono">Cascadia Mono, Consolas, Courier New</FontFamily>
```

- [ ] **Schritt 2:** Anwenden auf DataGrid-Spalten mit technischen Werten. Fundorte per Suche: `git grep -n "Header=\"Code\"\|Header=\"Meter\"\|Header=\"DN" -- src/AuswertungPro.Next.UI/Views`. Für jede gefundene `DataGridTextColumn` mit Header `Code`, `Meter` oder `DN …` einen ElementStyle setzen (bestehende ElementStyles erweitern statt ersetzen):

```xaml
<DataGridTextColumn.ElementStyle>
    <Style TargetType="TextBlock">
        <Setter Property="FontFamily" Value="{StaticResource FontMono}"/>
    </Style>
</DataGridTextColumn.ElementStyle>
```

- [ ] **Schritt 3:** Build + Tests (0 Fehler) + App-Stichprobe: Protokoll-Grid im PlayerWindow zeigt Code/Meter in Monospace.
- [ ] **Schritt 4:** Committen: `feat(theme): Monospace fuer Code-/Meter-/DN-Spalten (FontMono-Token)`.

### Task 11: Zustandsklasse farblich (Severity-Brushes verdrahten)

- [ ] **Schritt 1: Testbare Mapping-Logik + Konverter anlegen** — Neue Datei `src/AuswertungPro.Next.UI/Controls/ZustandsklasseToBrushConverter.cs`:

```csharp
using System;
using System.Globalization;
using System.Windows.Data;

namespace AuswertungPro.Next.UI.Controls;

/// <summary>
/// Zustandsklasse ("0".."5", Text) -> Ressourcen-Key der Severity-Skala.
/// 5 = kritisch (rot) .. 1/0 = gut (gruen); unbekannt -> kein Eingriff.
/// </summary>
public sealed class ZustandsklasseToBrushConverter : IValueConverter
{
    /// <summary>Reine Mapping-Logik, ohne WPF-Abhaengigkeit testbar.</summary>
    public static string? MapToSeverityKey(string? zustandsklasse)
        => zustandsklasse?.Trim() switch
        {
            "5" => "Severity5Brush",
            "4" => "Severity4Brush",
            "3" => "Severity3Brush",
            "2" => "Severity2Brush",
            "1" or "0" => "Severity1Brush",
            _ => null
        };

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var key = MapToSeverityKey(value?.ToString());
        if (key is null)
            return Binding.DoNothing;

        return System.Windows.Application.Current?.TryFindResource(key) ?? Binding.DoNothing;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
```

- [ ] **Schritt 2: Test anlegen** — Neue Datei `tests/AuswertungPro.Next.UI.Tests/ZustandsklasseToBrushConverterTests.cs`:

```csharp
using AuswertungPro.Next.UI.Controls;

namespace AuswertungPro.Next.UI.Tests;

public sealed class ZustandsklasseToBrushConverterTests
{
    [Theory]
    [InlineData("5", "Severity5Brush")]
    [InlineData("4", "Severity4Brush")]
    [InlineData("3", "Severity3Brush")]
    [InlineData("2", "Severity2Brush")]
    [InlineData("1", "Severity1Brush")]
    [InlineData("0", "Severity1Brush")]
    [InlineData(" 3 ", "Severity3Brush")]
    public void MapToSeverityKey_kennt_alle_klassen(string input, string expected)
        => Assert.Equal(expected, ZustandsklasseToBrushConverter.MapToSeverityKey(input));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("x")]
    [InlineData("6")]
    public void MapToSeverityKey_unbekannt_liefert_null(string? input)
        => Assert.Null(ZustandsklasseToBrushConverter.MapToSeverityKey(input));
}
```

- [ ] **Schritt 3:** Test laufen lassen: `dotnet test tests/AuswertungPro.Next.UI.Tests/AuswertungPro.Next.UI.Tests.csproj --filter "FullyQualifiedName~ZustandsklasseToBrush"` → PASS.
- [ ] **Schritt 4: Anwenden in der Haltungsliste.** Fundort: `git grep -n "Zustandsklasse" -- src/AuswertungPro.Next.UI/Views/Pages/DataPage.xaml` (die Spalte, die das Feld `Zustandsklasse` anzeigt). Konverter in den Ressourcen der Seite registrieren und in der Spalte den Text einfärben (`Foreground` via Konverter, zusätzlich `FontWeight="SemiBold"`). Hintergrund NICHT einfärben (bleibt ruhig, Signalwirkung über Textfarbe).
- [ ] **Schritt 5:** Build + Tests + App-Stichprobe in BEIDEN Themes (Klasse 3 = Bernstein, Klasse 5 = Rot, gut lesbar).
- [ ] **Schritt 6:** Committen: `feat(ui): Zustandsklasse farbig nach Severity-Skala (Haltungsliste)`.

### Task 12: Hartcodierte Farben-Audit (sichere Richtung, ordnerweise)

**Regeln — ausschliesslich diese Ersetzungen, alles andere stehen lassen:**

| Hex im XAML | Ersetzen durch |
|---|---|
| Grün-Töne `#16A34A`, `#3FB950`, `#22C55E` als Foreground/Background von Erfolg/„Ja"-Aktionen | `{DynamicResource SuccessBrush}` |
| Rot-Töne `#DC2626`, `#F85149`, `#EF4444` bei Fehler/Löschen | `{DynamicResource DangerBrush}` |
| Orange `#F59E0B`, `#D29922` bei Warnungen | `{DynamicResource WarningBrush}` |
| Akzent-Blau `#2563EB`, `#539BF5`, `#1D4ED8` bei Buttons/Links/Hervorhebung | `{DynamicResource AccentBrush}` (Hover: `AccentHoverBrush`) |

**Tabu (NIE anfassen):** alles in `Theme/`, `PlayerWindow*`-Overlay-/Zeichen-Farben, Masken-/Erkennungs-Farben (KI-Overlays), Karten-Feature-Farben (`KarteView`/Mapping), QGIS-/GeoJSON-Farbdefinitionen in C#-Strings, Farben in Convertern/Code-Behind. Im Zweifel: NICHT ersetzen und Fundstelle im Commit-Body als „belassen" notieren.

- [ ] **Schritt 1:** Inventur: `git grep -nE "(Background|Foreground|BorderBrush)=\"#[0-9A-Fa-f]{6,8}\"" -- src/AuswertungPro.Next.UI/Views/Pages src/AuswertungPro.Next.UI/Dialogs` → Trefferliste sichten.
- [ ] **Schritt 2:** Ordner `Views/Pages` nach den Regeln oben ersetzen. Build + App-Stichprobe beide Themes. Commit: `refactor(theme): semantische Brushes statt Hex in Views/Pages (Audit Teil 1)`.
- [ ] **Schritt 3:** Ordner `Dialogs` genauso. Commit: `refactor(theme): semantische Brushes statt Hex in Dialogs (Audit Teil 2)`.
- [ ] **Schritt 4:** Abschlussbericht an den Nutzer: Anzahl ersetzt / bewusst belassen (mit Begründung), Rest-Trefferliste für spätere Runden.

---

## Abnahme des Gesamtplans

- [ ] `git status --short` → sauber (keine uncommitteten Änderungen).
- [ ] `dotnet build AuswertungPro.sln` → 0 Fehler; `dotnet test AuswertungPro.sln --no-build` → 0 Fehler.
- [ ] `git log --oneline -15` dem Nutzer zeigen.
- [ ] Offene Nutzer-Punkte melden: (1) Zone-1.15-Verifikation (Task 8 Schritt 4), (2) QGIS: installiertes Plugin enthält noch `_zlog`-Debug-Zeilen — bei Gelegenheit Plugin aus dem Repo neu installieren (`integrations/qgis/install-sewerstudio-bridge.ps1`), (3) NICHT gepusht — Push nur auf Wunsch.
