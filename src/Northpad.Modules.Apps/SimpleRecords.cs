using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Northpad.Core;
using Northpad.Core.Modules;
using Northpad.Core.Storage;

namespace Northpad.Modules.Apps;

public sealed class NamedRow
{
    public NamedRow(StoredDocument document, string title)
    {
        Document = document;
        Title = string.IsNullOrWhiteSpace(title) ? "Untitled" : title.Trim();
    }

    public StoredDocument Document { get; }

    public string Title { get; }
}

public partial class MailViewModel : ObservableObject, IFlushable
{
    private readonly IDocumentRepository _documents;
    private readonly IUserConfirmation _confirmation;
    private Guid? _editing;

    public MailViewModel(IDocumentRepository documents, IUserConfirmation confirmation)
    {
        _documents = documents;
        _confirmation = confirmation;
    }

    public ObservableCollection<NamedRow> Items { get; } = [];

    [ObservableProperty]
    private string _subject = string.Empty;

    [ObservableProperty]
    private string _to = string.Empty;

    [ObservableProperty]
    private string _body = string.Empty;

    [ObservableProperty]
    private string _status = "Mail stays on this computer. Northpad does not contact an email provider.";

    public void Initialize(Guid? entityId)
    {
        Reload();
        var match = entityId is Guid id ? Items.FirstOrDefault(item => item.Document.Id == id) : null;
        if (match is not null)
        {
            Edit(match);
        }
    }

    [RelayCommand]
    private void NewItem()
    {
        _editing = null;
        Subject = string.Empty;
        To = string.Empty;
        Body = string.Empty;
        Status = "New message";
    }

    [RelayCommand]
    private void Save()
    {
        var payload = DocJson.Write(new Dictionary<string, string>
        {
            ["subject"] = Subject ?? string.Empty,
            ["to"] = To ?? string.Empty,
            ["body"] = Body ?? string.Empty,
        });
        if (_editing is Guid id)
        {
            _documents.Update(id, payload, null);
        }
        else
        {
            var created = _documents.Create(DocumentKinds.Mail, payload, null);
            _editing = created.Id;
        }

        Status = "Saved";
        Reload();
    }

    [RelayCommand]
    private void Delete()
    {
        if (_editing is not Guid id || !_confirmation.Confirm("Delete message", "Delete this message from this computer?"))
        {
            return;
        }

        _documents.Delete(id);
        NewItem();
        Reload();
    }

    public void Edit(NamedRow row)
    {
        var values = DocJson.Read(row.Document.Payload);
        _editing = row.Document.Id;
        Subject = values.Get("subject");
        To = values.Get("to");
        Body = values.Get("body");
        Status = "Saved";
    }

    public void Flush()
    {
        if (_editing is null && string.IsNullOrWhiteSpace(Subject) && string.IsNullOrWhiteSpace(Body))
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(Subject) || !string.IsNullOrWhiteSpace(Body) || _editing is not null)
        {
            Save();
        }
    }

    private void Reload()
    {
        Items.Clear();
        foreach (var item in _documents.List(DocumentKinds.Mail))
        {
            Items.Add(new NamedRow(item, DocJson.Read(item.Payload).Get("subject")));
        }
    }
}

public partial class PasswordsViewModel : ObservableObject, IFlushable
{
    private readonly IDocumentRepository _documents;
    private readonly IUserConfirmation _confirmation;
    private Guid? _editing;

    public PasswordsViewModel(IDocumentRepository documents, IUserConfirmation confirmation)
    {
        _documents = documents;
        _confirmation = confirmation;
    }

    public ObservableCollection<NamedRow> Items { get; } = [];

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _username = string.Empty;

    [ObservableProperty]
    private string _secret = string.Empty;

    [ObservableProperty]
    private string _site = string.Empty;

    [ObservableProperty]
    private bool _showSecret;

    [ObservableProperty]
    private string _status = "Passwords stay in the vault. Northpad does not fill them into websites.";

    public void Initialize(Guid? entityId) => Reload();

    [RelayCommand]
    private void NewItem()
    {
        _editing = null;
        Name = string.Empty;
        Username = string.Empty;
        Secret = string.Empty;
        Site = string.Empty;
        ShowSecret = false;
    }

    [RelayCommand]
    private void Save()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            Status = "Add a name.";
            return;
        }

        var payload = DocJson.Write(new Dictionary<string, string>
        {
            ["name"] = Name.Trim(),
            ["username"] = Username ?? string.Empty,
            ["secret"] = Secret ?? string.Empty,
            ["site"] = Site ?? string.Empty,
        });
        if (_editing is Guid id)
        {
            _documents.Update(id, payload, null);
        }
        else
        {
            var created = _documents.Create(DocumentKinds.Password, payload, null);
            _editing = created.Id;
        }

        Status = "Saved";
        ShowSecret = false;
        Reload();
    }

    [RelayCommand]
    private void Delete()
    {
        if (_editing is not Guid id || !_confirmation.Confirm("Delete password", "Delete this password from this computer?"))
        {
            return;
        }

        _documents.Delete(id);
        NewItem();
        Reload();
    }

    [RelayCommand]
    private void ToggleSecret() => ShowSecret = !ShowSecret;

    [RelayCommand]
    private void CopySecret()
    {
        if (string.IsNullOrEmpty(Secret))
        {
            return;
        }

        System.Windows.Clipboard.SetText(Secret);
        Status = "Copied. It stays on the clipboard until you copy something else.";
    }

    public void Edit(NamedRow row)
    {
        var values = DocJson.Read(row.Document.Payload);
        _editing = row.Document.Id;
        Name = values.Get("name");
        Username = values.Get("username");
        Secret = values.Get("secret");
        Site = values.Get("site");
        ShowSecret = false;
        Status = "Saved";
    }

    public void Flush()
    {
        if (_editing is not null || !string.IsNullOrWhiteSpace(Name))
        {
            Save();
        }
    }

    private void Reload()
    {
        Items.Clear();
        foreach (var item in _documents.List(DocumentKinds.Password))
        {
            Items.Add(new NamedRow(item, DocJson.Read(item.Payload).Get("name")));
        }
    }
}

public partial class WalletViewModel : ObservableObject, IFlushable
{
    private readonly IDocumentRepository _documents;
    private readonly IUserConfirmation _confirmation;
    private Guid? _editing;

    public WalletViewModel(IDocumentRepository documents, IUserConfirmation confirmation)
    {
        _documents = documents;
        _confirmation = confirmation;
    }

    public ObservableCollection<NamedRow> Items { get; } = [];

    [ObservableProperty]
    private string _label = string.Empty;

    [ObservableProperty]
    private string _amount = string.Empty;

    [ObservableProperty]
    private string _note = string.Empty;

    [ObservableProperty]
    private string _total = "0.00";

    [ObservableProperty]
    private string _status = "A private ledger. Northpad does not store card numbers or connect to a bank.";

    public void Initialize(Guid? entityId)
    {
        Reload();
        if (entityId is Guid id)
        {
            var match = Items.FirstOrDefault(item => item.Document.Id == id);
            if (match is not null)
            {
                Edit(match);
            }
        }
    }

    [RelayCommand]
    private void NewItem()
    {
        _editing = null;
        Label = string.Empty;
        Amount = string.Empty;
        Note = string.Empty;
    }

    [RelayCommand]
    private void Save()
    {
        if (string.IsNullOrWhiteSpace(Label))
        {
            Status = "Add a label.";
            return;
        }

        if (!decimal.TryParse(Amount.Trim(), NumberStyles.Number, CultureInfo.CurrentCulture, out var parsed)
            && !decimal.TryParse(Amount.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out parsed))
        {
            Status = "Enter an amount like 12.50.";
            return;
        }

        var payload = DocJson.Write(new Dictionary<string, string>
        {
            ["label"] = Label.Trim(),
            ["amount"] = parsed.ToString("0.00", CultureInfo.InvariantCulture),
            ["note"] = Note ?? string.Empty,
        });
        var sort = DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        if (_editing is Guid id)
        {
            var existing = Items.FirstOrDefault(item => item.Document.Id == id);
            _documents.Update(id, payload, existing?.Document.SortKey ?? sort);
        }
        else
        {
            var created = _documents.Create(DocumentKinds.Wallet, payload, sort);
            _editing = created.Id;
        }

        Status = "Saved";
        Reload();
    }

    [RelayCommand]
    private void Delete()
    {
        if (_editing is not Guid id || !_confirmation.Confirm("Delete entry", "Delete this ledger entry from this computer?"))
        {
            return;
        }

        _documents.Delete(id);
        NewItem();
        Reload();
    }

    public void Edit(NamedRow row)
    {
        var values = DocJson.Read(row.Document.Payload);
        _editing = row.Document.Id;
        Label = values.Get("label");
        Amount = values.Get("amount");
        Note = values.Get("note");
    }

    public void Flush()
    {
        if (_editing is not null || !string.IsNullOrWhiteSpace(Label))
        {
            Save();
        }
    }

    private void Reload()
    {
        Items.Clear();
        decimal total = 0;
        foreach (var item in _documents.List(DocumentKinds.Wallet))
        {
            var values = DocJson.Read(item.Payload);
            Items.Add(new NamedRow(item, values.Get("label") + "  " + values.Get("amount")));
            if (decimal.TryParse(values.Get("amount"), NumberStyles.Number, CultureInfo.InvariantCulture, out var amount))
            {
                total += amount;
            }
        }

        Total = total.ToString("0.00", CultureInfo.CurrentCulture);
    }
}
