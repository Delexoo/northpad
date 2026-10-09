using System.Windows;
using System.Windows.Controls;
using Microsoft.Web.WebView2.Core;
using Northpad.Core.Storage;

namespace Northpad.Modules.Apps;

public partial class PrivateWeb : UserControl
{
    private bool _ready;
    private Func<Uri, bool>? _allow;

    public PrivateWeb()
    {
        InitializeComponent();
    }

    public async Task<bool> EnsureReady()
    {
        if (_ready)
        {
            return true;
        }

        try
        {
            await Web.EnsureCoreWebView2Async();
            var settings = Web.CoreWebView2.Settings;
            settings.IsPasswordAutosaveEnabled = false;
            settings.IsGeneralAutofillEnabled = false;
            settings.IsSwipeNavigationEnabled = false;
            settings.AreDevToolsEnabled = false;
            Web.CoreWebView2.Profile.PreferredTrackingPreventionLevel = CoreWebView2TrackingPreventionLevel.Strict;
            Web.CoreWebView2.NavigationStarting += OnNavigationStarting;
            _ready = true;
            Fallback.Visibility = Visibility.Collapsed;
            return true;
        }
        catch (Exception)
        {
            Fallback.Text = "The browser, search, and video pages need the Windows WebView2 runtime. The other Northpad tools do not.";
            Fallback.Visibility = Visibility.Visible;
            Web.Visibility = Visibility.Collapsed;
            return false;
        }
    }

    public void UseAllowance(Func<Uri, bool>? allow) => _allow = allow;

    public async Task ShowHtml(string html)
    {
        if (!await EnsureReady())
        {
            return;
        }

        Web.NavigateToString(html);
    }

    public async Task Show(Uri uri)
    {
        if (!await EnsureReady())
        {
            return;
        }

        Web.CoreWebView2.Navigate(uri.AbsoluteUri);
    }

    private void OnNavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs args)
    {
        if (!Uri.TryCreate(args.Uri, UriKind.Absolute, out var uri) || !EndpointPolicy.IsHttpOrHttps(uri))
        {
            args.Cancel = true;
            return;
        }

        if (_allow is not null && !_allow(uri))
        {
            args.Cancel = true;
        }
    }
}
