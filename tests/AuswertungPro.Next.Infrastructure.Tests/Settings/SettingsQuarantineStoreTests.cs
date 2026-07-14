using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Infrastructure.Settings;

namespace AuswertungPro.Next.Infrastructure.Tests.Settings;

public sealed class SettingsQuarantineStoreTests : IDisposable
{
    private readonly string _tempDirectory = Path.Combine(
        Path.GetTempPath(),
        $"SettingsQuarantineStoreTests_{Guid.NewGuid():N}");

    [Fact]
    public void BuildQuarantinePath_PreservesExistingNameFormat()
    {
        var service = new SettingsQuarantineStore();
        var utc = new DateTime(2026, 6, 21, 14, 30, 55, 123, DateTimeKind.Utc);

        var result = service.BuildQuarantinePath(@"C:\app", utc);

        Assert.Equal(
            @"C:\app\settings.corrupt-20260621-143055123.json",
            result);
    }

    [Fact]
    public void InstanceService_MovesCorruptSettingsThroughContract()
    {
        Directory.CreateDirectory(_tempDirectory);
        var settingsPath = Path.Combine(_tempDirectory, "settings.json");
        File.WriteAllText(settingsPath, "{kaputt");
        var messages = new List<string>();
        ISettingsQuarantineStore service = new SettingsQuarantineStore();

        service.TryMoveToQuarantine(
            settingsPath,
            _tempDirectory,
            new InvalidDataException("ungueltig"),
            (message, _) => messages.Add(message));

        Assert.False(File.Exists(settingsPath));
        var quarantineFile = Assert.Single(
            Directory.GetFiles(_tempDirectory, "settings.corrupt-*.json"));
        Assert.Equal("{kaputt", File.ReadAllText(quarantineFile));
        Assert.Single(messages);
    }

    [Fact]
    public void InstanceService_ReportsMissingSettingsWithoutThrowing()
    {
        var messages = new List<string>();
        ISettingsQuarantineStore service = new SettingsQuarantineStore();

        var exception = Record.Exception(() => service.TryMoveToQuarantine(
            Path.Combine(_tempDirectory, "missing.json"),
            _tempDirectory,
            new InvalidDataException("ungueltig"),
            (message, _) => messages.Add(message)));

        Assert.Null(exception);
        Assert.Single(messages);
        Assert.Contains("nicht gefunden", messages[0], StringComparison.OrdinalIgnoreCase);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDirectory))
                Directory.Delete(_tempDirectory, recursive: true);
        }
        catch
        {
            // Test-Aufraeumen ist best effort.
        }
    }
}
