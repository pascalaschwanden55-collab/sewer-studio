using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.UI.Ai.Coding;

public static class CodingProtocolEntryCopier
{
    public static void CopyEditableValues(ProtocolEntry source, ProtocolEntry target)
    {
        target.Code = source.Code;
        target.Beschreibung = source.Beschreibung;
        target.MeterStart = source.MeterStart;
        target.MeterEnd = source.MeterEnd;
        target.IsStreckenschaden = source.IsStreckenschaden;
        target.Zeit = source.Zeit;
        target.CodeMeta = source.CodeMeta;
        target.FotoPaths = source.FotoPaths?.ToList() ?? new List<string>();
        target.OriginalFotoPaths = source.OriginalFotoPaths?.ToList() ?? new List<string>();
        target.Training = ProtocolEntryCloner.CloneTrainingMeta(source.Training);
    }

    public static void CopyValues(ProtocolEntry source, ProtocolEntry target)
    {
        target.Code = source.Code;
        target.Beschreibung = source.Beschreibung;
        target.MeterStart = source.MeterStart;
        target.MeterEnd = source.MeterEnd;
        target.IsStreckenschaden = source.IsStreckenschaden;
        target.Mpeg = source.Mpeg;
        target.Zeit = source.Zeit;
        target.Source = source.Source;
        target.CodeMeta = source.CodeMeta;
        target.Ai = source.Ai;
        target.FotoPaths = source.FotoPaths?.ToList() ?? new List<string>();
        target.OriginalFotoPaths = source.OriginalFotoPaths?.ToList() ?? new List<string>();
        target.Training = ProtocolEntryCloner.CloneTrainingMeta(source.Training);
    }
}
