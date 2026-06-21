using System.Globalization;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.UI.Ai;

public static class CodingLiveFindingCodeMetaWriter
{
    public static void ApplyToEntry(ProtocolEntry entry, string code, LiveFrameFinding finding)
    {
        if (!string.IsNullOrWhiteSpace(finding.PositionClock))
        {
            entry.CodeMeta ??= new ProtocolEntryCodeMeta { Code = code };
            entry.CodeMeta.Parameters["vsa.uhr.von"] = finding.PositionClock!;
        }

        if (finding.CrossSectionReductionPercent is > 0)
        {
            entry.CodeMeta ??= new ProtocolEntryCodeMeta { Code = code };
            entry.CodeMeta.Parameters["vsa.querschnitt.prozent"] =
                finding.CrossSectionReductionPercent.Value.ToString(CultureInfo.InvariantCulture);
        }
        else if (finding.IntrusionPercent is > 0)
        {
            entry.CodeMeta ??= new ProtocolEntryCodeMeta { Code = code };
            entry.CodeMeta.Parameters["vsa.querschnitt.prozent"] =
                finding.IntrusionPercent.Value.ToString(CultureInfo.InvariantCulture);
        }
    }
}
