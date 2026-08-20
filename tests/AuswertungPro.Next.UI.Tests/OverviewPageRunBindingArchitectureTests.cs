using System;
using System.IO;
using System.Text.RegularExpressions;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// WPF bindet <c>Run.Text</c> standardmaessig TwoWay. Zeigt ein Run damit eine rein
/// berechnete Eigenschaft an, stuerzt die Oberflaeche beim Aktualisieren ab:
/// "TwoWay- oder OneWayToSource-Bindungen funktionieren nicht mit der
/// schreibgeschuetzten Eigenschaft ..." (real passiert am 2026-08-20 mit
/// <c>SchaechteGesamt</c>).
///
/// Der Rauchtest, der das sonst faengt, laeuft nur im Kindprozess und ist
/// uebersprungen. Darum haelt dieser Waechter die Regel textlich fest.
/// </summary>
public sealed class OverviewPageRunBindingArchitectureTests
{
    [Fact]
    public void Run_Bindungen_im_Cockpit_sind_immer_OneWay()
    {
        var xaml = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(), "src", "AuswertungPro.Next.UI", "Views", "Pages", "OverviewPage.xaml"));

        var runBindings = Regex.Matches(xaml, @"<Run\s+Text=""\{Binding[^}]*\}""", RegexOptions.Singleline);

        Assert.NotEmpty(runBindings);
        foreach (Match match in runBindings)
        {
            Assert.True(
                match.Value.Contains("Mode=OneWay", StringComparison.Ordinal),
                $"Run-Bindung ohne Mode=OneWay gefunden — stuerzt bei nur lesbaren Eigenschaften ab:\n{match.Value}");
        }
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "src")) &&
                Directory.Exists(Path.Combine(directory.FullName, "tests")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Repository root not found.");
    }
}
