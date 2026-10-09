using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Northpad.Core;
using Northpad.Core.Modules;
using Northpad.Core.Storage;
using Northpad.Core.Vault;

namespace Northpad.Modules.Apps;

public partial class LibraryViewModel : ObservableObject
{
    private readonly IFileStore _files;
    private readonly IFilePicker _picker;
    private readonly IUserConfirmation _confirmation;
    private readonly string _kind;
    private readonly string _filter;
    private readonly bool _preview;

    public LibraryViewModel(
        IFileStore files,
        IFilePicker picker,
        IUserConfirmation confirmation,
        string kind,
        string title,
        string filter,
        bool preview)
    {
        _files = files;
        _picker = picker;
        _confirmation = confirmation;
        _kind = kind;
        _filter = filter;
        _preview = preview;
        Title = title;
    }

    public string Title { get; }

    public ObservableCollection<StoredFile> Items { get; } = [];

    [ObservableProperty]
    private StoredFile? _selected;

    [ObservableProperty]
    private ImageSource? _image;

    [ObservableProperty]
    private string _status = "Imported files are encrypted on this computer. Nothing is uploaded.";

    public void Initialize(Guid? entityId)
    {
        Reload();
        if (entityId is Guid id)
        {
            Selected = Items.FirstOrDefault(item => item.Id == id);
            LoadPreview();
        }
    }

    [RelayCommand]
    private void Import()
    {
        var path = _picker.PickOpen("Import", _filter);
        if (path is null)
        {
            return;
        }

        try
        {
            var stored = _files.Import(path, _kind);
            Items.Insert(0, stored);
            Selected = stored;
            LoadPreview();
            Status = "Imported";
        }
        catch (Exception exception) when (exception is StorageException or VaultLockedException or IOException)
        {
            Status = exception is StorageException ? exception.Message : "The file could not be imported.";
        }
    }

    [RelayCommand]
    private void Export()
    {
        if (Selected is null)
        {
            return;
        }

        var path = _picker.PickSave("Export a decrypted copy", Selected.Name, "All files|*.*");
        if (path is null)
        {
            return;
        }

        try
        {
            _files.Export(Selected.Id, path);
            Status = "Exported a decrypted copy to the folder you chose.";
        }
        catch (Exception exception) when (exception is StorageException or VaultLockedException or IOException)
        {
            Status = "The file could not be exported.";
        }
    }

    [RelayCommand]
    private void Delete()
    {
        if (Selected is null || !_confirmation.Confirm("Delete file", "Delete this encrypted file from this computer?"))
        {
            return;
        }

        _files.Delete(Selected.Id);
        Selected = null;
        Image = null;
        Reload();
        Status = "Deleted";
    }

    [RelayCommand]
    private void Choose(StoredFile? file)
    {
        Selected = file;
        LoadPreview();
    }

    private void Reload() => Replace(Items, _files.List(_kind));

    private void LoadPreview()
    {
        Image = null;
        if (!_preview || Selected is null)
        {
            return;
        }

        try
        {
            var bytes = _files.ReadBytes(Selected.Id);
            using var stream = new MemoryStream(bytes);
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.DecodePixelWidth = 960;
            image.StreamSource = stream;
            image.EndInit();
            image.Freeze();
            Image = image;
        }
        catch (Exception)
        {
            Status = "This picture could not be shown.";
        }
    }

    private static void Replace(ObservableCollection<StoredFile> target, IReadOnlyList<StoredFile> source)
    {
        target.Clear();
        foreach (var item in source)
        {
            target.Add(item);
        }
    }
}

public sealed class PhotosViewModel : LibraryViewModel
{
    public PhotosViewModel(IFileStore files, IFilePicker picker, IUserConfirmation confirmation)
        : base(files, picker, confirmation, FileKinds.Photo, "Photos", "Pictures|*.png;*.jpg;*.jpeg;*.gif;*.bmp", true)
    {
    }
}

public sealed class DriveViewModel : LibraryViewModel
{
    public DriveViewModel(IFileStore files, IFilePicker picker, IUserConfirmation confirmation)
        : base(files, picker, confirmation, FileKinds.Drive, "Drive", "All files|*.*", false)
    {
    }
}
