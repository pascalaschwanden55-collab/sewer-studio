using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using AuswertungPro.Next.UI.Views.Pages;

namespace AuswertungPro.Next.UI.Tests;

public sealed class DataGridColorCellStyleFactoryTests
{
    [Theory]
    [InlineData("Zustandsklasse")]
    [InlineData("Eigentuemer")]
    [InlineData("Pruefungsresultat")]
    [InlineData("Referenzpruefung")]
    [InlineData("Ausgefuehrt_durch")]
    [InlineData("Sanieren_JaNein")]
    public void CreateHaltungenStyle_returns_style_for_highlighted_fields(string fieldName)
    {
        RunOnSta(() =>
        {
            var style = DataGridColorCellStyleFactory.CreateHaltungenStyle(fieldName);

            Assert.NotNull(style);
            AssertBindingPath(style!, $"Fields[{fieldName}]");
        });
    }

    [Fact]
    public void CreateHaltungenStyle_returns_null_for_plain_fields()
    {
        RunOnSta(() =>
        {
            var style = DataGridColorCellStyleFactory.CreateHaltungenStyle("Bemerkungen");

            Assert.Null(style);
        });
    }

    [Theory]
    [InlineData("Zustandsklasse")]
    [InlineData("Eigentumer")]
    [InlineData("Ausgefuehrt durch")]
    [InlineData("Ausgeführt durch")]
    [InlineData("Sanieren durch")]
    [InlineData("Sanieren Ja/Nein")]
    [InlineData("Ja/Nein")]
    [InlineData("Dichtheit")]
    public void CreateSchaechteStyle_returns_style_for_normalized_highlighted_columns(string columnName)
    {
        RunOnSta(() =>
        {
            var style = DataGridColorCellStyleFactory.CreateSchaechteStyle(columnName);

            Assert.NotNull(style);
            AssertBindingPath(style!, $"Fields[{columnName}]");
        });
    }

    private static void AssertBindingPath(Style style, string expectedPath)
    {
        var binding = style.Setters
            .OfType<Setter>()
            .Select(setter => setter.Value)
            .OfType<Binding>()
            .FirstOrDefault();

        Assert.NotNull(binding);
        Assert.Equal(expectedPath, binding!.Path.Path);
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
