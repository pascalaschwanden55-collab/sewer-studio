using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// WPF-Vertrag: Fuer Collections mit EnableCollectionSynchronization muss JEDER
/// Zugriff — auch vom UI-Thread — unter dem gemeinsamen Lock laufen. Verstoss
/// fuehrt zum Crash "Index out of range" in ListCollectionView.ProcessCollectionChanged
/// (3x im App-Log vom 02.07.2026 beim Schacht-Loeschen). Import-Services und
/// VsaPageViewModel halten den Lock bereits; dieser Guard sichert die
/// SchaechtePage-Mutationen dauerhaft ab.
/// </summary>
public sealed class SchaechteCollectionSyncGuardTests
{
    [Fact]
    public void SchaechtePage_RecordMutationen_LaufenUnterCollectionLock()
    {
        var root = FindRepositoryRoot();
        var viewModelPath = Path.Combine(
            root, "src", "AuswertungPro.Next.UI", "ViewModels", "Pages", "SchaechtePageViewModel.cs");

        var lines = File.ReadAllLines(viewModelPath);
        var mutation = new Regex(@"Records\.(Add|Insert|RemoveAt|Remove|Move|Clear)\(");

        var verstoesse = new System.Collections.Generic.List<string>();
        for (var i = 0; i < lines.Length; i++)
        {
            if (!mutation.IsMatch(lines[i]))
                continue;

            // Innerhalb weniger Zeilen davor muss der gemeinsame Lock stehen.
            var fensterStart = Math.Max(0, i - 8);
            var abgesichert = Enumerable.Range(fensterStart, i - fensterStart)
                .Any(j => lines[j].Contains("lock (_shell.CollectionLock)"));

            if (!abgesichert)
                verstoesse.Add($"Zeile {i + 1}: {lines[i].Trim()}");
        }

        Assert.True(verstoesse.Count == 0,
            "Records-Mutationen ohne CollectionLock gefunden (WPF-Sync-Vertrag, Crash-Gefahr):\n"
            + string.Join("\n", verstoesse));
    }
}
