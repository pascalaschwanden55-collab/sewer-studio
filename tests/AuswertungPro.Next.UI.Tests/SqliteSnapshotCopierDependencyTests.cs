using System.Reflection;
using AuswertungPro.Next.Application.Backup;
using AuswertungPro.Next.Application.Diagnostics;
using AuswertungPro.Next.Infrastructure.Backup;
using Microsoft.Extensions.Logging;

namespace AuswertungPro.Next.UI.Tests;

public sealed class SqliteSnapshotCopierDependencyTests
{
    [Fact]
    public void ServiceProvider_und_Vollsicherung_verwenden_dieselbe_Instanz()
    {
        using var loggerFactory = LoggerFactory.Create(_ => { });
        var services = new ServiceProvider(
            new AppSettings { EnableRestorePoints = false },
            new DiagnosticsOptions(),
            loggerFactory.CreateLogger("test"),
            loggerFactory);
        var field = typeof(FullBackupService).GetField(
            "_sqliteSnapshots",
            BindingFlags.Instance | BindingFlags.NonPublic);
        var knowledgeBackupField = typeof(AuswertungPro.Next.UI.Services.KnowledgeBackupTransferService).GetField(
            "_sqliteSnapshots",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.IsType<SqliteSnapshotCopyService>(services.SqliteSnapshots);
        Assert.Same(services.SqliteSnapshots, services.GetService(typeof(ISqliteSnapshotCopier)));
        Assert.NotNull(field);
        Assert.Same(services.SqliteSnapshots, field!.GetValue(services.FullBackup));
        Assert.NotNull(knowledgeBackupField);
        Assert.Same(services.SqliteSnapshots, knowledgeBackupField!.GetValue(services.KnowledgeBackup));
    }
}
