using AuswertungPro.Next.UI.Services;
using AuswertungPro.Next.UI.ViewModels;
using Xunit;

namespace AuswertungPro.Next.UI.Tests;

public sealed class KiBereitschaftRegelTests
{
    [Theory]
    [InlineData(false, "", KiBereitschaft.NichtGestartet, "KI nicht gestartet")]
    [InlineData(true, "KI STARTET", KiBereitschaft.Startet, "KI startet")]
    [InlineData(true, "KI BEREIT", KiBereitschaft.Bereit, "Analyse bereit")]
    [InlineData(true, "KI WARNUNG", KiBereitschaft.PruefungNoetig, "Prüfung nötig")]
    public void Leistentext_folgt_dem_Laufzeitstatus(bool sichtbar, string titel, KiBereitschaft erwartet, string text)
    {
        var stand = KiBereitschaftRegel.Bestimme(new AiRuntimeStatus(sichtbar, titel, "", ""));
        Assert.Equal(erwartet, stand);
        Assert.Equal(text, KiBereitschaftRegel.Text(stand));
    }
}
