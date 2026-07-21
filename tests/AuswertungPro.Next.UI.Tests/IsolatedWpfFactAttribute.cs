namespace AuswertungPro.Next.UI.Tests;

[AttributeUsage(AttributeTargets.Method)]
internal sealed class IsolatedWpfFactAttribute : FactAttribute
{
    public IsolatedWpfFactAttribute()
    {
        if (!WpfIsolatedTestProcess.IsChildProcess)
            Skip = "Dieses Szenario laeuft nur im isolierten WPF-Kindprozess.";
    }
}
