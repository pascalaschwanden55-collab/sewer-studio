using System;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using AuswertungPro.Next.Domain.Models;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests.ArchitectureTests;

/// <summary>
/// Architektur-Test fuer P2.1 Domain-INPC Entkopplung
/// (ADR docs/adrs/2026-05-10-p2-1-domain-inpc-decouple.md).
///
/// Aktuell:
///   HaltungRecord + SchachtRecord implementieren INotifyPropertyChanged.
///   Test ist mit [Skip(...)] markiert solange das so ist — er wird zum
///   Quality-Gate sobald Step 4 der Migration durch ist (INPC raus aus
///   Domain). Dann: Skip entfernen, Test ist scharf und schuetzt vor
///   Regression.
///
/// Warum gerade so:
///   - Test als Code-Review-Anker: jede Session sieht ihn und merkt,
///     dass die Migration nicht durch ist.
///   - Test-Logik ist heute schon korrekt (Reflection ueber Domain-
///     Namespace) — bei Removal der INPC-Implementierungen schaltet der
///     Test ohne Code-Anpassung scharf.
/// </summary>
public class DomainInpcTests
{
    /// <summary>
    /// Zaehlt alle exported Types in <c>AuswertungPro.Next.Domain</c>, die
    /// <see cref="INotifyPropertyChanged"/> implementieren.
    /// Erwartet: 0 (Domain-Layer ist UI-frei).
    /// </summary>
    [Fact(Skip = "P2.1 Migration noch nicht durch — siehe docs/adrs/2026-05-10-p2-1-domain-inpc-decouple.md. " +
                  "Skip entfernen sobald HaltungRecord + SchachtRecord ohne INotifyPropertyChanged sind.")]
    public void DomainTypes_DoNotImplementInpc()
    {
        var domainAssembly = typeof(HaltungRecord).Assembly;
        var inpcInDomain = domainAssembly.GetExportedTypes()
            .Where(t => typeof(INotifyPropertyChanged).IsAssignableFrom(t))
            .Where(t => !t.IsInterface)
            .Select(t => t.FullName)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(inpcInDomain);
    }

    /// <summary>
    /// Sanity-Check: Test laeuft ueberhaupt (Skip auf dem anderen
    /// Test verhindert, dass die Test-Datei vergessen wird).
    /// </summary>
    [Fact]
    public void DomainAssembly_LoadsAndContainsHaltungRecord()
    {
        var assembly = typeof(HaltungRecord).Assembly;
        Assert.NotNull(assembly);
        Assert.Contains(
            assembly.GetExportedTypes(),
            t => t == typeof(HaltungRecord));
    }
}
