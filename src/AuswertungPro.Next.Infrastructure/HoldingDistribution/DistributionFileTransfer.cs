using AuswertungPro.Next.Application.Import;

namespace AuswertungPro.Next.Infrastructure.HoldingDistribution;

/// <summary>
/// Kompatibilitaetsfassade; die Dateiuebertragung liegt im injizierbaren Instanzdienst.
/// </summary>
public static class DistributionFileTransfer
{
    private static readonly IDistributionFileTransfer Default = new DistributionFileTransferService();

    public static IDistributionFileTransfer Current => Default;

    [Obsolete("Die Dateiuebertragungs-Fassade ist unveraenderbar. Abhaengigkeit direkt uebergeben.")]
    public static void Use(IDistributionFileTransfer transfer)
    {
        ArgumentNullException.ThrowIfNull(transfer);
        throw new NotSupportedException(
            "Die Dateiuebertragungs-Fassade kann nicht mehr global ersetzt werden.");
    }

    public static string EnsureUniquePath(string path, bool overwrite)
        => Current.EnsureUniquePath(path, overwrite);

    public static void MoveOrCopy(string source, string destination, bool move, bool overwrite)
        => Current.MoveOrCopy(source, destination, move, overwrite);
}
