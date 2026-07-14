using System.Reflection;
using AuswertungPro.Next.UI.Player;
using AuswertungPro.Next.UI.Views.Windows;
using LibVLCSharp.Shared;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerMediaOwnershipContractTests
{
    private static readonly Type[] RawMediaTypes =
    [
        typeof(LibVLC),
        typeof(MediaPlayer)
    ];

    [Fact]
    public void PlayerWindow_Besitzt_Runtime_Und_Hosts_Aber_Keine_Rohen_Mediafelder()
    {
        var fields = typeof(PlayerWindow).GetFields(
            BindingFlags.Instance |
            BindingFlags.Public |
            BindingFlags.NonPublic |
            BindingFlags.DeclaredOnly);
        var properties = typeof(PlayerWindow).GetProperties(
            BindingFlags.Instance |
            BindingFlags.Public |
            BindingFlags.NonPublic |
            BindingFlags.DeclaredOnly);

        Assert.Contains(fields, field => field.FieldType == typeof(PlayerMediaRuntime));
        Assert.Contains(fields, field => field.FieldType == typeof(PlayerMediaHosts));
        Assert.DoesNotContain(fields, field => RawMediaTypes.Contains(field.FieldType));
        Assert.DoesNotContain(properties, property => RawMediaTypes.Contains(property.PropertyType));
    }

    [Fact]
    public void PlayerMediaRuntime_Gibt_Rohen_Mediaplayer_Nicht_Als_Eigenschaft_Oder_Feld_Frei()
    {
        var publicFields = typeof(PlayerMediaRuntime).GetFields(
            BindingFlags.Instance |
            BindingFlags.Public |
            BindingFlags.DeclaredOnly);
        var publicProperties = typeof(PlayerMediaRuntime).GetProperties(
            BindingFlags.Instance |
            BindingFlags.Public |
            BindingFlags.DeclaredOnly);

        Assert.DoesNotContain(publicFields, field => RawMediaTypes.Contains(field.FieldType));
        Assert.DoesNotContain(publicProperties, property => RawMediaTypes.Contains(property.PropertyType));
    }
}
