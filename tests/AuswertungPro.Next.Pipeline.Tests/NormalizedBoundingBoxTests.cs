using System.Globalization;
using System.Threading;
using AuswertungPro.Next.Application.Ai;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

public sealed class NormalizedBoundingBoxTests
{
    [Fact]
    public void ToYoloLine_nutzt_Punkt_als_Dezimaltrennzeichen_auch_auf_deDE()
    {
        var bbox = new NormalizedBoundingBox
        {
            XCenter = 0.5,
            YCenter = 0.25,
            Width = 0.125,
            Height = 0.0625
        };

        var original = Thread.CurrentThread.CurrentCulture;
        try
        {
            // de-DE verwendet Komma als Dezimaltrennzeichen.
            // YOLO-Format verlangt Punkt — ToYoloLine muss InvariantCulture nutzen.
            Thread.CurrentThread.CurrentCulture = new CultureInfo("de-DE");
            var line = bbox.ToYoloLine(classId: 3);

            Assert.Equal("3 0.500000 0.250000 0.125000 0.062500", line);
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = original;
        }
    }
}
