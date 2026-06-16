using System;
using System.IO;
using System.Reflection;
using AuswertungPro.Next.Infrastructure;
using AuswertungPro.Next.Infrastructure.Map;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests;

/// <summary>
/// Tests fuer das Sicherheitsnetz "keine_Zuordnung": Dichtheits-Haltungen, deren Schacht-Paar
/// NICHT im amtlichen Abwasserkataster steht, werden in den Sammelordner umgelenkt.
/// Deckt die Kataster-Existenzpruefung (HaltungCadastreIndex.PairExists) und die
/// Umlenk-Entscheidung (HoldingFolderDistributor.ResolveDistributionRoot) ab.
/// </summary>
public sealed class HaltungCadastreUnmatchedRoutingTests
{
    // ── Mini-Kataster aus echten Uri-Beispielen (siehe KIT-Pruefberichte) ──
    private static HaltungCadastreIndex BuildTestCadastre()
    {
        var path = Path.Combine(Path.GetTempPath(), $"kataster_test_{Guid.NewGuid():N}.tsv");
        File.WriteAllText(path,
            "# source=test\tbytes=0\tmtimeUtc=2026-01-01T00:00:00.0000000Z\n" +
            HaltungCadastreExtractor.TableHeader + "\n" +
            "865-864\t865\t864\t70.51\t250\tPE\n" +
            "6926-6925\t6926\t6925\t67.51\t315\tPE\n" +
            // Schacht 07.993164 traegt im Kataster einen Praefix:
            "07.993164-993162\t07.993164\t993162\t15.58\t160\tPE\n");
        try { return HaltungCadastreIndex.Load(path); }
        finally { try { File.Delete(path); } catch { /* best effort */ } }
    }

    // ─────────────────────────── PairExists ───────────────────────────

    [Fact]
    public void PairExists_KnownPair_True()
    {
        var cad = BuildTestCadastre();
        Assert.True(cad.PairExists("865", "864"));
    }

    [Fact]
    public void PairExists_ReversedOrder_True()
    {
        // PDF schreibt "864 -> 865", Kataster fuehrt "865-864". Reihenfolge egal.
        var cad = BuildTestCadastre();
        Assert.True(cad.PairExists("864", "865"));
    }

    [Fact]
    public void PairExists_UnknownPair_False()
    {
        // 6927->6926 ist im echten Uri-Netz nicht benachbart.
        var cad = BuildTestCadastre();
        Assert.False(cad.PairExists("6927", "6926"));
    }

    [Fact]
    public void PairExists_PrefixMismatch_FalseBecauseExactCompare()
    {
        // Bewusste Entscheidung: exakter Vergleich. PDF "993164" matcht NICHT "07.993164".
        var cad = BuildTestCadastre();
        Assert.False(cad.PairExists("993164", "993162"));
    }

    [Theory]
    [InlineData("865", "")]
    [InlineData("", "864")]
    [InlineData(null, "864")]
    public void PairExists_EmptyInput_False(string? a, string? b)
    {
        var cad = BuildTestCadastre();
        Assert.False(cad.PairExists(a!, b!));
    }

    // ──────────────────── ResolveDistributionRoot ────────────────────

    private static string InvokeResolveRoot(string dest, string? haltungId, IHaltungCadastreResolver? cadastre)
    {
        var m = typeof(HoldingFolderDistributor).GetMethod(
            "ResolveDistributionRoot",
            BindingFlags.NonPublic | BindingFlags.Static)!;
        return (string)m.Invoke(null, new object?[] { dest, haltungId, cadastre })!;
    }

    private const string Dest = @"C:\Ziel\Gemeinde";
    private static readonly string Unmatched = Path.Combine(Dest, "keine_Zuordnung");

    [Fact]
    public void ResolveRoot_KnownHaltung_StaysInNormalRoot()
    {
        var root = InvokeResolveRoot(Dest, "865-864", BuildTestCadastre());
        Assert.Equal(Dest, root);
    }

    [Fact]
    public void ResolveRoot_UnknownHaltung_GoesToUnmatchedFolder()
    {
        var root = InvokeResolveRoot(Dest, "6927-6926", BuildTestCadastre());
        Assert.Equal(Unmatched, root);
    }

    [Fact]
    public void ResolveRoot_PrefixMismatchHaltung_GoesToUnmatchedFolder()
    {
        // 993164-993162: Paar existiert nur als 07.993164-993162 -> exakt kein Treffer -> keine_Zuordnung.
        var root = InvokeResolveRoot(Dest, "993164-993162", BuildTestCadastre());
        Assert.Equal(Unmatched, root);
    }

    [Fact]
    public void ResolveRoot_NoCadastre_StaysInNormalRoot()
    {
        // Ohne Kataster: exakt wie bisher, nie umlenken (rein additiv).
        var root = InvokeResolveRoot(Dest, "6927-6926", null);
        Assert.Equal(Dest, root);
    }

    [Fact]
    public void ResolveRoot_ShaftInspection_NeverRedirected()
    {
        // Einzelschacht-Pruefungen ("Schacht_...") bleiben im eigenen Ordner.
        var root = InvokeResolveRoot(Dest, "Schacht_993170", BuildTestCadastre());
        Assert.Equal(Dest, root);
    }

    [Fact]
    public void ResolveRoot_SingleIdWithoutPair_StaysInNormalRoot()
    {
        // Keine ableitbaren zwei Schaechte -> konservativ NICHT umlenken (kein Fehl-Umlenken).
        var root = InvokeResolveRoot(Dest, "123456", BuildTestCadastre());
        Assert.Equal(Dest, root);
    }

    [Fact]
    public void ResolveRoot_EmptyHaltung_StaysInNormalRoot()
    {
        var root = InvokeResolveRoot(Dest, "", BuildTestCadastre());
        Assert.Equal(Dest, root);
    }
}
