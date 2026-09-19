using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AuswertungPro.Next.UI.Services;
using Xunit;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Auditbefund 15 (18.09.2026): Die verzoegerte und die sofortige Einstellungsspeicherung
/// entnahmen ihren Auftrag zwar unter einer Sperre, SCHRIEBEN ihn aber ausserhalb davon.
/// Begann der verzoegerte Lauf zuerst und wurde langsamer fertig, lag hinterher der
/// AELTERE Stand auf der Platte — betroffen waren Anzeigeoptionen, Sicherungspfade und
/// gemerkte Ordner.
///
/// Die Reihenfolge ist deshalb eine eigene, pruefbare Regel: Jeder Schreibauftrag traegt
/// eine Nummer, und ein veralteter Stand wird verworfen statt geschrieben.
/// </summary>
public sealed class SettingsWriteOrderTests
{
    [Fact]
    public void Nummern_steigen_monoton()
    {
        var reihenfolge = new SettingsWriteOrder();

        var a = reihenfolge.Next();
        var b = reihenfolge.Next();
        var c = reihenfolge.Next();

        Assert.True(a < b && b < c, $"{a} < {b} < {c}");
    }

    [Fact]
    public void Ein_aelterer_Stand_wird_nach_einem_neueren_verworfen()
    {
        var reihenfolge = new SettingsWriteOrder();
        var alt = reihenfolge.Next();
        var neu = reihenfolge.Next();

        Assert.True(reihenfolge.Write(neu, () => { }), "Der neuere Stand muss geschrieben werden.");
        Assert.False(reihenfolge.Write(alt, () => { }), "Der aeltere Stand darf den neueren nicht ueberschreiben.");
    }

    [Fact]
    public void Derselbe_Stand_wird_nicht_zweimal_geschrieben()
    {
        var reihenfolge = new SettingsWriteOrder();
        var nummer = reihenfolge.Next();

        Assert.True(reihenfolge.Write(nummer, () => { }));
        Assert.False(reihenfolge.Write(nummer, () => { }));
    }

    [Fact]
    public async Task Unter_Last_gewinnt_immer_der_neueste_Stand()
    {
        var reihenfolge = new SettingsWriteOrder();
        var auftraege = Enumerable.Range(0, 200).Select(_ => reihenfolge.Next()).ToArray();
        var geschrieben = new List<long>();
        var sperre = new object();

        // Absichtlich in zufaelliger Reihenfolge — genau das macht der Wettlauf.
        await Task.WhenAll(auftraege.OrderBy(_ => Guid.NewGuid()).Select(nummer => Task.Run(() =>
        {
            // Das Mitschreiben laeuft INNERHALB der Regel — nur so misst der Test die
            // tatsaechliche Reihenfolge auf der Platte und nicht die des Testcodes.
            reihenfolge.Write(nummer, () => { lock (sperre) geschrieben.Add(nummer); });
        })));

        Assert.NotEmpty(geschrieben);
        // Was durchkam, muss streng aufsteigend sein: nie ein Rueckschritt auf der Platte.
        Assert.Equal(geschrieben.OrderBy(x => x).ToArray(), geschrieben.ToArray());
        // Der zuletzt vergebene Stand muss am Ende gewonnen haben.
        Assert.Equal(auftraege.Max(), geschrieben.Max());
    }
}
