using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

using AuswertungPro.Next.Application.Dossiers;
using AuswertungPro.Next.Application.Dossiers.Preview;
using AuswertungPro.Next.Domain.Models.Dossiers;
using AuswertungPro.Next.UI.Views.Windows;

using Xunit;

namespace AuswertungPro.Next.UI.Tests;

public sealed class DossierRevisionRowFocusTests
{
    [Fact]
    public void Leere_sichtbare_Aenderungszeile_hat_sofort_vier_Eingaben()
    {
        RunOnSta(() =>
        {
            var host = new StackPanel();
            var dossier = new DossierDefinition();
            var panel = CreatePanel(host, dossier);

            panel.Baue(ChangePage(), [ChangeField()]);

            Assert.Single(dossier.Changes);
            Assert.True(panel.Kennt(DossierPreviewTarget.Row("Aenderungen", 0)));
            Assert.True(panel.Kennt(DossierPreviewTarget.RowCell(
                "Aenderungen", 0, "Version")));
            Assert.True(panel.Kennt(DossierPreviewTarget.RowCell(
                "Aenderungen", 0, "Datum")));
            Assert.True(panel.Kennt(DossierPreviewTarget.RowCell(
                "Aenderungen", 0, "Visum")));
            Assert.True(panel.Kennt(DossierPreviewTarget.RowCell(
                "Aenderungen", 0, "Aenderung")));
            Assert.Equal(4, Nachfahren(host).OfType<RichTextBox>().Count());
            var entfernen = Assert.Single(Nachfahren(host)
                .OfType<Button>()
                .Where(button => string.Equals(
                    button.Content as string,
                    "✕",
                    StringComparison.Ordinal)));
            Assert.False(entfernen.IsEnabled);

            Nachfahren(host).OfType<RichTextBox>().First().AppendText("1");
            Assert.True(entfernen.IsEnabled);

            entfernen.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            var ersatz = Assert.Single(dossier.Changes);
            Assert.False(DossierChangeRows.HasContent(ersatz));
            Assert.Equal(1, DossierChangeRows.RemoveEmpty(dossier));
            Assert.Empty(dossier.Changes);
        });
    }

    [Fact]
    public void Sprung_in_Aenderungszelle_zeigt_alle_vier_Eingaben_der_Zeile_und_fokussiert_die_Zelle()
    {
        RunOnSta(() =>
        {
            var host = new StackPanel();
            var dossier = new DossierDefinition
            {
                Changes =
                [
                    new DossierChangeRow { Version = "1" },
                    new DossierChangeRow { Version = "2" }
                ]
            };
            var panel = CreatePanel(host, dossier);

            panel.Baue(ChangePage(), [ChangeField()]);
            var editors = Nachfahren(host).OfType<RichTextBox>().ToList();
            Assert.Equal(8, editors.Count);

            var window = new Window
            {
                Content = host,
                Width = 420,
                Height = 700,
                ShowInTaskbar = false,
                WindowStyle = WindowStyle.None
            };

            try
            {
                window.Show();

                Assert.True(panel.SpringeZu(DossierPreviewTarget.RowCell(
                    "Aenderungen", 0, "Datum")));
                PumpDispatcherFor(TimeSpan.FromMilliseconds(300));

                Assert.All(editors.Take(4), editor =>
                    Assert.Equal(Visibility.Visible, editor.Visibility));
                Assert.All(editors.Skip(4), editor =>
                    Assert.Equal(Visibility.Collapsed, editor.Visibility));
                Assert.True(editors[1].IsKeyboardFocused);

                var alleFelder = Assert.Single(Nachfahren(host)
                    .OfType<Button>()
                    .Where(button => string.Equals(
                        button.Content as string,
                        "Alle Felder anzeigen",
                        StringComparison.Ordinal)));
                var rueckgaengig = Assert.Single(Nachfahren(host)
                    .OfType<Button>()
                    .Where(button => string.Equals(
                        button.Content as string,
                        "↶",
                        StringComparison.Ordinal)));
                Assert.Equal(Visibility.Visible, alleFelder.Visibility);
                Assert.Equal(Visibility.Visible, rueckgaengig.Visibility);

                alleFelder.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

                Assert.Equal(Visibility.Collapsed, alleFelder.Visibility);
                Assert.Equal(Visibility.Visible, rueckgaengig.Visibility);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void Rueckgaengig_und_wiederholen_stellen_geloeschten_Text_im_Dossier_wieder_her()
    {
        RunOnSta(() =>
        {
            var host = new StackPanel();
            var dossier = new DossierDefinition
            {
                Changes = [new DossierChangeRow { Version = "1" }]
            };
            var panel = CreatePanel(host, dossier);
            panel.Baue(ChangePage(), [ChangeField()]);

            var window = new Window
            {
                Content = host,
                Width = 420,
                Height = 700,
                ShowInTaskbar = false,
                WindowStyle = WindowStyle.None
            };

            try
            {
                window.Show();
                var editor = Nachfahren(host).OfType<RichTextBox>().First();
                editor.Focus();
                PumpDispatcherFor(TimeSpan.FromMilliseconds(50));

                editor.SelectAll();
                editor.Selection.Text = string.Empty;
                PumpDispatcherFor(TimeSpan.FromMilliseconds(50));
                Assert.Equal(string.Empty, dossier.Changes[0].Version);

                var undo = Assert.Single(Nachfahren(host)
                    .OfType<Button>()
                    .Where(button => string.Equals(
                        button.Content as string,
                        "↶",
                        StringComparison.Ordinal)));
                var undoCommand = Assert.IsType<RoutedUICommand>(undo.Command);
                Assert.Same(editor, undo.CommandTarget);
                Assert.True(undoCommand.CanExecute(null, undo.CommandTarget));

                undoCommand.Execute(null, undo.CommandTarget);
                PumpDispatcherFor(TimeSpan.FromMilliseconds(50));
                Assert.Equal("1", dossier.Changes[0].Version);

                var redo = Assert.Single(Nachfahren(host)
                    .OfType<Button>()
                    .Where(button => string.Equals(
                        button.Content as string,
                        "↷",
                        StringComparison.Ordinal)));
                var redoCommand = Assert.IsType<RoutedUICommand>(redo.Command);
                Assert.Same(editor, redo.CommandTarget);
                Assert.True(redoCommand.CanExecute(null, redo.CommandTarget));

                redoCommand.Execute(null, redo.CommandTarget);
                PumpDispatcherFor(TimeSpan.FromMilliseconds(50));
                Assert.Equal(string.Empty, dossier.Changes[0].Version);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void Neuaufbau_verwendet_die_Textleiste_erneut_und_verwirft_das_alte_Ziel()
    {
        RunOnSta(() =>
        {
            var host = new StackPanel();
            var dossier = new DossierDefinition
            {
                Changes = [new DossierChangeRow { Version = "1" }]
            };
            var panel = CreatePanel(host, dossier);
            panel.Baue(ChangePage(), [ChangeField()]);

            var window = new Window
            {
                Content = host,
                Width = 420,
                Height = 700,
                ShowInTaskbar = false,
                WindowStyle = WindowStyle.None
            };

            try
            {
                window.Show();
                var altesFeld = Nachfahren(host).OfType<RichTextBox>().First();
                altesFeld.Focus();
                PumpDispatcherFor(TimeSpan.FromMilliseconds(50));

                var alterKnopf = Assert.Single(Nachfahren(host)
                    .OfType<Button>()
                    .Where(button => string.Equals(
                        button.Content as string,
                        "↶",
                        StringComparison.Ordinal)));
                Assert.Same(altesFeld, alterKnopf.CommandTarget);

                panel.Baue(ChangePage(), [ChangeField()]);

                var neuerKnopf = Assert.Single(Nachfahren(host)
                    .OfType<Button>()
                    .Where(button => string.Equals(
                        button.Content as string,
                        "↶",
                        StringComparison.Ordinal)));
                Assert.Same(alterKnopf, neuerKnopf);
                Assert.Null(neuerKnopf.CommandTarget);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void Sprung_in_leere_Themenbemerkung_fokussiert_das_Bemerkungsfeld()
    {
        RunOnSta(() =>
        {
            var host = new StackPanel();
            var area = new DossierAreaSettings
            {
                Topics =
                [
                    new DossierTopicRow
                    {
                        Title = "Ausfuehrungstermin",
                        Text = string.Empty
                    }
                ]
            };
            var panel = CreatePanel(host, new DossierDefinition(), area);

            panel.Baue(TopicPage(), [TopicField()]);
            var editors = Nachfahren(host).OfType<RichTextBox>().ToList();
            Assert.Equal(2, editors.Count);

            var window = new Window
            {
                Content = host,
                Width = 420,
                Height = 700,
                ShowInTaskbar = false,
                WindowStyle = WindowStyle.None
            };

            try
            {
                window.Show();

                Assert.True(panel.SpringeZu(DossierPreviewTarget.RowCell(
                    "Themen", 0, "Text")));
                PumpDispatcherFor(TimeSpan.FromMilliseconds(300));

                Assert.All(editors, editor =>
                    Assert.Equal(Visibility.Visible, editor.Visibility));
                Assert.True(editors[1].IsKeyboardFocused);
            }
            finally
            {
                window.Close();
            }
        });
    }

    private static DossierPreviewFieldPanel CreatePanel(
        Panel host,
        DossierDefinition dossier,
        DossierAreaSettings? area = null)
        => new(
            host,
            area ?? new DossierAreaSettings(),
            dossier,
            System.IO.Path.GetTempPath(),
            new DossierPreviewDocument([]),
            new PlanImageConverterStub(),
            new PlanImageAdjusterStub(),
            () => new Dictionary<string, string>(),
            () => { },
            _ => { },
            (_, _) => { },
            _ => Brushes.Black,
            _ => { },
            () => new Window());

    private static DossierPreviewField ChangeField()
        => new(
            "Aenderungen",
            "Änderungswesen",
            DossierPreviewFieldKind.Rows,
            () => string.Empty,
            null);

    private static DossierPreviewPage ChangePage()
        => new(
            1,
            "Deckblatt",
            new DossierPreviewGeometry(794, 1123, DossierPreviewEdges.Zero),
            [],
            ["Aenderungen"]);

    private static DossierPreviewField TopicField()
        => new(
            "Themen",
            "Themen der Informationstabelle",
            DossierPreviewFieldKind.Rows,
            () => string.Empty,
            null);

    private static DossierPreviewPage TopicPage()
        => new(
            3,
            "Informationen Sanierung",
            new DossierPreviewGeometry(794, 1123, DossierPreviewEdges.Zero),
            [],
            ["Themen"]);

    private static IEnumerable<DependencyObject> Nachfahren(DependencyObject wurzel)
    {
        foreach (var kind in LogicalTreeHelper.GetChildren(wurzel)
                     .OfType<DependencyObject>())
        {
            yield return kind;
            foreach (var nachfahr in Nachfahren(kind))
                yield return nachfahr;
        }
    }

    private static void PumpDispatcherFor(TimeSpan duration)
    {
        var frame = new System.Windows.Threading.DispatcherFrame();
        var timer = new System.Windows.Threading.DispatcherTimer { Interval = duration };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            frame.Continue = false;
        };
        timer.Start();
        System.Windows.Threading.Dispatcher.PushFrame(frame);
    }

    private static void RunOnSta(Action test)
    {
        ExceptionDispatchInfo? fehler = null;
        var thread = new Thread(() =>
        {
            try
            {
                test();
            }
            catch (Exception ex)
            {
                fehler = ExceptionDispatchInfo.Capture(ex);
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        fehler?.Throw();
    }

    private sealed class PlanImageConverterStub : IPlanImageConverter
    {
        public bool NeedsConversion(string? path) => false;

        public Task<PlanImageResult> ConvertAsync(
            string sourcePath,
            string targetFolder,
            CancellationToken ct = default)
            => Task.FromResult(PlanImageResult.Failed("Im Test nicht verwendet."));
    }

    private sealed class PlanImageAdjusterStub : IPlanImageAdjuster
    {
        public PlanImageResult Rotate(string? imagePath, string targetFolder, int degrees)
            => PlanImageResult.Failed("Im Test nicht verwendet.");

        public PlanImageResult Crop(
            string? imagePath,
            string targetFolder,
            int x,
            int y,
            int width,
            int height)
            => PlanImageResult.Failed("Im Test nicht verwendet.");
    }
}
