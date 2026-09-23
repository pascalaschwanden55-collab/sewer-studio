using AuswertungPro.Next.Application.Common;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.Projects;

/// <summary>
/// Audit A08/A02 (23.09.2026), Entscheid «nur im selben Ordner»: Videos, Fotos, PDFs, Kosten und Dossiers eines
/// Projekts liegen in seinem Ordner und werden relativ dazu gefunden. Eine Projektdatei in einem anderen Ordner
/// fand danach keines davon mehr — Kosten und Dossiers erschienen leer.
/// </summary>
public sealed class SpeichernUnterRegelTests
{
    private const string Alt = @"D:\Projekte\Zone1\Projektdateien\projekt.json";

    [Theory]
    [InlineData(@"D:\Projekte\Zone1\Projektdateien\projekt_kopie.json")]
    [InlineData(@"D:\Projekte\Zone1\projekt.json")]              // alter Ablageort im selben Projekt
    [InlineData(@"d:\projekte\zone1\Projektdateien\Neu.json")]   // Gross-/Kleinschreibung
    public void Im_selben_projektordner_ist_es_erlaubt(string neu)
        => Assert.Null(SpeichernUnterRegel.Sperrgrund(Alt, neu));

    [Theory]
    [InlineData(@"D:\Projekte\Zone2\Projektdateien\projekt.json")]
    [InlineData(@"E:\Sicherung\projekt.json")]
    [InlineData(@"D:\Projekte\Zone1\Unterordner\projekt.json")]
    public void In_einem_anderen_ordner_wird_es_gesperrt(string neu)
    {
        var grund = SpeichernUnterRegel.Sperrgrund(Alt, neu);
        Assert.NotNull(grund);
        Assert.Contains(@"D:\Projekte\Zone1", grund);
        Assert.Contains("Explorer", grund);
    }

    [Fact]
    public void Ein_noch_nie_gespeichertes_projekt_darf_ueberall_hin()
        => Assert.Null(SpeichernUnterRegel.Sperrgrund(null, @"E:\Neu\projekt.json"));
}
