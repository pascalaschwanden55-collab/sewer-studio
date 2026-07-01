using System.IO;
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

    [Fact]
    public void DataPage_and_SchaechtePage_delegate_color_style_selection()
    {
        var root = SourceTextTestHelpers.FindRepositoryRoot();
        var pagesRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI", "Views", "Pages");
        var dataPage = File.ReadAllText(Path.Combine(pagesRoot, "DataPage.xaml.cs"));
        var schaechtePage = File.ReadAllText(Path.Combine(pagesRoot, "SchaechtePage.xaml.cs"));

        Assert.Contains("DataGridColorCellStyleFactory.CreateHaltungenStyle(", dataPage, StringComparison.Ordinal);
        Assert.Contains("DataGridColorCellStyleFactory.CreateSchaechteStyle(", schaechtePage, StringComparison.Ordinal);
        Assert.DoesNotContain("ZustandsklasseCellStyleFactory.Create", dataPage, StringComparison.Ordinal);
        Assert.DoesNotContain("ZustandsklasseCellStyleFactory.Create", schaechtePage, StringComparison.Ordinal);
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
