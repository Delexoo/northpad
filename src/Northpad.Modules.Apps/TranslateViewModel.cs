using System.Collections.ObjectModel;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Northpad.Core;
using Northpad.Core.Modules;
using Northpad.Core.Storage;
using Northpad.Core.Translate;

namespace Northpad.Modules.Apps;

public partial class TranslateViewModel : ObservableObject
{
    private readonly IDocumentRepository _documents;
    private readonly INetworkPreferences _network;
    private readonly IUserConfirmation _confirmation;
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(20) };

    public TranslateViewModel(IDocumentRepository documents, INetworkPreferences network, IUserConfirmation confirmation)
    {
        _documents = documents;
        _network = network;
        _confirmation = confirmation;
        Endpoint = network.TranslateEndpoint;
    }

    public ObservableCollection<NamedRow> Glossary { get; } = [];

    [ObservableProperty]
    private string _sourceText = string.Empty;

    [ObservableProperty]
    private string _result = string.Empty;

    [ObservableProperty]
    private string _from = "en";

    [ObservableProperty]
    private string _to = "es";

    [ObservableProperty]
    private string _phrase = string.Empty;

    [ObservableProperty]
    private string _meaning = string.Empty;

    [ObservableProperty]
    private string _endpoint = string.Empty;

    [ObservableProperty]
    private string _status = "The glossary stays on this computer. Text is sent only if you add your own LibreTranslate address and allow network.";

    public void Initialize(Guid? entityId) => Reload();

    [RelayCommand]
    private void AddPhrase()
    {
        if (string.IsNullOrWhiteSpace(Phrase) || string.IsNullOrWhiteSpace(Meaning))
        {
            Status = "Add a phrase and its meaning.";
            return;
        }

        var payload = DocJson.Write(new Dictionary<string, string>
        {
            ["source"] = Phrase.Trim(),
            ["translation"] = Meaning.Trim(),
            ["from"] = From.Trim(),
            ["to"] = To.Trim(),
        });
        _documents.Create(DocumentKinds.Glossary, payload, null);
        Phrase = string.Empty;
        Meaning = string.Empty;
        Status = "Saved in the glossary";
        Reload();
    }

    [RelayCommand]
    private void DeletePhrase(NamedRow? row)
    {
        if (row is null || !_confirmation.Confirm("Delete phrase", "Delete this glossary entry from this computer?"))
        {
            return;
        }

        _documents.Delete(row.Document.Id);
        Reload();
    }

    [RelayCommand]
    private async Task Translate()
    {
        var match = Glossary.Select(row => DocJson.Read(row.Document.Payload))
            .FirstOrDefault(values =>
                values.Get("source").Equals(SourceText.Trim(), StringComparison.OrdinalIgnoreCase)
                && values.Get("from").Equals(From.Trim(), StringComparison.OrdinalIgnoreCase)
                && values.Get("to").Equals(To.Trim(), StringComparison.OrdinalIgnoreCase));
        if (match is not null)
        {
            Result = match.Get("translation");
            Status = "From your glossary";
            return;
        }

        if (string.IsNullOrWhiteSpace(Endpoint))
        {
            Result = string.Empty;
            Status = "No glossary match. Northpad did not send this text anywhere.";
            return;
        }

        if (!_network.IsAllowed)
        {
            Status = "Allow network tools in Settings before using your LibreTranslate server.";
            return;
        }

        try
        {
            _network.SetTranslateEndpoint(Endpoint);
        }
        catch (Exception)
        {
            Status = "That address is not a local or self-hosted LibreTranslate server Northpad will use.";
            return;
        }

        try
        {
            var body = JsonSerializer.Serialize(new
            {
                q = SourceText,
                source = string.IsNullOrWhiteSpace(From) ? "en" : From.Trim(),
                target = string.IsNullOrWhiteSpace(To) ? "es" : To.Trim(),
                format = "text",
            });
            using var response = await _http.PostAsync(
                new Uri(_network.TranslateEndpoint.TrimEnd('/') + "/translate"),
                new StringContent(body, Encoding.UTF8, "application/json"));
            var json = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode || !LibreTranslate.TryRead(json, out var translated))
            {
                Status = "The translation server did not return a translation.";
                return;
            }

            Result = translated;
            Status = "Translated by your server";
        }
        catch (Exception)
        {
            Status = "The translation server could not be reached.";
        }
    }

    private void Reload()
    {
        Glossary.Clear();
        foreach (var item in _documents.List(DocumentKinds.Glossary))
        {
            var values = DocJson.Read(item.Payload);
            Glossary.Add(new NamedRow(item, values.Get("source") + " → " + values.Get("translation")));
        }
    }
}
