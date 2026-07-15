namespace AuswertungPro.Next.Application.Import;

/// <summary>
/// Findet das fuer den KINS-Seitensplit massgebliche Gesamtprotokoll.
/// </summary>
public interface IKinsGesamtprotokollLocator
{
    string? Finde(string sourceFolder);
}
