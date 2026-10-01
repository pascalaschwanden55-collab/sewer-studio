using System.Security.Cryptography;
using System.Text;

namespace AuswertungPro.Next.Application.Backup;

public static class BackupExternalPathKey
{
    public static string ForPath(string path)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar).ToUpperInvariant())));
}
