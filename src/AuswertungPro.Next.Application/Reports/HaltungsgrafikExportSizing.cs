using System;

namespace AuswertungPro.Next.Application.Reports;

internal static class HaltungsgrafikExportSizing
{
    private const int BaseSvgHeight = 700;
    private const int DenseEntryThreshold = 28;
    private const int ExtraHeightPerEntry = 14;
    private const int MaxSvgHeight = 1100;

    public static int ChooseSvgHeight(int entryCount)
    {
        if (entryCount <= DenseEntryThreshold)
            return BaseSvgHeight;

        var extra = (entryCount - DenseEntryThreshold) * ExtraHeightPerEntry;
        return Math.Min(MaxSvgHeight, BaseSvgHeight + extra);
    }
}
