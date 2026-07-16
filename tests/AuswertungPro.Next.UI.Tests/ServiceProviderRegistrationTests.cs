using System.Reflection;
using AuswertungPro.Next.Application.Diagnostics;
using AuswertungPro.Next.Application.Projects;
using Microsoft.Extensions.Logging;

namespace AuswertungPro.Next.UI.Tests;

public sealed class ServiceProviderRegistrationTests
{
    [Fact]
    public void GetService_liefert_die_bereits_erzeugte_Instanz()
    {
        using var loggerFactory = LoggerFactory.Create(_ => { });
        var services = CreateServices(loggerFactory);

        Assert.Same(services.Projects, services.GetService(typeof(IProjectRepository)));
    }

    [Fact]
    public void GetService_wirft_bei_einem_unbekannten_Typ_sichtbar()
    {
        using var loggerFactory = LoggerFactory.Create(_ => { });
        var services = CreateServices(loggerFactory);

        var error = Assert.Throws<InvalidOperationException>(
            () => services.GetService(typeof(ServiceProviderRegistrationTests)));

        Assert.Contains(typeof(ServiceProviderRegistrationTests).FullName!, error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void GetService_wirft_bei_fehlendem_Typ_sichtbar()
    {
        using var loggerFactory = LoggerFactory.Create(_ => { });
        var services = CreateServices(loggerFactory);

        Assert.Throws<ArgumentNullException>(() => services.GetService(null!));
    }

    [Fact]
    public void Zentrale_Registrierung_enthaelt_alle_bisherigen_Dienste()
    {
        using var loggerFactory = LoggerFactory.Create(_ => { });
        var services = CreateServices(loggerFactory);
        var field = typeof(ServiceProvider).GetField(
            "_services",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(field);
        var registrations = Assert.IsAssignableFrom<IReadOnlyDictionary<Type, object>>(field!.GetValue(services));
        Assert.Equal(110, registrations.Count);
        Assert.All(registrations, registration =>
            Assert.Same(registration.Value, services.GetService(registration.Key)));
    }

    private static ServiceProvider CreateServices(ILoggerFactory loggerFactory)
        => new(
            new AppSettings { EnableRestorePoints = false },
            new DiagnosticsOptions(),
            loggerFactory.CreateLogger("test"),
            loggerFactory);
}
