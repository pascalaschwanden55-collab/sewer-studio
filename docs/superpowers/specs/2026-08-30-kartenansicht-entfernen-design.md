# Kartenansicht entfernen — Design

Datum: 2026-08-30
Status: freigegeben, Umsetzungsplan folgt

## Ausgangslage

SewerStudio besitzt eine eingebaute Kartenansicht auf Basis von Mapsui. Sie wurde
nie produktiv genutzt. Die raeumliche Arbeit laeuft ueber die QGIS-Bruecke, die
effektiver und funktionsreicher ist.

Die Kartenansicht bleibt damit toter Code: rund 2500 Zeilen, 18 Testdateien,
zwei ServiceProvider-Registrierungen und das NuGet-Paket `Mapsui.Wpf 5.1.0`
samt seiner transitiven SkiaSharp-Abhaengigkeit.

## Ziel und Nicht-Ziel

**Ziel:** Die Kartenansicht vollstaendig entfernen — Einstiegspunkte, Code,
Tests, Registrierungen, NuGet-Paket und die rein kartenbezogenen Einstellungen.

**Nicht-Ziel:** Die QGIS-Bruecke wird nicht angefasst. Alles, was der QGIS-Weg
braucht, bleibt unveraendert bestehen — auch wenn "Karte" im Namen steht.

## Was verschwindet

### Einstiegspunkte

Nach diesen vier Aenderungen ist die Karte im Programm nicht mehr erreichbar:

- Navigationseintrag `"Karte"` in `ShellViewModel.cs:141` samt Hinweistext in
  `ShellViewModel.NavigationSupport.cs:56`
- Menuepunkte `Karte...` und `Karte abkoppeln` in `MainWindow.xaml:122` und
  `:133` samt dem gesamten Abkoppel-/Andock-Code in `MainWindow.xaml.cs`
  (`OpenKarte_Click`, `OpenExistingKartePage`, `OpenNewKarteWindow`,
  `TrackDetachedKarteWindow`, `CreateKarteDetachedPlaceholder`, Feld
  `_detachedKarteWindow`)
- `DataTemplate` fuer `KarteViewModel` in `App.xaml:71`
- Hintergrund-Vorladen `Mapping.KarteNetzVorladen.ImHintergrund(_sp, p)` in
  `ShellViewModel.cs:398`

### Code

- Ordner `src/AuswertungPro.Next.UI/Mapping/` — 25 der 26 Dateien.
  `KatasterXtfPathResolver.cs` bleibt (siehe unten).
- `ViewModels/Pages/KarteViewModel.cs`
- `ViewModels/Pages/KarteHaltungInfoBuilder.cs`
- `Views/Pages/KartePage.xaml` und `.xaml.cs`
- `Views/Windows/KarteWindow.xaml` und `.xaml.cs`
- `Services/KarteVideoLauncher.cs`
- `src/AuswertungPro.Next.Application/Map/IOfflineBasemapPathResolver.cs`
- `src/AuswertungPro.Next.Infrastructure/Map/OfflineBasemapDirectoryResolver.cs`

### Registrierungen

`ServiceProvider.cs` und `ServiceProviderRegistrationMap.cs` verlieren
`OfflineBasemapPaths` (`IOfflineBasemapPathResolver`) und `BasemapLayers`
(`Mapping.IKarteBasemapLayerFactory`). Auch das Singleton-Feld `NetworkFeatures`
(`NetworkFeatureCache`) faellt weg; es ist keine Vertragsregistrierung, sondern
eine direkte Eigenschaft.

Die in `ServiceProviderRegistrationTests` fest erwartete Zahl sinkt von
**154 auf 152**.

### NuGet

`Mapsui.Wpf 5.1.0` wird aus `AuswertungPro.Next.UI.csproj` entfernt, samt dem
danebenstehenden Kommentar zur transitiven `SkiaSharp.Views.WPF`-Abhaengigkeit.

Gepruefte Voraussetzung: Mapsui wird ausschliesslich im Ordner `Mapping/`, in
`KarteViewModel.cs`, in `KartePage.xaml` und in zwei Testdateien verwendet —
alle davon werden geloescht.

### Einstellungen

Aus `AppSettings.cs` fallen `QgisTilesPath` und `OfflineBasemapPath` weg.

Es entsteht **kein Migrationsbedarf**: Eine bestehende `settings.json` mit diesen
Feldern wird weiter gelesen, weil unbekannte JSON-Felder beim Einlesen ignoriert
werden. Ein Versionstor ist deshalb nicht noetig.

## Was bleibt — und warum

| Bleibt | Grund |
|---|---|
| `ViewModels/Pages/KarteHaltungNameMatcher.cs` | Der QGIS-Weg braucht ihn: `DataPageProjectBindingController.cs:148` findet damit den Projektdatensatz zur QGIS-Auswahl |
| `Mapping/KatasterXtfPathResolver.cs` | Verwendet von `QgisBridgeSnapshotBuilder`, `SettingsSaveWorkflow`, `ExportPageViewModel` und `SettingsPageViewModel` |
| `AppSettings.AbwasserkatasterXtfPath` | Gehoert zur Verteilung und zum Kataster-Abgleich, nicht zur Karte |
| `tests/.../KarteHaltungNameMatcherTests.cs` | Sichert den bleibenden Matcher ab |
| `tests/.../KatasterXtfPathResolverTests.cs` und `...DependencyTests.cs` | Sichern den bleibenden Resolver ab |
| Ordner `basemap_tiles` auf der Platte | Reine Nutzdaten, nicht im Git. Bewusste Entscheidung: unangetastet lassen |

### Namensvermerk statt Umbenennung

`KarteHaltungNameMatcher` traegt nach dem Umbau einen irrefuehrenden Namen: Es
gibt keine Karte mehr, aber die Klasse heisst noch danach. Eine Umbenennung waere
Arbeit an funktionierendem Code und wurde bewusst zurueckgestellt.

Stattdessen erhaelt die Klasse einen Kommentar im Kopf, der festhaelt:

- Der Name stammt aus der entfernten Kartenansicht.
- Heute dient sie ausschliesslich der QGIS-Auswahl.
- Sie darf spaeter umbenannt oder ganz entfernt werden, wenn der QGIS-Weg sie
  nicht mehr braucht.

Dasselbe gilt sinngemaess fuer `KatasterXtfPathResolver` im dann sonst leeren
Ordner `Mapping/`.

## Tests

Geloescht werden 18 Testdateien:

`DnLineWidthMapperTests`, `FliessrichtungsPfeilBuilderTests`,
`KarteBasemapLayerDependencyTests`, `KarteBasemapLayerServiceTests`,
`KarteBasemapWahlTests`, `KarteDockingTests`, `KarteHaltungInfoBuilderTests`,
`KarteSettingsPathTests`, `KarteZoomStufenPolicyTests`, `MapPreloadPolicyTests`,
`NetzLevelOfDetailTests`, `OfflineBasemapBaseResolverTests`,
`OfflineBasemapPathResolverDependencyTests`, `PolylineMathTests`,
`SchachtSichtbarkeitPolicyTests`, `SchadenPositionInterpolatorTests`,
`ZustandsklasseMapColorsTests` (UI-Tests) sowie
`Map/OfflineBasemapDirectoryResolverTests` (Infrastructure-Tests).

### Geprueft: keine Abdeckungsluecke

`KarteSettingsPathTests` prueft mit
`KarteViewModel_NutztKatasterXtfAusOrdnerWennDateipfadAltIst` auch den
Kataster-Rueckfall, also Logik die bleibt. Diese Logik ist bereits vollstaendig
durch `KatasterXtfPathResolverTests` abgedeckt (bestehende Datei, bevorzugte
Datei aus Ordner, Ordner im Pfadfeld, groesste XTF als Rueckfall). Beim Loeschen
geht keine Absicherung verloren.

### Drei Waechter werden angepasst, nicht geloescht

Das ist die einzige echte Fallgrube des Umbaus:

1. **`ServiceProviderRegistrationTests`** — erwartete Registrierungszahl
   154 -> 152, mit einer neuen Begruendungszeile in der bestehenden Historie
   (`// 154 -> 152: Kartenansicht entfernt, ...`).
2. **`DesignAuditThemeResourceTests.Map_and_counter_inspection_markers_use_fluent_icons`**
   — prueft in einem Test **zwei** Seiten. Der `KartePage.xaml`-Teil faellt weg,
   der `HaltungsansichtView.xaml`-Teil bleibt bestehen. Der Test wird auf die
   verbleibende Seite eingeengt und passend umbenannt.
3. **`ArchitectureDriftRatchetTests`** — `ViewModels/Pages/KarteViewModel.cs`
   wird aus der Ausnahmeliste gestrichen.

### Nicht betroffen

- `UebersprungeneTestsWaechterTests`: Die dortigen Treffer auf "Kartendienst"
  meinen den Grundbuch-Dienst des Kantons Uri (Dossiers), nicht die
  Kartenansicht. Die sieben erlaubten Skip-Stellen bleiben unveraendert.
- `CLAUDE.md`: erwaehnt die Kartenansicht, `KartePage` und Mapsui nirgends.
  Keine Doku-Nachfuehrung noetig.
- Kein Test bindet den Navigationseintrag `"Karte"` fest.

## Vorgehen

Vier Schritte, jeder fuer sich baubar und einzeln committet:

1. **Einstiegspunkte kappen** — die Karte ist im Programm weg, der Code liegt
   noch. Sofort rueckgaengig zu machen, falls sich doch etwas zeigt.
2. **Code und Tests loeschen, Registrierungen zurueckbauen** — inklusive der
   beiden Namensvermerke an den bleibenden Klassen.
3. **Waechtertests anpassen** — die drei oben genannten.
4. **NuGet und Einstellungen entfernen** — Mapsui, `QgisTilesPath`,
   `OfflineBasemapPath`.

## Beweis

Belegt ist der Umbau erst, wenn alles davon zutrifft:

- `dotnet build AuswertungPro.sln` laeuft ohne Fehler und ohne neue Warnungen.
- `dotnet test AuswertungPro.sln` ist vollstaendig gruen.
- Im gestarteten Programm: Die Navigation zeigt keinen Eintrag "Karte", das
  Menue keine Punkte "Karte..." / "Karte abkoppeln".
- Eine Auswahl in QGIS springt weiterhin auf die richtige Haltung — der Beweis,
  dass `KarteHaltungNameMatcher` und die Bruecke unbeschaedigt sind.

**Wichtig beim Bauen:** SewerStudio muss geschlossen sein. Ein laufendes Programm
sperrt die DLLs, und dann wird ein alter Stand getestet.
