using System.Reflection;
using AuswertungPro.Next.UI;
using AuswertungPro.Next.UI.ViewModels.Pages;

namespace AuswertungPro.Next.UI.Tests;

public sealed class ExportPageViewModelDependencyTests
{
    [Fact]
    public void ViewModel_speichert_keinen_ServiceProvider_als_Feld()
    {
        var fields = typeof(ExportPageViewModel).GetFields(
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

        Assert.DoesNotContain(fields, field => field.FieldType == typeof(ServiceProvider));
    }
}
