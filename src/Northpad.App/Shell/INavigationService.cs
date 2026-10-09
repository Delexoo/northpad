namespace Northpad.App.Shell;

public interface INavigationService
{
    void GoHome();

    void OpenSearch();

    void OpenSettings();

    void OpenModule(string moduleId, Guid? entityId = null);

    void LockWorkspace();

    void CompleteUnlock();
}
