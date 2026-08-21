using System.IO;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Alle schreibenden Aktionen der Import-Seite teilen sich Projektdateien und
/// Projektdaten. Die Laufzeittests pruefen die sichtbare Sperrwirkung; diese kleine
/// Verdrahtungspruefung verhindert, dass ein neuer Befehl den gemeinsamen Ablauf umgeht.
/// </summary>
public sealed class ImportSharedLockGuardTests
{
    private static string ImportPageSource()
        => string.Join(
            Environment.NewLine,
            File.ReadAllText(RepoFile(
                "src", "AuswertungPro.Next.UI", "ViewModels", "Pages", "ImportPageViewModel.cs")),
            File.ReadAllText(RepoFile(
                "src", "AuswertungPro.Next.UI", "ViewModels", "Pages",
                "ImportPageViewModel.SharedOperation.cs")));

    [Fact]
    public void Gemeinsamer_Ablauf_erwirbt_und_loest_die_Importsperre_im_finally()
    {
        var source = ImportPageSource();

        var start = source.IndexOf(
            "private async Task RunWithSharedImportLockAsync(Func<Task> operation)",
            StringComparison.Ordinal);
        Assert.True(start >= 0, "RunWithSharedImportLockAsync nicht gefunden.");
        var ende = source.IndexOf(
            "private void SetSharedImportInProgress",
            StringComparison.Ordinal);
        Assert.True(ende > start, "Ende von RunWithSharedImportLockAsync nicht gefunden.");

        var methode = source[start..ende];
        Assert.Contains("_sharedImportState.TryAcquire()", methode);
        Assert.Contains("_sharedImportState.Release();", methode);
        Assert.Contains("finally", methode);
    }

    [Fact]
    public void Mehrere_Importseiten_teilen_die_Sperre_ohne_starke_globale_Referenzen()
    {
        var source = ImportPageSource();

        Assert.Contains(
            "ConditionalWeakTable<ShellViewModel, SharedImportOperationState>",
            source);
        Assert.Contains("WeakReference<ImportPageViewModel>", source);
        Assert.Contains("SharedImportOperationState : IShellOperationGuard", source);
        Assert.Contains("_shell.RegisterShellOperationGuard(_sharedImportState);", source);
    }

    [Fact]
    public void Fensterschliessen_verwendet_den_gemeinsamen_Shell_Schutz()
    {
        var source = File.ReadAllText(RepoFile(
            "src", "AuswertungPro.Next.UI", "MainWindow.xaml.cs"));

        Assert.Contains("closeVm.ConfirmLeaveCurrentContext()", source);
    }

    [Fact]
    public void Importabschluesse_verwenden_nur_den_gebundenen_internen_Speicherweg()
    {
        var source = ImportPageSource();

        Assert.Contains("CreateActiveImportProjectSaveDelegate", source);
        Assert.DoesNotContain("_shell.TrySaveProject", source);
        Assert.Equal(6, CountOccurrences(source, "SaveProject: _saveProjectForActiveImport"));
    }

    [Fact]
    public void Sperre_meldet_den_wechsel_an_alle_Projektbefehle()
    {
        var source = ImportPageSource();

        var start = source.IndexOf(
            "partial void OnIsImportInProgressChanged(bool value)", StringComparison.Ordinal);
        Assert.True(start >= 0, "OnIsImportInProgressChanged nicht gefunden.");
        var ende = source.IndexOf("partial void OnCanCancelChanged", StringComparison.Ordinal);
        Assert.True(ende > start, "Ende von OnIsImportInProgressChanged nicht gefunden.");

        var methode = source[start..ende];
        Assert.Contains("MakeProjectPortableCommand.NotifyCanExecuteChanged();", methode);
        Assert.Contains("AssignPhotosFromFolderCommand.NotifyCanExecuteChanged();", methode);
        Assert.Contains("ImportKanalProjektCommand.NotifyCanExecuteChanged();", methode);
        Assert.Contains("ProtokollNeuGenerierenCommand.NotifyCanExecuteChanged();", methode);
    }

    [Theory]
    [InlineData("private Task ImportSchachtPdfsFolderAsync()")]
    [InlineData("private Task MakeProjectPortableAsync()")]
    [InlineData("private Task AssignPhotosFromFolderAsync()")]
    [InlineData("private Task ProtokollNeuGenerierenAsync()")]
    [InlineData("private async Task ImportKanalProjektAsync()")]
    public void Schreibende_Projektaktion_verwendet_den_gemeinsamen_Ablauf(string signatur)
    {
        var source = ImportPageSource();
        var start = source.IndexOf(signatur, StringComparison.Ordinal);
        Assert.True(start >= 0, $"{signatur} nicht gefunden.");

        var ausschnitt = source.Substring(start, Math.Min(1_200, source.Length - start));
        Assert.Contains("RunWithSharedImportLockAsync", ausschnitt);
    }

    private static int CountOccurrences(string source, string token)
    {
        var count = 0;
        var index = 0;
        while ((index = source.IndexOf(token, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += token.Length;
        }

        return count;
    }
}
