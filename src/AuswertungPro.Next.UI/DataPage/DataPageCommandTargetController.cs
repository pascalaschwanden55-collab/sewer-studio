using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.DataPage;

public static class DataPageCommandTargetController
{
    public static bool HasTarget(HaltungRecord? commandRecord, HaltungRecord? selected)
        => commandRecord is not null || selected is not null;
}
