using System.Diagnostics.CodeAnalysis;
using AuswertungPro.Next.Application.Protocol;

namespace AuswertungPro.Next.UI.Ai.Training;

public static class TrainingProtocolStartdataCatalogController
{
    public const string MissingCatalogStatusText = "Kein Code-Katalog verfuegbar.";

    public static ICodeCatalogProvider? Resolve(
        ICodeCatalogProvider? injectedCatalog,
        Func<ICodeCatalogProvider?> fallbackCatalog)
    {
        ArgumentNullException.ThrowIfNull(fallbackCatalog);

        return injectedCatalog ?? fallbackCatalog();
    }

    public static bool EnsureAvailable(
        [NotNullWhen(true)] ICodeCatalogProvider? catalog,
        Action<Action> onUi,
        Action<string> setReviewStatusText)
    {
        ArgumentNullException.ThrowIfNull(onUi);
        ArgumentNullException.ThrowIfNull(setReviewStatusText);

        if (catalog is not null)
            return true;

        onUi(() => setReviewStatusText(MissingCatalogStatusText));
        return false;
    }
}
