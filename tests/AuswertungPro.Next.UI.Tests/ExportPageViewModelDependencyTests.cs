using System.Reflection;
using AuswertungPro.Next.Application.Import;
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

    [Fact]
    public void ViewModel_verwendet_den_Application_Vertrag_fuer_Importdateien()
    {
        var field = typeof(ExportPageViewModel).GetField(
            "_storedImportFiles",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(field);
        Assert.Equal(typeof(IStoredImportFileService), field!.FieldType);
    }
}
