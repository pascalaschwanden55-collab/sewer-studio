using System.IO;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Waechter zur Aufgabe 1 der Optikanalyse 28.09.2026 («Nova-Dialog statt Windows-MessageBox»):
/// Ausserhalb von <c>DialogService.cs</c> darf im UI-Projekt kein direkter <c>MessageBox.Show</c>
/// mehr aufgerufen werden - jede Meldung/Rueckfrage geht ueber den Nova-Dialog
/// (<c>Views/Windows/NovaDialog.cs</c>, <c>NovaDialogWindow.xaml(.cs)</c>). Der Rueckfall auf die
/// echte Windows-MessageBox bleibt bewusst in <c>DialogService.AufUiThread</c> bestehen
/// (Kommandozeilen-/Testumgebung ohne <c>Application.Current</c>). <c>NovaDialog.cs</c> und
/// <c>NovaDialogWindow.xaml.cs</c> rufen selbst kein <c>MessageBox.Show</c> auf - nur
/// <c>DialogService.cs</c> steht deshalb auf der Ausnahmeliste; die anderen beiden werden von der
/// Suche ohnehin nicht getroffen, das bleibt in
/// <see cref="Die_zwei_Dateien_bestehen_wirklich_und_verwenden_MessageBox_bzw_den_Nova_Dialog"/>
/// zusaetzlich belegt.
/// </summary>
public sealed class DesignAuditDialogeTests
{
    private static readonly string[] ErlaubteDateien =
    [
        Path.Combine("Services", "DialogService.cs")
    ];

    [Fact]
    public void Kein_MessageBox_Show_ausserhalb_des_Nova_Dialogs()
    {
        var uiRoot = RepoFile("src", "AuswertungPro.Next.UI");
        var verstoesse = new List<string>();

        foreach (var datei in Directory.EnumerateFiles(uiRoot, "*.cs", SearchOption.AllDirectories))
        {
            if (IstAusgeschlossenerOrdner(datei, uiRoot))
                continue;

            var relativ = Path.GetRelativePath(uiRoot, datei);
            if (ErlaubteDateien.Contains(relativ, StringComparer.OrdinalIgnoreCase))
                continue;

            var inhalt = File.ReadAllText(datei);
            if (inhalt.Contains("MessageBox.Show", StringComparison.Ordinal))
                verstoesse.Add(relativ);
        }

        Assert.True(
            verstoesse.Count == 0,
            "Direkter MessageBox.Show ausserhalb des Nova-Dialogs in: " + string.Join(", ", verstoesse));
    }

    [Fact]
    public void Die_zwei_Dateien_bestehen_wirklich_und_verwenden_MessageBox_bzw_den_Nova_Dialog()
    {
        var uiRoot = RepoFile("src", "AuswertungPro.Next.UI");
        var pflichtDateien = ErlaubteDateien
            .Append(Path.Combine("Views", "Windows", "NovaDialogWindow.xaml.cs"))
            .Append(Path.Combine("Views", "Windows", "NovaDialog.cs"));
        foreach (var relativ in pflichtDateien)
        {
            var pfad = Path.Combine(uiRoot, relativ);
            Assert.True(File.Exists(pfad), $"Erwartete Datei fehlt: {relativ}");
        }

        var dialogService = File.ReadAllText(Path.Combine(uiRoot, "Services", "DialogService.cs"));
        Assert.Contains("MessageBox.Show", dialogService);
        Assert.Contains("NovaDialog.", dialogService);

        // NovaDialog.cs/NovaDialogWindow.xaml.cs rufen selbst kein MessageBox.Show auf - sie
        // stehen deshalb bewusst NICHT auf der Ausnahmeliste oben und werden vom Waechter mit
        // durchsucht (bleiben aber unauffaellig, weil sie den String nirgends enthalten).
        var novaDialogFassade = File.ReadAllText(Path.Combine(uiRoot, "Views", "Windows", "NovaDialog.cs"));
        Assert.Contains("NovaDialogWindow", novaDialogFassade);
        Assert.DoesNotContain("MessageBox.Show", novaDialogFassade);

        var novaDialogWindow = File.ReadAllText(Path.Combine(uiRoot, "Views", "Windows", "NovaDialogWindow.xaml.cs"));
        Assert.DoesNotContain("MessageBox.Show", novaDialogWindow);
    }

    private static bool IstAusgeschlossenerOrdner(string datei, string uiRoot)
    {
        var relativ = Path.GetRelativePath(uiRoot, datei);
        var segmente = relativ.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return segmente.Contains("obj") || segmente.Contains("bin");
    }
}
