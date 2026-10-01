using AuswertungPro.Next.Application.UseCases.ProjektPruefung;
using AuswertungPro.Next.Infrastructure.Projects;

namespace AuswertungPro.Next.UI;

public sealed partial class ServiceProvider
{
    private IProjektPruefung? _projektPruefung;
    public IProjektPruefung ProjektPruefung => _projektPruefung ??= new ProjektPruefungService();
}
