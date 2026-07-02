using System;
using AuswertungPro.Next.UI.Controls;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Sichert den Vertrag der Animationsdauern, auf den spaetere Pakete (Toast/Busy/Mikro-Feedback)
/// aufbauen: dokumentierte Werte + streng steigende Ordnung Fast &lt; Normal &lt; Slow.
/// </summary>
public sealed class AnimationTokensTests
{
    [Fact]
    public void Durations_have_documented_values()
    {
        Assert.Equal(TimeSpan.FromMilliseconds(120), AnimationTokens.Fast);
        Assert.Equal(TimeSpan.FromMilliseconds(180), AnimationTokens.Normal);
        Assert.Equal(TimeSpan.FromMilliseconds(300), AnimationTokens.Slow);
    }

    [Fact]
    public void Durations_are_strictly_increasing()
    {
        Assert.True(AnimationTokens.Fast < AnimationTokens.Normal);
        Assert.True(AnimationTokens.Normal < AnimationTokens.Slow);
    }
}
