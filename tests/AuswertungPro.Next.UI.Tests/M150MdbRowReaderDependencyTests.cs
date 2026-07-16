using System.Reflection;
using AuswertungPro.Next.Application.Diagnostics;
using AuswertungPro.Next.Infrastructure.Import.Xtf;
using AuswertungPro.Next.Infrastructure.Import.WinCan;
using Microsoft.Extensions.Logging;

namespace AuswertungPro.Next.UI.Tests;

public sealed class M150MdbRowReaderDependencyTests
{
    [Fact]
    public void ServiceProvider_registriert_und_uebergibt_den_M150MdbLeser()
    {
        using var loggerFactory = LoggerFactory.Create(_ => { });
        var services = new ServiceProvider(
            new AppSettings { EnableRestorePoints = false },
            new DiagnosticsOptions(),
            loggerFactory.CreateLogger("test"),
            loggerFactory);

        Assert.Same(
            services.M150MdbRows,
            services.GetService(typeof(IM150MdbRowReader)));

        var winCan = Assert.IsType<WinCanDbImportService>(services.WinCanImport);
        var rowReader = typeof(WinCanDbImportService)
            .GetField("_m150MdbRows", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(winCan);
        var xtfImport = typeof(WinCanDbImportService)
            .GetField("_xtfImport", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(winCan);

        Assert.Same(services.M150MdbRows, rowReader);
        Assert.Same(services.XtfImport, xtfImport);
    }

    [Fact]
    public void Statische_M150MdbFassade_ist_unveraenderbar()
    {
        var before = M150MdbRowReader.Current;
        var use = typeof(M150MdbRowReader).GetMethod(nameof(M150MdbRowReader.Use));

        var error = Assert.Throws<TargetInvocationException>(
            () => use!.Invoke(null, [new FakeMdbRowReader()]));

        Assert.IsType<NotSupportedException>(error.InnerException);
        Assert.Same(before, M150MdbRowReader.Current);
    }

    private sealed class FakeMdbRowReader : IM150MdbRowReader
    {
        public bool TryReadRows(
            string mdbPath,
            out List<Dictionary<string, string>> rows,
            out string? error)
        {
            rows = [];
            error = null;
            return true;
        }
    }
}
