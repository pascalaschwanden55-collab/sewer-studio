using System.IO;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows.Controls;
using System.Windows.Data;
using AuswertungPro.Next.UI.Views.Pages;

namespace AuswertungPro.Next.UI.Tests;

public sealed class DataGridStandardTextColumnFactoryTests
{
    [Fact]
    public void Create_builds_standard_text_column_metadata()
    {
        RunOnSta(() =>
        {
            var column = DataGridStandardTextColumnFactory.Create("Bemerkungen", "Bemerkungen");

            Assert.Equal("Bemerkungen", column.Header);
            Assert.Equal(DataGridLengthUnitType.SizeToHeader, column.Width.UnitType);

            var binding = Assert.IsType<Binding>(column.Binding);
            Assert.Equal("Fields[Bemerkungen]", binding.Path.Path);
            Assert.Equal(BindingMode.TwoWay, binding.Mode);
            Assert.Equal(UpdateSourceTrigger.LostFocus, binding.UpdateSourceTrigger);
        });
    }

    [Fact]
    public void DataPageColumnFactory_delegates_standard_text_columns_to_factory()
    {
        var source = File.ReadAllText(Path.Combine(
            TestRepoPaths.FindRepositoryRoot(),
            "src",
            "AuswertungPro.Next.UI",
            "Views",
            "Pages",
            "DataPageColumnFactory.cs"));

        Assert.Contains("DataGridStandardTextColumnFactory.Create", source, StringComparison.Ordinal);
        Assert.DoesNotContain("new DataGridTextColumn", source, StringComparison.Ordinal);
        Assert.DoesNotContain("new Binding($\"Fields[{fieldName}]\")", source, StringComparison.Ordinal);
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
            ExceptionDispatchInfo.Capture(exception).Throw();
    }
}
