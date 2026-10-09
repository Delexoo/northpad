using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Northpad.Core.Storage;

namespace Northpad.Modules.Apps;

public partial class BrowserViewModel : ObservableObject
{
    private readonly INetworkPreferences _network;

    public BrowserViewModel(INetworkPreferences network)
    {
        _network = network;
    }

    [ObservableProperty]
    private string _address = string.Empty;

    [ObservableProperty]
    private string _status = "Nothing loads until you enter an address and allow network. The page renderer is Windows WebView2. Sites you open can see that request.";

    [ObservableProperty]
    private bool _networkAllowed;

    public event Action<Uri>? NavigateRequested;

    public event Action? StartRequested;

    public void Initialize(Guid? entityId)
    {
        NetworkAllowed = _network.IsAllowed;
        StartRequested?.Invoke();
    }

    [RelayCommand]
    private void AllowNetwork()
    {
        _network.SetAllowed(true);
        NetworkAllowed = true;
        Status = "Network is allowed for pages you open.";
    }

    [RelayCommand]
    private void Go()
    {
        if (!_network.IsAllowed)
        {
            Status = "Allow network before opening a page.";
            return;
        }

        var text = Address.Trim();
        if (!text.Contains("://", StringComparison.Ordinal))
        {
            text = "https://" + text;
        }

        if (!Uri.TryCreate(text, UriKind.Absolute, out var uri) || !EndpointPolicy.IsHttpOrHttps(uri))
        {
            Status = "Enter an http or https address.";
            return;
        }

        NavigateRequested?.Invoke(uri);
        Status = uri.Host;
    }
}

public partial class WebSearchViewModel : ObservableObject
{
    private readonly INetworkPreferences _network;

    public WebSearchViewModel(INetworkPreferences network)
    {
        _network = network;
        Instance = network.SearchBase;
    }

    [ObservableProperty]
    private string _query = string.Empty;

    [ObservableProperty]
    private string _instance = string.Empty;

    [ObservableProperty]
    private string _status = "Search uses SearXNG, an open-source metasearch engine. It stays idle until you allow network and search.";

    [ObservableProperty]
    private bool _networkAllowed;

    public event Action<Uri>? NavigateRequested;

    public void Initialize(Guid? entityId)
    {
        NetworkAllowed = _network.IsAllowed;
        Instance = _network.SearchBase;
    }

    [RelayCommand]
    private void AllowNetwork()
    {
        _network.SetAllowed(true);
        NetworkAllowed = true;
        Status = "A search will be sent to the SearXNG server below. Northpad does not add an account.";
    }

    [RelayCommand]
    private void Search()
    {
        if (!_network.IsAllowed)
        {
            Status = "Allow network before searching.";
            return;
        }

        if (string.IsNullOrWhiteSpace(Query))
        {
            return;
        }

        try
        {
            _network.SetSearchBase(Instance);
        }
        catch (Exception)
        {
            Status = "Use an https SearXNG address.";
            return;
        }

        var root = new Uri(_network.SearchBase.TrimEnd('/') + "/");
        var target = new Uri(root, "search?q=" + Uri.EscapeDataString(Query.Trim()));
        NavigateRequested?.Invoke(target);
        Status = "Results are loaded from " + root.Host + ".";
    }
}

public partial class VideoViewModel : ObservableObject
{
    private readonly INetworkPreferences _network;

    public VideoViewModel(INetworkPreferences network)
    {
        _network = network;
        Template = network.VideoTemplate;
    }

    [ObservableProperty]
    private string _query = string.Empty;

    [ObservableProperty]
    private string _template = string.Empty;

    [ObservableProperty]
    private string _status = "YouTube plays through Piped, an open-source frontend. It does not use a Google account. Video data can still come from YouTube's servers.";

    [ObservableProperty]
    private bool _networkAllowed;

    public event Action<Uri>? NavigateRequested;

    public void Initialize(Guid? entityId)
    {
        NetworkAllowed = _network.IsAllowed;
        Template = _network.VideoTemplate;
    }

    [RelayCommand]
    private void AllowNetwork()
    {
        _network.SetAllowed(true);
        NetworkAllowed = true;
        Status = "Playback uses the frontend below. Northpad does not sign in to Google.";
    }

    [RelayCommand]
    private void Search()
    {
        if (!_network.IsAllowed)
        {
            Status = "Allow network before opening video.";
            return;
        }

        if (string.IsNullOrWhiteSpace(Query))
        {
            return;
        }

        try
        {
            _network.SetVideoTemplate(Template);
        }
        catch (Exception)
        {
            Status = "Use an https address containing {query}.";
            return;
        }

        var address = _network.VideoTemplate.Replace("{query}", Uri.EscapeDataString(Query.Trim()), StringComparison.Ordinal);
        if (!Uri.TryCreate(address, UriKind.Absolute, out var uri))
        {
            Status = "The video address is not valid.";
            return;
        }

        NavigateRequested?.Invoke(uri);
        Status = "Opened " + uri.Host + ". A player may also request video files from YouTube's servers.";
    }

    public bool AllowNavigation(Uri uri)
    {
        if (uri.Scheme != Uri.UriSchemeHttps)
        {
            return false;
        }

        var host = uri.IdnHost;
        if (host.Equals("youtube.com", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".youtube.com", StringComparison.OrdinalIgnoreCase)
            || host.Equals("youtu.be", StringComparison.OrdinalIgnoreCase)
            || host.Equals("google.com", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".google.com", StringComparison.OrdinalIgnoreCase)
            || host.Contains("accounts.google", StringComparison.OrdinalIgnoreCase))
        {
            return host.EndsWith(".googlevideo.com", StringComparison.OrdinalIgnoreCase)
                || host.Equals("googlevideo.com", StringComparison.OrdinalIgnoreCase);
        }

        return true;
    }
}

public static class WebPage
{
    public const string Start = """
        <!DOCTYPE html>
        <html><head><meta charset="utf-8"><title>northpad</title></head>
        <body style="margin:0;background:#111;color:#f4f4f2;font-family:Segoe UI,sans-serif">
        <main style="padding:64px 48px;max-width:640px">
        <p style="letter-spacing:.14em;text-transform:uppercase;font-size:12px;color:#aaa">northpad</p>
        <h1 style="font-weight:600;font-size:48px;letter-spacing:-.04em;margin:12px 0">Browser</h1>
        <p style="font-size:18px;line-height:1.45;color:#ccc">Enter an address above. This page does not contact anyone.</p>
        </main></body></html>
        """;
}
