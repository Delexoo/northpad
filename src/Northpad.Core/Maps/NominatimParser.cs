using System.Globalization;
using System.Text.Json;

namespace Northpad.Core.Maps;

public static class NominatimParser
{
    public static bool TryRead(string json, out double latitude, out double longitude, out string label)
    {
        latitude = 0;
        longitude = 0;
        label = string.Empty;
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            foreach (var item in document.RootElement.EnumerateArray())
            {
                if (!item.TryGetProperty("lat", out var latText) || !item.TryGetProperty("lon", out var lonText))
                {
                    continue;
                }

                if (!double.TryParse(latText.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out latitude)
                    || !double.TryParse(lonText.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out longitude))
                {
                    continue;
                }

                label = item.TryGetProperty("display_name", out var name) ? name.GetString() ?? string.Empty : string.Empty;
                return true;
            }
        }
        catch (JsonException)
        {
            return false;
        }

        return false;
    }
}
