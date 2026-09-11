namespace AuswertungPro.Next.Application.Backup;

public interface IBackupAdditionalFolders
{
    IReadOnlyList<string> Load();
    void Save(IEnumerable<string> folders);
}
