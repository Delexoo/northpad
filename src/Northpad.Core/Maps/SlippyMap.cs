namespace Northpad.Core.Maps;

public static class SlippyMap
{
    public static (int X, int Y) Tile(double latitude, double longitude, int zoom)
    {
        zoom = Math.Clamp(zoom, 0, 18);
        var latitudeClamped = Math.Clamp(latitude, -85.05112878, 85.05112878);
        var longitudeWrapped = ((longitude + 180d) % 360d + 360d) % 360d - 180d;
        var scale = 1 << zoom;
        var x = (int)Math.Floor((longitudeWrapped + 180d) / 360d * scale);
        var latitudeRadians = latitudeClamped * Math.PI / 180d;
        var y = (int)Math.Floor((1d - Math.Log(Math.Tan(latitudeRadians) + 1d / Math.Cos(latitudeRadians)) / Math.PI) / 2d * scale);
        var limit = scale - 1;
        return (Math.Clamp(x, 0, limit), Math.Clamp(y, 0, limit));
    }

    public static Uri TileUri(int zoom, int x, int y)
    {
        var scale = 1 << Math.Clamp(zoom, 0, 18);
        var wrappedX = ((x % scale) + scale) % scale;
        var clampedY = Math.Clamp(y, 0, scale - 1);
        return new Uri($"https://tile.openstreetmap.org/{zoom}/{wrappedX}/{clampedY}.png");
    }
}
