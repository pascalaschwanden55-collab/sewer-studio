using System.Runtime.ExceptionServices;
using System.Threading;
using Xunit;

namespace AuswertungPro.Next.UI.Tests;

public sealed class FluentIconTests
{
    [Fact]
    public void Glyph_uebernimmt_den_Wert_als_sichtbaren_Text()
    {
        RunOnSta(() =>
        {
            var icon = new FluentIcon { Glyph = "\uE74E" };

            Assert.Equal("\uE74E", icon.Text);
            Assert.Same(IconFonts.Default, icon.FontFamily);
        });
    }

    private static void RunOnSta(Action action)
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
            ExceptionDispatchInfo.Capture(exception).Throw();
    }
}
