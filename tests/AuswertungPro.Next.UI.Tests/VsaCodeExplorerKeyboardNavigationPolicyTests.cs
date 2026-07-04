using System.Windows.Input;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Tests;

public sealed class VsaCodeExplorerKeyboardNavigationPolicyTests
{
    [Theory]
    [InlineData(true, 0)]
    [InlineData(false, 2)]
    public void Resolve_navigiert_mit_escape_zurueck_wenn_ergebnis_oder_unterebene_aktiv_ist(
        bool showResultPanel,
        int currentLevel)
    {
        var action = VsaCodeExplorerKeyboardNavigationPolicy.Resolve(
            Key.Escape,
            ModifierKeys.None,
            isTextBoxFocused: false,
            showResultPanel,
            currentLevel);

        Assert.Equal(VsaCodeExplorerKeyboardNavigationAction.NavigateBack, action);
    }

    [Fact]
    public void Resolve_ignoriert_escape_im_root_ohne_ergebnis()
    {
        var action = VsaCodeExplorerKeyboardNavigationPolicy.Resolve(
            Key.Escape,
            ModifierKeys.None,
            isTextBoxFocused: false,
            showResultPanel: false,
            currentLevel: 0);

        Assert.Null(action);
    }

    [Fact]
    public void Resolve_navigiert_mit_backspace_nur_ausserhalb_von_textfeldern_zurueck()
    {
        Assert.Equal(
            VsaCodeExplorerKeyboardNavigationAction.NavigateBack,
            VsaCodeExplorerKeyboardNavigationPolicy.Resolve(
                Key.Back,
                ModifierKeys.None,
                isTextBoxFocused: false,
                showResultPanel: false,
                currentLevel: 0));

        Assert.Null(
            VsaCodeExplorerKeyboardNavigationPolicy.Resolve(
                Key.Back,
                ModifierKeys.None,
                isTextBoxFocused: true,
                showResultPanel: true,
                currentLevel: 3));
    }

    [Fact]
    public void Resolve_uebernimmt_mit_control_s()
    {
        var action = VsaCodeExplorerKeyboardNavigationPolicy.Resolve(
            Key.S,
            ModifierKeys.Control,
            isTextBoxFocused: false,
            showResultPanel: false,
            currentLevel: 0);

        Assert.Equal(VsaCodeExplorerKeyboardNavigationAction.ApplyAndClose, action);
    }

    [Fact]
    public void Resolve_ignoriert_nicht_gemappte_tasten_und_falsche_modifier()
    {
        Assert.Null(
            VsaCodeExplorerKeyboardNavigationPolicy.Resolve(
                Key.S,
                ModifierKeys.None,
                isTextBoxFocused: false,
                showResultPanel: false,
                currentLevel: 0));

        Assert.Null(
            VsaCodeExplorerKeyboardNavigationPolicy.Resolve(
                Key.Enter,
                ModifierKeys.Control,
                isTextBoxFocused: false,
                showResultPanel: true,
                currentLevel: 1));
    }
}
