using System.ComponentModel;
using System.Linq;
using AuswertungPro.Next.Domain.Models;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

/// <summary>
/// Architektur-Tests fuer den Domain-Layer.
///
/// ADR-0004: Domain-Modelle implementieren aktuell INotifyPropertyChanged.
/// Das ist Tech-Debt (UI-Pattern in Domain-Schicht). Migrationspfad:
/// ViewModel-Wrapper komplettieren -> UI-Bindings umstellen ->
/// INotifyPropertyChanged aus Domain entfernen.
///
/// Dieser Test friert den heutigen Stand ein. Wenn die Migration fortschreitet,
/// muss die erwartete Liste angepasst werden — das macht jede Verschiebung
/// im Code-Review sichtbar.
/// </summary>
public sealed class DomainArchitectureTests
{
    [Fact]
    public void Domain_InotifyPropertyChanged_TechDebt_FrozenList_Adr0004()
    {
        // Welche Domain-Modelle implementieren INotifyPropertyChanged?
        var domainAssembly = typeof(HaltungRecord).Assembly;
        var inpcImplementers = domainAssembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && typeof(INotifyPropertyChanged).IsAssignableFrom(t))
            .Select(t => t.Name)
            .OrderBy(n => n, System.StringComparer.Ordinal)
            .ToArray();

        // ADR-0004: Aktuell akzeptiert. Wenn die Migration fortschreitet,
        // muss diese Liste schrumpfen — Test bewusst anpassen.
        var expected = new[] { "HaltungRecord", "SchachtRecord" };

        Assert.Equal(expected, inpcImplementers);
    }

    [Fact]
    public void Domain_HasNoWpfReference()
    {
        // Domain darf KEIN PresentationCore / PresentationFramework
        // referenzieren (das waere ein klarer Architektur-Bruch).
        // INotifyPropertyChanged ist in System.ComponentModel — das ist
        // ok (BCL), nicht WPF.
        var domainAssembly = typeof(HaltungRecord).Assembly;
        var refs = domainAssembly.GetReferencedAssemblies()
            .Select(a => a.Name)
            .ToArray();

        Assert.DoesNotContain("PresentationCore", refs);
        Assert.DoesNotContain("PresentationFramework", refs);
        Assert.DoesNotContain("WindowsBase", refs);
    }
}
