using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using AuswertungPro.Next.UI;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Erzeugt jedes Bedienelement der Oberflaeche einmal und prueft, ob WPF dabei
/// Bindungsfehler meldet (Codeaudit 2026-08-17, Punkt 3).
///
/// Warum es das braucht: XAML-Verweise ins Leere, fehlende Ressourcen und
/// Bindungen auf nicht vorhandene Eigenschaften meldet WPF nur in die
/// Ablaufverfolgung. Im laufenden Programm hoert dort niemand zu — die Anzeige
/// bleibt einfach leer, ohne Fehlermeldung. Ein Test, der zuhoert, macht diese
/// ganze Fehlerklasse zum ersten Mal sichtbar.
///
/// Laeuft im isolierten WPF-Kindprozess (bestehende Infrastruktur, wie
/// BeobachtungenWindowIsolatedSmokeTests): Ein Application-Objekt ist
/// prozessweit und einmalig, und im Elternprozess darf es keines geben.
/// Deshalb hier KEIN eigener Wirt.
/// </summary>
[Collection("IsolatedWpf")]
public sealed class OberflaechenBindungsRauchtestTests
{
    private static readonly string ChildTestName =
        typeof(OberflaechenBindungsRauchtestTests).FullName
        + "."
        + nameof(Kindprozess_erzeugt_alle_Bedienelemente);

    /// <summary>
    /// Bindungen, die den Fenster-Vorfahren suchen, sind ohne echtes
    /// Fensterhandle nicht bewertbar; im laufenden Programm sitzt jede Seite in
    /// einem Fenster. Bewusst eng gefasst — ausgeschlossen ist nur die nicht
    /// aufloesbare Vorfahrensuche, nicht jede Meldung mit "RelativeSource".
    /// </summary>
    private const string NichtBewertbar =
        "Cannot find source for binding with reference 'RelativeSource FindAncestor, "
        + "AncestorType='System.Windows.Window'";

    [Fact]
    public async Task Alle_Bedienelemente_ErzeugenKeineBindungsfehler()
    {
        Assert.Null(System.Windows.Application.Current);

        var ergebnis = await WpfIsolatedTestProcess.RunAsync(
            ChildTestName,
            TimeSpan.FromSeconds(120));

        Assert.Null(System.Windows.Application.Current);
        Assert.False(ergebnis.TimedOut, ergebnis.DescribeFailure());
        Assert.True(ergebnis.ExitCode == 0, ergebnis.DescribeFailure());
        Assert.True(ergebnis.ChildScenarioCompleted, ergebnis.DescribeFailure());
    }

    [IsolatedWpfFact]
    public void Kindprozess_erzeugt_alle_Bedienelemente()
    {
        StaTestRunner.Run(() =>
        {
            var app = new App { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            app.InitializeComponent();

            var fehler = new List<string>();
            PresentationTraceSources.Refresh();
            var zuhoerer = new SammelZuhoerer(fehler);
            PresentationTraceSources.DataBindingSource.Listeners.Add(zuhoerer);
            PresentationTraceSources.DataBindingSource.Switch.Level =
                SourceLevels.Error | SourceLevels.Warning;

            try
            {
                var typen = typeof(App).Assembly.GetTypes()
                    .Where(t => !t.IsAbstract && t.IsPublic)
                    .Where(t => typeof(UserControl).IsAssignableFrom(t)
                                || typeof(Page).IsAssignableFrom(t))
                    .Where(t => t.GetConstructor(Type.EmptyTypes) is not null)
                    .OrderBy(t => t.Name, StringComparer.Ordinal)
                    .ToList();

                Assert.True(typen.Count >= 30,
                    $"Nur {typen.Count} Bedienelemente gefunden - der Rauchtest greift ins Leere.");

                var befunde = new List<string>();
                foreach (var typ in typen)
                {
                    fehler.Clear();
                    try
                    {
                        var element = (FrameworkElement)Activator.CreateInstance(typ)!;
                        element.Measure(new Size(1200, 800));
                        element.Arrange(new Rect(0, 0, 1200, 800));
                        element.UpdateLayout();
                    }
                    catch (Exception ex)
                    {
                        var kern = ex.InnerException ?? ex;
                        befunde.Add($"{typ.Name}: Erzeugung scheitert ({kern.GetType().Name}: {kern.Message})");
                        continue;
                    }

                    befunde.AddRange(fehler
                        .Where(f => !f.Contains(NichtBewertbar, StringComparison.Ordinal))
                        .Select(f => $"{typ.Name}: {f}"));
                }

                Assert.True(befunde.Count == 0,
                    $"{befunde.Count} Bindungsfehler in der Oberflaeche:{Environment.NewLine}  "
                    + string.Join(Environment.NewLine + "  ", befunde));

                // Beleg fuer den Elternprozess: Nur so ist ein gruener Kindlauf
                // von einem gar nicht ausgefuehrten Szenario zu unterscheiden.
                WpfIsolatedTestProcess.MarkChildScenarioCompleted();
            }
            finally
            {
                PresentationTraceSources.DataBindingSource.Listeners.Remove(zuhoerer);
            }
        });
    }

    private sealed class SammelZuhoerer(List<string> ziel) : TraceListener
    {
        public override void Write(string? nachricht) { }

        public override void WriteLine(string? nachricht)
        {
            if (!string.IsNullOrWhiteSpace(nachricht))
                ziel.Add(nachricht);
        }
    }
}
