# Gegenpruefung Aussensicht-Audit - 2026-05-10

Quelle: vom Nutzer eingefuegter "Audit SewerStudio - Aussensicht".  
Ziel: Behauptungen gegen den aktuellen Workspace `C:\Sewer-Studio_KI_4.3`
pruefen, ohne Ruecksicht-Bonus, aber auch ohne alte Befunde ungeprueft zu
uebernehmen.

## Kurzfazit

Der Fremdaudit trifft den Repo-Eindruck bei Legacy-Doku, alter PowerShell-App,
leerer `AuswertungPro.Wpf`-Shell, Tool-Spikes und net10-/Setup-Dokumentation gut.

Mehrere der schwersten Punkte sind im aktuellen Stand aber falsch oder
ueberholt:

- `src/AuswertungPro.Next.UI/artifacts/` ist aktuell nicht getrackt.
- `*_wpftmp.csproj` ist aktuell nicht getrackt.
- `sidecar/yolo11m.pt` ist aktuell nicht getrackt.
- Es gibt Central Package Management via `Directory.Packages.props`.
- Es gibt nur eine getrackte Solution: `AuswertungPro.sln`.
- `KnowledgeBaseContext` liegt aktuell in Infrastructure, nicht in UI.
- `README.md` erwaehnt Sidecar, YOLO, Ollama und `AuswertungPro.Next`.
- Die genannten God-File-Zeilenzahlen sind fuer die aktuellen Dateien deutlich
  zu hoch, weil inzwischen vieles gesplittet wurde.
- `.NET 10` ist am 2026-05-10 nicht mehr automatisch "Preview"; der
  Dokumentations-/Reproduzierbarkeits-Punkt bleibt aber gueltig.

## Gepruefte Behauptungen

| Behauptung | Status | Aktueller Befund |
| --- | --- | --- |
| 262 MB `artifacts/` im Git, ca. 1378 getrackte Dateien | Falsch fuer aktuellen Tree | `git ls-files src/AuswertungPro.Next.UI/artifacts` ergibt 0. Der Ordner existiert aktuell nicht im Workspace. History wurde nicht per `git filter-repo`/`rev-list` analysiert. |
| WPF-Build-Tempdatei `*_wpftmp.csproj` getrackt | Falsch fuer aktuellen Tree | `git ls-files "*_wpftmp.csproj"` ergibt 0. |
| Tote Zwillings-UI `src/AuswertungPro.Wpf` | Stimmt | `src/AuswertungPro.Wpf/AuswertungPro.Wpf.csproj` ist getrackt, nicht in der Root-Solution, und enthaelt keine echte App-Quelle. |
| Alle Projekte targeten `net10.0` / `net10.0-windows` | Stimmt | Domain/Application/Infrastructure targeten `net10.0`, UI `net10.0-windows10.0.19041`, `AuswertungPro.Wpf` `net10.0-windows`. |
| `.NET 10` sei Preview | Falsch/ueberholt | Stand 2026-05-10 ist .NET 10 nicht mehr automatisch Preview. Trotzdem fehlt `global.json`, daher bleibt SDK-Reproduzierbarkeit ein echter Punkt. |
| AI/KnowledgeBase/SQLite liegen in UI | Teilweise veraltet | `UI/Ai` existiert, aber nur mit 23 `.cs`-Dateien. `KnowledgeBaseContext` und SQLite-Zugriffe liegen aktuell unter `Infrastructure/Ai`. UI referenziert `Microsoft.Data.Sqlite` noch im csproj, aber direkte SQLite-Usings in UI-Code wurden nicht gefunden. |
| Zwei widerspruechliche READMEs | Teilweise | `README.md` ist aktuell und nennt .NET, Sidecar, YOLO, Ollama und `AuswertungPro.Next`. `README_v2.md` ist historische PowerShell-Doku. Die Koexistenz ist verwirrend, aber nicht beide READMEs sind gleichermassen "Wahrheit". |
| `START.md` verweist tot auf `docs/...`; `docs/` existiere nicht | Teilweise falsch | `docs/` existiert. Der konkrete Link `docs/Programmpruefung_AuswertungPro_2026-02-20.md` scheint nicht vorhanden. `START.md` hat aber bereits einen historischen Disclaimer. |
| God-Files mit 4580/4382/3298/2856/2436 Zeilen | Falsch/ueberholt fuer genannte Dateien | Aktuell: `PlayerWindow.xaml.cs` 678, `HoldingFolderDistributor.cs` 137, `DataPageViewModel.cs` 606, `ProtocolPdfExporter.cs` 860, `DataPage.xaml.cs` 810. Es gibt trotzdem grosse UI-Dateien wie `CodingModeWindow.xaml.cs`. |
| Kein Central Package Management, Package-Drift Sqlite 8 vs 10 | Falsch fuer Hauptprojekte | `Directory.Packages.props` ist vorhanden und aktiviert `ManagePackageVersionsCentrally`. Hauptprojekte nutzen versionslose PackageReferences. |
| Tool-Spike-Projekte nicht in Root-Solution | Stimmt im Kern | 11 Tool-`csproj` gefunden, nicht in `AuswertungPro.sln`. Einige haben inline Package-Versionen, z.B. `PdfHeaderReader` mit altem PdfPig Alpha. |
| Pro-Projekt-Solutions neben Root-Solution | Falsch fuer getrackten Tree | `git ls-files "*.sln"` zeigt nur `AuswertungPro.sln`. Weitere `.sln` liegen in ignorierten `.claude/worktrees`, nicht im Git-Tree. |
| PowerShell-Schatten-App in der Wurzel | Stimmt | Zahlreiche `.ps1` und `Services/*.ps1` sind getrackt und beschreiben/implementieren die alte PowerShell-App. |
| `sidecar/yolo11m.pt` eingecheckt | Falsch fuer aktuellen Tree | `git ls-files sidecar/yolo11m.pt` ergibt 0. Lokale Modellgewichte existieren im Workspace, sind aber ignoriert. |
| Hand-DI `src/AuswertungPro.Next.UI/ServiceProvider.cs` mit 443 Zeilen | Falsch/ueberholt | Datei existiert nicht. Aktuell wird `Microsoft.Extensions.DependencyInjection` ueber `Composition/ServiceCollectionConfigurator.cs` und `App.xaml.cs` genutzt. |
| Doku-Karteileichen / drei Marken | Stimmt im Kern | `README_v2.md`, `START.md`, `LIEFERUEBERSICHT.md`, `DATEIEN_MANIFEST.md`, `ARCHITECTURE.md` und `CLAUDE.md` zeigen historische PowerShell- oder KI-spezifische Perspektiven. Teilweise gibt es Disclaimer, aber die Root-Struktur bleibt fuer Neulinge unklar. |

## Was aus dem Fremdaudit uebernommen werden sollte

1. Legacy-PowerShell-App und Legacy-Doku sichtbar archivieren oder aus dem Root
   entfernen.
2. `src/AuswertungPro.Wpf` klaeren: loeschen, archivieren oder in Solution
   aufnehmen, aber nicht als leere Shell liegen lassen.
3. Tool-Projekte klassifizieren: behalten, archivieren oder in eine separate
   Tools-Solution aufnehmen.
4. Reproduzierbarkeit verbessern: `global.json`, NuGet-Quelle fuer
   `UglyToad.PdfPig 1.7.0-custom-5`, Restore-/Setup-Doku, ggf. Lockfiles.
5. Historische Docs in `docs/archive/legacy-powershell/` verschieben und
   README.md als einzige Einstiegstuer halten.

## Was nicht ungeprueft uebernommen werden sollte

1. `git filter-repo` fuer `artifacts/` oder `sidecar/yolo11m.pt` nur dann
   ansetzen, wenn eine separate History-Pruefung zeigt, dass diese Dateien
   wirklich in alten Commits liegen. Im aktuellen Tree sind sie nicht getrackt.
2. "Kein CPM" und Package-Drift in Hauptprojekten sind falsch. CPM existiert.
3. "KnowledgeBaseContext in UI" ist falsch fuer den aktuellen Stand.
4. "Pro-Projekt-Solutions im Git" ist falsch fuer den aktuellen getrackten Tree.
5. "README erwaehnt KI-Pipeline nirgends" ist falsch fuer die aktuelle
   `README.md`.

## Ergebnis

Der Fremdaudit ist als Warnsignal nuetzlich, aber nicht als aktueller
Tatsachenbericht. Seine groesste valide Botschaft ist Repo-Hygiene und
Onboarding-Klarheit: alte PowerShell-Welt, neue .NET-Welt, Tool-Projekte und
KI-Sidecar muessen im Root eindeutig sortiert werden.

Die technischen Kernbefunde aus dem aktuellen Audit bleiben davon unberuehrt:
Import/Matching ist stark; Runtime-/UI-Risiken liegen vor allem bei VLC,
Codiermodus, grossen WPF-Klassen, relativen Medienlinks und Fresh-Machine-Setup.
