using System.Windows.Controls;
using System.Windows.Data;

namespace AuswertungPro.Next.UI.Views.Pages;

public static class DataGridStandardTextColumnFactory
{
    public static DataGridTextColumn Create(
        string fieldName,
        string header,
        UpdateSourceTrigger updateSourceTrigger = UpdateSourceTrigger.LostFocus)
    {
        return new DataGridTextColumn
        {
            Header = header,
            Binding = new Binding($"Fields[{fieldName}]")
            {
                Mode = BindingMode.TwoWay,
                UpdateSourceTrigger = updateSourceTrigger
            },
            Width = DataGridLength.SizeToHeader
        };
    }
}
