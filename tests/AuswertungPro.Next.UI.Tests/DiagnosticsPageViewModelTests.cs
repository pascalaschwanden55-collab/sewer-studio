using AuswertungPro.Next.Application.Diagnostics;
using AuswertungPro.Next.UI.ViewModels.Pages;

namespace AuswertungPro.Next.UI.Tests;

public sealed class DiagnosticsPageViewModelTests
{
    [Fact]
    public void Konstruktor_zeigt_die_gelesenen_logzeilen()
    {
        var viewModel = new DiagnosticsPageViewModel(
            new FakeLogTailReader(new LogTailReadResult(
                FileExists: true,
                Lines: ["Warnung 1", "Fehler 2"],
                UserMessage: null)));

        Assert.Equal("Warnung 1" + Environment.NewLine + "Fehler 2", viewModel.LogTail);
    }

    [Fact]
    public void Konstruktor_zeigt_verstaendlichen_hinweis_wenn_datei_fehlt()
    {
        var viewModel = new DiagnosticsPageViewModel(
            new FakeLogTailReader(new LogTailReadResult(
                FileExists: false,
                Lines: [],
                UserMessage: null)));

        Assert.Equal("Noch keine Log-Datei vorhanden.", viewModel.LogTail);
    }

    [Fact]
    public void Konstruktor_zeigt_sichere_fachmeldung_statt_roher_fehlerdetails()
    {
        var viewModel = new DiagnosticsPageViewModel(
            new FakeLogTailReader(new LogTailReadResult(
                FileExists: true,
                Lines: [],
                UserMessage: "Tageslog konnte nicht gelesen werden. Details stehen im Programmlog.")));

        Assert.Equal(
            "Tageslog konnte nicht gelesen werden. Details stehen im Programmlog.",
            viewModel.LogTail);
    }

    private sealed class FakeLogTailReader(LogTailReadResult result) : ILogTailReader
    {
        public LogTailReadResult ReadToday(int maximumLines = 200) => result;
    }
}
