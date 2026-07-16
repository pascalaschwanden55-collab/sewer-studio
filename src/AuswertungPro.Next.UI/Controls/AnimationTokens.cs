using System;

namespace AuswertungPro.Next.UI.Controls;

/// <summary>
/// Zentrale Animationsdauern fuer Code-Animationen (Storyboards/BeginAnimation im Code-Behind).
/// Spiegeln die XAML-Tokens AnimDurationFast/Normal/Slow in Theme/Controls.xaml, damit XAML- und
/// Code-Animationen dieselben Werte nutzen. TimeSpan ist nicht const-faehig -> static readonly.
/// </summary>
public static class AnimationTokens
{
    /// <summary>Hover/Press — 120 ms.</summary>
    public static readonly TimeSpan Fast = TimeSpan.FromMilliseconds(120);

    /// <summary>Ein-/Ausblenden — 180 ms.</summary>
    public static readonly TimeSpan Normal = TimeSpan.FromMilliseconds(180);

    /// <summary>Seiten-/Ansichtswechsel — 300 ms.</summary>
    public static readonly TimeSpan Slow = TimeSpan.FromMilliseconds(300);

    /// <summary>Hero-/Entrance-Effekte (Fenster-Auftritt, gestaffelte Karten) — 450 ms.</summary>
    public static readonly TimeSpan XSlow = TimeSpan.FromMilliseconds(450);
}
