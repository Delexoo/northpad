using System.Globalization;
using System.Windows.Threading;
using Northpad.Core;
using Northpad.Core.Modules;
using Northpad.Core.Storage;
using Northpad.Core.Vault;

namespace Northpad.Modules.Apps;

public sealed class ReminderMonitor : IDisposable
{
    private readonly IDocumentRepository _documents;
    private readonly IUserConfirmation _confirmation;
    private readonly IVaultService _vault;
    private readonly DispatcherTimer _timer;

    public ReminderMonitor(IDocumentRepository documents, IUserConfirmation confirmation, IVaultService vault)
    {
        _documents = documents;
        _confirmation = confirmation;
        _vault = vault;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
        _timer.Tick += (_, _) => Check();
    }

    public void Start()
    {
        if (!_vault.IsUnlocked)
        {
            return;
        }

        _timer.Start();
        Check();
    }

    public void Stop() => _timer.Stop();

    public void Dispose() => _timer.Stop();

    private void Check()
    {
        if (!_vault.IsUnlocked)
        {
            Stop();
            return;
        }

        try
        {
            var now = DateTime.Now;
            foreach (var item in _documents.List(DocumentKinds.Reminder))
            {
                var values = DocJson.Read(item.Payload);
                if (values.Get("done") == "true" || values.Get("notified") == "true")
                {
                    continue;
                }

                if (!DateTime.TryParseExact(values.Get("due"), "yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var due)
                    || due > now)
                {
                    continue;
                }

                values["notified"] = "true";
                _documents.Update(item.Id, DocJson.Write(values), item.SortKey);
                _confirmation.Notify("Reminder", string.IsNullOrWhiteSpace(values.Get("title")) ? "A reminder is due." : values.Get("title"));
                return;
            }
        }
        catch (Exception)
        {
            Stop();
        }
    }
}
