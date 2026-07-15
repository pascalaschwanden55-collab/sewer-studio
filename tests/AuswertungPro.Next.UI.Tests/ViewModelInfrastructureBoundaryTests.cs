using System.IO;
using System.Text.RegularExpressions;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Boundary-Ratchet: ViewModels und Views sollen Persistenz-Stores/-Repositories
/// nicht direkt per <c>new</c> instanziieren, sondern per Interface injiziert
/// bekommen (Application-Portschicht statt konkreter Infrastruktur).
///
/// Die bekannten Alt-Stellen sind hier exakt eingefroren. Der Test erzwingt
/// Set-Gleichheit: jede NEUE direkte Store-Instanziierung schlaegt fehl, und
/// jede aufgeloeste Alt-Stelle MUSS aus der Allowlist entfernt werden (sonst
/// wird der Test rot). So kann die Schuld nur sinken, nie wachsen.
/// </summary>
public sealed class ViewModelInfrastructureBoundaryTests
{
    // Format je Eintrag: "<Pfad relativ zu src/AuswertungPro.Next.UI> :: <Typname>"
    private static readonly HashSet<string> ErlaubteAltStellen = new(StringComparer.Ordinal)
    {
        "ViewModels/Pages/DataPageViewModel.DropdownOptions.cs :: MeasureTemplateStore",
        "ViewModels/Pages/DataPageViewModel.cs :: ProjectCostStoreRepository",
        "ViewModels/Pages/DataPageViewModel.cs :: TrainingCenterStore",
        "ViewModels/Pages/OverviewPageViewModel.cs :: ProjectCostStore",
        "ViewModels/Pages/ProjectPageViewModel.cs :: FileDropdownOptionsStore",
        "ViewModels/Pages/SchaechtePageViewModel.cs :: FileDropdownOptionsStore",
        "ViewModels/Windows/CostCatalogEditorViewModel.cs :: CostCatalogStore",
        "ViewModels/Windows/MeasureTemplateEditorViewModel.cs :: CostCatalogStore",
        "ViewModels/Windows/MeasureTemplateEditorViewModel.cs :: MeasureTemplateStore",
        "ViewModels/Windows/PositionTemplateEditorViewModel.cs :: CostCatalogStore",
        "ViewModels/Windows/PositionTemplateEditorViewModel.cs :: PositionTemplateStore",
        "Views/Pages/Schachtansicht/SchachtMassnahmenDialogController.cs :: ProjectCostStoreRepository",
    };

    private static readonly Regex StoreNew =
        new(@"\bnew\s+(\w+(?:Store|Repository))\s*\(", RegexOptions.Compiled);

    [Fact]
    public void ViewModels_und_Views_instanziieren_keine_neuen_Stores_direkt()
    {
        var uiRoot = RepoFile("src", "AuswertungPro.Next.UI");
        var aktuell = new HashSet<string>(StringComparer.Ordinal);

        foreach (var unterordner in new[] { "ViewModels", "Views" })
        {
            var root = Path.Combine(uiRoot, unterordner);
            if (!Directory.Exists(root))
                continue;

            foreach (var pfad in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
            {
                var quelle = File.ReadAllText(pfad);
                var rel = Path.GetRelativePath(uiRoot, pfad).Replace('\\', '/');
                foreach (Match m in StoreNew.Matches(quelle))
                    aktuell.Add($"{rel} :: {m.Groups[1].Value}");
            }
        }

        var neueVerstoesse = aktuell.Except(ErlaubteAltStellen).OrderBy(x => x).ToArray();
        var aufgeloesteAberNochGelistet = ErlaubteAltStellen.Except(aktuell).OrderBy(x => x).ToArray();

        Assert.True(
            neueVerstoesse.Length == 0,
            "Neue direkte Store-Instanziierung in ViewModel/View gefunden. Bitte per Interface " +
            "injizieren (Application-Vertrag + ServiceProvider), nicht per new:\n  " +
            string.Join("\n  ", neueVerstoesse));

        Assert.True(
            aufgeloesteAberNochGelistet.Length == 0,
            "Diese Alt-Stellen wurden aufgeloest — bitte aus der Allowlist entfernen, damit die " +
            "Schuld messbar sinkt:\n  " + string.Join("\n  ", aufgeloesteAberNochGelistet));
    }
}
