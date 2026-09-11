using AuswertungPro.Next.Application.Projects;
using AuswertungPro.Next.Application.UseCases.Objektakten;
using AuswertungPro.Next.Infrastructure.Objektakten;
using AuswertungPro.Next.Infrastructure.Projects;

namespace AuswertungPro.Next.UI;

public sealed partial class ServiceProvider
{
    private IObjektaktenPaketService? _objektaktenPakete;
    public IObjektaktenPaketService ObjektaktenPakete => _objektaktenPakete ??= new ObjektaktenPaketService();

    private IObjektaktenListenErgaenzungen? _objektaktenListenErgaenzungen;
    /// <summary>Eigene Listeneintraege und Korrekturen, programmweit in AppData - wie die
    /// zusaetzlichen Sicherungsordner.</summary>
    public IObjektaktenListenErgaenzungen ObjektaktenListenErgaenzungen
        => _objektaktenListenErgaenzungen ??= new ObjektaktenListenErgaenzungenStore(AppSettings.AppDataDir);
}
