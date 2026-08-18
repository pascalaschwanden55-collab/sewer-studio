using System.IO;
using System.Text.RegularExpressions;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Boundary-Ratchet: ViewModels, Views und DataPage-Controller sollen Persistenz-Stores/-Repositories
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
    private static readonly string[] GeschuetzteUiBereiche = ["ViewModels", "Views", "DataPage"];

    // Format je Eintrag: "<Pfad relativ zu src/AuswertungPro.Next.UI> :: <Typname>"
    private static readonly HashSet<string> ErlaubteAltStellen = new(StringComparer.Ordinal)
    {
    };

    private static readonly HashSet<string> ErlaubteKompatibilitaetsFassaden = new(StringComparer.Ordinal)
    {
        "DataPage/DataPagePrintController.cs :: CostStoreCompatibility :: 1",
        "ViewModels/Pages/BuilderPageViewModel.cs :: CostStoreCompatibility :: 1",
        "ViewModels/Pages/ExportPageViewModel.cs :: CostStoreCompatibility :: 1",
        "ViewModels/Pages/OverviewPageViewModel.cs :: CostStoreCompatibility :: 2",
        "ViewModels/Pages/ProjectPageViewModel.cs :: DropdownOptionsCompatibility.Default :: 1",
        "ViewModels/Pages/SanierungsMatrixPageViewModel.Compatibility.cs :: CostStoreCompatibility :: 1",
        "ViewModels/Pages/SchachtSanierungsMatrixPageViewModel.cs :: CostStoreCompatibility :: 1",
        "ViewModels/Pages/SchaechtePageViewModel.cs :: CostStoreCompatibility :: 1",
        "ViewModels/Pages/SchaechtePageViewModel.cs :: DropdownOptionsCompatibility.Default :: 1",
        "ViewModels/Windows/CostCalculatorViewModel.cs :: CostStoreCompatibility :: 1",
        "ViewModels/Windows/CostCatalogEditorViewModel.cs :: CostStoreCompatibility :: 1",
        "ViewModels/Windows/MeasureTemplateEditorViewModel.cs :: CostStoreCompatibility :: 2",
        "ViewModels/Windows/PositionTemplateEditorViewModel.cs :: CostStoreCompatibility :: 2",
    };

    private const string VerboteneTypen =
        "CostCatalogStore|FileDropdownOptionsStore|MeasureTemplateStore|" +
        "PositionTemplateStore|ProjectCostStoreRepository|TrainingCenterStore";

    // Erfasst auch voll qualifizierte Namen wie new AuswertungPro.Next....ProjectCostStoreRepository(...).
    private static readonly Regex ExplizitesStoreNew = new(
        $@"\bnew\s+(?:global::)?(?:[A-Za-z_]\w*\.)*(?<type>{VerboteneTypen})\s*\(",
        RegexOptions.Compiled);

    // Erfasst die kurze C#-Schreibweise: CostCatalogStore _store = new();
    // sowie Properties: TrainingCenterStore Store { get; } = new();
    private static readonly Regex AbgeleitetesStoreNew = new(
        $@"\b(?<type>{VerboteneTypen})\s+[A-Za-z_]\w*\s*(?:\{{[^\r\n]*\}})?\s*=\s*new\s*\(",
        RegexOptions.Compiled);

    [Fact]
    public void Geschuetzte_UI_Ablaufe_instanziieren_keine_neuen_Stores_direkt()
    {
        var uiRoot = RepoFile("src", "AuswertungPro.Next.UI");
        var aktuell = new HashSet<string>(StringComparer.Ordinal);

        foreach (var unterordner in GeschuetzteUiBereiche)
        {
            var root = Path.Combine(uiRoot, unterordner);
            if (!Directory.Exists(root))
                continue;

            foreach (var pfad in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
            {
                var quelle = File.ReadAllText(pfad);
                var rel = Path.GetRelativePath(uiRoot, pfad).Replace('\\', '/');
                foreach (var regex in new[] { ExplizitesStoreNew, AbgeleitetesStoreNew })
                {
                    foreach (Match m in regex.Matches(quelle))
                        aktuell.Add($"{rel} :: {m.Groups["type"].Value}");
                }
            }
        }

        var neueVerstoesse = aktuell.Except(ErlaubteAltStellen).OrderBy(x => x).ToArray();
        var aufgeloesteAberNochGelistet = ErlaubteAltStellen.Except(aktuell).OrderBy(x => x).ToArray();

        Assert.True(
            neueVerstoesse.Length == 0,
            "Neue direkte Store-Instanziierung im geschuetzten UI-Ablauf gefunden. Bitte per Interface " +
            "injizieren (Application-Vertrag + ServiceProvider), nicht per new:\n  " +
            string.Join("\n  ", neueVerstoesse));

        Assert.True(
            aufgeloesteAberNochGelistet.Length == 0,
            "Diese Alt-Stellen wurden aufgeloest — bitte aus der Allowlist entfernen, damit die " +
            "Schuld messbar sinkt:\n  " + string.Join("\n  ", aufgeloesteAberNochGelistet));
    }

    [Fact]
    public void Kompatibilitaets_Fassaden_in_geschuetzten_UI_Ablaufen_duerfen_nur_sinken()
    {
        var uiRoot = RepoFile("src", "AuswertungPro.Next.UI");
        var aktuell = new HashSet<string>(StringComparer.Ordinal);
        var marker = new[]
        {
            "CostStoreCompatibility",
            "DropdownOptionsCompatibility.Default",
        };

        foreach (var unterordner in GeschuetzteUiBereiche)
        {
            var root = Path.Combine(uiRoot, unterordner);
            foreach (var pfad in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
            {
                var quelle = File.ReadAllText(pfad);
                var rel = Path.GetRelativePath(uiRoot, pfad).Replace('\\', '/');
                foreach (var eintrag in marker)
                {
                    var anzahl = Regex.Matches(quelle, Regex.Escape(eintrag)).Count;
                    if (anzahl > 0)
                        aktuell.Add($"{rel} :: {eintrag} :: {anzahl}");
                }
            }
        }

        var neueVerstoesse = aktuell.Except(ErlaubteKompatibilitaetsFassaden).OrderBy(x => x).ToArray();
        var aufgeloesteAberNochGelistet = ErlaubteKompatibilitaetsFassaden.Except(aktuell).OrderBy(x => x).ToArray();

        Assert.True(
            neueVerstoesse.Length == 0,
            "Neue Kompatibilitaets-Fassade im geschuetzten UI-Ablauf gefunden. Neue Aufrufer muessen " +
            "die Application-Vertraege injiziert bekommen:\n  " + string.Join("\n  ", neueVerstoesse));
        Assert.True(
            aufgeloesteAberNochGelistet.Length == 0,
            "Diese Kompatibilitaets-Fassaden wurden reduziert - bitte die Altliste nachziehen:\n  " +
            string.Join("\n  ", aufgeloesteAberNochGelistet));
    }
}
