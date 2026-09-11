using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Import;

namespace AuswertungPro.Next.Infrastructure.Tests.Import;

/// <summary>
/// Abnahme am echten Kundenbestand (Buerglen, GKS Cahenzli): zehn eingescannte
/// Dichtheitspruefungen und neun Aushaerteprotokolle ohne jede Textebene. Der Test
/// laeuft nur, wenn diese Quelle auf dem Rechner liegt, und liest sie ausschliesslich.
/// </summary>
public sealed class SanierungsprotokollEchteQuelleTests : IDisposable
{
    private const string Quelle =
        @"D:\Videoprojekte\Bürglen\2.26.208 Buerglen UR Sanierungsmassnahmen\Projects\"
        + @"2.26.208 Buerglen UR Sanierungsmassnahmen\Misc\Docu";

    private readonly string _root = Path.Combine(
        Path.GetTempPath(), $"sanprot-echt-{Guid.NewGuid():N}");

    /// <summary>Die 19 Begleitprotokolle und ihre Haltungen laut Importbericht.</summary>
    private static readonly (string Haltung, string Bezeichnung)[] Bestand =
    [
        ("59435-60284", "H12"), ("60284-60191", "H13"), ("60191-60188", "H14"),
        ("60188-60105", "H15"), ("60105-59610", "H16"), ("59610-60106", "H17"),
        ("59314-59295", "H41"), ("59295-60262", "H42"), ("60248-60247", "H66"),
        ("525145-505377", "H73"), ("505377-80480", "H74"), ("80480-80478", "H75"),
        ("80478-80475", "H76"), ("80475-80462", "H77"), ("80462-80461", "H78"),
        ("80461-80454", "H79")
    ];

    [Fact]
    public void EingescannteBegleitprotokolle_LandenBeiIhrenHaltungen()
    {
        if (!Directory.Exists(Quelle))
            return;   // Quelle nicht auf diesem Rechner — der Test ist maschinengebunden.

        var projektOrdner = Path.Combine(_root, "Projekt");
        Directory.CreateDirectory(projektOrdner);

        var projekt = new Project();
        foreach (var (haltung, bezeichnung) in Bestand)
        {
            var record = new HaltungRecord { ImportBezeichnung = bezeichnung };
            record.SetFieldValue(FieldKeys.HoldingName, haltung, FieldSource.Legacy, userEdited: false);
            projekt.Data.Add(record);
        }

        var ergebnis = new DichtheitImportDistributionService()
            .Distribute(projekt, projektOrdner, Quelle);

        var abgelegt = Directory
            .GetFiles(Path.Combine(projektOrdner, "Haltungen_Verteilt"), "*.pdf", SearchOption.AllDirectories)
            .Select(p => Path.GetRelativePath(projektOrdner, p))
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
            .ToList();

        // Gemessen 2026-09-09: 30 Ablagen aus 19 Dokumenten. Die zehn Dichtheits-
        // pruefungen ergeben 16 Ablagen (fuenf decken mehrere Haltungen ab), acht der
        // neun Aushaerteprotokolle weitere 14. Das neunte
        // (Aushärtungsprotokoll H73_H74.pdf) bleibt liegen: Das OCR liest sein
        // Haltungsfeld als "HH" — die Ziffern fehlen. Es wird gemeldet, nicht geraten.
        Assert.True(
            ergebnis.Verteilt >= 30,
            "Verteilt: " + ergebnis.Verteilt
            + " | Nicht zugeordnet: " + ergebnis.NichtZugeordnet
            + "\n" + string.Join("\n", abgelegt)
            + "\n---\n" + string.Join("\n", ergebnis.Messages));

        // Jede nicht zugeordnete Datei hat genau eine Meldezeile — und die nennt sie
        // beim Namen. Zusaetzliche Detailgruende duerfen dazukommen.
        var meldungen = ergebnis.Messages
            .Where(m => m.Contains("nicht zugeordnet", StringComparison.OrdinalIgnoreCase)
                        || m.Contains("nicht verteilt", StringComparison.OrdinalIgnoreCase))
            .ToList();
        Assert.Equal(ergebnis.NichtZugeordnet, meldungen.Count);
        Assert.All(meldungen, m => Assert.Contains(".pdf", m, StringComparison.OrdinalIgnoreCase));

        // Kein einziges dieser Protokolle darf bei einem Schacht oder unter einer
        // erfundenen Nummer liegen.
        Assert.All(abgelegt, p =>
            Assert.Contains(
                Bestand.Select(b => b.Haltung),
                h => p.Contains(h, StringComparison.OrdinalIgnoreCase)));

        // Die Sammelpruefung H12_H13 liegt in beiden Haltungen.
        Assert.Contains(abgelegt, p => p.Contains("59435-60284", StringComparison.Ordinal) && p.EndsWith("_DP.pdf", StringComparison.Ordinal));
        Assert.Contains(abgelegt, p => p.Contains("60284-60191", StringComparison.Ordinal) && p.EndsWith("_DP.pdf", StringComparison.Ordinal));

        // Und das Aushaerteprotokoll von H66 liegt bei 60248-60247.
        Assert.Contains(abgelegt, p =>
            p.Contains("60248-60247", StringComparison.Ordinal) && p.EndsWith("_AH.pdf", StringComparison.Ordinal));
    }

    [Fact]
    public void KeinBegleitprotokoll_LaeuftNochInDenSchachtweg()
    {
        if (!Directory.Exists(Quelle))
            return;

        var durchgelassen = Directory
            .GetFiles(Quelle, "*.pdf")
            .Where(p =>
            {
                var name = Path.GetFileName(p);
                return name.StartsWith("DP ", StringComparison.OrdinalIgnoreCase)
                       || name.StartsWith("Aush", StringComparison.OrdinalIgnoreCase);
            })
            .Where(ShaftPdfRelevance.ShouldProcess)
            .Select(Path.GetFileName)
            .ToList();

        Assert.True(durchgelassen.Count == 0, string.Join("\n", durchgelassen));
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); }
        catch { }
    }
}
