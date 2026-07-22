using System.Globalization;
using System.Windows;
using AuswertungPro.Next.UI.Controls;
using Xunit;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Sichert die Zustandsauswahl des <see cref="StatusHost"/> ueber seinen
/// <see cref="EnumToVisibilityConverter"/>: nur der Bereich, dessen Name dem aktuellen
/// <see cref="StatusHostState"/> entspricht, ist sichtbar.
/// </summary>
public sealed class EnumToVisibilityConverterTests
{
    [Theory]
    [InlineData(StatusHostState.Content, "Content", true)]
    [InlineData(StatusHostState.Loading, "Loading", true)]
    [InlineData(StatusHostState.Empty, "Empty", true)]
    [InlineData(StatusHostState.Error, "Error", true)]
    [InlineData(StatusHostState.Content, "Loading", false)]
    [InlineData(StatusHostState.Error, "Content", false)]
    [InlineData(StatusHostState.Empty, "Error", false)]
    public void Matches_vergleicht_Zustand_mit_Zielname(StatusHostState state, string zielZone, bool erwartet)
        => Assert.Equal(erwartet, EnumToVisibilityConverter.Matches(state, zielZone));

    [Fact]
    public void Matches_ist_false_bei_null()
    {
        Assert.False(EnumToVisibilityConverter.Matches(null, "Content"));
        Assert.False(EnumToVisibilityConverter.Matches(StatusHostState.Content, null));
    }

    [Fact]
    public void Convert_liefert_Visible_bei_Treffer_und_Collapsed_sonst()
    {
        var converter = new EnumToVisibilityConverter();

        Assert.Equal(Visibility.Visible, converter.Convert(
            StatusHostState.Loading, typeof(Visibility), "Loading", CultureInfo.InvariantCulture));
        Assert.Equal(Visibility.Collapsed, converter.Convert(
            StatusHostState.Loading, typeof(Visibility), "Content", CultureInfo.InvariantCulture));
    }

    [Fact]
    public void Genau_ein_Zustand_ist_je_State_sichtbar()
    {
        string[] zonen = ["Content", "Loading", "Empty", "Error"];
        foreach (StatusHostState state in System.Enum.GetValues<StatusHostState>())
        {
            var sichtbar = System.Array.FindAll(zonen, z => EnumToVisibilityConverter.Matches(state, z));
            Assert.Single(sichtbar);
        }
    }
}
