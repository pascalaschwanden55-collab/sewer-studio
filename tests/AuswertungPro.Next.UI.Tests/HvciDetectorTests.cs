using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Charakterisierungstests für HvciDetector (reine HVCI-Entscheidungslogik aus Registry-Wert).
/// </summary>
public sealed class HvciDetectorTests
{
    [Fact]
    public void IsEnabled_GanzzahlEins_True()
    {
        Assert.True(HvciDetector.IsEnabled(1));
    }

    [Fact]
    public void IsEnabled_GanzzahlNull_False()
    {
        Assert.False(HvciDetector.IsEnabled(0));
    }

    [Fact]
    public void IsEnabled_NullReferenz_False()
    {
        Assert.False(HvciDetector.IsEnabled(null));
    }

    [Fact]
    public void IsEnabled_String_False()
    {
        // Registry kann Strings liefern — kein int → nicht als HVCI aktiv werten
        Assert.False(HvciDetector.IsEnabled("1"));
    }

    [Fact]
    public void IsEnabled_AndererInt_False()
    {
        Assert.False(HvciDetector.IsEnabled(2));
        Assert.False(HvciDetector.IsEnabled(-1));
    }

    [Fact]
    public void IsEnabled_Long_False()
    {
        // long 1L ist kein int — Typprüfung muss greifen
        Assert.False(HvciDetector.IsEnabled(1L));
    }
}
