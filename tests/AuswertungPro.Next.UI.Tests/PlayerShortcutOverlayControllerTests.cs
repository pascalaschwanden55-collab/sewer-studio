using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerShortcutOverlayControllerTests
{
    [Theory]
    [InlineData(Key.F1)]
    [InlineData(Key.OemQuestion)]
    public void HandleKey_oeffnet_verdeckte_Tastaturhilfe(Key key)
    {
        StaTestRunner.Run(() =>
        {
            var overlay = new Border { Visibility = Visibility.Collapsed };
            var controller = new PlayerShortcutOverlayController(overlay);

            var outcome = controller.HandleKey(key);

            Assert.Equal(PlayerShortcutOverlayKeyOutcome.Handled, outcome);
            Assert.Equal(Visibility.Visible, overlay.Visibility);
        });
    }

    [Theory]
    [InlineData(Key.Escape)]
    [InlineData(Key.F1)]
    [InlineData(Key.OemQuestion)]
    public void HandleKey_schliesst_sichtbare_Tastaturhilfe(Key key)
    {
        StaTestRunner.Run(() =>
        {
            var overlay = new Border { Visibility = Visibility.Visible };
            var controller = new PlayerShortcutOverlayController(overlay);

            var outcome = controller.HandleKey(key);

            Assert.Equal(PlayerShortcutOverlayKeyOutcome.Handled, outcome);
            Assert.Equal(Visibility.Collapsed, overlay.Visibility);
        });
    }

    [Fact]
    public void HandleKey_blockiert_andere_Shortcuts_solange_Hilfe_sichtbar_ist()
    {
        StaTestRunner.Run(() =>
        {
            var overlay = new Border { Visibility = Visibility.Visible };
            var controller = new PlayerShortcutOverlayController(overlay);

            var outcome = controller.HandleKey(Key.Space);

            Assert.Equal(PlayerShortcutOverlayKeyOutcome.Blocked, outcome);
            Assert.Equal(Visibility.Visible, overlay.Visibility);
        });
    }

    [Fact]
    public void HandleKey_laesst_normale_Taste_bei_verdeckter_Hilfe_durch()
    {
        StaTestRunner.Run(() =>
        {
            var overlay = new Border { Visibility = Visibility.Collapsed };
            var controller = new PlayerShortcutOverlayController(overlay);

            var outcome = controller.HandleKey(Key.Space);

            Assert.Equal(PlayerShortcutOverlayKeyOutcome.Continue, outcome);
            Assert.Equal(Visibility.Collapsed, overlay.Visibility);
        });
    }
}
