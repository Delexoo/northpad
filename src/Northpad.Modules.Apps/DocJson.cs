using System.Text.Json;

namespace Northpad.Modules.Apps;

internal static class DocJson
{
    public static string Write(IReadOnlyDictionary<string, string> values) =>
        JsonSerializer.Serialize(values);

    public static Dictionary<string, string> Read(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    public static string Get(this Dictionary<string, string> values, string key) =>
        values.TryGetValue(key, out var value) ? value : string.Empty;
}
