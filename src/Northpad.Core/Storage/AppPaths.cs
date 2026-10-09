namespace Northpad.Core.Storage;

public sealed class AppPaths
{
    public AppPaths(string root)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        Root = root;
        DatabasePath = Path.Combine(root, "northpad.db");
        KeyPath = Path.Combine(root, "vault.key");
        LogDirectory = Path.Combine(root, "logs");
        LogPath = Path.Combine(LogDirectory, "northpad.log");
        FilesDirectory = Path.Combine(root, "files");
    }

    public string Root { get; }

    public string DatabasePath { get; }

    public string KeyPath { get; }

    public string LogDirectory { get; }

    public string LogPath { get; }

    public string FilesDirectory { get; }

    public static AppPaths ForDefaultLocation()
    {
        var root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Northpad");
        return new AppPaths(root);
    }

    public void EnsureDirectories()
    {
        Directory.CreateDirectory(Root);
        Directory.CreateDirectory(LogDirectory);
        Directory.CreateDirectory(FilesDirectory);
    }
}
