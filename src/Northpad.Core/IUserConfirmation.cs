namespace Northpad.Core;

public interface IUserConfirmation
{
    bool Confirm(string title, string message);

    void Notify(string title, string message);
}

public interface IFilePicker
{
    string? PickOpen(string title, string filter);

    string? PickSave(string title, string suggestedFileName, string filter);

    string? PickFolder(string title);
}
