# Kartenansicht entfernen — Umsetzungsplan

> **Fuer agentische Arbeiter:** ERFORDERLICHER SUB-SKILL: Nutze
> `superpowers:subagent-driven-development` (empfohlen) oder
> `superpowers:executing-plans`, um diesen Plan Aufgabe fuer Aufgabe umzusetzen.
> Schritte verwenden Checkbox-Syntax (`- [ ]`) zur Nachverfolgung.

**Ziel:** Die nie produktiv genutzte Mapsui-Kartenansicht vollstaendig aus
SewerStudio entfernen — Einstiegspunkte, Code, Tests, DI-Registrierungen,
NuGet-Pakete und die rein kartenbezogenen Einstellungen.

**Architektur:** Reiner Rueckbau, kein neuer Code. Die raeumliche Arbeit laeuft
weiterhin ausschliesslich ueber die QGIS-Bruecke, die unangetastet bleibt. Zwei
Klassen mit "Karte" im Namen bleiben bestehen, weil der QGIS-Weg sie braucht;
sie erhalten einen Namensvermerk statt einer Umbenennung.

**Tech Stack:** C# / .NET 10, WPF, xUnit. Entfernt werden die NuGet-Pakete
`Mapsui.Wpf 5.1.0` und der nur deswegen gesetzte Pin `SkiaSharp.Views.WPF 3.119.4`.

**Spec:** `docs/superpowers/specs/2026-08-30-kartenansicht-entfernen-design.md`

## Globale Randbedingungen

- **SewerStudio muss geschlossen sein.** Ein laufendes Programm sperrt die DLLs;
  dann testet man einen alten Stand und haelt ihn faelschlich fuer gruen.
- Build: `dotnet build AuswertungPro.sln` — ohne Fehler, ohne neue Warnungen.
- Test: `dotnet test AuswertungPro.sln` — vollstaendig gruen.
- **Jede Aufgabe endet gruen.** Loeschen von Produktivcode und Anpassen der
  zugehoerigen Waechtertests gehoeren in dieselbe Aufgabe, sonst ist der Stand
  dazwischen rot.
- Kommentare auf Deutsch (Projektregel).
- Die QGIS-Bruecke wird nicht angefasst.
- Der Ordner `basemap_tiles` (8,8 GB, nicht im Git) bleibt unberuehrt.

## Abweichungen vom Design — vor der Umsetzung lesen

Die Voruntersuchung hat vier Punkte gefunden, die das Design nicht kannte:

1. **Es sind sieben Waechter, nicht drei.** Zusaetzlich zu den drei genannten
   brechen beim Loeschen von `KarteViewModel` bzw. `KarteWindow`:
   `InspectionProtocolFileLocatorDependencyTests.cs:45`,
   `KatasterXtfPathResolverDependencyTests.cs:85` (Datei bleibt, nur eine Zeile
   faellt) und `WindowOpenCloseSmokeTests.cs:14`.
   Den siebten fand erst der Testlauf:
   `DesignAuditThemeResourceTests.Shell_navigation_uses_unique_semantic_icons`
   zaehlt die Navigationseintraege (16 -> 15) und bricht schon durch Aufgabe 1.
   Er war durch Suche nicht auffindbar, weil er die Zahl aus einem
   Regex-Treffer ableitet und das Wort "Karte" nirgends vorkommt.
2. **`SettingsProgramCleanupRequestFactory.cs:85`** liest `OfflineBasemapPath`
   als geschuetzten Ordner der Programm-Aufraeumfunktion. Geprueft und
   unbedenklich: Die Aufraeumfunktion durchsucht nur `src`, `tests`, `tools`,
   `sidecar`, `training`, `integrations`, `.worktrees` und loescht dort nur
   `bin`, `obj`, `TestResults` und Python-Caches. `basemap_tiles` liegt direkt
   unter dem Programmordner und wird nie durchlaufen. Der Schutzeintrag war
   wirkungslose Vorsicht und darf ersatzlos entfallen.
3. **`SkiaSharp.Views.WPF 3.119.4`** ist ein Pin, der laut eigenem Kommentar nur
   wegen Mapsui existiert. Kein Quelltext im Repo verwendet SkiaSharp. Der Pin
   faellt deshalb mit.
4. **Drei Aufgaben statt vier.** Das Design sah vier Schritte vor. Schritt 2
   (Code loeschen) und Schritt 3 (Waechter anpassen) muessen zusammen einen
   Commit bilden, weil der Stand dazwischen sonst rot ist. Zusaetzlich muss
   `KarteDockingTests` bereits in Aufgabe 1 weichen, weil sie die
   Einstiegspunkte im Quelltext festnagelt.

## Dateistruktur

**Aufgabe 1 — Einstiegspunkte (Karte im Programm nicht mehr erreichbar):**
- Modify: `src/AuswertungPro.Next.UI/ViewModels/ShellViewModel.cs:141-152, 397-398`
- Modify: `src/AuswertungPro.Next.UI/ViewModels/ShellViewModel.NavigationSupport.cs:56`
- Modify: `src/AuswertungPro.Next.UI/MainWindow.xaml:122-127, 132-138`
- Modify: `src/AuswertungPro.Next.UI/MainWindow.xaml.cs:22, 201-303`
- Modify: `src/AuswertungPro.Next.UI/App.xaml:71-73`
- Delete: `tests/AuswertungPro.Next.UI.Tests/KarteDockingTests.cs`

**Aufgabe 2 — Code, Tests, Registrierungen, Waechter:**
- Delete: 25 Dateien in `src/AuswertungPro.Next.UI/Mapping/` (alle ausser
  `KatasterXtfPathResolver.cs`)
- Delete: `src/AuswertungPro.Next.UI/ViewModels/Pages/KarteViewModel.cs`,
  `KarteHaltungInfoBuilder.cs`
- Delete: `src/AuswertungPro.Next.UI/Views/Pages/KartePage.xaml` + `.xaml.cs`
- Delete: `src/AuswertungPro.Next.UI/Views/Windows/KarteWindow.xaml` + `.xaml.cs`
- Delete: `src/AuswertungPro.Next.UI/Services/KarteVideoLauncher.cs`
- Delete: `src/AuswertungPro.Next.Application/Map/IOfflineBasemapPathResolver.cs`
- Delete: `src/AuswertungPro.Next.Infrastructure/Map/OfflineBasemapDirectoryResolver.cs`
- Delete: 17 Testdateien (Liste in der Aufgabe)
- Modify: `src/AuswertungPro.Next.UI/ServiceProvider.cs:147, 149-152, 373-374`
- Modify: `src/AuswertungPro.Next.UI/ServiceProviderRegistrationMap.cs:110-111`
- Modify: `src/AuswertungPro.Next.UI/ViewModels/Pages/KarteHaltungNameMatcher.cs`
  (Namensvermerk)
- Modify: `src/AuswertungPro.Next.UI/Mapping/KatasterXtfPathResolver.cs`
  (Namensvermerk)
- Modify: `src/AuswertungPro.Next.UI/QgisBridge/QgisBridgeSelection.cs:5` (Kommentar)
- Modify: 6 Waechter-/Testdateien

**Aufgabe 3 — NuGet und Einstellungen:**
- Modify: `src/AuswertungPro.Next.UI/AuswertungPro.Next.UI.csproj:37-46`
- Modify: `src/AuswertungPro.Next.UI/AppSettings.cs:327-334`
- Modify: `src/AuswertungPro.Next.UI/Settings/SettingsProgramCleanupRequestFactory.cs:85`

---

## Aufgabe 1: Einstiegspunkte kappen

Nach dieser Aufgabe ist die Karte im laufenden Programm nicht mehr erreichbar,
der Code liegt aber noch vollstaendig da. Das ist der Punkt, an dem ein
unerwartetes Problem mit einem einzigen `git revert` erledigt waere.

**Files:**
- Modify: `src/AuswertungPro.Next.UI/ViewModels/ShellViewModel.cs`
- Modify: `src/AuswertungPro.Next.UI/ViewModels/ShellViewModel.NavigationSupport.cs`
- Modify: `src/AuswertungPro.Next.UI/MainWindow.xaml`
- Modify: `src/AuswertungPro.Next.UI/MainWindow.xaml.cs`
- Modify: `src/AuswertungPro.Next.UI/App.xaml`
- Delete: `tests/AuswertungPro.Next.UI.Tests/KarteDockingTests.cs`

**Interfaces:**
- Consumes: nichts (erste Aufgabe)
- Produces: Nach dieser Aufgabe existieren `KarteViewModel`, `KartePage`,
  `KarteWindow`, `KarteVideoLauncher` und `Mapping.KarteNetzVorladen` noch als
  Typen, werden aber von keinem Produktivpfad mehr aufgerufen.

- [ ] **Schritt 1: Navigationseintrag "Karte" aus ShellViewModel entfernen**

In `src/AuswertungPro.Next.UI/ViewModels/ShellViewModel.cs` diesen ganzen
Block (Zeilen 141-152) loeschen:

```csharp
            new("\uE707", "Karte", () => new AuswertungPro.Next.UI.Views.Pages.KartePage
            {
                DataContext = new Pages.KarteViewModel(
                    this,
                    settings: _sp.Settings,
                    networkFeatures: _sp.NetworkFeatures,
                    playVideo: KarteVideoLauncher.Create(_sp),
                    inspectionProtocolFiles: _sp.InspectionProtocolFiles,
                    katasterXtfPaths: _sp.KatasterXtfPaths,
                    offlineBasemapPaths: _sp.OfflineBasemapPaths,
                    basemapLayers: _sp.BasemapLayers)
            }),
```

Die Zeile davor (`new("\uE898", "Export", ...)`) und die danach
(`new("\uE7BA", "Medienkonflikte", ...)`) bleiben unveraendert.

- [ ] **Schritt 2: Hintergrund-Vorladen des Kartennetzes entfernen**

In derselben Datei diese zwei Zeilen (397-398) loeschen, samt der Leerzeile
davor:

```csharp

        // Kartennetz im Hintergrund vorladen -> die Karte ist beim ersten Oeffnen sofort da.
        Mapping.KarteNetzVorladen.ImHintergrund(_sp, p);
```

Die Methode endet danach mit `RefreshTitleAndDirty();` gefolgt von der
schliessenden Klammer.

- [ ] **Schritt 3: Hinweistext der Navigation entfernen**

In `src/AuswertungPro.Next.UI/ViewModels/ShellViewModel.NavigationSupport.cs`
Zeile 56 loeschen:

```csharp
            "Karte" => "Haltungen raeumlich ansehen und von der Karte aus oeffnen.",
```

- [ ] **Schritt 4: Beide Menuepunkte aus MainWindow.xaml entfernen**

In `src/AuswertungPro.Next.UI/MainWindow.xaml` den Menuepunkt "Karte..."
(Zeilen 122-127) loeschen:

```xml
                    <MenuItem Header="Karte..." Click="OpenKarte_Click"
                              ToolTip="Kartenansicht in einem eigenen Fenster oeffnen.">
                        <MenuItem.Icon>
                            <ui:FluentIcon Glyph="&#xE707;" Foreground="{DynamicResource MutedBrush}"/>
                        </MenuItem.Icon>
                    </MenuItem>
```

Und den Menuepunkt "Karte abkoppeln" (Zeilen 133-138) loeschen:

```xml
                    <MenuItem Header="Karte abkoppeln" Click="OpenKarte_Click"
                              ToolTip="Kartenansicht in einem eigenen Fenster oeffnen.">
                        <MenuItem.Icon>
                            <ui:FluentIcon Glyph="&#xE8A7;" Foreground="{DynamicResource MutedBrush}"/>
                        </MenuItem.Icon>
                    </MenuItem>
```

**Korrektur bei der Umsetzung:** Der `<Separator/>` in Zeile 132 bleibt stehen.
Beim Lesen im Umfeld zeigte sich, dass er nicht allein zum Kartenpunkt gehoert:
Er trennt "Fokusmodus" von den Fenster-Aktionen, und "System-Monitor oeffnen"
steht weiterhin dahinter. Das Menue "Ansicht" lautet danach: Fokusmodus,
Trennstrich, System-Monitor oeffnen.

- [ ] **Schritt 5: Abkoppel-Code aus MainWindow.xaml.cs entfernen**

In `src/AuswertungPro.Next.UI/MainWindow.xaml.cs` zuerst das Feld in Zeile 22
loeschen:

```csharp
    private KarteWindow? _detachedKarteWindow;
```

Dann den zusammenhaengenden Block der Zeilen 201-303 loeschen — das ist die
Leerzeile vor `OpenKarte_Click` bis zur schliessenden Klammer von
`CreateKarteDetachedPlaceholder`. Es sind genau diese fuenf Methoden:
`OpenKarte_Click`, `OpenExistingKartePage`, `OpenNewKarteWindow`,
`TrackDetachedKarteWindow`, `CreateKarteDetachedPlaceholder`.

Danach folgt auf das Ende der KI-Start-Methode direkt
`private void OpenSystemMonitor_Click(object sender, RoutedEventArgs e)`.

- [ ] **Schritt 6: DataTemplate aus App.xaml entfernen**

In `src/AuswertungPro.Next.UI/App.xaml` die Zeilen 71-73 loeschen:

```xml
            <DataTemplate DataType="{x:Type vm:KarteViewModel}">
                <views:KartePage/>
            </DataTemplate>
```

- [ ] **Schritt 7: KarteDockingTests loeschen**

Diese Datei nagelt genau die Einstiegspunkte im Quelltext fest, die eben
verschwunden sind — alle vier Testfaelle pruefen geloeschten oder gleich
folgenden Code (`shell.CurrentPage is KartePage currentPage`,
`networkFeatures: _sp.NetworkFeatures`, `KarteWindow.xaml`, `KartePage.xaml.cs`).
Sie muss deshalb schon jetzt weg, nicht erst in Aufgabe 2.

```bash
git rm tests/AuswertungPro.Next.UI.Tests/KarteDockingTests.cs
```

- [ ] **Schritt 7b: Navigationszaehler anpassen (erst beim Testlauf entdeckt)**

`DesignAuditThemeResourceTests.Shell_navigation_uses_unique_semantic_icons`
zaehlt die Eintraege der Navigationsliste und erwartet 16. Nach dem Entfernen
des Kartenpunkts sind es 15. Das ist der siebte Waechter — weder das Design
noch die Voruntersuchung hatten ihn gefunden, weil er die Zahl aus einem
Regex-Treffer ableitet und das Wort "Karte" nicht enthaelt.

In `tests/AuswertungPro.Next.UI.Tests/DesignAuditThemeResourceTests.cs`
Zeile 594 durch zwei Zeilen ersetzen — dieselbe Form wie beim
Registrierungszaehler, also mit Begruendung in der Historie:

```csharp
        // 15 -> 16: Navigationspunkt "Dossiers" (Eigentuemerdossier je Liegenschaft).
        // 16 -> 15: Kartenansicht entfernt; die raeumliche Arbeit laeuft ueber QGIS.
        Assert.Equal(15, matches.Count);
```

- [ ] **Schritt 8: Bauen**

**Vorher pruefen: SewerStudio ist geschlossen.**

```bash
dotnet build AuswertungPro.sln
```

Erwartet: erfolgreich, keine neuen Warnungen. Wenn ungenutzte `using`-Direktiven
in `MainWindow.xaml.cs` als Warnung erscheinen, diese entfernen — aber nur die,
die wirklich niemand mehr braucht.

- [ ] **Schritt 9: Testen**

```bash
dotnet test AuswertungPro.sln
```

Erwartet: vollstaendig gruen. Sollte etwas rot sein, ist es ein Waechter, den
die Voruntersuchung nicht gefunden hat — dann diesen Fund hier notieren, bevor
er behoben wird.

- [ ] **Schritt 10: Sichtpruefung im Programm**

Programm starten. Die Navigation links zeigt keinen Eintrag "Karte" mehr. Im
Menue gibt es weder "Karte..." noch "Karte abkoppeln". Alle uebrigen
Menuepunkte und Navigationseintraege sind unveraendert vorhanden.

- [ ] **Schritt 11: Committen**

```bash
git add -A src/AuswertungPro.Next.UI tests/AuswertungPro.Next.UI.Tests
git commit -m "refactor(karte): Einstiegspunkte der Kartenansicht entfernen

Navigation, beide Menuepunkte, DataTemplate und das Hintergrund-Vorladen
sind weg. Der Kartencode selbst liegt noch da und wird als naechstes
geloescht.

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>"
```

---

## Aufgabe 2: Code, Tests, Registrierungen und Waechter

Die grosse Aufgabe. Loeschen und Waechteranpassung gehoeren zwingend in einen
Commit — jeder Zwischenstand waere rot.

**Files:**
- Delete: 25 Mapping-Dateien, 7 weitere Quelldateien, 17 Testdateien (Listen unten)
- Modify: `src/AuswertungPro.Next.UI/ServiceProvider.cs`
- Modify: `src/AuswertungPro.Next.UI/ServiceProviderRegistrationMap.cs`
- Modify: `src/AuswertungPro.Next.UI/ViewModels/Pages/KarteHaltungNameMatcher.cs`
- Modify: `src/AuswertungPro.Next.UI/Mapping/KatasterXtfPathResolver.cs`
- Modify: `src/AuswertungPro.Next.UI/QgisBridge/QgisBridgeSelection.cs`
- Modify: `tests/AuswertungPro.Next.UI.Tests/ServiceProviderRegistrationTests.cs`
- Modify: `tests/AuswertungPro.Next.UI.Tests/DesignAuditThemeResourceTests.cs`
- Modify: `tests/AuswertungPro.Next.UI.Tests/ArchitectureDriftRatchetTests.cs`
- Modify: `tests/AuswertungPro.Next.UI.Tests/InspectionProtocolFileLocatorDependencyTests.cs`
- Modify: `tests/AuswertungPro.Next.UI.Tests/KatasterXtfPathResolverDependencyTests.cs`
- Modify: `tests/AuswertungPro.Next.UI.Tests/WindowOpenCloseSmokeTests.cs`

**Interfaces:**
- Consumes: Aufgabe 1 hat alle Produktivaufrufe der Kartentypen entfernt.
- Produces: `ServiceProvider` verliert die drei oeffentlichen Eigenschaften
  `OfflineBasemapPaths` (`IOfflineBasemapPathResolver`), `BasemapLayers`
  (`Mapping.IKarteBasemapLayerFactory`) und `NetworkFeatures`
  (`Mapping.NetworkFeatureCache`). Die Registrierungszahl in
  `ServiceProviderRegistrationMap` sinkt von 154 auf 152. Bestehen bleiben
  `KarteHaltungNameMatcher.Matches(string?, string?)` und
  `KatasterXtfPathResolver.Resolve(AppSettings)` mit unveraenderter Signatur.

- [ ] **Schritt 1: 25 Mapping-Dateien loeschen**

`KatasterXtfPathResolver.cs` bleibt als einzige Datei im Ordner.

```bash
cd src/AuswertungPro.Next.UI/Mapping
git rm DnLineWidthMapper.cs FliessrichtungsPfeilBuilder.cs HaltungDnProvider.cs \
  IKarteBasemapLayerFactory.cs KarteBasemapAuswahl.cs KarteBasemapLayerFactory.cs \
  KarteBasemapLayerService.cs KarteBasemapWahl.cs KarteDetailFeatureBuilder.cs \
  KarteNetzFeatureBuilder.cs KarteNetzVorladen.cs KarteSchachtFeatureBuilder.cs \
  KarteSchadenFeatureBuilder.cs KarteZoomStufenPolicy.cs LocalXyzTileSource.cs \
  MapPreloadPolicy.cs MapsuiColorExtensions.cs NetworkFeatureCache.cs \
  NetzLevelOfDetail.cs OfflineBasemapBaseResolver.cs OnlineXyzTileSource.cs \
  PolylineMath.cs SchachtSichtbarkeitPolicy.cs SchadenPositionInterpolator.cs \
  ZustandsklasseMapColors.cs
cd ../../..
```

Kontrolle: `ls src/AuswertungPro.Next.UI/Mapping/` zeigt nur noch
`KatasterXtfPathResolver.cs`.

- [ ] **Schritt 2: Restliche Kartenquelldateien loeschen**

```bash
git rm src/AuswertungPro.Next.UI/ViewModels/Pages/KarteViewModel.cs \
       src/AuswertungPro.Next.UI/ViewModels/Pages/KarteHaltungInfoBuilder.cs \
       src/AuswertungPro.Next.UI/Views/Pages/KartePage.xaml \
       src/AuswertungPro.Next.UI/Views/Pages/KartePage.xaml.cs \
       src/AuswertungPro.Next.UI/Views/Windows/KarteWindow.xaml \
       src/AuswertungPro.Next.UI/Views/Windows/KarteWindow.xaml.cs \
       src/AuswertungPro.Next.UI/Services/KarteVideoLauncher.cs \
       src/AuswertungPro.Next.Application/Map/IOfflineBasemapPathResolver.cs \
       src/AuswertungPro.Next.Infrastructure/Map/OfflineBasemapDirectoryResolver.cs
```

`KarteHaltungNameMatcher.cs` im selben Ordner `ViewModels/Pages/` bleibt.

- [ ] **Schritt 3: 17 Testdateien loeschen**

```bash
cd tests/AuswertungPro.Next.UI.Tests
git rm DnLineWidthMapperTests.cs FliessrichtungsPfeilBuilderTests.cs \
  KarteBasemapLayerDependencyTests.cs KarteBasemapLayerServiceTests.cs \
  KarteBasemapWahlTests.cs KarteHaltungInfoBuilderTests.cs \
  KarteSettingsPathTests.cs KarteZoomStufenPolicyTests.cs \
  MapPreloadPolicyTests.cs NetzLevelOfDetailTests.cs \
  OfflineBasemapBaseResolverTests.cs OfflineBasemapPathResolverDependencyTests.cs \
  PolylineMathTests.cs SchachtSichtbarkeitPolicyTests.cs \
  SchadenPositionInterpolatorTests.cs ZustandsklasseMapColorsTests.cs
cd ../..
git rm tests/AuswertungPro.Next.Infrastructure.Tests/Map/OfflineBasemapDirectoryResolverTests.cs
```

`KarteHaltungNameMatcherTests.cs`, `KatasterXtfPathResolverTests.cs` und
`KatasterXtfPathResolverDependencyTests.cs` bleiben — sie sichern Code ab, der
bestehen bleibt.

Hinweis zur Abdeckung: `KarteSettingsPathTests` enthielt mit
`KarteViewModel_NutztKatasterXtfAusOrdnerWennDateipfadAltIst` auch einen Test
fuer den Kataster-Rueckfall. Diese Logik bleibt vollstaendig durch
`KatasterXtfPathResolverTests` abgedeckt (bestehende Datei, bevorzugte Datei aus
Ordner, Ordner im Pfadfeld, groesste XTF als Rueckfall); es entsteht keine
Luecke.

- [ ] **Schritt 4: Drei Eigenschaften aus ServiceProvider entfernen**

In `src/AuswertungPro.Next.UI/ServiceProvider.cs` Zeile 147 loeschen:

```csharp
        public IOfflineBasemapPathResolver OfflineBasemapPaths { get; }
```

Und die Zeilen 149-152 loeschen (Eigenschaft, Kommentar und Singleton-Feld):

```csharp
        public Mapping.IKarteBasemapLayerFactory BasemapLayers { get; }
        // Kartennetz-Cache (Netzlinien + raeumlicher Index): einmal gebaut, ueber alle
        // Kartenoeffnungen wiederverwendet, beim Start vorladbar. Singleton.
        public AuswertungPro.Next.UI.Mapping.NetworkFeatureCache NetworkFeatures { get; } = new();
```

`public IVsaCatalogPathResolver VsaCatalogPaths { get; }` steht zwischen den
beiden Bloecken und bleibt.

Dann im Konstruktor die Zeilen 373-374 loeschen:

```csharp
            OfflineBasemapPaths = new OfflineBasemapDirectoryResolver();
            BasemapLayers = new Mapping.KarteBasemapLayerService();
```

- [ ] **Schritt 5: Zwei Registrierungen aus der Map entfernen**

In `src/AuswertungPro.Next.UI/ServiceProviderRegistrationMap.cs` die Zeilen
110-111 loeschen:

```csharp
            [typeof(IOfflineBasemapPathResolver)] = services.OfflineBasemapPaths,
            [typeof(Mapping.IKarteBasemapLayerFactory)] = services.BasemapLayers,
```

`NetworkFeatures` steht hier nicht — es war eine direkte Eigenschaft, keine
Vertragsregistrierung. Deshalb sinkt die Zahl um zwei, nicht um drei.

- [ ] **Schritt 6: Registrierungszahl im Waechter anpassen**

In `tests/AuswertungPro.Next.UI.Tests/ServiceProviderRegistrationTests.cs`
nach dem letzten Historieneintrag (`// 153 -> 154: ...`, Zeilen 101-102) eine
neue Begruendungszeile ergaenzen und die Zahl in den Zeilen 104-105 aendern:

```csharp
        // 154 -> 152: Kartenansicht entfernt (IOfflineBasemapPathResolver,
        // IKarteBasemapLayerFactory). Die raeumliche Arbeit laeuft ueber QGIS.
        Assert.True(
            registrations.Count == 152,
            $"Erwartet 152 Registrierungen, tatsaechlich {registrations.Count}. Bei einem neuen " +
```

Den Rest der Meldung unveraendert lassen.

- [ ] **Schritt 7: DesignAudit-Test auf die verbleibende Seite einengen**

In `tests/AuswertungPro.Next.UI.Tests/DesignAuditThemeResourceTests.cs` den
Test in den Zeilen 496-505 ersetzen. Vorher:

```csharp
    public void Map_and_counter_inspection_markers_use_fluent_icons()
    {
        var map = ReadUiFile("Views", "Pages", "KartePage.xaml");
        var holdings = ReadUiFile("Views", "Pages", "Haltungsansicht", "HaltungsansichtView.xaml");

        Assert.Contains("Glyph=\"&#xE91F;\"", map);
        Assert.DoesNotContain("Text=\"&#x25CF;\"", map);
        Assert.Contains("Glyph=\"&#xE8AB;\"", holdings);
        Assert.DoesNotContain("Text=\"⇄\"", holdings);
    }
```

Nachher — nur der Kartenteil faellt weg, der Name wird ehrlich:

```csharp
    public void Counter_inspection_markers_use_fluent_icons()
    {
        var holdings = ReadUiFile("Views", "Pages", "Haltungsansicht", "HaltungsansichtView.xaml");

        Assert.Contains("Glyph=\"&#xE8AB;\"", holdings);
        Assert.DoesNotContain("Text=\"⇄\"", holdings);
    }
```

Das `[Fact]`-Attribut ueber der Methode bleibt unveraendert stehen.

- [ ] **Schritt 8: KarteViewModel aus dem Architektur-Ratchet streichen**

In `tests/AuswertungPro.Next.UI.Tests/ArchitectureDriftRatchetTests.cs`
Zeile 34 loeschen:

```csharp
        "ViewModels/Pages/KarteViewModel.cs",
```

Der Kommentar ueber der Liste sagt ausdruecklich "Diese Liste darf schrumpfen,
niemals wachsen" — Streichen ist also genau der vorgesehene Weg.

- [ ] **Schritt 9: KarteViewModel aus zwei Dependency-Theories streichen**

In `tests/AuswertungPro.Next.UI.Tests/InspectionProtocolFileLocatorDependencyTests.cs`
Zeile 45 loeschen:

```csharp
    [InlineData(typeof(KarteViewModel), "_inspectionProtocolFiles")]
```

In `tests/AuswertungPro.Next.UI.Tests/KatasterXtfPathResolverDependencyTests.cs`
Zeile 85 loeschen:

```csharp
    [InlineData(typeof(KarteViewModel), "_katasterXtfPaths")]
```

Achtung: Diese zweite Datei bleibt bestehen. Nur die eine Zeile faellt; die
Eintraege fuer `ExportPageViewModel`, `SettingsPageViewModel` und
`QgisBridgeSnapshotBuilder` gehoeren zum QGIS-Weg und bleiben.

Falls eine `using`-Direktive danach in einer der beiden Dateien ungenutzt ist,
ebenfalls entfernen — aber nur, wenn der Compiler es meldet.

- [ ] **Schritt 10: KarteWindow aus dem Fenster-Rauchtest entfernen**

In `tests/AuswertungPro.Next.UI.Tests/WindowOpenCloseSmokeTests.cs` enthaelt
der erste Testfall ausschliesslich `KarteWindow`. Ein Test, der nach dem
Entfernen nichts mehr prueft, ist schlechter als kein Test — deshalb faellt der
ganze `[Fact]` weg (Zeilen 9-17):

```csharp
    [Fact]
    public void Einfache_fenster_lassen_sich_oeffnen_und_wieder_schliessen()
    {
        StaTestRunner.Run(() =>
        {
            OpenAndClose(new KarteWindow());
        });
    }
```

Der zweite Testfall `Fachfenster_lassen_sich_oeffnen_und_wieder_schliessen`
mit `HydraulikPanelWindow`, `FloatingGridWindow`, `LiveFrameWindow` und
`TextPreviewWindow` bleibt vollstaendig erhalten.

- [ ] **Schritt 11: Namensvermerk an KarteHaltungNameMatcher**

In `src/AuswertungPro.Next.UI/ViewModels/Pages/KarteHaltungNameMatcher.cs` den
Klassenkommentar ergaenzen. Vorher endet er mit:

```csharp
/// bleiben unberuehrt. Gleiche Regel wie im QGIS-Bridge, damit ein Kartenklick dieselbe
/// Haltung findet wie die Bridge.
/// </summary>
```

Nachher:

```csharp
/// bleiben unberuehrt.
///
/// Namensvermerk: Der Name stammt aus der am 2026-08-30 entfernten Kartenansicht.
/// Heute dient die Klasse ausschliesslich der QGIS-Auswahl
/// (DataPage/DataPageProjectBindingController). Sie darf spaeter umbenannt oder
/// ganz entfernt werden, wenn der QGIS-Weg sie nicht mehr braucht.
/// </summary>
```

- [ ] **Schritt 12: Namensvermerk an KatasterXtfPathResolver**

In `src/AuswertungPro.Next.UI/Mapping/KatasterXtfPathResolver.cs` ueber der
Klassendeklaration einen Kommentar einfuegen:

```csharp
/// <summary>
/// Loest den Pfad zur Abwasserkataster-XTF aus den Einstellungen auf.
///
/// Namensvermerk: Diese Klasse ist der einzige verbliebene Inhalt des Ordners
/// "Mapping". Die Kartenansicht wurde am 2026-08-30 entfernt; verwendet wird sie
/// heute von der QGIS-Bruecke, dem Einstellungs-Speicherweg und der Exportseite.
/// Sie darf spaeter in einen passender benannten Ordner verschoben werden.
/// </summary>
public static class KatasterXtfPathResolver
```

- [ ] **Schritt 13: Veralteten Kommentar in der QGIS-Bruecke berichtigen**

In `src/AuswertungPro.Next.UI/QgisBridge/QgisBridgeSelection.cs` nennt Zeile 5
Seiten, die es nicht mehr gibt. Vorher:

```csharp
/// auf welcher Seite oder in welchem Fenster (Haltungen-Seite, Karte-Seite, KarteWindow)
```

Nachher:

```csharp
/// auf welcher Seite (Haltungen-Seite, Schaechte-Seite, Dossier-Cockpit)
```

Die drei genannten Seiten sind belegt: `QgisBridgeSelection.Set` bzw.
`SetSchacht` wird nach dem Umbau noch aufgerufen von `DataPage.xaml.cs:217`,
`HaltungsansichtView.xaml.cs:163`, `SchaechtePage.QgisSelection.cs:24`,
`SchachtansichtView.xaml.cs:86`, `SchachtSanierungsMatrixPageViewModel.cs:137`
und `DossierQgisSelectionReporter.cs:15`. Der Rest des Kommentars (Abwahl
loescht nicht, Ruecksetzen beim Projektwechsel) bleibt unveraendert.

- [ ] **Schritt 14: Bauen**

**SewerStudio muss geschlossen sein.**

```bash
dotnet build AuswertungPro.sln
```

Erwartet: erfolgreich. Fehler an dieser Stelle sind uebersehene Referenzen —
die Meldung nennt Datei und Zeile. Alle ungenutzten `using`-Direktiven, die
der Compiler jetzt meldet, entfernen.

- [ ] **Schritt 15: Testen**

```bash
dotnet test AuswertungPro.sln
```

Erwartet: vollstaendig gruen.

- [ ] **Schritt 16: Committen**

```bash
git add -A
git commit -m "refactor(karte): Kartencode, Tests und Registrierungen entfernen

25 Mapping-Dateien, KarteViewModel, KartePage, KarteWindow, KarteVideoLauncher
und der Offline-Basemap-Resolver sind weg. Sechs Waechter angepasst; die
Registrierungszahl sinkt von 154 auf 152.

KarteHaltungNameMatcher und KatasterXtfPathResolver bleiben mit Namensvermerk
bestehen: die QGIS-Bruecke braucht sie.

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>"
```

---

## Aufgabe 3: NuGet-Pakete und Einstellungen entfernen

**Files:**
- Modify: `src/AuswertungPro.Next.UI/AuswertungPro.Next.UI.csproj`
- Modify: `src/AuswertungPro.Next.UI/AppSettings.cs`
- Modify: `src/AuswertungPro.Next.UI/Settings/SettingsProgramCleanupRequestFactory.cs`

**Interfaces:**
- Consumes: Nach Aufgabe 2 verwendet kein Quelltext mehr Mapsui oder
  `AppSettings.OfflineBasemapPath` / `AppSettings.QgisTilesPath` — mit der
  einen Ausnahme in `SettingsProgramCleanupRequestFactory`, die dieser Schritt
  aufloest.
- Produces: keine neuen oeffentlichen Schnittstellen.

- [ ] **Schritt 1: Mapsui und den SkiaSharp-Pin aus der csproj entfernen**

In `src/AuswertungPro.Next.UI/AuswertungPro.Next.UI.csproj` die Zeilen 37-46
loeschen — das Paket, den ganzen Erklaerkommentar und den nur deswegen
gesetzten SkiaSharp-Pin:

```xml
    <PackageReference Include="Mapsui.Wpf" Version="5.1.0" />
    <!-- Fix: Mapsui.Wpf 5.1.0 zieht SkiaSharp.Views.WPF als transitive Abhängigkeit rein.
         Ohne expliziten Pin greift NuGet auf net462-Assets zurück (net10.0-windows7.0 ist
         inkompatibel mit net8.0-windows10.0.19041 aus v3.119.2), was zum Laden der alten
         .NET-Framework-SKElement-Assembly führt. Diese bricht bei Pan/Drag unter .NET 10
         mit weißem Bildschirm (OnRender wird nicht ausgelöst / PresentationSource-Fallback
         funktioniert nicht). v3.119.4 fügt net10.0-windows10.0.19041 als explizites TFM
         hinzu; zusammen mit dem TargetFramework-Wechsel auf windows10.0.19041 wird die
         korrekte WPF-Assembly geladen. -->
    <PackageReference Include="SkiaSharp.Views.WPF" Version="3.119.4" />
```

Begruendung fuer den Pin: Der Kommentar sagt selbst, dass er nur wegen Mapsui
existiert, und im ganzen Repo verwendet kein Quelltext SkiaSharp. Sollte der
Build wider Erwarten scheitern, den SkiaSharp-Pin allein wieder eintragen und
den Grund im Commit festhalten.

Das `TargetFramework` bleibt unveraendert. Es wurde zwar seinerzeit wegen
Mapsui auf `windows10.0.19041` gehoben, wird aber inzwischen vom uebrigen
Programm mitgetragen und ist nicht Teil dieses Umbaus.

- [ ] **Schritt 2: Zwei Einstellungen aus AppSettings entfernen**

In `src/AuswertungPro.Next.UI/AppSettings.cs` die Zeilen 327-328 loeschen:

```csharp
    // Lokale QGIS-XYZ-Kacheln fuer die Kartenansicht. Fehlt der Ordner, bleibt es beim WMS.
    public string QgisTilesPath { get; set; } = DefaultQgisExportDirectory + @"\tiles_test";
```

Und die Zeilen 330-334:

```csharp
    // Offline-Hintergrundkarten: Basisordner im Programmordner mit den Unterordnern
    // "satellit" (SWISSIMAGE, JPEG) und "av" (AV-Karte farbig/Grundbuch, PNG), Kanton Uri z18.
    // Standard-Hintergrund der App-Karte; fehlt ein Ordner, wird stattdessen OSM online genutzt.
    // In den Einstellungen aenderbar.
    public string OfflineBasemapPath { get; set; } = @"c:\Sewer-Studio_KI_4.5\basemap_tiles";
```

`AbwasserkatasterXtfPath` und `KantonUriXtfDirectory` darueber bleiben — sie
gehoeren zur Verteilung und zum Kataster-Abgleich, nicht zur Karte.

Trotz des Namens ist `QgisTilesPath` kein Teil der QGIS-Bruecke: Kein
Produktivcode liest die Eigenschaft, sie wurde nur von der Kartenansicht
gebraucht.

Kein Versionstor und keine Migration noetig: Eine bestehende `settings.json`
mit diesen Feldern bleibt lesbar, weil unbekannte JSON-Felder beim Einlesen
ignoriert werden.

- [ ] **Schritt 3: Schutzeintrag aus der Aufraeum-Factory entfernen**

In `src/AuswertungPro.Next.UI/Settings/SettingsProgramCleanupRequestFactory.cs`
in `BuildProtectedProjectRoots` die Zeile 85 loeschen und das Komma der
Vorzeile entfernen. Vorher:

```csharp
            TryNormalizeDirectory(settings.KantonUriXtfDirectory),
            TryNormalizeDirectory(settings.OfflineBasemapPath)
        };
```

Nachher:

```csharp
            TryNormalizeDirectory(settings.KantonUriXtfDirectory)
        };
```

Geprueft und unbedenklich: `ProgramCleanupService` durchsucht nur die
Traversal-Wurzeln `src`, `tests`, `tools`, `sidecar`, `training`,
`integrations` und `.worktrees` und loescht dort ausschliesslich `bin`, `obj`,
`TestResults` sowie Python-Cache-Ordner. Der Standardpfad
`c:\Sewer-Studio_KI_4.5\basemap_tiles` (8,8 GB) liegt direkt unter dem
Programmordner, wird nie durchlaufen und war damit auch vorher nie gefaehrdet.
Der Schutzeintrag war wirkungslose Vorsicht.

- [ ] **Schritt 4: Bauen**

**SewerStudio muss geschlossen sein.**

```bash
dotnet build AuswertungPro.sln
```

Erwartet: erfolgreich. Beim ersten Bau nach dem Paketwechsel laedt NuGet neu.

- [ ] **Schritt 5: Testen**

```bash
dotnet test AuswertungPro.sln
```

Erwartet: vollstaendig gruen.

- [ ] **Schritt 6: Sichtpruefung im Programm — der eigentliche Beweis**

Programm starten und drei Dinge pruefen:

1. Die Navigation zeigt keinen Eintrag "Karte"; das Menue keine Punkte
   "Karte..." oder "Karte abkoppeln".
2. Die Einstellungsseite laesst sich oeffnen und speichern.
3. **Eine Auswahl in QGIS springt weiterhin auf die richtige Haltung.** Das ist
   der Beweis, dass `KarteHaltungNameMatcher` und die Bruecke unbeschaedigt
   sind — der einzige Punkt, den kein Test abdeckt.

- [ ] **Schritt 7: Committen**

```bash
git add -A
git commit -m "refactor(karte): Mapsui-Paket und Karteneinstellungen entfernen

Mapsui.Wpf und der nur deswegen gesetzte SkiaSharp-Pin sind raus, ebenso
QgisTilesPath und OfflineBasemapPath. Bestehende settings.json bleiben
lesbar, weil unbekannte Felder ignoriert werden.

Der Ordner basemap_tiles bleibt auf der Platte unberuehrt.

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>"
```

---

## Beweis — der Umbau gilt erst als belegt, wenn alles zutrifft

- [ ] `dotnet build AuswertungPro.sln` ohne Fehler und ohne neue Warnungen
- [ ] `dotnet test AuswertungPro.sln` vollstaendig gruen
- [ ] `ls src/AuswertungPro.Next.UI/Mapping/` zeigt nur `KatasterXtfPathResolver.cs`
- [ ] `grep -rn "Mapsui" --include=*.cs --include=*.xaml --include=*.csproj src tests`
      liefert nichts (Treffer unter `obj/` sind alte Zwischenstaende und zaehlen nicht)
- [ ] Navigation ohne "Karte", Menue ohne "Karte..." und ohne "Karte abkoppeln"
- [ ] QGIS-Auswahl springt weiterhin auf die richtige Haltung
- [ ] Ordner `basemap_tiles` unveraendert vorhanden
