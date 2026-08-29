using System.Threading;
using System.Windows;
using System.Windows.Controls;
using AuswertungPro.Next.UI.Helpers;

namespace AuswertungPro.Next.UI.Tests;

public sealed class KeyboardTextInputFocusGuardTests
{
    [Fact]
    public void Text_und_Auswahlfelder_werden_als_aktive_Eingabe_erkannt()
    {
        RunInSta(() =>
        {
            Assert.True(KeyboardTextInputFocusGuard.IsTextInput(new TextBox()));
            Assert.True(KeyboardTextInputFocusGuard.IsTextInput(new RichTextBox()));
            Assert.True(KeyboardTextInputFocusGuard.IsTextInput(new PasswordBox()));
            Assert.True(KeyboardTextInputFocusGuard.IsTextInput(new ComboBox { IsEditable = true }));
            Assert.True(KeyboardTextInputFocusGuard.IsTextInput(new ComboBox { IsEditable = false }));
            Assert.True(KeyboardTextInputFocusGuard.IsTextInput(new ComboBoxItem()));
        });
    }

    [Fact]
    public void Andere_Steuerelemente_sperren_Fensterkuerzel_nicht()
    {
        RunInSta(() =>
        {
            Assert.False(KeyboardTextInputFocusGuard.IsTextInput(new Button()));
            Assert.False(KeyboardTextInputFocusGuard.IsTextInput(null));
        });
    }

    [Fact]
    public void Ein_eingeklapptes_Eingabefeld_sperrt_die_Kuerzel_nicht_mehr()
    {
        RunInSta(() =>
        {
            // Nach dem Bestaetigen wird das Eingabemarker-Feld ausgeblendet, behaelt aber
            // den Tastaturfokus. Sonst blieben Leertaste, R, S, P, D und M danach still.
            Assert.False(KeyboardTextInputFocusGuard.IsTextInput(
                new TextBox { Visibility = Visibility.Collapsed }));
            Assert.False(KeyboardTextInputFocusGuard.IsTextInput(
                new TextBox { Visibility = Visibility.Hidden }));
            Assert.False(KeyboardTextInputFocusGuard.IsTextInput(
                new ComboBox { Visibility = Visibility.Collapsed }));
        });
    }

    [Fact]
    public void Ein_gesperrtes_Eingabefeld_sperrt_die_Kuerzel_nicht()
    {
        RunInSta(() =>
        {
            Assert.False(KeyboardTextInputFocusGuard.IsTextInput(new TextBox { IsEnabled = false }));
            Assert.False(KeyboardTextInputFocusGuard.IsTextInput(new ComboBox { IsEnabled = false }));
        });
    }

    private static void RunInSta(Action action)
    {
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                error = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (error is not null)
            throw error;
    }
}
