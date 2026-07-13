using System.IO;
using System.Windows;
using AuswertungPro.Next.Application.Protocol;
using AuswertungPro.Next.UI.ViewModels.Windows;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CatalogSelectorViewModelTests
{
    [Fact]
    public void Discovery_filter_and_apply_use_only_injected_catalog_directory()
    {
        using var temp = new TempDirectory();
        var sectionPath = WriteCatalog(temp.Path, "section.xml", "SEC", "VSA 2019");
        var nodePath = WriteCatalog(temp.Path, "node.xml", "NOD", "VSA 2019");

        StaTestRunner.Run(() =>
        {
            var window = CreateTestWindow();
            var viewModel = new CatalogSelectorViewModel(
                window,
                currentCatalogPath: sectionPath,
                winCanCatalogDir: null,
                lastProjectPath: null,
                discovery: new WinCanCatalogDiscoveryService(),
                initialDirectories: [temp.Path]);

            Assert.Equal(2, viewModel.FilteredCatalogs.Count);
            Assert.Contains("CH", viewModel.CountryOptions);
            Assert.Contains("VSA 2019", viewModel.StandardOptions);
            Assert.Contains("section.xml", viewModel.CurrentCatalogInfo, StringComparison.Ordinal);

            viewModel.FilterObjectType = "NOD";
            var selected = Assert.Single(viewModel.FilteredCatalogs);
            Assert.Equal(nodePath, selected.FilePath);
            viewModel.SelectedCatalog = selected;
            Assert.True(viewModel.ApplyCommand.CanExecute(null));
            window.Loaded += (_, _) => viewModel.ApplyCommand.Execute(null);

            Assert.True(window.ShowDialog());
            Assert.Equal(nodePath, viewModel.ResultPath);
        });
    }

    private static string WriteCatalog(string directory, string fileName, string objectType, string standard)
    {
        var path = Path.Combine(directory, fileName);
        File.WriteAllText(
            path,
            $$"""
              <WCCat xmlns="CDLAB.WinCan.WinCanCatalog_2011-04-04_2">
                <CATALOG>
                  <CAT_BaseType>{{standard}}</CAT_BaseType>
                  <CAT_CustomType></CAT_CustomType>
                  <CAT_Country>CH</CAT_Country>
                  <CAT_Language>DEU</CAT_Language>
                  <CAT_ObjectType>{{objectType}}</CAT_ObjectType>
                  <CAT_Version>1</CAT_Version>
                </CATALOG>
              </WCCat>
              """);
        return path;
    }

    private static Window CreateTestWindow()
        => new()
        {
            ShowActivated = false,
            ShowInTaskbar = false,
            WindowStartupLocation = WindowStartupLocation.Manual,
            Left = -10_000,
            Top = -10_000,
            Width = 200,
            Height = 150
        };

    private sealed class TempDirectory : IDisposable
    {
        public TempDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "SewerStudio_CatalogSelector_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
                Directory.Delete(Path, recursive: true);
        }
    }
}
