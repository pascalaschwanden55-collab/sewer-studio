using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using AuswertungPro.Next.UI.Views.Windows;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

public sealed class TrainingStudioModelSelectionFocusPolicyTests
{
    [Fact]
    public void Nach_einer_Modellwahl_gibt_die_Liste_den_Tastaturfokus_frei()
    {
        // Die Auswahlliste bleibt sichtbar und behaelt sonst den Fokus.
        // A, K, V und die Pfeiltasten waeren danach still, bis woanders geklickt wird.
        Assert.True(TrainingStudioModelSelectionFocusPolicy.ShouldReleaseFocus(
            hasSelection: true, listHasFocus: true));
    }

    [Fact]
    public void Eine_programmgesteuerte_Auswahl_reisst_den_Fokus_nicht_weg()
    {
        // Die Kandidatenliste wird nach der KI-Bereitschaft neu aufgebaut und dabei
        // vorbelegt. Wer gerade woanders schreibt, darf den Fokus nicht verlieren.
        Assert.False(TrainingStudioModelSelectionFocusPolicy.ShouldReleaseFocus(
            hasSelection: true, listHasFocus: false));
    }

    [Fact]
    public void Eine_geleerte_Liste_gibt_keinen_Fokus_frei()
    {
        Assert.False(TrainingStudioModelSelectionFocusPolicy.ShouldReleaseFocus(
            hasSelection: false, listHasFocus: true));
    }

    [Fact]
    public void Das_Fokusziel_muss_fokussierbar_sein()
    {
        // Ein Image ist in WPF NICHT fokussierbar - stuende es als Ziel im Handler,
        // bliebe der Fokus still auf der Auswahlliste und der Fix liefe ins Leere.
        RunInSta(() =>
        {
            Assert.False(new Image().Focusable);
            Assert.True(new Window().Focusable);
        });

        var code = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(), "src", "AuswertungPro.Next.UI", "Views", "Windows",
            "TrainingStudioWindow.xaml.cs"));
        Assert.Contains("TrainingStudioModelSelectionFocusPolicy.ShouldReleaseFocus", code, StringComparison.Ordinal);
        Assert.Contains("Keyboard.Focus(this)", code, StringComparison.Ordinal);
        Assert.DoesNotContain("Keyboard.Focus(FrameImage)", code, StringComparison.Ordinal);
    }

    private static void RunInSta(Action action)
    {
        Exception? fehler = null;
        var thread = new Thread(() => { try { action(); } catch (Exception ex) { fehler = ex; } });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (fehler is not null) throw fehler;
    }
}
