using System.IO;

namespace AuswertungPro.Next.UI.Tests;

public sealed class DesignAuditDialogMigrationTests
{
    private static readonly string[] DialogHotspots =
    [
        Path.Combine("Views", "Pages", "DataPage.xaml.cs"),
        Path.Combine("Views", "Windows", "TrainingCenterWindow.xaml.cs"),
        Path.Combine("Views", "ProtocolObservationsWindow.xaml.cs")
    ];

    [Fact]
    public void Top_dialog_hotspots_use_dialog_service_instead_of_direct_message_boxes()
    {
        foreach (var relativePath in DialogHotspots)
        {
            var code = ReadUiFile(relativePath);

            Assert.DoesNotContain("MessageBox.Show", code);
            Assert.Contains(".Dialogs", code);
        }
    }

    [Fact]
    public void Dialog_service_keeps_warning_confirmations_available()
    {
        var interfaceCode = ReadUiFile(Path.Combine("Services", "IDialogService.cs"));
        var serviceCode = ReadUiFile(Path.Combine("Services", "DialogService.cs"));

        Assert.Contains("ConfirmWarn", interfaceCode);
        Assert.Contains("ConfirmWarn", serviceCode);
        Assert.Contains("MessageBoxButton.YesNo", serviceCode);
        Assert.Contains("MessageBoxImage.Warning", serviceCode);
        Assert.Contains("MessageBoxResult.No", serviceCode);
    }

    private static string ReadUiFile(string relativePath)
    {
        var path = Path.Combine(FindRepoRoot(), "src", "AuswertungPro.Next.UI", relativePath);
        return File.ReadAllText(path);
    }

    private static string FindRepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "AuswertungPro.sln")))
                return current.FullName;

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Repo root with AuswertungPro.sln was not found.");
    }
}
