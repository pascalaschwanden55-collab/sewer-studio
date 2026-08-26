using System;
using System.Collections.Generic;
using System.Linq;

using AuswertungPro.Next.Application.Dossiers;

namespace AuswertungPro.Next.Application.Dossiers.Preview;

/// <summary>
/// Eine zusammenhaengende Klickflaeche auf der echten PDF-Seite. Die
/// Koordinaten bleiben in PDF-Punkten; erst der WPF-Renderer rechnet sie in
/// Bildschirmpixel um.
/// </summary>
public sealed record DossierOutputPreviewHitArea(
    DossierPreviewTarget Target,
    double Left,
    double Bottom,
    double Right,
    double Top);

/// <summary>
/// Verbindet die einzelnen PDF-Woerter eines Textes zu einer grossen,
/// verstaendlichen Klickflaeche. So muss nicht mehr exakt ein einzelner
/// Buchstabe getroffen werden; auch Zwischenraeume und mehrzeilige Zellen
/// gehoeren zur bearbeitbaren Stelle.
/// </summary>
public static class DossierOutputPreviewHitAreaBuilder
{
    private const double PaddingPoints = 4;
    private const double MinimumCellWidthPoints = 36;

    public static IReadOnlyList<DossierOutputPreviewHitArea> Build(
        DossierOutputPreviewPage page,
        IReadOnlyDictionary<int, IReadOnlyList<DossierPreviewTarget>> hits)
    {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(hits);

        var indicesByTarget = new Dictionary<DossierPreviewTarget, List<int>>();
        foreach (var (wordIndex, targets) in hits)
        {
            if (wordIndex < 0 || wordIndex >= page.Words.Count)
                continue;

            foreach (var target in targets.Distinct())
            {
                if (!indicesByTarget.TryGetValue(target, out var indices))
                    indicesByTarget[target] = indices = [];

                if (!indices.Contains(wordIndex))
                    indices.Add(wordIndex);
            }
        }

        var result = new List<DossierOutputPreviewHitArea>();
        foreach (var (target, indices) in indicesByTarget)
        {
            indices.Sort();
            foreach (var group in Groups(target, indices))
                result.Add(CreateArea(page, target, group));
        }

        return result;
    }

    private static IEnumerable<IReadOnlyList<int>> Groups(
        DossierPreviewTarget target,
        IReadOnlyList<int> indices)
    {
        if (indices.Count == 0)
            yield break;

        // Eine Tabellenzeile oder -zelle ist bereits eine eindeutige Adresse.
        // Alle ihre Woerter bilden deshalb gemeinsam genau eine Klickflaeche.
        if (target.Kind is DossierPreviewTargetKind.Row or DossierPreviewTargetKind.RowCell)
        {
            yield return indices;
            yield break;
        }

        var current = new List<int> { indices[0] };
        for (var index = 1; index < indices.Count; index++)
        {
            // Ein einzelnes ausgelassenes PDF-Wort ist meist nur ein getrennt
            // geliefertes Satzzeichen. Das soll keine Luecke im Klickbereich
            // erzeugen.
            if (indices[index] <= indices[index - 1] + 2)
            {
                current.Add(indices[index]);
                continue;
            }

            yield return current;
            current = [indices[index]];
        }

        yield return current;
    }

    private static DossierOutputPreviewHitArea CreateArea(
        DossierOutputPreviewPage page,
        DossierPreviewTarget target,
        IReadOnlyList<int> indices)
    {
        var words = indices.Select(index => page.Words[index]).ToList();
        var left = words.Min(word => word.Left) - PaddingPoints;
        var bottom = words.Min(word => word.Bottom) - PaddingPoints;
        var right = words.Max(word => word.Right) + PaddingPoints;
        var top = words.Max(word => word.Top) + PaddingPoints;

        if (target.Kind is DossierPreviewTargetKind.RowCell
            && right - left < MinimumCellWidthPoints)
        {
            var extra = (MinimumCellWidthPoints - (right - left)) / 2;
            left -= extra;
            right += extra;
        }

        return new DossierOutputPreviewHitArea(
            target,
            Math.Clamp(left, 0, page.Width),
            Math.Clamp(bottom, 0, page.Height),
            Math.Clamp(right, 0, page.Width),
            Math.Clamp(top, 0, page.Height));
    }
}
