namespace AuswertungPro.Next.UI.DataPage;

public static class DataPageRowNavigationController
{
    public const string InvalidMovePositionMessage = "Bitte eine gültige Zahl eingeben.";
    public const string MoveNotPossibleMessage = "Verschieben nicht möglich. Bitte Zeile auswählen.";
    public const string InvalidRowNumberMessage = "Bitte eine gültige Zeilennummer eingeben.";

    public static bool TryMoveToPosition(
        string? text,
        Func<int, bool> moveToPosition,
        Action<string, string> showInfo)
    {
        if (!int.TryParse((text ?? string.Empty).Trim(), out var position))
        {
            showInfo(InvalidMovePositionMessage, "Position");
            return false;
        }

        if (moveToPosition(position))
            return true;

        showInfo(MoveNotPossibleMessage, "Position");
        return false;
    }

    public static bool TryResolveRowIndex(
        string? text,
        int recordCount,
        Action<string, string> showInfo,
        out int rowIndex)
    {
        rowIndex = -1;
        if (!int.TryParse((text ?? string.Empty).Trim(), out var rowNumber) || rowNumber < 1)
        {
            showInfo(InvalidRowNumberMessage, "Gehe zu Zeile");
            return false;
        }

        var resolved = rowNumber - 1;
        if (resolved >= recordCount)
            resolved = recordCount - 1;
        if (resolved < 0)
            return false;

        rowIndex = resolved;
        return true;
    }
}
