using System.Collections.ObjectModel;
using System.Globalization;
using System.Net.Http;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Northpad.Core.Maps;
using Northpad.Core.Storage;

namespace Northpad.Modules.Apps;

public sealed class MapTile
{
    public MapTile(int column, int row, ImageSource? image)
    {
        Column = column;
        Row = row;
        Image = image;
    }

    public int Column { get; }

    public int Row { get; }

    public ImageSource? Image { get; }
}

public partial class MapsViewModel : ObservableObject
{
    private const string UserAgent = "Northpad/0.1 (personal map client)";
    private readonly INetworkPreferences _network;
    private readonly HttpClient _http;
    private double _latitude = 20;
    private double _longitude;
    private int _zoom = 2;
    private int _generation;

    public MapsViewModel(INetworkPreferences network)
    {
        _network = network;
        var handler = new SocketsHttpHandler();
        _http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(12) };
        _http.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", UserAgent);
    }

    public ObservableCollection<MapTile> Tiles { get; } = [];

    [ObservableProperty]
    private string _place = string.Empty;

    [ObservableProperty]
    private string _status = "Map tiles stay off until you allow network. Northpad does not read your location. Tiles come from OpenStreetMap.";

    [ObservableProperty]
    private bool _networkAllowed;

    public void Initialize(Guid? entityId)
    {
        NetworkAllowed = _network.IsAllowed;
        if (NetworkAllowed)
        {
            _ = LoadTilesAsync();
        }
    }

    [RelayCommand]
    private void AllowNetwork()
    {
        _network.SetAllowed(true);
        NetworkAllowed = true;
        Status = "Tiles are requested from tile.openstreetmap.org. © OpenStreetMap contributors.";
        _ = LoadTilesAsync();
    }

    [RelayCommand]
    private Task PanLeft() => Move(-1, 0);

    [RelayCommand]
    private Task PanRight() => Move(1, 0);

    [RelayCommand]
    private Task PanUp() => Move(0, -1);

    [RelayCommand]
    private Task PanDown() => Move(0, 1);

    [RelayCommand]
    private Task ZoomIn()
    {
        _zoom = Math.Min(16, _zoom + 1);
        return LoadTilesAsync();
    }

    [RelayCommand]
    private Task ZoomOut()
    {
        _zoom = Math.Max(1, _zoom - 1);
        return LoadTilesAsync();
    }

    [RelayCommand]
    private async Task FindPlace()
    {
        if (!_network.IsAllowed)
        {
            Status = "Allow network before searching for a place.";
            return;
        }

        if (string.IsNullOrWhiteSpace(Place))
        {
            return;
        }

        try
        {
            var uri = new Uri("https://nominatim.openstreetmap.org/search?format=jsonv2&limit=1&q=" + Uri.EscapeDataString(Place.Trim()));
            using var response = await _http.GetAsync(uri);
            var json = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode || !NominatimParser.TryRead(json, out var latitude, out var longitude, out var label))
            {
                Status = "No place was found.";
                return;
            }

            _latitude = latitude;
            _longitude = longitude;
            _zoom = 12;
            Status = label + "  © OpenStreetMap contributors.";
            await LoadTilesAsync();
        }
        catch (Exception)
        {
            Status = "The place search could not be reached.";
        }
    }

    private Task Move(int dx, int dy)
    {
        var tile = SlippyMap.Tile(_latitude, _longitude, _zoom);
        var scale = 1 << _zoom;
        var x = ((tile.X + dx) % scale + scale) % scale;
        var y = Math.Clamp(tile.Y + dy, 0, scale - 1);
        var n = scale;
        _longitude = x / (double)n * 360d - 180d;
        var mercator = Math.PI * (1 - 2 * y / (double)n);
        _latitude = Math.Atan(Math.Sinh(mercator)) * 180d / Math.PI;
        return LoadTilesAsync();
    }

    private async Task LoadTilesAsync()
    {
        if (!_network.IsAllowed)
        {
            return;
        }

        var generation = ++_generation;
        var center = SlippyMap.Tile(_latitude, _longitude, _zoom);
        var slots = new List<(int Column, int Row, Uri Uri)>();
        for (var row = 0; row < 3; row++)
        {
            for (var column = 0; column < 3; column++)
            {
                slots.Add((column, row, SlippyMap.TileUri(_zoom, center.X + column - 1, center.Y + row - 1)));
            }
        }

        var tiles = new List<MapTile>();
        foreach (var slot in slots)
        {
            ImageSource? image = null;
            try
            {
                var bytes = await _http.GetByteArrayAsync(slot.Uri);
                image = Decode(bytes);
            }
            catch (Exception)
            {
                image = null;
            }

            tiles.Add(new MapTile(slot.Column, slot.Row, image));
        }

        if (generation != _generation)
        {
            return;
        }

        Tiles.Clear();
        foreach (var tile in tiles)
        {
            Tiles.Add(tile);
        }

        Status = "© OpenStreetMap contributors. Tiles from tile.openstreetmap.org.";
    }

    private static ImageSource? Decode(byte[] bytes)
    {
        using var stream = new System.IO.MemoryStream(bytes);
        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.StreamSource = stream;
        image.EndInit();
        image.Freeze();
        return image;
    }
}
