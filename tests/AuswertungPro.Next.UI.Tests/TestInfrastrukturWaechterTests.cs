using System.IO;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Wächter für die Test-Infrastruktur: Schnelllauf ohne Kindprozesse und die Layout-Regel nach <c>Show()</c>.
/// </summary>
public sealed class TestInfrastrukturWaechterTests
{
    private const string KindprozessTrait = "[Trait(TestKategorie.Name, TestKategorie.Kindprozess)]";

    /// <summary>
    /// Sperrklinke (Stand 30.09.2026): Testdateien, die noch <c>ApplicationIdle</c> erwähnen. Eine neue Datei
    /// damit ist rot (stattdessen <c>WpfTestHilfe.WarteAufLayout</c> verwenden). Eine Datei, die ihn nicht mehr
    /// enthält, muss aus der Liste. Bestehende Aufrufe bleiben unverändert.
    /// </summary>
    private static readonly HashSet<string> DateienMitApplicationIdle = new(StringComparer.Ordinal)
    {
        "AboutWindowIsolatedSmokeTests.cs",
        "AufklappLayoutBedienTests.cs",
        "BearbeitungErledigtUiTests.cs",
        "ListenErgaenzungWindowIsolatedSmokeTests.cs",
        "NovaDialogHeaderIsolatedSmokeTests.cs",
        "NovaDialogWindowIsolatedSmokeTests.cs",
        "NovaGrafikWiederladenTests.cs",
        "ObjektakteAufklappTests.cs",
        "ObjektakteUiTests.cs",
        "PlayerDispatcherSchedulerTests.cs",
        "SchachtTabellenAuswahlTests.cs",
        "VerteilenWindowIsolatedSmokeTests.cs",
        "WindowOpenCloseSmokeTests.cs",
        "WindowsThemeFollowServiceDispatchTests.cs",
        "WpfTestHilfe.cs",
        "XtfLieferungUiTests.cs"
    };

    [Fact]
    public void Test_classes_starting_a_child_process_carry_the_kindprozess_trait()
    {
        var offenders = TestDateien()
            .Where(datei => datei.Name != "WpfIsolatedTestProcess.cs")
            .Where(datei => File.ReadAllText(datei.FullName).Contains("WpfIsolatedTestProcess.RunAsync", StringComparison.Ordinal))
            .Where(datei => !File.ReadAllText(datei.FullName).Contains(KindprozessTrait, StringComparison.Ordinal))
            .Select(datei => datei.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            offenders.Length == 0,
            "Diese Testklassen starten einen Kindprozess, tragen aber " + KindprozessTrait
            + " nicht. Ohne das Trait läuft der Schnelllauf nicht ohne Kindprozesse:\n  "
            + string.Join("\n  ", offenders));
    }

    [Fact]
    public void Application_idle_in_tests_is_frozen_to_the_current_files()
    {
        var current = TestDateien()
            .Where(datei => datei.Name != nameof(TestInfrastrukturWaechterTests) + ".cs")
            .Where(datei => File.ReadAllText(datei.FullName).Contains("ApplicationIdle", StringComparison.Ordinal))
            .Select(datei => datei.Name)
            .ToHashSet(StringComparer.Ordinal);

        var neu = current.Except(DateienMitApplicationIdle).OrderBy(name => name, StringComparer.Ordinal).ToArray();
        var erledigt = DateienMitApplicationIdle.Except(current).OrderBy(name => name, StringComparer.Ordinal).ToArray();

        Assert.True(
            neu.Length == 0,
            "Neue Testdatei mit ApplicationIdle. Nach Show() im Kindprozess WpfTestHilfe.WarteAufLayout "
            + "(UpdateLayout) verwenden:\n  " + string.Join("\n  ", neu));
        Assert.True(
            erledigt.Length == 0,
            "Diese Dateien enthalten kein ApplicationIdle mehr. Aus DateienMitApplicationIdle entfernen, "
            + "damit die Liste dauerhaft sinkt:\n  " + string.Join("\n  ", erledigt));
    }

    private static IEnumerable<FileInfo> TestDateien()
    {
        var ordner = TestRepoPaths.RepoFile("tests", "AuswertungPro.Next.UI.Tests");
        var trenner = Path.DirectorySeparatorChar;
        return Directory.EnumerateFiles(ordner, "*.cs", SearchOption.AllDirectories)
            .Where(pfad => !pfad.Contains($"{trenner}obj{trenner}", StringComparison.OrdinalIgnoreCase))
            .Where(pfad => !pfad.Contains($"{trenner}bin{trenner}", StringComparison.OrdinalIgnoreCase))
            .Select(pfad => new FileInfo(pfad));
    }
}
