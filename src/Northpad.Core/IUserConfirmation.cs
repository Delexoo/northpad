namespace Northpad.Core;

public interface IUserConfirmation
{
    bool Confirm(string title, string message);
}
