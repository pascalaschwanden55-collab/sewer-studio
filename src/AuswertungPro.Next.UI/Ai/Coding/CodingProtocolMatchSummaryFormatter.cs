using AuswertungPro.Next.Application.Ai.Evaluation;

namespace AuswertungPro.Next.UI.Ai.Coding;

public static class CodingProtocolMatchSummaryFormatter
{
    public static string Format(CodingMatchRouting? routing)
    {
        if (routing == null)
            return "Abgleich: noch nicht ausgefuehrt";

        var green = routing.Trainingskandidaten.Count;
        var yellow = routing.ReviewGelb.Count;
        var wrong = routing.FalscherCodeReview.Count;
        var missed = routing.Verpasst.Count;
        var extra = routing.Fehlalarm.Count;
        var hits = green + yellow;

        return
            $"Abgleich: {hits} Treffer ({green} gruen/{yellow} gelb) | " +
            $"{wrong} falscher Code | {missed} fehlen | {extra} extra | " +
            $"P {routing.Match.Precision:P0} R {routing.Match.Recall:P0}";
    }

    public static bool CanAcceptGreenMatches(CodingMatchRouting? routing)
        => routing?.Trainingskandidaten.Count > 0;
}
