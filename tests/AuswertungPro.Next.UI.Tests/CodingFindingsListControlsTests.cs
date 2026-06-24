using System.Reflection;
using System.Threading;
using System.Windows.Controls;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.UI.Views.Windows;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingFindingsListControlsTests
{
    [Fact]
    public void ShowFindings_sets_projected_findings_items_source()
    {
        RunOnStaThread(() =>
        {
            var list = new ListBox();
            var findings = new[]
            {
                new LiveFrameFinding(
                    Label: "Riss",
                    Severity: 2,
                    PositionClock: null,
                    ExtentPercent: null,
                    VsaCodeHint: "BAB")
            };
            var show = FindMethod("ShowFindings", typeof(ItemsControl), typeof(IEnumerable<LiveFrameFinding>));
            Assert.NotNull(show);

            show.Invoke(null, [list, findings]);

            var item = Assert.Single(Items(list));
            Assert.Equal("Riss", item.Label);
            Assert.Equal("BAB", item.VsaCode);
        });
    }

    [Fact]
    public void ShowBoundary_sets_single_boundary_item()
    {
        RunOnStaThread(() =>
        {
            var list = new ListBox();
            var show = FindMethod("ShowBoundary", typeof(ItemsControl), typeof(string), typeof(string));
            Assert.NotNull(show);

            show.Invoke(null, [list, "BCD", "Rohranfang"]);

            var item = Assert.Single(Items(list));
            Assert.Equal("Rohranfang", item.Label);
            Assert.Equal("BCD", item.VsaCode);
        });
    }

    [Fact]
    public void ShowPossibleBoundary_sets_single_possible_boundary_item()
    {
        RunOnStaThread(() =>
        {
            var list = new ListBox();
            var show = FindMethod("ShowPossibleBoundary", typeof(ItemsControl), typeof(string), typeof(string));
            Assert.NotNull(show);

            show.Invoke(null, [list, "BCE", "Rohrende"]);

            var item = Assert.Single(Items(list));
            Assert.StartsWith("M", item.Label, StringComparison.Ordinal);
            Assert.EndsWith("Rohrende", item.Label, StringComparison.Ordinal);
            Assert.Equal("BCE", item.VsaCode);
        });
    }

    [Fact]
    public void ShowResolvedFinding_overwrites_vsa_hint()
    {
        RunOnStaThread(() =>
        {
            var list = new ListBox();
            var finding = new LiveFrameFinding(
                Label: "Rohrende",
                Severity: 4,
                PositionClock: null,
                ExtentPercent: null,
                VsaCodeHint: "BCA");
            var show = FindMethod("ShowResolvedFinding", typeof(ItemsControl), typeof(LiveFrameFinding), typeof(string));
            Assert.NotNull(show);

            show.Invoke(null, [list, finding, "BCE"]);

            var item = Assert.Single(Items(list));
            Assert.Equal("Rohrende", item.Label);
            Assert.Equal("BCE", item.VsaCode);
        });
    }

    private static IReadOnlyList<AiFindingDisplayItem> Items(ItemsControl list)
        => Assert.IsAssignableFrom<IEnumerable<AiFindingDisplayItem>>(list.ItemsSource).ToList();

    private static MethodInfo? FindMethod(string name, params Type[] parameterTypes)
        => typeof(AiFindingDisplayItemFactory).Assembly
            .GetType("AuswertungPro.Next.UI.Views.Windows.CodingFindingsListControls")
            ?.GetMethod(
                name,
                BindingFlags.Public | BindingFlags.Static,
                binder: null,
                types: parameterTypes,
                modifiers: null);

    private static void RunOnStaThread(Action action)
    {
        Exception? exception = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                exception = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (exception is not null)
            throw exception;
    }
}
