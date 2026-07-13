using System.Reflection;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Media;
using AuswertungPro.Next.UI.Services;
using AuswertungPro.Next.UI.Views.Windows;

namespace AuswertungPro.Next.UI.Tests;

public sealed class MediaSearchWindowDependencyTests
{
    [Fact]
    public void Fenster_nimmt_nur_seine_benoetigten_abhaengigkeiten_entgegen()
    {
        var constructor = typeof(MediaSearchWindow).GetConstructor(
        [
            typeof(IReadOnlyList<HaltungRecord>),
            typeof(string),
            typeof(IDialogService),
            typeof(AppSettings),
            typeof(BatchMediaSearchService)
        ]);

        Assert.NotNull(constructor);

        var instanceFields = typeof(MediaSearchWindow).GetFields(
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        Assert.DoesNotContain(instanceFields, field => field.FieldType == typeof(ServiceProvider));
    }

    [Fact]
    public void ServiceProvider_stellt_medien_suchdienst_zentral_bereit()
    {
        var property = typeof(ServiceProvider).GetProperty(nameof(ServiceProvider.BatchMediaSearch));

        Assert.NotNull(property);
        Assert.Equal(typeof(BatchMediaSearchService), property.PropertyType);
        Assert.False(property.CanWrite);
    }
}
