using AuswertungPro.Next.UI.ViewModels;
using Xunit;

namespace AuswertungPro.Next.UI.Tests;

public sealed class ShellNovaKopfzeileTests
{
    [Fact]
    public void Brotkrume_ist_Projekt_Schraegstrich_Seite()
        => Assert.Equal("Göschenen 2026 / Übersicht", ShellNovaKopfzeile.Brotkrume("Göschenen 2026", "Uebersicht"));

    [Fact]
    public void Brotkrume_ohne_Projekt_zeigt_nur_die_Seite()
        => Assert.Equal("Haltungen", ShellNovaKopfzeile.Brotkrume("", "Haltungen"));

    [Fact]
    public void Speicherstand_nennt_Projekt_und_Uhrzeit()
    {
        var t = new System.DateTime(2026, 9, 6, 14, 32, 0, System.DateTimeKind.Local);
        Assert.Equal("Göschenen 2026 · gespeichert 14:32", ShellNovaKopfzeile.Speicherstand("Göschenen 2026", t, false));
        Assert.Equal("Göschenen 2026 · ungespeichert", ShellNovaKopfzeile.Speicherstand("Göschenen 2026", t, true));
        Assert.Equal("Kein Projekt geöffnet", ShellNovaKopfzeile.Speicherstand("", null, false));
    }
}
