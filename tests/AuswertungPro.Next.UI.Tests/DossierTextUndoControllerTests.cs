using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

using AuswertungPro.Next.UI.Views.Windows;

using Xunit;

namespace AuswertungPro.Next.UI.Tests;

public sealed class DossierTextUndoControllerTests
{
    [Fact]
    public void Leiste_zeigt_klare_Pfeile_mit_Tastaturhinweisen()
    {
        RunOnSta(() =>
        {
            var host = new StackPanel();
            var controller = new DossierTextUndoController(host);
            var buttons = Nachfahren(controller.View).OfType<Button>().ToList();

            var undo = Assert.Single(buttons.Where(button =>
                button.Content is AuswertungPro.Next.UI.FluentIcon { Glyph: "\uE7A7" }));
            var redo = Assert.Single(buttons.Where(button =>
                button.Content is AuswertungPro.Next.UI.FluentIcon { Glyph: "\uE7A6" }));

            Assert.Same(ApplicationCommands.Undo, undo.Command);
            Assert.Same(ApplicationCommands.Redo, redo.Command);
            Assert.False(undo.Focusable);
            Assert.False(redo.Focusable);
            Assert.False(undo.IsEnabled);
            Assert.False(redo.IsEnabled);
            Assert.Equal("Rückgängig (Strg+Z)", undo.ToolTip);
            Assert.Equal("Wiederholen (Strg+Y)", redo.ToolTip);
        });
    }

    [Fact]
    public void Dynamisch_hinzugefuegtes_Textfeld_wird_zentral_als_Ziel_gemerkt()
    {
        RunOnSta(() =>
        {
            var host = new StackPanel();
            var controller = new DossierTextUndoController(host);
            host.Children.Add(controller.View);
            var editor = new RichTextBox();
            host.Children.Add(editor);

            var window = new Window
            {
                Content = host,
                Width = 320,
                Height = 200,
                ShowInTaskbar = false,
                WindowStyle = WindowStyle.None
            };

            try
            {
                window.Show();
                editor.Focus();
                PumpDispatcherFor(TimeSpan.FromMilliseconds(50));

                var buttons = Nachfahren(controller.View).OfType<Button>().ToList();
                Assert.All(buttons, button => Assert.Same(editor, button.CommandTarget));

                controller.Reset();
                Assert.All(buttons, button => Assert.Null(button.CommandTarget));
            }
            finally
            {
                window.Close();
            }
        });
    }

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
}
