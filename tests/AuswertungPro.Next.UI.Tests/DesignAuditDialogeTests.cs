using System.IO;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Waechter zur Aufgabe 1 der Optikanalyse 28.09.2026 («Nova-Dialog statt Windows-MessageBox»):
/// Ausserhalb von <c>DialogService</c> und den <c>NovaDialog*</c>-Dateien darf im UI-Projekt kein
/// direkter <c>MessageBox.Show</c> mehr aufgerufen werden - jede Meldung/Rueckfrage geht ueber den
/// Nova-Dialog. Der Rueckfall auf die echte Windows-MessageBox bleibt bewusst in
/// <c>DialogService.AufUiThread</c> bestehen (Kommandozeilen-/Testumgebung ohne
/// <c>Application.Current</c>).
/// </summary>
public sealed class DesignAuditDialogeTests
{
    private static readonly string[] ErlaubteDateien =
    [
        Path.Combine("Services", "DialogService.cs"),
        Path.Combine("Views", "Windows", "NovaDialogWindow.xaml.cs"),
        Path.Combine("Views", "Windows", "NovaDialog.cs")
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
    public void Die_drei_erlaubten_Dateien_bestehen_wirklich_und_verwenden_MessageBox_oder_den_Nova_Dialog()
    {
        var uiRoot = RepoFile("src", "AuswertungPro.Next.UI");
        foreach (var relativ in ErlaubteDateien)
        {
            var pfad = Path.Combine(uiRoot, relativ);
            Assert.True(File.Exists(pfad), $"Erwartete Datei fehlt: {relativ}");
        }

        var dialogService = File.ReadAllText(Path.Combine(uiRoot, "Services", "DialogService.cs"));
        Assert.Contains("MessageBox.Show", dialogService);
        Assert.Contains("NovaDialog.", dialogService);

        var novaDialogFassade = File.ReadAllText(Path.Combine(uiRoot, "Views", "Windows", "NovaDialog.cs"));
        Assert.Contains("NovaDialogWindow", novaDialogFassade);
    }

    private static bool IstAusgeschlossenerOrdner(string datei, string uiRoot)
    {
        var relativ = Path.GetRelativePath(uiRoot, datei);
        var segmente = relativ.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return segmente.Contains("obj") || segmente.Contains("bin");
    }
}
