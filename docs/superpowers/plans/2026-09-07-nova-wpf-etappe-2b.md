# Nova WPF-Etappe 2b — Tabellen, Suche und Eingabefelder nach Prototyp (Haltungen und Schächte)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development to implement this plan task-by-task.

**Goal:** Die Seiten Haltungen und Schächte sehen im Nova-Layout so aus wie der freigegebene Prototyp: Kompakt-Tabelle mit Statusspalten und Zustandsklassen-Chips, einzeilige Zeilen, Kopf in Grossbuchstaben, Suche als Pille rechts, Eingabefelder in den Prototyp-Themen, Übersicht ohne Fehlertext.

**Architecture:** Nur additive Spaltenfabriken (Template-Spalten, nur lesend) neben den bestehenden Fabriken; die Spaltenansichten (`DataPageColumnViewCatalog`, `SchaechteColumnViewCatalog`) führen die neuen virtuellen Spalten unter festen Schlüsseln. Regeln (Status, Chip-Text, Grossschreibung, Faktenzusammensetzung) bleiben WPF-frei in `Application/UseCases/…`. Die alte Haltungs-/Schachtansicht (Umschalter) bleibt unverändert.

**Tech Stack:** WPF/.NET 10, CommunityToolkit.Mvvm, xunit; Wächter `DesignAudit*`, `XamlActionWiringGuardTests`, `UiArchitectureGuardTests`, `MaintainabilityFitnessTests` (1000 Zeilen je .cs), isolierte WPF-Smoketests (`WpfIsolatedTestProcess`, `[IsolatedWpfFact]`).

**Spec:** `docs/reviews/2026-09-06-nova/wpf-etappe-2/PROTOTYP-INVENTAR.md` Abschnitte 4.3 (Haltungen, Zellregeln, Kompakt-Spalten), 4.4 (Schächte), 5 (Übersicht), 9 (Feldlisten je Thema). Prototypbilder `docs/reviews/2026-09-06-nova/optimiert/v2/nachweise/haltungen-1920x1080.png`. Anlass: Pascals Bild vom 07.09. (echtes Projekt, 272 Haltungen): alte Tabelle mit „Alle Spalten 52", mehrzeilige Schadenzelle, nur sechs Zeilen sichtbar, `{DependencyProperty…}` bei DN / Profil.

## Global Constraints

- Worktree `C:\Sewer-Studio_KI_4.5-nova`, Branch `feature/nova-etappe-2b`; Hauptbaum nie anfassen; App nie autonom mit echtem Profil starten.
- Keine NuGet-Änderungen. Farben/Schriftgrössen/Rundungen ausserhalb `Theme/*.xaml` nur als `DynamicResource`-Tokens (`Text*`, `Radius*`, `*Brush`). Sichtbare Texte mit echten Umlauten, Schweizer `ss`; Quellcode-Kommentare mit ae/oe/ue.
- Keine Fachfunktion verschwindet: Verschieben auf Position, Gehe zu Zeile, Filterzeile (ZK 0–4, mit Video, mit Schäden) bleiben erreichbar. Die Filterzeile bleibt sichtbar, unterhalb der Spaltenchips (Entscheid Pascal 07.09.).
- Werte, Export, Speicherformat und Bearbeitung der Felder bleiben unverändert; Statusspalten sind nur lesend und werden nie exportiert oder gespeichert (`DataPageLayout`/Spaltenlayout muss virtuelle Spalten tolerieren, nie persistieren als Feld).
- Zustandsklassenfarben unverändert (`ZustandsklasseColorPalette`), Chip-Tinte über `ZustandsklasseInkPolicy`. Dunkler Akzent bleibt `#2563EB`.
- Alte Haltungs-/Schachtansicht (Menü Ansicht) bleibt unverändert und weiterhin erreichbar.
- Neue Regeln WPF-frei nach `src/AuswertungPro.Next.Application/UseCases/…` mit Test; UI ruft nur ViewModel/Service; kein Service-Locator in Seiten.
- Jede Aufgabe endet grün: `dotnet build AuswertungPro.sln` 0/0 und die genannten Testfilter.

---

### Task 1: Tabellenkopf in Grossbuchstaben, Zahlen rechts, NR nur in „Alle Spalten"

**Files:**
- Modify: `src/AuswertungPro.Next.UI/Theme/Theme.xaml`, `Theme/ThemeLight.xaml` (DataGridColumnHeader-Template, ~Z. 770–796)
- Create: `src/AuswertungPro.Next.UI/Controls/GrossbuchstabenConverter.cs`
- Modify: `src/AuswertungPro.Next.UI/DataPage/DataPageColumnStyleRules.cs` (Zahlenspalten vervollständigen)
- Modify: `src/AuswertungPro.Next.UI/DataPage/DataPageColumnViewCatalog.cs`, `SchaechteColumnViewCatalog.cs` (NR-Regel)
- Test: `tests/AuswertungPro.Next.UI.Tests/DesignAuditNovaTabelleTests.cs`, `DataPageColumnStyleRulesTests.cs` erweitern

**Interfaces:** `GrossbuchstabenConverter : IValueConverter` (string → `ToUpperInvariant`, andere Typen unverändert). `DataPageColumnStyleRules.IstZahlenspalte` deckt mindestens: `DN_mm`, `Lichte_Breite_mm`, `Haltungslaenge_m`, `Baujahr`, `VSA_Zustandsnote_D/S/B`, `Renovierung_Inliner_m`, `Anschluesse_verpressen`, `Reparatur_Manschette`, `Linerendmanschette_LEM`, `Reparatur_Kurzliner`, `Erneuerung_Neubau_m`, `Kosten`, `Dimension 1 mm`, `Dimension 2 mm`, `Tiefe_m` (echte Feldnamen aus `FieldKeys` verwenden, per Grep prüfen).

- [ ] Wächter rot: `DesignAuditNovaTabelleTests.Tabellenkopf_schreibt_gross` prüft in beiden Themes, dass das Header-Template den `GrossbuchstabenConverter` verwendet und `Typography.Capitals` nicht mehr trägt (Kapitälchen greifen mit der Programmschrift nicht; Prototyp: Grossbuchstaben, Letter-Spacing).
- [ ] Header-Template: `ContentPresenter` bekommt eine `ContentTemplate` mit `TextBlock Text="{Binding Converter={StaticResource Grossbuchstaben}}"`, `FontSize={DynamicResource TextXS}`, `FontWeight=SemiBold`, `Foreground={DynamicResource MutedBrush}`, `TextTrimming=CharacterEllipsis`; nur für String-Header (Template-Selector oder `DataTemplate DataType=sys:String`), sonst bisheriger Presenter. Konverter in `App.xaml` als Ressource `Grossbuchstaben` registrieren.
- [ ] `IstZahlenspalte` vervollständigen; Test mit Theory über alle genannten Felder; Zahlen erhalten `TextAlignment.Right` und `FontFamily={DynamicResource FontMono}` (prüfen, dass die Fabrik das bereits setzt; sonst ergänzen).
- [ ] NR: `DataPageColumnView.Enthaelt("NR")` ist nur in „Alle Spalten" wahr; Test `Kompakt_zeigt_keine_NR_Spalte`. Gleiche Regel für Schächte.
- [ ] Grün: `dotnet test tests/AuswertungPro.Next.UI.Tests --no-build --filter "DesignAuditNovaTabelle|DataPageColumnStyleRules|DataPageColumnViewCatalog|SchaechteColumnViewCatalog|DesignAudit"`; Commit „Nova-Etappe 2b: Tabellenkopf gross, Zahlen rechts, NR nur in Alle Spalten".

### Task 2: Zeilenstatus-Regel (WPF-frei)

**Files:**
- Create: `src/AuswertungPro.Next.Application/UseCases/NaechsteAufgabe/HaltungZeilenStatus.cs`
- Test: `tests/AuswertungPro.Next.Infrastructure.Tests/HaltungZeilenStatusTests.cs`

**Interfaces (Produces):**
```csharp
public enum KiAmpel { KeineAnalyse, Offen, Geprueft, Kritisch }
public sealed record HaltungZeilenStatusErgebnis(KiAmpel Ampel, string AmpelText, int OffeneBefunde,
    HaltungPruefstand Pruefstand, string PruefungText, bool HatVideo, bool HatProtokoll, string ZustandsklasseChip);
public static class HaltungZeilenStatus { public static HaltungZeilenStatusErgebnis Bestimme(HaltungRecord record); }
```
Regeln (Inventar 4.3 Zellregeln): Ampel: `Pruefstand == Offen` → `KeineAnalyse`/„keine Analyse"; offene KI-Befunde n > 0 → `Offen`/„<n> offen"; sonst wenn Zustandsklasse ≤ 1 → `Kritisch`/„geprüft"; sonst `Geprueft`/„geprüft". Prüfung-Text aus `HaltungPruefstatus.Text`. `HatVideo` aus `HaltungPruefstatus.HatVideo`. `HatProtokoll`: `PDF_Path`/`PDF_Eigen`/`PDF_All` nicht leer (Feldnamen wie im `ExcelTemplateExportService`-Linkvertrag). `ZustandsklasseChip`: „Z0"–„Z4" bei gültiger Klasse, sonst „–".

- [ ] Tests rot: leerer Datensatz → KeineAnalyse/„–"; zwei offene KI-Befunde → „2 offen"; abgeschlossen + Z1 → Kritisch „geprüft"; abgeschlossen + Z4 → Geprueft; Video/PDF-Flags.
- [ ] Implementieren, grün, Commit „Nova-Etappe 2b: Zeilenstatus-Regel fuer KI-Ampel, Pruefung, Video, Protokoll".

### Task 3: Statusspalten und Zustandsklassen-Chip in der Haltungstabelle

**Files:**
- Create: `src/AuswertungPro.Next.UI/Views/Pages/HaltungStatusColumnFactory.cs` (Template-Spalten KI, Prüfung, Video, Protokoll)
- Create: `src/AuswertungPro.Next.UI/Views/Pages/ZustandsklasseChipColumnFactory.cs` (Chip 34×22, Mono 12 700, Hintergrund Klassenfarbe, Tinte `ZustandsklasseInkPolicy`; „–" gestrichelt mit Tooltip „nicht berechnet"; Bearbeiten weiterhin per Auswahl 0–4 wie `SchaechteZustandsklasseColumnFactory`)
- Modify: `DataPageColumnFactory.cs`/`DataPageColumnSetup.cs`/`DataPage.ColumnLayout.cs`/`DataGridColumnLayoutController.cs` (virtuelle Spalten mit Schlüsseln `Nova_KI`, `Nova_Pruefung`, `Nova_Video`, `Nova_Protokoll`; nie in Export/Layout-Persistenz als Feld)
- Modify: `DataPageColumnViewCatalog.cs` Kompakt = Prototyp (10): HoldingName, Street, PipeMaterial, NominalDiameterMm, HoldingLengthMeters, ConditionClass, Nova_KI, Nova_Pruefung, Nova_Video, Nova_Protokoll; Bewertung erhält zusätzlich Nova_Pruefung.
- Modify: `DataPage.xaml`/`DataPage.ColumnViews.cs`: Statusspalten nur im Nova-Layout anhängen; in der alten Haltungsansicht bleibt alles wie bisher.
- Test: `HaltungStatusColumnFactoryTests` (STA über `StaTestRunner`-Muster: Zellvorlagen erzeugen, Bindungen vorhanden, Video-Knopf ruft `PlayVideoCommand`, Protokoll-Knopf `OpenOriginalPdfCommand`), `DataPageColumnViewCatalogTests` (Kompakt-Liste exakt), Wächter `DesignAuditNovaTabelleTests` (Chip-Masse über Tokens, keine festen Farben).

- [ ] Ampel: `Ellipse` 9 px (`SuccessBrush`/`WarningBrush`/`DangerBrush`/`MutedBrush`) + Text; Prüfung: Badge-Pille (`SuccessSubtle`/`KiSubtleBrush`/`SurfaceSubtleBrush`) mit Text; Video: Knopf `ui:FluentIcon` ▶ (`&#xE768;`) mit `AutomationProperties.Name="Video <Name> abspielen"` und ToolTip, sonst „–" mit Tooltip „kein Video"; Protokoll: Knopf „PDF" oder „–". Beide Knöpfe rufen die vorhandenen ViewModel-Befehle mit dem Zeilen-Datensatz als Parameter (`Command="{Binding DataContext.PlayVideoCommand, RelativeSource={RelativeSource AncestorType=DataGrid}}"`, `CommandParameter="{Binding}"`).
- [ ] Live-Aktualisierung: Ampel/Prüfung binden per `MultiBinding` auf `.` und `Fields[<WorkflowStatus>]` und `Protocol` (prüfen, ob `HaltungRecord` `PropertyChanged` für `Protocol` feuert; falls nicht, additiv ergänzen wie bei `SchachtRecord`, mit Test); Konverter ruft `HaltungZeilenStatus.Bestimme`.
- [ ] Der Chip ersetzt die farbige Zelle der Zustandsklasse nur im Nova-Layout; `ZustandsklasseCellStyleFactory` bleibt für die alte Ansicht.
- [ ] Einzeilige Zeilen: in Kompakt keine Umbruchspalte (bereits so); zusätzlich `DataGrid.RowHeight` im Nova-Layout auf einen festen Wert aus Token (`Auto` nur in „Alle Spalten"/„Bewertung", wo „Primäre Schäden" umbricht — dort Umbruch auf höchstens 3 Zeilen begrenzen: `MaxLines`/`MaxHeight` in `DataGridWrappingTextColumnFactory`, mit Tooltip Volltext).
- [ ] Grün: Build 0/0; Filter `"HaltungStatusColumn|ZustandsklasseChip|DataPageColumnViewCatalog|DesignAudit|XamlActionWiring|UiArchitectureGuard|MaintainabilityFitness|DataPageNovaLayoutIsolated"`; Commit „Nova-Etappe 2b: Statusspalten KI/Pruefung/Video/Protokoll und Zustandsklassen-Chip".

### Task 4: Kompakt einmalig als Standard, Suche als Pille, Verschieben/Gehe-zu in „Weitere Aktionen"

**Files:**
- Modify: `AppSettings.cs` (`DataPageLayout.NovaKompaktEinmalGesetzt` bool, `SchaechtePageLayout` analog), `DataPage.ColumnViews.cs`, `SchaechtePage.ColumnViews.cs`
- Modify: `DataPage.xaml` (Zeile 340–390: Suchfeld als Pille rechts in der Werkzeugleiste mit `kbd` „F3", Platzhalter „Suche Haltung"; Zeile mit „Verschieben auf Pos." und „Gehe zu Zeile" entfällt; beide werden Menüpunkte unter `Weitere Aktionen → Reihenfolge`, die ein kleines `Popup` mit Eingabefeld und Pfeilknopf öffnen — gleiche Befehle wie bisher), `DataPage.xaml.cs`/Partial für Popup-Öffnen, `SchaechtePage.xaml` (Suche als Pille rechts, ohne F3)
- Modify: `DataPageColumnViewController` / Filterzeile: Filter bleibt, wandert direkt unter die Spaltenchips (eine Zeile, links „Filter:", rechts Zählertext).
- Test: `DesignAuditNovaHaltungenTests`/`DesignAuditNovaSchaechteTests` (Suchpille vorhanden, keine Zeile „Verschieben auf Pos.:" mehr, Menüpunkte vorhanden, F3 verdrahtet), `DataPageColumnViewControllerTests` (Migration: gespeicherte Ansicht „alle" + Flag false → einmal „kompakt" setzen und Flag true; danach bleibt die Nutzerwahl).

- [ ] F3 fokussiert das Suchfeld (bestehende Tastenbehandlung prüfen: `grep -rn "Key.F3"`; ohne Treffer in `DataPage` ergänzen).
- [ ] Grün: Filter `"DesignAuditNovaHaltungen|DesignAuditNovaSchaechte|DataPageColumnView|XamlActionWiring|DesignAudit|DataPageNovaLayoutIsolated|SchaechteNovaLayoutIsolated"`; Commit „Nova-Etappe 2b: Suche als Pille, Kompakt einmalig Standard, Reihenfolge-Werkzeuge im Menue".

### Task 5: Eingabefelder in den Prototyp-Themen (Haltungen)

**Files:**
- Modify: `src/AuswertungPro.Next.UI/DataPage/DataPageRecordDetailsBuilder.cs` (Themen und Reihenfolge nach Inventar 9.1: Stammdaten 14, Bewertung 9, Sanierung 10, Kosten und Bemerkungen 3; alle übrigen Felder in „Weitere Angaben")
- Modify: `HaltungFelderDrawer.xaml(.cs)` („Weitere Angaben" standardmässig zugeklappt; Feldzähler je Thema)
- Test: `DataPageRecordDetailsBuilderTests` (Theory je Thema: exakte Feldliste in Reihenfolge; unbekanntes Feld → Weitere Angaben), `HaltungFelderDrawerFilterTests` (Weitere zugeklappt), bestehende Tests nachziehen (`SchaechteRecordDetailsBuilderTests` unberührt).

- [ ] Feldnamen aus `FieldKeys` bzw. dem echten Feldkatalog (`FieldCatalog.Definitions`) auflösen; keine erfundenen Schlüssel — jedes Inventarfeld muss einen Treffer haben, sonst im Bericht nennen.
- [ ] Alte Detailfenster/Alte Ansicht verwenden denselben Builder: neue Themenamen gelten dort mit (bewusst, ein Wortlaut).
- [ ] Grün: Filter `"RecordDetailsBuilder|HaltungFelderDrawer|DesignAudit|DataPageNovaLayoutIsolated"`; Commit „Nova-Etappe 2b: Eingabefelder in den vier Prototyp-Themen".

### Task 6: Übersicht ohne Fehlertext; Schächte gleichziehen (Chip, Protokoll-Knopf)

**Files:**
- Modify: `Application/UseCases/Uebersicht/HaltungFaktenText.cs` (`Zusammen` ignoriert `null`, leer und den Text von `DependencyProperty.UnsetValue` — WPF-frei: alles, was nicht string ist oder mit „{" beginnt), `HaltungListItemConverters.FaktZusammenConverter`/`FaktWertConverter` (UnsetValue → null)
- Modify: `HaltungUebersichtPanel.xaml`: ohne gewählten Datensatz nur Leerzustand („Keine Haltung gewählt. Links eine Zeile wählen."), Rohrring/Fakten/Schadenliste `Collapsed`. `SchachtUebersichtPanel` gleich prüfen.
- Modify: `SchaechteZustandsklasseColumnFactory.cs` → Chip wie Task 3 (gemeinsame `ZustandsklasseChipColumnFactory` verwenden), Schacht-Spalte `PDF_Path` in Kompakt/Dokumente als Knopf „PDF" (nur lesend; Bearbeiten des Pfads bleibt in „Alle Spalten"/Eingabefeldern) über den vorhandenen Weg der Seite (`SchaechteFileActionController` / Befehl des ViewModels, kein neuer Dateizugriff).
- Test: `HaltungFaktenTextTests` (UnsetValue-Text wird ignoriert), `SchaechteZustandsklasseColumnFactoryTests` (STA), `SchaechteColumnViewCatalogTests`, Wächter `DesignAuditNovaSchaechteTests`.

- [ ] Grün: Filter `"HaltungFaktenText|SchaechteZustandsklasse|SchaechteColumnView|DesignAuditNovaSchaechte|DesignAudit|SchaechteNovaLayoutIsolated|DataPageNovaLayoutIsolated"`; Commit „Nova-Etappe 2b: Uebersicht ohne Fehltext, Schachtliste mit Chip und Protokoll-Knopf".

### Task 7: Prüfhost mit realistischen Daten, Bilder, Abnahme, CLAUDE.md

**Files:**
- Modify: `docs/reviews/2026-09-06-nova/wpf-etappe-2/werkzeug/Program.cs` (Testprojekt: 40 Haltungen mit langen „Primäre Schäden"-Texten, gemischten Prüfständen, Videos/PDF-Pfaden, gespeicherte Ansicht „alle" in der settings.json, um die Migration zu zeigen; Schächte mit PDF)
- Create: `docs/reviews/2026-09-06-nova/wpf-etappe-2b/ABNAHME.md`, `bilder/haltungen-{hell,dunkel}.png`, `schaechte-{hell,dunkel}.png` (Full HD, erste Zeile gewählt)
- Modify: `CLAUDE.md` Abschnitt „Nova-Etappe 2b (2026-09-07)" nach „Nova-Etappe 2"

- [ ] Release-Gesamtlauf `dotnet build AuswertungPro.sln -c Release && dotnet test AuswertungPro.sln -c Release --no-build`: 0 Fehler; Zahlen in die Abnahme.
- [ ] Jedes Bild ansehen und mit `haltungen-1920x1080.png` des Prototyps vergleichen: Kompakt 10 Spalten, Chips, einzeilige Zeilen (mindestens 12 sichtbare Zeilen bei 1920×1080 mit offenen Eingabefeldern), Kopf gross, Suchpille, vier Themen. Abweichungen benennen.
- [ ] Commit „Nova-Etappe 2b: Abnahme mit Pruefhost-Bildern, CLAUDE.md nachgefuehrt". Kein Merge/Push durch den Umsetzer.
