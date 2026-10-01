using System.IO;

using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Der Formular-Editor darf die Regel nicht selbst nachbauen. Tabelle und
/// Formular muessen denselben Weg gehen, sonst driften sie auseinander — genau
/// das ist in diesem Programm schon einmal passiert (zwei Suchregeln fuer
/// dieselbe Datei).
///
/// Geprueft wird nur der Rumpf von CommitHaltungDetailField: Ein Treffer
/// irgendwo in der 1000-Zeilen-Datei wuerde nichts beweisen.
/// </summary>
public sealed class HaltungsnameFolgtSchaechtenGuardTests
{
    [Fact]
    public void Der_Formular_Editor_verwendet_dieselbe_Regel_wie_die_Tabelle()
    {
        var rumpf = MethodenRumpf("CommitHaltungDetailField");

        Assert.Contains(
            "DataPageCellEditController.ApplySchachtChange",
            rumpf,
            StringComparison.Ordinal);
        Assert.DoesNotContain("HoldingNameFromShafts", rumpf, StringComparison.Ordinal);
    }

    [Fact]
    public void Die_Schachtfeldnamen_stehen_nur_an_einer_Stelle()
    {
        var rumpf = MethodenRumpf("CommitHaltungDetailField");

        Assert.Contains(
            "DataPageCellEditController.SchachtObenFeld",
            rumpf,
            StringComparison.Ordinal);
        Assert.Contains(
            "DataPageCellEditController.SchachtUntenFeld",
            rumpf,
            StringComparison.Ordinal);
        Assert.DoesNotContain("\"Schacht_oben\"", rumpf, StringComparison.Ordinal);
        Assert.DoesNotContain("\"Schacht_unten\"", rumpf, StringComparison.Ordinal);
    }

    /// <summary>Der Rumpf einer Methode aus DataPage.xaml.cs, ueber Klammerzaehlung.</summary>
    private static string MethodenRumpf(string methodenName)
    {
        var pfad = Path.Combine(
            FindRepositoryRoot(),
            "src",
            "AuswertungPro.Next.UI",
            "Views",
            "Pages",
            "DataPage.xaml.cs");
        var quelle = File.ReadAllText(pfad);

        var start = quelle.IndexOf("void " + methodenName + "(", StringComparison.Ordinal);
        Assert.True(start >= 0, $"Die Methode {methodenName} wurde nicht gefunden.");

        var offen = quelle.IndexOf('{', start);
        Assert.True(offen >= 0, $"Kein Rumpf zu {methodenName} gefunden.");

        var tiefe = 0;
        for (var index = offen; index < quelle.Length; index++)
        {
            if (quelle[index] == '{')
                tiefe++;
            else if (quelle[index] == '}' && --tiefe == 0)
                return quelle[offen..(index + 1)];
        }

        Assert.Fail($"Der Rumpf von {methodenName} ist nicht geschlossen.");
        return string.Empty;
    }
}
