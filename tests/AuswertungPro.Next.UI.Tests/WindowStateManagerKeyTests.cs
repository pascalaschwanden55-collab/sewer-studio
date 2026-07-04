using System;
using System.Threading;
using System.Windows;
using AuswertungPro.Next.UI.Services;
using Xunit;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// P5: getrennte StateKeys, damit abgekoppelte Fenster gleichen Typs (generisches Window)
/// sich nicht denselben Positions-Eintrag teilen.
/// </summary>
public sealed class WindowStateManagerKeyTests
{
    [Fact]
    public void ResolveKey_uses_explicit_state_key_over_type_name()
    {
        RunOnSta(() =>
        {
            var a = new Window();
            var b = new Window(); // gleicher Typ "Window"

            var keyA = WindowStateManager.ResolveKey(a, "PhotoGalleryWindow");
            var keyB = WindowStateManager.ResolveKey(b, "SystemMonitorWindow");

            Assert.NotEqual(keyA, keyB);
            Assert.Equal("PhotoGalleryWindow", keyA);
        });
    }

    [Fact]
    public void ResolveKey_falls_back_to_type_name_without_state_key()
    {
        RunOnSta(() =>
        {
            var window = new Window();
            Assert.Equal("Window", WindowStateManager.ResolveKey(window, null));
            Assert.Equal("Window", WindowStateManager.ResolveKey(window, "   "));
        });
    }

    private static void RunOnSta(Action action)
    {
        Exception? exception = null;
        var thread = new Thread(() =>
        {
            try { action(); }
            catch (Exception ex) { exception = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (exception is not null)
            throw exception;
    }
}
