using System.Windows;
using System.Windows.Controls;

namespace AuswertungPro.Next.UI.Views.Pages;

public static class DataGridEditedTextValueResolver
{
    public static string? Resolve(FrameworkElement? element)
    {
        if (element is ComboBox combo)
            return ResolveComboBoxValue(combo);
        if (element is TextBox textBox)
            return textBox.Text;

        return null;
    }

    public static bool TryResolve(FrameworkElement? element, out string value)
    {
        var resolved = Resolve(element);
        if (resolved is null)
        {
            value = string.Empty;
            return false;
        }

        value = resolved;
        return true;
    }

    public static string ResolveComboBoxValue(ComboBox combo)
    {
        if (combo.SelectedItem is string selected && !string.IsNullOrWhiteSpace(selected))
            return selected;

        return combo.Text ?? string.Empty;
    }
}
