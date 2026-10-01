using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows.Controls;
using AuswertungPro.Next.Application.UseCases.CodingSuggestions;
using AuswertungPro.Next.UI.Player;
using Xunit;

namespace AuswertungPro.Next.UI.Tests;

public sealed class SuggestionMarkerControllerTests
{
    [Fact]
    public void Build_zeichnet_je_offenem_Vorschlag_einen_Marker_und_Clear_entfernt_alle()
    {
        var result = RunOnSta(() =>
        {
            var canvas = new Canvas();
            var gesprungen = new List<double>();
            var controller = new SuggestionMarkerController(
                canvas,
                () => (0.0, 400.0),
                () => 200.0,
                gesprungen.Add);

            var rows = new List<CodingSuggestionRow>
            {
                new(new CodingSuggestion(CodingSuggestionKind.Rohranfang, 4, null, false, 0.9, true, 0.85)),
                new(new CodingSuggestion(CodingSuggestionKind.Bogen, 100, 9.4, false, 0.9, true, 0)),
                new(new CodingSuggestion(CodingSuggestionKind.Rohrende, 500, null, false, 0.9, true, 0.89)) // ausserhalb
            };
            controller.Build(rows);
            var nachBuild = canvas.Children.Count;
            var links = Canvas.GetLeft(canvas.Children[1]);
            controller.Clear();
            return (nachBuild, links, nachClear: canvas.Children.Count);
        });

        Assert.Equal(2, result.nachBuild);
        Assert.Equal(200.0 - 1, result.links, 3);
        Assert.Equal(0, result.nachClear);
    }

    private static T RunOnSta<T>(Func<T> func)
    {
        T result = default!;
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try { result = func(); }
            catch (Exception ex) { error = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (error is not null) throw error;
        return result;
    }
}
