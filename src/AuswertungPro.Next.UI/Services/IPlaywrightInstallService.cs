using System.Threading;
using System.Threading.Tasks;

namespace AuswertungPro.Next.UI;

public interface IPlaywrightInstallService
{
    bool IsChromiumInstalled();

    Task<PlaywrightInstallResult> InstallChromiumAsync(CancellationToken ct = default);
}
