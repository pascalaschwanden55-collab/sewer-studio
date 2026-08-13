using AuswertungPro.Next.Application.Import;
using AuswertungPro.Next.Infrastructure.Import.Protocols;

namespace AuswertungPro.Next.UI.Services;

/// <summary>
/// Uebergangsfassade fuer Aufrufer ohne injizierten Dienst (aeltere Konstruktoren,
/// Tests). Neue Aufrufer erhalten den Locator ueber den ServiceProvider.
/// </summary>
internal static class SchachtProtocolFileCompatibility
{
    internal static ISchachtProtocolFileLocator Default { get; } = new SchachtProtocolFileLocator();
}
