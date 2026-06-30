using System.Windows.Input;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.DataPage;

public static class DataPageRecordCommandRouter
{
    public const string MissingSelectionMessage =
        "Keine Zeile erkannt. Bitte direkt auf eine Zeile rechtsklicken oder zuerst eine Zeile auswaehlen.";

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
}
