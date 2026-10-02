using System.Windows;
using System.Windows.Controls;
using AuswertungPro.Next.UI.Views;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Die gemeinsame Verdrahtung des VSA-Formulars von Eintrags-Editor und Beobachtungs-Katalog
/// (B6, Deepscan 02.10.2026). Laeuft im isolierten Kindprozess (WPF-Regel).
/// </summary>
public sealed class VsaFormularVerdrahtungTests
{
    [Fact]
    public async Task Verdrahtung_funktioniert_isoliert()
    {
        var result = await WpfIsolatedTestProcess.RunAsync(
            GetType().FullName + "." + nameof(Kindprozess_prueft_Verdrahtung), TimeSpan.FromSeconds(90));
        Assert.False(result.TimedOut, result.DescribeFailure());
        Assert.True(result.ExitCode == 0, result.DescribeFailure());
        Assert.True(result.ChildScenarioCompleted, result.DescribeFailure());
    }

    [IsolatedWpfFact]
    public void Kindprozess_prueft_Verdrahtung()
    {
        StaTestRunner.Run(() =>
        {
            var f = new VsaFormularVerdrahtung.Felder(
                new TextBox(), new TextBox(), new TextBox(), new TextBox(), new TextBox(), new TextBox(),
                new TextBox(), new TextBox(),
                new ComboBox { IsEditable = true }, new ComboBox { IsEditable = true }, new ComboBox { IsEditable = true },
                new CheckBox());
            var validierungen = 0;
            VsaFormularVerdrahtung.Verdrahte(f, () => validierungen++);

            // Jede Aenderung validiert.
            f.Distanz.Text = "2.50";
            Check(validierungen >= 1, "TextChanged validiert");
            var vorher = validierungen;
            f.Verbindung.IsChecked = true;
            Check(validierungen == vorher + 1, "Checked validiert");
            f.Verbindung.IsChecked = false;
            Check(validierungen == vorher + 2, "Unchecked validiert");

            // Beim Verlassen: lesbare Zahl wird normalisiert und validiert (der neue Text validiert zusaetzlich).
            vorher = validierungen;
            f.Distanz.RaiseEvent(new RoutedEventArgs(UIElement.LostFocusEvent));
            Check(f.Distanz.Text == "2.5", "Zahl normalisiert: " + f.Distanz.Text);
            Check(validierungen > vorher, "Verlassen validiert");

            // Unlesbarer Text bleibt stehen, es wird trotzdem validiert (dort steht der Fehler).
            f.Q1.Text = "abc";
            vorher = validierungen;
            f.Q1.RaiseEvent(new RoutedEventArgs(UIElement.LostFocusEvent));
            Check(f.Q1.Text == "abc", "unlesbarer Text bleibt: " + f.Q1.Text);
            Check(validierungen == vorher + 1, "unlesbar validiert genau einmal");

            // Ohne Validierung (null) wird nur normalisiert.
            var box = new TextBox { Text = "7.0" };
            VsaFormularVerdrahtung.Zahl(box, null);
            Check(box.Text == "7", "Zahl ohne validiere: " + box.Text);

            WpfIsolatedTestProcess.MarkChildScenarioCompleted();
        });
    }

    private static void Check(bool bedingung, string was)
    {
        if (!bedingung)
            throw new InvalidOperationException("Verdrahtung verletzt: " + was);
    }
}
