# Anweisung für Codex: Kataster-Abgleich in der Dichtheits-Verteilung anschließen

**Revier:** UI (`AppSettings.cs`, `ExportPageViewModel.cs`) — dein Bereich.
**Die ganze Logik ist fertig und an echten Daten getestet** (Claude, Infrastructure). Du schließt nur 3 Stellen an.
**Wichtig:** rein additiv. Ohne Kataster verhält sich die Verteilung exakt wie bisher.

## Hintergrund (was schon fertig ist)

Beim Verteilen von Dichtheitsprüfungs-PDFs wird das Schacht-Paar (in beliebiger Reihenfolge) gegen den amtlichen Abwasserkataster aufgelöst → korrekte Haltungsnummer in richtiger Reihenfolge. Getestet an 3 echten KIT-Prüfberichten: vorher 0 zugeordnet, jetzt 18/23 Seiten korrekt, 0 Falsch-Treffer (Schacht-Prüfungen und nicht-im-Kataster-Paare bleiben korrekt unzugeordnet).

Fertige Bausteine in `src/AuswertungPro.Next.Infrastructure/Map/`:
- `HaltungCadastreIndex` (implementiert `IHaltungCadastreResolver`)
- `HaltungCadastreIndex.EnsureAndLoad(xtfPfad)` → baut die Tabelle einmal (fest im SewerStudio-Ordner `%LOCALAPPDATA%\SewerStudio\map\abwasserkataster_haltungen.tsv`, ~2 s beim ersten Mal, danach sofort) und liefert den Resolver.
- `HoldingFolderDistributor.DistributeDichtheit(...)` und `DistributeDichtheitFiles(...)` haben bereits einen **optionalen** Parameter `IHaltungCadastreResolver? cadastre = null`.

## Schritt 1 — Einstellung hinzufügen

In `src/AuswertungPro.Next.UI/AppSettings.cs` (Klasse `AppSettings`, neben `EvalSetRoot` bei Zeile ~82), neue Property im gleichen Stil:

```csharp
/// <summary>Amtlicher Abwasserkataster (SIA405-XTF) für die Haltungs-Zuordnung bei der Verteilung.</summary>
public string AbwasserkatasterXtfPath { get; set; } = @"D:\QGIS_V4\Export_Sewer_Studio\Abwasserkataster_Uri_korrigiert.xtf";
```

(Optional, schön: ein Textfeld + „Durchsuchen"-Button auf der Einstellungs-Seite, damit der Pfad in der UI änderbar ist. Nicht zwingend für die Funktion.)

## Schritt 2 — Resolver bauen + übergeben

In `src/AuswertungPro.Next.UI/ViewModels/Pages/ExportPageViewModel.cs`, Methode `DistributeDichtheitAsync()` (ab Zeile ~441).

Oben die Using-Zeile ergänzen (falls nicht vorhanden):
```csharp
using AuswertungPro.Next.Infrastructure.Map;
```

Direkt **vor** dem `IReadOnlyList<...> results;` (≈ Zeile 485) den Resolver laden (off-thread, damit die UI nicht hängt):
```csharp
// Amtlichen Kataster laden (einmaliger Tabellen-Bau, danach gecached im SewerStudio-Ordner).
IHaltungCadastreResolver? cadastre = null;
var katasterPfad = _sp.Settings.AbwasserkatasterXtfPath;
try
{
    cadastre = await Task.Run(() => HaltungCadastreIndex.EnsureAndLoad(katasterPfad));
}
catch
{
    // Kataster optional: ohne ihn läuft die Verteilung wie bisher.
}
```

Dann bei **beiden** Aufrufen `cadastre: cadastre` ergänzen:

```csharp
results = await Task.Run(() => HoldingFolderDistributor.DistributeDichtheitFiles(
    pdfFiles: selectedPdfFiles,
    destGemeindeFolder: destFolder,
    moveInsteadOfCopy: false,
    overwrite: false,
    project: _shell.Project,
    progress: progress,
    cadastre: cadastre));     // << NEU
```
```csharp
results = await Task.Run(() => HoldingFolderDistributor.DistributeDichtheit(
    pdfSourceFolder: pdfFolder!,
    destGemeindeFolder: destFolder,
    moveInsteadOfCopy: false,
    overwrite: false,
    project: _shell.Project,
    progress: progress,
    cadastre: cadastre));     // << NEU
```

Das ist alles. `_sp.Settings` ist die `AppSettings`-Instanz (wie an anderen Stellen genutzt).

## Grenzen (bitte einhalten)

- **Rein additiv.** Nur die beiden Dichtheits-Aufrufe bekommen `cadastre:`. Die normale PDF-Verteilung (`DistributeFiles`/`Distribute`) und die Schacht-/TXT-Verteilung NICHT anfassen — die haben den Parameter (noch) nicht, das würde nicht kompilieren.
- Wenn die Kataster-Datei fehlt, ist `cadastre` einfach `null` → alte Verhaltensweise. Kein harter Fehler.
- Erststart baut die Tabelle (~2 s, 600 MB XTF streamen); läuft in `Task.Run`, blockiert die UI nicht.

## Test danach

„Dichtheitsprüfung verteilen" mit den KIT-Prüfberichten aus `H:\swisstransfer_...` → die Haltungen (865-864, 866-865, 6926-6925 …) landen in korrekt benannten Ordnern. Schacht-Prüfungen (z.B. „Schacht (W110)") bleiben bewusst unzugeordnet.

## Rückfrage an Claude

Wenn der Kataster-Schritt auch in der **normalen PDF-Verteilung** (Haltungsinspektionen) oder der **Schacht-Verteilung** helfen soll, sag Bescheid — dann ergänze ich den `cadastre`-Parameter dort in der Infrastructure (mein Revier), und du schließt ihn analog an.
