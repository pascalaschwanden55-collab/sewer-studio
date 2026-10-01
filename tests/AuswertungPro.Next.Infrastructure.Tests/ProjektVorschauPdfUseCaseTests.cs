using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Dashboard;
using AuswertungPro.Next.Application.UseCases.Uebersicht;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests;

/// <summary>
/// Nova-Fixwelle F3: Das Vorschau-PDF laeuft fuer die klassische und die neue Uebersicht ueber
/// denselben Ablauf. Ohne Vorschau wird nichts gefragt, ohne Ziel nichts geschrieben.
/// </summary>
public sealed class ProjektVorschauPdfUseCaseTests
{
    private static ProjectPreview Vorschau(string name)
        => ProjectPreviewFactory.FromProject(new Domain.Models.Project { Name = name }, string.Empty);

    private sealed record Lauf(List<string> Gefragt, List<string> Geschrieben);

    private static (ProjektVorschauPdfActions Actions, Lauf Lauf) Baue(
        Func<ProjectPreview?> vorschau,
        Func<string, string?> ziel,
        Func<ProjectPreview, Task<byte[]>>? bytes = null)
    {
        var lauf = new Lauf([], []);
        var actions = new ProjektVorschauPdfActions(
            vorschau,
            name => { lauf.Gefragt.Add(name); return ziel(name); },
            bytes ?? (_ => Task.FromResult(new byte[] { 1, 2, 3 })),
            (pfad, _) => { lauf.Geschrieben.Add(pfad); return Task.CompletedTask; });
        return (actions, lauf);
    }

    [Fact]
    public async Task Ohne_Vorschau_wird_kein_Ziel_gefragt()
    {
        var (actions, lauf) = Baue(() => null, _ => "C:\\egal.pdf");

        var ergebnis = await ProjektVorschauPdfUseCase.AusfuehrenAsync(actions);

        Assert.Equal(ProjektVorschauPdfStatus.KeineVorschau, ergebnis.Status);
        Assert.Empty(lauf.Gefragt);
        Assert.Empty(lauf.Geschrieben);
    }

    [Fact]
    public async Task Ein_abgebrochener_Dateidialog_schreibt_nichts()
    {
        var (actions, lauf) = Baue(() => Vorschau("Projekt A"), _ => null);

        var ergebnis = await ProjektVorschauPdfUseCase.AusfuehrenAsync(actions);

        Assert.Equal(ProjektVorschauPdfStatus.Abgebrochen, ergebnis.Status);
        Assert.Single(lauf.Gefragt);
        Assert.Empty(lauf.Geschrieben);
    }

    [Fact]
    public async Task Erfolgreicher_Lauf_meldet_den_Zielpfad()
    {
        var (actions, lauf) = Baue(() => Vorschau("Projekt A"), _ => "C:\\ziel.pdf");

        var ergebnis = await ProjektVorschauPdfUseCase.AusfuehrenAsync(actions);

        Assert.Equal(ProjektVorschauPdfStatus.Geschrieben, ergebnis.Status);
        Assert.Equal("C:\\ziel.pdf", ergebnis.Pfad);
        Assert.Equal(["C:\\ziel.pdf"], lauf.Geschrieben);
    }

    [Fact]
    public async Task Ein_Fehler_beim_Erzeugen_wird_gemeldet_und_nicht_geworfen()
    {
        var (actions, _) = Baue(
            () => Vorschau("Projekt A"),
            _ => "C:\\ziel.pdf",
            _ => throw new InvalidOperationException("PDF kaputt"));

        var ergebnis = await ProjektVorschauPdfUseCase.AusfuehrenAsync(actions);

        Assert.Equal(ProjektVorschauPdfStatus.Fehler, ergebnis.Status);
        Assert.IsType<InvalidOperationException>(ergebnis.Fehler);
    }

    [Fact]
    public void Dateiname_behaelt_Leerzeichen_und_ersetzt_ungueltige_Zeichen()
    {
        var name = ProjektVorschauPdfUseCase.Dateiname("A:B/C", new DateTime(2026, 9, 7));

        Assert.Equal("Projektvorschau_A_B_C_20260907.pdf", name);
        Assert.StartsWith("Projektvorschau_Projekt A_", ProjektVorschauPdfUseCase.Dateiname("Projekt A"), StringComparison.Ordinal);
    }

    [Fact]
    public void Ohne_Projektnamen_heisst_die_Datei_Projekt()
        => Assert.StartsWith("Projektvorschau_Projekt_", ProjektVorschauPdfUseCase.Dateiname("   "), StringComparison.Ordinal);
}
