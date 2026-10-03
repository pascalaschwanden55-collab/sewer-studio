using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using AuswertungPro.Next.UI.Helpers;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerKeyboardPresenterTests
{
    [Fact]
    public void Aufbau_fuehrt_keine_Quellen_oder_Aktionen_aus()
        => Run(c =>
        {
            Assert.Empty(c.Calls);
            Assert.Equal(0, c.ControllerCreations);
            Assert.Equal(Visibility.Collapsed, c.Overlay.Visibility);
        });

    [Fact]
    public void Textfokus_sperrt_Schriftzeichen_und_ESC_vor_dem_Overlay()
        => Run(c =>
        {
            c.TextFocused = true;
            c.Overlay.Visibility = Visibility.Visible;
            foreach (var key in new[] { Key.Space, Key.OemQuestion, Key.Escape, Key.D })
            {
                var e = c.Key(key);
                c.Driver.HandleKey(e);
                Assert.False(e.Handled);
                Assert.Equal(Visibility.Visible, c.Overlay.Visibility);
            }
            Assert.Equal(["focus", "focus", "focus", "focus"], c.Calls);
            Assert.Equal(0, c.ControllerCreations);
        });

    [Fact]
    public void F1_zeigt_und_schliesst_Hilfe_trotz_Textfokus_ohne_Aktionsowner()
        => Run(c =>
        {
            c.TextFocused = true;
            var open = c.Key(Key.F1);
            c.Driver.HandleKey(open);
            Assert.True(open.Handled);
            Assert.Equal(Visibility.Visible, c.Overlay.Visibility);
            var close = c.Key(Key.F1);
            c.Driver.HandleKey(close);
            Assert.True(close.Handled);
            Assert.Equal(Visibility.Collapsed, c.Overlay.Visibility);
            Assert.Equal(["focus", "focus"], c.Calls);
            Assert.Equal(0, c.ControllerCreations);
        });

    [Fact]
    public void Sichtbare_Hilfe_blockiert_Wiedergabe_und_ESC_schliesst_nur_Hilfe()
        => Run(c =>
        {
            c.Overlay.Visibility = Visibility.Visible;
            var blocked = c.Key(Key.Space);
            c.Driver.HandleKey(blocked);
            Assert.False(blocked.Handled);
            Assert.Equal(Visibility.Visible, c.Overlay.Visibility);
            var escape = c.Key(Key.Escape);
            c.Driver.HandleKey(escape);
            Assert.True(escape.Handled);
            Assert.Equal(Visibility.Collapsed, c.Overlay.Visibility);
            Assert.Equal(["focus", "focus"], c.Calls);
            Assert.Equal(0, c.ControllerCreations);
        });

    [Fact]
    public void Auch_unbekannte_Taste_liest_Actions_und_lazyer_Owner_behaelt_erste_Bindung()
        => Run(c =>
        {
            var unknown = c.Key(Key.A);
            c.Driver.HandleKey(unknown);
            Assert.False(unknown.Handled);
            Assert.Equal(["focus", "actions:first", "create", "can-cancel"], c.Calls);
            c.Calls.Clear();
            c.ActionTag = "later";
            var space = c.Key(Key.Space);
            c.DuringAction = () => Assert.False(space.Handled);
            c.Driver.HandleKey(space);
            Assert.True(space.Handled);
            Assert.Equal(["focus", "actions:later", "can-cancel", "first:toggle"], c.Calls);
            Assert.Equal(1, c.ControllerCreations);
        });

    [Fact]
    public void ESC_liest_Abbruchfaehigkeit_nach_Actions_und_markiert_erst_nach_Aktion()
        => Run(c =>
        {
            c.CanCancel = false;
            var first = c.Key(Key.Escape);
            c.Driver.HandleKey(first);
            Assert.False(first.Handled);
            c.Calls.Clear();
            var second = c.Key(Key.Escape);
            c.AfterActionsRead = () => c.CanCancel = true;
            c.DuringAction = () => Assert.False(second.Handled);
            c.Driver.HandleKey(second);
            Assert.True(second.Handled);
            Assert.Equal(["focus", "actions:first", "can-cancel", "first:cancel"], c.Calls);
            Assert.Equal(1, c.ControllerCreations);
        });

    [Fact]
    public void Fehlgeschlagene_Aktion_reicht_Ausnahme_weiter_und_markiert_nicht_behandelt()
        => Run(c =>
        {
            var e = c.Key(Key.Space);
            var expected = new InvalidOperationException("Kontrollierter Wiedergabefehler");
            c.DuringAction = () => throw expected;

            var actual = Assert.Throws<InvalidOperationException>(() => c.Driver.HandleKey(e));

            Assert.Same(expected, actual);
            Assert.False(e.Handled);
            Assert.Equal(["focus", "actions:first", "create", "can-cancel"], c.Calls);
            Assert.Equal(1, c.ControllerCreations);
        });

    [Fact]
    public void Show_und_Hide_markieren_vor_der_sichtbaren_Aenderung()
        => Run(c =>
        {
            var show = new RoutedEventArgs(Button.ClickEvent);
            var hide = new RoutedEventArgs(Button.ClickEvent);
            var active = show;
            var observed = new List<Visibility>();
            var descriptor = DependencyPropertyDescriptor.FromProperty(UIElement.VisibilityProperty, typeof(Border));
            Assert.NotNull(descriptor);
            EventHandler changed = (_, _) =>
            {
                Assert.True(active.Handled);
                observed.Add(c.Overlay.Visibility);
            };
            descriptor.AddValueChanged(c.Overlay, changed);
            try
            {
                c.Driver.Show(show);
                active = hide;
                c.Driver.Hide(hide);
            }
            finally { descriptor.RemoveValueChanged(c.Overlay, changed); }
            Assert.Equal([Visibility.Visible, Visibility.Collapsed], observed);
            Assert.True(show.Handled);
            Assert.True(hide.Handled);
            Assert.Empty(c.Calls);
        });

    [Fact]
    public void Ohne_Fokusport_bleibt_der_bestehende_Defaultguard_aktiv()
        => Run(c =>
        {
            Assert.False(KeyboardTextInputFocusGuard.IsTextInputFocused());
            var driver = new PlayerKeyboardPresenter(c.OverlayController, c.Owner, c.ReadActions, c.ReadCanCancel);
            var e = c.Key(Key.Space);
            driver.HandleKey(e);
            Assert.True(e.Handled);
            Assert.Equal(["actions:first", "create", "can-cancel", "first:toggle"], c.Calls);
        });

    private sealed class Context : IDisposable
    {
        internal readonly List<string> Calls = [];
        internal readonly Border Overlay = new() { Visibility = Visibility.Collapsed };
        internal readonly HwndSource Source = new(new HwndSourceParameters("keyboard-presenter-test"));
        internal readonly PlayerShortcutOverlayController OverlayController;
        internal readonly PlayerKeyboardActionControllerOwner Owner;
        internal readonly PlayerKeyboardPresenter Driver;
        internal bool TextFocused, CanCancel;
        internal int ControllerCreations;
        internal string ActionTag = "first";
        internal Action? AfterActionsRead, DuringAction;

        internal Context()
        {
            OverlayController = new(Overlay);
            Owner = new(actions =>
            {
                ControllerCreations++;
                Calls.Add("create");
                return PlayerKeyboardActionControllerFactory.Create(actions);
            });
            Driver = new(OverlayController, Owner, ReadActions, ReadCanCancel,
                () => { Calls.Add("focus"); return TextFocused; });
        }

        internal PlayerKeyboardActionControllerFactoryActions ReadActions()
        {
            var tag = ActionTag;
            Calls.Add("actions:" + tag);
            AfterActionsRead?.Invoke();
            void Execute(string name) { DuringAction?.Invoke(); Calls.Add(tag + ":" + name); }
            return new(() => Execute("cancel"), () => Execute("toggle"), () => Execute("stop"),
                pause => Execute("pause:" + pause), () => Execute("play"),
                speed => Execute("speed:" + speed), seconds => Execute("jump:" + seconds),
                () => Execute("detect"), () => Execute("mark"));
        }
        internal bool ReadCanCancel() { Calls.Add("can-cancel"); return CanCancel; }
        internal KeyEventArgs Key(Key key) => new(Keyboard.PrimaryDevice, Source, 0, key)
        { RoutedEvent = Keyboard.PreviewKeyDownEvent };
        public void Dispose() => Source.Dispose();
    }

    private static void Run(Action<Context> test)
        => StaTestRunner.Run(() => { using var c = new Context(); test(c); });
}
