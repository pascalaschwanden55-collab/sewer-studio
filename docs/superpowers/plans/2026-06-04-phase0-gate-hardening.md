# Phase 0 Gate Hardening Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Haerte die Self-Training-Gates so, dass ein unbeaufsichtigter Batch keine falschen Labels automatisch zu Gold/KB macht.

**Architecture:** Die Umsetzung bleibt deterministisch und schmal: Katalog-/Validatorlogik in Domain, Import-Pairing in Infrastructure, Auto-Gold-Entscheidung in Application. Sidecar, GPU, YOLO/DINO/SAM und UI bleiben unberuehrt.

**Tech Stack:** C#/.NET 10, xUnit, bestehende Projekte `AuswertungPro.Next.Domain`, `AuswertungPro.Next.Application`, `AuswertungPro.Next.Infrastructure`.

---

## Scope Check

Die Spec deckt mehrere Gates ab, aber alle gehoeren zum selben Self-Training-Sicherheitsnetz. Die Reihenfolge bleibt: C-Teil1, C-Teil2, D, A+B. Jeder Gate-Block bekommt eigene Tests und einen eigenen Commit.

## File Structure

- Modify: `src/AuswertungPro.Next.Domain/VsaCatalog/VsaCodeTree.cs`
  - Nur Kommentartexte im `StreckenschadenCodes`-Bereich korrigieren.
- Create: `src/AuswertungPro.Next.Domain/VsaCatalog/VsaCodeValidator.cs`
  - Reine statische Katalogpruefung fuer PDF-Parse-Eintritt.
- Modify: `src/AuswertungPro.Next.Infrastructure/Ai/VsaCodeResolver.cs`
  - Kurzer Kommentar im aktiven `StreckenschadenCodes`-Set, dass `VsaCodeTree.Groups["BB"]` die Bedeutungsquelle ist.
- Modify: `src/AuswertungPro.Next.Infrastructure/Ai/Training/TrainingCenterImportService.cs`
  - Parser mit `VsaCodeValidator` schuetzen.
  - `ResolvePair` und `ResolveProtocolOnlyPair` fuer Video/PDF-Paarung einfuehren.
  - `ScanAsync` und `ScanProtocolOnlyAsync` verdrahten.
- Modify: `src/AuswertungPro.Next.Application/Ai/Training/TrainingCenterSettings.cs`
  - Neue Auto-Gold-Gate-Schalter.
- Modify: `src/AuswertungPro.Next.Application/Ai/Training/SelfTrainingAutoAcceptPolicy.cs`
  - KB-Agreement, Confidence und Frame-Zuverlaessigkeit in `Decide`.
- Create: `src/AuswertungPro.Next.Application/Ai/Training/SelfTrainingFramePositionPolicy.cs`
  - Reine Hilfslogik fuer `PdfPhoto`/`VideoTimestamp` vs. `VideoLinear`.
- Modify: `src/AuswertungPro.Next.Infrastructure/Ai/Training/SelfTrainingOrchestrator.cs`
  - Settings und Frame-Zuverlaessigkeit an `Decide` durchreichen.
- Create: `tests/AuswertungPro.Next.Pipeline.Tests/VsaCodeValidatorTests.cs`
  - Domain-Validator-Tests.
- Create: `tests/AuswertungPro.Next.Infrastructure.Tests/TrainingCenterImportServiceParseTests.cs`
  - Parser-Gate gegen Muellcodes.
- Create: `tests/AuswertungPro.Next.Infrastructure.Tests/TrainingCenterImportServicePairingTests.cs`
  - Pairing-Gate mit Temp-Dateien.
- Modify: `tests/AuswertungPro.Next.Pipeline.Tests/SelfTrainingAutoAcceptPolicyTests.cs`
  - Auto-Gold-Gates.
- Create: `tests/AuswertungPro.Next.Pipeline.Tests/SelfTrainingFramePositionPolicyTests.cs`
  - Frame-Zuverlaessigkeit.

---

### Task 1: Gate C-Teil1 Kommentar-Fix

**Files:**
- Modify: `src/AuswertungPro.Next.Domain/VsaCatalog/VsaCodeTree.cs`
- Modify: `src/AuswertungPro.Next.Infrastructure/Ai/VsaCodeResolver.cs`

- [ ] **Step 1: Wahrheitsquelle sichtbar pruefen**

Run:

```powershell
$p='src/AuswertungPro.Next.Domain/VsaCatalog/VsaCodeTree.cs'
$c=Get-Content -LiteralPath $p
for($i=122;$i -le 144;$i++){('{0}:{1}' -f $i,$c[$i-1])}
```

Expected: `BBA` labelt Wurzeln, `BBB` Anhaftende Stoffe, `BBC` Ablagerungen Sohle, `BBD` Eindringen Boden.

- [ ] **Step 2: Kommentartexte in `VsaCodeTree.cs` korrigieren**

In `VsaCodeTree.cs` im Bereich `StreckenschadenCodes` die Docstring-Zeilen und Inline-Kommentare so ersetzen:

```csharp
/// Prueft ob ein VSA-Code typischerweise ein Streckenschaden ist (requiresRange laut Katalog).
/// Typisch fuer: Risse laengs (BABA/BABAB), Korrosion (BAFA), Wurzeln (BBA),
/// Anhaftende Stoffe (BBB), Ablagerungen Sohle (BBC), eindringender Boden (BBD) etc.
```

Und die BB-Kommentare im HashSet:

```csharp
"BBA",    // Wurzeln
"BBAA",   // Wurzeln - Pfahlwurzel
"BBAB",   // Wurzeln - feiner Einwuchs
"BBB",    // Anhaftende Stoffe
"BBBA",   // Anhaftende Stoffe - Inkrustation
"BBC",    // Ablagerungen Sohle
"BBCA",   // Ablagerungen Sohle - Sand
"BBCB",   // Ablagerungen Sohle - Kies
"BBCC",   // Ablagerungen Sohle - Hart
"BBD",    // Eindringen Boden
"BBDA",   // Eindringen Boden - Sand
"BBDB",   // Eindringen Boden - Humus
```

- [ ] **Step 3: Kommentar in aktivem Resolver-Set setzen**

In `src/AuswertungPro.Next.Infrastructure/Ai/VsaCodeResolver.cs` direkt vor `private static readonly HashSet<string> StreckenschadenCodes` diesen Kommentar einfuegen:

```csharp
// Bedeutungen der BB-Codes kommen aus VsaCodeTree.Groups["BB"]; dieses Set ist nur
// eine Streckenschaden-Heuristik und darf keine umetikettierten Fachbedeutungen tragen.
```

- [ ] **Step 4: Build pruefen**

Run:

```powershell
dotnet build src/AuswertungPro.Next.Domain/AuswertungPro.Next.Domain.csproj -v minimal
dotnet build src/AuswertungPro.Next.Infrastructure/AuswertungPro.Next.Infrastructure.csproj -v minimal
```

Expected: beide Builds erfolgreich.

- [ ] **Step 5: Commit**

```powershell
git add src/AuswertungPro.Next.Domain/VsaCatalog/VsaCodeTree.cs src/AuswertungPro.Next.Infrastructure/Ai/VsaCodeResolver.cs
git commit -m "docs(ai): correct VSA BB code comments"
```

---

### Task 2: Gate C-Teil2 VsaCodeValidator

**Files:**
- Create: `src/AuswertungPro.Next.Domain/VsaCatalog/VsaCodeValidator.cs`
- Create: `tests/AuswertungPro.Next.Pipeline.Tests/VsaCodeValidatorTests.cs`

- [ ] **Step 1: Failing Validator-Tests schreiben**

Create `tests/AuswertungPro.Next.Pipeline.Tests/VsaCodeValidatorTests.cs`:

```csharp
using AuswertungPro.Next.Domain.VsaCatalog;

namespace AuswertungPro.Next.Pipeline.Tests;

public sealed class VsaCodeValidatorTests
{
    [Theory]
    [InlineData("BAB")]
    [InlineData("BABBB")]
    [InlineData("BBAA")]
    [InlineData("bca.eb")]
    public void IsKnownCode_accepts_known_main_code_and_subcodes(string code)
    {
        Assert.True(VsaCodeValidator.IsKnownCode(code));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("BA")]
    [InlineData("BB")]
    [InlineData("ABC")]
    [InlineData("XY")]
    [InlineData("BBZ")]
    [InlineData("BA-")]
    [InlineData("B A B")]
    public void IsKnownCode_rejects_groups_unknown_codes_and_noise(string code)
    {
        Assert.False(VsaCodeValidator.IsKnownCode(code));
    }
}
```

- [ ] **Step 2: Test rot laufen lassen**

Run:

```powershell
dotnet test tests/AuswertungPro.Next.Pipeline.Tests/AuswertungPro.Next.Pipeline.Tests.csproj --filter VsaCodeValidatorTests -v minimal
```

Expected: FAIL, weil `VsaCodeValidator` noch fehlt.

- [ ] **Step 3: Minimalen Validator implementieren**

Create `src/AuswertungPro.Next.Domain/VsaCatalog/VsaCodeValidator.cs`:

```csharp
using System.Text.RegularExpressions;

namespace AuswertungPro.Next.Domain.VsaCatalog;

/// <summary>
/// Strenger Eintrittsfilter fuer Trainingslabels aus freiem PDF-Text.
/// UI-/KI-Resolver duerfen fallback-toleranter sein; dieser Validator nicht.
/// </summary>
public static class VsaCodeValidator
{
    private static readonly Regex CodePattern = new("^[A-Z]{3,8}$", RegexOptions.Compiled);

    public static bool IsKnownCode(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return false;

        var normalized = code.Trim().Replace(".", "").ToUpperInvariant();
        if (!CodePattern.IsMatch(normalized))
            return false;

        var groupKey = normalized[..2];
        if (!VsaCodeTree.Groups.TryGetValue(groupKey, out var group))
            return false;

        var mainKey = normalized[..3];
        return group.Codes.ContainsKey(mainKey);
    }
}
```

- [ ] **Step 4: Validator-Test gruen laufen lassen**

Run:

```powershell
dotnet test tests/AuswertungPro.Next.Pipeline.Tests/AuswertungPro.Next.Pipeline.Tests.csproj --filter VsaCodeValidatorTests -v minimal
```

Expected: PASS.

- [ ] **Step 5: Commit**

```powershell
git add src/AuswertungPro.Next.Domain/VsaCatalog/VsaCodeValidator.cs tests/AuswertungPro.Next.Pipeline.Tests/VsaCodeValidatorTests.cs
git commit -m "feat(domain): add VSA code validator"
```

---

### Task 3: Gate C-Teil2 Parser-Eintritt schuetzen

**Files:**
- Modify: `src/AuswertungPro.Next.Infrastructure/Ai/Training/TrainingCenterImportService.cs`
- Create: `tests/AuswertungPro.Next.Infrastructure.Tests/TrainingCenterImportServiceParseTests.cs`

- [ ] **Step 1: Failing Parse-Test schreiben**

Create `tests/AuswertungPro.Next.Infrastructure.Tests/TrainingCenterImportServiceParseTests.cs`:

```csharp
using AuswertungPro.Next.Infrastructure.Ai.Training;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class TrainingCenterImportServiceParseTests
{
    [Fact]
    public void ExtractEntriesFromChunkText_discards_unknown_codes_from_free_text()
    {
        const string text = """
            12.300 BAB Riss laengs bei 3 Uhr
            13.400 ABC Das ist kein VSA-Code
            14.500 BBAA Wurzeln an der Rohrwand
            15.600 BA Nur Gruppe, kein Befundcode
            """;

        var entries = TrainingCenterImportService.ExtractEntriesFromChunkText(text);

        Assert.Collection(entries,
            first =>
            {
                Assert.Equal("BAB", first.Code);
                Assert.Equal(12.300, first.MeterStart, 3);
            },
            second =>
            {
                Assert.Equal("BBAA", second.Code);
                Assert.Equal(14.500, second.MeterStart, 3);
            });
    }
}
```

- [ ] **Step 2: Test rot laufen lassen**

Run:

```powershell
dotnet test tests/AuswertungPro.Next.Infrastructure.Tests/AuswertungPro.Next.Infrastructure.Tests.csproj --filter TrainingCenterImportServiceParseTests -v minimal
```

Expected: FAIL, weil `ExtractEntriesFromChunkText` privat ist oder Muellcodes noch nicht filtert.

- [ ] **Step 3: Parser-Naht und Validator-Verdrahtung implementieren**

In `TrainingCenterImportService.cs` oben ergaenzen:

```csharp
using AuswertungPro.Next.Domain.VsaCatalog;
```

Die Methode und den verschachtelten Record ersetzen:

```csharp
/// <summary>
/// Extrahiert Beobachtungen aus dem Chunk-Text (Fretz-Format + Standard).
/// Unbekannte Codes werden am Parse-Eintritt verworfen, damit PDF-Freitext
/// nicht als Trainingslabel in den Batch gelangt.
/// </summary>
internal static List<ProtocolEntry> ExtractEntriesFromChunkText(string text)
{
    var entries = new List<ProtocolEntry>();
    if (string.IsNullOrWhiteSpace(text))
        return entries;

    // Fretz-Format: "[Foto?] [HH:MM:SS] [Meter] [Code] [Beschreibung]"
    var fretzRx = new Regex(
        @"^\s*(?:\d{1,5}\s+)?(?:\d{2}:\d{2}:\d{2}\s+)?(?<meter>\d{1,4}[.,]\d{1,3})\s+(?<code>[A-Z]{2,6}(?:\.[A-Z]{1,2})*)\s+(?<text>.+?)(?:\s{2,}|$)",
        RegexOptions.Multiline);

    foreach (Match m in fretzRx.Matches(text))
    {
        var code = m.Groups["code"].Value.Trim();
        if (!VsaCodeValidator.IsKnownCode(code))
            continue;

        var desc = m.Groups["text"].Value.Trim();
        if (double.TryParse(m.Groups["meter"].Value.Replace(',', '.'),
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var meter))
        {
            entries.Add(new ProtocolEntry(code.Replace(".", "").ToUpperInvariant(), desc, meter));
        }
    }

    return entries;
}

internal sealed record ProtocolEntry(string Code, string Beschreibung, double MeterStart);
```

- [ ] **Step 4: Parser-Test gruen laufen lassen**

Run:

```powershell
dotnet test tests/AuswertungPro.Next.Infrastructure.Tests/AuswertungPro.Next.Infrastructure.Tests.csproj --filter TrainingCenterImportServiceParseTests -v minimal
```

Expected: PASS.

- [ ] **Step 5: Commit**

```powershell
git add src/AuswertungPro.Next.Infrastructure/Ai/Training/TrainingCenterImportService.cs tests/AuswertungPro.Next.Infrastructure.Tests/TrainingCenterImportServiceParseTests.cs
git commit -m "fix(training): filter unknown protocol codes"
```

---

### Task 4: Gate D Pairing-Helfer

**Files:**
- Modify: `src/AuswertungPro.Next.Infrastructure/Ai/Training/TrainingCenterImportService.cs`
- Create: `tests/AuswertungPro.Next.Infrastructure.Tests/TrainingCenterImportServicePairingTests.cs`

- [ ] **Step 1: Failing Pairing-Tests schreiben**

Create `tests/AuswertungPro.Next.Infrastructure.Tests/TrainingCenterImportServicePairingTests.cs`:

```csharp
using AuswertungPro.Next.Infrastructure.Ai.Training;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class TrainingCenterImportServicePairingTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "ap-pairing-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void ResolvePair_keeps_unambiguous_one_to_one_case()
    {
        var video = WriteFile("video.mp4", 10);
        var proto = WriteFile("bericht.pdf", 10);

        var pair = TrainingCenterImportService.ResolvePair([video], [proto], "24379-41412");

        Assert.Equal(video, pair.VideoPath);
        Assert.Equal(proto, pair.ProtocolPath);
    }

    [Fact]
    public void ResolvePair_prefers_id_matching_video_over_largest_video()
    {
        var matchingVideo = WriteFile("H_06.24379-41412.mp4", 10);
        var largeWrongVideo = WriteFile("H_99999-88888.mp4", 200);
        var proto = WriteFile("protokoll_24379-41412.pdf", 10);

        var pair = TrainingCenterImportService.ResolvePair(
            [matchingVideo, largeWrongVideo], [proto], "24379-41412");

        Assert.Equal(matchingVideo, pair.VideoPath);
        Assert.Equal(proto, pair.ProtocolPath);
    }

    [Fact]
    public void ResolvePair_prefers_id_matching_protocol_over_best_protocol_keyword()
    {
        var video = WriteFile("H_24379-41412.mp4", 10);
        var wrongProtocol = WriteFile("bericht_99999-88888.pdf", 200);
        var matchingProtocol = WriteFile("bericht_24379-41412.pdf", 10);

        var pair = TrainingCenterImportService.ResolvePair(
            [video], [wrongProtocol, matchingProtocol], "24379-41412");

        Assert.Equal(video, pair.VideoPath);
        Assert.Equal(matchingProtocol, pair.ProtocolPath);
    }

    [Fact]
    public void ResolvePair_clears_wrong_protocol_when_video_matches_case()
    {
        var video = WriteFile("H_24379-41412.mp4", 10);
        var wrongProtocol = WriteFile("bericht_99999-88888.pdf", 10);
        var otherWrongProtocol = WriteFile("bericht_88888-77777.pdf", 20);

        var pair = TrainingCenterImportService.ResolvePair(
            [video], [wrongProtocol, otherWrongProtocol], "24379-41412");

        Assert.Equal(video, pair.VideoPath);
        Assert.Equal("", pair.ProtocolPath);
    }

    [Fact]
    public void ResolvePair_uses_normalized_haltung_key_for_area_prefixes()
    {
        var video = WriteFile("H_06.24379-41412.mp4", 10);
        var wrongLargeVideo = WriteFile("H_99999-88888.mp4", 200);
        var proto = WriteFile("protokoll_24379-41412.pdf", 10);

        var pair = TrainingCenterImportService.ResolvePair([video, wrongLargeVideo], [proto], "06.24379-41412");

        Assert.Equal(video, pair.VideoPath);
        Assert.Equal(proto, pair.ProtocolPath);
    }

    [Fact]
    public void ResolveProtocolOnlyPair_keeps_protocol_and_clears_conflicting_video()
    {
        var wrongVideo = WriteFile("H_99999-88888.mp4", 10);
        var proto = WriteFile("protokoll_24379-41412.pdf", 10);

        var pair = TrainingCenterImportService.ResolveProtocolOnlyPair(
            [wrongVideo], [proto], "24379-41412");

        Assert.Equal("", pair.VideoPath);
        Assert.Equal(proto, pair.ProtocolPath);
    }

    private string WriteFile(string name, int bytes)
    {
        Directory.CreateDirectory(_root);
        var path = Path.Combine(_root, name);
        File.WriteAllBytes(path, new byte[bytes]);
        return path;
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }
}
```

- [ ] **Step 2: Tests rot laufen lassen**

Run:

```powershell
dotnet test tests/AuswertungPro.Next.Infrastructure.Tests/AuswertungPro.Next.Infrastructure.Tests.csproj --filter TrainingCenterImportServicePairingTests -v minimal
```

Expected: FAIL, weil `ResolvePair` und `ResolveProtocolOnlyPair` fehlen.

- [ ] **Step 3: Pairing-Helfer implementieren**

In `TrainingCenterImportService.cs` nach `PickBestProtocol` einfuegen:

```csharp
internal static (string VideoPath, string ProtocolPath) ResolvePair(
    IReadOnlyList<string> videos,
    IReadOnlyList<string> protos,
    string caseId)
{
    return ResolvePairCore(videos, protos, caseId, preserveProtocolOnConflict: false);
}

internal static (string VideoPath, string ProtocolPath) ResolveProtocolOnlyPair(
    IReadOnlyList<string> videos,
    IReadOnlyList<string> protos,
    string caseId)
{
    return ResolvePairCore(videos, protos, caseId, preserveProtocolOnConflict: true);
}

private static (string VideoPath, string ProtocolPath) ResolvePairCore(
    IReadOnlyList<string> videos,
    IReadOnlyList<string> protos,
    string caseId,
    bool preserveProtocolOnConflict)
{
    var videoList = videos.ToList();
    var protoList = protos.ToList();

    var bestVideo = videoList.Count > 0 ? PickBestVideo(videoList, caseId) : "";
    var bestProto = protoList.Count > 0 ? PickBestProtocol(protoList) ?? "" : "";

    if (videoList.Count <= 1 && protoList.Count <= 1)
    {
        return preserveProtocolOnConflict
            ? DropContradiction(bestVideo, bestProto, caseId, preserveProtocolOnConflict: true)
            : (bestVideo, bestProto);
    }

    var caseKey = EvalContaminationGuard.NormalizeHaltungKey(caseId);
    var matchingVideo = PickVideoByHaltungKey(videoList, caseKey, caseId);
    if (!string.IsNullOrWhiteSpace(matchingVideo))
        bestVideo = matchingVideo;

    var matchingProto = PickProtocolByHaltungKey(protoList, caseKey);
    if (!string.IsNullOrWhiteSpace(matchingProto))
        bestProto = matchingProto;

    return DropContradiction(bestVideo, bestProto, caseId, preserveProtocolOnConflict);
}

private static string PickVideoByHaltungKey(List<string> videos, string? caseKey, string caseId)
{
    if (string.IsNullOrWhiteSpace(caseKey))
        return "";

    var matches = videos
        .Where(v => string.Equals(NormalizeFileHaltungKey(v), caseKey, StringComparison.OrdinalIgnoreCase))
        .ToList();

    return matches.Count == 0 ? "" : PickBestVideo(matches, caseId);
}

private static string PickProtocolByHaltungKey(List<string> protos, string? caseKey)
{
    if (string.IsNullOrWhiteSpace(caseKey))
        return "";

    var matches = protos
        .Where(p => string.Equals(NormalizeFileHaltungKey(p), caseKey, StringComparison.OrdinalIgnoreCase))
        .ToList();

    return matches.Count == 0 ? "" : PickBestProtocol(matches) ?? "";
}

private static (string VideoPath, string ProtocolPath) DropContradiction(
    string videoPath,
    string protocolPath,
    string caseId,
    bool preserveProtocolOnConflict)
{
    if (string.IsNullOrWhiteSpace(videoPath) || string.IsNullOrWhiteSpace(protocolPath))
        return (videoPath, protocolPath);

    var videoKey = NormalizeFileHaltungKey(videoPath);
    var protocolKey = NormalizeFileHaltungKey(protocolPath);
    if (videoKey is null || protocolKey is null)
        return (videoPath, protocolPath);

    if (string.Equals(videoKey, protocolKey, StringComparison.OrdinalIgnoreCase))
        return (videoPath, protocolPath);

    if (preserveProtocolOnConflict)
        return ("", protocolPath);

    var caseKey = EvalContaminationGuard.NormalizeHaltungKey(caseId);
    var videoMatchesCase = caseKey is not null
        && string.Equals(videoKey, caseKey, StringComparison.OrdinalIgnoreCase);
    var protocolMatchesCase = caseKey is not null
        && string.Equals(protocolKey, caseKey, StringComparison.OrdinalIgnoreCase);

    if (videoMatchesCase && !protocolMatchesCase)
        return (videoPath, "");
    if (protocolMatchesCase && !videoMatchesCase)
        return ("", protocolPath);

    return (videoPath, "");
}

private static string? NormalizeFileHaltungKey(string path)
{
    return EvalContaminationGuard.NormalizeHaltungKey(Path.GetFileNameWithoutExtension(path));
}
```

- [ ] **Step 4: Pairing-Tests gruen laufen lassen**

Run:

```powershell
dotnet test tests/AuswertungPro.Next.Infrastructure.Tests/AuswertungPro.Next.Infrastructure.Tests.csproj --filter TrainingCenterImportServicePairingTests -v minimal
```

Expected: PASS.

---

### Task 5: Gate D in `ScanAsync` und `ScanProtocolOnlyAsync` verdrahten

**Files:**
- Modify: `src/AuswertungPro.Next.Infrastructure/Ai/Training/TrainingCenterImportService.cs`
- Modify: `tests/AuswertungPro.Next.Infrastructure.Tests/TrainingCenterImportServicePairingTests.cs`

- [ ] **Step 1: Failing Scan-Verdrahtungstests ergaenzen**

Append to `TrainingCenterImportServicePairingTests`:

```csharp
[Fact]
public async Task ScanAsync_uses_id_matching_pair_when_folder_is_ambiguous()
{
    var caseDir = Path.Combine(_root, "24379-41412");
    Directory.CreateDirectory(caseDir);
    var matchingVideo = WriteFile(Path.Combine("24379-41412", "H_24379-41412.mp4"), 10);
    WriteFile(Path.Combine("24379-41412", "H_99999-88888.mp4"), 200);
    var proto = WriteFile(Path.Combine("24379-41412", "bericht_24379-41412.pdf"), 10);

    var cases = await new TrainingCenterImportService().ScanAsync(caseDir);

    var result = Assert.Single(cases);
    Assert.Equal(matchingVideo, result.VideoPath);
    Assert.Equal(proto, result.ProtocolPath);
}

[Fact]
public async Task ScanProtocolOnlyAsync_keeps_protocol_and_drops_conflicting_video()
{
    var caseDir = Path.Combine(_root, "24379-41412");
    Directory.CreateDirectory(caseDir);
    WriteFile(Path.Combine("24379-41412", "H_99999-88888.mp4"), 10);
    var proto = WriteFile(Path.Combine("24379-41412", "bericht_24379-41412.pdf"), 10);

    var cases = await new TrainingCenterImportService().ScanProtocolOnlyAsync(caseDir);

    var result = Assert.Single(cases);
    Assert.Equal("", result.VideoPath);
    Assert.Equal(proto, result.ProtocolPath);
}
```

Replace `WriteFile` in the same test file so nested relative names work:

```csharp
private string WriteFile(string name, int bytes)
{
    var path = Path.Combine(_root, name);
    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
    File.WriteAllBytes(path, new byte[bytes]);
    return path;
}
```

- [ ] **Step 2: Tests rot laufen lassen**

Run:

```powershell
dotnet test tests/AuswertungPro.Next.Infrastructure.Tests/AuswertungPro.Next.Infrastructure.Tests.csproj --filter TrainingCenterImportServicePairingTests -v minimal
```

Expected: FAIL, weil `ScanAsync`/`ScanProtocolOnlyAsync` noch die alten Picker direkt verwenden.

- [ ] **Step 3: Scan-Methoden verdrahten**

In `ScanAsync` diese Zeilen:

```csharp
var bestVideo = videos.Count > 0 ? PickBestVideo(videos, caseId) : "";
var bestProto = protos.Count > 0 ? PickBestProtocol(protos) ?? "" : "";
```

ersetzen durch:

```csharp
var (bestVideo, bestProto) = ResolvePair(videos, protos, caseId);
```

In `ScanProtocolOnlyAsync` diese Zeilen:

```csharp
var bestVideo = videos.Count > 0 ? PickBestVideo(videos, caseId) : "";

var proto = PickBestProtocol(protos);
var inspectionDate = ResolveInspectionDate(folder, proto ?? string.Empty, bestVideo);
if (proto is null) continue; // Nur Non-Protocol-Dateien -> ueberspringen
```

ersetzen durch:

```csharp
var (bestVideo, proto) = ResolveProtocolOnlyPair(videos, protos, caseId);
var inspectionDate = ResolveInspectionDate(folder, proto, bestVideo);
if (string.IsNullOrWhiteSpace(proto)) continue; // Nur Non-Protocol-Dateien -> ueberspringen
```

- [ ] **Step 4: Gate-D-Tests gruen laufen lassen**

Run:

```powershell
dotnet test tests/AuswertungPro.Next.Infrastructure.Tests/AuswertungPro.Next.Infrastructure.Tests.csproj --filter "TrainingCenterImportServicePairingTests|TrainingCenterImportServiceParseTests" -v minimal
```

Expected: PASS.

- [ ] **Step 5: Commit Gate D**

```powershell
git add src/AuswertungPro.Next.Infrastructure/Ai/Training/TrainingCenterImportService.cs tests/AuswertungPro.Next.Infrastructure.Tests/TrainingCenterImportServicePairingTests.cs
git commit -m "feat(training): resolve protocol video pairs by Haltung id"
```

---

### Task 6: Gate A Auto-Gold-Policy haerten

**Files:**
- Modify: `src/AuswertungPro.Next.Application/Ai/Training/SelfTrainingAutoAcceptPolicy.cs`
- Modify: `tests/AuswertungPro.Next.Pipeline.Tests/SelfTrainingAutoAcceptPolicyTests.cs`

- [ ] **Step 1: Failing Policy-Tests ergaenzen**

Append to `SelfTrainingAutoAcceptPolicyTests`:

```csharp
[Fact]
public void RequireKbAgreement_CleanExactWithAgreement_Approves()
{
    var d = SelfTrainingAutoAcceptPolicy.Decide(
        MatchLevel.ExactMatch,
        requireHumanReview: false,
        KbCheckResult.KbAgreement,
        requireKbAgreement: true,
        confidenceScore: 1.0,
        confidenceThreshold: 1.0,
        framePositionReliable: true);

    Assert.Equal(TrainingSampleStatus.Approved, d.Status);
    Assert.Equal(KbIndexState.Pending, d.KbIndexState);
    Assert.False(d.RouteToReview);
    Assert.Null(d.Reason);
}

[Fact]
public void RequireKbAgreement_CleanExactWithoutKbSignal_RoutesToReview()
{
    var d = SelfTrainingAutoAcceptPolicy.Decide(
        MatchLevel.ExactMatch,
        requireHumanReview: false,
        KbCheckResult.KbNoSignal,
        requireKbAgreement: true,
        confidenceScore: 1.0,
        confidenceThreshold: 1.0,
        framePositionReliable: true);

    Assert.Equal(TrainingSampleStatus.New, d.Status);
    Assert.Equal(KbIndexState.None, d.KbIndexState);
    Assert.True(d.RouteToReview);
    Assert.Equal(SelfTrainingAutoAcceptPolicy.KbAgreementRequiredReason, d.Reason);
}

[Fact]
public void ConfidenceBelowThreshold_RoutesToReview()
{
    var d = SelfTrainingAutoAcceptPolicy.Decide(
        MatchLevel.ExactMatch,
        requireHumanReview: false,
        KbCheckResult.KbAgreement,
        requireKbAgreement: true,
        confidenceScore: 0.99,
        confidenceThreshold: 1.0,
        framePositionReliable: true);

    Assert.Equal(TrainingSampleStatus.New, d.Status);
    Assert.Equal(KbIndexState.None, d.KbIndexState);
    Assert.True(d.RouteToReview);
    Assert.Equal(SelfTrainingAutoAcceptPolicy.ConfidenceInsufficientReason, d.Reason);
}

[Fact]
public void UnreliableFramePosition_RoutesToReview()
{
    var d = SelfTrainingAutoAcceptPolicy.Decide(
        MatchLevel.ExactMatch,
        requireHumanReview: false,
        KbCheckResult.KbAgreement,
        requireKbAgreement: true,
        confidenceScore: 1.0,
        confidenceThreshold: 1.0,
        framePositionReliable: false);

    Assert.Equal(TrainingSampleStatus.New, d.Status);
    Assert.Equal(KbIndexState.None, d.KbIndexState);
    Assert.True(d.RouteToReview);
    Assert.Equal(SelfTrainingAutoAcceptPolicy.FramePositionUnverifiedReason, d.Reason);
}

[Fact]
public void BackwardsCompatibleDefaults_StillApproveCleanExactWhenCalledOldWay()
{
    var d = SelfTrainingAutoAcceptPolicy.Decide(MatchLevel.ExactMatch, requireHumanReview: false);

    Assert.Equal(TrainingSampleStatus.Approved, d.Status);
    Assert.Equal(KbIndexState.Pending, d.KbIndexState);
    Assert.False(d.RouteToReview);
}
```

- [ ] **Step 2: Tests rot laufen lassen**

Run:

```powershell
dotnet test tests/AuswertungPro.Next.Pipeline.Tests/AuswertungPro.Next.Pipeline.Tests.csproj --filter SelfTrainingAutoAcceptPolicyTests -v minimal
```

Expected: FAIL, weil neue Parameter und Konstanten fehlen.

- [ ] **Step 3: Policy implementieren**

Replace `SelfTrainingAutoAcceptPolicy` body with:

```csharp
public static class SelfTrainingAutoAcceptPolicy
{
    public const string HumanReviewRequiredReason = "HumanReviewRequired";
    public const string KbDisagreementReason = "KbDisagreement";
    public const string KbAgreementRequiredReason = "KbAgreementRequired";
    public const string ConfidenceInsufficientReason = "ConfidenceInsufficient";
    public const string FramePositionUnverifiedReason = "FramePositionUnverified";

    public readonly record struct Decision(
        TrainingSampleStatus Status,
        KbIndexState KbIndexState,
        bool RouteToReview,
        string? Reason);

    public static Decision Decide(
        MatchLevel level,
        bool requireHumanReview,
        KbCheckResult kbCheck = KbCheckResult.KbNoSignal,
        bool requireKbAgreement = false,
        double confidenceScore = 1.0,
        double confidenceThreshold = 1.0,
        bool framePositionReliable = true)
    {
        if (kbCheck == KbCheckResult.KbDisagreement)
            return Review(KbDisagreementReason);

        bool cleanExact = level == MatchLevel.ExactMatch;
        if (!cleanExact)
            return Review(null);

        if (requireHumanReview)
            return Review(HumanReviewRequiredReason);

        if (confidenceScore < confidenceThreshold)
            return Review(ConfidenceInsufficientReason);

        if (requireKbAgreement && kbCheck != KbCheckResult.KbAgreement)
            return Review(KbAgreementRequiredReason);

        if (!framePositionReliable)
            return Review(FramePositionUnverifiedReason);

        return new Decision(
            TrainingSampleStatus.Approved,
            KbIndexState.Pending,
            RouteToReview: false,
            Reason: null);
    }

    private static Decision Review(string? reason)
        => new(
            TrainingSampleStatus.New,
            KbIndexState.None,
            RouteToReview: true,
            Reason: reason);
}
```

- [ ] **Step 4: Policy-Tests gruen laufen lassen**

Run:

```powershell
dotnet test tests/AuswertungPro.Next.Pipeline.Tests/AuswertungPro.Next.Pipeline.Tests.csproj --filter SelfTrainingAutoAcceptPolicyTests -v minimal
```

Expected: PASS.

---

### Task 7: Gate B Settings, Frame-Policy und Orchestrator-Verdrahtung

**Files:**
- Modify: `src/AuswertungPro.Next.Application/Ai/Training/TrainingCenterSettings.cs`
- Create: `src/AuswertungPro.Next.Application/Ai/Training/SelfTrainingFramePositionPolicy.cs`
- Modify: `src/AuswertungPro.Next.Infrastructure/Ai/Training/SelfTrainingOrchestrator.cs`
- Create: `tests/AuswertungPro.Next.Pipeline.Tests/SelfTrainingFramePositionPolicyTests.cs`

- [ ] **Step 1: Failing Frame-Policy-Test schreiben**

Create `tests/AuswertungPro.Next.Pipeline.Tests/SelfTrainingFramePositionPolicyTests.cs`:

```csharp
using AuswertungPro.Next.Application.Ai.Training;

namespace AuswertungPro.Next.Pipeline.Tests;

public sealed class SelfTrainingFramePositionPolicyTests
{
    [Theory]
    [InlineData(false, false, true)] // PdfPhoto
    [InlineData(false, true, true)]  // PdfPhoto mit Zeit bleibt sicher
    [InlineData(true, true, true)]   // VideoTimestamp
    [InlineData(true, false, false)] // VideoLinear
    public void IsReliable_matches_self_training_source_type_semantics(
        bool usedVideoFallback,
        bool hasProtocolTimestamp,
        bool expected)
    {
        Assert.Equal(expected,
            SelfTrainingFramePositionPolicy.IsReliable(usedVideoFallback, hasProtocolTimestamp));
    }
}
```

- [ ] **Step 2: Frame-Policy-Test rot laufen lassen**

Run:

```powershell
dotnet test tests/AuswertungPro.Next.Pipeline.Tests/AuswertungPro.Next.Pipeline.Tests.csproj --filter SelfTrainingFramePositionPolicyTests -v minimal
```

Expected: FAIL, weil `SelfTrainingFramePositionPolicy` fehlt.

- [ ] **Step 3: Frame-Policy implementieren**

Create `src/AuswertungPro.Next.Application/Ai/Training/SelfTrainingFramePositionPolicy.cs`:

```csharp
namespace AuswertungPro.Next.Application.Ai.Training;

/// <summary>
/// Bewertet, ob ein Self-Training-Frame verlaesslich an die Protokollposition gebunden ist.
/// PdfPhoto und VideoTimestamp sind stabil; VideoLinear ist nur geschaetzt.
/// </summary>
public static class SelfTrainingFramePositionPolicy
{
    public static bool IsReliable(bool usedVideoFallback, bool hasProtocolTimestamp)
        => !usedVideoFallback || hasProtocolTimestamp;
}
```

- [ ] **Step 4: Settings ergaenzen**

Append to `TrainingCenterSettings` after `RequireHumanReview`:

```csharp
/// <summary>
/// Wenn true, darf Auto-Gold nur entstehen, wenn die KB den KI-Code aktiv bestaetigt.
/// KbNoSignal bleibt Review. Default true = streng fuer unbeaufsichtigte Batch-Laeufe.
/// </summary>
public bool RequireKbAgreementForAutoGold { get; set; } = true;

/// <summary>
/// Mindestscore fuer Auto-Gold. Heute erreicht nur ExactMatch 1.0; der Wert bleibt als
/// Reserve fuer spaetere Lockerungen explizit konfigurierbar.
/// </summary>
public double AutoAcceptConfidenceThreshold { get; set; } = 1.0;

/// <summary>
/// Wenn true, werden per linearer Meter-zu-Zeit-Schaetzung erzeugte VideoFrames nie Auto-Gold.
/// Sie bleiben Review-Kandidaten, weil ihre Position nicht belastbar genug ist.
/// </summary>
public bool RequireReliableFramePositionForAutoGold { get; set; } = true;
```

- [ ] **Step 5: Orchestrator an neue Policy anschliessen**

In `SelfTrainingOrchestrator.cs` vor dem `Decide`-Aufruf einfuegen:

```csharp
var framePositionReliable = !_settings.RequireReliableFramePositionForAutoGold
    || SelfTrainingFramePositionPolicy.IsReliable(usedVideoFallback, entry.Zeit.HasValue);
```

Den alten `Decide`-Aufruf:

```csharp
var decision = SelfTrainingAutoAcceptPolicy.Decide(comparison.Level, _settings.RequireHumanReview, kbCheck);
```

ersetzen durch:

```csharp
var decision = SelfTrainingAutoAcceptPolicy.Decide(
    comparison.Level,
    _settings.RequireHumanReview,
    kbCheck,
    _settings.RequireKbAgreementForAutoGold,
    comparison.ConfidenceScore,
    _settings.AutoAcceptConfidenceThreshold,
    framePositionReliable);
```

- [ ] **Step 6: A+B Tests gruen laufen lassen**

Run:

```powershell
dotnet test tests/AuswertungPro.Next.Pipeline.Tests/AuswertungPro.Next.Pipeline.Tests.csproj --filter "SelfTrainingAutoAcceptPolicyTests|SelfTrainingFramePositionPolicyTests" -v minimal
dotnet build src/AuswertungPro.Next.Infrastructure/AuswertungPro.Next.Infrastructure.csproj -v minimal
```

Expected: PASS und Build erfolgreich.

- [ ] **Step 7: Commit Gate A+B**

```powershell
git add src/AuswertungPro.Next.Application/Ai/Training/TrainingCenterSettings.cs src/AuswertungPro.Next.Application/Ai/Training/SelfTrainingAutoAcceptPolicy.cs src/AuswertungPro.Next.Application/Ai/Training/SelfTrainingFramePositionPolicy.cs src/AuswertungPro.Next.Infrastructure/Ai/Training/SelfTrainingOrchestrator.cs tests/AuswertungPro.Next.Pipeline.Tests/SelfTrainingAutoAcceptPolicyTests.cs tests/AuswertungPro.Next.Pipeline.Tests/SelfTrainingFramePositionPolicyTests.cs
git commit -m "feat(training): harden auto gold gates"
```

---

### Task 8: Abschlussverifikation

**Files:**
- Keine Quelldateien werden in dieser Task geaendert.

- [ ] **Step 1: Alle gezielten Tests laufen lassen**

Run:

```powershell
dotnet test tests/AuswertungPro.Next.Pipeline.Tests/AuswertungPro.Next.Pipeline.Tests.csproj --filter "VsaCodeValidatorTests|SelfTrainingAutoAcceptPolicyTests|SelfTrainingFramePositionPolicyTests" -v minimal
dotnet test tests/AuswertungPro.Next.Infrastructure.Tests/AuswertungPro.Next.Infrastructure.Tests.csproj --filter "TrainingCenterImportServiceParseTests|TrainingCenterImportServicePairingTests" -v minimal
```

Expected: PASS.

- [ ] **Step 2: Projektweite Tests laufen lassen**

Run:

```powershell
dotnet test AuswertungPro.sln -v minimal
```

Expected: PASS.

- [ ] **Step 3: SelfTrainingHarness-Garantie laufen lassen**

Run:

```powershell
dotnet run --project tools/SelfTrainingHarness/SelfTrainingHarness.csproj -- D:\Haltungen 3 http://localhost:11434 qwen3-vl:8b-q8
```

Expected: Ausgabe enthaelt `Status=Approved (Auto-Gold):     0` und `KbIndexState=Indexed (Auto-KB):  0`. Wenn lokal keine passenden Haltungen oder kein Ollama laufen, den Grund im Abschlussbericht nennen und nicht als bestanden behaupten.

- [ ] **Step 4: Arbeitsbaum pruefen**

Run:

```powershell
git status --short
git log -8 --oneline
```

Expected: nur bekannte untracked Dateien ausserhalb dieser Phase; Commits fuer C1, C2, D und A+B sind sichtbar.

---

## Self-Review

**Spec coverage:**
- C-Teil1: Task 1 korrigiert nur Kommentare und setzt den Resolver-Hinweis.
- C-Teil2: Tasks 2 und 3 bauen den Validator und verdrahten ihn am Parse-Eintritt, nicht in `IsIndexWorthy`.
- D: Tasks 4 und 5 bauen und verdrahten ID-basiertes Pairing, inklusive Protocol-only-Schutz.
- A+B: Tasks 6 und 7 haerten Auto-Gold mit KB-Agreement, Confidence und Frame-Zuverlaessigkeit.
- Verification: Task 8 deckt gezielte Tests, Gesamttest und Harness ab.

**Placeholder scan:** Der Plan enthaelt konkrete Dateien, Tests, Implementierungscode, Befehle und erwartete Ergebnisse.

**Type consistency:** `VsaCodeValidator`, `ResolvePair`, `ResolveProtocolOnlyPair`, `SelfTrainingFramePositionPolicy`, neue Settings und neue `Decide`-Parameter werden vor ihrer Verwendung definiert.
