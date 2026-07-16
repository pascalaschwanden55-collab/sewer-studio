using System.Reflection;
using System.Xml.Linq;
using AuswertungPro.Next.Application.Diagnostics;
using AuswertungPro.Next.Infrastructure.Import.Xtf;
using Microsoft.Extensions.Logging;

namespace AuswertungPro.Next.UI.Tests;

public sealed class M150SourceFileReaderDependencyTests
{
    [Fact]
    public void ServiceProvider_registriert_und_uebergibt_den_M150QuelldateiLeser()
    {
        using var loggerFactory = LoggerFactory.Create(_ => { });
        var services = new ServiceProvider(
            new AppSettings { EnableRestorePoints = false },
            new DiagnosticsOptions(),
            loggerFactory.CreateLogger("test"),
            loggerFactory);

        Assert.Same(
            services.M150SourceFiles,
            services.GetService(typeof(IM150SourceFileReader)));

        var adapter = Assert.IsType<XtfImportServiceAdapter>(services.XtfImport);
        var legacy = typeof(XtfImportServiceAdapter)
            .GetField("_svc", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(adapter);
        var sourceReader = typeof(LegacyXtfImportService)
            .GetField("_m150SourceFiles", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(legacy);

        Assert.Same(services.M150SourceFiles, sourceReader);
    }

    [Fact]
    public void Statische_M150QuelldateiFassade_ist_unveraenderbar()
    {
        var before = M150SourceFileReader.Current;
        var use = typeof(M150SourceFileReader).GetMethod(nameof(M150SourceFileReader.Use));

        var error = Assert.Throws<TargetInvocationException>(
            () => use!.Invoke(null, [new FakeSourceReader()]));

        Assert.IsType<NotSupportedException>(error.InnerException);
        Assert.Same(before, M150SourceFileReader.Current);
    }

    private sealed class FakeSourceReader : IM150SourceFileReader
    {
        public XDocument LoadXml(string path) => throw new NotSupportedException();

        public string ReadUtf8Text(string path) => throw new NotSupportedException();
    }
}
