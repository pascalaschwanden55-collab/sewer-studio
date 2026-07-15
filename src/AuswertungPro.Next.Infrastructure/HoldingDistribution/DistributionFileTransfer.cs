using AuswertungPro.Next.Application.Import;

namespace AuswertungPro.Next.Infrastructure.HoldingDistribution;

/// <summary>
/// Kompatibilitaetsfassade; die Dateiuebertragung liegt im injizierbaren Instanzdienst.
/// </summary>
public static class DistributionFileTransfer
{
    private static IDistributionFileTransfer _current = new DistributionFileTransferService();

    public static IDistributionFileTransfer Current => Volatile.Read(ref _current);

    public static void Use(IDistributionFileTransfer transfer)
        => Volatile.Write(
            ref _current,
            transfer ?? throw new ArgumentNullException(nameof(transfer)));

    public static string EnsureUniquePath(string path, bool overwrite)
        => Current.EnsureUniquePath(path, overwrite);

    public static void MoveOrCopy(string source, string destination, bool move, bool overwrite)
        => Current.MoveOrCopy(source, destination, move, overwrite);
}
