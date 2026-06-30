using System.Windows.Input;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.DataPage;

public static class DataPageRecordCommandRouter
{
    public const string MissingSelectionMessage =
        "Keine Zeile erkannt. Bitte direkt auf eine Zeile rechtsklicken oder zuerst eine Zeile auswaehlen.";
    public const string MissingPositionSelectionMessage =
        "Keine Zeile erkannt. Bitte zuerst eine Haltung auswaehlen.";

    public static bool TryExecute(
        HaltungRecord? record,
        ICommand command,
        Action<string, string> showInfo,
        string missingSelectionTitle)
    {
        if (record is null)
        {
            showInfo(MissingSelectionMessage, missingSelectionTitle);
            return false;
        }

        command.Execute(record);
        return true;
    }

    public static bool TrySelectAndExecute(
        HaltungRecord? record,
        Action<HaltungRecord> selectRecord,
        ICommand command,
        Action<string, string> showInfo,
        string missingSelectionTitle)
    {
        if (record is null)
        {
            showInfo(MissingPositionSelectionMessage, missingSelectionTitle);
            return false;
        }

        selectRecord(record);
        if (!command.CanExecute(null))
            return false;

        command.Execute(null);
        return true;
    }
}
