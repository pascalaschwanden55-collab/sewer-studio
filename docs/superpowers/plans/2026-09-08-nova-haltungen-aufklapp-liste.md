# Haltungen als Aufklapp-Liste (Nova, 2026-09-08)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development to implement this plan task-by-task.

**Goal:** Die Haltungsseite bekommt als neue Standardansicht eine Übersichtsliste, in der jede Haltung mit einem Pfeil direkt in der Liste aufklappt und darunter alle Felder geordnet in den Themen (Stammdaten, Bewertung, Sanierung, Kosten und Bemerkungen, Weitere Angaben) zum Ausfüllen zeigt. Die Tabelle bleibt als zweite Ansicht erreichbar.

**Architecture:** Neues Control `HaltungAufklappListe` (ListBox mit Virtualisierung, ein Kopfzeilen-Template je Haltung, genau EINE aufgeklappte Haltung — Akkordeon). Der aufgeklappte Bereich verwendet dieselbe Formularfabrik wie die Eingabefelder-Schublade (`DataPageRecordDetailsBuilder` + `DataPageDetailItemFactory` mit Konfliktregel, `DataPageDetailLiveSync`, `RecordDetailsView` kompakt je Thema). Ein eigener `DataPageAufklappListeController` (DataPage-Namespace, ausserhalb der `DataPage`-Teildateien) verdrahtet Auswahl, Aufklappen, Live-Abgleich und Konflikt-Hinweis. Die Seite schaltet über `AppSettings.HaltungenAnsicht` („liste" Standard | „tabelle") zwischen Liste und Tabelle; die alte Haltungsansicht bleibt über `ShowHaltungenNovaLayout=false` unverändert.

**Tech Stack:** WPF/.NET 10, CommunityToolkit.Mvvm, xunit; Wächter `DesignAudit*`, `XamlActionWiringGuardTests`, `UiArchitectureGuardTests`, `MaintainabilityFitnessTests` (1000 Zeilen je .cs; `DataPage`-Teildateien gemeinsam ≤ 2000), isolierte WPF-Smoketests (`WpfIsolatedTestProcess`, `[IsolatedWpfFact]`, `NovaRenderingChecks`).

**Spec:** Pascals Wunsch vom 08.09.2026: „eine Übersichtsliste, wo jede Haltung aufklappbar ist, wo geordnet alle Felder zum Ausfüllen/Bearbeiten da sind; Tabellenansicht brauche ich nicht", Vorbild die aufklappbaren Themen mit Pfeil (`<details class="sec">`, Chevron ▸ dreht um 90°) im Entwurf `C:\Users\Besitzer\Desktop\SewerStudio-Nova-Komplett.html` (Zeilen 670–706) und die Kompakt-Spalten der Tabelle (Haltung, Strasse, Material, DN, Länge, Zustand, KI, Video, Protokoll). Feldlisten je Thema: `DataPageRecordDetailsBuilder` (17/9/11/3 + Weitere Angaben).

## Global Constraints

- Worktree `C:\Sewer-Studio_KI_4.5-nova`, Branch `feature/nova-haltungsliste` ab `cfccda464`; Hauptbaum nie anfassen; App nie autonom mit echtem Profil starten.
- Keine NuGet-Änderungen. Farben/Schriftgrössen/Rundungen ausserhalb `Theme/*.xaml` nur als `DynamicResource`-Tokens. Sichtbare Texte mit echten Umlauten, Schweizer `ss`; Quellcode-Kommentare mit ae/oe/ue. Jeder Icon-Knopf hat `AutomationProperties.Name` UND `ToolTip`; Glyphen nur als `ui:FluentIcon`.
- **Datensicherheit:** Jede Feldänderung läuft ausschliesslich über den bestehenden Rückschreibweg der `DataPageDetailItemFactory` (Konfliktregel `IstKonflikt`, `SetFieldValue(..., FieldSource.Manual, userEdited: true)`, AutoSave über den bestehenden Weg). Kein zweiter Schreibpfad. Ein Formular existiert nur für die EINE aufgeklappte Haltung; beim Wechsel wird der vorherige `DataPageDetailLiveSync` entsorgt.
- Keine Fachfunktion verschwindet: Tabelle (mit Statusspalten, Spaltenansichten, Abdocken, Spalte leeren) bleibt über `Weitere Aktionen → Ansicht → Tabelle` erreichbar; alte Haltungsansicht bleibt. Suche (F3), Filterzeile, Übersicht rechts (Rohrring), Kontextmenü der Zeile (Video, Protokoll, Beobachtungen, Zur Haltung …) gelten in der Liste gleich wie in der Tabelle.
- Virtualisierung: `VirtualizingStackPanel` mit `VirtualizationMode="Standard"` (kein Recycling, weil der aufgeklappte Inhalt Editoren trägt); bei 300 Haltungen entstehen nur Kopfzeilen, kein Formular je Zeile.
- Regeln WPF-frei nach `Application/UseCases/…`; UI ruft nur ViewModel/Service; kein Service-Locator in Seiten. Neue Verdrahtung in einem eigenen Controller im DataPage-Namespace, nicht in den `DataPage`-Teildateien (Grenze 2000, aktuell 1990).
- Jede Aufgabe endet grün: `dotnet build AuswertungPro.sln` 0/0 und die genannten Testfilter; isolierte Smoketests werden vom Controller selbst nachgefahren.

---

### Task 1: Control `HaltungAufklappListe` mit Controller

**Files:**
- Create: `src/AuswertungPro.Next.UI/Views/Pages/Haltungsansicht/HaltungAufklappListe.xaml`, `.xaml.cs`
- Create: `src/AuswertungPro.Next.UI/DataPage/DataPageAufklappListeController.cs`
- Create: `src/AuswertungPro.Next.UI/DataPage/HaltungThemenGruppierung.cs` (aus `HaltungFelderDrawer.ThemaAnzeige`/Gruppierung herausgezogen und von beiden verwendet; `ThemaAnzeige` bleibt als Typ erhalten)
- Test: `tests/AuswertungPro.Next.UI.Tests/HaltungAufklappListeIsolatedSmokeTests.cs` (Kindprozess), `HaltungThemenGruppierungTests.cs`, `DesignAuditNovaAufklappListeTests.cs` (Wächter: Tokens, Umlaute, `AutomationProperties.Name`+ToolTip an Pfeil/Knöpfen, `VirtualizationMode="Standard"`, kein Formular-Template ausserhalb des aufgeklappten Zweigs)

**Interfaces (Produces):**
```csharp
public partial class HaltungAufklappListe : UserControl {
  // ItemsSource/SelectedItem werden von der Seite gebunden (Records / Selected, TwoWay)
  public HaltungRecord? Aufgeklappt { get; }                 // DP, genau eine Haltung offen (Akkordeon), null = keine
  public event EventHandler? AufgeklapptChanged;
  public Func<HaltungRecord, IReadOnlyList<RecordDetailGroup>>? DetailBuilder { get; set; }
  public IReadOnlyList<ThemaAnzeige>? Themen { get; }         // DP, nur fuer die offene Haltung gesetzt
  public string Hinweis { get; set; }                         // Konflikt-Hinweis in der Kopfzeile des offenen Eintrags
  public ICommand? VideoCommand, ProtokollCommand { get; set; } // von der Seite gesetzt (vm.PlayVideoCommand, vm.OpenOriginalPdfCommand)
  public void KlappeAuf(HaltungRecord? record); public void KlappeZu();
}
public sealed class DataPageAufklappListeController { // Muster DataPageNovaWorkspaceController
  public DataPageAufklappListeController(HaltungAufklappListe liste, Func<DataPageViewModel?> vm, Func<HaltungRecord, IReadOnlyList<RecordDetailGroup>> detailBuilder);
  public void Verdrahte(); public void AktualisiereFormular(); public void MeldeKonflikt(string feld, string aktuell, string eingabe); public void Dispose();
}
```

- [ ] Kopfzeile je Haltung (Grid, feste Spalten wie Kompakt): Pfeil-`ToggleButton` (`ui:FluentIcon` Chevron `&#xE76C;`, dreht bei offen um 90° über `RotateTransform`, `AutomationProperties.Name="Haltung aufklappen"`), Haltungsname fett (Mono `FontMono`), Strasse, Material, DN rechts Mono, Länge rechts Mono (Ellipsis bei Platzmangel), Zustandsklassen-Marke (`ZustandsklasseChipTextConverter`/`ZustandsklasseChipHintergrundConverter`/`ZustandsklasseInkConverter`, 34×22, `RadiusS`, „–" gestrichelt mit Tooltip „nicht berechnet"), KI-Ampel (Punkt 9 px + Text) und Prüfung-Badge über `HaltungZeilenStatusConverter` (MultiBinding `.`, `Fields[Offen_abgeschlossen]`, `Protocol`), Video ▶ und Protokoll „PDF" (Knöpfe binden `VideoCommand`/`ProtokollCommand` mit `CommandParameter={Binding}`; ohne hinterlegten Pfad „–" mit denselben Hinweistexten wie `HaltungStatusColumnFactory` — Texte aus `HaltungProtokollQuelle`/`HaltungPruefstatus` wiederverwenden, nicht kopieren). Zeilenhöhe `RowHeightCompact`. Klick auf die Kopfzeile wählt die Haltung (`SelectedItem`), Klick auf den Pfeil, Doppelklick, Enter oder Leertaste klappt auf/zu; Escape klappt zu; Pfeiltasten wechseln die Auswahl (ListBox-Standard).
- [ ] Aufgeklappter Bereich (nur für `Aufgeklappt`, per DataTrigger-Template mit `ContentControl`, sonst leer): Kopf mit Haltungsname (Mono), Konflikt-`Hinweis` (`WarningBrush`, nur wenn gesetzt), Knöpfe „Alle auf"/„Alle zu"; darunter die Themen als `ItemsControl` mit `UniformGrid Rows="1"` (nebeneinander, bei Breite < 1100 px `Columns="2"`), je Thema ein `Expander` (Kopf „▸ Titel" + Zähler-Pille wie in `HaltungFelderDrawer`, „Weitere Angaben" zugeklappt) mit `controls:RecordDetailsView IsCompactLayout="True" IsHeaderVisible="False" Groups="{Binding EinzelGruppe}"`. Themen kommen aus `HaltungThemenGruppierung.Bilde(gruppen)` (aus dem Drawer extrahiert, gleiche Reihenfolge und Zähler 17/9/11/3).
- [ ] Controller: bei `AufgeklapptChanged` und bei `vm.Selected`-Wechsel (Auswahl folgt dem Aufklappen, nicht umgekehrt: eine Auswahl per Pfeiltaste klappt NICHT automatisch auf) Formular über `detailBuilder(record)` bauen, `Themen` setzen, `new DataPageDetailLiveSync(record, gruppen)`; alten Sync entsorgen; `MeldeKonflikt` schreibt den Hinweis wie `DataPageNovaWorkspaceController.MeldeKonflikt` (gleicher Wortlaut, gemeinsame Hilfsfunktion statt Kopie).
- [ ] Tests: `HaltungThemenGruppierungTests` (Reihenfolge, Zähler, Weitere zugeklappt; und der Drawer verwendet dieselbe Gruppierung — Wächter per Grep). Isolierter Smoketest: 40 Datensätze → `ListBox` zeigt Kopfzeilen, KEIN `RecordDetailsView` im Baum; `KlappeAuf(r1)` → genau ein `RecordDetailsView`-Satz mit fünf Themen (Zähler 17/9/11/3/n), Pfeil gedreht, `Selected == r1`; `KlappeAuf(r2)` → r1-Formular weg, genau ein Sync; Feldänderung über den `RecordDetailItem` der Gruppe (Wert setzen und Rückschreiben wie `DataPageFormularTabelleAbgleichTests`) → `record.GetFieldValue` geändert, `FieldSource.Manual`, `UserEdited`; externe `SetFieldValue` am Datensatz → Formularwert folgt (Live-Sync); `KlappeZu()` → kein Formular, Sync entsorgt. Ohne Auswahl: kein Fehltext, keine Ausnahme.
- [ ] Grün: `dotnet build AuswertungPro.sln`; `dotnet test tests/AuswertungPro.Next.UI.Tests --no-build --filter "HaltungAufklappListe|HaltungThemenGruppierung|DesignAuditNovaAufklappListe|HaltungFelderDrawer|DesignAudit|XamlActionWiring|UiArchitectureGuard|MaintainabilityFitness|UebersprungeneTests"`. Commit „Haltungen: Aufklapp-Liste als Control mit Formular je offener Haltung".

### Task 2: Ansicht in die Seite einbauen, Standard „Liste"

**Files:**
- Modify: `src/AuswertungPro.Next.UI/AppSettings.cs` (`HaltungenAnsicht` string, Standard `"liste"`; erlaubte Werte `liste`/`tabelle`, alles andere → `liste`)
- Create: `src/AuswertungPro.Next.UI/DataPage/HaltungenAnsichtRegel.cs` (WPF-frei: `Normalisiere(string?)`, `IstListe`, Umschaltlogik: Liste sichtbar ⇔ Nova-Layout an UND Ansicht „liste"; Tabelle ⇔ Nova an UND „tabelle"; alte Ansicht ⇔ Nova aus) + Test
- Modify: `src/AuswertungPro.Next.UI/Views/Pages/DataPage.xaml` (Liste im `GridHost` in derselben Zelle wie `Grid` (Row 1, Col 0), `Visibility` je Ansicht; Menü `Weitere Aktionen → Ansicht`: zwei checkbare Punkte „Aufklapp-Liste" und „Tabelle" (genau einer angehakt) neben „Alte Haltungsansicht"; Kontextmenü der Tabelle auch an der Liste (gleiche Befehle, `DataPageRightClickController`-Weg für Zeilenaktionen wiederverwenden)
- Create: `src/AuswertungPro.Next.UI/Views/Pages/DataPage.AufklappListe.cs` (nur Verdrahtung: Elemente reichen, Controller anlegen, Ansicht anwenden — klein halten, Grenze 2000)
- Modify: `DataPageNovaWorkspaceController`/`DataPage.NovaWorkspace.cs`: in der Listenansicht ist die Schublade (`FelderDrawer`) samt `DrawerRow`/`DrawerSplitterRow` auf 0 und zugeklappt, `ColumnViewChips` ausgeblendet (die Liste hat feste Spalten), Filterzeile und Suchpille bleiben, `Uebersicht` rechts bleibt und folgt der Auswahl. Abdocken (`UndockButton`) ist in der Listenansicht deaktiviert mit Tooltip „Abdocken gilt für die Tabelle" (Fachfunktion bleibt über die Tabelle).
- Test: `HaltungenAnsichtRegelTests`, `DesignAuditNovaHaltungenTests` erweitern (Menüpunkte, Liste in `DataPage.xaml`, Chips/Schublade an die Ansicht gebunden), `DataPageNovaLayoutIsolatedSmokeTests` erweitern (Standard „liste": Liste sichtbar, Tabelle und Schublade collapsed, Übersicht sichtbar; Umschalten auf „tabelle": Tabelle sichtbar, Liste weg, Schublade wieder da; Einstellung gespeichert)

- [ ] Suche/Filter: Liste bindet `ItemsSource="{Binding Records}"` — dieselbe Standard-`CollectionView` wie die Tabelle, damit `SearchText` und die Filterzeile ohne zweiten Weg wirken; `SelectedItem="{Binding Selected, Mode=TwoWay}"`. Beim Wechsel der Ansicht bleibt die Auswahl erhalten; die Liste scrollt die Auswahl in Sicht (`ScrollIntoView`).
- [ ] `ShellViewModel.NavigateToHolding` (Sprung aus Dossier/Karte) selektiert den Datensatz; in der Listenansicht wird er zusätzlich aufgeklappt.
- [ ] Grün: Build; Filter `"HaltungenAnsichtRegel|HaltungAufklappListe|DesignAuditNovaHaltungen|DataPageNovaLayoutIsolated|DesignAudit|XamlActionWiring|UiArchitectureGuard|MaintainabilityFitness|UebersprungeneTests|DataPageColumnView"`; voller UI-Lauf. Commit „Haltungen: Aufklapp-Liste als Standardansicht, Tabelle als zweite Ansicht".

### Task 3: Prüfhost-Bilder, Abnahme, CLAUDE.md

**Files:**
- Modify: `docs/reviews/2026-09-06-nova/wpf-etappe-2/werkzeug/Program.cs` (Argument `Liste`: Ansicht „liste", erste Haltung aufklappen; ein Bild mit zugeklappter Liste und eines mit aufgeklappter Haltung, hell und dunkel)
- Create: `docs/reviews/2026-09-06-nova/aufklapp-liste/ABNAHME.md`, `bilder/haltungen-liste-{zu,auf}-{hell,dunkel}.png`
- Modify: `CLAUDE.md` Abschnitt „Nova: Haltungen als Aufklapp-Liste (2026-09-08)" nach „Nova-Fixwelle 2b"

- [ ] Release-Gesamtlauf `dotnet build AuswertungPro.sln -c Release && dotnet test AuswertungPro.sln -c Release --no-build` → 0 Fehler, 0 Warnungen; Zahlen in die Abnahme. Bilder ansehen: Kopfzeilen einzeilig, Pfeil dreht, fünf Themen nebeneinander mit Zählern, Felder editierbar sichtbar, Übersicht rechts gefüllt, kein Fehltext; mindestens 12 Kopfzeilen bei zugeklappter Liste (Full HD).
- [ ] Commit „Haltungen: Abnahme der Aufklapp-Liste mit Pruefhost-Bildern, CLAUDE.md nachgefuehrt". Kein Merge, kein Push durch den Umsetzer.

### Task 4: Haltungsgrafik des Protokolls in der Übersicht (statt Rohrring)

**Spec-Ergänzung (Pascal 08.09.):** „Bei der Haltung die Haltungsgrafik wie beim Erstellen eines AWU-Haltungsprotokolls, analog WinCan im Nova-Stil." Quelle der Zeichnung ist `Application/Reports/HaltungsgrafikSvgBuilder.BuildHaltungsgrafikSvg(...)` (senkrechtes Rohr in Aufnahmerichtung, Schadenssymbole über `DamageSymbolRenderer`, Meter-Skala, Fliesspfeil, Schachtknoten, Label-Tabelle), verwendet von `ProtocolPdfExporter` (~Z. 876). Das Programm hat keinen SVG-Renderer für WPF.

**Files:**
- Create: `src/AuswertungPro.Next.UI/Controls/SvgTeilmengeZeichner.cs` (zeichnet die von unseren Buildern verwendete SVG-Teilmenge — `rect`, `line`, `circle`, `polygon`, `polyline`, `path` (M/L/A/Z wie im Builder), `text` (x, y, text-anchor, font-size, font-weight), `g` mit `transform="translate|rotate"` — als WPF-Shapes auf ein `Canvas`; Farben werden über eine Zuordnungstabelle Builder-Farbe → Theme-Token (`TextBrush`, `MutedBrush`, `BorderBrush`, `AccentBrush`, Zustandsfarben, `FaintBrush`) gemappt; unbekannte Elemente/Attribute → `NotSupportedException` mit Elementname)
- Create: `src/AuswertungPro.Next.UI/Views/Pages/Haltungsansicht/HaltungsgrafikControl.xaml(.cs)` (DP `Record`, `Catalog` (aus `App.Services`? nein: von der Seite gesetzt), `FlowDown`; baut das SVG über den Builder mit denselben Argumenten wie der PDF-Weg (Länge, Einträge ohne gelöschte, Fotonummern leer, Start-/Endknoten aus `Schacht_oben`/`Schacht_unten`, `brand` = Akzentfarbe des Themes, Höhe an die Panelhöhe angepasst über `overrideHeight`) und zeichnet es über den Zeichner; `Viewbox` Uniform; Tooltip je Symbol mit Code + Klartext (Titel aus dem SVG `<title>` falls vorhanden, sonst aus dem Eintrag))
- Modify: `HaltungUebersichtPanel.xaml(.cs)`: die Grafik ersetzt den Rohrring (RohrringControl bleibt als Klasse erhalten, nicht mehr im Panel; Wächter `DesignAuditNovaHaltungenTests`/`RohrringGeometrieTests` entsprechend anpassen — Geometrie-Tests bleiben, XAML-Wächter auf die Grafik umstellen); Live-Aktualisierung wie bisher (Protokoll-/Feldwechsel zeichnet neu, gedrosselt auf einen Dispatcher-Durchlauf)
- Test: `SvgTeilmengeZeichnerTests` (STA: jedes Element der Teilmenge → passendes Shape mit Position/Farbe; unbekanntes Element wirft), Wächter `HaltungsgrafikSvgBuilderTeilmengeTests` (WPF-frei: ein repräsentatives SVG des Builders mit allen Symbolarten enthält nur Elemente/Attribute der Teilmenge — Grep über den Builder-Quelltext UND ein erzeugtes SVG), `HaltungsgrafikControlIsolatedSmokeTests` (Kindprozess: Panel mit Haltung mit drei Befunden zeigt Grafik mit drei Symbolen, ohne Auswahl nichts, dunkles Theme ohne feste Farben), `DesignAuditNovaHaltungenTests` erweitert.

- [ ] Nova-Stil: Strichstärken/Schriften aus dem SVG übernehmen, Farben nur über Tokens; die Beschriftungstabelle unter der Grafik verwendet `FontMono`/`TextXS`; Zustandsfarben Z0-Z4 unverändert.
- [ ] Grün: Build; Filter `"SvgTeilmenge|HaltungsgrafikSvgBuilder|HaltungsgrafikControl|HaltungUebersicht|DesignAudit|XamlActionWiring|UiArchitectureGuard|MaintainabilityFitness|UebersprungeneTests|DataPageNovaLayoutIsolated"`. Commit „Haltungen: Haltungsgrafik des Protokolls in der Übersicht".

### Task 5: Schachtgrafik in der Schachtansicht (analog WinCan)

**Spec-Ergänzung (Pascal 08.09.):** „Beim Schacht könnte da eine Schachtgrafik sein" — WinCan-Art: senkrechter Schnitt. Es gibt keine bestehende Schachtgrafik im Programm.

**Files:**
- Create: `src/AuswertungPro.Next.Application/Reports/SchachtgrafikSvgBuilder.cs` (WPF-frei, gleiche SVG-Teilmenge wie Task 4): Deckel oben mit Schachtnummer, Konus, Schachtwand, Sohle als Zonen (Beschriftung links, `MutedBrush`-Farbe), Tiefe (`Tiefe_m`) als Masslinie rechts, Innenmasse (`Dimension 1 mm` × `Dimension 2 mm`) unter der Sohle, Fliesspfeil; angeschlossene Haltungen als kurze Rohrstummel links (Zulauf: Haltungen mit `Schacht_unten` = Schachtnummer) und rechts (Ablauf: `Schacht_oben` = Schachtnummer) mit Haltungsname und DN; Schäden aus dem Schachtprotokoll nach Ort (`Konus`/`Schachtwand`/`Sohle`/`Anschluss`; Zuordnung über eine WPF-freie `SchachtSchadenOrtRegel` aus dem Eintrag: Feld `Ort`/Text; unbekannt → Schachtwand) mit `DamageSymbolRenderer`-Symbol in der Zone und Label-Tabelle wie bei der Haltung (Code, Klartext, Stufe). Keine erfundenen Werte: fehlende Tiefe/Masse → Zone ohne Masslinie, Hinweis „Tiefe nicht erfasst".
- Create: `Views/Pages/Schachtansicht/SchachtgrafikControl.xaml(.cs)` (DP `Record`, `Haltungen` (IReadOnlyList<HaltungRecord> der Seite), zeichnet über `SvgTeilmengeZeichner`), ersetzt den Grundriss-Kreis in `SchachtUebersichtPanel` (Grundriss-Masstext bleibt in der Grafik enthalten).
- Test: `SchachtgrafikSvgBuilderTests` (WPF-frei: Zonen, Zu-/Abläufe je Richtung, Schaden je Ort in der richtigen Zone, fehlende Tiefe), `SchachtSchadenOrtRegelTests`, `SchaechteNovaLayoutIsolatedSmokeTests` erweitert (Grafik sichtbar, drei Schäden = drei Symbole), `DesignAuditNovaSchaechteTests` erweitert.

- [ ] Grün: Build; Filter `"Schachtgrafik|SchachtSchadenOrt|SchachtUebersicht|SchaechteNovaLayoutIsolated|DesignAudit|XamlActionWiring|UiArchitectureGuard|MaintainabilityFitness|UebersprungeneTests"`. Commit „Schächte: Schachtgrafik analog WinCan in der Schachtansicht".

### Task 6: Abnahme-Nachtrag für beide Grafiken

- [ ] Prüfhost: Bilder `haltungen-liste-auf-hell/dunkel` (Grafik sichtbar) und `schaechte-grafik-hell/dunkel` neu; ABNAHME.md (aufklapp-liste) um Abschnitt „Grafiken" ergänzen; CLAUDE.md-Abschnitt ergänzen (SVG-Teilmenge als Vertrag: neue SVG-Elemente im Builder brauchen den Zeichner); Release-Gesamtlauf 0/0. Commit „Nova: Haltungs- und Schachtgrafik abgenommen".
