using System.Text.Json;

namespace AuswertungPro.Next.Pipeline.Tests;

/// <summary>
/// Programmweite Konsistenz-Riegel fuer das VSA-KEK-2020-Katalogmanifest.
/// Hintergrund (Abgleich 2026-07-10 gegen WinCan VX "VSA-2019" und
/// KIAS/IBAK-Export Erstfeld_Jagdmatt): Services, KI-Befundliste und
/// Fremdformat-Importe referenzieren nackte Hauptcodes (z.B. AED, BDC).
/// Diese Tests stellen sicher, dass die Code-Hierarchie im Manifest
/// geschlossen bleibt und Import-Anker nicht wieder verloren gehen.
/// </summary>
public sealed class VsaKekManifestIntegrityTests
{
    // BBD hat bewusst KEINEN Basiscode (nur Untercodes BBDA..BBDZ),
    // siehe VsaKekManifestTruthTests.Manifest_locks_ba_and_bb_code_truth
    // und CLAUDE.md ("kein Basiscode BBD, nur Untercodes").
    private static readonly HashSet<string> FamiliesWithoutBaseCode =
        new(StringComparer.OrdinalIgnoreCase) { "BBD" };

    [Fact]
    public void Manifest_codes_sind_eindeutig()
    {
        var codes = LoadCodes();

        var duplicates = codes
            .GroupBy(c => c.Code, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        Assert.True(duplicates.Count == 0, $"Doppelte Codes im Manifest: {string.Join(", ", duplicates)}");
    }

    [Fact]
    public void Jede_code_familie_hat_einen_gruppeneintrag()
    {
        var codes = LoadCodes();
        var byCode = codes.ToDictionary(c => c.Code, StringComparer.OrdinalIgnoreCase);

        var missing = codes
            .Where(c => c.Code.Length > 3)
            .Select(c => c.Code[..3])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(f => !byCode.ContainsKey(f) && !FamiliesWithoutBaseCode.Contains(f))
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .ToList();

        Assert.True(missing.Count == 0,
            $"Familien ohne Hauptcode-Eintrag (Klartext-Aufloesung bricht): {string.Join(", ", missing)}");
    }

    [Fact]
    public void CanonicalCode_zeigt_immer_auf_existierenden_code()
    {
        var codes = LoadCodes();
        var known = new HashSet<string>(codes.Select(c => c.Code), StringComparer.OrdinalIgnoreCase);

        var broken = codes
            .Where(c => !string.IsNullOrWhiteSpace(c.CanonicalCode) && !known.Contains(c.CanonicalCode!))
            .Select(c => $"{c.Code}->{c.CanonicalCode}")
            .ToList();

        Assert.True(broken.Count == 0, $"canonicalCode zeigt ins Leere: {string.Join(", ", broken)}");
    }

    [Fact]
    public void Selektierbare_codes_haben_deutschen_klartext()
    {
        var codes = LoadCodes();

        var broken = codes
            .Where(c => c.IsSelectable)
            .Where(c => string.IsNullOrWhiteSpace(c.Title)
                || string.Equals(c.Title!.Trim(), c.Code, StringComparison.OrdinalIgnoreCase))
            .Select(c => c.Code)
            .ToList();

        Assert.True(broken.Count == 0,
            $"Selektierbare Codes ohne Klartext-Titel (UI zeigt rohen Code): {string.Join(", ", broken)}");
    }

    // Hauptcodes, die Fremdformat-Importe als nackte Codes liefern:
    //  - IBAK/KIAS (Daten.txt) exportiert NUR Hauptcodes: real belegt im
    //    Export Erstfeld_Jagdmatt u.a. AEC, AED, BDC, BDG (1046 Befunde).
    //  - Services referenzieren zusaetzlich die Schacht-Familien (D-Gruppe).
    [Theory]
    [InlineData("AEC")]
    [InlineData("AED")]
    [InlineData("BBE")]
    [InlineData("BBH")]
    [InlineData("BDC")]
    [InlineData("BDE")]
    [InlineData("BDF")]
    [InlineData("BDG")]
    [InlineData("DAB")]
    [InlineData("DAF")]
    [InlineData("DAK")]
    [InlineData("DCH")]
    [InlineData("DCI")]
    [InlineData("DDC")]
    [InlineData("DDG")]
    public void Import_hauptcodes_loesen_auf_klartext_auf(string code)
    {
        var titles = LoadCodes().ToDictionary(
            c => c.Code,
            c => c.Title ?? "",
            StringComparer.OrdinalIgnoreCase);

        Assert.True(titles.TryGetValue(code, out var title),
            $"Hauptcode {code} fehlt im Manifest - Import/KI-Liste zeigt rohen Code.");
        Assert.False(string.IsNullOrWhiteSpace(title), $"Hauptcode {code} ohne Titel.");
        Assert.False(string.Equals(title, code, StringComparison.OrdinalIgnoreCase),
            $"Hauptcode {code} hat den Code als Titel.");
    }

    // WinCan-Kataloge (VSA-2019) fuehren die EN-13508-2-Z-Codes "andere";
    // die VSA-KEK-2020-ILI-Enum nicht. Als Import-Anker muessen sie im
    // Manifest existieren, duerfen aber NICHT neu erfassbar sein.
    [Theory]
    [InlineData("BDGZ")]
    [InlineData("DDGZ")]
    public void WinCan_z_codes_sind_import_anker_aber_nicht_selektierbar(string code)
    {
        var entry = LoadCodes().SingleOrDefault(c =>
            string.Equals(c.Code, code, StringComparison.OrdinalIgnoreCase));

        Assert.True(entry is not null, $"{code} fehlt im Manifest (WinCan-Import-Anker).");
        Assert.False(entry!.IsSelectable, $"{code} darf nicht selektierbar sein (nicht in ILI-Enum).");
        Assert.False(string.IsNullOrWhiteSpace(entry.Title), $"{code} ohne Klartext-Titel.");
    }

    private sealed record ManifestCode(string Code, string? Title, string? CanonicalCode, bool IsSelectable);

    private static List<ManifestCode> LoadCodes()
    {
        var path = FindManifestPath();
        using var stream = File.OpenRead(path);
        using var doc = JsonDocument.Parse(stream);

        return doc.RootElement
            .GetProperty("codes")
            .EnumerateArray()
            .Where(e => e.TryGetProperty("code", out _))
            .Select(e => new ManifestCode(
                e.GetProperty("code").GetString()!,
                e.TryGetProperty("title", out var t) ? t.GetString() : null,
                e.TryGetProperty("canonicalCode", out var cc) ? cc.GetString() : null,
                e.TryGetProperty("isSelectable", out var s) && s.ValueKind == JsonValueKind.True))
            .ToList();
    }

    private static string FindManifestPath()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var candidate = Path.Combine(
                current.FullName,
                "src",
                "AuswertungPro.Next.UI",
                "Data",
                "vsa_kek_2020_catalog_manifest.json");

            if (File.Exists(candidate))
                return candidate;

            current = current.Parent;
        }

        throw new FileNotFoundException("VSA-KEK-Katalogmanifest wurde nicht gefunden.");
    }
}
