using System;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace AuswertungPro.Next.Infrastructure.Ai.QualityGate;

internal static class QualityGateWeightModuleInitializer
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        // Test runners must stay deterministic and must never consume a developer's AppData DB.
        // The WPF application is the only entry assembly that performs automatic activation.
        var entryAssemblyName = Assembly.GetEntryAssembly()?.GetName().Name;
        if (!string.Equals(
                entryAssemblyName,
                "AuswertungPro.Next.UI",
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        QualityGateWeightBootstrapper.LoadAndActivate();
    }
}
