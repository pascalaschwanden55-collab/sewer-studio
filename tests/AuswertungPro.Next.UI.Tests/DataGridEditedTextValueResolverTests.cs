using System.Threading;
using System.Windows.Controls;
using AuswertungPro.Next.UI.Views.Pages;

namespace AuswertungPro.Next.UI.Tests;

public sealed class DataGridEditedTextValueResolverTests
{
    [Fact]
    public void ResolveComboBoxValue_prefers_non_blank_selected_string()
    {
        RunOnSta(() =>
        {
            var combo = new ComboBox { Text = "Freitext" };
            combo.Items.Add("Auswahl");
            combo.SelectedItem = "Auswahl";

            var value = DataGridEditedTextValueResolver.ResolveComboBoxValue(combo);

            Assert.Equal("Auswahl", value);
        });
    }

    [Fact]
    public void ResolveComboBoxValue_falls_back_to_text()
    {
        RunOnSta(() =>
        {
            var combo = new ComboBox
            {
                SelectedItem = " ",
                Text = "Freitext"
            };

            var value = DataGridEditedTextValueResolver.ResolveComboBoxValue(combo);

            Assert.Equal("Freitext", value);
        });
    }

    [Fact]
    public void Resolve_returns_textbox_text()
    {
        RunOnSta(() =>
        {
            var textBox = new TextBox { Text = "Bemerkung" };

            var value = DataGridEditedTextValueResolver.Resolve(textBox);

            Assert.Equal("Bemerkung", value);
        });
    }

    [Fact]
    public void TryResolve_returns_false_for_unsupported_element()
    {
        var success = DataGridEditedTextValueResolver.TryResolve(null, out var value);

        Assert.False(success);
        Assert.Equal(string.Empty, value);
    }

    private static void RunOnSta(Action action)
    {
        Exception? exception = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                exception = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (exception is not null)
            throw exception;
    }
}
