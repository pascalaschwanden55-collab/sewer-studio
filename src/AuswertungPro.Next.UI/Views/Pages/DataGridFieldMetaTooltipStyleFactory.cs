using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace AuswertungPro.Next.UI.Views.Pages;

public static class DataGridFieldMetaTooltipStyleFactory
{
    public static Style Create(string fieldName, Style? baseStyle)
    {
        var style = new Style(typeof(DataGridCell), baseStyle);

        var tooltip = new TextBlock();
        var binding = new MultiBinding { StringFormat = "Quelle: {0} | UserEdited: {1} | Konflikt: {2}" };
        binding.Bindings.Add(new Binding($"FieldMeta[{fieldName}].Source"));
        binding.Bindings.Add(new Binding($"FieldMeta[{fieldName}].UserEdited"));
        binding.Bindings.Add(new Binding($"FieldMeta[{fieldName}].Conflict"));
        tooltip.SetBinding(TextBlock.TextProperty, binding);

        style.Setters.Add(new Setter(FrameworkElement.ToolTipProperty, tooltip));
        return style;
    }
}
