using System;

namespace AuswertungPro.Next.UI.Services;

public sealed class DashboardRefreshNotifier
{
    public event EventHandler? CostsChanged;

    public void NotifyCostsChanged()
        => CostsChanged?.Invoke(this, EventArgs.Empty);
}
