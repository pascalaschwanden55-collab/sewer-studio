using System.Collections.ObjectModel;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.UI.Collections;

namespace AuswertungPro.Next.UI.Ai.Training;

public static class TrainingSampleCollectionController
{
    public static void ReplaceWith(
        ObservableCollection<TrainingSample> samples,
        IEnumerable<TrainingSample> items)
    {
        ObservableCollectionContentController.ReplaceWith(samples, items);
    }

    public static void Append(
        ObservableCollection<TrainingSample> samples,
        IEnumerable<TrainingSample> items)
    {
        ObservableCollectionContentController.Append(samples, items);
    }

    public static void ReplaceOnUi(
        ObservableCollection<TrainingSample> samples,
        IEnumerable<TrainingSample> items,
        Action<Action> onUi)
    {
        ArgumentNullException.ThrowIfNull(onUi);

        onUi(() => ReplaceWith(samples, items));
    }
}
