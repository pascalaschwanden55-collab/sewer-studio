using System.IO;
using System.Windows.Input;
using AuswertungPro.Next.UI.Views.Windows;

namespace AuswertungPro.Next.UI.Tests;

public sealed class TrainingStudioKeyboardShortcutPolicyTests
{
    [Theory]
    [InlineData(Key.A, TrainingStudioKeyboardShortcutAction.Accept)]
    [InlineData(Key.K, TrainingStudioKeyboardShortcutAction.Correct)]
    [InlineData(Key.V, TrainingStudioKeyboardShortcutAction.Discard)]
    [InlineData(Key.Right, TrainingStudioKeyboardShortcutAction.NextItem)]
    [InlineData(Key.Left, TrainingStudioKeyboardShortcutAction.PreviousItem)]
    public void Resolve_bewahrt_die_bisherigen_Kuerzel_ausserhalb_von_Textfeldern(
        Key key,
        TrainingStudioKeyboardShortcutAction expected)
    {
        var decision = TrainingStudioKeyboardShortcutPolicy.Resolve(
            key,
            ModifierKeys.None,
            isTextInputFocused: false,
            isBusy: false);

        Assert.True(decision.ShouldHandle);
        Assert.Equal(expected, decision.Action);
    }

    [Theory]
    [InlineData(Key.A)]
    [InlineData(Key.K)]
    [InlineData(Key.V)]
    [InlineData(Key.Right)]
    [InlineData(Key.Left)]
    public void Resolve_laesst_Eingaben_im_Textfeld_unberuehrt(Key key)
    {
        var decision = TrainingStudioKeyboardShortcutPolicy.Resolve(
            key,
            ModifierKeys.None,
            isTextInputFocused: true,
            isBusy: false);

        Assert.False(decision.ShouldHandle);
        Assert.Null(decision.Action);
    }

    [Theory]
    [InlineData(Key.A, ModifierKeys.Control)]
    [InlineData(Key.V, ModifierKeys.Control)]
    [InlineData(Key.K, ModifierKeys.Shift)]
    [InlineData(Key.Right, ModifierKeys.Alt)]
    public void Resolve_loest_nur_unmodifizierte_Kuerzel_aus(Key key, ModifierKeys modifiers)
    {
        var decision = TrainingStudioKeyboardShortcutPolicy.Resolve(
            key,
            modifiers,
            isTextInputFocused: false,
            isBusy: false);

        Assert.False(decision.ShouldHandle);
        Assert.Null(decision.Action);
    }

    [Fact]
    public void Resolve_blockiert_ein_Kuerzel_waehrend_des_Pdf_Imports_ohne_Aktion()
    {
        var decision = TrainingStudioKeyboardShortcutPolicy.Resolve(
            Key.A,
            ModifierKeys.None,
            isTextInputFocused: false,
            isBusy: true);

        Assert.True(decision.ShouldHandle);
        Assert.Null(decision.Action);
    }

    [Theory]
    [InlineData(Key.Enter)]
    [InlineData(Key.Escape)]
    public void Resolve_laesst_lokale_Eingabetasten_unberuehrt(Key key)
    {
        var decision = TrainingStudioKeyboardShortcutPolicy.Resolve(
            key,
            ModifierKeys.None,
            isTextInputFocused: false,
            isBusy: false);

        Assert.False(decision.ShouldHandle);
        Assert.Null(decision.Action);
    }

    [Fact]
    public void Fensterverdrahtung_verwendet_die_sichere_Policy_statt_Window_InputBindings()
    {
        var root = TestRepoPaths.FindRepoRoot();
        var windowsRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI", "Views", "Windows");
        var xaml = File.ReadAllText(Path.Combine(windowsRoot, "TrainingStudioWindow.xaml"));
        var code = File.ReadAllText(Path.Combine(windowsRoot, "TrainingStudioWindow.xaml.cs"));

        Assert.DoesNotContain("<Window.InputBindings>", xaml, StringComparison.Ordinal);
        Assert.Contains("TrainingStudioKeyboardShortcutPolicy.Resolve", code, StringComparison.Ordinal);
        Assert.Contains("KeyboardTextInputFocusGuard.IsTextInputFocused()", code, StringComparison.Ordinal);
        Assert.Contains("command?.CanExecute(null) == true", code, StringComparison.Ordinal);
    }
}
