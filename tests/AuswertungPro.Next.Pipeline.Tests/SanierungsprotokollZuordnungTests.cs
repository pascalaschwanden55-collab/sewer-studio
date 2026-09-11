using System;
using System.Collections.Generic;
using System.Linq;
using AuswertungPro.Next.Application.UseCases.Import.Quellen;

namespace AuswertungPro.Next.Pipeline.Tests;

/// <summary>
/// Buerglen 2026-09-09: Dichtheitspruefungen und Aushaerteprotokolle nennen ihre Haltung
/// nur als WinCan-Laufnummer („Haltung: H66"), nicht als Schachtpaar 60248-60247. Eine
/// Pruefung deckt dabei oft mehrere Haltungen ab (<c>DP H12_H13.pdf</c>: von Schacht
/// 59435 bis 60191 ueber zwei Haltungen).
/// </summary>
public sealed class SanierungsprotokollZuordnungTests
{
    private static readonly IReadOnlyList<SanierungsprotokollHaltung> Bestand =
    [
        new("59435-60284", "H12"),
        new("60284-60191", "H13"),
        new("60248-60247", "H66"),
        new("80475-80462", "H77")
    ];

    [Fact]
    public void EinzelneHaltung_WirdEindeutigZugeordnet()
    {
        var befund = SanierungsprotokollZuordnung.Ordne(
            "DP H66.pdf", "Druckpruefprotokoll ... Haltung H66 ... SIA 190", Bestand);

        Assert.Equal(["60248-60247"], befund.Ziele.Select(z => z.Haltung));
        Assert.Empty(befund.Hinweise);
    }

    [Fact]
    public void SammelPruefung_LandetInJederBetroffenenHaltung()
    {
        var befund = SanierungsprotokollZuordnung.Ordne(
            "DP H12_H13.pdf", "Von Schacht 59435 Bis Schacht 60191 Haltung H12 H13", Bestand);

        Assert.Equal(["59435-60284", "60284-60191"], befund.Ziele.Select(z => z.Haltung));
        Assert.Empty(befund.Hinweise);
    }

    [Fact]
    public void BezeichnungNurImDateinamen_ZaehltNicht()
    {
        // Der Dateiname allein ist eine Vermutung. Steht die Bezeichnung nicht auch im
        // Dokument, wird nicht verteilt — dieselbe Regel wie bei den Videorollen.
        var befund = SanierungsprotokollZuordnung.Ordne(
            "DP H66.pdf", "Druckpruefprotokoll ohne Haltungsangabe", Bestand);

        Assert.Empty(befund.Ziele);
        Assert.Contains(befund.Hinweise, h => h.Contains("H66", StringComparison.Ordinal));
    }

    [Fact]
    public void UnbekannteBezeichnung_WirdGemeldetUndNichtGeraten()
    {
        var befund = SanierungsprotokollZuordnung.Ordne(
            "DP H99.pdf", "Haltung H99", Bestand);

        Assert.Empty(befund.Ziele);
        Assert.Contains(befund.Hinweise, h => h.Contains("H99", StringComparison.Ordinal));
    }

    [Fact]
    public void MehrdeutigeBezeichnung_BekommtNichts()
    {
        IReadOnlyList<SanierungsprotokollHaltung> doppelt =
        [
            new("100-200", "H5"),
            new("300-400", "H5")
        ];

        var befund = SanierungsprotokollZuordnung.Ordne("DP H5.pdf", "Haltung H5", doppelt);

        Assert.Empty(befund.Ziele);
        Assert.Contains(befund.Hinweise, h => h.Contains("mehrdeutig", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void EineOffeneHaltung_ReisstDieUebrigenNichtMit()
    {
        var befund = SanierungsprotokollZuordnung.Ordne(
            "DP H66_H99.pdf", "Haltung H66 H99", Bestand);

        Assert.Equal(["60248-60247"], befund.Ziele.Select(z => z.Haltung));
        Assert.Contains(befund.Hinweise, h => h.Contains("H99", StringComparison.Ordinal));
    }

    [Fact]
    public void TeilnummerGiltNicht()
    {
        // "H7" darf nicht auf "H77" passen — weder im Dateinamen noch im Text.
        var befund = SanierungsprotokollZuordnung.Ordne("DP H7.pdf", "Haltung H77", Bestand);

        Assert.Empty(befund.Ziele);
    }

    [Fact]
    public void OhneBezeichnungImDateinamen_BleibtDasErgebnisLeer()
    {
        var befund = SanierungsprotokollZuordnung.Ordne(
            "Druckpruefung.pdf", "Haltung H66", Bestand);

        Assert.Empty(befund.Ziele);
        Assert.NotEmpty(befund.Hinweise);
    }

    [Fact]
    public void HaltungOhneWinCanBezeichnung_StoertNicht()
    {
        IReadOnlyList<SanierungsprotokollHaltung> gemischt =
        [
            new("100-200", null),
            new("60248-60247", "H66")
        ];

        var befund = SanierungsprotokollZuordnung.Ordne("DP H66.pdf", "Haltung H66", gemischt);

        Assert.Equal(["60248-60247"], befund.Ziele.Select(z => z.Haltung));
    }

    [Fact]
    public void LeererDokumenttext_VerteiltNichts()
    {
        var befund = SanierungsprotokollZuordnung.Ordne("DP H66.pdf", "", Bestand);

        Assert.Empty(befund.Ziele);
        Assert.NotEmpty(befund.Hinweise);
    }
}
