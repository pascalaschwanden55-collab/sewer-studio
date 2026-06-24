using System;
using System.Windows.Documents;

namespace AuswertungPro.Next.UI.Ai;

public static class CodingImportReferenceControls
{
    public static void SetCount(Run countRun, int count)
    {
        ArgumentNullException.ThrowIfNull(countRun);

        countRun.Text = count.ToString();
    }
}
