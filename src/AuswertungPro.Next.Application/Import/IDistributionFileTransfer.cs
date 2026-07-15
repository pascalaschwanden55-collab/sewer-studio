namespace AuswertungPro.Next.Application.Import;

/// <summary>
/// Stellt freie Zieldateinamen bereit und kopiert oder verschiebt Dateien
/// bei der manuellen Haltungs- und Schachtverteilung.
/// </summary>
public interface IDistributionFileTransfer
{
    string EnsureUniquePath(string path, bool overwrite);

    void MoveOrCopy(string source, string destination, bool move, bool overwrite);
}
