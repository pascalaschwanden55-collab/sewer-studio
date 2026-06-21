using AuswertungPro.Next.Application.Common;

namespace AuswertungPro.Next.Infrastructure.Costs;

internal static class AtomicJsonFileWriter
{
    public static void WriteAllText(string path, string content)
        => AtomicTextFileWriter.WriteAllText(path, content);
}
