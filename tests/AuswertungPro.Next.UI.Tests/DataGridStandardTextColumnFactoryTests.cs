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

    /// <summary>
    /// Fix-Runde 1 (F4): Die Drei-Zeilen-Grenze gilt nur im Nova-Layout. Die alte
    /// Haltungsansicht bleibt unveraendert — dort wird keine Zelle gekuerzt.
    /// </summary>
    [Fact]
    public void Die_Hoehengrenze_gilt_nur_auf_ausdruecklichen_Wunsch()
    {
        RunOnSta(() =>
        {
            var ohne = DataGridStandardTextColumnFactory.Create("Bemerkungen", "Bemerkungen");
            Assert.DoesNotContain(
                ohne.ElementStyle.Setters.OfType<System.Windows.Setter>(),
                setter => setter.Property == System.Windows.FrameworkElement.MaxHeightProperty);

            var mit = DataGridStandardTextColumnFactory.Create(
                "Bemerkungen", "Bemerkungen", UpdateSourceTrigger.LostFocus, hoeheBegrenzen: true);
            Assert.Contains(
                mit.ElementStyle.Setters.OfType<System.Windows.Setter>(),
                setter => setter.Property == System.Windows.FrameworkElement.MaxHeightProperty
                    && Equals(setter.Value, AuswertungPro.Next.UI.DataPage.DataPageColumnStyleRules.MaximaleZellenhoehe));
        });
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
