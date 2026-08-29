using System.Windows.Input;
using AuswertungPro.Next.UI.Views;

namespace AuswertungPro.Next.UI.Tests;

public sealed class ProtocolObservationsEditTriggerPolicyTests
{
    [Fact]
    public void Enter_oeffnet_den_Bearbeitungsdialog()
    {
        Assert.True(ProtocolObservationsEditTriggerPolicy.OpensEditor(Key.Enter));
    }

    [Theory]
    [InlineData(Key.Down)]
    [InlineData(Key.Up)]
    [InlineData(Key.Escape)]
    [InlineData(Key.Space)]
    [InlineData(Key.Delete)]
    public void Andere_Tasten_oeffnen_nichts(Key key)
    {
        // Die Liste muss sich mit den Pfeiltasten durchgehen lassen, ohne dass
        // bei jeder Zeile der Dialog aufspringt.
        Assert.False(ProtocolObservationsEditTriggerPolicy.OpensEditor(key));
    }

    [Fact]
    public void Ohne_gewaehlte_Zeile_oeffnet_nichts()
    {
        Assert.False(ProtocolObservationsEditTriggerPolicy.CanOpenEditor(
            hasSelectedEntry: false, isOpeningDialog: false, isRefreshingEntries: false));
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void Waehrend_Dialog_oder_Neuaufbau_oeffnet_nichts(bool isOpeningDialog, bool isRefreshingEntries)
    {
        Assert.False(ProtocolObservationsEditTriggerPolicy.CanOpenEditor(
            hasSelectedEntry: true, isOpeningDialog, isRefreshingEntries));
    }

    [Fact]
    public void Mit_gewaehlter_Zeile_und_ruhigem_Fenster_darf_geoeffnet_werden()
    {
        Assert.True(ProtocolObservationsEditTriggerPolicy.CanOpenEditor(
            hasSelectedEntry: true, isOpeningDialog: false, isRefreshingEntries: false));
    }
}
