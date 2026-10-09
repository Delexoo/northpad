namespace Northpad.Core.Storage;

public interface INetworkPreferences
{
    bool IsAllowed { get; }

    string SearchBase { get; }

    string VideoTemplate { get; }

    string TranslateEndpoint { get; }

    void SetAllowed(bool allowed);

    void SetSearchBase(string value);

    void SetVideoTemplate(string value);

    void SetTranslateEndpoint(string value);
}

public sealed class NetworkPreferences : INetworkPreferences
{
    public const string DefaultSearchBase = "https://searx.be";
    public const string DefaultVideoTemplate = "https://piped.video/results?search_query={query}";

    private readonly ISettingsStore _settings;

    public NetworkPreferences(ISettingsStore settings)
    {
        _settings = settings;
    }

    public bool IsAllowed => string.Equals(_settings.Get("network.allowed"), "1", StringComparison.Ordinal);

    public string SearchBase => HttpsOrDefault(_settings.Get("search.base"), DefaultSearchBase);

    public string VideoTemplate => HttpsOrDefault(_settings.Get("video.template"), DefaultVideoTemplate);

    public string TranslateEndpoint => _settings.Get("translate.endpoint")?.Trim() ?? string.Empty;

    public void SetAllowed(bool allowed) => _settings.Set("network.allowed", allowed ? "1" : "0");

    public void SetSearchBase(string value) => _settings.Set("search.base", RequireHttps(value));

    public void SetVideoTemplate(string value)
    {
        var trimmed = RequireHttps(value);
        if (!trimmed.Contains("{query}", StringComparison.Ordinal))
        {
            throw new Vault.VaultStateException("The video address needs the text {query} where the search goes.");
        }

        _settings.Set("video.template", trimmed);
    }

    public void SetTranslateEndpoint(string value)
    {
        var trimmed = value.Trim();
        if (trimmed.Length == 0)
        {
            _settings.Set("translate.endpoint", string.Empty);
            return;
        }

        if (!EndpointPolicy.IsAllowedTranslateEndpoint(trimmed))
        {
            throw new Vault.VaultStateException("Use your own LibreTranslate address. Northpad does not send text to Google, Microsoft, Apple, or DeepL.");
        }

        _settings.Set("translate.endpoint", trimmed.TrimEnd('/'));
    }

    private static string HttpsOrDefault(string? value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        return Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps
            ? value.Trim()
            : fallback;
    }

    private static string RequireHttps(string value)
    {
        var trimmed = value.Trim();
        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps || !string.IsNullOrEmpty(uri.UserInfo))
        {
            throw new Vault.VaultStateException("Use an https address.");
        }

        return trimmed;
    }
}

public static class EndpointPolicy
{
    private static readonly string[] BlockedHosts =
    [
        "google.com",
        "googleapis.com",
        "gstatic.com",
        "microsoft.com",
        "bing.com",
        "apple.com",
        "icloud.com",
        "deepl.com",
    ];

    public static bool IsAllowedTranslateEndpoint(string value)
    {
        if (!Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri) || !string.IsNullOrEmpty(uri.UserInfo))
        {
            return false;
        }

        if (IsLoopback(uri))
        {
            return uri.Scheme is "http" or "https";
        }

        if (uri.Scheme != Uri.UriSchemeHttps)
        {
            return false;
        }

        var host = uri.IdnHost;
        foreach (var blocked in BlockedHosts)
        {
            if (host.Equals(blocked, StringComparison.OrdinalIgnoreCase)
                || host.EndsWith("." + blocked, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    public static bool IsHttpOrHttps(Uri uri) =>
        uri.Scheme is "http" or "https" && string.IsNullOrEmpty(uri.UserInfo);

    private static bool IsLoopback(Uri uri) =>
        uri.IsLoopback || uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase);
}
